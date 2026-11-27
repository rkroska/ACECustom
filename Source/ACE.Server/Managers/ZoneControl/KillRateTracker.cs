using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

using log4net;

using ACE.Entity.Enum;
using ACE.Server.Entity.Actions;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// How a counted kill was made, for balance work (melee vs missile vs war vs void). Read from the killer at the kill: the
    /// combat stance, and for magic the school of their last successful cast. Plain field reads, no inventory or objects.
    /// </summary>
    public enum KillStyle
    {
        Melee = 0,
        Missile = 1,
        War = 2,
        Void = 3,
        /// <summary>The top damager was the player's combat pet.</summary>
        Pet = 4,
        /// <summary>Anything else: out of combat stance (a DoT finishing it), another magic school, an admin smite.</summary>
        Other = 5,
    }

    /// <summary>
    /// T11+ kills per character per clock hour, for staff (/killrate and the portal's Kill Rates page). Counted in memory on
    /// the kill path (TierRewards.OnCreatureKilled): no database read or write per kill. A restart clears the memory; each
    /// finished hour is written once, off the kill path, to the server log (one [KILLRATE] line per character) and to the shard
    /// table kill_rate_hour (<see cref="KillRateHistory"/>), which the portal's 7d / 30d windows read.
    ///
    /// What counts is the Prestige Coin kill: a T11-T25 Zone Control creature that pays kill XP or luminance, credited to the
    /// top damager (a pet's kill to its owner), online, not a mule or an Olthoi player. Fellowship and Zone Share sharers are
    /// not counted - the killer only, as creature_kills and coins do. Counting does not depend on the zone paying coins or on
    /// the coin falloff.
    ///
    /// Per character: the last <see cref="KillRateStore.HoursKept"/> UTC clock hours, each with kills per kill tier, the first
    /// and last kill time (to mark part hours) and the character's own tier that hour (TierRewardScale.OwnTier, read once per
    /// hour on the player's own thread when their first kill of the hour opens the bucket).
    /// </summary>
    public static class KillRateTracker
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>The live store. Tests make their own.</summary>
        internal static readonly KillRateStore Store = new();

        /// <summary>
        /// Once a minute on a pool thread: writes the [KILLRATE] lines of hours that just ended, saves them to kill_rate_hour,
        /// drops idle characters and prunes old history. On a timer, not the first kill of the next hour, so an hour is sealed
        /// even when nobody kills after it (a quiet night, or a restart soon after), and no landblock thread pays for the work.
        /// Held here so it is never collected.
        /// </summary>
        private static readonly Timer FlushTimer;

        /// <summary>Starts the flush timer when the tracker is first used, after marking the current hour as the one running,
        /// so the first hour since start is logged when it ends.</summary>
        static KillRateTracker()
        {
            Store.Advance(KillRateStore.HourOf(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            FlushTimer = new Timer(_ => Flush(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        /// <summary>This server run's id in kill_rate_hour: the process start, unix seconds. A restart mid-hour gives the hour one
        /// row per run, added up when read.</summary>
        public static readonly long RunId = new DateTimeOffset(System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()).ToUnixTimeSeconds();

        /// <summary>The UTC day the history was last pruned (0 = not yet this run).</summary>
        private static long lastPruneDay;

        /// <summary>The timer's tick: seals the hours that ended since the last tick (see <see cref="FlushAt"/>).</summary>
        internal static void Flush() => FlushAt(KillRateStore.HourOf(DateTimeOffset.UtcNow.ToUnixTimeSeconds()), shutdown: false);

        /// <summary>
        /// At server shutdown, once every player has logged off: seals the hours that ended since the last timer tick (a restart
        /// right on the hour) and the running hour, marked "partial: server shutdown", in one batch for one clock reading, so no
        /// counted hour leaves no trace.
        /// </summary>
        public static void FlushAtShutdown() => FlushAt(KillRateStore.HourOf(DateTimeOffset.UtcNow.ToUnixTimeSeconds()), shutdown: true);

        /// <summary>
        /// Every hour that ended before <paramref name="hour"/>: one log line per character, then saved to kill_rate_hour (a
        /// small batch, off the game threads; a failed write is retried next minute). At shutdown, the running hour too, partial,
        /// in the same batch: one clock reading, so an hour that ends during the shutdown is sealed either way. Then prunes idle
        /// characters and, once per UTC day, the history past kill_rate_history_days.
        /// </summary>
        /// <param name="hour">The current UTC hour number.</param>
        /// <param name="shutdown">The server is stopping: also save the running hour, and don't promise a retry.</param>
        private static void FlushAt(long hour, bool shutdown)
        {
            try
            {
                var finished = Store.Advance(hour);

                var records = new List<KillRateStore.HourRecord>();
                foreach (var h in finished)
                {
                    foreach (var line in Store.LogLines(h))
                        log.Info(line);
                    records.AddRange(Store.Snapshot(h, RunId, partial: false));
                }
                if (shutdown)
                {
                    foreach (var line in Store.LogLines(hour))
                        log.Info(line + " | partial: server shutdown");
                    records.AddRange(Store.Snapshot(hour, RunId, partial: true));
                }
                KillRateHistory.Save(records, final: shutdown);   // also retries any batch an earlier tick could not write

                if (finished.Count > 0)
                    Store.Prune(hour);

                var day = hour / 24;
                if (!shutdown && day != lastPruneDay)
                {
                    lastPruneDay = day;
                    KillRateHistory.Prune(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                }
            }
            catch (Exception ex)
            {
                log.Error($"[KillRateTracker] {(shutdown ? "FlushAtShutdown" : "Flush")}: {ex}");
            }
        }

        /// <summary>
        /// The style of a kill from what the killer was doing at the time: a pet's kill is Pet; in magic stance the school of
        /// the last successful cast decides War or Void; melee and missile stance are what they say; anything else is Other.
        /// </summary>
        /// <param name="petKill">The top damager was the player's combat pet.</param>
        /// <param name="mode">The player's combat stance at the kill.</param>
        /// <param name="lastCastSchool">The school of the player's last successful cast.</param>
        /// <returns>The style to count the kill under.</returns>
        public static KillStyle StyleOf(bool petKill, CombatMode mode, MagicSchool lastCastSchool)
        {
            if (petKill)
                return KillStyle.Pet;

            return mode switch
            {
                CombatMode.Melee => KillStyle.Melee,
                CombatMode.Missile => KillStyle.Missile,
                CombatMode.Magic => lastCastSchool switch
                {
                    MagicSchool.WarMagic => KillStyle.War,
                    MagicSchool.VoidMagic => KillStyle.Void,
                    _ => KillStyle.Other,
                },
                _ => KillStyle.Other,
            };
        }

        /// <summary>
        /// Counts one kill at <paramref name="killTier"/> (11-25) for <paramref name="player"/>, made in <paramref name="style"/>.
        /// Called on the victim's thread once the killer is resolved; thread-safe and O(1) (a lock only when a character's hour opens).
        /// </summary>
        public static void Record(Player player, int killTier, KillStyle style = KillStyle.Other)
        {
            try
            {
                if (player == null)
                    return;

                // only the count here: the hourly log and prune run on FlushTimer, off the kill path
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var opened = Store.Record(player.Guid.Full, player.Name, killTier, now, style);
                if (opened != null)
                    LookUpOwnTier(player, opened);
            }
            catch (Exception ex)
            {
                log.Error($"[KillRateTracker] Record for {player?.Name}: {ex}");
            }
        }

        /// <summary>Own tier reads the player's augmentations and skills: looked up on their own thread, once per hour. A separate
        /// method so the closure is only allocated when an hour opens, not on every kill.</summary>
        private static void LookUpOwnTier(Player player, KillRateStore.Bucket opened)
        {
            var guid = player.Guid.Full;
            LandblockManager.RunOnThreadFor(player, ActionType.TierRewards_KillRateOwnTier,
                () => Store.SetOwnTier(guid, opened, TierRewardScale.OwnTier(player)));
        }

        /// <summary>Every character with T11+ kills in the last <paramref name="hours"/> clock hours up to <paramref name="now"/>
        /// (unix seconds; the running hour included). Callers that also label the hours pass the same <paramref name="now"/>.</summary>
        public static List<KillRateStore.CharacterReport> Report(long now, int hours) => Store.Report(now, hours);

        /// <summary>
        /// The last <paramref name="days"/> UTC days (today included) from kill_rate_hour, plus this run's hours still in memory
        /// (the running hour, and any hour not saved yet), one row per character per day. Reads the database: call it off the
        /// game threads (the portal does).
        /// </summary>
        public static List<KillRateStore.CharacterReport> DayReport(long now, int days)
        {
            var nowHour = KillRateStore.HourOf(now);
            var firstHour = (nowHour / 24 - days + 1) * 24;

            var records = KillRateHistory.Load(firstHour, nowHour);
            for (var h = Math.Max(firstHour, nowHour - KillRateStore.HoursKept + 1); h <= nowHour; h++)
                records.AddRange(Store.Snapshot(h, RunId, partial: h == nowHour));
            return KillRateStore.DayReport(records, now, days);
        }
    }

    /// <summary>
    /// The counters behind <see cref="KillRateTracker"/>, and the rules that turn them into rates. No game objects, so it can
    /// be tested on its own. Writers (kills, on many landblock threads) and readers (the command, the portal) run at once:
    /// counts use Interlocked, a new hour's bucket is swapped in with a compare-exchange, and readers see each number whole.
    /// </summary>
    public sealed class KillRateStore
    {
        /// <summary>Clock hours kept per character. Older buckets are overwritten as new hours arrive.</summary>
        public const int HoursKept = 48;

        public const int MinTier = TierRewardScale.MinTier;
        public const int MaxTier = ZoneRare.MaxTier;
        public const int TierCount = MaxTier - MinTier + 1;

        public const long HourSeconds = 3600;

        /// <summary>Kill styles counted per hour (<see cref="KillStyle"/>).</summary>
        public const int StyleCount = (int)KillStyle.Other + 1;

        /// <summary>A style's name for the report and the log ("Missile").</summary>
        public static string StyleName(KillStyle style) => style.ToString();

        /// <summary>A kill hour counts as full only if the first kill came in its first 5 minutes and the last in its last 5:
        /// a character who played 20 minutes of the hour must not drag their average down.</summary>
        public const long PartHourSlackSeconds = 300;

        /// <summary>An hour is "clean" (used for the average) when own-tier kills are at least this share of its kills - the
        /// same 95 pct the hand-built table used.</summary>
        public const double CleanOwnShare = 0.95;

        /// <summary>An average over fewer clean hours than this is a short one: shown marked, ranked after the steady ones.</summary>
        public const int MinSteadyHours = 3;

        /// <summary>One character's kills in one UTC clock hour.</summary>
        public sealed class Bucket
        {
            /// <summary>The UTC hour number: unix seconds / 3600.</summary>
            public readonly long Hour;

            private readonly int[] kills = new int[TierCount];
            private readonly int[] styles = new int[StyleCount];
            private long first = long.MaxValue;
            private long last = long.MinValue;
            private int ownTier;

            internal Bucket(long hour) => Hour = hour;

            /// <summary>Counts one kill: its time, its style (an unknown style counts as Other) and its tier.</summary>
            /// <param name="killTier">The kill tier, 11-25 (checked by the caller).</param>
            /// <param name="now">Unix seconds of the kill.</param>
            /// <param name="style">How the kill was made.</param>
            internal void Add(int killTier, long now, KillStyle style)
            {
                // times and style first, the tier count last: a reader that sees a kill always sees its time and style set
                InterlockedMin(ref first, now);
                InterlockedMax(ref last, now);
                var s = (int)style;
                Interlocked.Increment(ref styles[s >= 0 && s < StyleCount ? s : (int)KillStyle.Other]);
                Interlocked.Increment(ref kills[killTier - MinTier]);
            }

            /// <summary>Kills per tier this hour as an array, index tier - MinTier (a copy).</summary>
            public int[] KillsSnapshot()
            {
                var a = new int[TierCount];
                for (var i = 0; i < TierCount; i++)
                    a[i] = Volatile.Read(ref kills[i]);
                return a;
            }

            /// <summary>Kills per style this hour as an array, index (int)KillStyle (a copy).</summary>
            public int[] StylesSnapshot()
            {
                var a = new int[StyleCount];
                for (var i = 0; i < StyleCount; i++)
                    a[i] = Volatile.Read(ref styles[i]);
                return a;
            }

            /// <summary>Kills made in <paramref name="style"/> this hour (all tiers).</summary>
            public int KillsIn(KillStyle style)
            {
                var s = (int)style;
                return s < 0 || s >= StyleCount ? 0 : Volatile.Read(ref styles[s]);
            }

            /// <summary>Kills at <paramref name="tier"/> this hour (0 outside T11-T25).</summary>
            public int KillsAt(int tier) => tier < MinTier || tier > MaxTier ? 0 : Volatile.Read(ref kills[tier - MinTier]);

            /// <summary>All T11-T25 kills this hour.</summary>
            public int Total
            {
                get
                {
                    var total = 0;
                    for (var i = 0; i < TierCount; i++)
                        total += Volatile.Read(ref kills[i]);
                    return total;
                }
            }

            /// <summary>Unix seconds of the first / last kill this hour.</summary>
            public long First => Interlocked.Read(ref first);
            public long Last => Interlocked.Read(ref last);

            /// <summary>The character's own tier this hour; 0 = not looked up yet.</summary>
            public int OwnTier => Volatile.Read(ref ownTier);
            internal void SetOwnTier(int tier) => Volatile.Write(ref ownTier, tier);
        }

        private sealed class Entry
        {
            public readonly Bucket[] Ring = new Bucket[HoursKept];
            public volatile string Name;
            public int LastOwnTier;

            public Entry(string name) => Name = name;
        }

        private readonly ConcurrentDictionary<uint, Entry> entries = new();

        /// <summary>Held by Prune for its scan-and-remove and by Record while it opens a new hour, so a prune can never remove
        /// an entry between a kill opening its hour there and that kill being counted.</summary>
        private readonly object pruneLock = new();

        /// <summary>The newest hour any kill was recorded in (0 = none yet): its change is what marks an hour as finished.</summary>
        private long currentHour;

        public static long HourOf(long unixSeconds) => unixSeconds / HourSeconds;

        /// <summary>
        /// Counts one kill. Returns the bucket when this kill opened it (the character's first kill this hour), so the caller can
        /// look up the own tier once; otherwise null. A tier outside T11-T25 is ignored. A kill stamped before the hour its ring
        /// slot already holds (a clock step backwards) is dropped rather than counted into the wrong hour.
        /// </summary>
        public Bucket Record(uint guid, string name, int killTier, long now, KillStyle style = KillStyle.Other)
        {
            if (killTier < MinTier || killTier > MaxTier)
                return null;

            var hour = HourOf(now);
            var slot = (int)(hour % HoursKept);

            while (true)
            {
                var entry = entries.GetOrAdd(guid, static (_, n) => new Entry(n), name);
                if (name != null && !string.Equals(entry.Name, name, StringComparison.Ordinal))
                    entry.Name = name;

                Bucket opened = null;
                var bucket = Volatile.Read(ref entry.Ring[slot]);
                if (bucket == null || bucket.Hour < hour)
                {
                    // opening an hour shares Prune's lock (once per character-hour, never per kill): Prune then either sees
                    // this bucket and keeps the entry, or removed it before and the check below counts the kill again
                    lock (pruneLock)
                    {
                        bucket = Volatile.Read(ref entry.Ring[slot]);
                        while (bucket == null || bucket.Hour < hour)
                        {
                            var fresh = new Bucket(hour);
                            var seen = Interlocked.CompareExchange(ref entry.Ring[slot], fresh, bucket);
                            if (ReferenceEquals(seen, bucket))
                            {
                                bucket = opened = fresh;
                                break;
                            }
                            bucket = seen;   // another thread opened it first
                        }
                    }
                }

                if (bucket.Hour != hour)
                    return null;

                bucket.Add(killTier, now, style);

                // Prune may have dropped this character (idle 48 hours) between GetOrAdd and here: count it again on the entry
                // that is in the store now, so the kill is not lost with the removed one
                if (entries.TryGetValue(guid, out var current) && ReferenceEquals(current, entry))
                    return opened;
            }
        }

        /// <summary>Stores the own tier looked up for <paramref name="bucket"/> (on the player's thread).</summary>
        public void SetOwnTier(uint guid, Bucket bucket, int ownTier)
        {
            if (bucket == null || ownTier <= 0)
                return;

            bucket.SetOwnTier(ownTier);
            if (entries.TryGetValue(guid, out var entry))
                Volatile.Write(ref entry.LastOwnTier, ownTier);
        }

        /// <summary>
        /// Moves the current hour to <paramref name="hour"/>. Returns the hours that ended because of it (oldest first, at most
        /// HoursKept), exactly once across threads; empty when the hour did not change or this is the first call since start.
        /// </summary>
        public IReadOnlyList<long> Advance(long hour)
        {
            var prev = Interlocked.Read(ref currentHour);
            while (hour > prev)
            {
                var seen = Interlocked.CompareExchange(ref currentHour, hour, prev);
                if (seen == prev)
                {
                    if (prev == 0)
                        return Array.Empty<long>();

                    var from = Math.Max(prev, hour - HoursKept);
                    var list = new List<long>((int)(hour - from));
                    for (var h = from; h < hour; h++)
                        list.Add(h);
                    return list;
                }
                prev = seen;
            }
            return Array.Empty<long>();
        }

        /// <summary>
        /// The [KILLRATE] log lines for <paramref name="hour"/>, one per character who killed in it:
        /// <c>[KILLRATE] 2026-11-26T06:00Z "Masta Mage Og" own T15 | T15:730 T14:2 | total 732 | Missile:700 War:32 | first 06:00:04 last 06:59:58</c>.
        /// A kill landing in the hour in the same instant it is logged may be missed by the line; the counters still have it.
        /// </summary>
        public List<string> LogLines(long hour)
        {
            var lines = new List<string>();
            var slot = (int)(hour % HoursKept);
            var stamp = HourStartUtc(hour).ToString("yyyy-MM-dd'T'HH':00Z'", CultureInfo.InvariantCulture);

            foreach (var entry in entries.Values.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            {
                var bucket = Volatile.Read(ref entry.Ring[slot]);
                if (bucket == null || bucket.Hour != hour || bucket.Total == 0)
                    continue;

                var own = bucket.OwnTier > 0 ? $"own T{bucket.OwnTier}" : "own ?";
                var sb = new StringBuilder();
                sb.Append($"[KILLRATE] {stamp} \"{entry.Name}\" {own} |");
                for (var tier = MaxTier; tier >= MinTier; tier--)
                {
                    var k = bucket.KillsAt(tier);
                    if (k > 0)
                        sb.Append($" T{tier}:{k}");
                }
                sb.Append($" | total {bucket.Total} |");
                for (var s = 0; s < StyleCount; s++)
                {
                    var k = bucket.KillsIn((KillStyle)s);
                    if (k > 0)
                        sb.Append(CultureInfo.InvariantCulture, $" {StyleName((KillStyle)s)}:{k}");
                }
                sb.Append($" | first {Clock(bucket.First)} last {Clock(bucket.Last)}");
                lines.Add(sb.ToString());
            }
            return lines;
        }

        /// <summary>Drops characters with no kill in the hours still kept, so memory stays bounded by recent killers.</summary>
        public void Prune(long hour)
        {
            // scan and remove under the lock Record takes to open an hour: no entry can gain a new hour between the two
            lock (pruneLock)
            {
                foreach (var kv in entries)
                {
                    var keep = false;
                    var ring = kv.Value.Ring;
                    for (var i = 0; i < ring.Length; i++)
                    {
                        var b = Volatile.Read(ref ring[i]);
                        if (b != null && b.Hour > hour - HoursKept)
                        {
                            keep = true;
                            break;
                        }
                    }
                    if (!keep)
                        entries.TryRemove(kv);   // only this entry: never one a kill re-added meanwhile
                }
            }
        }

        // ---- reports (the command and the portal) ------------------------------------------------------------------

        /// <summary>One character's numbers for one clock hour.</summary>
        public sealed class HourRow
        {
            public long Hour { get; init; }
            /// <summary>Unix seconds the hour starts at.</summary>
            public long HourStart => Hour * HourSeconds;
            /// <summary>Own tier that hour (the character's latest known when that hour's lookup has not landed); 0 = unknown.</summary>
            public int OwnTier { get; init; }
            /// <summary>Own-tier kills (0 when the own tier is unknown).</summary>
            public int Own { get; init; }
            public int Total { get; init; }
            /// <summary>Kills per tier, only tiers with kills.</summary>
            public SortedDictionary<int, int> ByTier { get; init; }
            /// <summary>Kills per style ("Missile": 700), all tiers, only styles with kills, in KillStyle order.</summary>
            public Dictionary<string, int> ByStyle { get; init; }
            public long First { get; init; }
            public long Last { get; init; }
            /// <summary>The hour still running.</summary>
            public bool Current { get; init; }
            /// <summary>Not a full hour of play: still running, or the first kill after :05 or the last before :55.</summary>
            public bool Part { get; init; }
            /// <summary>Counts toward the average: full, own tier known, and own-tier kills at least 95 pct of the hour's.</summary>
            public bool Clean { get; init; }
            /// <summary>Show all kills next to the own-tier count ("188 (389)"): see <see cref="ShowsTotal(int, int, bool, bool)"/>.</summary>
            public bool ShowsTotal { get; init; }
            /// <summary>Day rows only (the 7d / 30d windows): clean hours that UTC day; null on hour rows.</summary>
            public int? CleanHours { get; init; }
            /// <summary>Day rows only: average own-tier kills over that day's clean hours; null on hour rows or with none.</summary>
            public double? AvgOwnPerHour { get; init; }
        }

        /// <summary>
        /// One character's counts for one hour as saved in kill_rate_hour: frozen, from one server run. A restart in the middle
        /// of an hour leaves one record per run for it; <see cref="MergeRuns"/> adds them up.
        /// </summary>
        public sealed class HourRecord
        {
            public uint Guid { get; init; }
            public string Name { get; init; }
            /// <summary>The UTC hour number.</summary>
            public long Hour { get; init; }
            /// <summary>Which server run counted it (the process start, unix seconds).</summary>
            public long RunId { get; init; }
            /// <summary>Own tier that hour; 0 = unknown.</summary>
            public int OwnTier { get; init; }
            /// <summary>Kills per tier, index tier - MinTier (length TierCount).</summary>
            public int[] Kills { get; init; }
            /// <summary>Kills per style, index (int)KillStyle (length StyleCount).</summary>
            public int[] Styles { get; init; }
            public long First { get; init; }
            public long Last { get; init; }
            /// <summary>Written before the hour ended (at a shutdown); the run's count for that hour stops there.</summary>
            public bool Partial { get; init; }
            public int Total => Kills.Sum();
            /// <summary>Own-tier kills (0 with an unknown own tier).</summary>
            public int Own => OwnTier >= MinTier && OwnTier <= MaxTier ? Kills[OwnTier - MinTier] : 0;
        }

        /// <summary>
        /// Every character's counts for <paramref name="hour"/>, frozen for saving (characters with no kill that hour left out).
        /// The own tier falls back to the character's latest known while the hour's lookup has not landed.
        /// </summary>
        /// <param name="hour">The UTC hour number.</param>
        /// <param name="runId">This server run's id.</param>
        /// <param name="partial">The hour has not ended (a shutdown).</param>
        /// <returns>One record per character who killed in that hour.</returns>
        public List<HourRecord> Snapshot(long hour, long runId, bool partial)
        {
            var slot = (int)(hour % HoursKept);
            var list = new List<HourRecord>();
            foreach (var kv in entries)
            {
                var b = Volatile.Read(ref kv.Value.Ring[slot]);
                if (b == null || b.Hour != hour || b.Total == 0)
                    continue;
                list.Add(new HourRecord
                {
                    Guid = kv.Key,
                    Name = kv.Value.Name,
                    Hour = hour,
                    RunId = runId,
                    OwnTier = b.OwnTier > 0 ? b.OwnTier : Volatile.Read(ref kv.Value.LastOwnTier),
                    Kills = b.KillsSnapshot(),
                    Styles = b.StylesSnapshot(),
                    First = b.First,
                    Last = b.Last,
                    Partial = partial,
                });
            }
            return list;
        }

        /// <summary>
        /// One record per character and hour: records of the same hour from several server runs (a restart mid-hour) add their
        /// counts, keep the earliest first and latest last kill, and take the own tier and name of the newest run that knew them.
        /// Duplicates of the same run (a row read back from the database and still in memory) count once.
        /// </summary>
        /// <param name="records">Saved and in-memory records, in any order.</param>
        /// <returns>The merged records, by character then hour.</returns>
        public static List<HourRecord> MergeRuns(IEnumerable<HourRecord> records)
        {
            var result = new List<HourRecord>();
            var distinct = records
                .GroupBy(r => (r.Guid, r.Hour, r.RunId))
                .Select(g => g.First());
            foreach (var g in distinct.GroupBy(r => (r.Guid, r.Hour)).OrderBy(g => g.Key.Guid).ThenBy(g => g.Key.Hour))
            {
                var runs = g.OrderBy(r => r.RunId).ToList();
                if (runs.Count == 1)
                {
                    result.Add(runs[0]);
                    continue;
                }
                var kills = new int[TierCount];
                var styles = new int[StyleCount];
                foreach (var r in runs)
                {
                    for (var i = 0; i < TierCount; i++) kills[i] += r.Kills[i];
                    for (var i = 0; i < StyleCount; i++) styles[i] += r.Styles[i];
                }
                var newest = runs[^1];
                result.Add(new HourRecord
                {
                    Guid = g.Key.Guid,
                    Name = newest.Name,
                    Hour = g.Key.Hour,
                    RunId = newest.RunId,
                    OwnTier = runs.LastOrDefault(r => r.OwnTier > 0)?.OwnTier ?? 0,
                    Kills = kills,
                    Styles = styles,
                    First = runs.Min(r => r.First),
                    Last = runs.Max(r => r.Last),
                    Partial = runs.All(r => r.Partial),
                });
            }
            return result;
        }

        /// <summary>
        /// The long-window report (7 or 30 UTC days up to and including today): the summary per character uses the same
        /// clean-hour rules over every hour of the window, and its rows are one per UTC day (kills, and the average own-tier
        /// kills over that day's clean hours).
        /// </summary>
        /// <param name="records">Saved and in-memory hour records (duplicates and split runs are merged here).</param>
        /// <param name="now">Unix seconds now.</param>
        /// <param name="days">Window length in UTC days, today included.</param>
        /// <returns>One report per character with kills in the window, sorted like <see cref="Report"/>.</returns>
        public static List<CharacterReport> DayReport(IEnumerable<HourRecord> records, long now, int days)
        {
            var nowHour = HourOf(now);
            var today = nowHour / 24;
            var firstHour = (today - days + 1) * 24;

            var merged = MergeRuns(records.Where(r => r.Hour >= firstHour && r.Hour <= nowHour && r.Total > 0));
            var result = new List<CharacterReport>();
            foreach (var g in merged.GroupBy(r => r.Guid))
            {
                var recs = g.OrderBy(r => r.Hour).ToList();
                var hourRows = recs.Select(r => Row(r.Hour, r.OwnTier, r.Kills, r.Styles, r.First, r.Last, nowHour)).ToList();
                var ownTier = hourRows.LastOrDefault(h => h.OwnTier > 0)?.OwnTier ?? 0;
                var summary = Summarize(g.Key, recs[^1].Name, ownTier, hourRows);

                var dayRows = recs.Zip(hourRows).GroupBy(p => p.First.Hour / 24).Select(d =>
                {
                    var kills = new int[TierCount];
                    var styles = new int[StyleCount];
                    foreach (var (rec, _) in d)
                    {
                        for (var i = 0; i < TierCount; i++) kills[i] += rec.Kills[i];
                        for (var i = 0; i < StyleCount; i++) styles[i] += rec.Styles[i];
                    }
                    var rows = d.Select(p => p.Second).ToList();
                    var clean = rows.Where(r => r.Clean).ToList();
                    var own = rows.Sum(r => r.Own);
                    var total = rows.Sum(r => r.Total);
                    var byTier = new SortedDictionary<int, int>();
                    for (var tier = MinTier; tier <= MaxTier; tier++)
                        if (kills[tier - MinTier] > 0)
                            byTier[tier] = kills[tier - MinTier];
                    return new HourRow
                    {
                        Hour = d.Key * 24,
                        OwnTier = rows.LastOrDefault(r => r.OwnTier > 0)?.OwnTier ?? 0,
                        Own = own,
                        Total = total,
                        ByTier = byTier,
                        ByStyle = StyleMap(styles),
                        First = rows.Min(r => r.First),
                        Last = rows.Max(r => r.Last),
                        Current = d.Key == today,
                        Part = clean.Count == 0,
                        Clean = clean.Count > 0,
                        // a day has no clean bar of its own: show the total by the hand-built table's rule only
                        ShowsTotal = ShowsTotal(own, total, part: true, clean: false),
                        CleanHours = clean.Count,
                        AvgOwnPerHour = clean.Count > 0 ? clean.Average(r => r.Own) : null,
                    };
                }).ToList();

                result.Add(new CharacterReport
                {
                    Guid = summary.Guid,
                    Name = summary.Name,
                    OwnTier = summary.OwnTier,
                    Hours = dayRows,
                    AvgOwnPerHour = summary.AvgOwnPerHour,
                    CleanHours = summary.CleanHours,
                    OwnKills = summary.OwnKills,
                    TotalKills = summary.TotalKills,
                    ByStyle = summary.ByStyle,
                    MainStyle = summary.MainStyle,
                });
            }
            return Sorted(result);
        }

        /// <summary>Own tier high to low, steady averages before short ones, then average, then name (the command and the page).</summary>
        private static List<CharacterReport> Sorted(IEnumerable<CharacterReport> reports)
            => reports
                .OrderByDescending(r => r.OwnTier)
                .ThenByDescending(r => r.Steady)
                .ThenByDescending(r => r.AvgOwnPerHour ?? -1)
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

        /// <summary>One character over the window asked for.</summary>
        public sealed class CharacterReport
        {
            public uint Guid { get; init; }
            public string Name { get; init; }
            /// <summary>Own tier now (the newest known); 0 = unknown.</summary>
            public int OwnTier { get; init; }
            /// <summary>Oldest hour first; only hours with kills.</summary>
            public List<HourRow> Hours { get; init; }
            /// <summary>Average own-tier kills over the clean hours; null when there are none.</summary>
            public double? AvgOwnPerHour { get; init; }
            public int CleanHours { get; init; }
            /// <summary>The average rests on at least <see cref="MinSteadyHours"/> clean hours; shorter ones rank after the steady
            /// ones in their tier (the command and the portal alike).</summary>
            public bool Steady => AvgOwnPerHour.HasValue && CleanHours >= MinSteadyHours;
            public int OwnKills { get; init; }
            public int TotalKills { get; init; }
            /// <summary>Own-tier kills / all kills over the window (0 when there are none).</summary>
            public double OwnShare => TotalKills > 0 ? (double)OwnKills / TotalKills : 0.0;
            /// <summary>Kills per style over the window, all tiers, only styles with kills, in KillStyle order.</summary>
            public Dictionary<string, int> ByStyle { get; init; }
            /// <summary>The style with the most kills over the window (the first in KillStyle order on a tie); null with no kills.</summary>
            public string MainStyle { get; init; }
        }

        /// <summary>
        /// Every character with kills in the last <paramref name="hours"/> clock hours up to <paramref name="now"/> (the running
        /// hour included; 1-HoursKept), sorted by own tier, then steady averages before short ones, then average, highest first.
        /// </summary>
        public List<CharacterReport> Report(long now, int hours)
        {
            hours = Math.Clamp(hours, 1, HoursKept);
            var nowHour = HourOf(now);
            var oldest = nowHour - hours + 1;

            var result = new List<CharacterReport>();
            foreach (var kv in entries)
            {
                var entry = kv.Value;
                var lastOwn = Volatile.Read(ref entry.LastOwnTier);

                var buckets = new List<Bucket>();
                for (var i = 0; i < entry.Ring.Length; i++)
                {
                    var b = Volatile.Read(ref entry.Ring[i]);
                    if (b != null && b.Hour >= oldest && b.Hour <= nowHour && b.Total > 0)
                        buckets.Add(b);
                }
                if (buckets.Count == 0)
                    continue;

                var rows = buckets.OrderBy(b => b.Hour).Select(b => Row(b, lastOwn, nowHour)).ToList();
                var newestKnown = rows.LastOrDefault(r => r.OwnTier > 0)?.OwnTier ?? lastOwn;
                result.Add(Summarize(kv.Key, entry.Name, newestKnown, rows));
            }

            return Sorted(result);
        }

        /// <summary>
        /// One bucket as a report row. The own tier is the bucket's, or <paramref name="fallbackOwnTier"/> (the character's
        /// latest known) while its lookup has not landed; with neither, own-tier kills read 0 and the hour is never clean.
        /// </summary>
        /// <param name="b">The hour's counters.</param>
        /// <param name="fallbackOwnTier">The character's latest known own tier; 0 = none.</param>
        /// <param name="nowHour">The running hour number, to mark the current hour.</param>
        /// <returns>The hour's numbers and flags.</returns>
        private static HourRow Row(Bucket b, int fallbackOwnTier, long nowHour)
            => Row(b.Hour, b.OwnTier > 0 ? b.OwnTier : fallbackOwnTier, b.KillsSnapshot(), b.StylesSnapshot(), b.First, b.Last, nowHour);

        /// <summary>
        /// One hour as a report row, from plain counts (a live bucket or a saved kill_rate_hour row): the same part / clean /
        /// shows-total rules either way.
        /// </summary>
        /// <param name="hour">The UTC hour number.</param>
        /// <param name="ownTier">The own tier that hour; 0 = unknown (own-tier kills read 0, never clean).</param>
        /// <param name="kills">Kills per tier, index tier - MinTier.</param>
        /// <param name="styles">Kills per style, index (int)KillStyle.</param>
        /// <param name="first">Unix seconds of the first kill.</param>
        /// <param name="last">Unix seconds of the last kill.</param>
        /// <param name="nowHour">The running hour number, to mark the current hour.</param>
        /// <returns>The hour's numbers and flags.</returns>
        private static HourRow Row(long hour, int ownTier, int[] kills, int[] styles, long first, long last, long nowHour)
        {
            var total = kills.Sum();
            var own = ownTier >= MinTier && ownTier <= MaxTier ? kills[ownTier - MinTier] : 0;
            var current = hour == nowHour;
            var part = IsPartHour(hour, first, last, current);
            var clean = IsCleanHour(own, total, part, ownTier > 0);

            var byTier = new SortedDictionary<int, int>();
            for (var tier = MinTier; tier <= MaxTier; tier++)
            {
                var k = kills[tier - MinTier];
                if (k > 0)
                    byTier[tier] = k;
            }

            return new HourRow
            {
                Hour = hour,
                OwnTier = ownTier,
                Own = own,
                Total = total,
                ByTier = byTier,
                ByStyle = StyleMap(styles),
                First = first,
                Last = last,
                Current = current,
                Part = part,
                Clean = clean,
                ShowsTotal = ShowsTotal(own, total, part, clean),
            };
        }

        /// <summary>Kills per style as "Missile": 700, only styles with kills, in KillStyle order.</summary>
        private static Dictionary<string, int> StyleMap(int[] styles)
        {
            var byStyle = new Dictionary<string, int>();
            for (var s = 0; s < StyleCount; s++)
                if (styles[s] > 0)
                    byStyle[StyleName((KillStyle)s)] = styles[s];
            return byStyle;
        }

        /// <summary>The summary over <paramref name="rows"/>: average own-tier kills over the clean hours, and the totals.</summary>
        internal static CharacterReport Summarize(uint guid, string name, int ownTier, List<HourRow> rows)
        {
            var clean = rows.Where(r => r.Clean).ToList();

            // style totals in KillStyle order, so a tie for the main style goes to the earlier style
            var byStyle = new Dictionary<string, int>();
            string mainStyle = null;
            var mainKills = 0;
            for (var s = 0; s < StyleCount; s++)
            {
                var key = StyleName((KillStyle)s);
                var k = rows.Sum(r => r.ByStyle != null && r.ByStyle.TryGetValue(key, out var n) ? n : 0);
                if (k <= 0)
                    continue;
                byStyle[key] = k;
                if (k > mainKills)
                {
                    mainKills = k;
                    mainStyle = key;
                }
            }

            return new CharacterReport
            {
                Guid = guid,
                Name = name,
                OwnTier = ownTier,
                Hours = rows,
                AvgOwnPerHour = clean.Count > 0 ? clean.Average(r => r.Own) : null,
                CleanHours = clean.Count,
                OwnKills = rows.Sum(r => r.Own),
                TotalKills = rows.Sum(r => r.Total),
                ByStyle = byStyle,
                MainStyle = mainStyle,
            };
        }

        /// <summary>True when the hour was not played in full: it is still running, the first kill came after the first
        /// PartHourSlackSeconds, or the last kill before the last PartHourSlackSeconds.</summary>
        public static bool IsPartHour(long hour, long first, long last, bool current)
        {
            if (current)
                return true;

            var start = hour * HourSeconds;
            return first > start + PartHourSlackSeconds || last < start + HourSeconds - PartHourSlackSeconds;
        }

        /// <summary>True when an hour counts toward the average: full, own tier known, own-tier kills above 0 and at least
        /// CleanOwnShare of all its kills.</summary>
        public static bool IsCleanHour(int own, int total, bool part, bool ownTierKnown = true)
            => !part && ownTierKnown && own > 0 && own >= CleanOwnShare * total;

        /// <summary>
        /// True when all kills are shown next to the own-tier count ("188 (389)"): off-tier kills above 5 and above 5 pct of the
        /// hour (the hand-built table's rule), or any off-tier kills in a full hour that missed the clean bar, so an hour left
        /// out of the average always shows why ("47 (50)").
        /// </summary>
        /// <param name="own">Own-tier kills that hour.</param>
        /// <param name="total">All kills that hour.</param>
        /// <param name="part">Not a full hour (see IsPartHour).</param>
        /// <param name="clean">Counts toward the average (see IsCleanHour).</param>
        /// <returns>Whether the total is worth showing.</returns>
        public static bool ShowsTotal(int own, int total, bool part, bool clean)
            => total > own && (total - own > Math.Max(5, 0.05 * total) || (!part && !clean));

        public static DateTime HourStartUtc(long hour) => DateTimeOffset.FromUnixTimeSeconds(hour * HourSeconds).UtcDateTime;

        /// <summary>A kill time as UTC HH:mm:ss for the log, or "-" for a bucket's unset first/last.</summary>
        private static string Clock(long unix)
            => unix == long.MaxValue || unix == long.MinValue ? "-" : DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        /// <summary>Lowers <paramref name="target"/> to <paramref name="value"/> if it is smaller, safely against other threads.</summary>
        private static void InterlockedMin(ref long target, long value)
        {
            var seen = Interlocked.Read(ref target);
            while (value < seen)
            {
                var prev = Interlocked.CompareExchange(ref target, value, seen);
                if (prev == seen)
                    return;
                seen = prev;
            }
        }

        /// <summary>Raises <paramref name="target"/> to <paramref name="value"/> if it is larger, safely against other threads.</summary>
        private static void InterlockedMax(ref long target, long value)
        {
            var seen = Interlocked.Read(ref target);
            while (value > seen)
            {
                var prev = Interlocked.CompareExchange(ref target, value, seen);
                if (prev == seen)
                    return;
                seen = prev;
            }
        }
    }
}
