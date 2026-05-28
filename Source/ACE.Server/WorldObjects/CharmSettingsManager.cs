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

        public static ManaBarrierBlock    ManaBarrier    { get; } = new();
        public static ExplosiveArrowBlock ExplosiveArrow { get; } = new();
        public static ShrapnelBlock       Shrapnel       { get; } = new();
        public static AgonyBlock          Agony          { get; } = new();
        public static PentaCastBlock      PentaCast      { get; } = new();
        public static PrismaticBlock      Prismatic      { get; } = new();
        public static AutoRebuffBlock     AutoRebuff     { get; } = new();

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
                "manabarrier"    => ManaBarrier,
                "explosivearrow" => ExplosiveArrow,
                "shrapnel"       => Shrapnel,
                "agony"          => Agony,
                "pentacast"      => PentaCast,
                "prismaticstrike"=> Prismatic,
                "autorebuff"     => AutoRebuff,
                _                => null
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
            ManaBarrier.Reset();    PersistBlock("manabarrier",    ManaBarrier);
            ExplosiveArrow.Reset(); PersistBlock("explosivearrow", ExplosiveArrow);
            Shrapnel.Reset();       PersistBlock("shrapnel",       Shrapnel);
            Agony.Reset();          PersistBlock("agony",          Agony);
            PentaCast.Reset();      PersistBlock("pentacast",      PentaCast);
            Prismatic.Reset();      PersistBlock("prismaticstrike",Prismatic);
            AutoRebuff.Reset();     PersistBlock("autorebuff",     AutoRebuff);
        }

        /// <summary>
        /// Resets a single named charm to defaults and persists.
        /// Returns false if the charm name is unknown.
        /// </summary>
        public static bool TryReset(string charmName)
        {
            switch (charmName)
            {
                case "manabarrier":    ManaBarrier.Reset();    PersistBlock("manabarrier",    ManaBarrier);    return true;
                case "explosivearrow": ExplosiveArrow.Reset(); PersistBlock("explosivearrow", ExplosiveArrow); return true;
                case "shrapnel":       Shrapnel.Reset();       PersistBlock("shrapnel",       Shrapnel);       return true;
                case "agony":          Agony.Reset();          PersistBlock("agony",          Agony);          return true;
                case "pentacast":      PentaCast.Reset();      PersistBlock("pentacast",      PentaCast);      return true;
                case "prismaticstrike":Prismatic.Reset();      PersistBlock("prismaticstrike",Prismatic);      return true;
                case "autorebuff":     AutoRebuff.Reset();     PersistBlock("autorebuff",     AutoRebuff);     return true;
                default: return false;
            }
        }

        public static string DumpCharm(string charmName)
        {
            return charmName switch
            {
                "manabarrier"     => ManaBarrier.Dump(),
                "explosivearrow"  => ExplosiveArrow.Dump(),
                "shrapnel"        => Shrapnel.Dump(),
                "agony"           => Agony.Dump(),
                "pentacast"       => PentaCast.Dump(),
                "prismaticstrike" => Prismatic.Dump(),
                "autorebuff"      => AutoRebuff.Dump(),
                _                 => null
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
            "manabarrier"     => ManaBarrier,
            "explosivearrow"  => ExplosiveArrow,
            "shrapnel"        => Shrapnel,
            "agony"           => Agony,
            "pentacast"       => PentaCast,
            "prismaticstrike" => Prismatic,
            "autorebuff"      => AutoRebuff,
            _                 => null
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

        // ─────────────────────────────────────────────────────────────────────
        //  Mana Barrier
        // ─────────────────────────────────────────────────────────────────────

        public sealed class ManaBarrierBlock : ICharmBlock
        {
            public bool  Enabled  { get; private set; } = true;
            // Per-element mana cost ratios (mana per 1 damage absorbed)
            public float Slash    { get; private set; } = 1.0f;
            public float Pierce   { get; private set; } = 1.0f;
            public float Bludgeon { get; private set; } = 1.0f;
            public float Fire     { get; private set; } = 1.0f;
            public float Cold     { get; private set; } = 1.0f;
            public float Acid     { get; private set; } = 1.0f;
            public float Electric { get; private set; } = 1.0f;
            public float Health   { get; private set; } = 1.0f;
            public float Nether   { get; private set; } = 1.0f;
            // Per-tier efficiency multipliers (multiplied on top of the ratio)
            // T1: 1.000 = 1 mana per 1 damage
            // T2: 0.667 = 1 mana per 1.5 damage (more efficient)
            // T3: 0.500 = 1 mana per 2 damage   (most efficient)
            public float T1Mod   { get; private set; } = 1.000f;
            public float T2Mod   { get; private set; } = 0.667f;
            public float T3Mod   { get; private set; } = 0.500f;

            public void Reset()
            {
                Enabled = true;
                Slash = Pierce = Bludgeon = Fire = Cold = Acid = Electric = Health = Nether = 1.0f;
                T1Mod = 1.000f; T2Mod = 0.667f; T3Mod = 0.500f;
            }

            public string TrySet(string key, string value)
            {
                switch (key)
                {
                    case "enabled": case "on": case "off": case "true": case "false":
                        var bKey = key == "enabled" ? value : key;
                        if (!ParseBool(bKey == "enabled" ? value : bKey, out var bv)) return $"Invalid bool: '{value}'. Use true/on or false/off.";
                        Enabled = bv; return $"manabarrier.enabled → {B(Enabled)}";

                    case "slash":    if (!ParseFloat(value, out var sv))  return $"Invalid float: '{value}'."; Slash    = sv;  return $"manabarrier.slash → {F(Slash)}";
                    case "pierce":   if (!ParseFloat(value, out var pv))  return $"Invalid float: '{value}'."; Pierce   = pv;  return $"manabarrier.pierce → {F(Pierce)}";
                    case "bludgeon": if (!ParseFloat(value, out var blv)) return $"Invalid float: '{value}'."; Bludgeon = blv; return $"manabarrier.bludgeon → {F(Bludgeon)}";
                    case "fire":     if (!ParseFloat(value, out var fv))  return $"Invalid float: '{value}'."; Fire     = fv;  return $"manabarrier.fire → {F(Fire)}";
                    case "cold":     if (!ParseFloat(value, out var cv))  return $"Invalid float: '{value}'."; Cold     = cv;  return $"manabarrier.cold → {F(Cold)}";
                    case "acid":     if (!ParseFloat(value, out var av))  return $"Invalid float: '{value}'."; Acid     = av;  return $"manabarrier.acid → {F(Acid)}";
                    case "electric": if (!ParseFloat(value, out var ev))  return $"Invalid float: '{value}'."; Electric = ev;  return $"manabarrier.electric → {F(Electric)}";
                    case "health":   if (!ParseFloat(value, out var hv))  return $"Invalid float: '{value}'."; Health   = hv;  return $"manabarrier.health → {F(Health)}";
                    case "nether":   if (!ParseFloat(value, out var nv))  return $"Invalid float: '{value}'."; Nether   = nv;  return $"manabarrier.nether → {F(Nether)}";
                    case "t1mod":    if (!ParseFloat(value, out var t1v)) return $"Invalid float: '{value}'."; T1Mod    = t1v; return $"manabarrier.t1mod → {F(T1Mod)}";
                    case "t2mod":    if (!ParseFloat(value, out var t2v)) return $"Invalid float: '{value}'."; T2Mod    = t2v; return $"manabarrier.t2mod → {F(T2Mod)}";
                    case "t3mod":    if (!ParseFloat(value, out var t3v)) return $"Invalid float: '{value}'."; T3Mod    = t3v; return $"manabarrier.t3mod → {F(T3Mod)}";
                    default: return null;
                }
            }

            public void ApplyRaw(string key, string value)
            {
                switch (key)
                {
                    case "enabled":  if (ParseBool(value, out var bv))  Enabled  = bv;  break;
                    case "slash":    if (ParseFloat(value, out var v))   Slash    = v;   break;
                    case "pierce":   if (ParseFloat(value, out var v2))  Pierce   = v2;  break;
                    case "bludgeon": if (ParseFloat(value, out var v3))  Bludgeon = v3;  break;
                    case "fire":     if (ParseFloat(value, out var v4))  Fire     = v4;  break;
                    case "cold":     if (ParseFloat(value, out var v5))  Cold     = v5;  break;
                    case "acid":     if (ParseFloat(value, out var v6))  Acid     = v6;  break;
                    case "electric": if (ParseFloat(value, out var v7))  Electric = v7;  break;
                    case "health":   if (ParseFloat(value, out var v8))  Health   = v8;  break;
                    case "nether":   if (ParseFloat(value, out var v9))  Nether   = v9;  break;
                    case "t1mod":    if (ParseFloat(value, out var t1v)) T1Mod    = t1v; break;
                    case "t2mod":    if (ParseFloat(value, out var t2v)) T2Mod    = t2v; break;
                    case "t3mod":    if (ParseFloat(value, out var t3v)) T3Mod    = t3v; break;
                }
            }

            public string GetRaw(string key) => key switch
            {
                "enabled"  => B(Enabled),
                "slash"    => F(Slash),
                "pierce"   => F(Pierce),
                "bludgeon" => F(Bludgeon),
                "fire"     => F(Fire),
                "cold"     => F(Cold),
                "acid"     => F(Acid),
                "electric" => F(Electric),
                "health"   => F(Health),
                "nether"   => F(Nether),
                "t1mod"    => F(T1Mod),
                "t2mod"    => F(T2Mod),
                "t3mod"    => F(T3Mod),
                _          => null
            };

            public IEnumerable<(string, string)> GetAllRaw() => new[]
            {
                ("enabled",  B(Enabled)),
                ("slash",    F(Slash)),
                ("pierce",   F(Pierce)),
                ("bludgeon", F(Bludgeon)),
                ("fire",     F(Fire)),
                ("cold",     F(Cold)),
                ("acid",     F(Acid)),
                ("electric", F(Electric)),
                ("health",   F(Health)),
                ("nether",   F(Nether)),
                ("t1mod",    F(T1Mod)),
                ("t2mod",    F(T2Mod)),
                ("t3mod",    F(T3Mod)),
            };

            public string Dump() =>
                $"[manabarrier] enabled={B(Enabled)} | ratio: slash={F(Slash)} pierce={F(Pierce)} bludgeon={F(Bludgeon)} " +
                $"fire={F(Fire)} cold={F(Cold)} acid={F(Acid)} electric={F(Electric)} health={F(Health)} nether={F(Nether)} " +
                $"| tiers: t1mod={F(T1Mod)} t2mod={F(T2Mod)} t3mod={F(T3Mod)}\n";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Explosive Arrow
        // ─────────────────────────────────────────────────────────────────────

        public sealed class ExplosiveArrowBlock : ICharmBlock
        {
            public bool  Enabled { get; private set; } = true;
            public float T1Min   { get; private set; } = 0.40f;
            public float T1Max   { get; private set; } = 0.60f;
            public float T2Min   { get; private set; } = 0.65f;
            public float T2Max   { get; private set; } = 0.85f;
            public float T3Min   { get; private set; } = 0.90f;
            public float T3Max   { get; private set; } = 1.10f;
            public float Radius  { get; private set; } = 15.0f;
            public float Height  { get; private set; } = 10.0f;
            public float Delay   { get; private set; } = 1.0f;

            public void Reset()
            {
                Enabled = true;
                T1Min = 0.40f; T1Max = 0.60f;
                T2Min = 0.65f; T2Max = 0.85f;
                T3Min = 0.90f; T3Max = 1.10f;
                Radius = 15.0f; Height = 10.0f; Delay = 1.0f;
            }

            public string TrySet(string key, string value)
            {
                switch (key)
                {
                    case "enabled": case "on": case "off": case "true": case "false":
                        var bKey = key == "enabled" ? value : key;
                        if (!ParseBool(bKey == "enabled" ? value : bKey, out var bv)) return $"Invalid bool: '{value}'.";
                        Enabled = bv; return $"explosivearrow.enabled → {B(Enabled)}";

                    case "t1min": if (!ParseFloat(value, out var v)) return $"Invalid float."; T1Min = v; return $"explosivearrow.t1min → {F(T1Min)}";
                    case "t1max": if (!ParseFloat(value, out var v2)) return $"Invalid float."; T1Max = v2; return $"explosivearrow.t1max → {F(T1Max)}";
                    case "t2min": if (!ParseFloat(value, out var v3)) return $"Invalid float."; T2Min = v3; return $"explosivearrow.t2min → {F(T2Min)}";
                    case "t2max": if (!ParseFloat(value, out var v4)) return $"Invalid float."; T2Max = v4; return $"explosivearrow.t2max → {F(T2Max)}";
                    case "t3min": if (!ParseFloat(value, out var v5)) return $"Invalid float."; T3Min = v5; return $"explosivearrow.t3min → {F(T3Min)}";
                    case "t3max": if (!ParseFloat(value, out var v6)) return $"Invalid float."; T3Max = v6; return $"explosivearrow.t3max → {F(T3Max)}";
                    case "radius": if (!ParseFloat(value, out var v7)) return $"Invalid float."; Radius = v7; return $"explosivearrow.radius → {F(Radius)}";
                    case "height": if (!ParseFloat(value, out var v8)) return $"Invalid float."; Height = v8; return $"explosivearrow.height → {F(Height)}";
                    case "delay":  if (!ParseFloat(value, out var v9)) return $"Invalid float."; Delay  = v9; return $"explosivearrow.delay → {F(Delay)}";
                    default: return null;
                }
            }

            public void ApplyRaw(string key, string value)
            {
                switch (key)
                {
                    case "enabled": if (ParseBool(value, out var bv))  Enabled = bv; break;
                    case "t1min":   if (ParseFloat(value, out var v))  T1Min   = v;  break;
                    case "t1max":   if (ParseFloat(value, out var v2)) T1Max   = v2; break;
                    case "t2min":   if (ParseFloat(value, out var v3)) T2Min   = v3; break;
                    case "t2max":   if (ParseFloat(value, out var v4)) T2Max   = v4; break;
                    case "t3min":   if (ParseFloat(value, out var v5)) T3Min   = v5; break;
                    case "t3max":   if (ParseFloat(value, out var v6)) T3Max   = v6; break;
                    case "radius":  if (ParseFloat(value, out var v7)) Radius  = v7; break;
                    case "height":  if (ParseFloat(value, out var v8)) Height  = v8; break;
                    case "delay":   if (ParseFloat(value, out var v9)) Delay   = v9; break;
                }
            }

            public string GetRaw(string key) => key switch
            {
                "enabled" => B(Enabled),
                "t1min"   => F(T1Min),
                "t1max"   => F(T1Max),
                "t2min"   => F(T2Min),
                "t2max"   => F(T2Max),
                "t3min"   => F(T3Min),
                "t3max"   => F(T3Max),
                "radius"  => F(Radius),
                "height"  => F(Height),
                "delay"   => F(Delay),
                _         => null
            };

            public IEnumerable<(string, string)> GetAllRaw() => new[]
            {
                ("enabled", B(Enabled)),
                ("t1min", F(T1Min)), ("t1max", F(T1Max)),
                ("t2min", F(T2Min)), ("t2max", F(T2Max)),
                ("t3min", F(T3Min)), ("t3max", F(T3Max)),
                ("radius", F(Radius)), ("height", F(Height)), ("delay", F(Delay)),
            };

            public string Dump() =>
                $"[explosivearrow] enabled={B(Enabled)} t1={F(T1Min)}-{F(T1Max)} t2={F(T2Min)}-{F(T2Max)} t3={F(T3Min)}-{F(T3Max)} " +
                $"radius={F(Radius)} height={F(Height)} delay={F(Delay)}\n";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Simple on/off blocks (stubs ready for future params)
        // ─────────────────────────────────────────────────────────────────────

        public sealed class ShrapnelBlock : ICharmBlock
        {
            public bool Enabled { get; private set; } = true;
            public void Reset() { Enabled = true; }

            public string TrySet(string key, string value)
            {
                if (key is "enabled" or "on" or "off" or "true" or "false")
                {
                    var bKey = key == "enabled" ? value : key;
                    if (!ParseBool(bKey == "enabled" ? value : bKey, out var bv)) return $"Invalid bool: '{value}'.";
                    Enabled = bv; return $"shrapnel.enabled → {B(Enabled)}";
                }
                return null;
            }

            public void ApplyRaw(string key, string value) { if (key == "enabled" && ParseBool(value, out var bv)) Enabled = bv; }
            public string GetRaw(string key) => key == "enabled" ? B(Enabled) : null;
            public IEnumerable<(string, string)> GetAllRaw() => new[] { ("enabled", B(Enabled)) };
            public string Dump() => $"[shrapnel] enabled={B(Enabled)} (no other tunables yet)\n";
        }

        public sealed class AgonyBlock : ICharmBlock
        {
            public bool Enabled { get; private set; } = true;
            public void Reset() { Enabled = true; }

            public string TrySet(string key, string value)
            {
                if (key is "enabled" or "on" or "off" or "true" or "false")
                {
                    var bKey = key == "enabled" ? value : key;
                    if (!ParseBool(bKey == "enabled" ? value : bKey, out var bv)) return $"Invalid bool: '{value}'.";
                    Enabled = bv; return $"agony.enabled → {B(Enabled)}";
                }
                return null;
            }

            public void ApplyRaw(string key, string value) { if (key == "enabled" && ParseBool(value, out var bv)) Enabled = bv; }
            public string GetRaw(string key) => key == "enabled" ? B(Enabled) : null;
            public IEnumerable<(string, string)> GetAllRaw() => new[] { ("enabled", B(Enabled)) };
            public string Dump() => $"[agony] enabled={B(Enabled)} (no other tunables yet)\n";
        }

        public sealed class PrismaticBlock : ICharmBlock
        {
            public bool Enabled { get; private set; } = true;
            public void Reset() { Enabled = true; }

            public string TrySet(string key, string value)
            {
                if (key is "enabled" or "on" or "off" or "true" or "false")
                {
                    var bKey = key == "enabled" ? value : key;
                    if (!ParseBool(bKey == "enabled" ? value : bKey, out var bv)) return $"Invalid bool: '{value}'.";
                    Enabled = bv; return $"prismaticstrike.enabled → {B(Enabled)}";
                }
                return null;
            }

            public void ApplyRaw(string key, string value) { if (key == "enabled" && ParseBool(value, out var bv)) Enabled = bv; }
            public string GetRaw(string key) => key == "enabled" ? B(Enabled) : null;
            public IEnumerable<(string, string)> GetAllRaw() => new[] { ("enabled", B(Enabled)) };
            public string Dump() => $"[prismaticstrike] enabled={B(Enabled)} (no other tunables yet)\n";
        }

        public sealed class AutoRebuffBlock : ICharmBlock
        {
            public bool Enabled { get; private set; } = true;
            public void Reset() { Enabled = true; }

            public string TrySet(string key, string value)
            {
                if (key is "enabled" or "on" or "off" or "true" or "false")
                {
                    var bKey = key == "enabled" ? value : key;
                    if (!ParseBool(bKey == "enabled" ? value : bKey, out var bv)) return $"Invalid bool: '{value}'.";
                    Enabled = bv; return $"autorebuff.enabled → {B(Enabled)}";
                }
                return null;
            }

            public void ApplyRaw(string key, string value) { if (key == "enabled" && ParseBool(value, out var bv)) Enabled = bv; }
            public string GetRaw(string key) => key == "enabled" ? B(Enabled) : null;
            public IEnumerable<(string, string)> GetAllRaw() => new[] { ("enabled", B(Enabled)) };
            public string Dump() => $"[autorebuff] enabled={B(Enabled)} (no other tunables yet)\n";
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
                        var bKey = key == "enabled" ? value : key;
                        if (!ParseBool(bKey == "enabled" ? value : bKey, out var bv)) return $"Invalid bool: '{value}'.";
                        Enabled = bv; return $"pentacast.enabled → {B(Enabled)}";

                    case "targets": if (!ParseInt(value, out var iv)) return $"Invalid int."; Targets = iv; return $"pentacast.targets → {Targets}";
                    case "range":   if (!ParseFloat(value, out var fv)) return $"Invalid float."; Range = fv; return $"pentacast.range → {F(Range)}";
                    default: return null;
                }
            }

            public void ApplyRaw(string key, string value)
            {
                switch (key)
                {
                    case "enabled": if (ParseBool(value, out var bv))  Enabled = bv; break;
                    case "targets": if (ParseInt(value, out var iv))   Targets = iv; break;
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

            public string Dump() =>
                $"[pentacast] enabled={B(Enabled)} targets={Targets} range={F(Range)}\n";
        }
    }
}
