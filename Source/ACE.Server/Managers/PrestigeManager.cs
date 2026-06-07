using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using log4net;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    public static class PrestigeManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Tier 1 starts at Variation 11
        // Retail is 0-10 (technically 0 is main world, others are specialized)
        public const int PRESTIGE_VAR_OFFSET = 10;
        public const int PRESTIGE_BASE_VARIATION = PRESTIGE_VAR_OFFSET + 1;
        private const int DEFAULT_PRESTIGE_MAX_TIER = 10;

        public class AllowedLandblockInfo
        {
            public ushort Landblock { get; set; }
            public string AreaName { get; set; }
            public uint? BoundaryWcid { get; set; }
            public float? BoundaryScale { get; set; }
            public uint? BoundaryScriptId { get; set; }
            public bool IsWiped { get; set; }
        }

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
        private static Dictionary<int, Dictionary<ushort, AllowedLandblockInfo>> _tierAllowedLandblocks = GetDefaultAllowedLandblocks();
        private static readonly object _allowedLandblocksLock = new object();
        private static readonly object _migrationLock = new object();
        private static volatile bool _databaseInitialized;

        private static Dictionary<int, Dictionary<ushort, AllowedLandblockInfo>> GetDefaultAllowedLandblocks()
        {
            var result = new Dictionary<int, Dictionary<ushort, AllowedLandblockInfo>>();
            foreach (var kvp in _defaultTierAllowedLandblocks)
            {
                var dict = new Dictionary<ushort, AllowedLandblockInfo>();
                foreach (var lb in kvp.Value)
                {
                    dict[lb] = new AllowedLandblockInfo
                    {
                        Landblock = lb,
                        AreaName = "Default"
                    };
                }
                result[kvp.Key] = dict;
            }
            return result;
        }

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
                if (!_tierAllowedLandblocks.TryGetValue(tier, out var allowedDict))
                    return true; // No restrictions configured for this tier

                if (allowedDict.Count == 0) return true; // Empty list = no restrictions

                var allowed = allowedDict.ContainsKey(landblockId);
                if (!allowed && log.IsDebugEnabled)
                {
                    log.Debug($"[PrestigeZone] IsLandblockAllowed MISS: variation={variation} -> tier={tier}, landblock=0x{landblockId:X4} not in allowed list.");
                }
                return allowed;
            }
        }

        /// <summary>
        /// Retrieves the custom allowed landblock info for a landblock.
        /// </summary>
        public static AllowedLandblockInfo GetAllowedLandblockInfo(int? variation, ushort landblockId)
        {
            EnsureDatabaseInitialized();

            var tier = GetTier(variation);
            if (tier <= 0) return null;

            lock (_allowedLandblocksLock)
            {
                if (!_tierAllowedLandblocks.TryGetValue(tier, out var allowedDict))
                    return null;

                if (allowedDict.TryGetValue(landblockId, out var info))
                    return info;

                return null;
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

        public static string GetVariationLabel(int? variation)
        {
            if (!variation.HasValue)
                return "base (retail / default / null)";

            if (variation.Value == 0)
                return "0 (retail base)";

            if (variation.Value > 0 && variation.Value <= PRESTIGE_VAR_OFFSET)
                return $"{variation.Value} (retail variation)";

            var tier = GetTier(variation.Value);
            return $"{variation.Value} (Prestige Tier {tier})";
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
                if (!_tierAllowedLandblocks.TryGetValue(tier, out var allowedDict))
                    return null;

                return new HashSet<ushort>(allowedDict.Keys);
            }
        }

        public static Dictionary<int, HashSet<ushort>> GetAllAllowedLandblocks()
        {
            EnsureDatabaseInitialized();

            lock (_allowedLandblocksLock)
            {
                return _tierAllowedLandblocks.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new HashSet<ushort>(kvp.Value.Keys)
                );
            }
        }

        public static Dictionary<int, Dictionary<ushort, AllowedLandblockInfo>> GetAllAllowedLandblockInfos()
        {
            EnsureDatabaseInitialized();

            lock (_allowedLandblocksLock)
            {
                return _tierAllowedLandblocks.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ToDictionary(k => k.Key, k => new AllowedLandblockInfo
                    {
                        Landblock = k.Value.Landblock,
                        AreaName = k.Value.AreaName,
                        BoundaryWcid = k.Value.BoundaryWcid,
                        BoundaryScale = k.Value.BoundaryScale,
                        BoundaryScriptId = k.Value.BoundaryScriptId,
                        IsWiped = k.Value.IsWiped
                    })
                );
            }
        }

        private static long CountRowsForTier(WorldDbContext context, int tier)
        {
            var conn = context.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
                context.Database.OpenConnection();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM prestige_allowed_landblocks WHERE tier = @tier";
                if (context.Database.CurrentTransaction != null)
                    cmd.Transaction = context.Database.CurrentTransaction.GetDbTransaction();
                var p = cmd.CreateParameter();
                p.ParameterName = "@tier";
                p.Value = tier;
                cmd.Parameters.Add(p);
                return Convert.ToInt64(cmd.ExecuteScalar());
            }
            finally
            {
                if (!wasOpen)
                    context.Database.CloseConnection();
            }
        }

        /// <summary>
        /// If <paramref name="tier"/> has no rows in <c>prestige_allowed_landblocks</c>, insert the current effective allow-list
        /// (in-memory defaults merged with any loaded DB state) so the next add/remove is incremental rather than replacing the tier with a single row.
        /// </summary>
        private static void EnsureTierSeededFromEffectiveSet(WorldDbContext context, int tier)
        {
            if (CountRowsForTier(context, tier) > 0)
                return;

            Dictionary<ushort, AllowedLandblockInfo> snapshot;
            lock (_allowedLandblocksLock)
            {
                if (!_tierAllowedLandblocks.TryGetValue(tier, out var allowedDict) || allowedDict.Count == 0)
                    return;

                snapshot = new Dictionary<ushort, AllowedLandblockInfo>(allowedDict);
            }

            foreach (var kvp in snapshot)
            {
                var info = kvp.Value;
                context.Database.ExecuteSqlRaw(@"
                    INSERT INTO prestige_allowed_landblocks (tier, landblock, area_name, boundary_wcid, boundary_scale, boundary_script_id, is_wiped, is_active, updated_at)
                    VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, 1, UTC_TIMESTAMP())
                    ON DUPLICATE KEY UPDATE
                        area_name = {2},
                        boundary_wcid = {3},
                        boundary_scale = {4},
                        boundary_script_id = {5},
                        is_wiped = {6},
                        is_active = 1,
                        updated_at = UTC_TIMESTAMP()",
                    tier, (int)kvp.Key, info.AreaName, (int?)info.BoundaryWcid, (double?)info.BoundaryScale, (int?)info.BoundaryScriptId, info.IsWiped ? 1 : 0);
            }
        }

        public static bool AddAllowedLandblock(int tier, ushort landblock, string areaName = "Unnamed Zone", uint? boundaryWcid = null, float? boundaryScale = null, uint? boundaryScriptId = null, bool isWiped = false)
        {
            EnsureDatabaseInitialized();

            if (tier <= 0)
                return false;

            using var context = new WorldDbContext();
            using var transaction = context.Database.BeginTransaction();
            try
            {
                EnsureTierSeededFromEffectiveSet(context, tier);

                context.Database.ExecuteSqlRaw(@"
                    INSERT INTO prestige_allowed_landblocks (tier, landblock, area_name, boundary_wcid, boundary_scale, boundary_script_id, is_wiped, is_active, updated_at)
                    VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, 1, UTC_TIMESTAMP())
                    ON DUPLICATE KEY UPDATE
                        area_name = {2},
                        boundary_wcid = {3},
                        boundary_scale = {4},
                        boundary_script_id = {5},
                        is_wiped = {6},
                        is_active = 1,
                        updated_at = UTC_TIMESTAMP()",
                    tier, landblock, areaName, (int?)boundaryWcid, (double?)boundaryScale, (int?)boundaryScriptId, isWiped ? 1 : 0);

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }

            ReloadAllowedLandblocksFromDatabase();
            return true;
        }

        public static bool RemoveAllowedLandblock(int tier, ushort landblock)
        {
            if (tier <= 0)
                return false;

            EnsureDatabaseInitialized();

            using var context = new WorldDbContext();
            using var transaction = context.Database.BeginTransaction();
            try
            {
                EnsureTierSeededFromEffectiveSet(context, tier);

                var updated = context.Database.ExecuteSqlRaw(@"
                    UPDATE prestige_allowed_landblocks
                    SET is_active = 0, updated_at = UTC_TIMESTAMP()
                    WHERE tier = {0} AND landblock = {1} AND is_active = 1",
                    tier, landblock);

                transaction.Commit();
                ReloadAllowedLandblocksFromDatabase();
                return updated > 0;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public static bool UpdateWipeStatus(int tier, ushort landblock, bool isWiped)
        {
            EnsureDatabaseInitialized();

            if (tier <= 0)
                return false;

            using var context = new WorldDbContext();
            using var transaction = context.Database.BeginTransaction();
            try
            {
                var updated = context.Database.ExecuteSqlRaw(@"
                    UPDATE prestige_allowed_landblocks
                    SET is_wiped = {2}, updated_at = UTC_TIMESTAMP()
                    WHERE tier = {0} AND landblock = {1} AND is_active = 1",
                    tier, landblock, isWiped ? 1 : 0);

                transaction.Commit();
                if (updated > 0)
                {
                    ReloadAllowedLandblocksFromDatabase();
                    return true;
                }
                return false;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public static int UpdateWipeStatusByAreaName(int tier, string areaName, bool isWiped)
        {
            EnsureDatabaseInitialized();

            if (tier <= 0 || string.IsNullOrEmpty(areaName))
                return 0;

            using var context = new WorldDbContext();
            using var transaction = context.Database.BeginTransaction();
            try
            {
                var updated = context.Database.ExecuteSqlRaw(@"
                    UPDATE prestige_allowed_landblocks
                    SET is_wiped = {2}, updated_at = UTC_TIMESTAMP()
                    WHERE tier = {0} AND LOWER(area_name) = LOWER({1}) AND is_active = 1",
                    tier, areaName, isWiped ? 1 : 0);

                transaction.Commit();
                if (updated > 0)
                {
                    ReloadAllowedLandblocksFromDatabase();
                }
                return updated;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>Loads active rows from <c>prestige_allowed_landblocks</c> into <see cref="_tierAllowedLandblocks"/>.</summary>
        private static void ReloadAllowedLandblocksFromDatabaseInternal()
        {
            log.Info("[PrestigeZone] Reloading prestige allowed landblocks from database...");
            var fromDb = new Dictionary<int, Dictionary<ushort, AllowedLandblockInfo>>();
            var seededTiers = new HashSet<int>();
            using var context = new WorldDbContext();
            context.Database.OpenConnection();
            try
            {
                var connection = context.Database.GetDbConnection();

                using (var tierCmd = connection.CreateCommand())
                {
                    tierCmd.CommandText = "SELECT DISTINCT tier FROM prestige_allowed_landblocks";
                    using var tierReader = tierCmd.ExecuteReader();
                    while (tierReader.Read())
                        seededTiers.Add(tierReader.GetInt32(0));
                }

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT tier, landblock, area_name, boundary_wcid, boundary_scale, boundary_script_id, is_wiped
                    FROM prestige_allowed_landblocks
                    WHERE is_active = 1
                    ORDER BY tier, landblock";

                using var reader = command.ExecuteReader();
                int count = 0;
                while (reader.Read())
                {
                    count++;
                    var tier = reader.GetInt32(0);
                    var landblock = Convert.ToUInt16(reader.GetInt32(1));
                    var areaName = reader.IsDBNull(2) ? "Unnamed Zone" : reader.GetString(2);
                    uint? boundaryWcid = reader.IsDBNull(3) ? null : (uint?)reader.GetInt32(3);
                    float? boundaryScale = reader.IsDBNull(4) ? null : (float?)reader.GetFloat(4);
                    uint? boundaryScriptId = reader.IsDBNull(5) ? null : (uint?)reader.GetInt32(5);
                    bool isWiped = reader.IsDBNull(6) ? false : reader.GetInt32(6) != 0;

                    if (!fromDb.TryGetValue(tier, out var dict))
                    {
                        dict = new Dictionary<ushort, AllowedLandblockInfo>();
                        fromDb[tier] = dict;
                    }

                    dict[landblock] = new AllowedLandblockInfo
                    {
                        Landblock = landblock,
                        AreaName = areaName,
                        BoundaryWcid = boundaryWcid,
                        BoundaryScale = boundaryScale,
                        BoundaryScriptId = boundaryScriptId,
                        IsWiped = isWiped
                    };
                }
                log.Info($"[PrestigeZone] Read {count} active landblock rows from database (tiers: {string.Join(", ", fromDb.Keys)})");
            }
            finally
            {
                context.Database.CloseConnection();
            }

            var loaded = GetDefaultAllowedLandblocks();
            foreach (var tier in seededTiers)
                loaded[tier] = new Dictionary<ushort, AllowedLandblockInfo>();

            foreach (var kvp in fromDb)
            {
                if (!loaded.TryGetValue(kvp.Key, out var dict))
                {
                    dict = new Dictionary<ushort, AllowedLandblockInfo>();
                    loaded[kvp.Key] = dict;
                }
                else
                {
                    dict.Clear();
                }
                foreach (var inner in kvp.Value)
                    dict[inner.Key] = inner.Value;
            }

            lock (_allowedLandblocksLock)
            {
                _tierAllowedLandblocks = loaded;
                log.Info($"[PrestigeZone] Cache updated: {string.Join(" | ", _tierAllowedLandblocks.Select(k => $"T{k.Key}={k.Value.Count}"))}");
            }
        }

        public static void ReloadAllowedLandblocksFromDatabase()
        {
            EnsureDatabaseInitialized();
            ReloadAllowedLandblocksFromDatabaseInternal();
        }

        /// <summary>
        /// Returns the HP multiplier for a given tier.
        /// Baseline: 1.0 (Tier 1 is the baseline, no scaling)
        /// </summary>
        public static float GetHPModifier(int tier)
        {
            if (tier <= 0) return 1.0f;
            // +25% HP per tier above Tier 1
            return 1.0f + ((tier - 1) * 0.25f);
        }

        /// <summary>
        /// Returns the Damage multiplier for a given tier.
        /// Baseline: 1.0 (Tier 1 is the baseline, no scaling)
        /// </summary>
        public static float GetDamageModifier(int tier)
        {
             if (tier <= 0) return 1.0f;
             // +15% Damage per tier above Tier 1
             return 1.0f + ((tier - 1) * 0.15f);
        }

        /// <summary>
        /// Returns the XP multiplier for a given tier.
        /// Baseline: 1.0 (Tier 1 is the baseline, no scaling)
        /// </summary>
        public static float GetXPRewardModifier(int tier)
        {
            if (tier <= 0) return 1.0f;
            // +10% XP per tier above Tier 1
            return 1.0f + ((tier - 1) * 0.10f);
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
        /// Returns the Value (Pyreal) multiplier for generated loot.
        /// </summary>
        public static float GetLootValueModifier(int tier)
        {
            if (tier <= 0) return 1.0f;
            // +20% Value per tier above Tier 1
            return 1.0f + ((tier - 1) * 0.20f);
        }

        /// <summary>
        /// Applies scaled bonuses to generated loot based on the monster's prestige tier.
        /// Does not modify <see cref="WorldObject.ItemWorkmanship"/> (workmanship stays as rolled/generated).
        /// </summary>
        public static void ApplyLootScaling(WorldObject wo, int tier)
        {
            if (tier <= 0) return;

            // 1. Mana bonus (10% more max mana per tier above Tier 1)
            if (wo.ItemMaxMana.HasValue)
            {
                wo.ItemMaxMana = (int?)Math.Round(wo.ItemMaxMana.Value * (1.0f + (tier - 1) * 0.1f));
                wo.ItemCurMana = wo.ItemMaxMana; // Fill it up
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
            var maxBefore = creature.Health.MaxValue;
            var healthPct = maxBefore > 0 ? (float)creature.Health.Current / maxBefore : 1f;

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

            if (hpMod != 1.0f)
            {
                var maxAfter = creature.Health.MaxValue;
                var newCur = (uint)Math.Clamp((uint)Math.Round(healthPct * maxAfter), 0u, maxAfter);
                creature.Health.Current = newCur;
            }
        }

        /// <summary>
        /// Clears prestige scaling from a creature without re-applying (use for admin tier 0 / retail).
        /// </summary>
        public static void ClearPrestigeScaling(Creature creature) => RemovePrestigeScaling(creature);

        /// <summary>
        /// Applies HP and Damage scaling to a spawned creature based on its location's prestige tier.
        /// </summary>
        public static void ApplyPrestigeScaling(Creature creature, int? variation = null)
        {
            var prev = creature.GetProperty(PropertyInt.PrestigeLevel) ?? 0;
            // Variations 11-20 are Prestige Tiers 1-10
            var tier = GetTier(variation ?? creature.Location?.Variation);
            if (tier <= 0)
            {
                if (prev > 0)
                    RemovePrestigeScaling(creature);
                return;
            }

            if (prev == tier)
                return;

            if (prev > 0)
                RemovePrestigeScaling(creature);

            // 1. HP Scaling (Multiply Base) — keep current health % across max-HP change (no free full heal)
            var hpMod = GetHPModifier(tier);
            if (hpMod != 1.0f)
            {
                var maxBefore = creature.Health.MaxValue;
                var healthPct = maxBefore > 0 ? (float)creature.Health.Current / maxBefore : 1f;
                creature.Health.StartingValue = (uint)Math.Round(creature.Health.StartingValue * hpMod);
                var maxAfter = creature.Health.MaxValue;
                creature.Health.Current = (uint)Math.Clamp((uint)Math.Round(healthPct * maxAfter), 0u, maxAfter);
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

        /// <summary>
        /// Prefer <see cref="WorldObject.Location"/>.Variation, then physics position variation, when comparing who should see whom.
        /// </summary>
        public static int? GetEffectiveVariationForVisibility(WorldObject wo)
        {
            if (wo == null)
                return null;

            // Most world objects have a location (or physics position) with a variation.
            // However, non-spatial objects (inventory items, escrow items, etc.) may not.
            // For networking boundaries we still need a deterministic "effective variation"
            // so we inherit it from the closest spatial owner (wielder/container/owner player).

            var direct = wo.Location?.Variation ?? wo.PhysicsObj?.Position.Variation;
            if (direct.HasValue)
                return direct;

            // Walk wielder / container chain (nested packs, etc.): same precedence as single-hop (wielder before container).
            var visited = new HashSet<uint> { wo.Guid.Full };
            for (var curr = wo; ; )
            {
                WorldObject next = null;
                if (curr.Wielder != null)
                    next = curr.Wielder;
                else if (curr.Container != null)
                    next = curr.Container;
                if (next == null)
                    break;
                if (!visited.Add(next.Guid.Full))
                    break;
                curr = next;
                var v = curr.Location?.Variation ?? curr.PhysicsObj?.Position.Variation;
                if (v.HasValue)
                    return v;
            }

            // Fallback for typical inventory items: inherit from online player owner.
            // (OwnerId is usually set for items in a player's inventory/equipment.)
            if (wo.OwnerId.HasValue)
            {
                var ownerPlayer = PlayerManager.GetOnlinePlayer(wo.OwnerId.Value);
                var viaOwnerPlayer = ownerPlayer?.Location?.Variation ?? ownerPlayer?.PhysicsObj?.Position.Variation;
                if (viaOwnerPlayer.HasValue)
                    return viaOwnerPlayer;
            }

            return null;
        }

        /// <summary>
        /// True when two variation values should share client <c>CreateObject</c> networking.
        /// Prestige variations (&gt; <see cref="PRESTIGE_VAR_OFFSET"/>) require an exact nullable match.
        /// Retail treats <c>null</c> and <c>0</c> as one &quot;base&quot; bucket; explicit retail layers <c>1..PRESTIGE_VAR_OFFSET</c> match only the same value.
        /// </summary>
        public static bool SameVariationForVisibility(int? a, int? b)
        {
            if (IsPrestigeVariation(a) || IsPrestigeVariation(b))
                return a == b;

            static int? NormalizeRetailBase(int? v)
            {
                if (!v.HasValue || v.Value == 0)
                    return null;
                return v;
            }

            var ca = NormalizeRetailBase(a);
            var cb = NormalizeRetailBase(b);
            return ca == cb;
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
                ReloadAllowedLandblocksFromDatabaseInternal();
                _databaseInitialized = true;
            }
        }

        private static void EnsurePrestigeAllowedLandblocksTable(WorldDbContext context)
        {
            context.Database.OpenConnection();
            try
            {
                var connection = context.Database.GetDbConnection();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT COUNT(*) FROM information_schema.tables
                        WHERE table_schema = DATABASE() AND table_name = 'prestige_allowed_landblocks'";
                    var count = Convert.ToInt64(cmd.ExecuteScalar());
                    if (count == 0)
                    {
                        context.Database.ExecuteSqlRaw(@"
                            CREATE TABLE `prestige_allowed_landblocks` (
                                `id` int(11) NOT NULL AUTO_INCREMENT,
                                `tier` int(11) NOT NULL,
                                `landblock` int(11) NOT NULL,
                                `area_name` varchar(100) NOT NULL DEFAULT 'Unnamed Zone',
                                `boundary_wcid` int(11) NULL DEFAULT NULL,
                                `boundary_scale` float NULL DEFAULT NULL,
                                `boundary_script_id` int(11) NULL DEFAULT NULL,
                                `is_active` tinyint(1) NOT NULL DEFAULT 1,
                                `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                                PRIMARY KEY (`id`),
                                UNIQUE KEY `ux_prestige_tier_landblock` (`tier`, `landblock`),
                                KEY `ix_prestige_tier_active` (`tier`, `is_active`),
                                KEY `ix_prestige_landblock_active` (`landblock`, `is_active`)
                            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                        return;
                    }
                }

                // If table already exists, make sure columns exist
                var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT COLUMN_NAME FROM information_schema.columns
                        WHERE table_schema = DATABASE() AND table_name = 'prestige_allowed_landblocks'";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                        existingColumns.Add(reader.GetString(0));
                }

                if (!existingColumns.Contains("area_name"))
                {
                    context.Database.ExecuteSqlRaw("ALTER TABLE `prestige_allowed_landblocks` ADD COLUMN `area_name` varchar(100) NOT NULL DEFAULT 'Unnamed Zone'");
                }
                if (!existingColumns.Contains("boundary_wcid"))
                {
                    context.Database.ExecuteSqlRaw("ALTER TABLE `prestige_allowed_landblocks` ADD COLUMN `boundary_wcid` int(11) NULL DEFAULT NULL");
                }
                if (!existingColumns.Contains("boundary_scale"))
                {
                    context.Database.ExecuteSqlRaw("ALTER TABLE `prestige_allowed_landblocks` ADD COLUMN `boundary_scale` float NULL DEFAULT NULL");
                }
                if (!existingColumns.Contains("boundary_script_id"))
                {
                    context.Database.ExecuteSqlRaw("ALTER TABLE `prestige_allowed_landblocks` ADD COLUMN `boundary_script_id` int(11) NULL DEFAULT NULL");
                }
                if (!existingColumns.Contains("is_wiped"))
                {
                    context.Database.ExecuteSqlRaw("ALTER TABLE `prestige_allowed_landblocks` ADD COLUMN `is_wiped` tinyint(1) NOT NULL DEFAULT 0");
                }
            }
            finally
            {
                context.Database.CloseConnection();
            }
        }
    }
}
