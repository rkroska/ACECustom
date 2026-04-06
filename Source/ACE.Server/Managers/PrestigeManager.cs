using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ACE.Database.Models.World;
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
        public const int PRESTIGE_BASE_VARIATION = PRESTIGE_VAR_OFFSET + 1;
        private const int DEFAULT_PRESTIGE_MAX_TIER = 10;

        /// <summary>
        /// Defines allowed landblocks for each prestige tier.
        /// Prevents players from exploring undesigned areas of the map.
        /// Empty HashSet = no restrictions for that tier (allows free exploration).
        /// </summary>
        private static readonly Dictionary<int, HashSet<ushort>> _defaultTierAllowedLandblocks = new()
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
        private static Dictionary<int, HashSet<ushort>> _tierAllowedLandblocks = CloneAllowedLandblocks(_defaultTierAllowedLandblocks);
        private static readonly object _allowedLandblocksLock = new object();
        private static readonly object _migrationLock = new object();
        private static volatile bool _databaseInitialized;

        /// <summary>
        /// Checks if a landblock is allowed for the given variation.
        /// Returns true if no restrictions configured or variation is retail (0-10).
        /// </summary>
        public static bool IsLandblockAllowed(int? variation, ushort landblockId)
        {
            EnsureDatabaseInitialized();

            var tier = GetTier(variation);
            if (tier <= 0) return true; // Retail zones unrestricted

            lock (_allowedLandblocksLock)
            {
                if (!_tierAllowedLandblocks.TryGetValue(tier, out var allowed))
                    return true; // No restrictions configured for this tier

                if (allowed.Count == 0) return true; // Empty list = no restrictions

                return allowed.Contains(landblockId);
            }
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

        /// <summary>
        /// Tier passed into kill XP / luminance / loot scaling: from the creature's <see cref="WorldObject.Location"/> variation only
        /// (<see cref="GetTier(int?)"/> — null and 0–10 → 0, 11+ → prestige). Retail instances are never scaled from a stale <see cref="PropertyInt.PrestigeLevel"/>.
        /// </summary>
        public static int GetKillScalingMonsterTier(WorldObject wo) => GetTier(wo?.Location?.Variation);

        public static HashSet<ushort> GetAllowedLandblocks(int? variation)
        {
            EnsureDatabaseInitialized();

            var tier = GetTier(variation);
            if (tier <= 0)
                return null;

            lock (_allowedLandblocksLock)
            {
                if (!_tierAllowedLandblocks.TryGetValue(tier, out var allowed))
                    return null;

                return allowed;
            }
        }

        public static Dictionary<int, HashSet<ushort>> GetAllAllowedLandblocks()
        {
            EnsureDatabaseInitialized();

            lock (_allowedLandblocksLock)
                return CloneAllowedLandblocks(_tierAllowedLandblocks);
        }

        public static bool AddAllowedLandblock(int tier, ushort landblock)
        {
            EnsureDatabaseInitialized();

            if (tier <= 0)
                return false;

            using var context = new WorldDbContext();
            context.Database.ExecuteSqlRaw(@"
                INSERT INTO prestige_allowed_landblocks (tier, landblock, is_active, updated_at)
                VALUES ({0}, {1}, 1, UTC_TIMESTAMP())
                ON DUPLICATE KEY UPDATE
                    is_active = 1,
                    updated_at = UTC_TIMESTAMP()",
                tier, landblock);

            ReloadAllowedLandblocksFromDatabase();
            return true;
        }

        public static bool RemoveAllowedLandblock(int tier, ushort landblock)
        {
            EnsureDatabaseInitialized();

            using var context = new WorldDbContext();
            var updated = context.Database.ExecuteSqlRaw(@"
                UPDATE prestige_allowed_landblocks
                SET is_active = 0, updated_at = UTC_TIMESTAMP()
                WHERE tier = {0} AND landblock = {1} AND is_active = 1",
                tier, landblock);

            ReloadAllowedLandblocksFromDatabase();
            return updated > 0;
        }

        public static void ReloadAllowedLandblocksFromDatabase()
        {
            EnsureDatabaseInitialized();

            var loaded = new Dictionary<int, HashSet<ushort>>();
            using var context = new WorldDbContext();
            context.Database.OpenConnection();
            try
            {
                var connection = context.Database.GetDbConnection();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT tier, landblock
                    FROM prestige_allowed_landblocks
                    WHERE is_active = 1
                    ORDER BY tier, landblock";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var tier = reader.GetInt32(0);
                    var landblock = Convert.ToUInt16(reader.GetInt32(1));

                    if (!loaded.TryGetValue(tier, out var set))
                    {
                        set = new HashSet<ushort>();
                        loaded[tier] = set;
                    }
                    set.Add(landblock);
                }
            }
            finally
            {
                context.Database.CloseConnection();
            }

            if (loaded.Count == 0)
                loaded = CloneAllowedLandblocks(_defaultTierAllowedLandblocks);

            lock (_allowedLandblocksLock)
                _tierAllowedLandblocks = loaded;
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
        /// Reverts HP and damage changes from <see cref="ApplyPrestigeScaling"/> using <see cref="PropertyInt.PrestigeLevel"/> as the tier that was applied.
        /// Call before re-applying a different tier so stats replace rather than compound.
        /// </summary>
        public static void RemovePrestigeScaling(Creature creature)
        {
            var oldTier = creature.GetProperty(PropertyInt.PrestigeLevel) ?? 0;
            if (oldTier <= 0)
                return;

            var hpMod = GetHPModifier(oldTier);
            if (hpMod != 1.0f)
                creature.Health.StartingValue = (uint)Math.Round(creature.Health.StartingValue / hpMod);

            var dmgMod = GetDamageModifier(oldTier);
            if (dmgMod != 1.0f)
            {
                var rating = ModToRating(dmgMod);
                var existing = creature.GetProperty(PropertyInt.DamageRating) ?? 0;
                var next = Math.Max(0, existing - rating);
                if (next == 0)
                    creature.RemoveProperty(PropertyInt.DamageRating);
                else
                    creature.SetProperty(PropertyInt.DamageRating, next);
            }

            creature.RemoveProperty(PropertyInt.PrestigeLevel);
            creature.SetMaxVitals();
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
                // Convert 1.15x -> 15 Damage Rating; stack on existing rating from the weenie
                var rating = ModToRating(dmgMod);
                var existing = creature.GetProperty(PropertyInt.DamageRating) ?? 0;
                creature.SetProperty(PropertyInt.DamageRating, existing + rating);
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

        public static bool IsPrestigeVariation(int? variation)
        {
            return variation.HasValue && variation.Value > PRESTIGE_VAR_OFFSET;
        }

        public static int GetBasePrestigeVariation()
        {
            return PRESTIGE_BASE_VARIATION;
        }

        public static int GetMaxConfiguredPrestigeVariation()
        {
            EnsureDatabaseInitialized();

            var maxTier = DEFAULT_PRESTIGE_MAX_TIER;
            lock (_allowedLandblocksLock)
            {
                if (_tierAllowedLandblocks.Count > 0)
                    maxTier = Math.Max(maxTier, _tierAllowedLandblocks.Keys.Max());
            }
            return PRESTIGE_VAR_OFFSET + maxTier;
        }

        public static List<int> GetDefaultMirrorTargetVariations(int? sourceVariation)
        {
            if (!sourceVariation.HasValue || sourceVariation.Value != PRESTIGE_BASE_VARIATION)
                return new List<int>();

            var targets = new List<int>();
            var maxVariation = GetMaxConfiguredPrestigeVariation();
            for (var variation = PRESTIGE_BASE_VARIATION + 1; variation <= maxVariation; variation++)
                targets.Add(variation);

            return targets;
        }

        public static List<int> NormalizeMirrorTargetVariations(IEnumerable<int> requestedTargets, int? sourceVariation)
        {
            var source = sourceVariation ?? 0;
            var maxVariation = GetMaxConfiguredPrestigeVariation();

            return requestedTargets
                .Where(v => v > PRESTIGE_VAR_OFFSET && v <= maxVariation && v != source)
                .Distinct()
                .OrderBy(v => v)
                .ToList();
        }

        public static bool IsCreateInstMirrorEligible(WeenieType weenieType, bool hasGeneratorProfiles)
        {
            if (hasGeneratorProfiles)
                return true;

            return weenieType switch
            {
                WeenieType.Creature => true,
                WeenieType.Vendor => true,
                WeenieType.Portal => true,
                WeenieType.LifeStone => true,
                WeenieType.Door => true,
                WeenieType.Chest => true,
                WeenieType.Container => true,
                WeenieType.PressurePlate => true,
                WeenieType.Switch => true,
                WeenieType.LightSource => true,
                WeenieType.Generic => true,
                _ => false,
            };
        }

        private static void EnsureDatabaseInitialized()
        {
            if (_databaseInitialized) return;

            lock (_migrationLock)
            {
                if (_databaseInitialized) return;

                using var context = new WorldDbContext();
                EnsurePrestigeAllowedLandblocksTable(context);
                _databaseInitialized = true;
            }
        }

        private static void EnsurePrestigeAllowedLandblocksTable(WorldDbContext context)
        {
            context.Database.OpenConnection();
            try
            {
                using var cmd = context.Database.GetDbConnection().CreateCommand();
                cmd.CommandText = @"
                    SELECT COUNT(*) FROM information_schema.tables
                    WHERE table_schema = DATABASE() AND table_name = 'prestige_allowed_landblocks'";
                var count = Convert.ToInt64(cmd.ExecuteScalar());
                if (count > 0)
                    return;

                context.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS `prestige_allowed_landblocks` (
                        `id` int(11) NOT NULL AUTO_INCREMENT,
                        `tier` int(11) NOT NULL,
                        `landblock` int(11) NOT NULL,
                        `is_active` tinyint(1) NOT NULL DEFAULT 1,
                        `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                        PRIMARY KEY (`id`),
                        UNIQUE KEY `ux_prestige_tier_landblock` (`tier`, `landblock`),
                        KEY `ix_prestige_tier_active` (`tier`, `is_active`),
                        KEY `ix_prestige_landblock_active` (`landblock`, `is_active`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
            }
            finally
            {
                context.Database.CloseConnection();
            }
        }

        private static Dictionary<int, HashSet<ushort>> CloneAllowedLandblocks(Dictionary<int, HashSet<ushort>> source)
        {
            return source.ToDictionary(kvp => kvp.Key, kvp => new HashSet<ushort>(kvp.Value));
        }
    }
}
