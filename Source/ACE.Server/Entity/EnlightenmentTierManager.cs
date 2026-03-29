using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Database;
using ACE.Database.Models.Shard;

using Microsoft.EntityFrameworkCore;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Cached enlightenment tier bands from <c>config_enlightenment_tier</c>. Reloadable at runtime via admin command.
    /// </summary>
    public static class EnlightenmentTierManager
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly object Sync = new();
        private static readonly object EnsureLock = new();
        private static IReadOnlyList<EnlightenmentTierSnapshot> _tiers = Array.Empty<EnlightenmentTierSnapshot>();
        private static string _lastLoadDetail = "not loaded";

        private const string CreateTierTableSql = @"
CREATE TABLE IF NOT EXISTS `config_enlightenment_tier` (
  `id` int NOT NULL AUTO_INCREMENT,
  `sort_order` int NOT NULL DEFAULT 0,
  `min_target_enl` int NOT NULL,
  `max_target_enl` int DEFAULT NULL COMMENT 'NULL = open-ended (final row only)',
  `lum_base_per_target` bigint NOT NULL DEFAULT 0,
  `lum_step_anchor` int DEFAULT NULL,
  `lum_step_every` int DEFAULT NULL,
  `lum_step_increment` decimal(10,4) DEFAULT NULL COMMENT 'per step added to 1.0 base multiplier',
  `item_wcid` int DEFAULT NULL,
  `item_count_target_minus` int DEFAULT NULL COMMENT 'required stacks = max(0, T - value)',
  `item_label` varchar(80) DEFAULT NULL,
  `quest_stamp` varchar(100) DEFAULT NULL,
  `quest_failure_message` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `IX_config_enlightenment_tier_min` (`min_target_enl`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";

        private const string SeedTierRowsSql = @"
INSERT INTO `config_enlightenment_tier`
  (`sort_order`, `min_target_enl`, `max_target_enl`, `lum_base_per_target`, `lum_step_anchor`, `lum_step_every`, `lum_step_increment`, `item_wcid`, `item_count_target_minus`, `item_label`, `quest_stamp`, `quest_failure_message`)
SELECT * FROM (
  SELECT 1 AS `sort_order`, 1 AS `min_target_enl`, 5 AS `max_target_enl`, CAST(0 AS SIGNED) AS `lum_base_per_target`, NULL AS `lum_step_anchor`, NULL AS `lum_step_every`, NULL AS `lum_step_increment`, NULL AS `item_wcid`, NULL AS `item_count_target_minus`, NULL AS `item_label`, NULL AS `quest_stamp`, NULL AS `quest_failure_message`
  UNION ALL SELECT 2, 6, 50, 0, NULL, NULL, NULL, 300000, 5, 'Enlightenment Tokens', NULL, NULL
  UNION ALL SELECT 3, 51, 150, 100000000, NULL, NULL, NULL, 300000, 5, 'Enlightenment Tokens', NULL, NULL
  UNION ALL SELECT 4, 151, 300, 1000000000, NULL, NULL, NULL, 90000217, 5, 'Enlightenment Medallions', 'ParagonEnlCompleted', 'You must have completed 50th Paragon to enlighten beyond level 150.'
  UNION ALL SELECT 5, 301, 325, 2000000000, 300, 50, 0.5000, 300101189, 5, 'Enlightenment Sigils', 'ParagonArmorCompleted', 'You must have completed 50th Armor Paragon to enlighten beyond level 300.'
  UNION ALL SELECT 6, 326, NULL, 2000000000, 300, 50, 0.5000, 98769999, 5, 'Crest of Enlightenment', 'ParagonArmorCompleted', 'You must have completed 50th Armor Paragon to enlighten beyond level 300.'
) AS `seed`
WHERE NOT EXISTS (SELECT 1 FROM `config_enlightenment_tier` LIMIT 1)";

        /// <summary>True if the active configuration came from the database (not compiled fallback).</summary>
        public static bool LoadedFromDatabase { get; private set; }

        /// <summary>
        /// Creates <c>config_enlightenment_tier</c> when missing and seeds default rows when the table is empty.
        /// Matches <c>Database/Updates/Shard/2026-03-28-00-Config-Enlightenment-Tier.sql</c>.
        /// </summary>
        public static void EnsureTableCreated()
        {
            lock (EnsureLock)
            {
                try
                {
                    ApplyTierTableSchema();
                    log.Info("config_enlightenment_tier: table ensured; default rows added if the table was empty.");
                }
                catch (Exception ex)
                {
                    log.Error($"Failed to create or seed config_enlightenment_tier: {ex.Message}", ex);
                }
            }
        }

        private static void ApplyTierTableSchema()
        {
            using var context = new ShardDbContext();
            context.Database.ExecuteSqlRaw(CreateTierTableSql);
            context.Database.ExecuteSqlRaw(SeedTierRowsSql);
        }

        public static void Initialize()
        {
            TryReload(out _);
        }

        /// <summary>
        /// Reload tier rows from the shard database. Runs the same create-if-missing and empty-table seed as <see cref="EnsureTableCreated"/> before loading.
        /// On failure: if there is no usable in-memory tier set yet, uses compiled defaults (last resort) and logs a warning; if a prior load succeeded, keeps the previous tier snapshot and logs a warning.
        /// </summary>
        public static bool TryReload(out string message)
        {
            lock (Sync)
            {
                try
                {
                    ApplyTierTableSchema();

                    var rows = DatabaseManager.EnlightenmentTiers.GetAllTiers();
                    if (rows == null || rows.Count == 0)
                    {
                        ApplyFallbackNoRows();
                        message = _lastLoadDetail;
                        return false;
                    }

                    var snapshots = rows.Select(EnlightenmentTierSnapshot.FromDb).ToList();
                    ValidateTiers(snapshots);
                    _tiers = snapshots;
                    LoadedFromDatabase = true;
                    _lastLoadDetail = $"Loaded {_tiers.Count} enlightenment tier row(s) from database.";
                    log.Info(_lastLoadDetail);
                    message = _lastLoadDetail;
                    return true;
                }
                catch (Exception ex)
                {
                    ApplyFallbackAfterException(ex);
                    message = _lastLoadDetail;
                    return false;
                }
            }
        }

        private static void ApplyFallbackNoRows()
        {
            if (_tiers.Count == 0)
            {
                var defaults = BuildCompiledDefaults().ToList();
                ValidateTiers(defaults);
                _tiers = defaults;
                LoadedFromDatabase = false;
                _lastLoadDetail = "config_enlightenment_tier has no rows; using compiled defaults.";
                log.Warn(_lastLoadDetail);
            }
            else
            {
                _lastLoadDetail = "Reload failed: config_enlightenment_tier has no rows. Previous enlightenment tier configuration retained.";
                log.Warn(_lastLoadDetail);
            }
        }

        private static void ApplyFallbackAfterException(Exception ex)
        {
            if (_tiers.Count == 0)
            {
                var defaults = BuildCompiledDefaults().ToList();
                ValidateTiers(defaults);
                _tiers = defaults;
                LoadedFromDatabase = false;
                _lastLoadDetail = $"Failed to load config_enlightenment_tier: {ex.Message}. Using compiled defaults.";
                log.Warn(_lastLoadDetail);
            }
            else
            {
                _lastLoadDetail = $"Failed to reload config_enlightenment_tier: {ex.Message}. Previous enlightenment tier configuration retained.";
                log.Warn(_lastLoadDetail);
            }
        }

        public static string GetLoadStatus() => _lastLoadDetail;

        /// <summary>
        /// Resolves the tier for target enlightenment T. When multiple rows would match (e.g. a misconfigured
        /// band with <c>max_target_enl</c> NULL that is not the final row), the row with the greatest
        /// <see cref="EnlightenmentTierSnapshot.MinTargetEnl"/> wins (most specific band).
        /// </summary>
        public static bool TryGetTier(int targetEnlightenment, out EnlightenmentTierSnapshot tier)
        {
            tier = null;
            foreach (var t in _tiers)
            {
                if (targetEnlightenment < t.MinTargetEnl)
                    continue;
                if (t.MaxTargetEnl.HasValue && targetEnlightenment > t.MaxTargetEnl.Value)
                    continue;
                if (tier == null || t.MinTargetEnl > tier.MinTargetEnl)
                    tier = t;
            }
            return tier != null;
        }

        public static long CalculateLuminanceCost(int targetEnlightenment)
        {
            if (!TryGetTier(targetEnlightenment, out var tier))
            {
                log.Error($"EnlightenmentTierManager: no tier for target {targetEnlightenment}.");
                return long.MaxValue;
            }
            return tier.CalculateLuminanceCost(targetEnlightenment);
        }

        private static void ValidateTiers(List<EnlightenmentTierSnapshot> sorted)
        {
            sorted.Sort((a, b) => a.MinTargetEnl.CompareTo(b.MinTargetEnl));
            if (sorted.Count == 0)
                throw new InvalidOperationException("No tiers defined.");

            if (sorted[0].MinTargetEnl != 1)
                throw new InvalidOperationException($"First tier must start at MinTargetEnl = 1 (got {sorted[0].MinTargetEnl}).");

            for (var i = 0; i < sorted.Count; i++)
            {
                var t = sorted[i];
                if (t.MaxTargetEnl.HasValue && t.MaxTargetEnl.Value < t.MinTargetEnl)
                    throw new InvalidOperationException($"Tier id {t.Id}: MaxTargetEnl < MinTargetEnl.");

                if (t.LumBasePerTarget < 0)
                    throw new InvalidOperationException($"Tier id {t.Id}: LumBasePerTarget must be non-negative.");
                if (t.LumStepAnchor.HasValue && t.LumStepAnchor.Value < 0)
                    throw new InvalidOperationException($"Tier id {t.Id}: LumStepAnchor must be non-negative.");
                if (t.LumStepIncrement.HasValue && t.LumStepIncrement.Value < 0)
                    throw new InvalidOperationException($"Tier id {t.Id}: LumStepIncrement must be non-negative.");

                if (t.ItemWcid.HasValue && !t.ItemCountTargetMinus.HasValue)
                    throw new InvalidOperationException($"Tier id {t.Id}: ItemWcid set but ItemCountTargetMinus is null.");

                if (t.ItemWcid.HasValue && string.IsNullOrEmpty(t.ItemLabel))
                    throw new InvalidOperationException($"Tier id {t.Id}: ItemWcid set but ItemLabel is empty.");

                var stepFields = 0;
                if (t.LumStepAnchor.HasValue) stepFields++;
                if (t.LumStepEvery.HasValue) stepFields++;
                if (t.LumStepIncrement.HasValue) stepFields++;
                if (stepFields != 0 && stepFields != 3)
                    throw new InvalidOperationException($"Tier id {t.Id}: set all of LumStepAnchor, LumStepEvery, LumStepIncrement, or none.");

                if (t.LumStepEvery.HasValue && t.LumStepEvery.Value <= 0)
                    throw new InvalidOperationException($"Tier id {t.Id}: LumStepEvery must be positive.");

                if (i < sorted.Count - 1)
                {
                    if (!t.MaxTargetEnl.HasValue)
                        throw new InvalidOperationException($"Tier id {t.Id}: only the final row may have NULL MaxTargetEnl.");
                    var next = sorted[i + 1];
                    if (next.MinTargetEnl != t.MaxTargetEnl.Value + 1)
                        throw new InvalidOperationException(
                            $"Gap or overlap between tier id {t.Id} (max {t.MaxTargetEnl}) and next tier (min {next.MinTargetEnl}).");
                }
                else
                {
                    if (t.MaxTargetEnl.HasValue)
                        throw new InvalidOperationException("Final tier must have MaxTargetEnl = NULL (open-ended).");
                }
            }

            // Spot-check: every T in a reasonable range hits exactly one tier
            const int checkMax = 10000;
            for (var t = 1; t <= checkMax; t++)
            {
                var count = sorted.Count(x =>
                    t >= x.MinTargetEnl && (!x.MaxTargetEnl.HasValue || t <= x.MaxTargetEnl.Value));
                if (count != 1)
                    throw new InvalidOperationException($"Target enlightenment {t} matches {count} tier(s); expected exactly 1.");
            }
        }

        private static IReadOnlyList<EnlightenmentTierSnapshot> BuildCompiledDefaults()
        {
            // Mirrors pre-refactor Enlightenment.cs + default ServerConfig luminance bases.
            const long enl50 = 100_000_000L;
            const long enl150 = 1_000_000_000L;
            const long enl300 = 2_000_000_000L;
            return new EnlightenmentTierSnapshot[]
            {
                new(0, 1, 5, 0, null, null, null, null, null, null, null, null),
                new(0, 6, 50, 0, null, null, null, 300000, 5, "Enlightenment Tokens", null, null),
                new(0, 51, 150, enl50, null, null, null, 300000, 5, "Enlightenment Tokens", null, null),
                new(0, 151, 300, enl150, null, null, null, 90000217, 5, "Enlightenment Medallions", "ParagonEnlCompleted",
                    "You must have completed 50th Paragon to enlighten beyond level 150."),
                new(0, 301, 325, enl300, 300, 50, 0.5m, 300101189, 5, "Enlightenment Sigils", "ParagonArmorCompleted",
                    "You must have completed 50th Armor Paragon to enlighten beyond level 300."),
                new(0, 326, null, enl300, 300, 50, 0.5m, 98769999, 5, "Crest of Enlightenment", "ParagonArmorCompleted",
                    "You must have completed 50th Armor Paragon to enlighten beyond level 300."),
            };
        }
    }

    public sealed class EnlightenmentTierSnapshot
    {
        public int Id { get; }
        public int MinTargetEnl { get; }
        public int? MaxTargetEnl { get; }
        public long LumBasePerTarget { get; }
        public int? LumStepAnchor { get; }
        public int? LumStepEvery { get; }
        public decimal? LumStepIncrement { get; }
        public int? ItemWcid { get; }
        public int? ItemCountTargetMinus { get; }
        public string ItemLabel { get; }
        public string QuestStamp { get; }
        public string QuestFailureMessage { get; }

        public EnlightenmentTierSnapshot(
            int id,
            int minTargetEnl,
            int? maxTargetEnl,
            long lumBasePerTarget,
            int? lumStepAnchor,
            int? lumStepEvery,
            decimal? lumStepIncrement,
            int? itemWcid,
            int? itemCountTargetMinus,
            string itemLabel,
            string questStamp,
            string questFailureMessage)
        {
            Id = id;
            MinTargetEnl = minTargetEnl;
            MaxTargetEnl = maxTargetEnl;
            LumBasePerTarget = lumBasePerTarget;
            LumStepAnchor = lumStepAnchor;
            LumStepEvery = lumStepEvery;
            LumStepIncrement = lumStepIncrement;
            ItemWcid = itemWcid;
            ItemCountTargetMinus = itemCountTargetMinus;
            ItemLabel = string.IsNullOrWhiteSpace(itemLabel) ? null : itemLabel.Trim();
            QuestStamp = string.IsNullOrWhiteSpace(questStamp) ? null : questStamp.Trim();
            QuestFailureMessage = string.IsNullOrWhiteSpace(questFailureMessage) ? null : questFailureMessage.Trim();
        }

        public static EnlightenmentTierSnapshot FromDb(ConfigEnlightenmentTier row)
        {
            return new EnlightenmentTierSnapshot(
                row.Id,
                row.MinTargetEnl,
                row.MaxTargetEnl,
                row.LumBasePerTarget,
                row.LumStepAnchor,
                row.LumStepEvery,
                row.LumStepIncrement,
                row.ItemWcid,
                row.ItemCountTargetMinus,
                row.ItemLabel,
                row.QuestStamp,
                row.QuestFailureMessage);
        }

        public long CalculateLuminanceCost(int targetEnlightenment)
        {
            decimal mult = 1.0m;
            if (LumStepAnchor.HasValue && LumStepEvery.HasValue && LumStepIncrement.HasValue)
            {
                var over = targetEnlightenment - LumStepAnchor.Value;
                if (over > 0)
                {
                    var steps = (over - 1) / LumStepEvery.Value;
                    mult = 1.0m + LumStepIncrement.Value * steps;
                }
            }
            return (long)Math.Ceiling(targetEnlightenment * LumBasePerTarget * mult);
        }

        public int GetRequiredItemCount(int targetEnlightenment)
        {
            if (!ItemWcid.HasValue || !ItemCountTargetMinus.HasValue)
                return 0;
            return Math.Max(0, targetEnlightenment - ItemCountTargetMinus.Value);
        }
    }
}
