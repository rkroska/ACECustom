using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

using log4net;
using Newtonsoft.Json;

using ACE.Common;
using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity.Actions;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>One tier's Triune pace (owner 2026-11-01): a player killing FloorKph around the clock reaches the tier's Points in
    /// SlowWeeks; CeilingKph around the clock reaches it in FastWeeks; kills past the ceiling in a week earn nothing.</summary>
    public class TriuneTierRow
    {
        public int Tier { get; set; }
        public double FloorKph { get; set; } = 200;
        public double CeilingKph { get; set; } = 400;
        public double SlowWeeks { get; set; } = 6;
        public double FastWeeks { get; set; } = 4;
        /// <summary>How far each of Creature / Item / Life climbs in this tier (= the rolls each school gets here; the next tier's
        /// gates ask this much more). Also each attack school gem's room at T15+: base + the summed Points of T15..this tier.</summary>
        public int Points { get; set; } = 500;
        /// <summary>School gems (owner 2026-11-01): weeks for one school to gain Points at floor pace / at ceiling pace. Since
        /// 2026-11-24 these bands only pace a school within 500 of the tier's ceiling (below that it catches up at the first
        /// band's chance all week), and kills past the ceiling pace keep the reduced chance (no weekly stop for gems).</summary>
        public double GemSlowWeeks { get; set; } = 4;
        public double GemFastWeeks { get; set; } = 2.5;
    }

    /// <summary>
    /// Hunting rewards (owner 2026-11-01 as Triune earning; since 2026-11-22 augmentations straight onto the character, T11-T25).
    /// The store keeps its old name (zc_triune_earning) and the class its old one, so the stored config and every caller still
    /// line up. Rules:
    ///   - a T11+ Zone Control kill that pays XP or lum, credited to the TOP DAMAGER (a pet's kill counts for its owner);
    ///   - only at the player's OWN tier (the tier they are in) - one tier below earns nothing, no dip;
    ///   - per tier, two weekly bands: the first FloorKph x 168 kills pay Points / SlowWeeks between them, the next
    ///     (CeilingKph - FloorKph) x 168 kills pay Points / FastWeeks - Points / SlowWeeks, kills past that pay nothing
    ///     (the school gems do not stop there: within 500 of the tier's ceiling they keep the reduced chance past the first band,
    ///     and further below they keep the first band's chance all week - SchoolGemChance, 2026-11-24);
    ///   - the week turns at a fixed local time (default Friday 12:00 US Eastern), checked lazily on the next kill;
    ///   - a whole reward (3 x that value, carried as a fraction in PropertyFloat.AugDripFraction) rolls ONE of Creature / Item /
    ///     Life among those with rolls left this tier (Points rolls each) and adds +1 straight onto its LumAug count - unless
    ///     that count is already at the tier's end, when the roll is lost (and still used up);
    ///   - T11-T14 earn with DripEnabled, T15-T25 with Enabled; the school gems drop at T11-T25 with GemsEnabled alone (since
    ///     2026-11-24 the drip no longer gates them at T11-T14). The week's kill count runs wherever either is on.
    /// Settings live in the shard config store (zc_triune_earning, JSON) and are edited with /zonecontrol triune. Off by default.
    /// The victim's thread reads the kill; the player's own thread reads and writes everything about the player.
    /// </summary>
    public static class TriuneEarning
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>The first Triune tier (T15). Rows below it, from <see cref="DripMinTier"/>, are the T11-T14 aug drip.</summary>
        public const int MinTier = 15;
        public const int MaxTier = ZoneRare.MaxTier;
        /// <summary>T11-T14 aug drip (owner 2026-11-08): the same weekly bands, each point +1 Creature / Item / Life.</summary>
        public const int DripMinTier = 11;
        public const int DripMaxTier = MinTier - 1;
        /// <summary>The first row in the store (the drip's T11).</summary>
        public const int FirstTier = DripMinTier;

        public static bool IsDripTier(int tier) => tier >= DripMinTier && tier <= DripMaxTier;
        public const double HoursPerWeek = 168;
        private const string StoreKey = "zc_triune_earning";

        public class Config
        {
            public bool Enabled { get; set; }
            /// <summary>0 = Sunday ... 6 = Saturday (System.DayOfWeek).</summary>
            public int ResetDay { get; set; } = (int)DayOfWeek.Friday;
            public int ResetHour { get; set; } = 12;
            /// <summary>A system time zone id (Windows ids work on Windows and, with ICU, on Linux). Bad id = UTC.</summary>
            public string ResetTimeZone { get; set; } = "Eastern Standard Time";
            /// <summary>School gem drops (SchoolGems.cs) - their own switch, T11-T25 (since 2026-11-24 also T11-T14 with the drip
            /// off); they share the weekly kill count with the hunting rewards.</summary>
            public bool GemsEnabled { get; set; }
            /// <summary>The base of the T15+ school gem ceiling (this + the summed Points: 4,500 at T15 ... 9,500 at T25). Until
            /// 2026-11-24 it was also the floor a school needed before its gems dropped; the floor is a flat 2,000 now (GemFloor).</summary>
            public int GemBaseCap { get; set; } = 4000;
            /// <summary>T11-T14 hunting rewards (owner 2026-11-08) - their own switch; T15-T25 use Enabled.</summary>
            public bool DripEnabled { get; set; }
            /// <summary>The aug ladder at T11 entry (owner 2026-10-02): Creature 4,000; Item, Life and the attack school 2,000.
            /// Each tier's Points sit on top: T11 ends at 4,500 / 2,500, T15 at 6,500 / 4,500, T25 at 11,500 / 9,500.</summary>
            public int DripBaseCreature { get; set; } = 4000;
            public int DripBaseItem { get; set; } = 2000;
            public List<TriuneTierRow> Tiers { get; set; } = new();
            /// <summary>'/zonecontrol triune move off': nobody is moved at login (a character still holding the old counters
            /// keeps them, counted in power, and earns nothing until moved).</summary>
            public bool MoveOff { get; set; }
        }

        // limits for every number a command or a hand-edited store can set (review 2026-11-01: finite and bounded, never NaN /
        // Infinity / 1e308): kills an hour, weeks, points per tier, the bought school base
        public const double MinKph = 0.01, MaxKph = 100_000, MinWeeks = 0.1, MaxWeeks = 520;
        public const int MaxPoints = 100_000, MaxGemBaseCap = 1_000_000;

        private static readonly object _lock = new();
        // volatile: read outside the lock (Current's fast path, LoadFailed), written under it
        private static volatile Config _current;
        /// <summary>True while the stored JSON could not be read: the running settings stay, and no edit may save over the store.</summary>
        private static volatile bool _loadFailed;
        // bumped by every settings change (an edit, a reload): a player's next hunting update after one sends every number,
        // so a plugin never keeps the old bands. Per player: the revision their client last got in full.
        private static int _settingsRev;
        private static readonly ConditionalWeakTable<Player, StrongBox<int>> SentRev = new();
        /// <summary>The stored settings could not be read (defaults or the last good copy are running).</summary>
        public static bool LoadFailed => _loadFailed;
        /// <summary>When a failed FIRST load is next retried on its own (a brief DB error must not leave earning off until a reload).</summary>
        private static DateTime _retryLoadAt;
        /// <summary>The failure was the first load (the defaults are running) - only then is the store re-read on its own.</summary>
        private static bool _firstLoadFailed;

        /// <summary>The resolved reset time zone, swapped as ONE reference so a reader never pairs a new zone with an old id.</summary>
        private sealed class ZoneCache { public string Id; public TimeZoneInfo Zone; }
        private static ZoneCache _zone;

        // ---- store ------------------------------------------------------------------------------------------------------

        /// <summary>The live config (loaded on first use). Never mutate it - edits go through Mutate.</summary>
        public static Config Current
        {
            get
            {
                var c = _current;
                if (c != null && !(_loadFailed && _firstLoadFailed && DateTime.UtcNow >= _retryLoadAt))
                    return c;
                lock (_lock)
                {
                    if (_current == null || (_loadFailed && _firstLoadFailed && DateTime.UtcNow >= _retryLoadAt))
                    {
                        // a retry that reads again is a settings change like an edit: the revision moves, so clients get every number
                        try { _current = Load(); _loadFailed = false; _firstLoadFailed = false; Interlocked.Increment(ref _settingsRev); }
                        catch (Exception ex)
                        {
                            // the defaults run (earning OFF) but are never saved over the store; retried once a minute
                            if (_current == null)
                                log.Error($"[TriuneEarning] load failed - defaults used (earning OFF), edits refused until the store reads again: {ex}");
                            _current ??= Normalize(new Config());
                            _loadFailed = true;
                            _firstLoadFailed = true;
                            _retryLoadAt = DateTime.UtcNow.AddMinutes(1);
                        }
                    }
                    return _current;
                }
            }
        }

        public static Config Defaults() => Normalize(new Config());

        /// <summary>Every tier T11..T25 present once, in order, with sane numbers.</summary>
        public static Config Normalize(Config cfg)
        {
            cfg ??= new Config();
            var rows = (cfg.Tiers ?? new List<TriuneTierRow>()).Where(r => r != null && r.Tier >= FirstTier && r.Tier <= MaxTier)
                .GroupBy(r => r.Tier).ToDictionary(g => g.Key, g => g.First());
            cfg.Tiers = new List<TriuneTierRow>();
            for (var t = FirstTier; t <= MaxTier; t++)
            {
                var d = DefaultRow(t);
                var r = rows.TryGetValue(t, out var have) ? have : d;
                r.Tier = t;
                r.FloorKph = InRange(r.FloorKph, MinKph, MaxKph, d.FloorKph);
                r.CeilingKph = InRange(r.CeilingKph, MinKph, MaxKph, Math.Min(MaxKph, r.FloorKph * 2));
                if (!(r.CeilingKph > r.FloorKph)) r.CeilingKph = Math.Min(MaxKph, r.FloorKph * 2);
                if (!(r.CeilingKph > r.FloorKph)) r.FloorKph = r.CeilingKph / 2;   // a floor at the top kph: the ceiling has no room above it
                r.SlowWeeks = InRange(r.SlowWeeks, MinWeeks, MaxWeeks, d.SlowWeeks);
                r.FastWeeks = InRange(r.FastWeeks, MinWeeks, MaxWeeks, Math.Min(d.FastWeeks, r.SlowWeeks));
                if (r.FastWeeks > r.SlowWeeks) r.FastWeeks = Math.Min(d.FastWeeks, r.SlowWeeks);
                r.Points = Math.Clamp(r.Points, 0, MaxPoints);
                r.GemSlowWeeks = InRange(r.GemSlowWeeks, MinWeeks, MaxWeeks, d.GemSlowWeeks);
                r.GemFastWeeks = InRange(r.GemFastWeeks, MinWeeks, MaxWeeks, Math.Min(d.GemFastWeeks, r.GemSlowWeeks));
                if (r.GemFastWeeks > r.GemSlowWeeks) r.GemFastWeeks = Math.Min(d.GemFastWeeks, r.GemSlowWeeks);
                cfg.Tiers.Add(r);
            }
            cfg.ResetDay = ((cfg.ResetDay % 7) + 7) % 7;
            cfg.ResetHour = Math.Clamp(cfg.ResetHour, 0, 23);
            if (string.IsNullOrWhiteSpace(cfg.ResetTimeZone)) cfg.ResetTimeZone = "UTC";
            cfg.GemBaseCap = Math.Clamp(cfg.GemBaseCap, 0, MaxGemBaseCap);
            cfg.DripBaseCreature = Math.Clamp(cfg.DripBaseCreature, 0, MaxGemBaseCap);
            cfg.DripBaseItem = Math.Clamp(cfg.DripBaseItem, 0, MaxGemBaseCap);
            return cfg;
        }

        /// <summary>
        /// A tier's default row. T15+ = the Triune pace (6 / 4 weeks, gems 4 / 2.5). T11-T14 = the drip pace (owner pick 11-08,
        /// option 2): a 200 kph player on UCM 24/7 finishes each step in the days a T10 player at 4 h/day needs; fast = slow / 1.5;
        /// the attack school's gems keep the same weeks so it stays matched to Item / Life.
        /// </summary>
        public static TriuneTierRow DefaultRow(int tier)
        {
            var (slow, fast) = tier switch
            {
                11 => (3.63, 2.42),
                12 => (5.30, 3.53),
                13 => (11.41, 7.61),
                14 => (16.83, 11.22),
                _ => (6.0, 4.0),
            };
            var drip = IsDripTier(tier);
            return new TriuneTierRow
            {
                Tier = tier,
                SlowWeeks = slow,
                FastWeeks = fast,
                GemSlowWeeks = drip ? slow : 4,
                GemFastWeeks = drip ? fast : 2.5,
            };
        }

        /// <summary>A finite value inside [min, max]; anything else (NaN, Infinity, out of range) becomes <paramref name="fallback"/>.</summary>
        public static double InRange(double v, double min, double max, double fallback) => double.IsFinite(v) && v >= min && v <= max ? v : fallback;

        private static Config Load()
        {
            string json = null;
            if (DatabaseManager.ShardConfig.StringExists(StoreKey))
                json = DatabaseManager.ShardConfig.GetString(StoreKey)?.Value;
            return string.IsNullOrWhiteSpace(json) ? Defaults() : Normalize(JsonConvert.DeserializeObject<Config>(json));
        }

        private static void SaveStore(Config cfg)
        {
            var jsonOut = JsonConvert.SerializeObject(cfg);
            if (DatabaseManager.ShardConfig.StringExists(StoreKey))
                DatabaseManager.ShardConfig.SaveString(new ConfigPropertiesString { Key = StoreKey, Value = jsonOut, Description = "Triune earning per tier (JSON)" });
            else
                DatabaseManager.ShardConfig.AddString(StoreKey, jsonOut, "Triune earning per tier (JSON)");
        }

        /// <summary>Edit a clone, save it, publish it. Returns the new config.</summary>
        public static Config Mutate(Action<Config> edit)
        {
            lock (_lock)
            {
                _ = Current;
                if (_loadFailed)
                    throw new InvalidOperationException("the stored Triune settings could not be read - fix them, then /zonecontrol triune reload (nothing was saved)");
                var clone = Normalize(JsonConvert.DeserializeObject<Config>(JsonConvert.SerializeObject(Current)));
                edit(clone);
                clone = Normalize(clone);
                SaveStore(clone);
                _current = clone;
                Interlocked.Increment(ref _settingsRev);
                return clone;
            }
        }

        /// <summary>Re-reads the store. A failed read keeps the config that is running now and throws (the caller reports it).</summary>
        public static void Reload()
        {
            lock (_lock)
            {
                Config fresh;
                try { fresh = Load(); }
                catch
                {
                    // the running settings stay; no edit may now save over the unreadable store until a reload reads it
                    _ = Current;
                    _loadFailed = true;
                    throw;
                }
                _current = fresh;
                _loadFailed = false;
                _firstLoadFailed = false;
                Interlocked.Increment(ref _settingsRev);
            }
        }

        public static TriuneTierRow GetTier(int tier) => Current.Tiers.FirstOrDefault(r => r.Tier == tier);

        /// <summary>The configured reset time zone (cached; a bad id falls back to UTC with one warning).</summary>
        public static TimeZoneInfo ResetZone(Config cfg)
        {
            var id = cfg.ResetTimeZone?.Trim() ?? "UTC";
            var cached = _zone;
            if (cached != null && cached.Id == id)
                return cached.Zone;
            TimeZoneInfo tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (Exception ex) { log.Warn($"[TriuneEarning] time zone '{id}' not found ({ex.Message}) - weeks turn in UTC."); tz = TimeZoneInfo.Utc; }
            _zone = new ZoneCache { Id = id, Zone = tz };
            return tz;
        }

        // ---- pure rules (tested) ----------------------------------------------------------------------------------------

        public static double Band1Kills(TriuneTierRow r) => r.FloorKph * HoursPerWeek;
        public static double Band2Kills(TriuneTierRow r) => Math.Max(0, r.CeilingKph - r.FloorKph) * HoursPerWeek;

        /// <summary>Points per kill in the first band: a week of floor-pace kills pays Points / SlowWeeks.</summary>
        public static double Rate1(TriuneTierRow r)
        {
            var b = Band1Kills(r);
            return b > 0 && r.SlowWeeks > 0 ? r.Points / r.SlowWeeks / b : 0.0;
        }

        /// <summary>Points per kill in the second band: a week of ceiling-pace kills pays Points / FastWeeks in all.</summary>
        public static double Rate2(TriuneTierRow r)
        {
            var b = Band2Kills(r);
            if (!(b > 0) || !(r.FastWeeks > 0) || !(r.SlowWeeks > 0))
                return 0.0;
            return Math.Max(0, r.Points / r.FastWeeks - r.Points / r.SlowWeeks) / b;
        }

        /// <summary>One school's gem chance per kill in the first band: a week of floor-pace kills drops Points / GemSlowWeeks.</summary>
        public static double GemChance1(TriuneTierRow r)
        {
            var b = Band1Kills(r);
            return b > 0 && r.GemSlowWeeks > 0 ? Math.Min(1.0, r.Points / r.GemSlowWeeks / b) : 0.0;
        }

        /// <summary>One school's gem chance per kill in the second band: a week of ceiling-pace kills drops Points / GemFastWeeks.</summary>
        public static double GemChance2(TriuneTierRow r)
        {
            var b = Band2Kills(r);
            if (!(b > 0) || !(r.GemFastWeeks > 0) || !(r.GemSlowWeeks > 0))
                return 0.0;
            return Math.Min(1.0, Math.Max(0, r.Points / r.GemFastWeeks - r.Points / r.GemSlowWeeks) / b);
        }

        /// <summary>One school's reduced gem chance per kill once the first band is used up: the second band's chance - or, for a
        /// tier whose gem weeks make the second band add nothing (gemfast = gemslow), the first band's, so the gems never stop.</summary>
        public static double GemChanceReduced(TriuneTierRow r)
        {
            if (r == null) return 0.0;
            var c2 = GemChance2(r);
            return c2 > 0 ? c2 : GemChance1(r);
        }

        /// <summary>
        /// One school's gem chance on the next kill when <paramref name="killsBefore"/> kills are already counted this week: the
        /// first band's chance, then the reduced chance for the rest of the week. No weekly stop for the school gems (2026-11-24):
        /// only the Creature / Item / Life rewards stop past the second band (KillValue); a school's gems end at its tier's ceiling.
        /// </summary>
        public static double GemChance(TriuneTierRow r, double killsBefore)
        {
            if (r == null) return 0.0;
            killsBefore = Math.Max(0, killsBefore);
            var b1 = Band1Kills(r);
            return Math.Min(1.0, GemChance1(r) * Overlap(killsBefore, 0, b1) + GemChanceReduced(r) * Overlap(killsBefore, b1, double.PositiveInfinity));
        }

        /// <summary>The slowdown window (owner 2026-11-24): a school this close to the tier's gem ceiling, or closer, rolls on the
        /// weekly bands (GemChance); further below, it is catching up and keeps the first band's chance all week.</summary>
        public const long GemSlowdownWindow = 500;

        /// <summary>
        /// One school's gem chance on the next kill, for a school at <paramref name="count"/> under the tier's gem
        /// <paramref name="ceiling"/>: while it catches up (more than GemSlowdownWindow below the ceiling) the first band's chance
        /// whatever the week's count; within the window the weekly bands - slower after the first band, never stopped.
        /// </summary>
        public static double SchoolGemChance(TriuneTierRow r, double killsBefore, long count, long ceiling)
        {
            if (r == null) return 0.0;
            return count < ceiling - GemSlowdownWindow ? GemChance1(r) : GemChance(r, killsBefore);
        }

        /// <summary>Does a school at <paramref name="count"/> feel the weekly slowdown under this <paramref name="ceiling"/>? Only
        /// within GemSlowdownWindow of it (pure, tested).</summary>
        public static bool GemSlowsDown(long count, long ceiling) => count >= ceiling - GemSlowdownWindow;

        /// <summary>The school count a gem earned at <paramref name="tier"/> can raise a school to. T11-T14: the
        /// attack school follows the Item / Life ladder (T11 2,500 ... T14 4,000); T15+: the base cap + the Triune points.</summary>
        public static long GemCeiling(Config cfg, int tier) => IsDripTier(tier) ? DripCeiling(cfg, tier, cfg.DripBaseItem) : cfg.GemBaseCap + TierCap(cfg, tier);

        /// <summary>The school count every attack school needs before its gems drop or work, at every tier (owner 2026-11-24):
        /// 2,000, the T11 entry of the attack school ladder - the barrier to entry.</summary>
        public const long GemFloorAugs = 2000;

        /// <summary>The school count a school needs before its gems drop or work at <paramref name="tier"/>: GemFloorAugs (2,000)
        /// at every tier since 2026-11-24 (catch-up). From there a school under the tier's ceiling rolls gems, so a player can
        /// build a second school while hunting with their first. Until then: T11-T14 that tier's entry on the Item / Life
        /// ladder, T15+ the base cap (4,000). The drop, the use and the pickup rules all read the floor here.</summary>
        public static long GemFloor(Config cfg, int tier) => GemFloorAugs;

        // ---- the augmentation ladder ------------------------------------------------------------------------------------

        /// <summary>Points summed over T11..<paramref name="tier"/> (every row from T11; below T11 = 0). One row's Points is how
        /// far each of Creature / Item / Life climbs in that tier: T11-T14 500 each, T15-T25 500 each.</summary>
        public static long DripPointsThrough(Config cfg, int tier)
        {
            long sum = 0;
            foreach (var r in cfg.Tiers)
                if (r.Tier >= FirstTier && r.Tier <= tier)
                    sum += r.Points;
            return sum;
        }

        /// <summary>The ladder value a school holds at <paramref name="tier"/>'s ENTRY (T11 = the base).</summary>
        public static long DripEntry(Config cfg, int tier, long baseValue) => baseValue + DripPointsThrough(cfg, tier - 1);

        /// <summary>Where a school's rewards stop at <paramref name="tier"/> (= the next tier's entry): Creature 4,500 at T11 ...
        /// 6,500 at T15 ... 11,500 at T25; Item / Life 2,500 at T11 ... 4,500 at T15 ... 9,500 at T25.</summary>
        public static long DripCeiling(Config cfg, int tier, long baseValue) => baseValue + DripPointsThrough(cfg, tier);

        public sealed class DripSchool
        {
            public string Name;
            /// <summary>The ONE stat (owner 2026-11-22): the LumAug count a reward adds to.</summary>
            public PropertyInt64 AugProperty;
            /// <summary>The rolls taken this tier on this school.</summary>
            public PropertyInt64 RollsProperty;
            public Func<Player, long> Count;
            public bool IsCreature;
        }

        /// <summary>The three schools a hunting reward feeds, one at random per reward.</summary>
        public static readonly DripSchool[] DripSchools =
        {
            new DripSchool { Name = "Creature", AugProperty = PropertyInt64.LumAugCreatureCount, RollsProperty = PropertyInt64.AugRollsCreature, Count = p => p.EffectiveCreatureAugCount, IsCreature = true },
            new DripSchool { Name = "Item",     AugProperty = PropertyInt64.LumAugItemCount,     RollsProperty = PropertyInt64.AugRollsItem,     Count = p => p.EffectiveItemAugCount },
            new DripSchool { Name = "Life",     AugProperty = PropertyInt64.LumAugLifeCount,     RollsProperty = PropertyInt64.AugRollsLife,     Count = p => p.EffectiveLifeAugCount },
        };

        public static long DripCeiling(Config cfg, int tier, DripSchool s) => DripCeiling(cfg, tier, s.IsCreature ? cfg.DripBaseCreature : cfg.DripBaseItem);

        /// <summary>
        /// The random roll (owner 2026-11-21): one reward goes to ONE school picked evenly from those with rolls left this tier.
        /// <paramref name="roll"/> is a uniform number in [0, 1). Returns the school's index, or -1 when every school has used
        /// its <paramref name="perSchool"/> rolls (nothing left to earn in this tier). Pure, tested.
        /// </summary>
        public static int PickSchool(long[] rollsTaken, long perSchool, double roll)
        {
            var open = new List<int>(rollsTaken.Length);
            for (var i = 0; i < rollsTaken.Length; i++)
                if (rollsTaken[i] < perSchool)
                    open.Add(i);
            if (open.Count == 0)
                return -1;
            var pick = (int)Math.Floor(Math.Clamp(roll, 0, 0.999999999) * open.Count);
            return open[Math.Min(pick, open.Count - 1)];
        }

        /// <summary>Does a roll on a school at <paramref name="count"/> add +1? Only below the tier's end - a roll on a full school is
        /// lost (it still uses up the roll). Pure, tested.</summary>
        public static bool RollAdds(long count, long tierEnd) => count < tierEnd;

        /// <summary>The rolls a moved Triune Weave counts as taken at the own tier (owner 2026-11-21): clamp(Triune - the Triune the
        /// tiers below it asked, 0, Points). Below T15 nothing (the T11-T14 rows never earned Triune). Pure, tested.</summary>
        public static long SeedRolls(Config cfg, int ownTier, long triune)
        {
            if (ownTier < MinTier)
                return 0;
            var row = cfg.Tiers.FirstOrDefault(r => r.Tier == ownTier);
            if (row == null)
                return 0;
            return Math.Clamp(triune - TierCap(cfg, ownTier - 1), 0, row.Points);
        }

        /// <summary>
        /// The seed for ONE school (review 2026-11-22): never more than the school actually climbed into the own tier,
        /// clamp(count - the tier's entry, 0, Points), and never more than the Triune seed above. A Triune holder whose bought
        /// count sat below its luminance cap (Triune gems had no cap check) is seeded only by how far that school really is,
        /// so its rolls can still carry it to the tier's end. Pure, tested.
        /// </summary>
        public static long SeedRolls(Config cfg, int ownTier, long triune, long count, long entryBase)
            => Math.Min(SeedRolls(cfg, ownTier, triune), Math.Max(0, count - DripEntry(cfg, ownTier, entryBase)));

        /// <summary>The rolls this player has taken on each school this tier (0 for a tier they have not earned in yet).</summary>
        public static long[] RollsTaken(Player player, int tier)
        {
            var current = (player.GetProperty(PropertyInt64.AugRollTier) ?? 0) == tier;
            return DripSchools.Select(s => current ? player.GetProperty(s.RollsProperty) ?? 0 : 0).ToArray();
        }

        /// <summary>True when every school has used this tier's rolls.</summary>
        public static bool RollsDone(Player player, TriuneTierRow row) => row == null || RollsTaken(player, row.Tier).All(r => r >= row.Points);

        /// <summary>
        /// Moves the roll counts to <paramref name="tier"/> when they belong to another tier (a new own tier starts with every
        /// roll unused). Returns true when it changed anything (the caller pushes the new state to the client).
        /// The part-way reward starts fresh only when the roll tier really moves between two tiers: a character with no roll
        /// tier yet keeps it (the Triune move carries one there before any tier is known - a kill, the hunting numbers or the
        /// move's own seed setting the first tier must not wipe it; /testchar clears both together).
        /// </summary>
        public static bool EnsureRollTier(Player player, int tier)
        {
            var prior = player.GetProperty(PropertyInt64.AugRollTier) ?? 0;
            if (prior == tier)
                return false;
            player.SetProperty(PropertyInt64.AugRollTier, tier);
            foreach (var s in DripSchools)
                player.RemoveProperty(s.RollsProperty);
            if (prior != 0)
                player.RemoveProperty(PropertyFloat.AugDripFraction);
            return true;
        }

        /// <summary>
        /// The count a luminance gem is PRICED from. DECIDED 11-08 (price floor), kept 11-22: Creature / Item / Life while the
        /// T11-T14 rewards are on and the player is T11-T14 = max(count, the own tier's entry on the ladder) - no cheap gems
        /// behind the tier. Everyone else: the count as it is. (The 11-16 rule 2 for drip-fed schools is gone: since
        /// 2026-11-22 every earned aug sits on the one count, so the price already reads it.)
        /// </summary>
        public static long GemPriceCount(Player player, string school, long used)
        {
            if (player == null)
                return used;
            var cfg = Current;
            var own = TierRewardScale.OwnTier(player);
            if (!cfg.DripEnabled || !IsDripTier(own))
                return used;
            long baseValue = school switch
            {
                "Creature" => cfg.DripBaseCreature,
                "Item" or "Life" => cfg.DripBaseItem,
                _ => -1,
            };
            if (baseValue < 0)
                return used;
            return GemPriceFrom(used, DripEntry(cfg, own, baseValue));
        }

        /// <summary>The price floor on plain numbers (tested).</summary>
        public static long GemPriceFrom(long used, long entry) => PriceFloor(used, entry);

        /// <summary>max(used, floor) - pure, tested.</summary>
        public static long PriceFloor(long used, long floor) => Math.Max(used, floor);

        /// <summary>
        /// Is <paramref name="weekStart"/> a new week for a count stored under <paramref name="storedStart"/>? Only when nothing is
        /// stored, or at least a full week has passed since it (167 h - the week the clocks spring forward is an hour short). So
        /// no week is ever shorter than that: an admin moving the reset day / hour / zone never hands out a second band - the
        /// running count simply carries on to the first new boundary a full week after the stored one. A stored start a full week
        /// or more in the FUTURE (a server clock that ran fast, a hand edit) is no real week either - the count starts again rather
        /// than freezing until that date.
        /// </summary>
        public static bool IsNewWeek(long storedStart, long weekStart)
        {
            const long fullWeek = (long)((HoursPerWeek - 1) * 3600);
            return storedStart <= 0 || weekStart - storedStart >= fullWeek || storedStart - weekStart >= fullWeek;
        }

        /// <summary>This week's own-tier kills for a player, 0 when their stored count belongs to an earlier week.</summary>
        public static double WeekKills(Player player, Config cfg)
        {
            var weekStart = ToUnix(WeekStartUtc(cfg, DateTime.UtcNow));
            return IsNewWeek(player.GetProperty(PropertyInt64.TriuneWeekStart) ?? 0, weekStart) ? 0 : player.GetProperty(PropertyFloat.TriuneWeekKills) ?? 0;
        }

        /// <summary>What the next kill is worth when <paramref name="killsBefore"/> own-tier kills are already counted this week.</summary>
        public static double KillValue(TriuneTierRow r, double killsBefore)
        {
            if (r == null) return 0.0;
            killsBefore = Math.Max(0, killsBefore);
            var b1 = Band1Kills(r);
            return Rate1(r) * Overlap(killsBefore, 0, b1) + Rate2(r) * Overlap(killsBefore, b1, b1 + Band2Kills(r));
        }

        /// <summary>
        /// The rewards the next kill is worth (owner 2026-11-21, random roll): each reward adds 1 to ONE school, so a tier holds
        /// 3 x Points rewards and the pace stays what it was when a point added 1 to all three - KillValue x 3. T15 at the defaults:
        /// 1 reward per ~134 kills in the first band, ~269 in the second.
        /// </summary>
        public static double RewardValue(TriuneTierRow r, double killsBefore) => KillValue(r, killsBefore) * DripSchools.Length;

        /// <summary>The most kills per reward ever shown: a band that pays, but slower than this, shows this (never 0 = "no rewards").</summary>
        public const long MaxKillsPerReward = 1_000_000_000_000;

        /// <summary>Kills per reward in each band (rounded; 0 when the band pays nothing).</summary>
        public static long KillsPerReward(double pointRate)
        {
            if (!(pointRate > 0))
                return 0;
            // bounded BEFORE the cast: a tiny rate (two almost equal week values) would not fit in a long
            var k = Math.Round(1.0 / (pointRate * DripSchools.Length));
            return k >= MaxKillsPerReward ? MaxKillsPerReward : Math.Max(1, (long)k);   // a paying band is never "0" (= no rewards)
        }

        /// <summary>How much of the kill [k, k + 1) falls inside the band [from, to) - 1 inside, 0 outside, the share at an edge.</summary>
        private static double Overlap(double k, double from, double to) => Math.Max(0, Math.Min(k + 1, to) - Math.Max(k, from));

        /// <summary>Points summed over T15..tier (the old Triune ladder: T15 500 ... T25 5,500). Still the T15+ school gem room and
        /// the T15 roll seed.</summary>
        public static long TierCap(Config cfg, int tier)
        {
            long sum = 0;
            foreach (var r in cfg.Tiers)
                if (r.Tier >= MinTier && r.Tier <= tier)
                    sum += r.Points;
            return sum;
        }

        /// <summary>
        /// The UTC moment the current week began: the last <paramref name="day"/> at <paramref name="hour"/>:00 local time
        /// in <paramref name="tz"/> at or before <paramref name="utcNow"/>. Daylight saving moves the UTC hour, never the local one.
        /// </summary>
        public static DateTime WeekStartUtc(DateTime utcNow, DayOfWeek day, int hour, TimeZoneInfo tz)
        {
            tz ??= TimeZoneInfo.Utc;
            var utc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
            var local = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
            var date = local.Date.AddDays(-(((int)local.DayOfWeek - (int)day + 7) % 7));
            // compared as UTC moments, never as local wall times: inside a fall-back overlap (or a 30 / 45 minute shift) the wall
            // clock can read "before the reset" after the reset has already happened
            var startUtc = BoundaryUtc(date, hour, tz);
            return startUtc > utc ? BoundaryUtc(date.AddDays(-7), hour, tz) : startUtc;
        }

        /// <summary>
        /// The UTC moment of <paramref name="hour"/>:00 local on <paramref name="date"/>. In a spring-forward gap: the first real
        /// moment after it (15-minute steps, for 30 / 45 minute zones). In a fall-back overlap (it happens twice): the FIRST one.
        /// </summary>
        private static DateTime BoundaryUtc(DateTime date, int hour, TimeZoneInfo tz)
        {
            var start = DateTime.SpecifyKind(date.Date.AddHours(hour), DateTimeKind.Unspecified);
            for (var guard = 0; guard < 16 && tz.IsInvalidTime(start); guard++)
                start = start.AddMinutes(15);
            return tz.IsAmbiguousTime(start)
                ? DateTime.SpecifyKind(start - tz.GetAmbiguousTimeOffsets(start).Max(), DateTimeKind.Utc)
                : TimeZoneInfo.ConvertTimeToUtc(start, tz);
        }

        public static DateTime WeekStartUtc(Config cfg, DateTime utcNow) => WeekStartUtc(utcNow, (DayOfWeek)cfg.ResetDay, cfg.ResetHour, ResetZone(cfg));

        /// <summary>The UTC moment the next week begins. Not WeekStartUtc + 7 days: across a daylight saving change that is an
        /// hour off the local reset time. A moment 8 days after this week's start always lies in the next week.</summary>
        public static DateTime NextWeekStartUtc(DateTime utcNow, DayOfWeek day, int hour, TimeZoneInfo tz)
            => WeekStartUtc(WeekStartUtc(utcNow, day, hour, tz).AddDays(8), day, hour, tz);

        public static DateTime NextWeekStartUtc(Config cfg, DateTime utcNow) => NextWeekStartUtc(utcNow, (DayOfWeek)cfg.ResetDay, cfg.ResetHour, ResetZone(cfg));

        public static long ToUnix(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();

        // ---- the kill ---------------------------------------------------------------------------------------------------

        /// <summary>Is this tier's hunting reward switched on? T11-T14: the drip switch; T15-T25: the Triune earning switch.</summary>
        public static bool RewardsOn(Config cfg, int tier) => IsDripTier(tier) ? cfg.DripEnabled : cfg.Enabled && tier >= MinTier && tier <= MaxTier;

        /// <summary>Does a kill at <paramref name="tier"/> count toward the week? Wherever the tier's rewards or the school gems are
        /// on: the count paces both (since 2026-11-24 the gems alone also count T11-T14 kills, so they slow down there too).</summary>
        public static bool CountsKills(Config cfg, int tier) => RewardsOn(cfg, tier) || SchoolGems.GemsDropAt(cfg, tier);

        /// <summary>Called once per creature death (Creature.OnDeath), on the victim's thread.</summary>
        public static void OnCreatureKilled(Creature victim)
        {
            try
            {
                if (victim == null || victim is Player || victim is Pet)
                    return;

                var cfg = Current;
                if (!cfg.Enabled && !cfg.GemsEnabled && !cfg.DripEnabled)   // the weekly count also paces the school gems
                    return;

                var killTier = TierRewardScale.KillTier(victim);
                if (killTier < DripMinTier)
                    return;
                // the week's count runs where the tier's rewards or the school gems are on (T11-T14: the drip or the gems; the
                // drip alone switches the Creature / Item / Life rewards there - Credit checks RewardsOn before granting any)
                if (!CountsKills(cfg, killTier))
                    return;

                var row = cfg.Tiers.FirstOrDefault(r => r.Tier == killTier);
                if (row == null || row.Points <= 0)
                    return;

                // only kills that pay kill XP or luminance (the bounty / coin rule): a summoned add or a no-XP spawn earns nothing
                if (!BountyManager.PaysBounty(victim))
                    return;

                // the ONLINE player, never the object the damage history remembers; mules and Olthoi earn nothing (as coins)
                var attacker = victim.DamageHistory.TopDamager?.TryGetPetOwnerOrAttacker() as Player;
                var player = attacker != null ? PlayerManager.GetOnlinePlayer(attacker.Guid) : null;
                if (player == null || !ReferenceEquals(player, attacker) || player.IsLoggingOut || player.IsMule || player.IsOlthoiPlayer)
                    return;

                // the gem roll (SchoolGems.AddToCorpse, after this on the same death) must read the week count BEFORE this kill:
                // the credit below may run at once (one tick group) or later (another), so the count is taken here, first
                // (CodeRabbit #590: the roll read kills + 1, a band edge late and 1 instead of 0 right after the reset)
                victim.TriuneWeekKillsAtDeath = WeekKills(player, cfg);
                LandblockManager.RunOnThreadFor(player, ActionType.TriuneEarning_Credit, () => Credit(player, killTier, cfg, row));
            }
            catch (Exception ex)
            {
                log.Error($"[TriuneEarning] OnCreatureKilled: {ex}");
            }
        }

        /// <summary>The player-thread half: own tier, the week, the band, the fraction, the rewards.</summary>
        private static void Credit(Player player, int killTier, Config cfg, TriuneTierRow row)
        {
            try
            {
                if (player.IsLoggingOut || !ReferenceEquals(PlayerManager.GetOnlinePlayer(player.Guid), player))
                    return;
                if (AugMove.AwaitsMove(player))
                    return;   // old counters not moved yet: nothing earned on them (the move adds them later)

                // the tier you are in: one tier below earns nothing
                if (TierRewardScale.OwnTier(player) != killTier)
                    return;

                // the week: a kill in a newer week starts the count again. The count goes on after a tier's rewards are all
                // taken - it also paces the school gems.
                var weekStart = ToUnix(WeekStartUtc(cfg, DateTime.UtcNow));
                var kills = player.GetProperty(PropertyFloat.TriuneWeekKills) ?? 0;
                var full = false;
                if (IsNewWeek(player.GetProperty(PropertyInt64.TriuneWeekStart) ?? 0, weekStart))
                {
                    player.SetProperty(PropertyInt64.TriuneWeekStart, weekStart);
                    kills = 0;
                    full = true;   // a new week: the reset time moves on
                }

                var value = RewardValue(row, kills);
                var b1 = Band1Kills(row);
                var b2 = b1 + Band2Kills(row);
                var after = kills + 1;
                player.SetProperty(PropertyFloat.TriuneWeekKills, after);

                var on = RewardsOn(cfg, killTier);
                // a character moved while no own tier was known gets its roll seed before its first roll here (once)
                if (on && AugMove.SeedPending(player))
                    full = true;
                if (on && EnsureRollTier(player, killTier))
                    full = true;
                if (on && !RollsDone(player, row))
                    GrantRewards(player, cfg, killTier, row, value);
                var canStillEarn = on && !RollsDone(player, row);

                PushHuntState(player, full);

                // the band line goes out after this kill's reward, so it reads in the order it happened. It covers the rewards
                // and the school gems alike (all ride the same weekly count) - and only a player who can still earn one of them
                // (read again: this kill may have just used the tier's last roll)
                var gemsLeft = SchoolGems.CanEarnAny(player, cfg, killTier);
                if (!canStillEarn && !gemsLeft)
                    return;
                var stored = player.GetProperty(PropertyInt64.TriuneWeekStart) ?? 0;
                // the end first: a kill that crosses both lines (an empty slow band) says the rewards stopped. Only the Creature /
                // Item / Life rewards stop there - the school gems keep their reduced chance (2026-11-24) - so the line is for a
                // player with rewards still to earn, and says so when gems keep dropping; a gems-only player hears nothing there
                if (canStillEarn && kills < b2 && after >= b2)
                    player.SendMessage(gemsLeft
                        ? $"Your kills here earn no more Creature, Item or Life augmentations until the week turns ({NextResetText(cfg, stored)}). Attack school gems keep dropping."
                        : $"Your kills here earn no augmentations until the week turns ({NextResetText(cfg, stored)}).", ChatMessageType.Broadcast);
                // the slower pace: the rewards, or a school gem within 500 of the tier's ceiling - a school still catching up keeps
                // its full chance all week (2026-11-24), so a player with nothing else to slow down hears nothing here
                else if (kills < b1 && after >= b1 && (canStillEarn || SchoolGems.AnySlowsDown(player, cfg, killTier)))
                    player.SendMessage($"Your hunting now earns augmentations at a slower pace until the week turns ({NextResetText(cfg, stored)}).", ChatMessageType.Broadcast);
            }
            catch (Exception ex)
            {
                log.Error($"[TriuneEarning] Credit for {player?.Name}: {ex}");
            }
        }

        /// <summary>
        /// Adds one kill's value to the carried fraction; each whole reward rolls ONE school with rolls left this tier (owner
        /// 2026-11-21). Below the tier's end it adds +1 straight onto that school's LumAug count (one stat, owner 2026-11-22) with
        /// one chat line; at the end the roll is lost, silently, and still counts. The fraction is dropped once no roll is left.
        /// </summary>
        private static void GrantRewards(Player player, Config cfg, int tier, TriuneTierRow row, double value)
        {
            if (!(value > 0))
                return;

            var (whole, fraction) = TierRewardScale.AddCoins(player.GetProperty(PropertyFloat.AugDripFraction) ?? 0, value);
            // one kill never grants more than this: whole rewards past it are dropped (not carried), so an extreme pace setting
            // can never loop thousands of times and flood the client in one kill. The part-way fraction carries as always.
            whole = Math.Min(whole, MaxRewardsPerKill);
            if (fraction > 0)
                player.SetProperty(PropertyFloat.AugDripFraction, fraction);
            else
                player.RemoveProperty(PropertyFloat.AugDripFraction);

            for (var n = 0; n < whole; n++)
            {
                var rolls = RollsTaken(player, tier);
                var i = PickSchool(rolls, row.Points, ThreadSafeRandom.Next(0.0f, 1.0f));
                if (i < 0)
                {
                    player.RemoveProperty(PropertyFloat.AugDripFraction);
                    break;
                }

                var s = DripSchools[i];
                var taken = rolls[i] + 1;
                player.SetProperty(s.RollsProperty, taken);
                player.Session?.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, s.RollsProperty, taken));

                var count = s.Count(player);
                if (!RollAdds(count, DripCeiling(cfg, tier, s)))
                    continue;   // lost on a full school: no line (owner 2026-11-21)

                // the stored count + 1: Count is the Effective count, which also adds a retired counter still held - writing that
                // back would fold the held counter in again at every reward
                var newCount = (player.GetProperty(s.AugProperty) ?? 0) + 1;
                player.SetProperty(s.AugProperty, newCount);
                player.Session?.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, s.AugProperty, newCount));
                player.SendMessage($"Your {s.Name} augmentations grow to {newCount:N0}.", ChatMessageType.Broadcast);
            }

            if (RollsDone(player, row))
                player.RemoveProperty(PropertyFloat.AugDripFraction);
        }

        /// <summary>The most whole rewards one kill grants (see GrantRewards; the default paces give one per ~134 kills).</summary>
        public const int MaxRewardsPerKill = 100;

        // ---- the client copy (a plugin reads these the way it reads banked luminance) ------------------------------------

        private static readonly PropertyInt64[] HuntProps =
        {
            PropertyInt64.AugRollTier, PropertyInt64.AugRollsCreature, PropertyInt64.AugRollsItem, PropertyInt64.AugRollsLife,
            PropertyInt64.AugHuntWeekKills, PropertyInt64.AugHuntWeekResetAt, PropertyInt64.AugHuntBand1Kills,
            PropertyInt64.AugHuntBand2Kills, PropertyInt64.AugHuntKillsPerReward1, PropertyInt64.AugHuntKillsPerReward2,
            PropertyInt64.AugHuntRollsPerSchool,
        };

        /// <summary>The per-school roll counts, in DripSchools order.</summary>
        private static readonly PropertyInt64[] RollProps = DripSchools.Select(s => s.RollsProperty).ToArray();

        /// <summary>
        /// Writes the "Hunting rewards" numbers onto the player and sends them to their client (owner 2026-11-22: a plugin can
        /// show the block live, like banked luminance). <paramref name="full"/> = every number (login, a new week, a new tier);
        /// otherwise only the kill count, which is all a plain kill changes (a reward sends its own roll count). The roll
        /// counts are moved to the own tier here too, so a plugin never shows another tier's rolls. <paramref name="send"/>
        /// false = only write them (at login, where the description that follows carries them - they are [SendOnLogin]).
        /// </summary>
        public static void PushHuntState(Player player, bool full, bool send = true)
        {
            if (player == null)
                return;
            // the revision BEFORE the settings: an edit publishes the settings first, then bumps the revision - read this way a
            // change between the two reads only costs one extra full update, never a stale one marked as sent
            var rev = Volatile.Read(ref _settingsRev);
            var cfg = Current;
            var own = TierRewardScale.OwnTier(player);
            // below T11 and never shown any: nothing to write (no rows of zeros on every retail character, every login)
            if (own < FirstTier && !player.GetProperty(PropertyInt64.AugHuntWeekKills).HasValue)
                return;
            var row = own >= FirstTier ? cfg.Tiers.FirstOrDefault(r => r.Tier == own) : null;
            // paused (old counters waiting for the move) shows as off: kills grant nothing then
            var on = row != null && row.Points > 0 && RewardsOn(cfg, own) && !AugMove.AwaitsMove(player);

            // the roll seed first (a character moved with no own tier known): setting the roll tier first would drop its part-way reward
            if (on && AugMove.SeedPending(player))
                full = true;
            if (on && EnsureRollTier(player, own))
                full = true;
            // the settings changed since this client last got every number: send them all again
            var sentRev = SentRev.GetValue(player, _ => new StrongBox<int>(rev - 1));
            if (sentRev.Value != rev)
                full = true;
            if (full)
                sentRev.Value = rev;

            var kills = (long)Math.Floor(WeekKills(player, cfg));
            var values = new Dictionary<PropertyInt64, long> { [PropertyInt64.AugHuntWeekKills] = kills };
            if (full)
            {
                var stored = player.GetProperty(PropertyInt64.TriuneWeekStart) ?? 0;
                values[PropertyInt64.AugHuntWeekResetAt] = ToUnix(NextResetUtc(cfg, kills > 0 ? stored : 0, DateTime.UtcNow));
                values[PropertyInt64.AugHuntBand1Kills] = on ? (long)Math.Round(Band1Kills(row)) : 0;
                values[PropertyInt64.AugHuntBand2Kills] = on ? (long)Math.Round(Band1Kills(row) + Band2Kills(row)) : 0;
                values[PropertyInt64.AugHuntKillsPerReward1] = on ? KillsPerReward(Rate1(row)) : 0;
                values[PropertyInt64.AugHuntKillsPerReward2] = on ? KillsPerReward(Rate2(row)) : 0;
                values[PropertyInt64.AugHuntRollsPerSchool] = on ? row.Points : 0;
                values[PropertyInt64.AugRollTier] = player.GetProperty(PropertyInt64.AugRollTier) ?? 0;
                foreach (var s in DripSchools)
                    values[s.RollsProperty] = player.GetProperty(s.RollsProperty) ?? 0;
            }

            foreach (var (prop, v) in values)
            {
                // the roll counts and the roll tier are the real bookkeeping - written where they change; the rest is a copy
                if (prop != PropertyInt64.AugRollTier && Array.IndexOf(RollProps, prop) < 0)
                    player.SetProperty(prop, v);
                if (send)
                    player.Session?.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, prop, v));
            }
        }

        // ---- text -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The next moment this player's count really starts again: the next boundary that is a new week for their stored start
        /// (after an admin moves the reset, the first new boundary can be too soon - IsNewWeek - and the one after it is theirs).
        /// <paramref name="storedStart"/> 0 = the server's next boundary.
        /// </summary>
        public static DateTime NextResetUtc(Config cfg, long storedStart, DateTime utcNow)
        {
            var next = NextWeekStartUtc(cfg, utcNow);
            for (var i = 0; i < 3 && storedStart > 0 && !IsNewWeek(storedStart, ToUnix(next)); i++)
                next = NextWeekStartUtc(cfg, next);
            return next;
        }

        /// <summary>"Eastern" for "Eastern Standard Time", "UTC" for UTC - the zone as a player reads it.</summary>
        public static string ZoneShortName(string id)
        {
            id = id?.Trim() ?? "UTC";
            foreach (var tail in new[] { " Standard Time", " Daylight Time" })
                if (id.EndsWith(tail, StringComparison.OrdinalIgnoreCase))
                    return id.Substring(0, id.Length - tail.Length);
            return id;
        }

        /// <summary>"2 days 4 hours", "1 day", "3 hours 12 minutes", "8 minutes". Pure, tested.</summary>
        public static string LeftText(TimeSpan left)
        {
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;
            static string Part(int n, string unit) => $"{n} {unit}{(n == 1 ? "" : "s")}";
            if (left.TotalDays >= 1)
                return left.Hours > 0 ? $"{Part((int)left.TotalDays, "day")} {Part(left.Hours, "hour")}" : Part((int)left.TotalDays, "day");
            if (left.TotalHours >= 1)
                return left.Minutes > 0 ? $"{Part((int)left.TotalHours, "hour")} {Part(left.Minutes, "minute")}" : Part((int)left.TotalHours, "hour");
            return Part(Math.Max(1, (int)Math.Ceiling(left.TotalMinutes)), "minute");
        }

        /// <summary>"Friday 12:00 Eastern, in 2 days 4 hours" - the next reset in the configured zone (for this player's stored
        /// week, if given).</summary>
        public static string NextResetText(Config cfg, long storedStart = 0)
        {
            var (when, left) = NextResetParts(cfg, storedStart);
            return $"{when}, in {left}";
        }

        private static (string When, string Left) NextResetParts(Config cfg, long storedStart)
        {
            var now = DateTime.UtcNow;
            var next = NextResetUtc(cfg, storedStart, now);
            var local = TimeZoneInfo.ConvertTimeFromUtc(next, ResetZone(cfg));
            // the zone the week really turns in (an unknown id falls back to UTC - show UTC, not the bad id)
            return ($"{local.ToString("dddd H:mm", CultureInfo.InvariantCulture)} {ZoneShortName(ResetZone(cfg).Id)}", LeftText(next - now));
        }

        /// <summary>"1 reward per 134 kills", or "no rewards" for a band that pays nothing (never "1 reward per 0 kills").</summary>
        public static string RewardPace(double pointRate)
        {
            var k = KillsPerReward(pointRate);
            return k > 0 ? $"1 reward per {k:N0} kills" : "no rewards";
        }

        /// <summary>One kill per point, rounded: "403".</summary>
        public static string KillsPerPoint(double rate) => rate > 0 ? Math.Round(1.0 / rate).ToString("N0", CultureInfo.InvariantCulture) : "-";

        /// <summary>"Creature 6,312 of 6,500", or "Creature 6,500 (full)" once nothing more can be earned there this tier
        /// (a school bought to the luminance cap can already sit past where the tier ends).</summary>
        internal static string SoFar(string school, long count, long end) => count >= end ? $"{school} {count:N0} (full)" : $"{school} {count:N0} of {end:N0}";

        /// <summary>
        /// The "Hunting rewards this week" block of /aug (owner-trimmed 2026-11-22). Example at T15:
        ///   Hunting rewards this week:
        ///     Each reward adds 1 to Creature, Item or Life at random. Attack school gems drop on their own.
        ///     Kills this week: 12,400
        ///       First 33,600 kills:    1 reward per 134 kills   &lt;- you are here (21,200 kills left)
        ///       After 33,600 kills:    1 reward per 269 kills
        ///       Beyond 67,200 kills:   no rewards until the week resets
        ///       School gems, each school from 2,000 up to 4,500: 1 per 269 kills.
        ///       From 4,000: 1 per 448 after 33,600 kills this week. They never stop for the week.
        ///     Week resets Friday 12:00 Eastern (in 2 days 4 hours).
        ///     This tier so far: Creature 6,312 of 6,500, Item 4,298 of 4,500, Life 4,305 of 4,500.
        /// </summary>
        public static List<string> StatusLines(Player player)
        {
            var cfg = Current;
            var lines = new List<string>();
            var own = TierRewardScale.OwnTier(player);
            var row = own >= FirstTier ? cfg.Tiers.FirstOrDefault(r => r.Tier == own) : null;
            var on = row != null && row.Points > 0 && RewardsOn(cfg, own);
            var gems = row != null && row.Points > 0 && SchoolGems.GemsDropAt(cfg, own);

            if (own < FirstTier)
            {
                lines.Add("Hunting rewards: they start once you can fight in the T11 lands.");
                return lines;
            }
            if (!on && !gems)
            {
                lines.Add("Hunting rewards: none in these lands right now.");
                return lines;
            }
            if (AugMove.AwaitsMove(player))
            {
                lines.Add("Hunting rewards: paused for now - your augmentations are being brought up to date.");
                return lines;
            }

            lines.Add("Hunting rewards this week:");
            lines.Add(on && gems ? "  Each reward adds 1 to Creature, Item or Life at random. Attack school gems drop on their own."
                : on ? "  Each reward adds 1 to Creature, Item or Life at random."
                : "  Attack school gems drop from your kills here.");

            var kills = WeekKills(player, cfg);
            var b1 = Band1Kills(row);
            var b2 = b1 + Band2Kills(row);
            lines.Add($"  Kills this week: {kills:N0}");
            if (on)
            {
                lines.Add($"    First {b1:N0} kills:    {RewardPace(Rate1(row))}{Here(kills < b1, b1 - kills)}");
                lines.Add($"    After {b1:N0} kills:    {RewardPace(Rate2(row))}{Here(kills >= b1 && kills < b2, b2 - kills)}");
                lines.Add($"    Beyond {b2:N0} kills:   no rewards until the week resets{Here(kills >= b2, 0)}");
            }
            // the school gems (2026-11-24): from the floor up to this tier's cap, the full chance all week while a school catches
            // up, the weekly slowdown only within 500 of the cap, and never a stop - a school's gems end only at the cap
            if (gems)
            {
                var gemCeiling = GemCeiling(cfg, own);
                var slowFrom = Math.Max(GemFloor(cfg, own), gemCeiling - GemSlowdownWindow);
                lines.Add($"    School gems, each school from {GemFloor(cfg, own):N0} up to {gemCeiling:N0}: 1 per {KillsPerPoint(GemChance1(row))} kills.");
                lines.Add($"    From {slowFrom:N0}: 1 per {KillsPerPoint(GemChanceReduced(row))} after {b1:N0} kills this week. They never stop for the week.");
            }
            var storedStart = player.GetProperty(PropertyInt64.TriuneWeekStart) ?? 0;
            var (when, left) = NextResetParts(cfg, kills > 0 ? storedStart : 0);
            lines.Add($"  Week resets {when} (in {left}).");

            // each school's count against where this tier ends (owner 2026-11-22): always true, also after the own tier changed
            // and came back (the roll counts restart then, so "312 of 500 rolls" would read as 0 of 500 with only 188 to go)
            if (on)
                lines.Add($"  This tier so far: {string.Join(", ", DripSchools.Select(s => SoFar(s.Name, s.Count(player), DripCeiling(cfg, own, s))))}."
                    + (RollsDone(player, row) ? " This tier's rewards are all used." : ""));

            return lines;
        }

        private static string Here(bool here, double left) => !here ? "" : left > 0 ? $"   <- you are here ({left:N0} kills left)" : "   <- you are here";
    }
}
