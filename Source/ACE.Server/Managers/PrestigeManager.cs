using System;
using System.Collections.Generic;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    public static class PrestigeManager
    {
        // Tier 1 starts at Variation 11
        // Retail is 0-10 (technically 0 is main world, others are specialized)
        public const int PRESTIGE_VAR_OFFSET = 10;

        /// <summary>
        /// Defines allowed landblocks for each prestige tier.
        /// Prevents players from exploring undesigned areas of the map.
        /// Empty HashSet = no restrictions for that tier (allows free exploration).
        /// </summary>
        private static readonly Dictionary<int, HashSet<ushort>> _tierAllowedLandblocks = new()
        {
            // Test configuration: Landblock 0xEAEA for all tiers during development
            // Expand these lists as content is designed for each tier
            [1] = new HashSet<ushort> { 0xEAEA },
            [2] = new HashSet<ushort> { 0xEAEA },
            [3] = new HashSet<ushort> { 0xEAEA },
            [4] = new HashSet<ushort> { 0xEAEA },
            [5] = new HashSet<ushort> { 0xEAEA },
            [6] = new HashSet<ushort> { 0xEAEA },
            [7] = new HashSet<ushort> { 0xEAEA },
            [8] = new HashSet<ushort> { 0xEAEA },
            [9] = new HashSet<ushort> { 0xEAEA },
            [10] = new HashSet<ushort> { 0xEAEA },
        };

        /// <summary>
        /// Checks if a landblock is allowed for the given variation.
        /// Returns true if no restrictions configured or variation is retail (0-10).
        /// </summary>
        public static bool IsLandblockAllowed(int? variation, ushort landblockId)
        {
            var tier = GetTier(variation);
            if (tier <= 0) return true; // Retail zones unrestricted
            
            if (!_tierAllowedLandblocks.TryGetValue(tier, out var allowed))
                return true; // No restrictions configured for this tier
            
            if (allowed.Count == 0) return true; // Empty list = no restrictions
            


            return allowed.Contains(landblockId);
        }

        /// <summary>
        /// Converts a Variation ID to a Prestige Tier.
        /// Retail (Null/0-10) returns 0.
        /// Variation 11 returns Tier 1.
        /// </summary>
        public static int GetTier(int variation)
        {
            if (variation <= PRESTIGE_VAR_OFFSET) return 0;
            return variation - PRESTIGE_VAR_OFFSET;
        }

        public static int GetTier(int? variation)
        {
            if (!variation.HasValue) return 0;
            return GetTier(variation.Value);
        }

        public static HashSet<ushort> GetAllowedLandblocks(int? variation)
        {
            var tier = GetTier(variation);
            if (tier <= 0 || !_tierAllowedLandblocks.TryGetValue(tier, out var allowed))
                return null;
            
            return allowed;
        }

        /// <summary>
        /// Returns the HP multiplier for a given tier.
        /// Baseline: 1.0
        /// </summary>
        public static float GetHPModifier(int tier)
        {
            if (tier <= 0) return 1.0f;
            // +25% HP per tier
            return 1.0f + (tier * 0.25f);
        }

        /// <summary>
        /// Returns the Damage multiplier for a given tier.
        /// Baseline: 1.0
        /// </summary>
        public static float GetDamageModifier(int tier)
        {
             if (tier <= 0) return 1.0f;
             // +15% Damage per tier
             return 1.0f + (tier * 0.15f);
        }

        /// <summary>
        /// Returns the XP multiplier for a given tier.
        /// Baseline: 1.0
        /// </summary>
        public static float GetXPRewardModifier(int tier)
        {
            if (tier <= 0) return 1.0f;
            // +10% XP per tier
            return 1.0f + (tier * 0.10f);
        }

        /// <summary>
        /// Returns the XP multiplier for a player killing a monster.
        /// Applies a 20% penalty for each tier the player is ABOVE the monster.
        /// </summary>
        public static float GetXPPenaltyMultiplier(int playerTier, int monsterTier)
        {
            if (playerTier <= monsterTier) return 1.0f;

            var diff = playerTier - monsterTier;
            // -20% XP per tier diff
            var multiplier = 1.0f - (diff * 0.20f);

            return Math.Max(0.0f, multiplier);
        }


        /// <summary>
        /// Returns the Workmanship/Mana bonus for generated loot.
        /// </summary>
        public static float GetLootWorkmanshipBonus(int tier)
        {
            if (tier <= 0) return 0.0f;
            // +1.0 Workmanship per tier (This is significant for loot gen)
            return tier * 1.0f;
        }

        /// <summary>
        /// Returns the Value (Pyreal) multiplier for generated loot.
        /// </summary>
        public static float GetLootValueModifier(int tier)
        {
            if (tier <= 0) return 1.0f;
            // +20% Value per tier
            return 1.0f + (tier * 0.20f);
        }

        /// <summary>
        /// Applies scaled bonuses to generated loot based on the monster's prestige tier.
        /// </summary>
        public static void ApplyLootScaling(WorldObject wo, int tier)
        {
            if (tier <= 0) return;

            // 1. Workmanship / Mana bonus
            var workmanshipBonus = GetLootWorkmanshipBonus(tier);
            if (workmanshipBonus > 0)
            {
                if (wo.ItemWorkmanship.HasValue)
                {
                    wo.ItemWorkmanship += (int)Math.Round(workmanshipBonus);
                }

                // Items with Mana also benefit from Workmanship scaling (e.g. 10% more mana per tier)
                if (wo.ItemMaxMana.HasValue)
                {
                    wo.ItemMaxMana = (int?)Math.Round(wo.ItemMaxMana.Value * (1.0f + tier * 0.1f));
                    wo.ItemCurMana = wo.ItemMaxMana; // Fill it up
                }
            }

            // 2. Value Bonus
            var valueMod = GetLootValueModifier(tier);
            if (valueMod != 1.0f)
            {
                if (wo.Value.HasValue)
                    wo.Value = (int?)Math.Round(wo.Value.Value * valueMod);
            }
        }

        /// <summary>
        /// Applies HP and Damage scaling to a spawned creature based on its location's prestige tier.
        /// </summary>
        public static void ApplyPrestigeScaling(Creature creature, int? variation = null)
        {
            // Variations 11-20 are Prestige Tiers 1-10
            var tier = GetTier(variation ?? creature.Location?.Variation);
            if (tier <= 0) return;

            // 1. HP Scaling (Mulitply Base)
            var hpMod = GetHPModifier(tier);
            if (hpMod != 1.0f)
            {
                creature.Health.StartingValue = (uint)Math.Round(creature.Health.StartingValue * hpMod);
                creature.SetMaxVitals(); // Refill to new max
            }

            // 2. Damage Scaling (Apply as DamageRating)
            var dmgMod = GetDamageModifier(tier);
            if (dmgMod != 1.0f)
            {
                // Convert 1.15x -> 15 Damage Rating
                var rating = ModToRating(dmgMod);
                creature.SetProperty(PropertyInt.DamageRating, rating);
            }

            // 3. Mark the creature with its tier for XP/Loot logic later
            creature.SetProperty(PropertyInt.PrestigeLevel, tier);
            
            // Log for visibility during testing
            // creature.EnqueueBroadcast(new Network.GameMessages.Messages.GameMessageSystemChat($"{creature.Name} spawned at Tier {tier}!", ChatMessageType.System));
        }

        /// <summary>
        /// Converts a 1.xx modifier to a +x rating (e.g. 1.15 -> 15)
        /// Copied from Creature_Rating for dependency-free use here.
        /// </summary>
        public static int ModToRating(float mod)
        {
            if (mod >= 1.0f)
                return (int)Math.Round(mod * 100 - 100);
            else
                return (int)Math.Round(-100 / mod + 100);
        }
    }
}
