using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ACE.Server.Managers;

namespace ACE.Server.Tests
{
    [TestClass]
    public class ServerStatusAnnouncerTests
    {
        [TestMethod]
        public void PreviousRunStillRunning_IsReportedAsCrash_WithLastSeenTime()
        {
            var lastSeen = new DateTime(2026, 11, 16, 8, 1, 0, DateTimeKind.Utc);
            var msg = ServerStatusAnnouncer.BuildOnlineMessage(ServerStatusAnnouncer.StateRunning, lastSeen, worldOpen: true);

            StringAssert.Contains(msg, "back online");
            StringAssert.Contains(msg, "unexpected stop");
            StringAssert.Contains(msg, "<t:" + new DateTimeOffset(lastSeen).ToUnixTimeSeconds() + ":t>");
        }

        [TestMethod]
        [DataRow(ServerStatusAnnouncer.StateClean)]
        [DataRow(ServerStatusAnnouncer.StateStopped)]
        [DataRow(null)]
        public void CleanStoppedOrUnknownPreviousRun_IsPlainOnline(string previous)
        {
            var msg = ServerStatusAnnouncer.BuildOnlineMessage(previous, DateTime.UtcNow, worldOpen: true);

            StringAssert.Contains(msg, "Server is online");
            Assert.IsFalse(msg.Contains("unexpected"), msg);
        }

        [TestMethod]
        public void ClosedWorld_IsMentioned()
        {
            StringAssert.Contains(ServerStatusAnnouncer.BuildOnlineMessage(null, null, worldOpen: false), "closed to players");
        }

        [TestMethod]
        public void RestartScheduled_UsesMinutesAndSaysPlanned()
        {
            var now = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
            var msg = ServerStatusAnnouncer.BuildRestartScheduledMessage(1800, now);

            StringAssert.Contains(msg, "30 minutes");
            StringAssert.Contains(msg, "<t:" + new DateTimeOffset(now.AddMinutes(30)).ToUnixTimeSeconds() + ":t>");
            StringAssert.Contains(msg, "not a crash");
        }

        [TestMethod]
        public void RestartScheduled_ShortIntervalInSeconds()
        {
            StringAssert.Contains(ServerStatusAnnouncer.BuildRestartScheduledMessage(60, DateTime.UtcNow), "60 seconds");
        }

        [TestMethod]
        public void ParseState_ReadsStateAndUtcTime()
        {
            var (state, utc) = ServerStatusAnnouncer.ParseState("running 2026-11-16T08:01:04.0000000Z");

            Assert.AreEqual("running", state);
            Assert.AreEqual(new DateTime(2026, 11, 16, 8, 1, 4, DateTimeKind.Utc), utc);
            Assert.AreEqual(DateTimeKind.Utc, utc.Value.Kind);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow(null)]
        public void ParseState_EmptyIsUnknown(string text)
        {
            var (state, utc) = ServerStatusAnnouncer.ParseState(text);
            Assert.IsNull(state);
            Assert.IsNull(utc);
        }

        [TestMethod]
        public void ParseState_BadTimeKeepsState()
        {
            var (state, utc) = ServerStatusAnnouncer.ParseState("clean not-a-date");
            Assert.AreEqual("clean", state);
            Assert.IsNull(utc);
        }
    }
}
