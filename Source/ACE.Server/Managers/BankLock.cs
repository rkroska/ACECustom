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
    /// <item>An emote set with a step on a banked currency left to run stops before its next step for a locked player
    /// (EmoteManager.DoEnqueue, <see cref="HasBankStep"/>): the rest of the set never runs around a refused bank step.</item>
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
        /// True when step <paramref name="fromIndex"/> or a later one of this set is an Int64 stat step (increment, decrement
        /// or set) on a banked currency - the lum token NPCs, and NPCs that charge or pay a banked currency by hand. For a
        /// locked player the set stops there (EmoteManager.DoEnqueue): the bank would refuse the step while the rest of the
        /// set ran on, handing over a reward whose price was never taken, or taking an item for a credit never made.
        /// </summary>
        public static bool HasBankStep(PropertiesEmote emoteSet, int fromIndex = 0)
        {
            var actions = emoteSet?.PropertiesEmoteAction;
            if (actions == null)
                return false;

            for (var i = Math.Max(0, fromIndex); i < actions.Count; i++)
            {
                if (IsBankStep(actions[i]))
                    return true;
            }

            return false;
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
        /// True when a locked player should keep an item rather than hand it to an NPC with this Give or Refuse emote set,
        /// because the NPC deals with it through the bank. The set is followed into every set its branches can continue in
        /// (Goto, the Inq tests, UpdateQuest: the sets of the NPC, <paramref name="allSets"/>, whose Quest is the step's
        /// Message), and the hand-in is kept back when some run of the chain could
        /// <list type="bullet">
        /// <item>reach a step on a banked currency (<see cref="HasBankStep"/>: that set would stop, after the item was taken), or</item>
        /// <item>pay luminance and nothing else (every luminance award is banked, Player_Luminance.AddLuminance - so it would pay
        /// nothing at all).</item>
        /// </list>
        /// Which way a test goes is not known in advance, so every branch counts, and so does a test outcome with no set of its
        /// own (it pays nothing). A chain that always pays something besides luminance - an item, XP, a skill, a spell, a stat,
        /// a teleport (<see cref="IsOtherReward"/>) - goes ahead, and only its luminance is not paid. Quest stamps and chat are
        /// bookkeeping, not a reward.
        /// </summary>
        /// <param name="emoteSet">The Give or Refuse set the hand-in would run</param>
        /// <param name="allSets">All the NPC's emote sets (its Biota.PropertiesEmote), where its branches continue</param>
        public static bool UsesBank(PropertiesEmote emoteSet, IEnumerable<PropertiesEmote> allSets)
        {
            if (emoteSet?.PropertiesEmoteAction == null)
                return false;

            var sets = allSets?.Where(s => s?.PropertiesEmoteAction != null).ToList() ?? new List<PropertiesEmote>();
            var walked = new Dictionary<PropertiesEmote, (bool, bool)>(ReferenceEqualityComparer.Instance);
            var onPath = new HashSet<PropertiesEmote>(ReferenceEqualityComparer.Instance);
            var (reachesBankStep, canPayOnlyLuminance) = Walk(emoteSet, sets, walked, onPath, 0);

            return reachesBankStep || canPayOnlyLuminance;
        }

        /// <summary>Deeper than any real emote chain; a deeper one is cut off there (as EmoteManager cuts off its own loops).</summary>
        private const int MaxBranchDepth = 16;

        /// <summary>
        /// The categories a branching step continues in: whichever the test reached, among the sets with the step's Message as
        /// their Quest (EmoteManager.ExecuteEmoteSet(category, emote.Message, ...)).
        /// </summary>
        private static readonly HashSet<EmoteCategory> BranchCategories = new HashSet<EmoteCategory>
        {
            EmoteCategory.QuestSuccess, EmoteCategory.QuestFailure,
            EmoteCategory.TestSuccess, EmoteCategory.TestFailure, EmoteCategory.TestNoQuality,
            EmoteCategory.EventSuccess, EmoteCategory.EventFailure,
            EmoteCategory.QuestNoFellow, EmoteCategory.TestNoFellow,
            EmoteCategory.NumFellowsSuccess, EmoteCategory.NumFellowsFailure,
            EmoteCategory.NumCharacterTitlesSuccess, EmoteCategory.NumCharacterTitlesFailure,
            EmoteCategory.GotoSet,
        };

        /// <summary>
        /// One emote set and every set it can branch into: whether some run reaches a bank step, and whether some run pays
        /// luminance and nothing else. A set runs all its own steps, and each branching step adds one of the sets it can
        /// continue in - or none, when the outcome reached has no set. So a run pays only luminance when the set's own steps
        /// pay nothing else, and the luminance comes from its own steps or from a branch that can itself pay only luminance.
        /// Each set is worked out once (<paramref name="walked"/>), so sets that several branches share cost nothing more.
        /// </summary>
        private static (bool ReachesBankStep, bool CanPayOnlyLuminance) Walk(PropertiesEmote set, List<PropertiesEmote> allSets,
            Dictionary<PropertiesEmote, (bool, bool)> walked, HashSet<PropertiesEmote> onPath, int depth)
        {
            if (walked.TryGetValue(set, out var known))
                return known;

            // a branch back into a set already being followed, or a chain deeper than any real one: count it as paying nothing more
            if (depth > MaxBranchDepth || !onPath.Add(set))
                return (false, false);

            try
            {
                var reachesBankStep = false;
                var ownLuminance = false;
                var ownOtherReward = false;
                var branchCanPayOnlyLuminance = false;

                foreach (var action in set.PropertiesEmoteAction)
                {
                    if (action == null)
                        continue;

                    var type = (EmoteType)action.Type;

                    if (IsBankStep(action))
                        reachesBankStep = true;
                    else if (type == EmoteType.AwardLuminance)
                        ownLuminance = true;
                    else if (EmoteManager.EmoteIsBranchingType(action) || type.ToString().StartsWith("Inq", StringComparison.Ordinal))
                    {
                        // (every Inq step tests and branches - the custom ones EmoteIsBranchingType does not list too)
                        if (string.IsNullOrEmpty(action.Message))
                            continue;

                        foreach (var next in allSets)
                        {
                            if (!BranchCategories.Contains(next.Category) || !string.Equals(next.Quest, action.Message, StringComparison.OrdinalIgnoreCase))
                                continue;

                            var branch = Walk(next, allSets, walked, onPath, depth + 1);
                            reachesBankStep |= branch.ReachesBankStep;
                            branchCanPayOnlyLuminance |= branch.CanPayOnlyLuminance;
                        }
                    }
                    else if (IsOtherReward(action))
                        ownOtherReward = true;
                }

                var outcome = (reachesBankStep, !ownOtherReward && (ownLuminance || branchCanPayOnlyLuminance));
                walked[set] = outcome;
                return outcome;
            }
            finally
            {
                onPath.Remove(set);
            }
        }

        /// <summary>
        /// A step that pays the player something besides luminance, making a hand-in worth doing without it: an item, XP, a
        /// skill, credits, a spell or an augmentation, a title, treasure, a contract, a stat of the character, a random quest
        /// stamp (its account bonus), a pet skin, a creature or object made, or a teleport. Not one: chat and motions, plain
        /// quest stamps and their bookkeeping, taking items, luminance (that is what is being looked for) and the branching
        /// steps (followed instead).
        /// </summary>
        private static bool IsOtherReward(PropertiesEmoteAction action)
        {
            switch ((EmoteType)action.Type)
            {
                case EmoteType.Give:
                case EmoteType.AwardXP:
                case EmoteType.AwardNoShareXP:
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
