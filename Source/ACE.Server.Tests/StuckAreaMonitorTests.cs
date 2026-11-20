using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Server.Managers;
using ACE.Server.Network;

namespace ACE.Server.Tests
{
    [TestClass]
    public class StuckAreaMonitorTests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 20, 13, 45, 0, DateTimeKind.Utc);
        private static readonly TimeSpan Window = TimeSpan.FromMinutes(30);

        private static StuckAreaMonitor.StuckEvent E(int minutesAgo, uint id, string name)
            => new StuckAreaMonitor.StuckEvent(Now.AddMinutes(-minutesAgo), id, name);

        [TestMethod]
        public void FivePlayers_InTheWindow_Alerts_MostStuckFirst()
        {
            var events = new List<StuckAreaMonitor.StuckEvent>
            {
                E(20, 1, "Schneebly"), E(15, 1, "Schneebly"), E(10, 1, "Schneebly"),
                E(12, 2, "Jake"), E(5, 2, "Jake"),
                E(3, 3, "Spooky-Quack"), E(2, 4, "Meshak"), E(1, 5, "Hauntone"),
            };

            var players = StuckAreaMonitor.Evaluate(events, Now, Window, 5);

            Assert.IsNotNull(players);
            CollectionAssert.AreEqual(new[] { "Schneebly", "Jake", "Hauntone", "Meshak", "Spooky-Quack" }, players.Select(p => p.Name).ToArray());
            Assert.AreEqual(3, players[0].Count);
        }

        [TestMethod]
        public void FourPlayers_DoesNotAlert()
        {
            var events = new List<StuckAreaMonitor.StuckEvent> { E(5, 1, "A"), E(4, 2, "B"), E(3, 3, "C"), E(2, 4, "D") };
            Assert.IsNull(StuckAreaMonitor.Evaluate(events, Now, Window, 5));
        }

        [TestMethod]
        public void OnePlayerStuckOverAndOver_IsNotAnArea()
        {
            // a single looping client (like Shadowfell) is their own problem, not several players
            var events = Enumerable.Range(0, 40).Select(i => E(i % 29, 7, "Shadowfell")).ToList();
            Assert.IsNull(StuckAreaMonitor.Evaluate(events, Now, Window, 5));
        }

        [TestMethod]
        public void EventsOlderThanTheWindow_AreDropped_AndDoNotCount()
        {
            var events = new List<StuckAreaMonitor.StuckEvent> { E(45, 1, "A"), E(40, 2, "B"), E(5, 3, "C"), E(4, 4, "D"), E(3, 5, "E") };

            Assert.IsNull(StuckAreaMonitor.Evaluate(events, Now, Window, 5));
            Assert.AreEqual(3, events.Count);
        }

        [TestMethod]
        public void ZeroMinPlayers_IsOff()
        {
            var events = new List<StuckAreaMonitor.StuckEvent> { E(1, 1, "A") };
            Assert.IsNull(StuckAreaMonitor.Evaluate(events, Now, Window, 0));
        }

        [TestMethod]
        public void AlertMessage_NamesTheLandblock_AndEachPlayer_InPlainAscii()
        {
            var msg = StuckAreaMonitor.BuildAlertMessage(0x9722, null, new List<(string, int)> { ("Schneebly", 12), ("Jake", 9) }, 30);

            StringAssert.Contains(msg, "[STUCK AREA] 2 players");
            StringAssert.Contains(msg, "landblock 9722 in the last 30 min");
            StringAssert.Contains(msg, "Schneebly x12, Jake x9");
            Assert.IsTrue(msg.All(c => c < 128), "alert must be plain ASCII");
        }

        [TestMethod]
        public void AlertMessage_ShowsANonBaseVariation()
        {
            StringAssert.Contains(StuckAreaMonitor.BuildAlertMessage(0x00E5, 3, new List<(string, int)> { ("A", 1) }, 30), "landblock 00E5 (variation 3)");
            Assert.IsFalse(StuckAreaMonitor.BuildAlertMessage(0x00E5, 0, new List<(string, int)> { ("A", 1) }, 30).Contains("variation"));
        }

        [TestMethod]
        public void Check_StillHappening_RemindsOnlyAfterTheInterval()
        {
            var tenMin = TimeSpan.FromMinutes(10);
            var quiet = TimeSpan.FromMinutes(20);
            Assert.AreEqual(StuckAreaMonitor.CheckOutcome.None, StuckAreaMonitor.DecideCheck(Now.AddMinutes(-2), quiet, Now, Now.AddMinutes(-9), tenMin));
            Assert.AreEqual(StuckAreaMonitor.CheckOutcome.Reminder, StuckAreaMonitor.DecideCheck(Now.AddMinutes(-2), quiet, Now, Now.AddMinutes(-10), tenMin));
        }

        [TestMethod]
        public void Check_ClearsOnlyAfterTheQuietPeriod_EvenWithRemindersOff()
        {
            var quiet = TimeSpan.FromMinutes(20);
            // 2026-11-20: the player count dipped below 5 at 14:20 but players were stuck until 14:58 - not clear yet
            Assert.AreNotEqual(StuckAreaMonitor.CheckOutcome.Cleared, StuckAreaMonitor.DecideCheck(Now.AddMinutes(-19), quiet, Now, Now, TimeSpan.FromMinutes(10)));
            Assert.AreEqual(StuckAreaMonitor.CheckOutcome.Cleared, StuckAreaMonitor.DecideCheck(Now.AddMinutes(-20), quiet, Now, Now, TimeSpan.FromMinutes(10)));
            Assert.AreEqual(StuckAreaMonitor.CheckOutcome.Cleared, StuckAreaMonitor.DecideCheck(Now.AddMinutes(-20), quiet, Now, Now, TimeSpan.Zero));
        }

        [TestMethod]
        public void Check_RemindersOff_NeverReminds()
        {
            Assert.AreEqual(StuckAreaMonitor.CheckOutcome.None, StuckAreaMonitor.DecideCheck(Now.AddMinutes(-1), TimeSpan.FromMinutes(20), Now, Now.AddHours(-5), TimeSpan.Zero));
        }

        [TestMethod]
        public void OngoingAndClearedMessages_ArePlainAscii_AndSaySo()
        {
            var ongoing = StuckAreaMonitor.BuildAlertMessage(0x9722, null, new List<(string, int)> { ("Jake", 4) }, 30, ongoing: true);
            var cleared = StuckAreaMonitor.BuildClearedMessage(0x9722, null, 20, new DateTime(2026, 11, 20, 14, 58, 54, DateTimeKind.Utc));

            StringAssert.Contains(ongoing, "[STUCK AREA] Still happening: 1 players");
            Assert.AreEqual("[OK] [STUCK AREA] landblock 9722 has cleared: nobody stuck loading there for 20 min (last at 14:58 UTC).", cleared);
            Assert.IsTrue((ongoing + cleared).All(c => c < 128 && c != '\r'), "in-game text must be plain ASCII with no carriage returns");
        }
    }

    [TestClass]
    public class StuckAreaTrackerTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 20, 13, 40, 0, DateTimeKind.Utc);

        // 5 players / 30 min, Discord cooldown 60, reminders every 10, clear after 20 quiet
        private static readonly StuckAreaMonitor.Settings S = new StuckAreaMonitor.Settings(5,
            TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(20));

        private static List<StuckAreaMonitor.Post> Stuck(StuckAreaMonitor.Tracker t, double minute, uint id, ushort lb = 0x9722, StuckAreaMonitor.Settings? s = null)
            => t.OnStuck(lb, null, id, "P" + id, T0.AddMinutes(minute), s ?? S);

        private static List<StuckAreaMonitor.Post> CheckAt(StuckAreaMonitor.Tracker t, double minute, StuckAreaMonitor.Settings? s = null)
            => t.OnCheck(T0.AddMinutes(minute), s ?? S);

        /// <summary>Five players stuck one a minute from minute 0: the alert comes with the fifth.</summary>
        private static StuckAreaMonitor.Tracker StartEpisode(double startMinute, List<StuckAreaMonitor.Post> alertOut)
        {
            var t = new StuckAreaMonitor.Tracker();
            for (uint i = 1; i <= 5; i++)
                alertOut.AddRange(Stuck(t, startMinute + i - 1, i));
            return t;
        }

        [TestMethod]
        public void FullEpisode_AlertOnce_RemindersInGameOnly_ThenOneClear()
        {
            var alert = new List<StuckAreaMonitor.Post>();
            var t = StartEpisode(0, alert);

            Assert.AreEqual(1, alert.Count, "one alert, at the fifth player");
            Assert.AreEqual(StuckAreaMonitor.PostKind.Alert, alert[0].Kind);
            Assert.IsTrue(alert[0].ToStaff && alert[0].ToDiscord);

            // more stuck loads while active: no new alert
            Assert.AreEqual(0, Stuck(t, 6, 6).Count);
            Assert.AreEqual(0, Stuck(t, 12, 1).Count);

            Assert.AreEqual(0, CheckAt(t, 10).Count, "9 min since the alert: no reminder yet");
            var reminder = CheckAt(t, 14);
            Assert.AreEqual(1, reminder.Count);
            Assert.AreEqual(StuckAreaMonitor.PostKind.Reminder, reminder[0].Kind);
            Assert.IsTrue(reminder[0].ToStaff);
            Assert.IsFalse(reminder[0].ToDiscord, "reminders stay in game");

            // last stuck load at minute 12: not clear at 31, clear at 32
            Assert.IsFalse(CheckAt(t, 31).Any(p => p.Kind == StuckAreaMonitor.PostKind.Cleared));
            var clear = CheckAt(t, 32);
            Assert.AreEqual(StuckAreaMonitor.PostKind.Cleared, clear.Single().Kind);
            Assert.IsTrue(clear[0].ToStaff && clear[0].ToDiscord);
            Assert.IsFalse(t.HasActiveAreas);
            Assert.AreEqual(0, CheckAt(t, 60).Count, "nothing after the clear");
        }

        [TestMethod]
        public void SecondEpisodeInsideTheCooldown_InGameOnly_AndItsClearSkipsDiscordToo()
        {
            var first = new List<StuckAreaMonitor.Post>();
            var t = StartEpisode(0, first);
            CheckAt(t, 25);   // last stuck at 4: clears at 24+
            Assert.IsFalse(t.HasActiveAreas);

            // back at minute 30, inside the 60 min Discord cooldown
            var second = new List<StuckAreaMonitor.Post>();
            for (uint i = 11; i <= 15; i++)
                second.AddRange(Stuck(t, 30 + i - 11, i));
            Assert.AreEqual(1, second.Count);
            Assert.IsTrue(second[0].ToStaff);
            Assert.IsFalse(second[0].ToDiscord, "Discord already had an alert for this area within the cooldown");

            var clear = CheckAt(t, 60);
            Assert.AreEqual(StuckAreaMonitor.PostKind.Cleared, clear.Single().Kind);
            Assert.IsTrue(clear[0].ToStaff);
            Assert.IsFalse(clear[0].ToDiscord, "no Discord all-clear for an episode Discord never saw start");
        }

        [TestMethod]
        public void SwitchedOffMidEpisode_StopsWithoutAFalseAllClear()
        {
            var alert = new List<StuckAreaMonitor.Post>();
            var t = StartEpisode(0, alert);
            var off = new StuckAreaMonitor.Settings(0, S.Window, S.DiscordCooldown, S.StaffReminder, S.ClearQuiet);

            Assert.AreEqual(0, Stuck(t, 6, 6, s: off).Count);
            Assert.AreEqual(0, CheckAt(t, 40, off).Count, "no all-clear while switched off");
            Assert.IsFalse(t.HasActiveAreas);
        }

        [TestMethod]
        public void InGamePostsOff_StillLogsAndPostsToDiscord()
        {
            var noStaff = new StuckAreaMonitor.Settings(5, S.Window, S.DiscordCooldown, TimeSpan.Zero, S.ClearQuiet);
            var t = new StuckAreaMonitor.Tracker();
            var posts = new List<StuckAreaMonitor.Post>();
            for (uint i = 1; i <= 5; i++)
                posts.AddRange(Stuck(t, i, i, s: noStaff));

            Assert.IsFalse(posts.Single().ToStaff);
            Assert.IsTrue(posts[0].ToDiscord);
            Assert.AreEqual(0, CheckAt(t, 15, noStaff).Count, "no reminders");
        }

        [TestMethod]
        public void DifferentLandblocks_AreSeparateAreas()
        {
            var t = new StuckAreaMonitor.Tracker();
            var posts = new List<StuckAreaMonitor.Post>();
            for (uint i = 1; i <= 4; i++)
                posts.AddRange(Stuck(t, i, i, lb: 0x9722));
            posts.AddRange(Stuck(t, 5, 5, lb: 0xC6A9));
            Assert.AreEqual(0, posts.Count);
        }
    }

    [TestClass]
    public class SentObjectLogTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 20, 14, 33, 0, DateTimeKind.Utc);

        private static SentObjectLog.Entry O(int secs, uint guid, string name, int bytes = 500)
            => new SentObjectLog.Entry(T0.AddSeconds(secs), guid, name, 1000 + guid, bytes);

        [TestMethod]
        public void KeepsTheLastCapacityEntries_OldestFirst()
        {
            var log = new SentObjectLog();
            for (uint i = 0; i < SentObjectLog.Capacity + 10; i++)
                log.Add(O((int)i, i, "obj" + i));

            var snap = log.Snapshot();

            Assert.AreEqual(SentObjectLog.Capacity, snap.Count);
            Assert.AreEqual(10u, snap[0].Guid);
            Assert.AreEqual((uint)(SentObjectLog.Capacity + 9), snap[^1].Guid);
        }

        [TestMethod]
        public void Report_ListsWhatWasSentAroundAndAfterTheLastAck_AndTheLargest()
        {
            var sent = new List<SentObjectLog.Entry>
            {
                O(-30, 1, "Old Thing"),                 // before portal entry
                O(1, 2, "Door"), O(2, 3, "Big Statue", 9000),
                O(5, 4, "Town Crier"), O(6, 5, "Suspect Object", 4000),
            };

            var report = SentObjectLog.BuildStuckReport("Spooky-Quack", T0.AddSeconds(20), T0, T0.AddSeconds(5), 1234, 5321, sent);

            StringAssert.Contains(report, "[PORTAL STUCK DIAG] Spooky-Quack: client last acknowledged 15.0s ago (seq 1234), 5,321 packets unacknowledged.");
            StringAssert.Contains(report, "Objects sent since entering portal space: 4 (");
            StringAssert.Contains(report, "Largest: Big Statue (wcid 1003, 0x00000003, 8.8 KB)");
            StringAssert.Contains(report, "around and after the last acknowledgement (2, last 10): Town Crier");
            StringAssert.Contains(report, "Suspect Object");
            Assert.IsFalse(report.Contains("Old Thing"), report);
            Assert.IsTrue(report.All(c => c < 128), "log line must be plain ASCII");
        }

        [TestMethod]
        public void Report_ClientThatNeverAcknowledged()
        {
            var report = SentObjectLog.BuildStuckReport("X", T0.AddSeconds(20), T0, null, 0, 12, new List<SentObjectLog.Entry> { O(1, 2, "Door") });
            StringAssert.Contains(report, "client has not acknowledged any packet, 12 packets unacknowledged.");
        }

        [TestMethod]
        public void Report_MarksAFullLogAsMore()
        {
            var sent = Enumerable.Range(0, SentObjectLog.Capacity).Select(i => O(i + 1, (uint)i, "o")).ToList();
            var report = SentObjectLog.BuildStuckReport("X", T0.AddSeconds(500), T0, T0.AddSeconds(400), 1, 0, sent);
            StringAssert.Contains(report, $"Objects sent since entering portal space: {SentObjectLog.Capacity}+ (");
        }
    }
}
