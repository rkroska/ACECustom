using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Server.Managers;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ChainState = ACE.Server.Managers.AccountQuestLimits.ChainState;
using GateHold = ACE.Server.Managers.AccountQuestLimits.GateHold;
using WorldQuest = ACE.Database.Models.World.Quest;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Account-locked content: the InqAccountQuest emote (EmoteType 155) and the %aqt / %aqc emote text, InqQuest, %tqt and
    /// %tqc counted across every character on an account. Pins the pure rules behind them: AccountQuestLimits.IsDone
    /// (QuestManager.HasQuest and CanSolve applied to all of an account's stamps; true = QuestSuccess, done / timer running;
    /// false = QuestFailure, go ahead), NextSolveTime and Completions (what %aqt and %aqc show), and AccountStamps (a
    /// character in memory is read from memory, never from its possibly stale saved row), and Admit / TryClaim / TryAct /
    /// Release (the account's claim on a gate: taken before the stamps are read, held to the end of the object's emotes, and
    /// taken over only from a chain that left the world before it acted). No DB, no world.
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

        // ---- the account's claim on the gate (Admit, TryClaim, TryAct, Release, Abandon, AwaitAnswer) ----
        // Each test uses its own account ids, and every gate a test touches is dropped after it (the claims are shared,
        // static state, so a repeated run starts clean). The chain delegates stand in for the object running the gate
        // (EmoteManager: Running while it has emotes in flight, Finished when it has none, Gone when it left the world with
        // emotes in flight); readIsDone for the account's stamps (true = done, false = go ahead, null = a database couldn't
        // be read).

        private static readonly Func<ChainState> Running = () => ChainState.Running;

        private readonly List<(uint AccountId, string Quest)> usedGates = new();

        private AccountQuestLimits.Claim TryClaim(uint accountId, string quest, Func<ChainState> chain)
        {
            usedGates.Add((accountId, quest));
            return AccountQuestLimits.TryClaim(accountId, quest, chain);
        }

        private bool Admit(uint? accountId, string quest, Func<ChainState> chain, Func<bool?> readIsDone, out AccountQuestLimits.Claim claim)
        {
            if (accountId.HasValue)
                usedGates.Add((accountId.Value, quest));

            return AccountQuestLimits.Admit(accountId, quest, chain, readIsDone, out claim);
        }

        [TestCleanup]
        public void ForgetUsedGates()
        {
            foreach (var (accountId, quest) in usedGates)
                AccountQuestLimits.Forget(accountId, quest);

            usedGates.Clear();
        }

        [TestMethod]
        public void Claim_HeldUntilReleased()
        {
            // the QuestFailure set (reward, StampQuest) runs a tick or more after the gate: a second check meanwhile is closed
            var first = TryClaim(9_000_001, "TestAccountQuest", Running);
            Assert.IsNotNull(first);
            Assert.IsNull(TryClaim(9_000_001, "TestAccountQuest", Running));
            Assert.IsNull(TryClaim(9_000_001, "testaccountquest", Running));   // any case

            AccountQuestLimits.Release(first);   // the object finished its emotes: reward and stamp done, or stopped
            Assert.IsNotNull(TryClaim(9_000_001, "TestAccountQuest", Running));
        }

        [TestMethod]
        public void Claim_ALongRunningChain_HoldsWithNoTimeLimit()
        {
            // a chain whose StampQuest waits a long time keeps the gate closed until it ends: there is no timer to run out
            var state = ChainState.Running;
            Assert.IsNotNull(TryClaim(9_000_002, "TestAccountQuest", () => state));

            for (var i = 0; i < 3; i++)
                Assert.IsNull(TryClaim(9_000_002, "TestAccountQuest", Running));

            state = ChainState.Finished;   // its emotes are over (released or not)
            Assert.IsNotNull(TryClaim(9_000_002, "TestAccountQuest", Running));
        }

        [TestMethod]
        public void Claim_IsPerAccountAndQuest()
        {
            Assert.IsNotNull(TryClaim(9_000_003, "TestAccountQuest", Running));
            Assert.IsNotNull(TryClaim(9_000_004, "TestAccountQuest", Running));   // another account
            Assert.IsNotNull(TryClaim(9_000_003, "OtherAccountQuest", Running));   // another quest
            AccountQuestLimits.Release(null);   // no claim: nothing to do
            Assert.IsTrue(AccountQuestLimits.TryAct(null));   // no claims: nothing to stop
        }

        [TestMethod]
        public void Claim_FinishedWithoutARelease_IsReplacedUnfenced()
        {
            var state = ChainState.Running;
            var old = TryClaim(9_000_005, "TestAccountQuest", () => state);
            Assert.IsTrue(AccountQuestLimits.TryAct(new[] { old }));

            state = ChainState.Finished;   // all of its steps ran, StampQuest included
            var next = TryClaim(9_000_005, "TestAccountQuest", Running);
            Assert.IsNotNull(next);

            Assert.IsFalse(AccountQuestLimits.IsTakenOver(old));   // not fenced: a Give it queued before it finished still runs

            AccountQuestLimits.Release(old);   // a late release leaves the new claim alone
            Assert.IsNull(TryClaim(9_000_005, "TestAccountQuest", Running));
        }

        [TestMethod]
        public void Claim_GoneBeforeItActed_IsFencedThenTakenOver()
        {
            // takeover first: the object left the world before its chain did anything, so another use may go ahead, and the
            // old chain, an action queued before included, never acts
            var state = ChainState.Running;
            var old = TryClaim(9_000_006, "TestAccountQuest", () => state);

            state = ChainState.Gone;
            Assert.IsFalse(Admit(9_000_006, "TestAccountQuest", Running, () => false, out var taker));
            Assert.IsNotNull(taker);
            Assert.IsTrue(AccountQuestLimits.IsTakenOver(old));
            Assert.IsFalse(AccountQuestLimits.TryAct(new[] { old }));
            Assert.IsTrue(AccountQuestLimits.TryAct(new[] { taker }));

            AccountQuestLimits.Release(old);   // the old chain stopping leaves the taker's claim alone
            Assert.IsNull(TryClaim(9_000_006, "TestAccountQuest", Running));
        }

        [TestMethod]
        public void Claim_GoneAfterItActed_StaysClosed()
        {
            // acting first: once the chain has acted (a step, and with it whatever the step queued, such as a Give), its
            // claim can't be taken over, so a chain that then can't finish keeps the gate closed (fails closed) rather than
            // risk a second reward
            var state = ChainState.Running;
            var old = TryClaim(9_000_007, "TestAccountQuest", () => state);
            Assert.IsTrue(AccountQuestLimits.TryAct(new[] { old }));

            state = ChainState.Gone;
            var read = false;
            Assert.IsTrue(Admit(9_000_007, "TestAccountQuest", Running, () => { read = true; return false; }, out var claim));
            Assert.IsFalse(read);
            Assert.IsNull(claim);
            Assert.IsFalse(AccountQuestLimits.IsTakenOver(old));
            Assert.IsTrue(AccountQuestLimits.TryAct(new[] { old }));   // whatever it still has queued may run
            Assert.IsNull(TryClaim(9_000_007, "TestAccountQuest", Running));
        }

        [TestMethod]
        public void Admit_ClaimsTheGateBeforeReadingTheStamps()
        {
            // while this use reads the stamps, the gate is already its own: another use can't claim it, stamp and release
            // between the read and the claim (landblocks tick in parallel), which would leave this use going ahead on an
            // old read
            var read = false;
            Assert.IsFalse(Admit(9_000_008, "TestAccountQuest", Running, () =>
            {
                read = true;
                Assert.IsNull(TryClaim(9_000_008, "TestAccountQuest", Running));
                return false;
            }, out var claim));

            Assert.IsTrue(read);
            Assert.IsNotNull(claim);
            Assert.IsNull(TryClaim(9_000_008, "TestAccountQuest", Running));
        }

        [TestMethod]
        public void Admit_WhileAnotherUseHoldsTheGate_IsDoneWithoutReading()
        {
            var first = TryClaim(9_000_009, "TestAccountQuest", Running);   // its reward and stamp in flight

            var read = false;
            Assert.IsTrue(Admit(9_000_009, "TestAccountQuest", Running, () => { read = true; return false; }, out var claim));
            Assert.IsFalse(read);
            Assert.IsNull(claim);

            // that use stamps and finishes: the next one claims, reads the stamp, is done, and leaves the gate free
            AccountQuestLimits.Release(first);
            Assert.IsTrue(Admit(9_000_009, "TestAccountQuest", Running, () => true, out claim));
            Assert.IsNull(claim);
            Assert.IsNotNull(TryClaim(9_000_009, "TestAccountQuest", Running));
        }

        [TestMethod]
        public void Admit_ReadFailsOrThrows_IsDoneAndFreesTheGate()
        {
            Assert.IsTrue(Admit(9_000_010, "TestAccountQuest", Running, () => null, out var claim));
            Assert.IsNull(claim);

            try
            {
                Admit(9_000_010, "TestAccountQuest", Running, () => throw new InvalidOperationException("read"), out _);
                Assert.Fail("The read's exception should reach the caller.");
            }
            catch (InvalidOperationException)
            {
            }

            Assert.IsNotNull(TryClaim(9_000_010, "TestAccountQuest", Running));
        }

        [TestMethod]
        public void Admit_NoAccount_DecidesWithoutAClaim()
        {
            Assert.IsFalse(Admit(null, "TestAccountQuest", Running, () => false, out var claim));
            Assert.IsNull(claim);
            Assert.IsTrue(Admit(null, "TestAccountQuest", Running, () => true, out claim));
            Assert.IsNull(claim);
        }

        [TestMethod]
        public void Claim_AbandonedAfterItActed_StaysClosed()
        {
            // the chain's target left the world to log out after the chain acted: a stamp would no longer be saved, so the
            // gate stays closed rather than let the reward through again
            var claim = TryClaim(9_000_011, "TestAccountQuest", Running);
            Assert.IsTrue(AccountQuestLimits.TryAct(new[] { claim }));

            AccountQuestLimits.Abandon(new[] { claim });
            AccountQuestLimits.Abandon(new[] { claim });   // again, from another step of the same chain
            AccountQuestLimits.Release(claim);             // and the object going idle
            Assert.IsNull(TryClaim(9_000_011, "TestAccountQuest", Running));
            Assert.AreEqual(GateHold.ClosedUntilRestart, AccountQuestLimits.GetGateHold(9_000_011, "TestAccountQuest"));
        }

        [TestMethod]
        public void Claim_AbandonedBeforeItActed_IsReleased()
        {
            var claim = TryClaim(9_000_012, "TestAccountQuest", Running);

            AccountQuestLimits.Abandon(new[] { claim });
            Assert.AreEqual(GateHold.None, AccountQuestLimits.GetGateHold(9_000_012, "TestAccountQuest"));
            Assert.IsNotNull(TryClaim(9_000_012, "TestAccountQuest", Running));
        }

        [TestMethod]
        public void GetGateHold_InProgressOrClosedUntilRestart()
        {
            // what %aqt shows when a claim holds the gate before any stamp: "a moment" while the reward is on its way (a
            // running chain), the restart note once a chain that acted can't finish; nothing for a finished chain, nor for
            // one whose object left before it acted (the next use can take that over)
            var state = ChainState.Running;
            var claim = TryClaim(9_000_013, "TestAccountQuest", () => state);
            Assert.AreEqual(GateHold.InProgress, AccountQuestLimits.GetGateHold(9_000_013, "testaccountquest"));

            state = ChainState.Gone;
            Assert.AreEqual(GateHold.None, AccountQuestLimits.GetGateHold(9_000_013, "TestAccountQuest"));
            Assert.IsTrue(AccountQuestLimits.TryAct(new[] { claim }));
            Assert.AreEqual(GateHold.ClosedUntilRestart, AccountQuestLimits.GetGateHold(9_000_013, "TestAccountQuest"));

            state = ChainState.Finished;
            Assert.AreEqual(GateHold.None, AccountQuestLimits.GetGateHold(9_000_013, "TestAccountQuest"));
            Assert.AreEqual(GateHold.None, AccountQuestLimits.GetGateHold(9_000_014, "TestAccountQuest"));   // no claim at all
        }

        [TestMethod]
        public void Claim_AwaitingAnAnswer_HoldsUntilTheAnswerTakesIt()
        {
            // the chain asked a yes/no question and its emotes ended: until the answer comes, the claim holds even with
            // nothing in flight, and the object going idle doesn't release it
            var state = ChainState.Running;
            var claim = TryClaim(9_000_015, "TestAccountQuest", () => state);
            Assert.IsTrue(AccountQuestLimits.TryAct(new[] { claim }));

            AccountQuestLimits.AwaitAnswer(new[] { claim }, TimeSpan.FromMinutes(5));
            state = ChainState.Finished;
            AccountQuestLimits.Release(claim);
            Assert.IsNull(TryClaim(9_000_015, "TestAccountQuest", Running));
            Assert.AreEqual(GateHold.InProgress, AccountQuestLimits.GetGateHold(9_000_015, "TestAccountQuest"));

            // the answer's set takes the claims on: while it runs they hold as usual, and when it ends they're released
            AccountQuestLimits.AnswerArrived(new[] { claim }, taken: true);
            state = ChainState.Running;
            Assert.IsNull(TryClaim(9_000_015, "TestAccountQuest", Running));

            state = ChainState.Finished;
            AccountQuestLimits.Release(claim);
            Assert.IsNotNull(TryClaim(9_000_015, "TestAccountQuest", Running));
        }

        [TestMethod]
        public void Claim_QuestionClosedWithNoAnswer_IsTakenOverFenced()
        {
            // the asking emotes ended at the question, then it closed with no answer (the player walked away): the claim is
            // over whatever its object does next, here leaving the world, and an answer that still turns up can't act on it
            var state = ChainState.Finished;
            var claim = TryClaim(9_000_016, "TestAccountQuest", () => state);
            Assert.IsTrue(AccountQuestLimits.TryAct(new[] { claim }));

            AccountQuestLimits.AwaitAnswer(new[] { claim }, TimeSpan.FromMinutes(5));
            AccountQuestLimits.Release(claim);
            state = ChainState.Gone;
            Assert.IsNull(TryClaim(9_000_016, "TestAccountQuest", Running));

            AccountQuestLimits.AwaitAnswer(new[] { claim }, TimeSpan.Zero);   // the question's time is up
            Assert.AreEqual(GateHold.None, AccountQuestLimits.GetGateHold(9_000_016, "TestAccountQuest"));
            Assert.IsNotNull(TryClaim(9_000_016, "TestAccountQuest", Running));
            Assert.IsTrue(AccountQuestLimits.IsTakenOver(claim));

            AccountQuestLimits.AnswerArrived(new[] { claim }, taken: true);
            Assert.IsFalse(AccountQuestLimits.TryAct(new[] { claim }));
        }

        [TestMethod]
        public void Claim_AnswerNotTaken_EndsAsWithNoQuestion()
        {
            // an answer the object doesn't take (busy with another use, or no set for it): with the asking emotes still
            // running, they keep holding the claim and release it when they end
            var state = ChainState.Running;
            var claim = TryClaim(9_000_017, "TestAccountQuest", () => state);
            Assert.IsTrue(AccountQuestLimits.TryAct(new[] { claim }));
            AccountQuestLimits.AwaitAnswer(new[] { claim }, TimeSpan.FromMinutes(5));

            AccountQuestLimits.AnswerArrived(new[] { claim }, taken: false);
            Assert.IsNull(TryClaim(9_000_017, "TestAccountQuest", Running));

            state = ChainState.Finished;
            AccountQuestLimits.Release(claim);
            Assert.IsNotNull(TryClaim(9_000_017, "TestAccountQuest", Running));

            // with the asking emotes over, released at once, though the object is busy with another use
            var other = ChainState.Finished;
            var parked = TryClaim(9_000_018, "TestAccountQuest", () => other);
            Assert.IsTrue(AccountQuestLimits.TryAct(new[] { parked }));
            AccountQuestLimits.AwaitAnswer(new[] { parked }, TimeSpan.FromMinutes(5));
            AccountQuestLimits.Release(parked);

            other = ChainState.Running;
            AccountQuestLimits.AnswerArrived(new[] { parked }, taken: false);
            Assert.AreEqual(GateHold.None, AccountQuestLimits.GetGateHold(9_000_018, "TestAccountQuest"));
            Assert.IsNotNull(TryClaim(9_000_018, "TestAccountQuest", Running));
            Assert.IsFalse(AccountQuestLimits.IsTakenOver(parked));
        }
    }
}
