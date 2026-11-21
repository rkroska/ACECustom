using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using log4net;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Server.WorldObjects.Managers;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Bank lock (2026-11-20): /banklock freezes one character's bank until /bankunlock (BankAdminCommands.cs). While
    /// PropertyBool.BankLocked is set on the character:
    /// <list type="number">
    /// <item>No banked currency changes. Every write of one goes through WorldObject or OfflinePlayer SetProperty /
    /// RemoveProperty (where the pyreal and economy ledgers hook in too), and those refuse it - so deposits, withdrawals,
    /// transfers either way, kill / quest / event income, vendor sales and purchases, fees, refunds, Powerball, the casino
    /// and emotes are all stopped, and so is any path added later. Staff commands that write inside <see cref="AdminWrite"/>
    /// (/setproperty, /bankwipe) still get through.</item>
    /// <item>The bank reads as empty to the game. The Player balance properties (Player_Bank.cs) and the InqInt64Stat emote
    /// return 0 for it, so every "can you pay" test that counts the bank sees nothing there, and nothing is handed over
    /// against a debit that would then be refused.</item>
    /// <item>The ways in that a player uses take nothing and say why: /bank and /b, /clap, using a lum token, selling to a
    /// vendor, handing an NPC an item it deals with through the bank (<see cref="UsesBank"/>), the Paragon Collector, an
    /// enlightenment gate fee the pack's coins can't cover, and a transfer TO a locked character (refused before anything
    /// leaves the sender's bank; the sender is not told why).</item>
    /// <item>An emote set that can still reach a step on a banked currency - its own, or a set it continues in - stops before
    /// its next step for a locked player (EmoteManager.DoEnqueue, <see cref="ReachesBankStep"/>), and the stop is logged
    /// (<see cref="NoteStoppedSet"/>): nothing runs around a refused bank step.</item>
    /// </list>
    /// Income nobody hands anything over for is not paid while the bank is locked, and says so: kill and quest luminance
    /// ("was not banked"), Zone Control Prestige Coins, Powerball winnings (the log says what to credit by hand), refunds.
    /// Kill pyreals stay on the corpse to be looted, and allegiance luminance waits in its cache until the bank is
    /// unlocked. Staff reads (/getproperty, /propertydump, /bankwipe, /banklock) and the ledgers read the stored balances.
    /// </summary>
    public static class BankLock
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>The eight banked currencies, in /b order, with the names /b gives them.</summary>
        public static readonly IReadOnlyList<(PropertyInt64 Property, string Name)> Currencies = new (PropertyInt64 Property, string Name)[]
        {
            (PropertyInt64.BankedPyreals, "Pyreals"),
            (PropertyInt64.BankedLuminance, "Luminance"),
            (PropertyInt64.BankedLegendaryKeys, "Legendary Keys"),
            (PropertyInt64.BankedMythicalKeys, "Mythical Keys"),
            (PropertyInt64.BankedEnlightenedCoins, "Enlightened Coins"),
            (PropertyInt64.BankedWeaklyEnlightenedCoins, "Weakly Enlightened Coins"),
            (PropertyInt64.BankedPrestigeCoins, "Prestige Coins"),
            (PropertyInt64.BankedEnlightenmentTokens, "Enlightenment Tokens"),
        };

        /// <summary>True for the eight banked currencies - the same eight as <see cref="Currencies"/> (BankLockTests checks both).</summary>
        public static bool IsBankProperty(PropertyInt64 property)
        {
            switch (property)
            {
                case PropertyInt64.BankedPyreals:
                case PropertyInt64.BankedLuminance:
                case PropertyInt64.BankedLegendaryKeys:
                case PropertyInt64.BankedMythicalKeys:
                case PropertyInt64.BankedEnlightenedCoins:
                case PropertyInt64.BankedWeaklyEnlightenedCoins:
                case PropertyInt64.BankedPrestigeCoins:
                case PropertyInt64.BankedEnlightenmentTokens:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>True when staff locked this character's bank. Online or offline.</summary>
        public static bool IsLocked(IPlayer character) => character != null && (character.GetProperty(PropertyBool.BankLocked) ?? false);

        // ---- staff writes ---------------------------------------------------------------------------------------------

        // Chat commands run on the world thread, and a staff write is made in the command itself, so a count per thread is
        // enough: a write on any other thread meanwhile is still refused.
        [ThreadStatic]
        private static int adminWriteDepth;

        /// <summary>
        /// Opens a staff write: until it is disposed, writes on this thread reach a locked bank. /setproperty and /bankwipe
        /// write inside one, so staff can still set or clear a locked balance. Use it in a using statement, and write absolute
        /// values with SetProperty / RemoveProperty: the Player balance properties still read 0 for a locked bank, so a
        /// read-modify-write through them (BankedPyreals += x) would replace the real balance.
        /// </summary>
        public static IDisposable AdminWrite()
        {
            adminWriteDepth++;
            return new AdminWriteScope();
        }

        /// <summary>True while a staff write is open on this thread.</summary>
        internal static bool AdminWriteOpen => adminWriteDepth > 0;

        private sealed class AdminWriteScope : IDisposable
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;
                adminWriteDepth--;
            }
        }

        // ---- the freeze -----------------------------------------------------------------------------------------------

        /// <summary>
        /// True when a write of <paramref name="property"/> on <paramref name="character"/> must not happen: it is a banked
        /// currency, that bank is locked, and no staff write is open on this thread. Called first in the property writers,
        /// so anything that is not a banked currency returns at the first test.
        /// </summary>
        public static bool RefusesWrite(IPlayer character, PropertyInt64 property)
        {
            // cheapest first: most writes are not to the bank, and a staff write needs no property lookup
            if (!IsBankProperty(property) || adminWriteDepth > 0 || !IsLocked(character))
                return false;

            NoteRefused(character, property);
            return true;
        }

        private static readonly ConcurrentDictionary<uint, DateTime> lastRefusalLogged = new ConcurrentDictionary<uint, DateTime>();

        /// <summary>One log line per character a minute, so a hunt on a locked character does not flood the log.</summary>
        private static void NoteRefused(IPlayer character, PropertyInt64 property)
        {
            var id = character.Guid.Full;
            var now = DateTime.UtcNow;

            if (lastRefusalLogged.TryGetValue(id, out var last) && now - last < TimeSpan.FromMinutes(1))
                return;

            lastRefusalLogged[id] = now;
            log.Info($"[BankLock] {character.Name} (0x{id:X8}): a change to {property} was refused - the bank is locked. Further refusals for this character are not logged for a minute.");
        }

        // ---- emotes ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// True when step <paramref name="fromIndex"/> or a later one of this set - or of any set those steps can continue in
        /// (Goto, the Inq tests, UpdateQuest: <see cref="BranchTargets"/>) - is an Int64 stat step (increment, decrement or set)
        /// on a banked currency: the lum token NPCs, and NPCs that charge or pay a banked currency by hand. For a locked player
        /// the set stops before its next step (EmoteManager.DoEnqueue). The bank would refuse that step while the chain ran on:
        /// a continuation is queued and runs after the steps that follow it, so a "Goto Pay, Give" set would hand over the item
        /// with the price never taken. A branch that may not be taken counts too - a locked player is stopped rather than paid
        /// around the bank.
        /// </summary>
        /// <param name="emoteSet">The set about to run its step <paramref name="fromIndex"/></param>
        /// <param name="fromIndex">The step about to run</param>
        /// <param name="allSets">All the NPC's emote sets (its Biota.PropertiesEmote), where its branches continue</param>
        public static bool ReachesBankStep(PropertiesEmote emoteSet, int fromIndex, IEnumerable<PropertiesEmote> allSets)
        {
            if (emoteSet?.PropertiesEmoteAction == null)
                return false;

            return new EmoteWalk(allSets).Outcome(emoteSet, fromIndex, 0).ReachesBankStep;
        }

        /// <summary>
        /// True when a locked player should keep an item rather than hand it to an NPC with this Give or Refuse emote set,
        /// because the NPC deals with it through the bank. The set is followed into every set its steps can continue in, and the
        /// hand-in is kept back when some run of the chain could
        /// <list type="bullet">
        /// <item>reach a step on a banked currency (<see cref="ReachesBankStep"/>: the chain would stop, after the item was
        /// taken), or</item>
        /// <item>pay luminance and nothing else (every luminance award is banked, Player_Luminance.AddLuminance - so it would pay
        /// nothing at all).</item>
        /// </list>
        /// Which way a test goes is not known in advance, so every outcome counts, and so does an outcome that runs no set (none
        /// is defined, or none is sure to be picked: it pays nothing). A chain that always pays something besides luminance - an
        /// item, XP, a skill, a spell, a stat, a teleport (<see cref="IsOtherReward"/>), in its own steps or in every set a
        /// continuation can run - goes ahead, and only its luminance is not paid. Quest stamps and chat are bookkeeping, not a
        /// reward.
        /// </summary>
        /// <param name="emoteSet">The Give or Refuse set the hand-in would run</param>
        /// <param name="allSets">All the NPC's emote sets (its Biota.PropertiesEmote), where its branches continue</param>
        public static bool UsesBank(PropertiesEmote emoteSet, IEnumerable<PropertiesEmote> allSets)
        {
            if (emoteSet?.PropertiesEmoteAction == null)
                return false;

            var outcome = new EmoteWalk(allSets).Outcome(emoteSet, 0, 0);
            return outcome.ReachesBankStep || outcome.CanPayOnlyLuminance;
        }

        private static bool IsBankStep(PropertiesEmoteAction action)
        {
            if (action == null)
                return false;

            switch ((EmoteType)action.Type)
            {
                case EmoteType.IncrementInt64Stat:
                case EmoteType.DecrementInt64Stat:
                case EmoteType.SetInt64Stat:
                    return action.Stat.HasValue && IsBankProperty((PropertyInt64)action.Stat.Value);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Where each branching step continues, as EmoteManager.ExecuteEmote dispatches it (and Confirmation_YesNo for InqYesNo):
        /// the set of one of these categories whose Quest is the step's Message. InqNumCharacterTitles is disabled there, so it
        /// continues nowhere.
        /// </summary>
        private static readonly Dictionary<EmoteType, EmoteCategory[]> BranchTargets = BuildBranchTargets();

        private static Dictionary<EmoteType, EmoteCategory[]> BuildBranchTargets()
        {
            var quest = new[] { EmoteCategory.QuestSuccess, EmoteCategory.QuestFailure };
            var fellowQuest = new[] { EmoteCategory.QuestSuccess, EmoteCategory.QuestFailure, EmoteCategory.QuestNoFellow };
            var statTest = new[] { EmoteCategory.TestSuccess, EmoteCategory.TestFailure, EmoteCategory.TestNoQuality };
            var test = new[] { EmoteCategory.TestSuccess, EmoteCategory.TestFailure };

            var targets = new Dictionary<EmoteType, EmoteCategory[]>
            {
                [EmoteType.Goto] = new[] { EmoteCategory.GotoSet },
                [EmoteType.InqEvent] = new[] { EmoteCategory.EventSuccess, EmoteCategory.EventFailure },
                [EmoteType.InqFellowNum] = new[] { EmoteCategory.NumFellowsSuccess, EmoteCategory.NumFellowsFailure, EmoteCategory.TestNoFellow },
                [EmoteType.InqFellowQuest] = fellowQuest,
                [EmoteType.UpdateFellowQuest] = fellowQuest,
                [EmoteType.InqNumCharacterTitles] = Array.Empty<EmoteCategory>(),
            };

            foreach (var type in new[] { EmoteType.InqQuest, EmoteType.InqMyQuest, EmoteType.InqQuestSolves, EmoteType.InqMyQuestSolves,
                EmoteType.InqServerQuestSolves, EmoteType.InqQuestBitsOn, EmoteType.InqQuestBitsOff, EmoteType.InqMyQuestBitsOn,
                EmoteType.InqMyQuestBitsOff, EmoteType.UpdateQuest, EmoteType.UpdateMyQuest, EmoteType.InqPetRegistryCount,
                EmoteType.InqPetRegistryCreatureType })
                targets[type] = quest;

            foreach (var type in new[] { EmoteType.InqBoolStat, EmoteType.InqIntStat, EmoteType.InqInt64Stat, EmoteType.InqFloatStat,
                EmoteType.InqStringStat, EmoteType.InqAttributeStat, EmoteType.InqRawAttributeStat, EmoteType.InqSecondaryAttributeStat,
                EmoteType.InqRawSecondaryAttributeStat, EmoteType.InqSkillStat, EmoteType.InqRawSkillStat, EmoteType.InqSkillTrained,
                EmoteType.InqSkillSpecialized, EmoteType.QuestCompletionCount })
                targets[type] = statTest;

            foreach (var type in new[] { EmoteType.InqOwnsItems, EmoteType.InqPackSpace, EmoteType.InqContractsFull, EmoteType.InqYesNo })
                targets[type] = test;

            return targets;
        }

        /// <summary>A branching step the table above does not know (a newer custom Inq test): every outcome category, to be safe.</summary>
        private static readonly EmoteCategory[] UnknownBranchTargets =
        {
            EmoteCategory.QuestSuccess, EmoteCategory.QuestFailure, EmoteCategory.QuestNoFellow,
            EmoteCategory.TestSuccess, EmoteCategory.TestFailure, EmoteCategory.TestNoQuality, EmoteCategory.TestNoFellow,
            EmoteCategory.EventSuccess, EmoteCategory.EventFailure,
            EmoteCategory.NumFellowsSuccess, EmoteCategory.NumFellowsFailure,
            EmoteCategory.NumCharacterTitlesSuccess, EmoteCategory.NumCharacterTitlesFailure,
        };

        /// <summary>The categories this step continues in, or null when it does not branch.</summary>
        private static EmoteCategory[] ContinuesIn(PropertiesEmoteAction action)
        {
            var type = (EmoteType)action.Type;
            if (BranchTargets.TryGetValue(type, out var categories))
                return categories;

            if (EmoteManager.EmoteIsBranchingType(action) || type.ToString().StartsWith("Inq", StringComparison.Ordinal))
                return UnknownBranchTargets;

            return null;
        }

        /// <summary>What the runs of an emote chain can come to (<see cref="EmoteWalk"/>).</summary>
        /// <param name="ReachesBankStep">Some run reaches a step on a banked currency</param>
        /// <param name="CanPayNoOtherReward">Some run pays no reward besides luminance (or nothing at all)</param>
        /// <param name="CanPayOnlyLuminance">Some run pays luminance and no other reward</param>
        private readonly record struct ChainOutcome(bool ReachesBankStep, bool CanPayNoOtherReward, bool CanPayOnlyLuminance);

        /// <summary>
        /// Follows one emote set and every set its steps can continue in, for one question. A set runs all its own steps, and each
        /// branching step adds one run of the sets it can continue in - or none, when the outcome reached has no set, or no set
        /// there is sure to be picked. Each set is worked out once from its first step (memoized), and a branch back into a set
        /// already being followed as a whole adds nothing (that set's own steps are counted where it was entered). A walk from a
        /// later step is not such a visit: a branch back into that set looks at the whole of it.
        /// </summary>
        private sealed class EmoteWalk
        {
            /// <summary>The emote engine aborts a chain nested past 100 (EmoteManager.Enqueue); a walk that deep assumes the worst.</summary>
            private const int MaxBranchDepth = 100;

            private readonly List<PropertiesEmote> allSets;
            private readonly Dictionary<PropertiesEmote, ChainOutcome> walked = new Dictionary<PropertiesEmote, ChainOutcome>(ReferenceEqualityComparer.Instance);
            private readonly HashSet<PropertiesEmote> onPath = new HashSet<PropertiesEmote>(ReferenceEqualityComparer.Instance);

            public EmoteWalk(IEnumerable<PropertiesEmote> allSets)
            {
                this.allSets = allSets?.Where(s => s?.PropertiesEmoteAction != null).ToList() ?? new List<PropertiesEmote>();
            }

            public ChainOutcome Outcome(PropertiesEmote set, int fromIndex, int depth)
            {
                // A walk from a later step (the DoEnqueue guard) is not a visit of the whole set: a branch back into the set runs
                // it again from its first step, which must still be looked at. So only whole-set walks are memoized and marked
                // as on the path.
                var wholeSet = fromIndex <= 0;
                if (wholeSet && walked.TryGetValue(set, out var known))
                    return known;

                // past the engine's own limit nothing more is known about the chain: it may reach the bank, and pay nothing else
                if (depth > MaxBranchDepth)
                    return new ChainOutcome(true, true, true);

                // a branch back into a set already being followed as a whole
                if (wholeSet && !onPath.Add(set))
                    return new ChainOutcome(false, true, false);

                try
                {
                    var reachesBankStep = false;
                    var ownLuminance = false;
                    var ownOtherReward = false;
                    var everyBranchCanPayNoOther = true;
                    var someBranchCanPayOnlyLuminance = false;

                    var actions = set.PropertiesEmoteAction;
                    for (var i = Math.Max(0, fromIndex); i < actions.Count; i++)
                    {
                        var action = actions[i];
                        if (action == null)
                            continue;

                        if (IsBankStep(action))
                        {
                            reachesBankStep = true;
                            continue;
                        }

                        if ((EmoteType)action.Type == EmoteType.AwardLuminance)
                        {
                            ownLuminance = true;
                            continue;
                        }

                        var categories = ContinuesIn(action);
                        if (categories != null)
                        {
                            // a test that continues nowhere (InqNumCharacterTitles) runs nothing more
                            if (categories.Length > 0)
                            {
                                var branch = Branch(categories, action.Message, depth);
                                reachesBankStep |= branch.ReachesBankStep;
                                everyBranchCanPayNoOther &= branch.CanPayNoOtherReward;
                                someBranchCanPayOnlyLuminance |= branch.CanPayOnlyLuminance;
                            }
                            continue;
                        }

                        if (IsOtherReward(action))
                            ownOtherReward = true;
                    }

                    var canPayNoOther = !ownOtherReward && everyBranchCanPayNoOther;
                    var outcome = new ChainOutcome(reachesBankStep, canPayNoOther, canPayNoOther && (ownLuminance || someBranchCanPayOnlyLuminance));
                    if (wholeSet)
                        walked[set] = outcome;
                    return outcome;
                }
                finally
                {
                    if (wholeSet)
                        onPath.Remove(set);
                }
            }

            /// <summary>
            /// One branching step, as EmoteManager.GetEmoteSet picks its continuation: among the sets of the outcome's category
            /// whose Quest is the step's Message (any Quest when the Message is null), the one with the lowest Probability above a
            /// roll in [0, 1). So every such set with a Probability above 0 can run, and an outcome category with no set at 1 or
            /// above can run nothing.
            /// </summary>
            private ChainOutcome Branch(EmoteCategory[] categories, string quest, int depth)
            {
                var reachesBankStep = false;
                var canPayNoOther = false;
                var canPayOnlyLuminance = false;

                foreach (var category in categories)
                {
                    var highestProbability = 0f;

                    foreach (var next in allSets)
                    {
                        if (next.Category != category || next.Probability <= 0)
                            continue;
                        if (quest != null && (next.Quest == null || !next.Quest.Equals(quest, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        highestProbability = Math.Max(highestProbability, next.Probability);

                        var run = Outcome(next, 0, depth + 1);
                        reachesBankStep |= run.ReachesBankStep;
                        canPayNoOther |= run.CanPayNoOtherReward;
                        canPayOnlyLuminance |= run.CanPayOnlyLuminance;
                    }

                    if (highestProbability < 1f)
                        canPayNoOther = true;
                }

                return new ChainOutcome(reachesBankStep, canPayNoOther, canPayOnlyLuminance);
            }
        }

        /// <summary>
        /// A step that pays the player something besides luminance, making a hand-in worth doing without it: an item, XP, a
        /// skill, credits, a spell or an augmentation, a title, treasure, a contract, a stat of the character, a random quest
        /// stamp (its account bonus), a pet skin, a creature or object made, or a teleport. Not one: chat and motions, plain
        /// quest stamps and their bookkeeping, taking items, an XP step that takes XP (a negative amount), luminance (that is
        /// what is being looked for) and the branching steps (followed instead).
        /// </summary>
        private static bool IsOtherReward(PropertiesEmoteAction action)
        {
            switch ((EmoteType)action.Type)
            {
                // a negative amount spends XP (ExecuteEmote: SpendXP), which is no reward
                case EmoteType.AwardXP:
                case EmoteType.AwardNoShareXP:
                    return (action.Amount64 ?? action.Amount ?? 0) > 0;

                case EmoteType.Give:
                case EmoteType.AwardLevelProportionalXP:
                case EmoteType.AwardLevelProportionalSkillXP:
                case EmoteType.AwardSkillXP:
                case EmoteType.AwardSkillPoints:
                case EmoteType.AwardTrainingCredits:
                case EmoteType.TeachSpell:
                case EmoteType.CastSpell:
                case EmoteType.CastSpellInstant:
                case EmoteType.PromptAddAugment:
                case EmoteType.PromptAddAugment10:
                case EmoteType.AddCharacterTitle:
                case EmoteType.CreateTreasure:
                case EmoteType.AddContract:
                case EmoteType.SetIntStat:
                case EmoteType.IncrementIntStat:
                case EmoteType.SetBoolStat:
                case EmoteType.SetFloatStat:
                case EmoteType.SetAttributeStat:
                case EmoteType.SetSecondaryAttributeStat:
                case EmoteType.GrantAttributeStat:
                case EmoteType.GrantVitalStat:
                case EmoteType.GrantRandomQuestStamp:
                case EmoteType.RegisterPetSkin:
                case EmoteType.Generate:
                case EmoteType.TeleportTarget:
                    return true;

                // an Int64 stat that is not banked (a bank one is a bank step)
                case EmoteType.IncrementInt64Stat:
                case EmoteType.SetInt64Stat:
                    return true;

                default:
                    return false;
            }
        }

        private static readonly ConcurrentDictionary<(uint Character, uint Source), DateTime> lastStopLogged = new ConcurrentDictionary<(uint Character, uint Source), DateTime>();

        /// <summary>
        /// Logs an emote set stopped for a locked player (EmoteManager.DoEnqueue), at most once a minute per character and NPC.
        /// When a hand-in was begun before the lock, its item is already gone, so this line is what lets staff pay the credit by
        /// hand: it names the NPC, the set, and the bank steps left unrun in that set.
        /// </summary>
        public static void NoteStoppedSet(IPlayer character, WorldObject source, PropertiesEmote emoteSet, int fromIndex)
        {
            var key = (character.Guid.Full, source.Guid.Full);
            var now = DateTime.UtcNow;
            if (lastStopLogged.TryGetValue(key, out var last) && now - last < TimeSpan.FromMinutes(1))
                return;
            lastStopLogged[key] = now;

            var steps = new List<string>();
            var actions = emoteSet.PropertiesEmoteAction;
            for (var i = Math.Max(0, fromIndex); i < actions.Count; i++)
            {
                var action = actions[i];
                if (!IsBankStep(action))
                    continue;

                var amount = action.Amount64 ?? action.Amount ?? 0;
                var how = (EmoteType)action.Type switch
                {
                    EmoteType.IncrementInt64Stat => $"+{amount:N0}",
                    EmoteType.DecrementInt64Stat => $"-{amount:N0}",
                    _ => $"set to {amount:N0}",
                };
                steps.Add($"{(PropertyInt64)action.Stat.Value} {how}");
            }

            var where = string.IsNullOrEmpty(emoteSet.Quest) ? emoteSet.Category.ToString() : $"{emoteSet.Category} {emoteSet.Quest}";
            var what = steps.Count > 0 ? string.Join(", ", steps) : "in a set it continues in";
            log.Warn($"[BankLock] {character.Name} (0x{character.Guid.Full:X8}): {source.Name} (wcid {source.WeenieClassId}) stopped its {where} set at step {fromIndex} - the bank is locked. Bank steps not run: {what}. If an item was handed in for a credit, pay it by hand after /bankunlock.");
        }

        // ---- staff commands -------------------------------------------------------------------------------------------

        /// <summary>The stored balance of each banked currency, in /b order: what is really there, locked or not.</summary>
        public static List<(string Name, PropertyInt64 Property, long Amount)> ReadStored(IPlayer character)
        {
            var balances = new List<(string Name, PropertyInt64 Property, long Amount)>(Currencies.Count);

            foreach (var (property, name) in Currencies)
                balances.Add((name, property, character.GetProperty(property) ?? 0));

            return balances;
        }

        /// <summary>"Pyreals 1,234; Luminance 0; ..." for staff output and the audit channel.</summary>
        public static string Describe(IEnumerable<(string Name, PropertyInt64 Property, long Amount)> balances)
        {
            return string.Join("; ", balances.Select(b => $"{b.Name} {b.Amount:N0}"));
        }

        /// <summary>
        /// Locks or unlocks a bank and saves the character. An online player's coin total is redrawn, as it counts the bank,
        /// and on unlock the allegiance luminance held back while it was locked is paid.
        /// </summary>
        public static void SetLocked(IPlayer character, bool locked)
        {
            if (locked)
                character.SetProperty(PropertyBool.BankLocked, true);
            else
                character.RemoveProperty(PropertyBool.BankLocked);

            if (character is Player online)
            {
                online.RefreshCoinValueAfterBankChange();

                // allegiance luminance waits in its cache while the bank is locked (Player_Allegiance.AddAllegianceLum): pay
                // it now (offline, it is paid at the next login)
                if (!locked)
                    online.AddAllegianceLum();
            }

            SaveNow(character);
        }

        /// <summary>
        /// Sets every banked currency of <paramref name="character"/> to 0, locked bank or not, clears the part of a Prestige
        /// Coin in progress, and saves. Returns the balances as they were. Run it on the world thread (a chat command).
        /// </summary>
        public static List<(string Name, PropertyInt64 Property, long Amount)> Wipe(IPlayer character, out double coinFraction)
        {
            var before = ReadStored(character);
            coinFraction = character.GetProperty(PropertyFloat.PrestigeCoinFraction) ?? 0;

            using (AdminWrite())
            {
                foreach (var (_, property, amount) in before)
                {
                    if (amount != 0)
                        character.SetProperty(property, 0);
                }
            }

            if (coinFraction != 0)
                character.RemoveProperty(PropertyFloat.PrestigeCoinFraction);

            if (character is Player online)
            {
                foreach (var (_, property, amount) in before)
                {
                    if (amount != 0)
                        online.Session?.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(online, property, 0));
                }

                online.RefreshCoinValueAfterBankChange();
            }

            SaveNow(character);

            return before;
        }

        /// <summary>
        /// Saves the character now: an online player through its own save, an offline one by its stored record (a failed
        /// offline save is retried with the next offline save cycle, PlayerManager.SaveOfflinePlayerNow).
        /// </summary>
        public static void SaveNow(IPlayer character)
        {
            if (character is Player online)
                online.SavePlayerToDatabase();
            else if (character is OfflinePlayer offline)
                PlayerManager.SaveOfflinePlayerNow(offline);
        }
    }
}
