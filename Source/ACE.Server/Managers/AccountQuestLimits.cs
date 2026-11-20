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
    /// After the gate lets a character through, the account holds a Claim on the quest's gate until the object running
    /// the reward has finished its emotes (or ClaimTimeout), so a second check can't pass before the stamp lands.
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
        /// a while after the gate, and until it does the account's stamps don't show the claim. So when the gate lets a
        /// character through, the account takes a Claim, and the gate stays closed for the account until the object running
        /// the reward has finished its emotes: the reward and the stamp, or a stop such as a full pack (EmoteManager releases
        /// its claims when it goes idle). ClaimTimeout ends a claim whose object never goes idle.
        /// </summary>
        public sealed class Claim
        {
            internal Claim((uint AccountId, string Quest) key, DateTime expires)
            {
                Key = key;
                Expires = expires;
            }

            internal (uint AccountId, string Quest) Key { get; }

            internal DateTime Expires { get; }
        }

        /// <summary>A claim ends at the latest this long after it was taken, released or not.</summary>
        public static readonly TimeSpan ClaimTimeout = TimeSpan.FromMinutes(5);

        private static readonly Dictionary<(uint AccountId, string Quest), Claim> claims = new();
        private static readonly object claimLock = new();

        /// <summary>One character's registry row for a quest.</summary>
        public readonly record struct Stamp(uint LastTimeCompleted, int NumTimesCompleted);

        /// <summary>
        /// What the %aqt and %aqc emote text show for an account: the time until the account can solve the quest again
        /// (TimeSpan.MinValue = now, TimeSpan.MaxValue = never, as QuestManager.GetNextSolveTime returns them) and the
        /// account's total completions.
        /// </summary>
        public readonly record struct AccountStatus(TimeSpan NextSolveTime, long Completions);

        /// <summary>
        /// The account-wide InqQuest test (HasQuest &amp;&amp; !CanSolve): true when a character on the player's account has the
        /// quest and the account can't solve it right now, or when another use on the account holds the quest's gate (Claim).
        /// Fails closed (true) when the database can't be read or the emote names no quest, so neither a database problem nor
        /// a misconfigured emote hands out extra rewards.
        /// </summary>
        /// <param name="claim">When this lets the character through (false), the account's claim on the gate, for the object
        /// running the reward to release when it goes idle; otherwise null.</param>
        public static bool HasQuestAndCannotSolve(Player player, string questFormat, out Claim claim)
        {
            claim = null;

            if (player == null)
                return true;

            var questName = GetQuestName(questFormat);
            if (questName.Length == 0)
            {
                LogRateLimited(false, $"[InqAccountQuest] An emote used by {player.Name} names no quest; treating it as done.");
                return true;
            }

            if (!TryReadAccountStamps(player, questName, out var stamps))
                return true;

            // A quest with no world row is deliberately not failed closed: as with InqQuest (HasQuest && !CanSolve, where
            // QuestManager.GetNextSolveTime can't solve a missing quest), it stays open until a character on the account
            // has the stamp, then closes for good. Content keeps working when its InqQuest step becomes InqAccountQuest.
            // A world database that can't be read does fail closed.
            if (!TryGetWorldQuest(questName, out var quest))
                return true;

            if (IsDone(stamps, quest, ServerConfig.quest_mindelta_rate.Value, (uint)Time.GetUnixTime()))
                return true;

            // Going ahead: claim the gate for the account until the object running the reward goes idle (see Claim).
            if (player.Account == null)
                return false;

            claim = TryClaim(player.Account.AccountId, questName, DateTime.UtcNow);
            return claim == null;
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
            if (questName.Length == 0 || !TryReadAccountStamps(player, questName, out var stamps) || !TryGetWorldQuest(questName, out var quest))
                return null;

            return new AccountStatus(NextSolveTime(stamps, quest, ServerConfig.quest_mindelta_rate.Value, (uint)Time.GetUnixTime()), Completions(stamps));
        }

        /// <summary>
        /// Takes the account's claim on a quest's gate, or returns null while another one is held (not released, not past
        /// ClaimTimeout). Quest names match without regard to case, as in the registry.
        /// </summary>
        internal static Claim TryClaim(uint accountId, string questName, DateTime now)
        {
            var key = (accountId, questName.ToLowerInvariant());

            lock (claimLock)
            {
                if (claims.TryGetValue(key, out var held) && held.Expires > now)
                    return null;

                var claim = new Claim(key, now + ClaimTimeout);
                claims[key] = claim;

                // claims past their timeout are dropped now and then, so the table stays small
                if (claims.Count > 1024)
                {
                    foreach (var expired in claims.Where(c => c.Value.Expires <= now).Select(c => c.Key).ToList())
                        claims.Remove(expired);
                }

                return claim;
            }
        }

        /// <summary>Ends a claim. One that timed out and was taken again since is left alone.</summary>
        internal static void Release(Claim claim)
        {
            if (claim == null)
                return;

            lock (claimLock)
            {
                if (claims.TryGetValue(claim.Key, out var held) && ReferenceEquals(held, claim))
                    claims.Remove(claim.Key);
            }
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

        /// <summary>The account's total completions of the quest (rows with a negative count are skipped).</summary>
        public static long Completions(IEnumerable<Stamp> stamps)
        {
            return stamps.Where(s => s.NumTimesCompleted >= 0).Sum(s => (long)s.NumTimesCompleted);
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
