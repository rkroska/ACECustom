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
        public static SplitCastBlock          SplitCast          { get; } = new();
        public static OmnistrikeBlock         Omnistrike         { get; } = new();
        public static AutoRebuffBlock         AutoRebuff         { get; } = new();
        public static InfiniteCastingBlock    InfiniteCasting    { get; } = new();
        public static AsheronsFavorBlock      AsheronsFavor      { get; } = new();
        public static ArtisansBlock           Artisans           { get; } = new();
        public static EssenceRefillBlock      EssenceRefill      { get; } = new();
        public static UniversalSummoningBlock UniversalSummoning { get; } = new();
        public static ForkBlock               Fork               { get; } = new();

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
            sb.Append(SplitCast.Dump());
            sb.Append(Omnistrike.Dump());
            sb.Append(AutoRebuff.Dump());
            sb.Append(InfiniteCasting.Dump());
            sb.Append(AsheronsFavor.Dump());
            sb.Append(Artisans.Dump());
            sb.Append(EssenceRefill.Dump());
            sb.Append(UniversalSummoning.Dump());
            sb.Append(Fork.Dump());
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
                "pentacast"          => SplitCast,
                "splitcast"          => SplitCast,
                "prismaticstrike"    => Omnistrike,
                "omnistrike"         => Omnistrike,
                "autorebuff"         => AutoRebuff,
                "infinitecasting"    => InfiniteCasting,
                "asheronsfavor"      => AsheronsFavor,
                "artisans"           => Artisans,
                "essencerefill"      => EssenceRefill,
                "universalsummoning" => UniversalSummoning,
                "fork"               => Fork,
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
            SplitCast.Reset();          PersistBlock("splitcast",          SplitCast);
            Omnistrike.Reset();          PersistBlock("omnistrike",         Omnistrike);
            AutoRebuff.Reset();         PersistBlock("autorebuff",         AutoRebuff);
            InfiniteCasting.Reset();    PersistBlock("infinitecasting",    InfiniteCasting);
            AsheronsFavor.Reset();      PersistBlock("asheronsfavor",      AsheronsFavor);
            Artisans.Reset();           PersistBlock("artisans",           Artisans);
            EssenceRefill.Reset();      PersistBlock("essencerefill",      EssenceRefill);
            UniversalSummoning.Reset(); PersistBlock("universalsummoning", UniversalSummoning);
            Fork.Reset();               PersistBlock("fork",               Fork);
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
                case "pentacast":
                case "splitcast":          SplitCast.Reset();          PersistBlock("splitcast",          SplitCast);          return true;
                case "prismaticstrike":
                case "omnistrike":         Omnistrike.Reset();          PersistBlock("omnistrike",         Omnistrike);          return true;
                case "autorebuff":         AutoRebuff.Reset();         PersistBlock("autorebuff",         AutoRebuff);         return true;
                case "infinitecasting":    InfiniteCasting.Reset();    PersistBlock("infinitecasting",    InfiniteCasting);    return true;
                case "asheronsfavor":      AsheronsFavor.Reset();      PersistBlock("asheronsfavor",      AsheronsFavor);      return true;
                case "artisans":           Artisans.Reset();           PersistBlock("artisans",           Artisans);           return true;
                case "essencerefill":      EssenceRefill.Reset();      PersistBlock("essencerefill",      EssenceRefill);      return true;
                case "universalsummoning": UniversalSummoning.Reset(); PersistBlock("universalsummoning", UniversalSummoning); return true;
                case "fork":               Fork.Reset();               PersistBlock("fork",               Fork);               return true;
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
                "pentacast"          => SplitCast.Dump(),
                "splitcast"          => SplitCast.Dump(),
                "prismaticstrike"    => Omnistrike.Dump(),
                "omnistrike"         => Omnistrike.Dump(),
                "autorebuff"         => AutoRebuff.Dump(),
                "infinitecasting"    => InfiniteCasting.Dump(),
                "asheronsfavor"      => AsheronsFavor.Dump(),
                "artisans"           => Artisans.Dump(),
                "essencerefill"      => EssenceRefill.Dump(),
                "universalsummoning" => UniversalSummoning.Dump(),
                "fork"               => Fork.Dump(),
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
                "pentacast"          => SplitCast.Help(),
                "splitcast"          => SplitCast.Help(),
                "prismaticstrike"    => Omnistrike.Help(),
                "omnistrike"         => Omnistrike.Help(),
                "autorebuff"         => AutoRebuff.Help(),
                "infinitecasting"    => InfiniteCasting.Help(),
                "asheronsfavor"      => AsheronsFavor.Help(),
                "artisans"           => Artisans.Help(),
                "essencerefill"      => EssenceRefill.Help(),
                "universalsummoning" => UniversalSummoning.Help(),
                "fork"               => Fork.Help(),
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
            "pentacast"          => SplitCast,
            "splitcast"          => SplitCast,
            "prismaticstrike"    => Omnistrike,
            "omnistrike"         => Omnistrike,
            "autorebuff"         => AutoRebuff,
            "infinitecasting"    => InfiniteCasting,
            "asheronsfavor"      => AsheronsFavor,
            "artisans"           => Artisans,
            "essencerefill"      => EssenceRefill,
            "universalsummoning" => UniversalSummoning,
            "fork"               => Fork,
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
            public float T1      { get; private set; } = 0.5f;
            public float T2      { get; private set; } = 1.0f;
            public float T3      { get; private set; } = 2.0f;

            public void Reset()
            {
                Enabled = true;
                Ratio = 1.0f;
                T1 = 0.5f; T2 = 1.0f; T3 = 2.0f;
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
                "[ManaBarrier] Adjustable Settings\n" +
                "  • Enabled  on / off\n" +
                "  • t1 float  — Mana Barrier: damage absorbed per mana (higher is more efficient)\n" +
                "  • t2 float  — Greater Mana Barrier: damage absorbed per mana (higher is more efficient)\n" +
                "  • t3 float  — Master Mana Barrier: damage absorbed per mana (higher is more efficient)\n" +
                "\n[Examples]\n" +
                "  • /charm manabarrier on\n" +
                "  • /charm manabarrier t1 2.5";

            public string Dump() =>
                "[ManaBarrier] Current Settings\n" +
                $"  • Enabled: {B(Enabled)}\n" +
                $"  • t1: {T1.ToString("0.0", CultureInfo.InvariantCulture)}\n" +
                $"  • t2: {T2.ToString("0.0", CultureInfo.InvariantCulture)}\n" +
                $"  • t3: {T3.ToString("0.0", CultureInfo.InvariantCulture)}\n";
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

                    case "t1min":
                        if (!ParseFloat(value, out var v)) return "Invalid float.";
                        if (v > T1Max) return $"t1min ({F(v)}) cannot be greater than t1max ({F(T1Max)}).";
                        T1Min = v; return $"explosivearrow.t1min = {F(T1Min)}";
                    case "t1max":
                        if (!ParseFloat(value, out var v2)) return "Invalid float.";
                        if (v2 < T1Min) return $"t1max ({F(v2)}) cannot be less than t1min ({F(T1Min)}).";
                        T1Max = v2; return $"explosivearrow.t1max = {F(T1Max)}";
                    case "t2min":
                        if (!ParseFloat(value, out var v3)) return "Invalid float.";
                        if (v3 > T2Max) return $"t2min ({F(v3)}) cannot be greater than t2max ({F(T2Max)}).";
                        T2Min = v3; return $"explosivearrow.t2min = {F(T2Min)}";
                    case "t2max":
                        if (!ParseFloat(value, out var v4)) return "Invalid float.";
                        if (v4 < T2Min) return $"t2max ({F(v4)}) cannot be less than t2min ({F(T2Min)}).";
                        T2Max = v4; return $"explosivearrow.t2max = {F(T2Max)}";
                    case "t3min":
                        if (!ParseFloat(value, out var v5)) return "Invalid float.";
                        if (v5 > T3Max) return $"t3min ({F(v5)}) cannot be greater than t3max ({F(T3Max)}).";
                        T3Min = v5; return $"explosivearrow.t3min = {F(T3Min)}";
                    case "t3max":
                        if (!ParseFloat(value, out var v6)) return "Invalid float.";
                        if (v6 < T3Min) return $"t3max ({F(v6)}) cannot be less than t3min ({F(T3Min)}).";
                        T3Max = v6; return $"explosivearrow.t3max = {F(T3Max)}";
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
                "[ExplosiveArrow] Adjustable Settings\n" +
                "  • Enabled  on / off\n" +
                "  • t1min float  — Explosive Arrow: minimum blast damage percentage\n" +
                "  • t1max float  — Explosive Arrow: maximum blast damage percentage\n" +
                "  • t2min float  — Greater Explosive Arrow: minimum blast damage percentage\n" +
                "  • t2max float  — Greater Explosive Arrow: maximum blast damage percentage\n" +
                "  • t3min float  — Master Explosive Arrow: minimum blast damage percentage\n" +
                "  • t3max float  — Master Explosive Arrow: maximum blast damage percentage\n" +
                "  • radius float  — AOE blast radius in meters\n" +
                "  • height float  — AOE blast cylinder height\n" +
                "  • delay float   — seconds between arrow hit and detonation\n" +
                "  • maxarrows int — maximum detonations per shot (1 to 10)\n" +
                "\n[Examples]\n" +
                "  • /charm explosivearrow on\n" +
                "  • /charm explosivearrow t1min 0.15\n" +
                "  • /charm explosivearrow maxarrows 3";

            public string Dump() =>
                "[ExplosiveArrow] Current Settings\n" +
                $"  • Enabled: {B(Enabled)}\n" +
                $"  • t1min: {T1Min.ToString("0.00", CultureInfo.InvariantCulture)}\n" +
                $"  • t1max: {T1Max.ToString("0.00", CultureInfo.InvariantCulture)}\n" +
                $"  • t2min: {T2Min.ToString("0.00", CultureInfo.InvariantCulture)}\n" +
                $"  • t2max: {T2Max.ToString("0.00", CultureInfo.InvariantCulture)}\n" +
                $"  • t3min: {T3Min.ToString("0.00", CultureInfo.InvariantCulture)}\n" +
                $"  • t3max: {T3Max.ToString("0.00", CultureInfo.InvariantCulture)}\n" +
                $"  • radius: {Radius.ToString("0.0", CultureInfo.InvariantCulture)}\n" +
                $"  • height: {Height.ToString("0.0", CultureInfo.InvariantCulture)}\n" +
                $"  • delay: {Delay.ToString("0.0", CultureInfo.InvariantCulture)}\n" +
                $"  • maxarrows: {MaxArrows}\n";
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
                "[Shrapnel] Adjustable Settings\n" +
                "  • Enabled  on / off\n" +
                "  • radius float  — Rocky Shrapnel: AOE physical blast radius in meters\n" +
                "  • height float  — Rocky Shrapnel: AOE physical blast height in meters\n" +
                "  • double float  — Rocky Shrapnel: double proc chance (0.0 to 1.0)\n" +
                "  • triple float  — Rocky Shrapnel: triple proc chance (0.0 to 1.0)\n" +
                "\n[Examples]\n" +
                "  • /charm shrapnel on\n" +
                "  • /charm shrapnel radius 8.0\n" +
                "  • /charm shrapnel double 0.5";

            public string Dump() =>
                "[Shrapnel] Current Settings\n" +
                $"  • Enabled: {B(Enabled)}\n" +
                $"  • radius: {Radius.ToString("0.0", CultureInfo.InvariantCulture)}\n" +
                $"  • height: {Height.ToString("0.0", CultureInfo.InvariantCulture)}\n" +
                $"  • double: {DoubleChance.ToString("0.00", CultureInfo.InvariantCulture)}\n" +
                $"  • triple: {TripleChance.ToString("0.00", CultureInfo.InvariantCulture)}\n";
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
                "[Agony] Adjustable Settings\n" +
                "  • Enabled  on / off\n" +
                "  • radius float  — Ring of Agony: AOE blast radius in meters\n" +
                "  • height float  — Ring of Agony: AOE blast height in meters\n" +
                "  • double float  — Ring of Agony: double proc chance (0.0 to 1.0)\n" +
                "  • triple float  — Ring of Agony: triple proc chance (0.0 to 1.0)\n" +
                "\n[Examples]\n" +
                "  • /charm agony on\n" +
                "  • /charm agony radius 8.0\n" +
                "  • /charm agony double 0.5";

            public string Dump() =>
                "[Agony] Current Settings\n" +
                $"  • Enabled: {B(Enabled)}\n" +
                $"  • radius: {Radius.ToString("0.0", CultureInfo.InvariantCulture)}\n" +
                $"  • height: {Height.ToString("0.0", CultureInfo.InvariantCulture)}\n" +
                $"  • double: {DoubleChance.ToString("0.00", CultureInfo.InvariantCulture)}\n" +
                $"  • triple: {TripleChance.ToString("0.00", CultureInfo.InvariantCulture)}\n";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Omni Strike
        // ─────────────────────────────────────────────────────────────────────

        public sealed class OmnistrikeBlock : ICharmBlock
        {
            public bool Enabled { get; private set; } = true;
            public void Reset() { Enabled = true; }

            public string TrySet(string key, string value)
            {
                if (key is "enabled" or "on" or "off" or "true" or "false")
                {
                    var valueToParse = key == "enabled" ? value : key;
                    if (!ParseBool(valueToParse, out var bv)) return $"Invalid bool: '{value}'";
                    Enabled = bv; return $"omnistrike.enabled = {B(Enabled)}";
                }
                return null;
            }

            public void ApplyRaw(string key, string value) { if (key == "enabled" && ParseBool(value, out var bv)) Enabled = bv; }
            public string GetRaw(string key) => key == "enabled" ? B(Enabled) : null;
            public IEnumerable<(string, string)> GetAllRaw() => new[] { ("enabled", B(Enabled)) };
            public string Help() =>
                "[Omnistrike] Adjustable Settings\n" +
                "  • Enabled  on / off\n" +
                "\n[Examples]\n" +
                "  • /charm omnistrike on";

            public string Dump() =>
                "[Omnistrike] Current Settings\n" +
                $"  • Enabled: {B(Enabled)}\n";
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
            public string Help() =>
                "[AutoRebuff] Adjustable Settings\n" +
                "  • Enabled  on / off\n" +
                "\n[Examples]\n" +
                "  • /charm autorebuff on";

            public string Dump() =>
                "[AutoRebuff] Current Settings\n" +
                $"  • Enabled: {B(Enabled)}\n";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Split Cast
        // ─────────────────────────────────────────────────────────────────────

        public sealed class SplitCastBlock : ICharmBlock
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
                        Enabled = bv; return $"splitcast.enabled = {B(Enabled)}";

                    case "targets":
                        if (!ParseInt(value, out var iv)) return $"Invalid int.";
                        if (iv < 1 || iv > 20) return $"Value must be between 1 and 20.";
                        Targets = iv; return $"splitcast.targets = {Targets}";
                    case "range":   if (!ParseFloat(value, out var fv)) return $"Invalid float."; Range = fv; return $"splitcast.range = {F(Range)}";
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
                "[SplitCast] Adjustable Settings\n" +
                "  • Enabled  on / off\n" +
                "  • targets int  — Split Cast: number of additional targets to split to (1 to 20)\n" +
                "  • range float  — Split Cast: search radius in meters for split targets\n" +
                "\n[Examples]\n" +
                "  • /charm splitcast on\n" +
                "  • /charm splitcast targets 6\n" +
                "  • /charm splitcast range 15.0";

            public string Dump() =>
                "[SplitCast] Current Settings\n" +
                $"  • Enabled: {B(Enabled)}\n" +
                $"  • targets: {Targets}\n" +
                $"  • range: {Range.ToString("0.0", CultureInfo.InvariantCulture)}\n";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Fork
        // ─────────────────────────────────────────────────────────────────────

        public sealed class ForkBlock : ICharmBlock
        {
            public bool  Enabled { get; private set; } = true;
            public int   Targets { get; private set; } = 4;
            public float Range   { get; private set; } = 25.0f;
            public float Delay   { get; private set; } = 0.5f;
            public float T1Mult  { get; private set; } = 0.50f;
            public float T2Mult  { get; private set; } = 0.75f;
            public float T3Mult  { get; private set; } = 1.00f;

            public void Reset() { Enabled = true; Targets = 4; Range = 25.0f; Delay = 0.5f; T1Mult = 0.50f; T2Mult = 0.75f; T3Mult = 1.00f; }

            public string TrySet(string key, string value)
            {
                switch (key)
                {
                    case "enabled": case "on": case "off": case "true": case "false":
                        var valueToParse = key == "enabled" ? value : key;
                        if (!ParseBool(valueToParse, out var bv)) return $"Invalid bool: '{value}'";
                        Enabled = bv; return $"fork.enabled = {B(Enabled)}";

                    case "targets":
                        if (!ParseInt(value, out var iv)) return $"Invalid int.";
                        if (iv < 1 || iv > 10) return $"Value must be between 1 and 10.";
                        Targets = iv; return $"fork.targets = {Targets}";

                    case "range":
                        if (!ParseFloat(value, out var fv)) return $"Invalid float.";
                        Range = fv; return $"fork.range = {F(Range)}";

                    case "delay":
                        if (!ParseFloat(value, out var dv)) return $"Invalid float.";
                        Delay = Math.Clamp(dv, 0f, 5f); return $"fork.delay = {F(Delay)}";

                    case "t1mult":
                        if (!ParseFloat(value, out var m1)) return $"Invalid float.";
                        T1Mult = Math.Clamp(m1, 0f, 2f); return $"fork.t1mult = {F(T1Mult)}";

                    case "t2mult":
                        if (!ParseFloat(value, out var m2)) return $"Invalid float.";
                        T2Mult = Math.Clamp(m2, 0f, 2f); return $"fork.t2mult = {F(T2Mult)}";

                    case "t3mult":
                        if (!ParseFloat(value, out var m3)) return $"Invalid float.";
                        T3Mult = Math.Clamp(m3, 0f, 2f); return $"fork.t3mult = {F(T3Mult)}";

                    default: return null;
                }
            }

            public void ApplyRaw(string key, string value)
            {
                switch (key)
                {
                    case "enabled": if (ParseBool(value,  out var bv)) Enabled = bv; break;
                    case "targets": if (ParseInt(value,   out var iv)) Targets = Math.Clamp(iv, 1, 10); break;
                    case "range":   if (ParseFloat(value, out var fv)) Range   = fv; break;
                    case "delay":   if (ParseFloat(value, out var dv)) Delay   = Math.Clamp(dv, 0f, 5f); break;
                    case "t1mult":  if (ParseFloat(value, out var m1)) T1Mult  = Math.Clamp(m1, 0f, 2f); break;
                    case "t2mult":  if (ParseFloat(value, out var m2)) T2Mult  = Math.Clamp(m2, 0f, 2f); break;
                    case "t3mult":  if (ParseFloat(value, out var m3)) T3Mult  = Math.Clamp(m3, 0f, 2f); break;
                }
            }

            public string GetRaw(string key) => key switch
            {
                "enabled" => B(Enabled),
                "targets" => Targets.ToString(),
                "range"   => F(Range),
                "delay"   => F(Delay),
                "t1mult"  => F(T1Mult),
                "t2mult"  => F(T2Mult),
                "t3mult"  => F(T3Mult),
                _         => null
            };

            public IEnumerable<(string, string)> GetAllRaw() => new[]
            {
                ("enabled", B(Enabled)),
                ("targets", Targets.ToString()),
                ("range",   F(Range)),
                ("delay",   F(Delay)),
                ("t1mult",  F(T1Mult)),
                ("t2mult",  F(T2Mult)),
                ("t3mult",  F(T3Mult)),
            };

            public string Help() =>
                "[Fork] Adjustable Settings\n" +
                "  • Enabled  on / off\n" +
                "  • targets int   — number of fork targets per hit (1 to 10)\n" +
                "  • range float   — search radius in meters for fork targets\n" +
                "  • delay float   — seconds to wait after hit before forks launch (default 0.50)\n" +
                "  • t1mult float  — T1 fork damage multiplier (default 0.50)\n" +
                "  • t2mult float  — T2 fork damage multiplier (default 0.75)\n" +
                "  • t3mult float  — T3 fork damage multiplier (default 1.00)\n" +
                "\n[Examples]\n" +
                "  • /charm fork on\n" +
                "  • /charm fork targets 6\n" +
                "  • /charm fork range 15.0\n" +
                "  • /charm fork delay 0.5\n" +
                "  • /charm fork t1mult 0.60";

            public string Dump() =>
                "[Fork] Current Settings\n" +
                $"  • Enabled: {B(Enabled)}\n" +
                $"  • targets: {Targets}\n" +
                $"  • range: {Range.ToString("0.0", CultureInfo.InvariantCulture)}\n" +
                $"  • delay: {F(Delay)}\n" +
                $"  • t1mult: {F(T1Mult)}\n" +
                $"  • t2mult: {F(T2Mult)}\n" +
                $"  • t3mult: {F(T3Mult)}\n";
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
            public string Help() =>
                "[InfiniteCasting] Adjustable Settings\n" +
                "  • Enabled  on / off\n" +
                "\n[Examples]\n" +
                "  • /charm infinitecasting on";

            public string Dump() =>
                "[InfiniteCasting] Current Settings\n" +
                $"  • Enabled: {B(Enabled)}\n";
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
                "[AsheronsFavor] Adjustable Settings\n" +
                "  • Enabled  on / off\n" +
                "  • t1health float — Asheron's Favor: Tier 1 Health multiplier\n" +
                "  • t1armor float  — Asheron's Favor: Tier 1 Natural Armor bonus\n" +
                "  • t2health float — Asheron's Favor: Tier 2 Health multiplier\n" +
                "  • t2armor float  — Asheron's Favor: Tier 2 Natural Armor bonus\n" +
                "  • t3health float — Asheron's Favor: Tier 3 Health multiplier\n" +
                "  • t3armor float  — Asheron's Favor: Tier 3 Natural Armor bonus\n" +
                "\n[Examples]\n" +
                "  • /charm asheronsfavor on\n" +
                "  • /charm asheronsfavor t3health 1.25\n" +
                "  • /charm asheronsfavor t3armor 300.0";

            public string Dump() =>
                "[AsheronsFavor] Current Settings\n" +
                $"  • Enabled: {B(Enabled)}\n" +
                $"  • t1health: {T1Health.ToString("0.00", CultureInfo.InvariantCulture)}\n" +
                $"  • t1armor: {T1Armor.ToString("0.0", CultureInfo.InvariantCulture)}\n" +
                $"  • t2health: {T2Health.ToString("0.00", CultureInfo.InvariantCulture)}\n" +
                $"  • t2armor: {T2Armor.ToString("0.0", CultureInfo.InvariantCulture)}\n" +
                $"  • t3health: {T3Health.ToString("0.00", CultureInfo.InvariantCulture)}\n" +
                $"  • t3armor: {T3Armor.ToString("0.0", CultureInfo.InvariantCulture)}\n";
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
                "[Artisans] Adjustable Settings\n" +
                "  • Enabled  on / off\n" +
                "  • t1 float  — Artisan's Charm: Tier 1 success chance bonus\n" +
                "  • t2 float  — Artisan's Charm: Tier 2 success chance bonus\n" +
                "  • t3 float  — Artisan's Charm: Tier 3 success chance bonus\n" +
                "\n[Examples]\n" +
                "  • /charm artisans on\n" +
                "  • /charm artisans t1 0.05";

            public string Dump() =>
                "[Artisans] Current Settings\n" +
                $"  • Enabled: {B(Enabled)}\n" +
                $"  • t1: {T1.ToString("0.00", CultureInfo.InvariantCulture)}\n" +
                $"  • t2: {T2.ToString("0.00", CultureInfo.InvariantCulture)}\n" +
                $"  • t3: {T3.ToString("0.00", CultureInfo.InvariantCulture)}\n";
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
                "[EssenceRefill] Adjustable Settings\n" +
                "  • Enabled  on / off\n" +
                "  • t1 float  — Essence Refill: Tier 1 pyreal discount percentage\n" +
                "  • t2 float  — Essence Refill: Tier 2 pyreal discount percentage\n" +
                "  • t3 float  — Essence Refill: Tier 3 pyreal discount percentage\n" +
                "\n[Examples]\n" +
                "  • /charm essencerefill on\n" +
                "  • /charm essencerefill t2 0.30";

            public string Dump() =>
                "[EssenceRefill] Current Settings\n" +
                $"  • Enabled: {B(Enabled)}\n" +
                $"  • t1: {T1.ToString("0.00", CultureInfo.InvariantCulture)}\n" +
                $"  • t2: {T2.ToString("0.00", CultureInfo.InvariantCulture)}\n" +
                $"  • t3: {T3.ToString("0.00", CultureInfo.InvariantCulture)}\n";
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
            public string Help() =>
                "[UniversalSummoning] Adjustable Settings\n" +
                "  • Enabled  on / off\n" +
                "\n[Examples]\n" +
                "  • /charm universalsummoning on";

            public string Dump() =>
                "[UniversalSummoning] Current Settings\n" +
                $"  • Enabled: {B(Enabled)}\n";
        }
    }
}
