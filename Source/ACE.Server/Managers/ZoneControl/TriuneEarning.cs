using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using log4net;
using Newtonsoft.Json;

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
        /// <summary>Triune earned in this tier (the next tier's hit gate asks this much more). Also each school gem's room in
        /// this tier: a school capped at the base reaches base + the summed Points of T15..this tier.</summary>
        public int Points { get; set; } = 500;
        /// <summary>School gems (owner 2026-11-01): weeks for one school to gain Points at floor pace / at ceiling pace.</summary>
        public double GemSlowWeeks { get; set; } = 4;
        public double GemFastWeeks { get; set; } = 2.5;
    }

    /// <summary>
    /// Triune earning (owner 2026-11-01, locked): Triune Weave straight onto the character, a fraction per kill, the way
    /// Prestige Coins are banked (TierRewards). Rules:
    ///   - a T15+ Zone Control kill that pays XP or lum, credited to the TOP DAMAGER (a pet's kill counts for its owner);
    ///   - only at the player's OWN tier (the tier they are in) - one tier below earns nothing, no dip;
    ///   - per tier, two weekly bands: the first FloorKph x 168 kills pay Points / SlowWeeks between them, the next
    ///     (CeilingKph - FloorKph) x 168 kills pay Points / FastWeeks - Points / SlowWeeks, kills past that pay nothing;
    ///   - the week turns at a fixed local time (default Friday 12:00 US Eastern), checked lazily on the next kill;
    ///   - earning at a tier stops at the sum of Points for every tier from T15 up to it (T15 = 500, T16 = 1,000 ...);
    ///   - the fraction is carried (PropertyFloat.TriuneFraction); every whole point lands on TriuneWeaveCount at once.
    /// Settings live in the shard config store (zc_triune_earning, JSON) and are edited with /zonecontrol triune. Off by default.
    /// The victim's thread reads the kill; the player's own thread reads and writes everything about the player.
    /// </summary>
    public static class TriuneEarning
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public const int MinTier = 15;
        public const int MaxTier = ZoneRare.MaxTier;
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
            /// <summary>School gem drops (SchoolGems.cs) - their own switch; they share the weekly kill count with Triune.</summary>
            public bool GemsEnabled { get; set; }
            /// <summary>The bought (luminance) school aug a player needs before that school's gems drop or work.</summary>
            public int GemBaseCap { get; set; } = 4000;
            public List<TriuneTierRow> Tiers { get; set; } = new();
        }

        // limits for every number a command or a hand-edited store can set (review 2026-11-01: finite and bounded, never NaN /
        // Infinity / 1e308): kills an hour, weeks, points per tier, the bought school base
        public const double MinKph = 0.01, MaxKph = 100_000, MinWeeks = 0.1, MaxWeeks = 520;
        public const int MaxPoints = 100_000, MaxGemBaseCap = 1_000_000;

        private static readonly object _lock = new();
        private static Config _current;
        /// <summary>True while the stored JSON could not be read: the running settings stay, and no edit may save over the store.</summary>
        private static bool _loadFailed;
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
                        try { _current = Load(); _loadFailed = false; _firstLoadFailed = false; }
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

        /// <summary>Every tier T15..T25 present once, in order, with sane numbers.</summary>
        public static Config Normalize(Config cfg)
        {
            cfg ??= new Config();
            var rows = (cfg.Tiers ?? new List<TriuneTierRow>()).Where(r => r != null && r.Tier >= MinTier && r.Tier <= MaxTier)
                .GroupBy(r => r.Tier).ToDictionary(g => g.Key, g => g.First());
            cfg.Tiers = new List<TriuneTierRow>();
            for (var t = MinTier; t <= MaxTier; t++)
            {
                var r = rows.TryGetValue(t, out var have) ? have : new TriuneTierRow { Tier = t };
                r.Tier = t;
                r.FloorKph = InRange(r.FloorKph, MinKph, MaxKph, 200);
                r.CeilingKph = InRange(r.CeilingKph, MinKph, MaxKph, Math.Min(MaxKph, r.FloorKph * 2));
                if (!(r.CeilingKph > r.FloorKph)) r.CeilingKph = Math.Min(MaxKph, r.FloorKph * 2);
                r.SlowWeeks = InRange(r.SlowWeeks, MinWeeks, MaxWeeks, 6);
                r.FastWeeks = InRange(r.FastWeeks, MinWeeks, MaxWeeks, Math.Min(4, r.SlowWeeks));
                if (r.FastWeeks > r.SlowWeeks) r.FastWeeks = Math.Min(4, r.SlowWeeks);
                r.Points = Math.Clamp(r.Points, 0, MaxPoints);
                r.GemSlowWeeks = InRange(r.GemSlowWeeks, MinWeeks, MaxWeeks, 4);
                r.GemFastWeeks = InRange(r.GemFastWeeks, MinWeeks, MaxWeeks, Math.Min(2.5, r.GemSlowWeeks));
                if (r.GemFastWeeks > r.GemSlowWeeks) r.GemFastWeeks = Math.Min(2.5, r.GemSlowWeeks);
                cfg.Tiers.Add(r);
            }
            cfg.ResetDay = ((cfg.ResetDay % 7) + 7) % 7;
            cfg.ResetHour = Math.Clamp(cfg.ResetHour, 0, 23);
            if (string.IsNullOrWhiteSpace(cfg.ResetTimeZone)) cfg.ResetTimeZone = "UTC";
            cfg.GemBaseCap = Math.Clamp(cfg.GemBaseCap, 0, MaxGemBaseCap);
            return cfg;
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
                var jsonOut = JsonConvert.SerializeObject(clone);
                if (DatabaseManager.ShardConfig.StringExists(StoreKey))
                    DatabaseManager.ShardConfig.SaveString(new ConfigPropertiesString { Key = StoreKey, Value = jsonOut, Description = "Triune earning per tier (JSON)" });
                else
                    DatabaseManager.ShardConfig.AddString(StoreKey, jsonOut, "Triune earning per tier (JSON)");
                _current = clone;
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

        /// <summary>Triune per kill in the first band: a week of floor-pace kills pays Points / SlowWeeks.</summary>
        public static double Rate1(TriuneTierRow r)
        {
            var b = Band1Kills(r);
            return b > 0 && r.SlowWeeks > 0 ? r.Points / r.SlowWeeks / b : 0.0;
        }

        /// <summary>Triune per kill in the second band: a week of ceiling-pace kills pays Points / FastWeeks in all.</summary>
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

        /// <summary>One school's gem chance on the next kill when <paramref name="killsBefore"/> kills are already counted this week.</summary>
        public static double GemChance(TriuneTierRow r, double killsBefore)
        {
            if (r == null) return 0.0;
            killsBefore = Math.Max(0, killsBefore);
            var b1 = Band1Kills(r);
            return Math.Min(1.0, GemChance1(r) * Overlap(killsBefore, 0, b1) + GemChance2(r) * Overlap(killsBefore, b1, b1 + Band2Kills(r)));
        }

        /// <summary>The school total (bought + charm) a gem earned at <paramref name="tier"/> can raise a school to.</summary>
        public static long GemCeiling(Config cfg, int tier) => cfg.GemBaseCap + TierCap(cfg, tier);

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

        /// <summary>How much of the kill [k, k + 1) falls inside the band [from, to) - 1 inside, 0 outside, the share at an edge.</summary>
        private static double Overlap(double k, double from, double to) => Math.Max(0, Math.Min(k + 1, to) - Math.Max(k, from));

        /// <summary>Triune at which earning at <paramref name="tier"/> stops: Points summed over T15..tier.</summary>
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

        /// <summary>Called once per creature death (Creature.OnDeath), on the victim's thread.</summary>
        public static void OnCreatureKilled(Creature victim)
        {
            try
            {
                if (victim == null || victim is Player || victim is Pet)
                    return;

                var cfg = Current;
                if (!cfg.Enabled && !cfg.GemsEnabled)   // the weekly count also paces the school gems
                    return;

                var killTier = TierRewardScale.KillTier(victim);
                if (killTier < MinTier)
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

        /// <summary>The player-thread half: own tier, the week, the band, the fraction, the whole points.</summary>
        private static void Credit(Player player, int killTier, Config cfg, TriuneTierRow row)
        {
            try
            {
                if (player.IsLoggingOut || !ReferenceEquals(PlayerManager.GetOnlinePlayer(player.Guid), player))
                    return;

                // the tier you are in: one tier below earns nothing
                if (TierRewardScale.OwnTier(player) != killTier)
                    return;

                // the week: a kill in a newer week starts the count again. The count goes on after Triune is complete for the
                // tier - it also paces the school gems.
                var weekStart = ToUnix(WeekStartUtc(cfg, DateTime.UtcNow));
                var kills = player.GetProperty(PropertyFloat.TriuneWeekKills) ?? 0;
                if (IsNewWeek(player.GetProperty(PropertyInt64.TriuneWeekStart) ?? 0, weekStart))
                {
                    player.SetProperty(PropertyInt64.TriuneWeekStart, weekStart);
                    kills = 0;
                }

                var value = KillValue(row, kills);
                var b1 = Band1Kills(row);
                var b2 = b1 + Band2Kills(row);
                var after = kills + 1;
                player.SetProperty(PropertyFloat.TriuneWeekKills, after);

                var triune = player.GetProperty(PropertyInt64.TriuneWeaveCount) ?? 0;
                var cap = TierCap(cfg, killTier);
                if (cfg.Enabled && triune < cap)
                    Grant(player, value, triune, cap);

                // the band line goes out after this kill's point, so it reads in the order it happened. It covers Triune and the
                // school gems alike (both ride the same weekly count) - and only a player who can still earn one of them
                // (the count is read again: this kill may have just completed the tier)
                if (!(cfg.Enabled && (player.GetProperty(PropertyInt64.TriuneWeaveCount) ?? 0) < cap) && !SchoolGems.CanEarnAny(player, cfg, killTier))
                    return;
                var stored = player.GetProperty(PropertyInt64.TriuneWeekStart) ?? 0;
                if (kills < b1 && after >= b1)
                    player.SendMessage($"The Weave now rewards your hunting at a slower pace until the week turns ({NextResetText(cfg, stored)}).", ChatMessageType.Broadcast);
                else if (kills < b2 && after >= b2)
                    player.SendMessage($"The Weave rests until the week turns ({NextResetText(cfg, stored)}). Your kills here earn no Triune or gems until then.", ChatMessageType.Broadcast);
            }
            catch (Exception ex)
            {
                log.Error($"[TriuneEarning] Credit for {player?.Name}: {ex}");
            }
        }

        /// <summary>Adds one kill's value to the carried fraction; whole points (never past the cap) land on the counter.</summary>
        private static void Grant(Player player, double value, long triune, long cap)
        {
            if (!(value > 0))
                return;

            var (whole, fraction) = TierRewardScale.AddCoins(player.GetProperty(PropertyFloat.TriuneFraction) ?? 0, value);
            if (whole > cap - triune)
            {
                whole = cap - triune;
                fraction = 0;
            }

            if (fraction > 0)
                player.SetProperty(PropertyFloat.TriuneFraction, fraction);
            else
                player.RemoveProperty(PropertyFloat.TriuneFraction);

            if (whole <= 0)
                return;

            triune += whole;
            player.SetProperty(PropertyInt64.TriuneWeaveCount, triune);
            player.Session?.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.TriuneWeaveCount, triune));

            if (triune >= cap)
            {
                player.RemoveProperty(PropertyFloat.TriuneFraction);
                player.SendMessage($"Your Triune Weave grows to {triune:N0} - complete for these lands.", ChatMessageType.Broadcast);
            }
            else
                player.SendMessage($"Your Triune Weave grows to {triune:N0}.", ChatMessageType.Broadcast);
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

        /// <summary>"Friday 12:00 PM, in 2d 4h" - the next reset in the configured zone (for this player's stored week, if given).</summary>
        public static string NextResetText(Config cfg, long storedStart = 0)
        {
            var now = DateTime.UtcNow;
            var next = NextResetUtc(cfg, storedStart, now);
            var local = TimeZoneInfo.ConvertTimeFromUtc(next, ResetZone(cfg));
            var left = next - now;
            var leftText = left.TotalHours >= 24 ? $"{(int)left.TotalDays}d {left.Hours}h" : $"{(int)left.TotalHours}h {left.Minutes}m";
            return $"{local.ToString("dddd h:mm tt", CultureInfo.InvariantCulture)}, in {leftText}";
        }

        /// <summary>One kill per point, rounded: "403".</summary>
        public static string KillsPerPoint(double rate) => rate > 0 ? Math.Round(1.0 / rate).ToString("N0", CultureInfo.InvariantCulture) : "-";

        /// <summary>/triune: where this character stands.</summary>
        public static List<string> StatusLines(Player player)
        {
            var cfg = Current;
            var lines = new List<string>();
            var triune = player.GetProperty(PropertyInt64.TriuneWeaveCount) ?? 0;
            var frac = player.GetProperty(PropertyFloat.TriuneFraction) ?? 0;
            var shown = triune + Math.Floor(Math.Clamp(frac, 0, 0.999) * 100) / 100;   // truncated: 12.996 shows 12.99, never 13.00
            lines.Add($"Triune Weave: {shown.ToString("N2", CultureInfo.InvariantCulture)}");

            if (!cfg.Enabled && !cfg.GemsEnabled)
            {
                lines.Add("Triune is not earned from kills right now.");
                return lines;
            }

            var own = TierRewardScale.OwnTier(player);
            var row = own >= MinTier ? cfg.Tiers.FirstOrDefault(r => r.Tier == own) : null;
            if (row == null || row.Points <= 0)
            {
                lines.Add("You earn Triune and school gems from kills once you can fight in the deepest lands (and only there).");
                return lines;
            }

            var kills = WeekKills(player, cfg);
            var b1 = Band1Kills(row);
            var b2 = b1 + Band2Kills(row);
            var pace = kills < b1 ? "full pace" : kills < b2 ? "slower pace" : "resting";
            lines.Add($"This week: {kills:N0} kill{(kills == 1 ? "" : "s")} in your own lands - {pace}. Full pace for the first {b1:N0}, slower up to {b2:N0}, then it rests.");
            var storedStart = player.GetProperty(PropertyInt64.TriuneWeekStart) ?? 0;
            lines.Add($"Week turns: {NextResetText(cfg, kills > 0 ? storedStart : 0)}.");

            if (cfg.Enabled)
            {
                var cap = TierCap(cfg, own);
                if (triune >= cap)
                    lines.Add($"Triune Weave: complete for these lands ({cap:N0}). Deeper lands will ask more of it.");
                else
                    lines.Add($"Triune Weave: 1 point per {KillsPerPoint(Rate1(row))} kills at full pace, 1 per {KillsPerPoint(Rate2(row))} at the slower pace. Complete here at {cap:N0}.");
            }

            if (cfg.GemsEnabled)
                lines.AddRange(SchoolGems.StatusLines(player, cfg, own, row));
            return lines;
        }
    }
}
