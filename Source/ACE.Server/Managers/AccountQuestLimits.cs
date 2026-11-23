using System;
using System.Collections.Generic;
using System.Linq;

using log4net;

using ACE.Common;
using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Server.WorldObjects;

using WorldQuest = ACE.Database.Models.World.Quest;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Account-locked content (ILT custom): InqQuest, %tqt and %tqc counted across every character on the player's account.
    /// Backs the InqAccountQuest emote (EmoteType 155) and the %aqt / %aqc emote text. Any weenie that gates on InqQuest
    /// becomes account-locked by switching that one step to InqAccountQuest; see docs/ACCOUNT_LOCKED_CONTENT_GUIDE.md.
    ///
    /// The quest's own rules from the quest table apply to the whole account, the same way QuestManager.GetNextSolveTime
    /// applies them to one character:
    ///   - MaxSolves (-1 = unlimited) is checked against the account's total completions;
    ///   - MinDelta (scaled by quest_mindelta_rate where QuestManager.CanScaleQuestMinDelta allows) runs from the newest
    ///     completion by any character on the account.
    /// So max_Solves 1 means once per account ever, and min_Delta 72000 means once per account every 20 hours.
    ///
    /// StampQuest is unchanged: it stamps the character that did it. Where each character's stamps are read from:
    ///   - every character of the account still in PlayerManager's online list, from memory. That includes a character
    ///     that is logging out: it stays online until its save (quest stamps included) has been written, while ILT lets
    ///     another character of the same account enter the world before then, so its newest stamps may not be saved yet;
    ///   - every other character, from the shard database. Deleted characters still count until the character is purged
    ///     (its quest rows cascade away with it), so deleting a character doesn't reset the timer.
    ///
    /// The gate claims the quest for the account before it reads the stamps, and when it lets a character through, the
    /// account keeps that Claim until the object running the reward has finished its emotes, so a second check can't pass
    /// before the stamp lands.
    /// </summary>
    public static class AccountQuestLimits
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>Errors and warnings are logged at most once a minute (the rest at debug level), so a database outage or
        /// a misconfigured emote doesn't flood the log on every use.</summary>
        private const double LogEverySeconds = 60;

        private static double nextLogAt;

        /// <summary>
        /// An account's hold on one quest's gate. The QuestSuccess or QuestFailure set runs on the next server tick (as for
        /// InqQuest) and some of its steps wait on their own (an NPC turning to give, Motion, CastSpell), so the stamp lands
        /// a while after the gate, and until it does the account's stamps don't show it. So the gate takes a Claim for the
        /// account before it reads the stamps (Admit), and while the claim's chain is Running (its object has emotes in
        /// flight, branches included, however long they wait) the gate stays closed for the account. EmoteManager releases
        /// its claims when those emotes have all finished. There is no timer on a running chain. A chain that asks a yes/no
        /// question (InqYesNo) keeps its claims while the question is open, up to its timeout, and the answer's set carries
        /// them on (AwaitAnswer, TryTakeAnswer, AnswerNotTaken).
        ///
        /// The chain that took a claim must TryAct before each of its steps (only that chain: other emote sets on the object
        /// don't), so what a step queues (a Give, a CastSpell) comes after it acted. A chain whose object left the world
        /// (Gone) before it acted can be taken over: its claim is fenced, so it never acts, and replaced. A chain that has
        /// acted is never taken over: if it stops before it finishes (its object left the world, its target left the world
        /// to log out, or a server error), the gate stays closed for the account until the server restarts (claims are
        /// memory only), failing closed as for a database error. Both checks run under one lock, so a chain either acts
        /// first and keeps the gate, or is fenced first and never acts.
        /// </summary>
        public sealed class Claim
        {
            internal Claim((uint AccountId, string Quest) key, Func<ChainState> chain)
            {
                Key = key;
                Chain = chain;
            }

            internal (uint AccountId, string Quest) Key { get; }

            /// <summary>Where the chain that took the claim stands now: the object running it, which a yes/no answer can move
            /// to the object that runs the answer (FollowChain). Dropped (Closed) when the chain is abandoned.</summary>
            internal Func<ChainState> Chain { get; private set; }

            /// <summary>Under claimLock: from now on the claim follows <paramref name="chain"/> (a closed claim stays closed).</summary>
            internal void FollowChain(Func<ChainState> chain)
            {
                if (!Closed)
                    Chain = chain;
            }

            /// <summary>Set when a chain that acted is abandoned: the claim keeps the gate closed until a restart. Read and
            /// written under claimLock.</summary>
            internal bool Closed { get; private set; }

            /// <summary>Under claimLock: keeps the gate closed for good and lets go of the chain's object.</summary>
            internal void Close()
            {
                Closed = true;
                Chain = ClosedChain;
            }

            /// <summary>Set once the chain has acted (TryAct). Read and written under claimLock.</summary>
            internal bool Acted { get; set; }

            /// <summary>
            /// Until when a yes/no question the chain asked (InqYesNo) stays open: its answer carries the claim on
            /// (EmoteManager.ExecuteAnswerSet), so until then the claim holds even with no emotes in flight. Read and written
            /// under claimLock.
            /// </summary>
            internal DateTime AwaitingAnswerUntil { get; set; }

            /// <summary>
            /// Set when the emotes holding the claim ended while their question was still open: from then on only the answer
            /// can carry the claim on, and once the question has closed with no answer taking it, the claim is over whatever
            /// the object does next. Read and written under claimLock.
            /// </summary>
            internal bool ParkedForAnswer { get; set; }

            /// <summary>Set when the claim is taken over: its chain may not act from then on. Read and written under
            /// claimLock.</summary>
            internal bool Fenced { get; set; }
        }

        /// <summary>Where the chain holding a claim stands.</summary>
        public enum ChainState
        {
            /// <summary>Its object still has emotes in flight and is in the world.</summary>
            Running,

            /// <summary>Its object has no emotes left in flight: the chain is over.</summary>
            Finished,

            /// <summary>Its object left the world with emotes still in flight: they can't finish.</summary>
            Gone,
        }

        private static readonly Dictionary<(uint AccountId, string Quest), Claim> claims = new();
        private static readonly object claimLock = new();

        private static readonly Func<ChainState> ClosedChain = () => ChainState.Gone;

        /// <summary>One character's registry row for a quest.</summary>
        public readonly record struct Stamp(uint LastTimeCompleted, int NumTimesCompleted);

        /// <summary>Why a claim keeps the account's gate closed when the stamps alone would let it through (for %aqt).</summary>
        public enum GateHold
        {
            /// <summary>No claim holds the gate.</summary>
            None,

            /// <summary>A reward is on its way: a chain still running, or a yes/no question it asked still open.</summary>
            InProgress,

            /// <summary>A chain acted and can't finish: the gate stays closed until the server restarts.</summary>
            ClosedUntilRestart,
        }

        /// <summary>
        /// What the %aqt and %aqc emote text show for an account: the time until the account can solve the quest again
        /// (TimeSpan.MinValue = now, TimeSpan.MaxValue = never, as QuestManager.GetNextSolveTime returns them), the
        /// account's total completions, and when the stamps would let it through, whether a claim holds the gate anyway.
        /// </summary>
        public readonly record struct AccountStatus(TimeSpan NextSolveTime, long Completions, GateHold Hold = GateHold.None);

        /// <summary>
        /// The account-wide InqQuest test (HasQuest &amp;&amp; !CanSolve): true when a character on the player's account has the
        /// quest and the account can't solve it right now, or when another use on the account holds the quest's gate (Claim).
        /// Fails closed (true) when the database can't be read or the emote names no quest, so neither a database problem nor
        /// a misconfigured emote hands out extra rewards.
        /// </summary>
        /// <param name="chain">Where the chain of the object running the gate stands (see Claim).</param>
        /// <param name="claim">When this lets the character through (false), the account's claim on the gate, for the object
        /// running the reward to release when its emotes have finished; otherwise null.</param>
        public static bool HasQuestAndCannotSolve(Player player, string questFormat, Func<ChainState> chain, out Claim claim)
        {
            claim = null;

            if (player == null)
                return true;

            var questName = GetQuestName(questFormat);
            if (string.IsNullOrWhiteSpace(questName))
            {
                LogRateLimited(false, $"[InqAccountQuest] An emote used by {player.Name} names no quest; treating it as done.");
                return true;
            }

            return Admit(player.Account?.AccountId, questName, chain, () =>
            {
                if (!TryReadAccountStamps(player, questName, out var stamps))
                    return null;

                // A quest with no world row is deliberately not failed closed: as with InqQuest (HasQuest && !CanSolve,
                // where QuestManager.GetNextSolveTime can't solve a missing quest), it stays open until a character on the
                // account has the stamp, then closes for good. Content keeps working when its InqQuest step becomes
                // InqAccountQuest. A world database that can't be read does fail closed.
                if (!TryGetWorldQuest(questName, out var quest))
                    return null;

                return IsDone(stamps, quest, ServerConfig.quest_mindelta_rate.Value, (uint)Time.GetUnixTime());
            }, out claim);
        }

        /// <summary>
        /// The gate behind HasQuestAndCannotSolve, with its reads passed in. The account's claim comes first and the reads
        /// after it: with landblocks ticking in parallel, stamps read before claiming could predate a stamp that another use
        /// lands and then releases, and this use would go ahead on the old read. With no account to claim for, it reads and
        /// decides without a claim.
        /// </summary>
        /// <param name="readIsDone">Reads the account's stamps and the world quest: true = done, false = go ahead, null = a
        /// database couldn't be read.</param>
        /// <returns>True (QuestSuccess) when another use holds the gate, the account is done or a read failed, with this
        /// use's claim released; false (go ahead) with the claim held.</returns>
        internal static bool Admit(uint? accountId, string questName, Func<ChainState> chain, Func<bool?> readIsDone, out Claim claim)
        {
            claim = null;

            Claim taken = null;
            if (accountId.HasValue)
            {
                taken = TryClaim(accountId.Value, questName, chain);
                if (taken == null)
                    return true;   // another use on the account holds the gate
            }

            bool? done;
            try
            {
                done = readIsDone();
            }
            catch
            {
                Release(taken);
                throw;
            }

            if (done != false)
            {
                Release(taken);
                return true;
            }

            claim = taken;
            return false;
        }

        /// <summary>
        /// The account-wide values behind %aqt and %aqc, or null when the emote names no quest or the shard or world
        /// database can't be read (the text then shows nothing in their place).
        /// </summary>
        public static AccountStatus? GetStatus(Player player, string questFormat)
        {
            if (player == null)
                return null;

            var questName = GetQuestName(questFormat);
            if (string.IsNullOrWhiteSpace(questName) || !TryReadAccountStamps(player, questName, out var stamps) || !TryGetWorldQuest(questName, out var quest))
                return null;

            var nextSolveTime = StatusNextSolveTime(stamps, quest, ServerConfig.quest_mindelta_rate.Value, (uint)Time.GetUnixTime());
            var hold = nextSolveTime == TimeSpan.MinValue && player.Account != null ? GetGateHold(player.Account.AccountId, questName) : GateHold.None;

            return new AccountStatus(nextSolveTime, Completions(stamps), hold);
        }

        /// <summary>
        /// Takes the account's claim on a quest's gate, or returns null while another chain holds it: one that is Running, one
        /// whose yes/no question is still open, or one that has acted and can't finish. A Finished chain's claim is replaced
        /// as it is; a Gone chain's claim that never acted, or one whose question closed unanswered, is fenced and replaced.
        /// Quest names match without regard to case, as in the registry.
        /// </summary>
        /// <param name="chain">Where the chain taking the claim stands (see Claim.Chain).</param>
        internal static Claim TryClaim(uint accountId, string questName, Func<ChainState> chain)
        {
            ArgumentNullException.ThrowIfNull(chain);

            var key = (accountId, questName.ToLowerInvariant());

            lock (claimLock)
            {
                if (claims.TryGetValue(key, out var held) && !TryEnd(held))
                    return null;

                var claim = new Claim(key, chain);
                claims[key] = claim;

                // claims whose chain is over or can be taken over are dropped now and then, so the table stays small
                if (claims.Count > 1024)
                {
                    foreach (var stale in claims.Where(c => c.Value != claim && TryEnd(c.Value)).Select(c => c.Key).ToList())
                        claims.Remove(stale);
                }

                return claim;
            }
        }

        /// <summary>
        /// Under claimLock: whether a claim may end for another to take its place. Not while its chain is Running or its
        /// question is open, nor once a chain that can't finish has acted (fails closed). A Gone chain that never acted is
        /// fenced here, so it never will. A parked claim whose question has closed is over whatever its object does next.
        /// </summary>
        private static bool TryEnd(Claim held)
        {
            if (held.Closed || held.AwaitingAnswerUntil > DateTime.UtcNow)
                return false;

            if (!held.ParkedForAnswer)
            {
                switch (held.Chain())
                {
                    case ChainState.Finished:
                        break;

                    case ChainState.Gone when !held.Acted:
                        held.Fenced = true;
                        break;

                    default:
                        return false;
                }
            }

            FenceIfUnanswered(held);
            return true;
        }

        /// <summary>
        /// Under claimLock, for a claim that is ending: if its chain asked a yes/no question that went unanswered, fences it,
        /// so an answer that still turns up (a confirmation handled late) can't act on it once another use may hold the gate.
        /// </summary>
        private static void FenceIfUnanswered(Claim claim)
        {
            if (claim.AwaitingAnswerUntil != DateTime.MinValue)
                claim.Fenced = true;
        }

        /// <summary>
        /// Ends a claim: its chain is over, or its use didn't go ahead. One replaced since, or closed, is left alone. One whose
        /// chain asked a yes/no question that is still open stays, parked for the answer, which carries it on; one whose
        /// question closed unanswered is fenced as it ends (FenceIfUnanswered).
        /// </summary>
        internal static void Release(Claim claim)
        {
            if (claim == null)
                return;

            lock (claimLock)
            {
                if (claim.Closed)
                    return;

                if (claim.AwaitingAnswerUntil > DateTime.UtcNow)
                {
                    claim.ParkedForAnswer = true;
                    return;
                }

                FenceIfUnanswered(claim);
                RemoveIfCurrent(claim);
            }
        }

        /// <summary>Under claimLock: drops a claim from the table unless another has taken its place.</summary>
        private static void RemoveIfCurrent(Claim claim)
        {
            if (IsCurrent(claim))
                claims.Remove(claim.Key);
        }

        /// <summary>
        /// A chain holding these claims asked a yes/no question (InqYesNo): the claims hold until the answer comes
        /// (TryTakeAnswer, AnswerNotTaken) or the question has been open for <paramref name="wait"/>, after which an answer
        /// is no longer taken: claims whose emotes ended meanwhile are then over (see TryEnd), the rest end with their
        /// emotes, and either way, fenced against an answer that still turns up (FenceIfUnanswered).
        /// </summary>
        internal static void AwaitAnswer(IReadOnlyCollection<Claim> chainClaims, TimeSpan wait)
        {
            if (chainClaims == null || chainClaims.Count == 0)
                return;

            lock (claimLock)
            {
                var until = DateTime.UtcNow + wait;

                foreach (var claim in chainClaims)
                    claim.AwaitingAnswerUntil = until;
            }
        }

        /// <summary>
        /// The answer to the chain's question has come, for an object to run as part of the chain. True while the question is
        /// still open (its deadline not passed) and the claims still hold the gate, with no other emotes holding them: either
        /// the asking emotes are over (parked) or the object running the answer is the one holding them. From here the claims
        /// follow that object's emotes (<paramref name="chain"/>), so they only ever have one holder. Otherwise false, and the
        /// answer must not run: the question had closed, the gate was taken over, or another object's asking emotes still
        /// hold the claims (they keep them); the claims end as for AnswerNotTaken.
        /// </summary>
        /// <param name="heldByAnswerer">Whether the object running the answer holds the claims already (its asking emotes
        /// are still in flight).</param>
        internal static bool TryTakeAnswer(IReadOnlyCollection<Claim> chainClaims, Func<ChainState> chain, bool heldByAnswerer)
        {
            ArgumentNullException.ThrowIfNull(chain);

            if (chainClaims == null || chainClaims.Count == 0)
                return true;

            lock (claimLock)
            {
                var now = DateTime.UtcNow;

                if (!chainClaims.All(c => !c.Closed && !c.Fenced && c.AwaitingAnswerUntil > now && IsCurrent(c) && (heldByAnswerer || c.ParkedForAnswer)))
                {
                    foreach (var claim in chainClaims)
                        EndUntaken(claim);

                    return false;
                }

                foreach (var claim in chainClaims)
                {
                    claim.AwaitingAnswerUntil = DateTime.MinValue;
                    claim.ParkedForAnswer = false;
                    claim.FollowChain(chain);
                }

                return true;
            }
        }

        /// <summary>
        /// The answer to the chain's question has come and won't run (the object is busy or gone, or has no set for that
        /// answer, or the player is gone): the claims end as they would have with no question. If the asking emotes are
        /// over, at once (fenced and released); otherwise they hold the claims until they end.
        /// </summary>
        internal static void AnswerNotTaken(IReadOnlyCollection<Claim> chainClaims)
        {
            if (chainClaims == null || chainClaims.Count == 0)
                return;

            lock (claimLock)
            {
                foreach (var claim in chainClaims)
                    EndUntaken(claim);
            }
        }

        /// <summary>Under claimLock: a claim whose answer won't run. The question is over either way.</summary>
        private static void EndUntaken(Claim claim)
        {
            var parked = claim.ParkedForAnswer;

            claim.AwaitingAnswerUntil = DateTime.MinValue;
            claim.ParkedForAnswer = false;

            if (parked && !claim.Closed)
            {
                claim.Fenced = true;
                RemoveIfCurrent(claim);
            }
        }

        /// <summary>Under claimLock: whether the claim still holds its gate (none has taken its place).</summary>
        private static bool IsCurrent(Claim claim) => claims.TryGetValue(claim.Key, out var held) && ReferenceEquals(held, claim);

        /// <summary>
        /// For a chain that stops before it finishes because its target left the world to log out, after which a stamp
        /// would no longer be saved: claims it never acted on are released, so the account can try again; claims it acted
        /// on are closed, so the gate stays closed for the account until a restart rather than let a reward through twice.
        /// Safe to call again for the same chain.
        /// </summary>
        internal static void Abandon(IReadOnlyCollection<Claim> chainClaims)
        {
            if (chainClaims == null || chainClaims.Count == 0)
                return;

            lock (claimLock)
            {
                foreach (var claim in chainClaims)
                {
                    if (claim.Acted)
                        claim.Close();
                    else
                        RemoveIfCurrent(claim);
                }
            }
        }

        /// <summary>
        /// Whether a claim keeps the account's gate on this quest closed for a reason other than a stamp: InProgress for a
        /// chain still running or a question it asked still open (its reward is on its way), ClosedUntilRestart for one that
        /// acted and can't finish. None once a question has closed with no answer taking the claim on.
        /// </summary>
        internal static GateHold GetGateHold(uint accountId, string questName)
        {
            var key = (accountId, questName.ToLowerInvariant());

            lock (claimLock)
            {
                if (!claims.TryGetValue(key, out var held))
                    return GateHold.None;

                if (held.Closed)
                    return GateHold.ClosedUntilRestart;

                if (held.AwaitingAnswerUntil > DateTime.UtcNow)
                    return GateHold.InProgress;

                if (held.ParkedForAnswer)
                    return GateHold.None;

                switch (held.Chain())
                {
                    case ChainState.Running:
                        return GateHold.InProgress;

                    case ChainState.Gone when held.Acted:
                        return GateHold.ClosedUntilRestart;

                    default:
                        return GateHold.None;
                }
            }
        }

        /// <summary>
        /// Before each step of a chain holding these claims: false if any of them was taken over, and the chain must stop;
        /// otherwise marks them as having acted, so none of them can be taken over from then on, including while what the
        /// step queues (a Give, a CastSpell) runs. Runs under the lock TryClaim takes, so a chain either acts first and keeps
        /// its claims, or is fenced first and never acts. No claims: true.
        /// </summary>
        internal static bool TryAct(IReadOnlyCollection<Claim> chainClaims)
        {
            if (chainClaims == null || chainClaims.Count == 0)
                return true;

            lock (claimLock)
            {
                if (chainClaims.Any(c => c.Fenced))
                    return false;

                foreach (var claim in chainClaims)
                    claim.Acted = true;

                return true;
            }
        }

        /// <summary>Drops the claim on a gate whatever its state, a closed one included. For tests, which share the table.</summary>
        internal static void Forget(uint accountId, string questName)
        {
            lock (claimLock)
                claims.Remove((accountId, questName.ToLowerInvariant()));
        }

        /// <summary>True once the claim was taken over: its chain may not act any more.</summary>
        internal static bool IsTakenOver(Claim claim)
        {
            if (claim == null)
                return false;

            lock (claimLock)
                return claim.Fenced;
        }

        /// <summary>The quest's world row (null when it has none), or false when the world database can't be read.</summary>
        private static bool TryGetWorldQuest(string questName, out WorldQuest quest)
        {
            try
            {
                quest = DatabaseManager.World.GetCachedQuest(questName);
                return true;
            }
            catch (Exception ex)
            {
                LogRateLimited(true, $"[InqAccountQuest] Could not read world quest '{questName}'. {ex}");
                quest = null;
                return false;
            }
        }

        /// <summary>The quest name without its optional @comment, or "" when there is none.</summary>
        private static string GetQuestName(string questFormat)
        {
            return string.IsNullOrEmpty(questFormat) ? "" : QuestManager.GetQuestName(questFormat);
        }

        /// <summary>
        /// Every stamp on the player's account for one quest (see the class summary for where each one is read from).
        /// False when the shard database can't be read.
        /// </summary>
        private static bool TryReadAccountStamps(Player player, string questName, out List<Stamp> stamps)
        {
            // Characters in memory: this one, and below, any other of the account still online.
            var inMemory = new List<(uint CharacterId, Stamp Stamp)>();
            var inMemoryIds = new HashSet<uint> { player.Guid.Full };

            var own = player.QuestManager.GetQuest(questName);
            if (own != null)
                inMemory.Add((player.Guid.Full, new Stamp(own.LastTimeCompleted, own.NumTimesCompleted)));

            var saved = new List<(uint CharacterId, Stamp Stamp)>();

            if (player.Account != null)
            {
                var accountId = player.Account.AccountId;

                foreach (var other in PlayerManager.GetAllOnline())
                {
                    if (other == player || other.Account == null || other.Account.AccountId != accountId)
                        continue;

                    inMemoryIds.Add(other.Guid.Full);

                    var otherQuest = other.QuestManager.GetQuest(questName);
                    if (otherQuest != null)
                        inMemory.Add((other.Guid.Full, new Stamp(otherQuest.LastTimeCompleted, otherQuest.NumTimesCompleted)));
                }

                try
                {
                    using (var context = new ShardDbContext())
                    {
                        var rows = (from q in context.CharacterPropertiesQuestRegistry
                                    join c in context.Character on q.CharacterId equals c.Id
                                    where c.AccountId == accountId && q.QuestName == questName
                                    select new { q.CharacterId, q.LastTimeCompleted, q.NumTimesCompleted }).ToList();

                        saved.AddRange(rows.Select(r => (r.CharacterId, new Stamp(r.LastTimeCompleted, r.NumTimesCompleted))));
                    }
                }
                catch (Exception ex)
                {
                    LogRateLimited(true, $"[InqAccountQuest] Could not read quest '{questName}' for account {accountId}. {ex}");
                    stamps = null;
                    return false;
                }
            }

            stamps = AccountStamps(inMemory, saved, inMemoryIds);
            return true;
        }

        /// <summary>
        /// The account's stamps for one quest: every character in memory, then the saved row of every character that is not.
        /// A character in memory is never also read from its saved row: that row can be older than its memory while a logout
        /// save is still queued, and a stamp it lacks in memory (erased, say) is gone even if the row is not yet.
        /// </summary>
        /// <param name="inMemory">Stamps read from characters in memory (the online character and any other still online).</param>
        /// <param name="saved">Saved rows from the shard database, every character of the account.</param>
        /// <param name="inMemoryIds">Every character read from memory, with or without a stamp.</param>
        public static List<Stamp> AccountStamps(IEnumerable<(uint CharacterId, Stamp Stamp)> inMemory, IEnumerable<(uint CharacterId, Stamp Stamp)> saved, ISet<uint> inMemoryIds)
        {
            var stamps = inMemory.Select(m => m.Stamp).ToList();
            stamps.AddRange(saved.Where(s => !inMemoryIds.Contains(s.CharacterId)).Select(s => s.Stamp));
            return stamps;
        }

        /// <summary>
        /// The pure rule behind InqAccountQuest: QuestManager.HasQuest and CanSolve applied to every stamp on an account.
        /// True = QuestSuccess (done, or the timer is still running); false = QuestFailure (go ahead).
        /// </summary>
        /// <param name="stamps">Every character's registry row for the quest (<see cref="AccountStamps"/>).</param>
        /// <param name="quest">The world quest, or null if it doesn't exist.</param>
        /// <param name="minDeltaRate">ServerConfig.quest_mindelta_rate.</param>
        /// <param name="currentTime">Now, in unix seconds.</param>
        public static bool IsDone(IReadOnlyCollection<Stamp> stamps, WorldQuest quest, double minDeltaRate, uint currentTime)
        {
            // HasQuest: a registry row with NumTimesCompleted >= 0. CanSolve: GetNextSolveTime is MinValue.
            return stamps.Any(s => s.NumTimesCompleted >= 0) && NextSolveTime(stamps, quest, minDeltaRate, currentTime) != TimeSpan.MinValue;
        }

        /// <summary>
        /// QuestManager.GetNextSolveTime for a whole account: TimeSpan.MaxValue when the quest can't be solved again (it is
        /// missing from the world, or the account reached MaxSolves), TimeSpan.MinValue when it can be solved now, otherwise
        /// the time left on the timer that the account's newest completion started. Rows with a negative count don't count
        /// as having the quest, as in QuestManager.HasQuest.
        /// </summary>
        public static TimeSpan NextSolveTime(IReadOnlyCollection<Stamp> stamps, WorldQuest quest, double minDeltaRate, uint currentTime)
        {
            // A quest missing from the world can't be solved.
            if (quest == null)
                return TimeSpan.MaxValue;

            var held = stamps.Where(s => s.NumTimesCompleted >= 0).ToList();
            if (held.Count == 0)
                return TimeSpan.MinValue;

            // Max solves, against the account's total (-1 = unlimited).
            if (quest.MaxSolves > -1 && held.Sum(s => (long)s.NumTimesCompleted) >= quest.MaxSolves)
                return TimeSpan.MaxValue;

            // The timer runs from the newest completion on the account. Summed as ulong so a very large MinDelta
            // can't wrap around to "solvable".
            var minDelta = QuestManager.CanScaleQuestMinDelta(quest) ? (uint)(quest.MinDelta * minDeltaRate) : quest.MinDelta;
            var nextSolveTime = (ulong)held.Max(s => s.LastTimeCompleted) + minDelta;

            if (currentTime >= nextSolveTime)
                return TimeSpan.MinValue;

            return TimeSpan.FromSeconds((double)(nextSolveTime - currentTime));
        }

        /// <summary>
        /// The time %aqt shows: NextSolveTime, except for a quest with no world row that no character on the account has.
        /// That one is open (IsDone lets its first use through, as InqQuest does), so it reads as now, like a quest with a
        /// row and no stamps, and a claim holding the gate meanwhile shows as such. Once a character has it, it is MaxValue.
        /// </summary>
        public static TimeSpan StatusNextSolveTime(IReadOnlyCollection<Stamp> stamps, WorldQuest quest, double minDeltaRate, uint currentTime)
        {
            if (quest == null && !stamps.Any(s => s.NumTimesCompleted >= 0))
                return TimeSpan.MinValue;

            return NextSolveTime(stamps, quest, minDeltaRate, currentTime);
        }

        /// <summary>The account's total completions of the quest (rows with a negative count are skipped).</summary>
        public static long Completions(IEnumerable<Stamp> stamps)
        {
            return stamps.Where(s => s.NumTimesCompleted >= 0).Sum(s => (long)s.NumTimesCompleted);
        }

        // ── IP-locked content (InqIPQuest, EmoteType 156) ────────────────────────────────────────────────────────────────
        //
        // "N per IP address": the quest's MinDelta window counted across every character on every account in the player's
        // IP group, which is
        //   - the player's own account,
        //   - every account with a character online from the same IP address now,
        //   - every account whose last login came from that address (account.last_Login_I_P).
        // The gate is done (QuestSuccess) once that many characters in the group completed the quest within the last
        // MinDelta seconds. It sits beside InqAccountQuest, it doesn't replace it: keep both for "once per account, at most
        // N per IP". An address in AllowUnlimitedSessionsFromIPAddresses is not limited (shared offices, the server's own
        // LAN). Claims use the account gate's machinery under a key of account 0 (no real account has id 0), so two
        // characters on one address can't both pass before the first stamp lands.

        private const string IpKeyPrefix = "ip:";

        /// <summary>What %iqt and %iqc show: the time until the IP group can do the quest again and how many characters in
        /// the group did it within the window.</summary>
        public readonly record struct IpStatus(TimeSpan NextSolveTime, int InWindow, int Limit, GateHold Hold = GateHold.None);

        /// <summary>
        /// The IP-wide test: true (QuestSuccess) when <paramref name="limit"/> or more characters in the player's IP group
        /// completed the quest within its MinDelta window, when another use on the address holds the gate, or when a database
        /// can't be read (fails closed). False lets the character through and returns the address's claim in
        /// <paramref name="claim"/>, released by EmoteManager when the object's emotes have finished.
        /// </summary>
        public static bool IpHasQuestAndCannotSolve(Player player, string questFormat, int limit, Func<ChainState> chain, out Claim claim)
        {
            claim = null;

            if (player == null)
                return true;

            var questName = GetQuestName(questFormat);
            if (string.IsNullOrWhiteSpace(questName) || limit < 1)
            {
                LogRateLimited(false, $"[InqIPQuest] An emote used by {player.Name} names no quest or a limit below 1; treating it as done.");
                return true;
            }

            var address = PlayerAddress(player);
            if (address == null)
                return true;

            if (IsUnlimitedAddress(address))
                return false;

            claim = null;
            var taken = TryClaim(0, IpKeyPrefix + address + "|" + questName, chain);
            if (taken == null)
                return true;   // another use on this address holds the gate

            bool done;
            try
            {
                done = !TryReadIpStamps(player, address, questName, out var stamps) || !TryGetWorldQuest(questName, out var quest)
                    || IpIsDone(stamps, quest, limit, ServerConfig.quest_mindelta_rate.Value, (uint)Time.GetUnixTime());
            }
            catch
            {
                Release(taken);
                throw;
            }

            if (done)
            {
                Release(taken);
                return true;
            }

            claim = taken;
            return false;
        }

        /// <summary>The values behind %iqt and %iqc, or null when they can't be read.</summary>
        public static IpStatus? GetIpStatus(Player player, string questFormat, int limit)
        {
            if (player == null || limit < 1)
                return null;

            var questName = GetQuestName(questFormat);
            var address = PlayerAddress(player);
            if (string.IsNullOrWhiteSpace(questName) || address == null)
                return null;

            if (IsUnlimitedAddress(address))
                return new IpStatus(TimeSpan.MinValue, 0, limit);

            if (!TryReadIpStamps(player, address, questName, out var stamps) || !TryGetWorldQuest(questName, out var quest))
                return null;

            var now = (uint)Time.GetUnixTime();
            var next = IpNextSolveTime(stamps, quest, limit, ServerConfig.quest_mindelta_rate.Value, now);
            var hold = next == TimeSpan.MinValue ? GetGateHold(0, IpKeyPrefix + address + "|" + questName) : GateHold.None;

            return new IpStatus(next, IpInWindow(stamps, quest, ServerConfig.quest_mindelta_rate.Value, now), limit, hold);
        }

        /// <summary>The pure rule behind InqIPQuest: true when at least <paramref name="limit"/> stamps fall within the quest's
        /// MinDelta window. A quest missing from the world never limits (InqAccountQuest decides those).</summary>
        public static bool IpIsDone(IReadOnlyCollection<Stamp> stamps, WorldQuest quest, int limit, double minDeltaRate, uint currentTime)
        {
            return quest != null && IpInWindow(stamps, quest, minDeltaRate, currentTime) >= limit;
        }

        /// <summary>How many stamps (characters) completed the quest within its MinDelta window.</summary>
        public static int IpInWindow(IReadOnlyCollection<Stamp> stamps, WorldQuest quest, double minDeltaRate, uint currentTime)
        {
            if (quest == null)
                return 0;

            var minDelta = QuestManager.CanScaleQuestMinDelta(quest) ? (uint)(quest.MinDelta * minDeltaRate) : quest.MinDelta;

            return stamps.Count(s => s.NumTimesCompleted > 0 && (ulong)s.LastTimeCompleted + minDelta > currentTime);
        }

        /// <summary>Time until fewer than <paramref name="limit"/> stamps are in the window (TimeSpan.MinValue = now).</summary>
        public static TimeSpan IpNextSolveTime(IReadOnlyCollection<Stamp> stamps, WorldQuest quest, int limit, double minDeltaRate, uint currentTime)
        {
            if (quest == null)
                return TimeSpan.MinValue;

            var minDelta = QuestManager.CanScaleQuestMinDelta(quest) ? (uint)(quest.MinDelta * minDeltaRate) : quest.MinDelta;

            var ends = stamps.Where(s => s.NumTimesCompleted > 0)
                .Select(s => (ulong)s.LastTimeCompleted + minDelta)
                .Where(end => end > currentTime)
                .OrderBy(end => end)
                .ToList();

            if (ends.Count < limit)
                return TimeSpan.MinValue;

            // the group is under the limit again once (count - limit + 1) of them have run out
            return TimeSpan.FromSeconds((double)(ends[ends.Count - limit] - currentTime));
        }

        private static string PlayerAddress(Player player) => player.Session?.EndPoint?.Address?.ToString();

        private static bool IsUnlimitedAddress(string address)
        {
            var unlimited = ConfigManager.Config.Server.Network.AllowUnlimitedSessionsFromIPAddresses;
            return unlimited != null && unlimited.Contains(address);
        }

        /// <summary>
        /// Every stamp in the player's IP group for one quest: characters in memory first (online ones of the group's accounts),
        /// then the saved row of every other character of those accounts. False when a database can't be read.
        /// </summary>
        private static bool TryReadIpStamps(Player player, string address, string questName, out List<Stamp> stamps)
        {
            stamps = null;

            var accountIds = new HashSet<uint>();
            if (player.Account != null)
                accountIds.Add(player.Account.AccountId);

            var online = PlayerManager.GetAllOnline();

            foreach (var other in online)
            {
                if (other.Account != null && PlayerAddress(other) == address)
                    accountIds.Add(other.Account.AccountId);
            }

            try
            {
                var addressBytes = System.Net.IPAddress.Parse(address).GetAddressBytes();

                using (var auth = new ACE.Database.Models.Auth.AuthDbContext())
                {
                    foreach (var id in auth.Account.Where(a => a.LastLoginIP == addressBytes).Select(a => a.AccountId).ToList())
                        accountIds.Add(id);
                }
            }
            catch (Exception ex)
            {
                LogRateLimited(true, $"[InqIPQuest] Could not read the accounts last seen on {address}. {ex}");
                return false;
            }

            var inMemory = new List<(uint CharacterId, Stamp Stamp)>();
            var inMemoryIds = new HashSet<uint>();

            foreach (var other in online)
            {
                if (other.Account == null || !accountIds.Contains(other.Account.AccountId))
                    continue;

                inMemoryIds.Add(other.Guid.Full);

                var q = other.QuestManager.GetQuest(questName);
                if (q != null)
                    inMemory.Add((other.Guid.Full, new Stamp(q.LastTimeCompleted, q.NumTimesCompleted)));
            }

            if (!inMemoryIds.Contains(player.Guid.Full))
            {
                inMemoryIds.Add(player.Guid.Full);
                var own = player.QuestManager.GetQuest(questName);
                if (own != null)
                    inMemory.Add((player.Guid.Full, new Stamp(own.LastTimeCompleted, own.NumTimesCompleted)));
            }

            var saved = new List<(uint CharacterId, Stamp Stamp)>();

            try
            {
                using (var context = new ShardDbContext())
                {
                    var ids = accountIds.ToList();
                    var rows = (from q in context.CharacterPropertiesQuestRegistry
                                join c in context.Character on q.CharacterId equals c.Id
                                where ids.Contains(c.AccountId) && q.QuestName == questName
                                select new { q.CharacterId, q.LastTimeCompleted, q.NumTimesCompleted }).ToList();

                    saved.AddRange(rows.Select(r => (r.CharacterId, new Stamp(r.LastTimeCompleted, r.NumTimesCompleted))));
                }
            }
            catch (Exception ex)
            {
                LogRateLimited(true, $"[InqIPQuest] Could not read quest '{questName}' for the accounts on {address}. {ex}");
                return false;
            }

            stamps = AccountStamps(inMemory, saved, inMemoryIds);
            return true;
        }

        /// <summary>Logs at error or warning level at most once a minute; otherwise at debug level.</summary>
        private static void LogRateLimited(bool error, string message)
        {
            var now = Time.GetUnixTime();
            if (now < nextLogAt)
            {
                log.Debug(message);
                return;
            }

            nextLogAt = now + LogEverySeconds;

            if (error)
                log.Error(message);
            else
                log.Warn(message);
        }
    }
}
