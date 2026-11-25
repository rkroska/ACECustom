using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Common.Extensions;
using ACE.Server.Managers;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using WorldQuest = ACE.Database.Models.World.Quest;

namespace ACE.Server.Tests
{
    /// <summary>
    /// IP-locked content: the InqIPQuest emote (EmoteType 156), "at most N characters per IP address within the quest's
    /// MinDelta". Pins the pure rules: AccountQuestLimits.IpInWindow, IpIsDone and IpNextSolveTime (what %iqt shows).
    /// No DB, no world.
    /// </summary>
    [TestClass]
    public class IpQuestLimitsTests
    {
        private const uint Now = 1_800_000_000;
        private const uint TwentyHours = 72000;

        private static WorldQuest Quest(uint minDelta = TwentyHours) => new WorldQuest { Name = "TestIpQuest", MinDelta = minDelta, MaxSolves = -1 };

        private static List<AccountQuestLimits.Stamp> Stamps(params (uint secondsAgo, int count)[] rows) =>
            rows.Select(r => new AccountQuestLimits.Stamp(Now - r.secondsAgo, r.count)).ToList();

        [TestMethod]
        public void NobodyOnTheAddress_GoesAhead()
        {
            Assert.IsFalse(AccountQuestLimits.IpIsDone(Stamps(), Quest(), 2, 1.0, Now));
        }

        [TestMethod]
        public void OneInTheWindow_LimitTwo_GoesAhead()
        {
            Assert.IsFalse(AccountQuestLimits.IpIsDone(Stamps((3600, 4)), Quest(), 2, 1.0, Now));
        }

        [TestMethod]
        public void TwoInTheWindow_LimitTwo_IsDone()
        {
            Assert.IsTrue(AccountQuestLimits.IpIsDone(Stamps((3600, 4), (7200, 1)), Quest(), 2, 1.0, Now));
        }

        [TestMethod]
        public void OldCompletionsDontCount()
        {
            var stamps = Stamps((3600, 4), (21 * 3600, 9), (30 * 3600, 2));
            Assert.AreEqual(1, AccountQuestLimits.IpInWindow(stamps, Quest(), 1.0, Now));
            Assert.IsFalse(AccountQuestLimits.IpIsDone(stamps, Quest(), 2, 1.0, Now));
        }

        [TestMethod]
        public void ErasedRowsDontCount()
        {
            Assert.AreEqual(0, AccountQuestLimits.IpInWindow(Stamps((60, 0), (60, -1)), Quest(), 1.0, Now));
        }

        [TestMethod]
        public void WindowEndsExactlyNow_DoesntCount()
        {
            Assert.AreEqual(0, AccountQuestLimits.IpInWindow(Stamps((TwentyHours, 1)), Quest(), 1.0, Now));
        }

        [TestMethod]
        public void MissingWorldQuest_NeverLimits()
        {
            Assert.IsFalse(AccountQuestLimits.IpIsDone(Stamps((60, 1), (60, 1)), null, 2, 1.0, Now));
        }

        [TestMethod]
        public void NextSolveTime_UnderTheLimit_IsNow()
        {
            Assert.AreEqual(TimeSpan.MinValue, AccountQuestLimits.IpNextSolveTime(Stamps((3600, 1)), Quest(), 2, 1.0, Now));
        }

        [TestMethod]
        public void NextSolveTime_AtTheLimit_IsWhenTheOldestRunsOut()
        {
            // done 1 h and 5 h ago: the 5 h one runs out first, in 15 h
            var t = AccountQuestLimits.IpNextSolveTime(Stamps((3600, 1), (5 * 3600, 1)), Quest(), 2, 1.0, Now);
            Assert.AreEqual(TimeSpan.FromHours(15), t);
        }

        [TestMethod]
        public void IqtText_HeldGate_SaysSoInsteadOfATime()
        {
            Assert.AreEqual("a moment", ACE.Server.WorldObjects.Managers.EmoteManager.IpTimeText(
                new AccountQuestLimits.IpStatus(TimeSpan.MinValue, 1, 2, AccountQuestLimits.GateHold.InProgress)));
            Assert.AreEqual("a while (after the next server restart)", ACE.Server.WorldObjects.Managers.EmoteManager.IpTimeText(
                new AccountQuestLimits.IpStatus(TimeSpan.MinValue, 1, 2, AccountQuestLimits.GateHold.ClosedUntilRestart)));
        }

        [TestMethod]
        public void IqtText_OpenGate_ShowsTheWait()
        {
            var wait = TimeSpan.FromHours(5);
            Assert.AreEqual(wait.GetFriendlyString(), ACE.Server.WorldObjects.Managers.EmoteManager.IpTimeText(new AccountQuestLimits.IpStatus(wait, 2, 2)));
        }

        [TestMethod]
        public void LimitOne_OneInTheWindow_IsDone_AndWaitsForIt()
        {
            // the gate's own limit (Amount 1) must drive both the gate and %iqt, not the default 2
            var stamps = Stamps((3600, 1));
            Assert.IsTrue(AccountQuestLimits.IpIsDone(stamps, Quest(), 1, 1.0, Now));
            Assert.AreEqual(TimeSpan.FromHours(19), AccountQuestLimits.IpNextSolveTime(stamps, Quest(), 1, 1.0, Now));
            Assert.AreEqual(TimeSpan.MinValue, AccountQuestLimits.IpNextSolveTime(stamps, Quest(), 2, 1.0, Now));
        }

        [TestMethod]
        public void RandomStampList_DropsNoQbQuests()
        {
            var kept = ACE.Server.WorldObjects.Managers.EmoteManager.WithoutQbExempt(new[] { "RealStamp", "HelperTimer", "OtherStamp" }, q => q == "HelperTimer");
            CollectionAssert.AreEqual(new[] { "RealStamp", "OtherStamp" }, kept);
        }

        [TestMethod]
        public void NextSolveTime_OverTheLimit_WaitsUntilBackUnder()
        {
            // three in the window, limit 2: two must run out to get under it (3 -> 2 is still at the limit).
            // They end in 10 h, 15 h and 19 h, so the group is under the limit after 15 h.
            var t = AccountQuestLimits.IpNextSolveTime(Stamps((3600, 1), (5 * 3600, 1), (10 * 3600, 1)), Quest(), 2, 1.0, Now);
            Assert.AreEqual(TimeSpan.FromHours(15), t);
        }
    }
}
