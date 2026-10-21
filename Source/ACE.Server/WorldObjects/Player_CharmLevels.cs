using System;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Levelled charm levels (owner 2026-10-17). The level of Split Arrow, Chain, Pandemic and Improved Cleave lives on the
    /// PLAYER (PropertyInt64 51200-51203); the charm item only shows it - its PropertyInt.CharmLevel is a mirror, written
    /// whenever the level changes, at login and when the charm is switched on. Using the charm still switches it on / off.
    ///
    /// A level is raised with an upgrade gem bought for Prestige Coins: the gem's Use emote is PromptAddAugment with the
    /// message "CharmLevel_&lt;charm&gt;_&lt;levels&gt;" (e.g. "CharmLevel_chain_5"). Using it needs the charm in the pack, never
    /// goes past level 100, and charges the charm's live "gemcost" setting in Prestige Coins (pack first, then the bank)
    /// for each level it adds, so the cost rises with the level. The gem is used up.
    /// </summary>
    partial class Player
    {
        public const string CharmLevelGemPrefix = "CharmLevel_";

        private const int MaxCharmLevel = CharmSettingsManager.LeveledCharmBlock.MaxLevel;

        /// <summary>The player property that holds a levelled charm's level; null for any other ability.</summary>
        public static PropertyInt64? CharmLevelProperty(int abilityId) => abilityId switch
        {
            CharmAbilityRegistry.SplitArrowAbilityId => PropertyInt64.SplitArrowCharmLevel,
            CharmAbilityRegistry.ChainAbilityId => PropertyInt64.ChainCharmLevel,
            CharmAbilityRegistry.PandemicAbilityId => PropertyInt64.PandemicCharmLevel,
            CharmAbilityRegistry.ImprovedCleaveAbilityId => PropertyInt64.ImprovedCleaveCharmLevel,
            _ => null,
        };

        /// <summary>The player's level (1-100) of a levelled charm; 0 when they have never had one (or the ability is not levelled).</summary>
        public int GetCharmLevel(int abilityId)
        {
            var prop = CharmLevelProperty(abilityId);
            if (prop == null) return 0;
            return (int)Math.Clamp(GetProperty(prop.Value) ?? 0, 0, MaxCharmLevel);
        }

        /// <summary>Sets the player's level of a levelled charm, mirrors it onto every copy carried and, when the charm is
        /// switched on, makes the ability use it at once.</summary>
        public void SetCharmLevel(int abilityId, int level)
        {
            var prop = CharmLevelProperty(abilityId);
            if (prop == null) return;

            level = Math.Clamp(level, 1, MaxCharmLevel);
            SetProperty(prop.Value, level);
            Session?.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, prop.Value, level));

            foreach (var item in GetAllPossessionsDeep())
                if (item.IsAbilityCharm && item.CharmGrantsAbility == abilityId)
                    MirrorCharmLevel(item, level);

            if (CharmAbilityRegistry.IsActive(this, abilityId))
                ActiveCharmLevels[abilityId] = level;
        }

        /// <summary>Before a levelled charm item is used: makes its shown level the player's. The first charm of its kind
        /// a player ever has starts the player's level at its own (1 for a bought one).</summary>
        public void SyncLevelledCharmItem(WorldObject item, int abilityId)
        {
            var prop = CharmLevelProperty(abilityId);
            if (prop == null) return;

            var level = GetCharmLevel(abilityId);
            if (level == 0)
            {
                level = Math.Clamp(item.CharmLevel ?? 1, 1, MaxCharmLevel);
                SetProperty(prop.Value, level);
            }
            MirrorCharmLevel(item, level);
        }

        /// <summary>At login: every levelled charm carried shows the player's level. A player with no level yet starts at
        /// the highest level among the copies carried (charms levelled before 2026-10-17 keep their level).</summary>
        public void SyncLevelledCharms()
        {
            var charms = GetAllPossessionsDeep()
                .Where(i => i.IsAbilityCharm && i.CharmGrantsAbility.HasValue && CharmLevelProperty(i.CharmGrantsAbility.Value) != null)
                .GroupBy(i => i.CharmGrantsAbility.Value);

            foreach (var kind in charms)
            {
                var abilityId = kind.Key;
                if (GetCharmLevel(abilityId) == 0)
                    SetProperty(CharmLevelProperty(abilityId).Value, Math.Clamp(kind.Max(i => i.CharmLevel ?? 1), 1, MaxCharmLevel));

                var level = GetCharmLevel(abilityId);
                foreach (var item in kind)
                    MirrorCharmLevel(item, level);
            }
        }

        private static void MirrorCharmLevel(WorldObject item, int level)
        {
            if (item.CharmLevel == level) return;
            item.CharmLevel = level;
            item.SaveBiotaToDatabase();
        }

        /// <summary>Prestige Coins in the pack plus the bank.</summary>
        public long PrestigeCoinsAvailable() => GetNumInventoryItemsOfWCID(PRESTIGE_COIN_WCID) + (BankedPrestigeCoins ?? 0);

        /// <summary>Takes Prestige Coins, pack first, then the bank (as vendors do). The caller checks there are enough.
        /// False when they could not all be taken (a pack consume that failed part-way and a bank too small for the rest);
        /// <paramref name="taken"/> is what WAS taken either way, so the caller can give it back.</summary>
        private bool SpendPrestigeCoins(long amount, out long taken)
        {
            long packBefore = GetNumInventoryItemsOfWCID(PRESTIGE_COIN_WCID);
            long fromPack = Math.Min(amount, packBefore);
            if (fromPack > 0 && !TryConsumeFromInventoryWithNetworking(PRESTIGE_COIN_WCID, (int)fromPack))
                fromPack = Math.Max(0, packBefore - GetNumInventoryItemsOfWCID(PRESTIGE_COIN_WCID));   // what it did take

            taken = fromPack;
            long fromBank = amount - fromPack;
            if (fromBank > 0)
            {
                if ((BankedPrestigeCoins ?? 0) < fromBank)
                    return false;
                BankedPrestigeCoins = (BankedPrestigeCoins ?? 0) - fromBank;
                taken += fromBank;
            }
            return true;
        }

        /// <summary>Gives Prestige Coins back into the bank (the payment for a gem that could not be completed).</summary>
        private void RefundPrestigeCoinsToBank(long amount)
        {
            if (amount <= 0) return;
            BankedPrestigeCoins = (BankedPrestigeCoins ?? 0) + amount;
            log.Warn($"[CHARM] {Name}: {amount} Prestige Coins put back in the bank after an upgrade gem could not be completed.");
        }

        /// <summary>Prestige Coins to raise a levelled charm from <paramref name="from"/> by <paramref name="levels"/>: the
        /// charm's gemcost at each level being left, added up.</summary>
        public static long CharmGemCost(CharmSettingsManager.LeveledCharmBlock block, int from, int levels)
        {
            double total = 0;
            for (var level = from; level < from + levels; level++)
                total += block.At("gemcost", level);
            return (long)Math.Round(total, MidpointRounding.AwayFromZero);
        }

        /// <summary>An upgrade gem was used (EmoteManager, PromptAddAugment "CharmLevel_&lt;charm&gt;_&lt;levels&gt;").</summary>
        public void UseCharmLevelGem(WorldObject gem, string message)
        {
            var rest = message.Substring(CharmLevelGemPrefix.Length);
            var cut = rest.LastIndexOf('_');
            var charmName = cut > 0 ? rest.Substring(0, cut) : "";
            if (cut <= 0 || !int.TryParse(rest.Substring(cut + 1), out var levels) || levels < 1
                || !CharmSettingsManager.LeveledBlocks.TryGetValue(charmName, out var block))
            {
                TellCharm($"This gem is not set up right ({message}). Please tell a developer.");
                return;
            }

            var abilityId = CharmSettingsManager.LeveledAbilityId(charmName);
            var charm = GetAllPossessionsDeep().FirstOrDefault(i => i.IsAbilityCharm && i.CharmGrantsAbility == abilityId);
            if (charm == null)
            {
                TellCharm($"You must possess a {block.DisplayName} charm to use this gem.");
                return;
            }

            SyncLevelledCharmItem(charm, abilityId);
            var from = GetCharmLevel(abilityId);
            var to = from + levels;
            if (to > MaxCharmLevel)
            {
                TellCharm($"Your {block.DisplayName} charm is level {from}. This gem adds {levels} and would take it past level {MaxCharmLevel}, so it was not used.");
                return;
            }

            var cost = CharmGemCost(block, from, levels);
            var have = PrestigeCoinsAvailable();
            if (have < cost)
            {
                TellCharm($"Raising your {block.DisplayName} charm from level {from} to {to} costs {cost:N0} Prestige Coins (pack or bank). You have {have:N0}.");
                return;
            }

            if (cost == 0)
            {
                ApplyCharmLevelGem(gem, block, abilityId, from, levels, 0);
                return;
            }

            // another prompt already open (a second gem, a rare or augment gem): say so, as Gem.cs does, instead of nothing
            if (!ConfirmationManager.EnqueueSend(new Confirmation_Custom(Guid, (response, _) =>
                {
                    if (response)
                        ApplyCharmLevelGem(gem, block, abilityId, from, levels, cost);
                }), $"Spend {cost:N0} Prestige Coins and this gem to raise your {block.DisplayName} charm from level {from} to level {to}?"))
                SendWeenieError(WeenieError.ConfirmationInProgress);
        }

        private void ApplyCharmLevelGem(WorldObject gem, CharmSettingsManager.LeveledCharmBlock block, int abilityId, int from, int levels, long cost)
        {
            // Everything can change between the prompt and the answer (another gem, coins spent, the gem moved) - check again.
            if (GetCharmLevel(abilityId) != from)
            {
                TellCharm($"Your {block.DisplayName} charm's level changed. Use the gem again.");
                return;
            }
            if (PrestigeCoinsAvailable() < cost)
            {
                TellCharm($"You no longer have the {cost:N0} Prestige Coins this needs.");
                return;
            }
            // The coins go first, then the gem (review 2026-10-20 round 2): whatever fails part-way, the player keeps the gem
            // and every coin taken goes back (to the bank), and a level is never given for less than its price.
            long taken = 0;
            if (cost > 0 && !SpendPrestigeCoins(cost, out taken))
            {
                RefundPrestigeCoinsToBank(taken);
                TellCharm($"The {cost:N0} Prestige Coins could not all be taken, so nothing was spent and the charm was not raised. Any coins taken are back in your bank.");
                SaveBiotaToDatabase();
                return;
            }
            if (!TryConsumeFromInventoryWithNetworking(gem, 1))
            {
                RefundPrestigeCoinsToBank(taken);
                TellCharm("Failed to consume the gem, so the charm was not raised." + (taken > 0 ? " The coins are back in your bank." : "") + " Please try again.");
                SaveBiotaToDatabase();
                return;
            }

            SetCharmLevel(abilityId, from + levels);
            TellCharm($"Your {block.DisplayName} charm is now level {from + levels}!" + (cost > 0 ? $" ({cost:N0} Prestige Coins spent.)" : ""));
            TellCharm(block.PlayerText(from + levels));
            SaveBiotaToDatabase();
        }

        private void TellCharm(string text) => Session?.Network.EnqueueSend(new GameMessageSystemChat(text, ChatMessageType.Broadcast));
    }
}
