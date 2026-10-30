using System;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Levelled charm coin ledger (owner 2026-10-24). For Split Arrow, Chain, Pandemic, Improved Cleave, Armor Breaker and
    /// Shield Arc the PLAYER keeps the Prestige Coins put into each charm (PropertyInt64 51200-51205); the level (1-100)
    /// is worked out from them by the charm's live price curve (CharmSettingsManager: coinbase, coingrowth), so a retune
    /// re-levels everyone without touching what they spent. The charm item only shows the level - its PropertyInt.CharmLevel
    /// is a mirror, written whenever the level changes, at login and when the charm is switched on. Using the charm still
    /// switches it on / off. Charm effects use the whole level; the share of the way to the next one is shown, not used.
    ///
    /// Coins go in with Charm Infusion Gems, bought from Enephior 1:1 for Prestige Coins (the vendor takes pack or bank).
    /// A gem is used ON a levelled charm in the player's own possession: it asks first, refuses a gem that would take the
    /// charm past level 100, checks everything again on Yes, uses up one gem and adds its coins. One gem type works for
    /// every charm; its PropertyInt.CharmInfusionCoins is what one gem puts in. No refunds - coins put in stay put in.
    /// </summary>
    partial class Player
    {
        private const int MaxCharmLevel = CharmSettingsManager.LeveledCharmBlock.MaxLevel;

        /// <summary>The player property that holds the coins put into a levelled charm; null for any other ability.</summary>
        public static PropertyInt64? CharmCoinsProperty(int abilityId) => abilityId switch
        {
            CharmAbilityRegistry.SplitArrowAbilityId => PropertyInt64.SplitArrowCharmCoins,
            CharmAbilityRegistry.ChainAbilityId => PropertyInt64.ChainCharmCoins,
            CharmAbilityRegistry.PandemicAbilityId => PropertyInt64.PandemicCharmCoins,
            CharmAbilityRegistry.ImprovedCleaveAbilityId => PropertyInt64.ImprovedCleaveCharmCoins,
            CharmAbilityRegistry.ArmorBreakerAbilityId => PropertyInt64.ArmorBreakerCharmCoins,
            CharmAbilityRegistry.ShieldArcAbilityId => PropertyInt64.ShieldArcCharmCoins,
            _ => null,
        };

        /// <summary>Prestige Coins this player has put into a levelled charm (0 for any other ability).</summary>
        public long GetCharmCoins(int abilityId)
        {
            var prop = CharmCoinsProperty(abilityId);
            return prop == null ? 0 : Math.Max(0, GetProperty(prop.Value) ?? 0);
        }

        /// <summary>The player's whole level (1-100) of a levelled charm, from the coins put in; 0 when the ability is not a
        /// levelled charm's.</summary>
        public int GetCharmLevel(int abilityId)
        {
            var block = CharmSettingsManager.LeveledBlockFor(abilityId);
            return block == null ? 0 : block.LevelFromCoins(GetCharmCoins(abilityId));
        }

        /// <summary>The level with the share of the way to the next one (41.95 = level 41, 95 pct to 42); 0 when not levelled.</summary>
        public double GetCharmLevelExact(int abilityId)
        {
            var block = CharmSettingsManager.LeveledBlockFor(abilityId);
            return block == null ? 0 : block.ExactLevelFromCoins(GetCharmCoins(abilityId));
        }

        /// <summary>Sets the coins put into a levelled charm (held to 0 .. the coins that reach level 100), then shows the
        /// level it reaches on every copy carried and, when the charm is switched on, makes the ability use it at once.
        /// Returns the whole level.</summary>
        public int SetCharmCoins(int abilityId, long coins)
        {
            var prop = CharmCoinsProperty(abilityId);
            var block = CharmSettingsManager.LeveledBlockFor(abilityId);
            if (prop == null || block == null) return 0;

            coins = Math.Clamp(coins, 0, block.MaxCoins);
            SetProperty(prop.Value, coins);
            Session?.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, prop.Value, coins));
            return ApplyCharmLevel(abilityId, block.LevelFromCoins(coins));
        }

        /// <summary>Sets a levelled charm to exactly the start of a level (test tools: /charm level, /testchar preset).</summary>
        public void SetCharmLevel(int abilityId, int level)
        {
            var block = CharmSettingsManager.LeveledBlockFor(abilityId);
            if (block == null) return;
            SetCharmCoins(abilityId, block.CoinsToReach(Math.Clamp(level, 1, MaxCharmLevel)));
        }

        /// <summary>Shows a level on every copy of the charm carried and, when the charm is on, in the live ability.</summary>
        private int ApplyCharmLevel(int abilityId, int level)
        {
            foreach (var item in GetAllPossessionsDeep())
                if (item.IsAbilityCharm && item.CharmGrantsAbility == abilityId)
                    MirrorCharmLevel(item, level);

            if (CharmAbilityRegistry.IsActive(this, abilityId))
                ActiveCharmLevels[abilityId] = level;
            return level;
        }

        /// <summary>Before a levelled charm item is used: makes its shown level the player's.</summary>
        public void SyncLevelledCharmItem(WorldObject item, int abilityId)
        {
            if (CharmCoinsProperty(abilityId) == null) return;
            MirrorCharmLevel(item, GetCharmLevel(abilityId));
        }

        /// <summary>At login, and after a price curve change: every levelled charm carried shows the level its coins reach
        /// today, and a switched-on one uses it.</summary>
        public void SyncLevelledCharms()
        {
            foreach (var block in CharmSettingsManager.LeveledBlocks.Values)
            {
                var abilityId = CharmSettingsManager.LeveledAbilityId(block.Name);
                ApplyCharmLevel(abilityId, block.LevelFromCoins(GetCharmCoins(abilityId)));
            }
        }

        private static void MirrorCharmLevel(WorldObject item, int level)
        {
            if (item.CharmLevel == level) return;
            item.CharmLevel = level;
            item.SaveBiotaToDatabase();
        }

        // -- Charm Infusion Gems ---------------------------------------------

        /// <summary>True for a Charm Infusion Gem (an item whose CharmInfusionCoins is set).</summary>
        public static bool IsCharmInfusionGem(WorldObject item) => (item?.GetProperty(PropertyInt.CharmInfusionCoins) ?? 0) > 0;

        /// <summary>"Level 41 (95% to 42)" - or "Level 100 (max)".</summary>
        public static string CharmLevelText(CharmSettingsManager.LeveledCharmBlock block, long coins)
        {
            var level = block.LevelFromCoins(coins);
            if (level >= MaxCharmLevel) return $"Level {MaxCharmLevel} (max)";
            // from the whole-coin thresholds, never a float difference (which could print -1% or 100%)
            var from = block.CoinsToReach(level);
            var span = block.CoinsToReach(level + 1) - from;
            var pct = span > 0 ? (int)Math.Clamp((coins - from) * 100 / span, 0, 99) : 0;
            return $"Level {level} ({pct}% to {level + 1})";
        }

        /// <summary>A Charm Infusion Gem was used on something (Gem.HandleActionUseOnTarget).</summary>
        public void UseCharmInfusionGem(WorldObject gem, WorldObject target)
        {
            var amount = (long)(gem.GetProperty(PropertyInt.CharmInfusionCoins) ?? 0);
            var abilityId = target?.CharmGrantsAbility ?? 0;
            var block = target != null && target.IsAbilityCharm ? CharmSettingsManager.LeveledBlockFor(abilityId) : null;

            if (amount <= 0)
            {
                TellCharm($"This gem is not set up right ({gem.WeenieClassId}). Please tell a developer.");
                SendUseDoneEvent();
                return;
            }
            if (block == null)
            {
                TellCharm("Use a Charm Infusion Gem on a levelled charm: Split Arrow, Chain, Pandemic, Improved Cleave, Armor Breaker or Shield Arc.");
                SendUseDoneEvent();
                return;
            }
            if (!OwnsItem(target) || !OwnsItem(gem))
            {
                TellCharm("The gem and the charm must both be in your own pack.");
                SendUseDoneEvent();
                return;
            }

            var coins = GetCharmCoins(abilityId);
            var room = block.MaxCoins - coins;
            if (room <= 0)
            {
                TellCharm($"Your {block.DisplayName} charm is already level {MaxCharmLevel}.");
                SendUseDoneEvent();
                return;
            }
            if (amount > room)
            {
                TellCharm($"Your {block.DisplayName} charm needs only {room:N0} more coins to reach level {MaxCharmLevel}. This gem holds {amount:N0}, so it was not used. Use smaller gems to finish.");
                SendUseDoneEvent();
                return;
            }

            // another prompt already open (a second gem, a rare or augment gem): say so, as Gem.cs does, instead of nothing
            // the use ends once the prompt is up, as crafting does (RecipeManager); the answer then applies or does nothing
            if (!ConfirmationManager.EnqueueSend(new Confirmation_Custom(Guid, (response, _) =>
                {
                    if (response)
                        ApplyCharmInfusionGem(gem, target, block, abilityId, coins, amount);
                }), CharmInfusionPromptText(block, coins, amount)))
            {
                SendUseDoneEvent(WeenieError.ConfirmationInProgress);
                return;
            }
            SendUseDoneEvent();
        }

        private void ApplyCharmInfusionGem(WorldObject gem, WorldObject charm, CharmSettingsManager.LeveledCharmBlock block, int abilityId, long coinsBefore, long amount)
        {
            // Everything can change between the prompt and the answer (another gem, a retune, the gem or charm moved) - check again.
            if (!OwnsItem(gem) || !OwnsItem(charm) || charm.CharmGrantsAbility != abilityId)
            {
                TellCharm("The gem and the charm must both still be in your pack. Nothing was used.");
                return;
            }
            if (GetCharmCoins(abilityId) != coinsBefore)
            {
                TellCharm($"Your {block.DisplayName} charm changed. Use the gem again.");
                return;
            }
            if (coinsBefore + amount > block.MaxCoins)
            {
                TellCharm($"That gem would now take your {block.DisplayName} charm past level {MaxCharmLevel}, so it was not used.");
                return;
            }
            // The gem goes first: if it cannot be used up, no coins are added (a gem never adds coins twice).
            if (!TryConsumeFromInventoryWithNetworking(gem, 1))
            {
                TellCharm("The gem could not be used, so nothing changed. Please try again.");
                return;
            }

            var level = SetCharmCoins(abilityId, coinsBefore + amount);
            var coins = GetCharmCoins(abilityId);
            log.Info($"[CHARM] {Name} put {amount} Prestige Coins into {block.DisplayName} with {gem.Name} ({gem.WeenieClassId}): {coinsBefore} -> {coins} coins, level {level}.");

            TellCharm($"Your {block.DisplayName} Charm now has {coins:N0} coins put in (+{amount:N0}). {CharmLevelText(block, coins)}.");
            TellCharm(CharmProgressText(block, coins));
            TellCharm(block.PlayerText(level));
            SaveBiotaToDatabase();
        }

        /// <summary>The Yes / No pop-up for a gem. Coins lead, as one coin can be well under 0.01 of a level:
        /// "Put 1 Prestige Coin into your Pandemic Charm?\n\nCoins: 963 -> 964\nLevel 5 (0% to 6), 258 more coins to level 6".</summary>
        public static string CharmInfusionPromptText(CharmSettingsManager.LeveledCharmBlock block, long coinsBefore, long amount)
        {
            var coinsAfter = coinsBefore + amount;
            var levelBefore = block.LevelFromCoins(coinsBefore);
            var levelAfter = block.LevelFromCoins(coinsAfter);

            string levelLine;
            if (levelAfter > levelBefore || levelAfter >= MaxCharmLevel)
                levelLine = $"Level {levelBefore} -> {CharmLevelText(block, coinsAfter)}";
            else
            {
                var toNext = block.CoinsToReach(levelAfter + 1) - coinsAfter;
                levelLine = $"{CharmLevelText(block, coinsAfter)}, {toNext:N0} more {(toNext == 1 ? "coin" : "coins")} to level {levelAfter + 1}";
            }

            return $"Put {amount:N0} Prestige {(amount == 1 ? "Coin" : "Coins")} into your {block.DisplayName} Charm?\n\nCoins: {coinsBefore:N0} -> {coinsAfter:N0}\n{levelLine}";
        }

        /// <summary>"Armor Breaker Charm - Level 41 (95% to 42) - 14,210 coins put in - next level in 46 coins".</summary>
        public static string CharmProgressText(CharmSettingsManager.LeveledCharmBlock block, long coins)
        {
            var level = block.LevelFromCoins(coins);
            var next = level >= MaxCharmLevel ? "" : $" - next level in {block.CoinsToReach(level + 1) - coins:N0} coins";
            return $"{block.DisplayName} Charm - {CharmLevelText(block, coins)} - {coins:N0} coins put in{next}";
        }

        /// <summary>True when the item is in this player's own pack or worn (not on the ground, not someone else's).</summary>
        private bool OwnsItem(WorldObject item) =>
            item != null && FindObject(item.Guid.Full, SearchLocations.MyInventory | SearchLocations.MyEquippedItems) != null;

        private void TellCharm(string text) => Session?.Network.EnqueueSend(new GameMessageSystemChat(text, ChatMessageType.Broadcast));
    }
}
