using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using ACE.Database.Models.World;

using Microsoft.EntityFrameworkCore;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Persistent, in-memory settings store for every ability charm.
    /// Backed by the world DB table <c>charm_settings</c> (key/value store).
    /// Values survive server restarts. Updated at runtime by the /charm command.
    /// </summary>
    public static class CharmSettingsManager
    {
        private static readonly log4net.ILog log =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly object Sync = new();

        // ─────────────────────────────────────────────────────────────────────
        //  Public per-charm settings blocks (lazy-initialised on first access)
        // ─────────────────────────────────────────────────────────────────────

        public static ManaBarrierBlock        ManaBarrier        { get; } = new();
        public static ExplosiveArrowBlock     ExplosiveArrow     { get; } = new();
        public static ShrapnelBlock           Shrapnel           { get; } = new();
        public static AgonyBlock              Agony              { get; } = new();
        public static PentaCastBlock          PentaCast          { get; } = new();
        public static PrismaticBlock          Prismatic          { get; } = new();
        public static AutoRebuffBlock         AutoRebuff         { get; } = new();
        public static InfiniteCastingBlock    InfiniteCasting    { get; } = new();
        public static AsheronsFavorBlock      AsheronsFavor      { get; } = new();
        public static ArtisansBlock           Artisans           { get; } = new();
        public static EssenceRefillBlock      EssenceRefill      { get; } = new();
        public static UniversalSummoningBlock UniversalSummoning { get; } = new();

        // ─────────────────────────────────────────────────────────────────────
        //  Startup
        // ─────────────────────────────────────────────────────────────────────

        public static void Initialize()
        {
            lock (Sync)
            {
                try
                {
                    EnsureTableCreated();
                    LoadAll();
                    log.Info("CharmSettingsManager: loaded from charm_settings table.");
                }
                catch (Exception ex)
                {
                    log.Error($"CharmSettingsManager: failed to initialize — using compiled defaults. {ex.Message}", ex);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API used by the /charm command
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns every charm block as a display string.
        /// </summary>
        public static string DumpAll()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Charm Settings ===");
            sb.Append(ManaBarrier.Dump());
            sb.Append(ExplosiveArrow.Dump());
            sb.Append(Shrapnel.Dump());
            sb.Append(Agony.Dump());
            sb.Append(PentaCast.Dump());
            sb.Append(Prismatic.Dump());
            sb.Append(AutoRebuff.Dump());
            sb.Append(InfiniteCasting.Dump());
            sb.Append(AsheronsFavor.Dump());
            sb.Append(Artisans.Dump());
            sb.Append(EssenceRefill.Dump());
            sb.Append(UniversalSummoning.Dump());
            return sb.ToString();
        }

        /// <summary>
        /// Sets a key on a named charm block, persists to DB, returns a human-readable result message.
        /// Returns null if the charm name is unknown.
        /// </summary>
        public static string TrySet(string charmName, string key, string value, out bool found)
        {
            found = true;
            ICharmBlock block = charmName switch
            {
                "manabarrier"        => ManaBarrier,
                "explosivearrow"     => ExplosiveArrow,
                "shrapnel"           => Shrapnel,
                "agony"              => Agony,
                "pentacast"          => PentaCast,
                "prismaticstrike"    => Prismatic,
                "autorebuff"         => AutoRebuff,
                "infinitecasting"    => InfiniteCasting,
                "asheronsfavor"      => AsheronsFavor,
                "artisans"           => Artisans,
                "essencerefill"      => EssenceRefill,
                "universalsummoning" => UniversalSummoning,
                _                    => null
            };

            if (block == null) { found = false; return null; }

            var result = block.TrySet(key, value);
            if (result != null)
                PersistKey(charmName, key, block.GetRaw(key));
            return result;
        }

        /// <summary>
        /// Resets all charms to defaults and persists.
        /// </summary>
        public static void ResetAll()
        {
            ManaBarrier.Reset();        PersistBlock("manabarrier",        ManaBarrier);
            ExplosiveArrow.Reset();     PersistBlock("explosivearrow",     ExplosiveArrow);
            Shrapnel.Reset();           PersistBlock("shrapnel",           Shrapnel);
            Agony.Reset();              PersistBlock("agony",              Agony);
            PentaCast.Reset();          PersistBlock("pentacast",          PentaCast);
            Prismatic.Reset();          PersistBlock("prismaticstrike",    Prismatic);
            AutoRebuff.Reset();         PersistBlock("autorebuff",         AutoRebuff);
            InfiniteCasting.Reset();    PersistBlock("infinitecasting",    InfiniteCasting);
            AsheronsFavor.Reset();      PersistBlock("asheronsfavor",      AsheronsFavor);
            Artisans.Reset();           PersistBlock("artisans",           Artisans);
            EssenceRefill.Reset();      PersistBlock("essencerefill",      EssenceRefill);
            UniversalSummoning.Reset(); PersistBlock("universalsummoning", UniversalSummoning);
        }

        /// <summary>
        /// Resets a single named charm to defaults and persists.
        /// Returns false if the charm name is unknown.
        /// </summary>
        public static bool TryReset(string charmName)
        {
            switch (charmName)
            {
                case "manabarrier":        ManaBarrier.Reset();        PersistBlock("manabarrier",        ManaBarrier);        return true;
                case "explosivearrow":     ExplosiveArrow.Reset();     PersistBlock("explosivearrow",     ExplosiveArrow);     return true;
                case "shrapnel":           Shrapnel.Reset();           PersistBlock("shrapnel",           Shrapnel);           return true;
                case "agony":              Agony.Reset();              PersistBlock("agony",              Agony);              return true;
                case "pentacast":          PentaCast.Reset();          PersistBlock("pentacast",          PentaCast);          return true;
                case "prismaticstrike":    Prismatic.Reset();          PersistBlock("prismaticstrike",    Prismatic);          return true;
                case "autorebuff":         AutoRebuff.Reset();         PersistBlock("autorebuff",         AutoRebuff);         return true;
                case "infinitecasting":    InfiniteCasting.Reset();    PersistBlock("infinitecasting",    InfiniteCasting);    return true;
                case "asheronsfavor":      AsheronsFavor.Reset();      PersistBlock("asheronsfavor",      AsheronsFavor);      return true;
                case "artisans":           Artisans.Reset();           PersistBlock("artisans",           Artisans);           return true;
                case "essencerefill":      EssenceRefill.Reset();      PersistBlock("essencerefill",      EssenceRefill);      return true;
                case "universalsummoning": UniversalSummoning.Reset(); PersistBlock("universalsummoning", UniversalSummoning); return true;
                default: return false;
            }
        }

        public static string DumpCharm(string charmName)
        {
            return charmName switch
            {
                "manabarrier"        => ManaBarrier.Dump(),
                "explosivearrow"     => ExplosiveArrow.Dump(),
                "shrapnel"           => Shrapnel.Dump(),
                "agony"              => Agony.Dump(),
                "pentacast"          => PentaCast.Dump(),
                "prismaticstrike"    => Prismatic.Dump(),
                "autorebuff"         => AutoRebuff.Dump(),
                "infinitecasting"    => InfiniteCasting.Dump(),
                "asheronsfavor"      => AsheronsFavor.Dump(),
                "artisans"           => Artisans.Dump(),
                "essencerefill"      => EssenceRefill.Dump(),
                "universalsummoning" => UniversalSummoning.Dump(),
                _                    => null
            };
        }

        public static string DumpHelp(string charmName)
        {
            return charmName switch
            {
                "manabarrier"        => ManaBarrier.Help(),
                "explosivearrow"     => ExplosiveArrow.Help(),
                "shrapnel"           => Shrapnel.Help(),
                "agony"              => Agony.Help(),
                "pentacast"          => PentaCast.Help(),
                "prismaticstrike"    => Prismatic.Help(),
                "autorebuff"         => AutoRebuff.Help(),
                "infinitecasting"    => InfiniteCasting.Help(),
                "asheronsfavor"      => AsheronsFavor.Help(),
                "artisans"           => Artisans.Help(),
                "essencerefill"      => EssenceRefill.Help(),
                "universalsummoning" => UniversalSummoning.Help(),
                _                    => null
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        //  DB helpers
        // ─────────────────────────────────────────────────────────────────────

        private const string CreateTableSql = @"
CREATE TABLE IF NOT EXISTS `charm_settings` (
  `charm_name`    varchar(50)  NOT NULL,
  `setting_key`   varchar(50)  NOT NULL,
  `setting_value` varchar(100) NOT NULL,
  `last_modified` datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`charm_name`, `setting_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Persistent runtime configuration for ILT ability charms.';";

        private static void EnsureTableCreated()
        {
            using var ctx = new WorldDbContext();
            ctx.Database.ExecuteSqlRaw(CreateTableSql);
        }

        private static void LoadAll()
        {
            using var ctx = new WorldDbContext();
            var rows = ctx.CharmSettings.ToListAsync().GetAwaiter().GetResult();
            foreach (var row in rows)
            {
                var block = GetBlock(row.CharmName);
                block?.ApplyRaw(row.SettingKey, row.SettingValue);
            }
        }

        private static void PersistKey(string charmName, string key, string value)
        {
            try
            {
                using var ctx = new WorldDbContext();
                ctx.Database.ExecuteSqlRaw(
                    "INSERT INTO charm_settings (charm_name, setting_key, setting_value) " +
                    "VALUES ({0}, {1}, {2}) " +
                    "ON DUPLICATE KEY UPDATE setting_value = VALUES(setting_value), last_modified = CURRENT_TIMESTAMP",
                    charmName, key, value ?? "");
            }
            catch (Exception ex)
            {
                log.Error($"CharmSettingsManager: failed to persist {charmName}.{key} — {ex.Message}", ex);
            }
        }

        private static void PersistBlock(string charmName, ICharmBlock block)
        {
            foreach (var (key, val) in block.GetAllRaw())
                PersistKey(charmName, key, val);
        }

        private static ICharmBlock GetBlock(string charmName) => charmName switch
        {
            "manabarrier"        => ManaBarrier,
            "explosivearrow"     => ExplosiveArrow,
            "shrapnel"           => Shrapnel,
            "agony"              => Agony,
            "pentacast"          => PentaCast,
            "prismaticstrike"    => Prismatic,
            "autorebuff"         => AutoRebuff,
            "infinitecasting"    => InfiniteCasting,
            "asheronsfavor"      => AsheronsFavor,
            "artisans"           => Artisans,
            "essencerefill"      => EssenceRefill,
            "universalsummoning" => UniversalSummoning,
            _                    => null
        };

        // ─────────────────────────────────────────────────────────────────────
        //  Block interface
        // ─────────────────────────────────────────────────────────────────────

        public interface ICharmBlock
        {
            /// <summary>Apply a raw string value from DB or command. Returns error message or null on success.</summary>
            string TrySet(string key, string value);
            void   ApplyRaw(string key, string value);
            string GetRaw(string key);
            IEnumerable<(string key, string value)> GetAllRaw();
            void   Reset();
            string Dump();
            string Help();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static bool ParseBool(string v, out bool result)
        {
            v = v.Trim().ToLower();
            if (v is "true" or "on" or "1")  { result = true;  return true; }
            if (v is "false" or "off" or "0") { result = false; return true; }
            result = false; return false;
        }

        private static bool ParseFloat(string v, out float result) =>
            float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

        private static bool ParseInt(string v, out int result) =>
            int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

        private static string F(float v) => v.ToString("G", CultureInfo.InvariantCulture);
        private static string B(bool  v) => v ? "true" : "false";
        private static string P(float v) => $"{F(v)} ({(v * 100.0f).ToString("0.0", CultureInfo.InvariantCulture)}%)";

        // ─────────────────────────────────────────────────────────────────────
        //  Mana Barrier
        // ─────────────────────────────────────────────────────────────────────

        public sealed class ManaBarrierBlock : ICharmBlock
        {
            public bool  Enabled { get; private set; } = true;
            // Single global ratio: mana cost per 1 damage absorbed (all damage types)
            // 1.0 = 1 mana per 1 damage, 0.5 = 1 mana per 2 damage, 2.0 = 2 mana per 1 damage
            public float Ratio   { get; private set; } = 1.0f;
            // Per-tier values: how much damage is absorbed per 1 mana spent
            // Higher tier = absorbs more damage per mana = better
            // T1: 1.0  = 1 damage per mana (1:1)
            // T2: 1.5  = 1.5 damage per mana
            // T3: 2.0  = 2 damage per mana
            public float T1      { get; private set; } = 1.0f;
            public float T2      { get; private set; } = 1.5f;
            public float T3      { get; private set; } = 2.0f;

            public void Reset()
            {
                Enabled = true;
                Ratio = 1.0f;
                T1 = 1.0f; T2 = 1.5f; T3 = 2.0f;
            }

            public string TrySet(string key, string value)
            {
                switch (key)
                {
                    case "enabled": case "on": case "off": case "true": case "false":
                        var valueToParse = key == "enabled" ? value : key;
                        if (!ParseBool(valueToParse, out var bv)) return $"Invalid bool: '{value}'. Use true/on or false/off.";
                        Enabled = bv; return $"manabarrier.enabled = {B(Enabled)}";

                    case "ratio":  if (!ParseFloat(value, out var rv))  return $"Invalid float: '{value}'."; Ratio = rv;  return $"manabarrier.ratio = {F(Ratio)}";
                    case "t1":     if (!ParseFloat(value, out var t1v)) return $"Invalid float: '{value}'."; T1 = t1v; return $"manabarrier.t1 = {F(T1)}";
                    case "t2":     if (!ParseFloat(value, out var t2v)) return $"Invalid float: '{value}'."; T2 = t2v; return $"manabarrier.t2 = {F(T2)}";
                    case "t3":     if (!ParseFloat(value, out var t3v)) return $"Invalid float: '{value}'."; T3 = t3v; return $"manabarrier.t3 = {F(T3)}";
                    default: return null;
                }
            }

            public void ApplyRaw(string key, string value)
            {
                switch (key)
                {
                    case "enabled": if (ParseBool(value, out var bv))  Enabled = bv;  break;
                    case "ratio":   if (ParseFloat(value, out var rv))  Ratio = rv;  break;
                    case "t1":      if (ParseFloat(value, out var t1v)) T1    = t1v; break;
                    case "t2":      if (ParseFloat(value, out var t2v)) T2    = t2v; break;
                    case "t3":      if (ParseFloat(value, out var t3v)) T3    = t3v; break;
                }
            }

            public string GetRaw(string key) => key switch
            {
                "enabled" => B(Enabled),
                "ratio"   => F(Ratio),
                "t1"      => F(T1),
                "t2"      => F(T2),
                "t3"      => F(T3),
                _         => null
            };

            public IEnumerable<(string, string)> GetAllRaw() => new[]
            {
                ("enabled", B(Enabled)),
                ("ratio",   F(Ratio)),
                ("t1",      F(T1)),
                ("t2",      F(T2)),
                ("t3",      F(T3)),
            };

            public string Help() =>
                "[manabarrier] adjustable settings:\n" +
                "  enabled  true/false/on/off   — global kill-switch (false = full dmg passes through, silent to player)\n" +
                "  ratio    float               — mana cost per 1 damage, all elements/physical (default: 1.0)\n" +
                "  t1       float               — Mana Barrier: damage absorbed per mana (default: 1.0)\n" +
                "  t2       float               — Greater Mana Barrier: damage absorbed per mana (default: 1.5)\n" +
                "  t3       float               — Master Mana Barrier: damage absorbed per mana (default: 2.0)\n" +
                "  Math: mana_spent = damage x ratio / tier\n" +
                "  Examples: /charm manabarrier ratio 0.5  |  /charm manabarrier t3 3.0  |  /charm reset manabarrier";

            public string Dump() =>
                "[manabarrier] settings:\n" +
                $"  enabled: {B(Enabled)}\n" +
                $"  ratio: {F(Ratio)}\n" +
                $"  t1: {F(T1)}\n" +
                $"  t2: {F(T2)}\n" +
                $"  t3: {F(T3)}\n";
        }


        // ─────────────────────────────────────────────────────────────────────
        //  Explosive Arrow
        // ─────────────────────────────────────────────────────────────────────

        public sealed class ExplosiveArrowBlock : ICharmBlock
        {
            public bool  Enabled   { get; private set; } = true;
            public float T1Min     { get; private set; } = 0.10f;
            public float T1Max     { get; private set; } = 0.15f;
            public float T2Min     { get; private set; } = 0.15f;
            public float T2Max     { get; private set; } = 0.25f;
            public float T3Min     { get; private set; } = 0.25f;
            public float T3Max     { get; private set; } = 0.33f;
            public float Radius    { get; private set; } = 15.0f;
            public float Height    { get; private set; } = 10.0f;
            public float Delay     { get; private set; } = 1.0f;
            public int   MaxArrows { get; private set; } = 5;

            public void Reset()
            {
                Enabled = true;
                T1Min = 0.10f; T1Max = 0.15f;
                T2Min = 0.15f; T2Max = 0.25f;
                T3Min = 0.25f; T3Max = 0.33f;
                Radius = 15.0f; Height = 10.0f; Delay = 1.0f;
                MaxArrows = 5;
            }

            public string TrySet(string key, string value)
            {
                switch (key)
                {
                    case "enabled": case "on": case "off": case "true": case "false":
                        var valueToParse = key == "enabled" ? value : key;
                        if (!ParseBool(valueToParse, out var bv)) return $"Invalid bool: '{value}'";
                        Enabled = bv; return $"explosivearrow.enabled = {B(Enabled)}";

                    case "t1min": if (!ParseFloat(value, out var v)) return $"Invalid float."; T1Min = v; return $"explosivearrow.t1min = {F(T1Min)}";
                    case "t1max": if (!ParseFloat(value, out var v2)) return $"Invalid float."; T1Max = v2; return $"explosivearrow.t1max = {F(T1Max)}";
                    case "t2min": if (!ParseFloat(value, out var v3)) return $"Invalid float."; T2Min = v3; return $"explosivearrow.t2min = {F(T2Min)}";
                    case "t2max": if (!ParseFloat(value, out var v4)) return $"Invalid float."; T2Max = v4; return $"explosivearrow.t2max = {F(T2Max)}";
                    case "t3min": if (!ParseFloat(value, out var v5)) return $"Invalid float."; T3Min = v5; return $"explosivearrow.t3min = {F(T3Min)}";
                    case "t3max": if (!ParseFloat(value, out var v6)) return $"Invalid float."; T3Max = v6; return $"explosivearrow.t3max = {F(T3Max)}";
                    case "radius": if (!ParseFloat(value, out var v7)) return $"Invalid float."; Radius = v7; return $"explosivearrow.radius = {F(Radius)}";
                    case "height": if (!ParseFloat(value, out var v8)) return $"Invalid float."; Height = v8; return $"explosivearrow.height = {F(Height)}";
                    case "delay":  if (!ParseFloat(value, out var v9)) return $"Invalid float."; Delay  = v9; return $"explosivearrow.delay = {F(Delay)}";
                    case "maxarrows":
                        if (!ParseInt(value, out var v10)) return $"Invalid int.";
                        if (v10 < 1 || v10 > 10) return $"Value must be between 1 and 10.";
                        MaxArrows = v10; return $"explosivearrow.maxarrows = {MaxArrows}";
                    default: return null;
                }
            }

            public void ApplyRaw(string key, string value)
            {
                switch (key)
                {
                    case "enabled":   if (ParseBool(value, out var bv))   Enabled = bv; break;
                    case "t1min":     if (ParseFloat(value, out var v))   T1Min   = v;  break;
                    case "t1max":     if (ParseFloat(value, out var v2))  T1Max   = v2; break;
                    case "t2min":     if (ParseFloat(value, out var v3))  T2Min   = v3; break;
                    case "t2max":     if (ParseFloat(value, out var v4))  T2Max   = v4; break;
                    case "t3min":     if (ParseFloat(value, out var v5))  T3Min   = v5; break;
                    case "t3max":     if (ParseFloat(value, out var v6))  T3Max   = v6; break;
                    case "radius":    if (ParseFloat(value, out var v7))  Radius  = v7; break;
                    case "height":    if (ParseFloat(value, out var v8))  Height  = v8; break;
                    case "delay":     if (ParseFloat(value, out var v9))  Delay   = v9; break;
                    case "maxarrows": if (ParseInt(value, out var v10)) MaxArrows = Math.Clamp(v10, 1, 10); break;
                }
            }

            public string GetRaw(string key) => key switch
            {
                "enabled"   => B(Enabled),
                "t1min"     => F(T1Min),
                "t1max"     => F(T1Max),
                "t2min"     => F(T2Min),
                "t2max"     => F(T2Max),
                "t3min"     => F(T3Min),
                "t3max"     => F(T3Max),
                "radius"    => F(Radius),
                "height"    => F(Height),
                "delay"     => F(Delay),
                "maxarrows" => MaxArrows.ToString(),
                _           => null
            };

            public IEnumerable<(string, string)> GetAllRaw() => new[]
            {
                ("enabled", B(Enabled)),
                ("t1min", F(T1Min)), ("t1max", F(T1Max)),
                ("t2min", F(T2Min)), ("t2max", F(T2Max)),
                ("t3min", F(T3Min)), ("t3max", F(T3Max)),
                ("radius", F(Radius)), ("height", F(Height)), ("delay", F(Delay)),
                ("maxarrows", MaxArrows.ToString()),
            };

            public string Help() =>
                "[explosivearrow] adjustable settings:\n" +
                "  enabled  true/false/on/off   — global kill-switch\n" +
                "  t1min/t1max  float           — Explosive Arrow: blast damage range as % of arrow dmg (default: 0.10-0.15)\n" +
                "  t2min/t2max  float           — Greater Explosive Arrow: blast damage range (default: 0.15-0.25)\n" +
                "  t3min/t3max  float           — Master Explosive Arrow: blast damage range (default: 0.25-0.33)\n" +
                "  radius  float               — AOE blast radius in meters (default: 15.0)\n" +
                "  height  float               — AOE blast cylinder height (default: 10.0)\n" +
                "  delay   float               — seconds between arrow hit and detonation (default: 1.0)\n" +
                "  maxarrows int               — max arrows allowed to detonate per shot [1-10] (default: 5)";

            public string Dump() =>
                "[explosivearrow] settings:\n" +
                $"  enabled: {B(Enabled)}\n" +
                $"  t1min: {F(T1Min)}\n" +
                $"  t1max: {F(T1Max)}\n" +
                $"  t2min: {F(T2Min)}\n" +
                $"  t2max: {F(T2Max)}\n" +
                $"  t3min: {F(T3Min)}\n" +
                $"  t3max: {F(T3Max)}\n" +
                $"  radius: {F(Radius)}\n" +
                $"  height: {F(Height)}\n" +
                $"  delay: {F(Delay)}\n" +
                $"  maxarrows: {MaxArrows}\n";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Simple on/off blocks (stubs ready for future params)
        // ─────────────────────────────────────────────────────────────────────

        public sealed class ShrapnelBlock : ICharmBlock
        {
            public bool  Enabled      { get; private set; } = true;
            public float Radius       { get; private set; } = 5.8f;
            public float Height       { get; private set; } = 7.0f;
            public float DoubleChance { get; private set; } = 0.0f;
            public float TripleChance { get; private set; } = 0.0f;

            public void Reset()
            {
                Enabled = true;
                Radius = 5.8f;
                Height = 7.0f;
                DoubleChance = 0.0f;
                TripleChance = 0.0f;
            }

            public string TrySet(string key, string value)
            {
                switch (key)
                {
                    case "enabled": case "on": case "off": case "true": case "false":
                        var valueToParse = key == "enabled" ? value : key;
                        if (!ParseBool(valueToParse, out var bv)) return $"Invalid bool: '{value}'";
                        Enabled = bv; return $"shrapnel.enabled = {B(Enabled)}";

                    case "radius": if (!ParseFloat(value, out var v)) return $"Invalid float."; Radius = v; return $"shrapnel.radius = {F(Radius)}";
                    case "height": if (!ParseFloat(value, out var v2)) return $"Invalid float."; Height = v2; return $"shrapnel.height = {F(Height)}";
                    case "double":
                        if (!ParseFloat(value, out var v3)) return $"Invalid float.";
                        v3 = Math.Clamp(v3, 0f, 1f);
                        if (TripleChance + v3 > 1.0f) return $"double + triple must not exceed 1.0 (triple is currently {P(TripleChance)}).";
                        DoubleChance = v3; return $"shrapnel.double = {P(DoubleChance)}";
                    case "triple":
                        if (!ParseFloat(value, out var v4)) return $"Invalid float.";
                        v4 = Math.Clamp(v4, 0f, 1f);
                        if (v4 + DoubleChance > 1.0f) return $"triple + double must not exceed 1.0 (double is currently {P(DoubleChance)}).";
                        TripleChance = v4; return $"shrapnel.triple = {P(TripleChance)}";
                    default: return null;
                }
            }

            public void ApplyRaw(string key, string value)
            {
                switch (key)
                {
                    case "enabled": if (ParseBool(value, out var bv))  Enabled = bv; break;
                    case "radius":  if (ParseFloat(value, out var v))  Radius  = v;  break;
                    case "height":  if (ParseFloat(value, out var v2)) Height  = v2; break;
                    case "double":  if (ParseFloat(value, out var v3)) DoubleChance = Math.Clamp(v3, 0f, Math.Max(0f, 1f - TripleChance)); break;
                    case "triple":  if (ParseFloat(value, out var v4)) TripleChance = Math.Clamp(v4, 0f, Math.Max(0f, 1f - DoubleChance)); break;
                }
            }

            public string GetRaw(string key) => key switch
            {
                "enabled" => B(Enabled),
                "radius"  => F(Radius),
                "height"  => F(Height),
                "double"  => F(DoubleChance),
                "triple"  => F(TripleChance),
                _         => null
            };

            public IEnumerable<(string, string)> GetAllRaw() => new[]
            {
                ("enabled", B(Enabled)),
                ("radius",  F(Radius)),
                ("height",  F(Height)),
                ("double",  F(DoubleChance)),
                ("triple",  F(TripleChance)),
            };

            public string Help() =>
                "[shrapnel] adjustable settings:\n" +
                "  enabled  true/false/on/off   — global kill-switch\n" +
                "  radius  float                — Rocky Shrapnel AOE physical blast radius in meters (default: 5.8)\n" +
                "  height  float                — Rocky Shrapnel AOE physical blast height in meters (default: 7.0)\n" +
                "  double  float                — double proc chance (default: 0.0; triple + double ≤ 1.0)\n" +
                "  triple  float                — triple proc chance (default: 0.0; triple + double ≤ 1.0)";

            public string Dump() =>
                "[shrapnel] settings:\n" +
                $"  enabled: {B(Enabled)}\n" +
                $"  radius: {F(Radius)}\n" +
                $"  height: {F(Height)}\n" +
                $"  double: {P(DoubleChance)}\n" +
                $"  triple: {P(TripleChance)}\n";
        }

        public sealed class AgonyBlock : ICharmBlock
        {
            public bool  Enabled      { get; private set; } = true;
            public float Radius       { get; private set; } = 5.8f;
            public float Height       { get; private set; } = 7.0f;
            public float DoubleChance { get; private set; } = 0.0f;
            public float TripleChance { get; private set; } = 0.0f;

            public void Reset()
            {
                Enabled = true;
                Radius = 5.8f;
                Height = 7.0f;
                DoubleChance = 0.0f;
                TripleChance = 0.0f;
            }

            public string TrySet(string key, string value)
            {
                switch (key)
                {
                    case "enabled": case "on": case "off": case "true": case "false":
                        var valueToParse = key == "enabled" ? value : key;
                        if (!ParseBool(valueToParse, out var bv)) return $"Invalid bool: '{value}'";
                        Enabled = bv; return $"agony.enabled = {B(Enabled)}";

                    case "radius": if (!ParseFloat(value, out var v)) return $"Invalid float."; Radius = v; return $"agony.radius = {F(Radius)}";
                    case "height": if (!ParseFloat(value, out var v2)) return $"Invalid float."; Height = v2; return $"agony.height = {F(Height)}";
                    case "double":
                        if (!ParseFloat(value, out var v3)) return $"Invalid float.";
                        v3 = Math.Clamp(v3, 0f, 1f);
                        if (TripleChance + v3 > 1.0f) return $"double + triple must not exceed 1.0 (triple is currently {P(TripleChance)}).";
                        DoubleChance = v3; return $"agony.double = {P(DoubleChance)}";
                    case "triple":
                        if (!ParseFloat(value, out var v4)) return $"Invalid float.";
                        v4 = Math.Clamp(v4, 0f, 1f);
                        if (v4 + DoubleChance > 1.0f) return $"triple + double must not exceed 1.0 (double is currently {P(DoubleChance)}).";
                        TripleChance = v4; return $"agony.triple = {P(TripleChance)}";
                    default: return null;
                }
            }

            public void ApplyRaw(string key, string value)
            {
                switch (key)
                {
                    case "enabled": if (ParseBool(value, out var bv))  Enabled = bv; break;
                    case "radius":  if (ParseFloat(value, out var v))  Radius  = v;  break;
                    case "height":  if (ParseFloat(value, out var v2)) Height  = v2; break;
                    case "double":  if (ParseFloat(value, out var v3)) DoubleChance = Math.Clamp(v3, 0f, Math.Max(0f, 1f - TripleChance)); break;
                    case "triple":  if (ParseFloat(value, out var v4)) TripleChance = Math.Clamp(v4, 0f, Math.Max(0f, 1f - DoubleChance)); break;
                }
            }

            public string GetRaw(string key) => key switch
            {
                "enabled" => B(Enabled),
                "radius"  => F(Radius),
                "height"  => F(Height),
                "double"  => F(DoubleChance),
                "triple"  => F(TripleChance),
                _         => null
            };

            public IEnumerable<(string, string)> GetAllRaw() => new[]
            {
                ("enabled", B(Enabled)),
                ("radius",  F(Radius)),
                ("height",  F(Height)),
                ("double",  F(DoubleChance)),
                ("triple",  F(TripleChance)),
            };

            public string Help() =>
                "[agony] adjustable settings:\n" +
                "  enabled  true/false/on/off   — global kill-switch\n" +
                "  radius  float                — Ring of Agony AOE blast radius in meters (default: 5.8)\n" +
                "  height  float                — Ring of Agony AOE blast height in meters (default: 7.0)\n" +
                "  double  float                — double proc chance (default: 0.0; triple + double ≤ 1.0)\n" +
                "  triple  float                — triple proc chance (default: 0.0; triple + double ≤ 1.0)";

            public string Dump() =>
                "[agony] settings:\n" +
                $"  enabled: {B(Enabled)}\n" +
                $"  radius: {F(Radius)}\n" +
                $"  height: {F(Height)}\n" +
                $"  double: {P(DoubleChance)}\n" +
                $"  triple: {P(TripleChance)}\n";
        }

        public sealed class PrismaticBlock : ICharmBlock
        {
            public bool Enabled { get; private set; } = true;
            public void Reset() { Enabled = true; }

            public string TrySet(string key, string value)
            {
                if (key is "enabled" or "on" or "off" or "true" or "false")
                {
                    var valueToParse = key == "enabled" ? value : key;
                    if (!ParseBool(valueToParse, out var bv)) return $"Invalid bool: '{value}'";
                    Enabled = bv; return $"prismaticstrike.enabled = {B(Enabled)}";
                }
                return null;
            }

            public void ApplyRaw(string key, string value) { if (key == "enabled" && ParseBool(value, out var bv)) Enabled = bv; }
            public string GetRaw(string key) => key == "enabled" ? B(Enabled) : null;
            public IEnumerable<(string, string)> GetAllRaw() => new[] { ("enabled", B(Enabled)) };
            public string Help() => "[prismaticstrike] adjustable settings:\n  enabled  true/false/on/off   — global kill-switch (no other tunables yet)";
            public string Dump() =>
                "[prismaticstrike] settings:\n" +
                $"  enabled: {B(Enabled)}\n" +
                "  (no other tunables yet)\n";
        }

        public sealed class AutoRebuffBlock : ICharmBlock
        {
            public bool Enabled { get; private set; } = true;
            public void Reset() { Enabled = true; }

            public string TrySet(string key, string value)
            {
                if (key is "enabled" or "on" or "off" or "true" or "false")
                {
                    var valueToParse = key == "enabled" ? value : key;
                    if (!ParseBool(valueToParse, out var bv)) return $"Invalid bool: '{value}'";
                    Enabled = bv; return $"autorebuff.enabled = {B(Enabled)}";
                }
                return null;
            }

            public void ApplyRaw(string key, string value) { if (key == "enabled" && ParseBool(value, out var bv)) Enabled = bv; }
            public string GetRaw(string key) => key == "enabled" ? B(Enabled) : null;
            public IEnumerable<(string, string)> GetAllRaw() => new[] { ("enabled", B(Enabled)) };
            public string Help() => "[autorebuff] adjustable settings:\n  enabled  true/false/on/off   — global kill-switch (no other tunables yet)";
            public string Dump() =>
                "[autorebuff] settings:\n" +
                $"  enabled: {B(Enabled)}\n" +
                "  (no other tunables yet)\n";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Penta Cast
        // ─────────────────────────────────────────────────────────────────────

        public sealed class PentaCastBlock : ICharmBlock
        {
            public bool  Enabled { get; private set; } = true;
            public int   Targets { get; private set; } = 4;
            public float Range   { get; private set; } = 10.0f;

            public void Reset() { Enabled = true; Targets = 4; Range = 10.0f; }

            public string TrySet(string key, string value)
            {
                switch (key)
                {
                    case "enabled": case "on": case "off": case "true": case "false":
                        var valueToParse = key == "enabled" ? value : key;
                        if (!ParseBool(valueToParse, out var bv)) return $"Invalid bool: '{value}'";
                        Enabled = bv; return $"pentacast.enabled = {B(Enabled)}";

                    case "targets":
                        if (!ParseInt(value, out var iv)) return $"Invalid int.";
                        if (iv < 1 || iv > 20) return $"Value must be between 1 and 20.";
                        Targets = iv; return $"pentacast.targets = {Targets}";
                    case "range":   if (!ParseFloat(value, out var fv)) return $"Invalid float."; Range = fv; return $"pentacast.range = {F(Range)}";
                    default: return null;
                }
            }

            public void ApplyRaw(string key, string value)
            {
                switch (key)
                {
                    case "enabled": if (ParseBool(value, out var bv))  Enabled = bv; break;
                    case "targets": if (ParseInt(value, out var iv)) Targets = Math.Clamp(iv, 1, 20); break;
                    case "range":   if (ParseFloat(value, out var fv)) Range   = fv; break;
                }
            }

            public string GetRaw(string key) => key switch
            {
                "enabled" => B(Enabled),
                "targets" => Targets.ToString(),
                "range"   => F(Range),
                _         => null
            };

            public IEnumerable<(string, string)> GetAllRaw() => new[]
            {
                ("enabled", B(Enabled)),
                ("targets", Targets.ToString()),
                ("range",   F(Range)),
            };

            public string Help() =>
                "[pentacast] adjustable settings:\n" +
                "  enabled  true/false/on/off   — global kill-switch\n" +
                "  targets  int                 — number of additional targets to bounce spell to [1-20] (default: 4)\n" +
                "  range    float               — radius in meters to find bounce targets (default: 10.0)";

            public string Dump() =>
                "[pentacast] settings:\n" +
                $"  enabled: {B(Enabled)}\n" +
                $"  targets: {Targets}\n" +
                $"  range: {F(Range)}\n";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Infinite Casting
        // ─────────────────────────────────────────────────────────────────────

        public sealed class InfiniteCastingBlock : ICharmBlock
        {
            public bool Enabled { get; private set; } = true;
            public void Reset() { Enabled = true; }

            public string TrySet(string key, string value)
            {
                if (key is "enabled" or "on" or "off" or "true" or "false")
                {
                    var valueToParse = key == "enabled" ? value : key;
                    if (!ParseBool(valueToParse, out var bv)) return $"Invalid bool: '{value}'";
                    Enabled = bv; return $"infinitecasting.enabled = {B(Enabled)}";
                }
                return null;
            }

            public void ApplyRaw(string key, string value) { if (key == "enabled" && ParseBool(value, out var bv)) Enabled = bv; }
            public string GetRaw(string key) => key == "enabled" ? B(Enabled) : null;
            public IEnumerable<(string, string)> GetAllRaw() => new[] { ("enabled", B(Enabled)) };
            public string Help() => "[infinitecasting] adjustable settings:\n  enabled  true/false/on/off   — global kill-switch (no other tunables yet)";
            public string Dump() =>
                "[infinitecasting] settings:\n" +
                $"  enabled: {B(Enabled)}\n" +
                "  (no other tunables yet)\n";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Asheron's Favor
        // ─────────────────────────────────────────────────────────────────────

        public sealed class AsheronsFavorBlock : ICharmBlock
        {
            public bool  Enabled  { get; private set; } = true;
            public float T1Health { get; private set; } = 1.10f;
            public float T1Armor  { get; private set; } = 50.0f;
            public float T2Health { get; private set; } = 1.15f;
            public float T2Armor  { get; private set; } = 100.0f;
            public float T3Health { get; private set; } = 1.20f;
            public float T3Armor  { get; private set; } = 250.0f;

            public void Reset()
            {
                Enabled = true;
                T1Health = 1.10f; T1Armor = 50.0f;
                T2Health = 1.15f; T2Armor = 100.0f;
                T3Health = 1.20f; T3Armor = 250.0f;
            }

            public string TrySet(string key, string value)
            {
                switch (key)
                {
                    case "enabled": case "on": case "off": case "true": case "false":
                        var valueToParse = key == "enabled" ? value : key;
                        if (!ParseBool(valueToParse, out var bv)) return $"Invalid bool: '{value}'";
                        Enabled = bv; return $"asheronsfavor.enabled = {B(Enabled)}";

                    case "t1health": if (!ParseFloat(value, out var v1)) return "Invalid float."; T1Health = v1; return $"asheronsfavor.t1health = {F(T1Health)}";
                    case "t1armor":  if (!ParseFloat(value, out var v2)) return "Invalid float."; T1Armor = v2; return $"asheronsfavor.t1armor = {F(T1Armor)}";
                    case "t2health": if (!ParseFloat(value, out var v3)) return "Invalid float."; T2Health = v3; return $"asheronsfavor.t2health = {F(T2Health)}";
                    case "t2armor":  if (!ParseFloat(value, out var v4)) return "Invalid float."; T2Armor = v4; return $"asheronsfavor.t2armor = {F(T2Armor)}";
                    case "t3health": if (!ParseFloat(value, out var v5)) return "Invalid float."; T3Health = v5; return $"asheronsfavor.t3health = {F(T3Health)}";
                    case "t3armor":  if (!ParseFloat(value, out var v6)) return "Invalid float."; T3Armor = v6; return $"asheronsfavor.t3armor = {F(T3Armor)}";
                    default: return null;
                }
            }

            public void ApplyRaw(string key, string value)
            {
                switch (key)
                {
                    case "enabled":  if (ParseBool(value, out var bv))  Enabled  = bv; break;
                    case "t1health": if (ParseFloat(value, out var v1)) T1Health = v1; break;
                    case "t1armor":  if (ParseFloat(value, out var v2)) T1Armor  = v2; break;
                    case "t2health": if (ParseFloat(value, out var v3)) T2Health = v3; break;
                    case "t2armor":  if (ParseFloat(value, out var v4)) T2Armor  = v4; break;
                    case "t3health": if (ParseFloat(value, out var v5)) T3Health = v5; break;
                    case "t3armor":  if (ParseFloat(value, out var v6)) T3Armor  = v6; break;
                }
            }

            public string GetRaw(string key) => key switch
            {
                "enabled"  => B(Enabled),
                "t1health" => F(T1Health),
                "t1armor"  => F(T1Armor),
                "t2health" => F(T2Health),
                "t2armor"  => F(T2Armor),
                "t3health" => F(T3Health),
                "t3armor"  => F(T3Armor),
                _          => null
            };

            public IEnumerable<(string, string)> GetAllRaw() => new[]
            {
                ("enabled",  B(Enabled)),
                ("t1health", F(T1Health)),
                ("t1armor",  F(T1Armor)),
                ("t2health", F(T2Health)),
                ("t2armor",  F(T2Armor)),
                ("t3health", F(T3Health)),
                ("t3armor",  F(T3Armor)),
            };

            public string Help() =>
                "[asheronsfavor] adjustable settings:\n" +
                "  enabled   true/false/on/off — global kill-switch\n" +
                "  t1health  float             — Tier 1 Health% multiplier (default: 1.10 = +10%)\n" +
                "  t1armor   float             — Tier 1 Natural Armor bonus (default: 50.0)\n" +
                "  t2health  float             — Tier 2 Health% multiplier (default: 1.15 = +15%)\n" +
                "  t2armor   float             — Tier 2 Natural Armor bonus (default: 100.0)\n" +
                "  t3health  float             — Tier 3 Health% multiplier (default: 1.20 = +20%)\n" +
                "  t3armor   float             — Tier 3 Natural Armor bonus (default: 250.0)";

            public string Dump() =>
                "[asheronsfavor] settings:\n" +
                $"  enabled: {B(Enabled)}\n" +
                $"  t1health: {F(T1Health)}\n" +
                $"  t1armor: {F(T1Armor)}\n" +
                $"  t2health: {F(T2Health)}\n" +
                $"  t2armor: {F(T2Armor)}\n" +
                $"  t3health: {F(T3Health)}\n" +
                $"  t3armor: {F(T3Armor)}\n";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Artisan's Charm
        // ─────────────────────────────────────────────────────────────────────

        public sealed class ArtisansBlock : ICharmBlock
        {
            public bool  Enabled { get; private set; } = true;
            public float T1      { get; private set; } = 0.04f;
            public float T2      { get; private set; } = 0.08f;
            public float T3      { get; private set; } = 0.12f;

            public void Reset()
            {
                Enabled = true;
                T1 = 0.04f;
                T2 = 0.08f;
                T3 = 0.12f;
            }

            public string TrySet(string key, string value)
            {
                switch (key)
                {
                    case "enabled": case "on": case "off": case "true": case "false":
                        var valueToParse = key == "enabled" ? value : key;
                        if (!ParseBool(valueToParse, out var bv)) return $"Invalid bool: '{value}'";
                        Enabled = bv; return $"artisans.enabled = {B(Enabled)}";

                    case "t1":
                        if (!ParseFloat(value, out var f1)) return "Invalid float.";
                        T1 = f1; return $"artisans.t1 = {P(T1)}";
                    case "t2":
                        if (!ParseFloat(value, out var f2)) return "Invalid float.";
                        T2 = f2; return $"artisans.t2 = {P(T2)}";
                    case "t3":
                        if (!ParseFloat(value, out var f3)) return "Invalid float.";
                        T3 = f3; return $"artisans.t3 = {P(T3)}";
                    default: return null;
                }
            }

            public void ApplyRaw(string key, string value)
            {
                switch (key)
                {
                    case "enabled": if (ParseBool(value, out var bv))  Enabled = bv; break;
                    case "t1":      if (ParseFloat(value, out var f1)) T1 = f1; break;
                    case "t2":      if (ParseFloat(value, out var f2)) T2 = f2; break;
                    case "t3":      if (ParseFloat(value, out var f3)) T3 = f3; break;
                }
            }

            public string GetRaw(string key) => key switch
            {
                "enabled" => B(Enabled),
                "t1"      => F(T1),
                "t2"      => F(T2),
                "t3"      => F(T3),
                _         => null
            };

            public IEnumerable<(string, string)> GetAllRaw() => new[]
            {
                ("enabled", B(Enabled)),
                ("t1",      F(T1)),
                ("t2",      F(T2)),
                ("t3",      F(T3)),
            };

            public string Help() =>
                "[artisans] adjustable settings:\n" +
                "  enabled  true/false/on/off — global kill-switch\n" +
                "  t1       float             — Tier 1 success chance bonus (default: 0.04 = +4%)\n" +
                "  t2       float             — Tier 2 success chance bonus (default: 0.08 = +8%)\n" +
                "  t3       float             — Tier 3 success chance bonus (default: 0.12 = +12%)";

            public string Dump() =>
                "[artisans] settings:\n" +
                $"  enabled: {B(Enabled)}\n" +
                $"  t1: {P(T1)}\n" +
                $"  t2: {P(T2)}\n" +
                $"  t3: {P(T3)}\n";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Summon Essence Refill (Pet Device Pyreal Auto-Refill)
        // ─────────────────────────────────────────────────────────────────────

        public sealed class EssenceRefillBlock : ICharmBlock
        {
            public bool  Enabled { get; private set; } = true;
            public float T1      { get; private set; } = 0.00f; // 0% discount by default
            public float T2      { get; private set; } = 0.25f; // 25% discount by default
            public float T3      { get; private set; } = 0.50f; // 50% discount by default

            public void Reset()
            {
                Enabled = true;
                T1 = 0.00f;
                T2 = 0.25f;
                T3 = 0.50f;
            }

            public string TrySet(string key, string value)
            {
                switch (key)
                {
                    case "enabled": case "on": case "off": case "true": case "false":
                        var valueToParse = key == "enabled" ? value : key;
                        if (!ParseBool(valueToParse, out var bv)) return $"Invalid bool: '{value}'";
                        Enabled = bv; return $"essencerefill.enabled = {B(Enabled)}";

                    case "t1":
                        if (!ParseFloat(value, out var f1)) return "Invalid float.";
                        T1 = Math.Clamp(f1, 0f, 1f); return $"essencerefill.t1 = {P(T1)}";
                    case "t2":
                        if (!ParseFloat(value, out var f2)) return "Invalid float.";
                        T2 = Math.Clamp(f2, 0f, 1f); return $"essencerefill.t2 = {P(T2)}";
                    case "t3":
                        if (!ParseFloat(value, out var f3)) return "Invalid float.";
                        T3 = Math.Clamp(f3, 0f, 1f); return $"essencerefill.t3 = {P(T3)}";
                    default: return null;
                }
            }

            public void ApplyRaw(string key, string value)
            {
                switch (key)
                {
                    case "enabled": if (ParseBool(value, out var bv))  Enabled = bv; break;
                    case "t1":      if (ParseFloat(value, out var f1)) T1 = Math.Clamp(f1, 0f, 1f); break;
                    case "t2":      if (ParseFloat(value, out var f2)) T2 = Math.Clamp(f2, 0f, 1f); break;
                    case "t3":      if (ParseFloat(value, out var f3)) T3 = Math.Clamp(f3, 0f, 1f); break;
                }
            }

            public string GetRaw(string key) => key switch
            {
                "enabled" => B(Enabled),
                "t1"      => F(T1),
                "t2"      => F(T2),
                "t3"      => F(T3),
                _         => null
            };

            public IEnumerable<(string, string)> GetAllRaw() => new[]
            {
                ("enabled", B(Enabled)),
                ("t1",      F(T1)),
                ("t2",      F(T2)),
                ("t3",      F(T3)),
            };

            public string Help() =>
                "[essencerefill] adjustable settings:\n" +
                "  enabled  true/false/on/off — global kill-switch\n" +
                "  t1       float             — Tier 1 pyreal discount percentage (default: 0.0 = 0%)\n" +
                "  t2       float             — Tier 2 pyreal discount percentage (default: 0.25 = 25%)\n" +
                "  t3       float             — Tier 3 pyreal discount percentage (default: 0.50 = 50%)";

            public string Dump() =>
                "[essencerefill] settings:\n" +
                $"  enabled: {B(Enabled)}\n" +
                $"  t1: {P(T1)}\n" +
                $"  t2: {P(T2)}\n" +
                $"  t3: {P(T3)}\n" +
                "  (base pyreal cost set in ServerConfig)\n";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Universal Summoning Mastery
        // ─────────────────────────────────────────────────────────────────────

        public sealed class UniversalSummoningBlock : ICharmBlock
        {
            public bool Enabled { get; private set; } = true;
            public void Reset() { Enabled = true; }

            public string TrySet(string key, string value)
            {
                if (key is "enabled" or "on" or "off" or "true" or "false")
                {
                    var valueToParse = key == "enabled" ? value : key;
                    if (!ParseBool(valueToParse, out var bv)) return $"Invalid bool: '{value}'";
                    Enabled = bv; return $"universalsummoning.enabled = {B(Enabled)}";
                }
                return null;
            }

            public void ApplyRaw(string key, string value) { if (key == "enabled" && ParseBool(value, out var bv)) Enabled = bv; }
            public string GetRaw(string key) => key == "enabled" ? B(Enabled) : null;
            public IEnumerable<(string, string)> GetAllRaw() => new[] { ("enabled", B(Enabled)) };
            public string Help() => "[universalsummoning] adjustable settings:\n  enabled  true/false/on/off   — global kill-switch (no other tunables yet)";
            public string Dump() =>
                "[universalsummoning] settings:\n" +
                $"  enabled: {B(Enabled)}\n" +
                "  (no other tunables yet)\n";
        }
    }
}
