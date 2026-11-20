using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Server.Managers;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using WorldQuest = ACE.Database.Models.World.Quest;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Account-locked content: the InqAccountQuest emote (EmoteType 155) and the %aqt / %aqc emote text, InqQuest, %tqt and
    /// %tqc counted across every character on an account. Pins the pure rules behind them: AccountQuestLimits.IsDone
    /// (QuestManager.HasQuest and CanSolve applied to all of an account's stamps; true = QuestSuccess, done / timer running;
    /// false = QuestFailure, go ahead), NextSolveTime and Completions (what %aqt and %aqc show), and AccountStamps (a
    /// character in memory is read from memory, never from its possibly stale saved row), and TryClaim / Release (the
    /// account's claim on a gate, from the gate to the end of the object's emotes, at most ClaimTimeout). No DB, no world.
    /// </summary>
    [TestClass]
    public class AccountQuestLimitsTests
    {
        private const uint Now = 1_800_000_000;

        private const uint TwentyHours = 72000;

        private static WorldQuest Quest(uint minDelta, int maxSolves = -1, string name = "TestAccountQuest") =>
            new WorldQuest { Name = name, MinDelta = minDelta, MaxSolves = maxSolves };

        /// <summary>Stamps given as (seconds ago, times completed), one per character.</summary>
        private static List<AccountQuestLimits.Stamp> Stamps(params (uint secondsAgo, int count)[] rows) =>
            rows.Select(r => new AccountQuestLimits.Stamp(Now - r.secondsAgo, r.count)).ToList();

        private static bool Done(List<AccountQuestLimits.Stamp> stamps, WorldQuest quest, double rate = 1.0) =>
            AccountQuestLimits.IsDone(stamps, quest, rate, Now);

        [TestMethod]
        public void NoCharacterOnTheAccountHasIt_GoesAhead()
        {
            Assert.IsFalse(Done(Stamps(), Quest(TwentyHours)));
        }

        [TestMethod]
        public void AnotherCharacterOpenedItAnHourAgo_IsDone()
        {
            Assert.IsTrue(Done(Stamps((3600, 1)), Quest(TwentyHours)));
        }

        [TestMethod]
        public void TimerRanOut_GoesAhead()
        {
            Assert.IsFalse(Done(Stamps((21 * 3600, 5)), Quest(TwentyHours)));
        }

        [TestMethod]
        public void TimerEndsExactlyNow_GoesAhead()
        {
            // GetNextSolveTime: solvable once currentTime >= LastTimeCompleted + MinDelta.
            Assert.IsFalse(Done(Stamps((TwentyHours, 1)), Quest(TwentyHours)));
            Assert.IsTrue(Done(Stamps((TwentyHours - 1, 1)), Quest(TwentyHours)));
        }

        [TestMethod]
        public void NewestStampOnTheAccountSetsTheTimer()
        {
            Assert.IsTrue(Done(Stamps((30 * 3600, 9), (2 * 3600, 3)), Quest(TwentyHours)));
        }

        [TestMethod]
        public void MaxSolvesOne_IsOncePerAccountEver()
        {
            Assert.IsTrue(Done(Stamps((10_000_000, 1)), Quest(0, maxSolves: 1)));
        }

        [TestMethod]
        public void MaxSolvesCountsTheWholeAccount()
        {
            Assert.IsFalse(Done(Stamps((1000, 1), (1000, 1)), Quest(0, maxSolves: 3)));
            Assert.IsTrue(Done(Stamps((1000, 2), (1000, 1)), Quest(0, maxSolves: 3)));
        }

        [TestMethod]
        public void MaxSolvesMinusOne_IsUnlimited()
        {
            Assert.IsFalse(Done(Stamps((1000, 500)), Quest(0, maxSolves: -1)));
        }

        [TestMethod]
        public void MinDeltaRate_ScalesTheTimer()
        {
            // 0.001 turns 20 hours into 72 seconds.
            Assert.IsFalse(Done(Stamps((120, 1)), Quest(TwentyHours), rate: 0.001));
            Assert.IsTrue(Done(Stamps((60, 1)), Quest(TwentyHours), rate: 0.001));
        }

        [TestMethod]
        public void ColoArenaQuests_AreNotScaled()
        {
            Assert.IsTrue(Done(Stamps((120, 1)), Quest(TwentyHours, name: "ColoArenaTest"), rate: 0.001));
        }

        [TestMethod]
        public void MissingWorldQuest_IsDoneOnlyIfSomeoneHasIt()
        {
            Assert.IsTrue(Done(Stamps((10_000_000, 1)), null));
            Assert.IsFalse(Done(Stamps(), null));
        }

        [TestMethod]
        public void NegativeCountRows_DoNotCountAsHavingTheQuest()
        {
            Assert.IsFalse(Done(Stamps((60, -1)), Quest(TwentyHours)));
        }

        [TestMethod]
        public void HugeMinDelta_DoesNotWrapAround()
        {
            Assert.IsTrue(Done(Stamps((60, 1)), Quest(uint.MaxValue)));
        }

        // ---- what %aqt and %aqc show (NextSolveTime, Completions) ----

        private static TimeSpan TimeLeft(List<AccountQuestLimits.Stamp> stamps, WorldQuest quest, double rate = 1.0) =>
            AccountQuestLimits.NextSolveTime(stamps, quest, rate, Now);

        [TestMethod]
        public void TimeLeft_CountsDownFromTheNewestStampOnTheAccount()
        {
            // the mule opened it 30 hours ago, the alt an hour ago: 19 hours left, whoever asks
            Assert.AreEqual(TimeSpan.FromHours(19), TimeLeft(Stamps((30 * 3600, 2), (3600, 1)), Quest(TwentyHours)));
        }

        [TestMethod]
        public void TimeLeft_IsMinValueWhenTheAccountCanGoAhead()
        {
            // TimeSpan.MinValue = solvable now, as QuestManager.GetNextSolveTime returns it
            Assert.AreEqual(TimeSpan.MinValue, TimeLeft(Stamps(), Quest(TwentyHours)));
            Assert.AreEqual(TimeSpan.MinValue, TimeLeft(Stamps((21 * 3600, 1)), Quest(TwentyHours)));
            Assert.AreEqual(TimeSpan.MinValue, TimeLeft(Stamps((60, -1)), Quest(TwentyHours)));
        }

        [TestMethod]
        public void TimeLeft_IsMaxValueWhenTheAccountCanNeverSolveItAgain()
        {
            Assert.AreEqual(TimeSpan.MaxValue, TimeLeft(Stamps((10_000_000, 1)), Quest(0, maxSolves: 1)));
            Assert.AreEqual(TimeSpan.MaxValue, TimeLeft(Stamps(), null));   // missing from the world, as GetNextSolveTime
        }

        [TestMethod]
        public void TimeLeft_FollowsTheMinDeltaRate()
        {
            // 0.5 turns 20 hours into 10; opened an hour ago
            Assert.AreEqual(TimeSpan.FromHours(9), TimeLeft(Stamps((3600, 1)), Quest(TwentyHours), rate: 0.5));
        }

        [TestMethod]
        public void TimeLeft_AgreesWithIsDone()
        {
            var cases = new[]
            {
                (Stamps(), Quest(TwentyHours)),
                (Stamps((3600, 1)), Quest(TwentyHours)),
                (Stamps((TwentyHours, 1)), Quest(TwentyHours)),
                (Stamps((1000, 2), (1000, 1)), Quest(0, maxSolves: 3)),
                (Stamps((60, -1)), Quest(TwentyHours)),
                (Stamps((60, 1)), (WorldQuest)null),
            };

            foreach (var (stamps, quest) in cases)
            {
                var hasQuest = stamps.Any(s => s.NumTimesCompleted >= 0);
                Assert.AreEqual(hasQuest && TimeLeft(stamps, quest) != TimeSpan.MinValue, Done(stamps, quest));
            }
        }

        [TestMethod]
        public void Completions_AddsUpEveryCharacterOnTheAccount()
        {
            Assert.AreEqual(0L, AccountQuestLimits.Completions(Stamps()));
            Assert.AreEqual(5L, AccountQuestLimits.Completions(Stamps((60, 2), (3600, 3), (90, -1))));   // negative rows skipped
        }

        // ---- where each character's stamp comes from (AccountStamps) ----

        private const uint Self = 0x50000001, Alt = 0x50000002, Mule = 0x50000003;

        private static (uint, AccountQuestLimits.Stamp) Row(uint character, uint secondsAgo, int count) =>
            (character, new AccountQuestLimits.Stamp(Now - secondsAgo, count));

        [TestMethod]
        public void LoggingOutAlt_ItsMemoryWinsOverItsStaleSavedRow()
        {
            // the alt opened the chest a minute ago and is still logging out: its save (and new stamp) is queued, so the
            // database still holds yesterday's row. Read from the saved row alone, the account would look free.
            var inMemory = new List<(uint, AccountQuestLimits.Stamp)> { Row(Alt, 60, 4) };
            var saved = new List<(uint, AccountQuestLimits.Stamp)> { Row(Alt, 90_000, 3), Row(Mule, 200_000, 1) };

            var stamps = AccountQuestLimits.AccountStamps(inMemory, saved, new HashSet<uint> { Self, Alt });

            Assert.AreEqual(2, stamps.Count);   // the alt from memory, the mule from its saved row
            Assert.IsTrue(Done(stamps, Quest(TwentyHours)));
            Assert.IsFalse(Done(saved.Select(s => s.Item2).ToList(), Quest(TwentyHours)));   // the race this closes
        }

        [TestMethod]
        public void CharacterInMemoryWithoutAStamp_HidesItsSavedRow()
        {
            // in memory with no stamp (erased, its save still queued): the saved row is not read back
            var saved = new List<(uint, AccountQuestLimits.Stamp)> { Row(Self, 60, 1) };

            var stamps = AccountQuestLimits.AccountStamps(new List<(uint, AccountQuestLimits.Stamp)>(), saved, new HashSet<uint> { Self });

            Assert.AreEqual(0, stamps.Count);
            Assert.IsFalse(Done(stamps, Quest(TwentyHours)));
        }

        [TestMethod]
        public void OfflineCharacters_AreReadFromTheirSavedRows()
        {
            var saved = new List<(uint, AccountQuestLimits.Stamp)> { Row(Alt, 3600, 2), Row(Mule, 100_000, 1) };

            var stamps = AccountQuestLimits.AccountStamps(new List<(uint, AccountQuestLimits.Stamp)>(), saved, new HashSet<uint> { Self });

            Assert.AreEqual(2, stamps.Count);
            Assert.IsTrue(Done(stamps, Quest(TwentyHours)));   // the alt, an hour ago
        }

        // ---- the account's claim on the gate, from the gate to the end of the object's emotes (TryClaim, Release) ----
        // Each test uses its own account ids: the claims are shared state.

        private static readonly DateTime T0 = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void Claim_HeldUntilReleased()
        {
            // the QuestFailure set (reward, StampQuest) runs a tick or more after the gate: a second check meanwhile is closed
            var first = AccountQuestLimits.TryClaim(9_000_001, "TestAccountQuest", T0);
            Assert.IsNotNull(first);
            Assert.IsNull(AccountQuestLimits.TryClaim(9_000_001, "TestAccountQuest", T0.AddMilliseconds(50)));
            Assert.IsNull(AccountQuestLimits.TryClaim(9_000_001, "testaccountquest", T0.AddMinutes(4)));   // any case, long waits

            AccountQuestLimits.Release(first);   // the object went idle: reward and stamp done, or stopped
            Assert.IsNotNull(AccountQuestLimits.TryClaim(9_000_001, "TestAccountQuest", T0.AddMinutes(4)));
        }

        [TestMethod]
        public void Claim_EndsAfterClaimTimeout()
        {
            Assert.IsNotNull(AccountQuestLimits.TryClaim(9_000_002, "TestAccountQuest", T0));
            Assert.IsNotNull(AccountQuestLimits.TryClaim(9_000_002, "TestAccountQuest", T0 + AccountQuestLimits.ClaimTimeout));
            Assert.AreEqual(TimeSpan.FromMinutes(5), AccountQuestLimits.ClaimTimeout);
        }

        [TestMethod]
        public void Claim_ReleasingOneThatTimedOut_KeepsTheNewOne()
        {
            var old = AccountQuestLimits.TryClaim(9_000_005, "TestAccountQuest", T0);
            var current = AccountQuestLimits.TryClaim(9_000_005, "TestAccountQuest", T0 + AccountQuestLimits.ClaimTimeout);
            Assert.IsNotNull(current);

            AccountQuestLimits.Release(old);   // the stuck object finally goes idle
            Assert.IsNull(AccountQuestLimits.TryClaim(9_000_005, "TestAccountQuest", T0 + AccountQuestLimits.ClaimTimeout + TimeSpan.FromSeconds(1)));
        }

        [TestMethod]
        public void Claim_IsPerAccountAndQuest()
        {
            Assert.IsNotNull(AccountQuestLimits.TryClaim(9_000_003, "TestAccountQuest", T0));
            Assert.IsNotNull(AccountQuestLimits.TryClaim(9_000_004, "TestAccountQuest", T0));   // another account
            Assert.IsNotNull(AccountQuestLimits.TryClaim(9_000_003, "OtherAccountQuest", T0));   // another quest
            AccountQuestLimits.Release(null);   // no claim: nothing to do
        }
    }
}
