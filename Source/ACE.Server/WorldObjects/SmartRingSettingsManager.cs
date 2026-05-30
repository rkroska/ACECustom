using System;
using System.Data.Common;
using System.Globalization;
using System.IO;

using ACE.Database;
using ACE.Database.Models.World;

using Microsoft.EntityFrameworkCore;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Persistent, in-memory settings store for global smart ring spell adjustments.
    /// Backed by the world DB table <c>smart_ring_settings</c> (key/value store).
    /// </summary>
    public static class SmartRingSettingsManager
    {
        private static readonly log4net.ILog log =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly object Sync = new();

        public static float Radius { get; private set; } = 5.8f;
        public static float Height { get; private set; } = 7.0f;
        public static float DoubleChance { get; private set; } = 0.0f;
        public static float TripleChance { get; private set; } = 0.0f;

        public static void Initialize()
        {
            lock (Sync)
            {
                try
                {
                    EnsureTableCreated();
                    LoadAll();
                    log.Info("SmartRingSettingsManager: loaded from smart_ring_settings table.");
                }
                catch (Exception ex)
                {
                    log.Error($"SmartRingSettingsManager: failed to initialize — using compiled defaults. {ex.Message}", ex);
                }
            }
        }

        private const string CreateTableSql = @"
CREATE TABLE IF NOT EXISTS `smart_ring_settings` (
  `setting_key`   varchar(50)  NOT NULL,
  `setting_value` varchar(100) NOT NULL,
  `last_modified` datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`setting_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Persistent runtime configuration for global player-casted smart ring spells.';";

        private static void EnsureTableCreated()
        {
            using var ctx = new WorldDbContext();
            ctx.Database.ExecuteSqlRaw(CreateTableSql);
        }

        private static void LoadAll()
        {
            using var ctx = new WorldDbContext();
            using var conn = ctx.Database.GetDbConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT setting_key, setting_value FROM smart_ring_settings";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var key = reader.GetString(0);
                var val = reader.GetString(1);
                ApplyRaw(key, val);
            }
        }

        private static string P(float v) => $"{v.ToString("G", CultureInfo.InvariantCulture)} ({(v * 100.0f).ToString("0.0", CultureInfo.InvariantCulture)}%)";

        public static (bool success, bool found, string message) TrySet(string key, string value)
        {
            lock (Sync)
            {
                switch (key.ToLower())
                {
                    case "radius":
                        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var r))
                            return (false, true, "Invalid float value for radius.");
                        if (r <= 0f)
                            return (false, true, "Radius must be greater than 0.");
                        Radius = r;
                        PersistKey("radius", Radius.ToString(CultureInfo.InvariantCulture));
                        return (true, true, $"radius updated to {Radius.ToString("0.0", CultureInfo.InvariantCulture)}");

                    case "height":
                        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
                            return (false, true, "Invalid float value for height.");
                        if (h <= 0f)
                            return (false, true, "Height must be greater than 0.");
                        Height = h;
                        PersistKey("height", Height.ToString(CultureInfo.InvariantCulture));
                        return (true, true, $"height updated to {Height.ToString("0.0", CultureInfo.InvariantCulture)}");

                    case "double":
                        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                            return (false, true, "Invalid float value for double.");
                        d = Math.Clamp(d, 0f, 1f);
                        if (TripleChance + d > 1.0f)
                            return (false, true, $"double + triple must not exceed 1.0 (triple is currently {P(TripleChance)}).");
                        DoubleChance = d;
                        PersistKey("double", DoubleChance.ToString(CultureInfo.InvariantCulture));
                        return (true, true, $"double updated to {P(DoubleChance)}");

                    case "triple":
                        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var t))
                            return (false, true, "Invalid float value for triple.");
                        t = Math.Clamp(t, 0f, 1f);
                        if (t + DoubleChance > 1.0f)
                            return (false, true, $"triple + double must not exceed 1.0 (double is currently {P(DoubleChance)}).");
                        TripleChance = t;
                        PersistKey("triple", TripleChance.ToString(CultureInfo.InvariantCulture));
                        return (true, true, $"triple updated to {P(TripleChance)}");

                    default:
                        return (false, false, null);
                }
            }
        }

        private static void ApplyRaw(string key, string value)
        {
            switch (key.ToLower())
            {
                case "radius":
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var r))
                        Radius = r;
                    break;
                case "height":
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
                        Height = h;
                    break;
                case "double":
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                        DoubleChance = Math.Clamp(d, 0f, Math.Max(0f, 1f - TripleChance));
                    break;
                case "triple":
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var t))
                        TripleChance = Math.Clamp(t, 0f, Math.Max(0f, 1f - DoubleChance));
                    break;
            }
        }

        private static void PersistKey(string key, string value)
        {
            try
            {
                using var ctx = new WorldDbContext();
                ctx.Database.ExecuteSqlRaw(
                    "INSERT INTO smart_ring_settings (setting_key, setting_value) " +
                    "VALUES ({0}, {1}) " +
                    "ON DUPLICATE KEY UPDATE setting_value = VALUES(setting_value), last_modified = CURRENT_TIMESTAMP",
                    key, value ?? "");
            }
            catch (Exception ex)
            {
                log.Error($"SmartRingSettingsManager: failed to persist {key} — {ex.Message}", ex);
            }
        }

        public static string Dump()
        {
            lock (Sync)
            {
                return "[Smart Ring] Current Settings\n" +
                       $"  • Radius: {Radius.ToString("0.0", CultureInfo.InvariantCulture)}\n" +
                       $"  • Height: {Height.ToString("0.0", CultureInfo.InvariantCulture)}\n" +
                       $"  • Double Proc Chance: {P(DoubleChance)}\n" +
                       $"  • Triple Proc Chance: {P(TripleChance)}\n";
            }
        }

        public static string Help()
        {
            return "[SmartRing] Adjustable Settings\n" +
                   "  • radius float  — adjust player smart ring spell radius in meters\n" +
                   "  • height float  — adjust player smart ring spell height in meters\n" +
                   "  • double float  — adjust player smart ring spell double proc chance (0.0 to 1.0)\n" +
                   "  • triple float  — adjust player smart ring spell triple proc chance (0.0 to 1.0)\n" +
                   "\n[Examples]\n" +
                   "  • /smartring radius 8.5\n" +
                   "  • /smartring double 0.15";
        }
    }
}
