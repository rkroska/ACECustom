using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Text;

using Microsoft.EntityFrameworkCore;

using ACE.Database.Models.Shard;
using ACE.Entity.Enum.Properties;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Read-only queries for the admin Economy page: supply per currency over time (economy_supply_daily), what creates
    /// and removes each currency (economy_flow_daily, and the pyreal ledger's hourly rows for pyreals), and the text
    /// prompt an admin can hand to Claude.
    ///
    /// Faucets and sinks are NET per source and detail: a vendor that takes 250,000 pyreals and hands back a 250,000
    /// trade note is neither. Sources that only move currency (bank, trade, give, ground) are reported as "internal".
    /// </summary>
    public static class EconomyReports
    {
        public const int MaxDays = 365;
        private const int DetailLimit = 50;
        private const int CacheSeconds = 60;

        // ---- DTOs ------------------------------------------------------------------------------------------------

        public class Supply
        {
            public string Day { get; set; }
            public long Banked { get; set; }
            public long Items { get; set; }
            public long Total { get; set; }
            public long Holders { get; set; }
            public long ActiveSupply { get; set; }
            public long ActiveHolders { get; set; }
            public long Top1PctAmount { get; set; }
            public long LargestHolder { get; set; }
            public long AdminSupply { get; set; }
            /// <summary>Net recorded flow since the previous snapshot; null when that period was not fully recorded.</summary>
            public long? FlowNet { get; set; }
            /// <summary>Change in supply since the previous snapshot that the recorded flows do not account for.</summary>
            public long? Unexplained { get; set; }
        }

        public class Change
        {
            public long Amount { get; set; }
            public double Pct { get; set; }
        }

        public class Flow
        {
            public int Days { get; set; }
            public long Faucets { get; set; }
            public long Sinks { get; set; }
            public long Net { get; set; }
            public long Internal { get; set; }
            public string FirstDay { get; set; }
        }

        public class SupplyPoint
        {
            public string Day { get; set; }
            public long Banked { get; set; }
            public long Items { get; set; }
            public long Total { get; set; }
            public long ActiveSupply { get; set; }
            public long? Unexplained { get; set; }
        }

        public class FlowPoint
        {
            public string Day { get; set; }
            public long Faucets { get; set; }
            public long Sinks { get; set; }
        }

        public class CurrencySummary
        {
            public string Key { get; set; }
            public string Name { get; set; }
            public Supply Supply { get; set; } = new();
            public Change Change7d { get; set; }
            public Change Change30d { get; set; }
            public Flow Flow { get; set; } = new();
            public double? WeeklyPct { get; set; }
            public string WeeklyPctBasis { get; set; } = "none";
            public string Status { get; set; } = "unknown";
            public List<SupplyPoint> Series { get; set; } = new();
            public List<FlowPoint> FlowSeries { get; set; } = new();
        }

        public class Overview
        {
            public DateTime GeneratedUtc { get; set; }
            public int Days { get; set; }
            public bool FlowTracking { get; set; }
            public DateTime? FlowTrackingSinceUtc { get; set; }
            public bool PyrealLedger { get; set; }
            public DateTime? LastSnapshotUtc { get; set; }
            public double TargetMinWeeklyPct { get; set; }
            public double TargetMaxWeeklyPct { get; set; }
            public List<CurrencySummary> Currencies { get; set; } = new();
        }

        public class SourceRow
        {
            public string Source { get; set; }
            public string Kind { get; set; }
            public long AmountIn { get; set; }
            public long AmountOut { get; set; }
            public long Events { get; set; }
            public long Characters { get; set; }
        }

        public class DetailRow
        {
            public string Source { get; set; }
            public string DetailKey { get; set; }
            public string DetailName { get; set; }
            public long AmountIn { get; set; }
            public long AmountOut { get; set; }
            public long Events { get; set; }
            public long Characters { get; set; }
        }

        public class EarnerRow
        {
            public uint CharId { get; set; }
            public string CharName { get; set; }
            public uint AccountId { get; set; }
            public long AmountIn { get; set; }
            public long AmountOut { get; set; }
            public long Net { get; set; }
        }

        public class HolderRow
        {
            public uint CharId { get; set; }
            public string CharName { get; set; }
            public uint AccountId { get; set; }
            public long Banked { get; set; }
            public DateTime? LastLoginUtc { get; set; }
        }

        public class SettingRow
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public string Description { get; set; }
        }

        public class CurrencyDetail
        {
            public CurrencySummary Summary { get; set; }
            public List<SourceRow> Sources { get; set; } = new();
            public List<DetailRow> Faucets { get; set; } = new();
            public List<DetailRow> Sinks { get; set; } = new();
            public List<EarnerRow> Earners { get; set; } = new();
            public List<HolderRow> Holders { get; set; } = new();
            public List<SettingRow> Settings { get; set; } = new();
            public List<string> Notes { get; set; } = new();
        }

        public class PromptResult
        {
            public string Text { get; set; }
            public string Currency { get; set; }
            public int Days { get; set; }
        }

        // ---- helpers ---------------------------------------------------------------------------------------------

        public static int ClampDays(int days) => Math.Clamp(days <= 0 ? 30 : days, 1, MaxDays);

        private static string DayText(DateTime day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private static List<T> Query<T>(string sql, Action<DbCommand> bind, Func<DbDataReader, T> map)
        {
            using var ctx = new ShardDbContext();
            var con = ctx.Database.GetDbConnection();
            if (con.State != ConnectionState.Open)
                con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 30;
            bind?.Invoke(cmd);

            var rows = new List<T>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                rows.Add(map(reader));
            return rows;
        }

        private static void Param(DbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        private static long L(DbDataReader r, int i) => r.IsDBNull(i) ? 0 : Convert.ToInt64(r.GetValue(i));

        private static string S(DbDataReader r, int i) => r.IsDBNull(i) ? "" : Convert.ToString(r.GetValue(i));

        private static bool TableExists(string table) =>
            Query("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = @t", c => Param(c, "@t", table), r => L(r, 0)).FirstOrDefault() > 0;

        private static readonly string InternalList = string.Join(",", EconomyLedger.InternalSources.Select(s => $"'{s}'"));

        /// <summary>The pyreal ledger's item rows key their detail as "itemWcid:detail"; bank rows as "detail".</summary>
        private const string PyrealDetail = "CASE WHEN `kind` = 1 THEN `detail_key` ELSE SUBSTRING(`detail_key`, LOCATE(':', `detail_key`) + 1) END";

        /// <summary>
        /// Pyreal ledger rows that are flows: bank and item rows, not the per-item sale rows, not balances minted by
        /// @copychar, and not the bookkeeping rows for what characters already held when tracking began.
        /// </summary>
        private static readonly string PyrealRows = $"`kind` IN (1, 2, 3) AND `source` NOT IN ('{PyrealLedger.SrcCopyChar}', '{PyrealLedger.SrcOpening}')";

        // ---- flow groups -----------------------------------------------------------------------------------------

        internal sealed class FlowGroup
        {
            public DateTime Day;
            public string Source;
            public string DetailKey;
            public string DetailName;
            public long AmountIn;
            public long AmountOut;
            public long Events;
        }

        private static readonly object cacheLock = new object();
        private static readonly Dictionary<int, (DateTime at, Dictionary<string, List<FlowGroup>> data)> flowCache = new();

        /// <summary>
        /// Per currency: one row per day, source and detail (summed over characters, bank and items). Internal sources
        /// are collapsed to one row per day and source. Cached for a minute, because the overview reads every currency.
        /// </summary>
        private static Dictionary<string, List<FlowGroup>> LoadFlowGroups(int days)
        {
            lock (cacheLock)
            {
                if (flowCache.TryGetValue(days, out var hit) && (DateTime.UtcNow - hit.at).TotalSeconds < CacheSeconds)
                    return hit.data;
            }

            var since = DateTime.UtcNow.Date.AddDays(-(days - 1));
            var data = EconomyLedger.Currencies.ToDictionary(c => c.Key, _ => new List<FlowGroup>(), StringComparer.OrdinalIgnoreCase);

            if (TableExists("economy_flow_daily"))
            {
                var rows = Query(
                    $"SELECT `currency`, `day_utc`, `source`, CASE WHEN `source` IN ({InternalList}) THEN '' ELSE `detail_key` END AS dk, MAX(`detail_name`), " +
                    "SUM(`amount_in`), SUM(`amount_out`), SUM(`events`) FROM `economy_flow_daily` WHERE `day_utc` >= @since GROUP BY `currency`, `day_utc`, `source`, dk",
                    c => Param(c, "@since", since),
                    r => (currency: S(r, 0), group: new FlowGroup { Day = Convert.ToDateTime(r.GetValue(1)).Date, Source = S(r, 2), DetailKey = S(r, 3), DetailName = S(r, 4), AmountIn = L(r, 5), AmountOut = L(r, 6), Events = L(r, 7) }));

                foreach (var (currency, group) in rows)
                    if (data.TryGetValue(currency, out var list))
                        list.Add(group);
            }

            if (TableExists("pyreal_ledger_hourly"))
            {
                data["pyreals"] = Query(
                    $"SELECT DATE(`hour_utc`) AS d, `source`, CASE WHEN `source` IN ({InternalList}) THEN '' ELSE {PyrealDetail} END AS dk, MAX(`detail_name`), " +
                    $"SUM(`amount_in`), SUM(`amount_out`), SUM(`events`) FROM `pyreal_ledger_hourly` WHERE {PyrealRows} AND `hour_utc` >= @since{PyrealLedgerReports.NotStaffSql()} GROUP BY d, `source`, dk",
                    c => Param(c, "@since", since),
                    r => new FlowGroup { Day = Convert.ToDateTime(r.GetValue(0)).Date, Source = S(r, 1), DetailKey = S(r, 2), DetailName = S(r, 3), AmountIn = L(r, 4), AmountOut = L(r, 5), Events = L(r, 6) });
            }

            lock (cacheLock)
            {
                // nothing older than the cache time is kept, so stepping through windows cannot pile data up
                foreach (var stale in flowCache.Where(kv => (DateTime.UtcNow - kv.Value.at).TotalSeconds >= CacheSeconds).Select(kv => kv.Key).ToList())
                    flowCache.Remove(stale);

                flowCache[days] = (DateTime.UtcNow, data);
            }

            return data;
        }

        /// <summary>
        /// Faucets and sinks from flow rows: within each group of rows (one source and detail) the net decides the side.
        /// Internal sources count toward neither. Pure; unit tested.
        /// </summary>
        internal static (long faucets, long sinks, long moved) NetFlow(IEnumerable<FlowGroup> groups)
        {
            long faucets = 0, sinks = 0, movedIn = 0, movedOut = 0;

            foreach (var g in groups.GroupBy(x => (Source: EconomyLedger.NetSource(x.Source), x.DetailKey)))
            {
                var amountIn = g.Sum(x => x.AmountIn);
                var amountOut = g.Sum(x => x.AmountOut);

                if (EconomyLedger.IsInternalSource(g.Key.Source))
                {
                    movedIn += amountIn;
                    movedOut += amountOut;
                    continue;
                }

                var net = amountIn - amountOut;
                if (net > 0)
                    faucets += net;
                else
                    sinks += -net;
            }

            // a move shows up once leaving and once arriving (often under two sources), so count one side
            return (faucets, sinks, Math.Max(movedIn, movedOut));
        }

        // ---- supply ----------------------------------------------------------------------------------------------

        private sealed class SupplyDay
        {
            public DateTime Day;
            /// <summary>When the snapshot was actually taken; gaps are measured from this, not from the calendar day.</summary>
            public DateTime TakenUtc;
            public Supply Supply;
        }

        /// <summary>Two snapshots closer together than this are not compared: one day's noise times seven is not a trend.</summary>
        internal const double MinTrendGapDays = 2;

        private static Dictionary<string, List<SupplyDay>> LoadSupply(int days)
        {
            var data = EconomyLedger.Currencies.ToDictionary(c => c.Key, _ => new List<SupplyDay>(), StringComparer.OrdinalIgnoreCase);
            if (!TableExists("economy_supply_daily"))
                return data;

            // far enough back for the 30 day comparison whatever window the page asked for
            var since = DateTime.UtcNow.Date.AddDays(-Math.Max(days, 40));

            var rows = Query(
                "SELECT `currency`, `day_utc`, `banked`, `items`, `holders`, `active_supply`, `active_holders`, `top1pct_amount`, `largest_holder`, `admin_supply`, `flow_net`, `taken_utc` " +
                "FROM `economy_supply_daily` WHERE `day_utc` >= @since ORDER BY `day_utc`",
                c => Param(c, "@since", since),
                r =>
                {
                    var day = Convert.ToDateTime(r.GetValue(1)).Date;
                    return (currency: S(r, 0), row: new SupplyDay
                    {
                        Day = day,
                        TakenUtc = r.IsDBNull(11) ? day : Convert.ToDateTime(r.GetValue(11)),
                        Supply = new Supply
                        {
                            Day = DayText(day), Banked = L(r, 2), Items = L(r, 3), Total = L(r, 2) + L(r, 3), Holders = L(r, 4), ActiveSupply = L(r, 5),
                            ActiveHolders = L(r, 6), Top1PctAmount = L(r, 7), LargestHolder = L(r, 8), AdminSupply = L(r, 9),
                            FlowNet = r.IsDBNull(10) ? null : Convert.ToInt64(r.GetValue(10)),
                        },
                    });
                });

            foreach (var (currency, row) in rows)
            {
                if (!data.TryGetValue(currency, out var list))
                    continue;

                // rows arrive oldest first, so the last one in the list is the snapshot this one's net is measured from
                if (list.Count > 0)
                    row.Supply.Unexplained = Unexplained(list[^1].Supply.Total, row.Supply.Total, row.Supply.FlowNet);

                list.Add(row);
            }

            return data;
        }

        /// <summary>
        /// The part of a supply change the recorded flows do not account for: positive = currency appeared with no
        /// record, negative = it vanished. Null when the period was not fully recorded. Pure; unit tested.
        /// </summary>
        internal static long? Unexplained(long previousTotal, long total, long? flowNet) =>
            flowNet.HasValue ? (total - previousTotal) - flowNet.Value : null;

        /// <summary>
        /// Percent change per week between two supply totals <paramref name="gapDays"/> apart, or null when it cannot
        /// be worked out. Pure; unit tested.
        /// </summary>
        internal static double? WeeklyPct(long earlier, long later, double gapDays)
        {
            if (earlier <= 0 || gapDays <= 0)
                return null;

            return (later - earlier) * 100.0 / earlier * (7.0 / gapDays);
        }

        /// <summary>Where a weekly percent change sits against the target band. Pure; unit tested.</summary>
        internal static string StatusOf(double? weeklyPct, double minPct, double maxPct)
        {
            if (!weeklyPct.HasValue)
                return "unknown";
            if (weeklyPct.Value > maxPct)
                return "inflating";
            if (weeklyPct.Value < minPct)
                return "deflating";
            return "healthy";
        }

        private static Change ChangeSince(List<SupplyDay> supply, SupplyDay latest, int daysBack)
        {
            // the newest snapshot at least daysBack old, but not so old that it measures a different period
            var reference = supply.LastOrDefault(s => s.Day <= latest.Day.AddDays(-daysBack) && s.Day >= latest.Day.AddDays(-daysBack - 1));
            if (reference == null)
                return null;

            var amount = latest.Supply.Total - reference.Supply.Total;
            return new Change { Amount = amount, Pct = reference.Supply.Total > 0 ? amount * 100.0 / reference.Supply.Total : 0 };
        }

        private static CurrencySummary BuildSummary(EconomyLedger.Currency currency, int days, List<SupplyDay> supply, List<FlowGroup> flows)
        {
            var today = DateTime.UtcNow.Date;
            var windowStart = today.AddDays(-(days - 1));
            var summary = new CurrencySummary { Key = currency.Key, Name = currency.Name };

            summary.Series = supply.Where(s => s.Day >= windowStart)
                .Select(s => new SupplyPoint { Day = s.Supply.Day, Banked = s.Supply.Banked, Items = s.Supply.Items, Total = s.Supply.Total, ActiveSupply = s.Supply.ActiveSupply, Unexplained = s.Supply.Unexplained })
                .ToList();

            var latest = supply.LastOrDefault();
            if (latest != null)
            {
                summary.Supply = latest.Supply;
                summary.Change7d = ChangeSince(supply, latest, 7);
                summary.Change30d = ChangeSince(supply, latest, 30);
            }

            var (faucets, sinks, moved) = NetFlow(flows);
            summary.Flow = new Flow
            {
                Days = days, Faucets = faucets, Sinks = sinks, Net = faucets - sinks, Internal = moved,
                FirstDay = flows.Count > 0 ? DayText(flows.Min(f => f.Day)) : null,
            };

            summary.FlowSeries = flows.GroupBy(f => f.Day).OrderBy(g => g.Key).Select(g =>
            {
                var (f, s, _) = NetFlow(g);
                return new FlowPoint { Day = DayText(g.Key), Faucets = f, Sinks = s };
            }).ToList();

            // weekly trend: two snapshots when there are two, otherwise recorded flows against the latest supply
            if (latest != null)
            {
                // about a week back if there is one, otherwise the oldest snapshot in the last week that is far enough away
                var reference = supply.LastOrDefault(s => s.Day <= latest.Day.AddDays(-7) && s.Day >= latest.Day.AddDays(-11))
                                ?? supply.FirstOrDefault(s => s.Day >= latest.Day.AddDays(-7) && (latest.TakenUtc - s.TakenUtc).TotalDays >= MinTrendGapDays);

                if (reference != null)
                {
                    summary.WeeklyPct = WeeklyPct(reference.Supply.Total, latest.Supply.Total, (latest.TakenUtc - reference.TakenUtc).TotalDays);
                    if (summary.WeeklyPct.HasValue)
                        summary.WeeklyPctBasis = "snapshots";
                }

                if (!summary.WeeklyPct.HasValue && flows.Count > 0 && latest.Supply.Total > 0)
                {
                    var from = today.AddDays(-6);
                    var firstDay = flows.Min(f => f.Day);
                    if (firstDay > from)
                        from = firstDay;

                    var (f, s, _) = NetFlow(flows.Where(x => x.Day >= from));
                    // today is only part of a day
                    var covered = Math.Max(1.0 / 24, (DateTime.UtcNow - from).TotalDays);
                    summary.WeeklyPct = (f - s) * 100.0 / latest.Supply.Total * (7.0 / covered);
                    summary.WeeklyPctBasis = "flows";
                }
            }

            summary.Status = StatusOf(summary.WeeklyPct, ServerConfig.economy_target_weekly_min_pct.Value, ServerConfig.economy_target_weekly_max_pct.Value);
            return summary;
        }

        // ---- page queries ----------------------------------------------------------------------------------------

        public static Overview GetOverview(int days)
        {
            days = ClampDays(days);
            var supply = LoadSupply(days);
            var flows = LoadFlowGroups(days);

            return new Overview
            {
                GeneratedUtc = DateTime.UtcNow,
                Days = days,
                FlowTracking = EconomyLedger.FlowTracking,
                FlowTrackingSinceUtc = EconomyLedger.FlowTrackingSinceUtc,
                PyrealLedger = PyrealLedger.IsInitialized,
                LastSnapshotUtc = EconomyLedger.LastSnapshotUtc,
                TargetMinWeeklyPct = ServerConfig.economy_target_weekly_min_pct.Value,
                TargetMaxWeeklyPct = ServerConfig.economy_target_weekly_max_pct.Value,
                Currencies = EconomyLedger.Currencies.Select(c => BuildSummary(c, days, supply[c.Key], flows[c.Key])).ToList(),
            };
        }

        private static readonly Dictionary<(string key, int days), (DateTime at, CurrencyDetail data)> detailCache = new();

        /// <summary>Cached for a minute: three grouped queries and a pass over every character, and the prompt asks for all currencies.</summary>
        public static CurrencyDetail GetCurrency(string key, int days)
        {
            var currency = EconomyLedger.Find(key);
            if (currency == null)
                return null;

            days = ClampDays(days);

            lock (cacheLock)
            {
                if (detailCache.TryGetValue((currency.Key, days), out var hit) && (DateTime.UtcNow - hit.at).TotalSeconds < CacheSeconds)
                    return hit.data;
            }

            var detail = BuildCurrency(currency, days);

            lock (cacheLock)
            {
                foreach (var stale in detailCache.Where(kv => (DateTime.UtcNow - kv.Value.at).TotalSeconds >= CacheSeconds).Select(kv => kv.Key).ToList())
                    detailCache.Remove(stale);

                detailCache[(currency.Key, days)] = (DateTime.UtcNow, detail);
            }

            return detail;
        }

        private static CurrencyDetail BuildCurrency(EconomyLedger.Currency currency, int days)
        {
            var since = DateTime.UtcNow.Date.AddDays(-(days - 1));
            var detail = new CurrencyDetail { Summary = BuildSummary(currency, days, LoadSupply(days)[currency.Key], LoadFlowGroups(days)[currency.Key]) };

            // table, filter and expressions differ between the pyreal ledger and the economy ledger; the shapes do not
            var pyreal = currency.FromPyrealLedger;
            var table = pyreal ? "pyreal_ledger_hourly" : "economy_flow_daily";
            var where = pyreal ? $"{PyrealRows} AND `hour_utc` >= @since{PyrealLedgerReports.NotStaffSql()}" : "`currency` = @currency AND `day_utc` >= @since";
            var detailKey = pyreal ? PyrealDetail : "`detail_key`";

            void Bind(DbCommand c)
            {
                Param(c, "@since", since);
                if (!pyreal)
                    Param(c, "@currency", currency.Key);
            }

            if (TableExists(table))
            {
                detail.Sources = Query(
                    $"SELECT `source`, SUM(`amount_in`), SUM(`amount_out`), SUM(`events`), COUNT(DISTINCT `char_id`) FROM `{table}` WHERE {where} GROUP BY `source` ORDER BY SUM(`amount_in`) + SUM(`amount_out`) DESC",
                    Bind,
                    r => new SourceRow { Source = S(r, 0), Kind = EconomyLedger.IsInternalSource(S(r, 0)) ? "internal" : "flow", AmountIn = L(r, 1), AmountOut = L(r, 2), Events = L(r, 3), Characters = L(r, 4) });

                var details = Query(
                    $"SELECT {EconomyLedger.NetSourceSql} AS src, {detailKey} AS dk, MAX(`detail_name`), SUM(`amount_in`), SUM(`amount_out`), SUM(`events`), COUNT(DISTINCT `char_id`) FROM `{table}` " +
                    $"WHERE {where} AND `source` NOT IN ({InternalList}) GROUP BY src, dk",
                    Bind,
                    r => new DetailRow { Source = S(r, 0), DetailKey = S(r, 1), DetailName = S(r, 2), AmountIn = L(r, 3), AmountOut = L(r, 4), Events = L(r, 5), Characters = L(r, 6) });

                // loot rows carry no id, and the stored name is just one corpse out of many
                foreach (var d in details.Where(d => d.Source == PyrealLedger.SrcLoot && string.IsNullOrEmpty(d.DetailKey)))
                    d.DetailName = "creature corpses (all)";

                detail.Faucets = details.Where(d => d.AmountIn > d.AmountOut).OrderByDescending(d => d.AmountIn - d.AmountOut).Take(DetailLimit).ToList();
                detail.Sinks = details.Where(d => d.AmountOut > d.AmountIn).OrderByDescending(d => d.AmountOut - d.AmountIn).Take(DetailLimit).ToList();

                detail.Earners = Query(
                    $"SELECT `char_id`, MAX(`char_name`), MAX(`account_id`), SUM(`amount_in`), SUM(`amount_out`) FROM `{table}` " +
                    $"WHERE {where} AND `source` NOT IN ({InternalList}) GROUP BY `char_id` ORDER BY SUM(`amount_in`) - SUM(`amount_out`) DESC LIMIT {DetailLimit}",
                    Bind,
                    r => new EarnerRow { CharId = Convert.ToUInt32(r.GetValue(0)), CharName = S(r, 1), AccountId = Convert.ToUInt32(L(r, 2)), AmountIn = L(r, 3), AmountOut = L(r, 4), Net = L(r, 3) - L(r, 4) });
            }

            detail.Holders = GetHolders(currency, 25);
            detail.Settings = SettingsFor(currency);
            detail.Notes = NotesFor(currency, detail.Summary);
            return detail;
        }

        /// <summary>Largest banked balances right now, from the characters in memory (staff excluded).</summary>
        private static List<HolderRow> GetHolders(EconomyLedger.Currency currency, int limit)
        {
            var rows = new List<HolderRow>();

            foreach (var player in PlayerManager.GetAllPlayers())
            {
                if ((player.Account?.AccessLevel ?? 0) > 0)
                    continue;

                long banked = 0;
                foreach (var property in currency.Bank)
                    banked += Math.Max(0, player.GetProperty(property) ?? 0);

                if (banked <= 0)
                    continue;

                var login = player.GetProperty(PropertyFloat.LoginTimestamp);
                rows.Add(new HolderRow
                {
                    CharId = player.Guid.Full, CharName = player.Name, AccountId = player.Account?.AccountId ?? 0, Banked = banked,
                    LastLoginUtc = login.HasValue && login.Value > 0 ? DateTimeOffset.FromUnixTimeSeconds((long)login.Value).UtcDateTime : null,
                });
            }

            return rows.OrderByDescending(r => r.Banked).Take(limit).ToList();
        }

        private static SettingRow Setting<T>(string name, ConfigProperty<T> property) where T : notnull =>
            new SettingRow { Name = name, Value = Convert.ToString(property.Value, CultureInfo.InvariantCulture), Description = property.Description };

        /// <summary>The live server settings that scale this currency's rewards.</summary>
        private static List<SettingRow> SettingsFor(EconomyLedger.Currency currency)
        {
            var rows = new List<SettingRow>();

            switch (currency.Key)
            {
                case "luminance":
                    rows.Add(Setting(nameof(ServerConfig.luminance_modifier), ServerConfig.luminance_modifier));
                    rows.Add(Setting(nameof(ServerConfig.quest_lum_modifier), ServerConfig.quest_lum_modifier));
                    break;
                case "enlightened-coins":
                case "weakly-enlightened-coins":
                    rows.Add(Setting(nameof(ServerConfig.coin_clap_mult), ServerConfig.coin_clap_mult));
                    rows.Add(Setting(nameof(ServerConfig.coin_reward_mult), ServerConfig.coin_reward_mult));
                    break;
            }

            rows.Add(Setting(nameof(ServerConfig.economy_target_weekly_min_pct), ServerConfig.economy_target_weekly_min_pct));
            rows.Add(Setting(nameof(ServerConfig.economy_target_weekly_max_pct), ServerConfig.economy_target_weekly_max_pct));
            return rows;
        }

        private static List<string> NotesFor(EconomyLedger.Currency currency, CurrencySummary summary)
        {
            var notes = new List<string>
            {
                "Supply is one snapshot per day (UTC). It counts banked balances and currency items in character packs; items in housing chests or on the ground are not counted.",
                $"Staff characters are left out of supply and holders. Active means logged in within the last {EconomyLedger.ActiveDays} days.",
                "Faucets and sinks are net per source and detail. Bank, transfer, trade, give and ground rows only move currency and are listed as internal.",
            };

            if (currency.FromPyrealLedger)
            {
                notes.Add("Pyreal flows come from the pyreal ledger (banked pyreals plus coins, trade notes, peas and boxes of notes at face value). Staff accounts and balances created by @copychar are left out. Unexplained is not worked out for pyreals; the Pyreal Ledger page has its own checks.");
                notes.Add("Dressing Room and Forge fees were recorded as Unattributed until the build of 2026-10-12 labeled them Fee; an Unattributed pyreal sink in older days is those fees.");
            }
            else
            {
                notes.Add(EconomyLedger.FlowTracking
                    ? $"Flow tracking for this currency began {EconomyLedger.FlowTrackingSinceUtc:yyyy-MM-dd HH:mm} UTC; earlier days have no faucet or sink data."
                    : "Flow tracking is off, so no faucets or sinks are being recorded (ServerConfig economy_ledger, or @economyledger on).");

                if (currency.ItemsUseStructure)
                    notes.Add("Each use of a key on a chest or door is a KeyUse sink, labeled with what was unlocked. Key items moving between characters and the world are counted as full keys, and the supply snapshot counts remaining uses, so Unexplained is not worked out for keys.");
                else
                    notes.Add("Unexplained = how much the supply changed since the previous snapshot, minus everything recorded in between. Positive means currency appeared with no record. " +
                              "Small values are timing noise (the pack count takes a minute or two); deleted characters show as a negative. It is blank when recording was off for part of the period.");
            }

            if (currency.Key == "luminance")
                notes.Add("Kill luminance shared to a fellowship member on another landblock thread is listed under Fellowship with no creature.");

            if (summary.WeeklyPctBasis == "flows")
                notes.Add("The weekly trend is estimated from recorded flows because there are not yet two supply snapshots to compare.");
            else if (summary.WeeklyPctBasis == "none")
                notes.Add("No weekly trend yet: it needs two supply snapshots, or one snapshot plus recorded flows.");

            return notes;
        }

        // ---- Claude prompt ---------------------------------------------------------------------------------------

        private static string N(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

        private static string Pct(double? value) => value.HasValue ? value.Value.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "%" : "unknown";

        /// <summary>
        /// A self-contained brief for Claude: the target, the numbers for one currency or all of them, the largest
        /// faucets and sinks with the ids needed to find them in the world database, and what to deliver.
        /// </summary>
        public static PromptResult GetPrompt(string key, int days)
        {
            days = ClampDays(days);
            var all = string.IsNullOrEmpty(key) || string.Equals(key, "all", StringComparison.OrdinalIgnoreCase);
            var currencies = all ? EconomyLedger.Currencies.ToList() : new List<EconomyLedger.Currency> { EconomyLedger.Find(key) };
            if (currencies.Any(c => c == null))
                return null;

            var min = ServerConfig.economy_target_weekly_min_pct.Value;
            var max = ServerConfig.economy_target_weekly_max_pct.Value;
            var top = all ? 12 : 30;
            var sb = new StringBuilder();

            sb.Append("# Economy review request\n\n");
            sb.Append("You are helping the owner of an Asheron's Call emulator server (ACE, C#, repository ACECustom) balance its economy. ");
            sb.Append("Below is live data exported from the server's admin Economy page. Investigate where the imbalance comes from and recommend specific quest, NPC, vendor or setting changes.\n\n");

            sb.Append("## Goal\n\n");
            sb.Append($"- Keep each currency's total supply between {Pct(min)} and {Pct(max)} per week: roughly flat, or slightly scarce. Above that band is inflating, below it is deflating.\n");
            sb.Append("- Prefer the smallest change that works. Prefer adding or raising sinks players want over cutting rewards players already rely on, unless one source is clearly out of line.\n");
            sb.Append("- Judge by supply held by ACTIVE characters as well as total supply: dormant hoards do not drive prices.\n\n");

            sb.Append("## How to read the data\n\n");
            sb.Append($"- Window: the last {days} days, generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC. Supply is a daily snapshot; faucets and sinks are recorded flows.\n");
            sb.Append("- Faucet = creates currency, sink = removes it. Both are NET per source and detail.\n");
            sb.Append("- Source says what kind of code path it was. Detail id is a weenie class id (wcid) for these sources: Emote and GiveNPC (the NPC), VendorBuy and VendorSell (the vendor), Kill (the creature), Chest (the container), Recipe (the recipe). For Command it is the chat command name.\n");
            sb.Append("- Emote covers quest rewards and NPC turn-ins, net of any currency item handed to that NPC. Fee is a Dressing Room or Forge fee. KeyUse is one key use spent on a chest. Casino is blackjack, net of bets. Clap is the /clap coin command.\n");
            sb.Append("- Unattributed means a code path nobody has labeled yet. For pyreals before 2026-10-12 it is mostly Dressing Room and Forge fees, not a problem.\n");
            sb.Append("- Unexplained = supply change between two snapshots that no recorded flow accounts for. A large positive value means an unlabeled code path, a database edit or a dupe; investigate that before balancing rewards.\n");
            sb.Append("- Characters = how many different characters used that source. A large amount from very few characters suggests an exploit or a bot rather than a balance problem.\n");
            sb.Append($"- Flow tracking for currencies other than pyreals: {(EconomyLedger.FlowTracking ? "on since " + (EconomyLedger.FlowTrackingSinceUtc?.ToString("yyyy-MM-dd HH:mm") ?? "?") + " UTC" : "OFF")}. ");
            sb.Append("A window that starts before that date understates faucets and sinks.\n\n");

            foreach (var currency in currencies)
            {
                var detail = GetCurrency(currency.Key, days);
                var s = detail.Summary;

                sb.Append($"## {currency.Name}\n\n");

                if (s.Supply.Day == null)
                    sb.Append("- Supply: no snapshot yet.\n");
                else
                {
                    sb.Append($"- Supply on {s.Supply.Day}: {N(s.Supply.Total)} total ({N(s.Supply.Banked)} banked, {N(s.Supply.Items)} in items), held by {N(s.Supply.Holders)} characters.\n");
                    sb.Append($"- Active characters hold {N(s.Supply.ActiveSupply)} across {N(s.Supply.ActiveHolders)} holders. The richest 1% of holders hold {N(s.Supply.Top1PctAmount)}; the largest single holder has {N(s.Supply.LargestHolder)}.\n");
                }

                sb.Append($"- Weekly trend: {Pct(s.WeeklyPct)} (basis: {s.WeeklyPctBasis}). Status against the target: {s.Status.ToUpperInvariant()}.\n");
                if (s.Supply.Unexplained.HasValue)
                    sb.Append($"- Unexplained since the previous snapshot: {N(s.Supply.Unexplained.Value)} (supply change minus recorded flows; positive = appeared with no record, small values are timing noise).\n");
                if (s.Change7d != null)
                    sb.Append($"- Change over 7 days: {N(s.Change7d.Amount)} ({Pct(s.Change7d.Pct)}).\n");
                if (s.Change30d != null)
                    sb.Append($"- Change over 30 days: {N(s.Change30d.Amount)} ({Pct(s.Change30d.Pct)}).\n");

                if (s.Flow.FirstDay == null)
                    sb.Append("- Flows: none recorded in this window.\n\n");
                else
                {
                    sb.Append($"- Flows since {s.Flow.FirstDay}: faucets {N(s.Flow.Faucets)}, sinks {N(s.Flow.Sinks)}, net {N(s.Flow.Net)}. Moved between characters or bank without being created: {N(s.Flow.Internal)}.\n");

                    var earned = detail.Earners.Where(e => e.Net > 0).ToList();
                    if (earned.Count > 0 && s.Flow.Faucets > 0)
                        sb.Append($"- Concentration: the top {Math.Min(10, earned.Count)} earning characters took a net {N(earned.Take(10).Sum(e => e.Net))}, against total faucets of {N(s.Flow.Faucets)}.\n");
                    sb.Append('\n');

                    AppendTable(sb, $"Top faucets ({currency.Name})", detail.Faucets.Take(top), d => d.AmountIn - d.AmountOut);
                    AppendTable(sb, $"Top sinks ({currency.Name})", detail.Sinks.Take(top), d => d.AmountOut - d.AmountIn);
                }

                var settings = detail.Settings.Where(x => !x.Name.StartsWith("economy_target")).ToList();
                if (settings.Count > 0)
                {
                    sb.Append("Reward settings in effect (live values, changed in game with @modifydouble):\n\n");
                    foreach (var setting in settings)
                        sb.Append($"- {setting.Name} = {setting.Value}\n");
                    sb.Append('\n');
                }
            }

            sb.Append("## What I need from you\n\n");
            sb.Append("1. For each currency that is outside the target band, name the sources responsible and say how confident you are. Separate \"too generous by design\" from \"a few characters abusing it\".\n");
            sb.Append("2. Look up the top faucets and sinks before recommending anything. In the ACE world database, an NPC's rewards are in weenie_properties_emote and weenie_properties_emote_action for its wcid; vendor prices are the weenie's buy and sell price floats plus the item Value; creature luminance is on the creature weenie. In the ACECustom repository, world content lives under Database/Updates/World and server code under Source/ACE.Server.\n");
            sb.Append("3. Recommend concrete changes, each with: the wcid or setting, the current value, the proposed value, the expected change in currency per week, and which players it affects.\n");
            sb.Append("4. Add up the expected effect and show the weekly trend you expect afterwards, against the target band.\n");
            sb.Append("5. List what the data cannot tell you and what you would measure next.\n\n");
            sb.Append("Rules: read-only investigation first; do not change code or data without asking. Verify claims against the real database rather than assuming retail values, because this server is heavily customised. Any text shown in the game client must be plain ASCII.\n");

            return new PromptResult { Text = sb.ToString(), Currency = all ? "all" : currencies[0].Key, Days = days };
        }

        private static void AppendTable(StringBuilder sb, string title, IEnumerable<DetailRow> rows, Func<DetailRow, long> net)
        {
            var list = rows.ToList();
            if (list.Count == 0)
                return;

            sb.Append($"{title}:\n\n| Source | Detail id | Detail | Net | Events | Characters |\n|---|---|---|---:|---:|---:|\n");
            foreach (var row in list)
                sb.Append($"| {row.Source} | {Cell(row.DetailKey)} | {Cell(row.DetailName)} | {N(net(row))} | {N(row.Events)} | {N(row.Characters)} |\n");
            sb.Append('\n');
        }

        private static string Cell(string text) => string.IsNullOrEmpty(text) ? "-" : text.Replace("|", "/").Replace("\r", " ").Replace("\n", " ");
    }
}
