using System;
using System.Collections.Generic;
using System.Linq;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Pyreal ledger reports added after the first days of live data (owner 2026-10-12). The dupe flags found nothing,
    /// while the things worth a look were all about rate and concentration: one character taking 24 billion from one
    /// NPC in an hour, one account making 94% of all vendor sales, 200 billion crossing accounts through one hub.
    ///
    /// - Suspect signals: earnings per hour, share of a single NPC or vendor, hours active.
    /// - Transfers between accounts.
    /// - Rates on the NPC and vendor lists.
    /// - Balances created by @copychar.
    /// Read-only. Staff accounts are left out of all of them.
    /// </summary>
    public static partial class PyrealLedgerReports
    {
        // ---- shared -----------------------------------------------------------------------------------------------

        /// <summary>Sources that are not a character earning or spending: moves, bookkeeping rows and admin copies.</summary>
        internal static readonly string NotEarnedList = string.Join(",",
            EconomyLedger.InternalSources.Append(PyrealLedger.SrcCopyChar).Distinct().Select(s => $"'{s}'"));

        /// <summary>Item rows key their detail as "itemWcid:detail"; bank rows as "detail".</summary>
        private const string DetailExpr = "CASE WHEN `kind` = 1 THEN `detail_key` ELSE SUBSTRING(`detail_key`, LOCATE(':', `detail_key`) + 1) END";

        private static readonly object staffLock = new object();
        private static HashSet<uint> staffAccounts = new HashSet<uint>();
        private static DateTime staffLoaded = DateTime.MinValue;

        /// <summary>Accounts with an access level above player. Cached for a minute.</summary>
        internal static HashSet<uint> StaffAccounts()
        {
            lock (staffLock)
            {
                if ((DateTime.UtcNow - staffLoaded).TotalSeconds < 60)
                    return staffAccounts;
            }

            var set = new HashSet<uint>();
            foreach (var player in PlayerManager.GetAllPlayers())
            {
                var account = player.Account;
                if (account != null && account.AccessLevel > 0)
                    set.Add(account.AccountId);
            }

            lock (staffLock)
            {
                staffAccounts = set;
                staffLoaded = DateTime.UtcNow;
            }

            return set;
        }

        /// <summary>" AND `account_id` NOT IN (...)" for the staff accounts, or nothing when there are none.</summary>
        internal static string NotStaffSql(string column = "`account_id`")
        {
            var staff = StaffAccounts();
            return staff.Count == 0 ? "" : $" AND {column} NOT IN ({string.Join(",", staff.OrderBy(a => a))})";
        }

        /// <summary>
        /// Accounts the owner has marked as player-run exchanges (ServerConfig pyreal_ledger_exchange_accounts): a
        /// comma-separated list of CHARACTER NAMES (owner 2026-10-13: a name is easier than an account number). Each
        /// name marks the whole account that owns that character. A plain number is still read as an account id.
        /// </summary>
        internal static HashSet<uint> ExchangeAccounts()
        {
            var (ids, names) = ParseExchangeList(ServerConfig.pyreal_ledger_exchange_accounts.Value);

            foreach (var name in names)
            {
                // a name that matches nobody (a typo, a deleted character) marks nothing
                var accountId = PlayerManager.FindByName(name)?.Account?.AccountId ?? 0;
                if (accountId != 0)
                    ids.Add(accountId);
            }

            return ids;
        }

        /// <summary>
        /// Splits the setting on commas and semicolons (not spaces: character names contain them) into account ids
        /// and character names. A leading "+" on a name is dropped. Pure; unit tested.
        /// </summary>
        internal static (HashSet<uint> ids, List<string> names) ParseExchangeList(string text)
        {
            var ids = new HashSet<uint>();
            var names = new List<string>();

            foreach (var raw in (text ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var part = raw.Trim().TrimStart('+').Trim();
                if (part.Length == 0)
                    continue;

                if (uint.TryParse(part, out var id))
                    ids.Add(id);
                else
                    names.Add(part);
            }

            return (ids, names);
        }

        // ---- suspect signals --------------------------------------------------------------------------------------

        public class SuspectOptions
        {
            /// <summary>Peak hour, share of a source and hours active only count for at least this many pyreals.</summary>
            public long MinAmount { get; set; } = 1_000_000_000;
            /// <summary>Currency disposed of beyond what was received only counts from this amount.</summary>
            public long MinSurplus { get; set; } = 1_000_000;
            /// <summary>Share of one NPC's or vendor's total that counts as concentrated.</summary>
            public double SharePct { get; set; } = 25;
            /// <summary>Share of the hours in the window with earnings that counts as round the clock.</summary>
            public double ActivePct { get; set; } = 80;
            /// <summary>A peak hour counts when it is this many times the typical account's peak hour.</summary>
            public double PeakTimesMedian { get; set; } = 10;
        }

        internal sealed class EarnRow
        {
            public uint AccountId;
            public DateTime Hour;
            public string Source;
            public string DetailKey;
            public string DetailName;
            public long AmountIn;
            public long AmountOut;
        }

        internal sealed class Signals
        {
            public long Created;
            public long PeakHourAmount;
            public DateTime? PeakHourUtc;
            public string PeakHourSource;
            public int ActiveHours;
            public string TopSource;
            public long TopSourceAmount;
            public long TopSourceTotal;
            public int TopSourceUsers;
            public double TopSourceSharePct;
        }

        private static string SourceLabel(string source, string detailName, string detailKey)
        {
            var detail = !string.IsNullOrEmpty(detailName) ? detailName : detailKey;
            return string.IsNullOrEmpty(detail) ? source : $"{source}: {detail}";
        }

        /// <summary>
        /// Per account: net created, the best single hour, hours with earnings, and the source (one NPC, one vendor)
        /// it took the largest share of. Amounts are net per source and detail, so buying a trade note for its face
        /// value earns nothing. Pure; unit tested.
        /// </summary>
        internal static Dictionary<uint, Signals> ComputeSignals(IEnumerable<EarnRow> rows)
        {
            var result = new Dictionary<uint, Signals>();
            var list = rows as IList<EarnRow> ?? rows.ToList();

            // server totals per source and detail, counting only accounts that came out ahead on it
            var perAccountSource = list.GroupBy(r => (r.AccountId, r.Source, r.DetailKey))
                .Select(g => (g.Key.AccountId, g.Key.Source, g.Key.DetailKey, name: g.Select(x => x.DetailName).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? "", net: g.Sum(x => x.AmountIn - x.AmountOut)))
                .Where(x => x.net > 0)
                .ToList();

            var sourceTotals = perAccountSource.GroupBy(x => (x.Source, x.DetailKey)).ToDictionary(g => g.Key, g => (total: g.Sum(x => x.net), users: g.Count()));

            foreach (var byAccount in list.GroupBy(r => r.AccountId))
            {
                var s = new Signals();

                foreach (var hour in byAccount.GroupBy(r => r.Hour))
                {
                    // net per source and detail within the hour, then only the gains
                    var gains = hour.GroupBy(r => (r.Source, r.DetailKey))
                        .Select(g => (g.Key.Source, g.Key.DetailKey, name: g.Select(x => x.DetailName).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? "", net: g.Sum(x => x.AmountIn - x.AmountOut)))
                        .Where(x => x.net > 0)
                        .ToList();

                    if (gains.Count == 0)
                        continue;

                    s.ActiveHours++;
                    var amount = gains.Sum(x => x.net);
                    if (amount > s.PeakHourAmount)
                    {
                        var best = gains.OrderByDescending(x => x.net).First();
                        s.PeakHourAmount = amount;
                        s.PeakHourUtc = hour.Key;
                        s.PeakHourSource = SourceLabel(best.Source, best.name, best.DetailKey);
                    }
                }

                s.Created = byAccount.Sum(r => r.AmountIn - r.AmountOut);
                result[byAccount.Key] = s;
            }

            foreach (var x in perAccountSource)
            {
                var s = result[x.AccountId];
                if (x.net <= s.TopSourceAmount)
                    continue;

                var (total, users) = sourceTotals[(x.Source, x.DetailKey)];
                s.TopSource = SourceLabel(x.Source, x.name, x.DetailKey);
                s.TopSourceAmount = x.net;
                s.TopSourceTotal = total;
                s.TopSourceUsers = users;
                s.TopSourceSharePct = total > 0 ? x.net * 100.0 / total : 0;
            }

            return result;
        }

        internal static long Median(IEnumerable<long> values)
        {
            var sorted = values.Where(v => v > 0).OrderBy(v => v).ToList();
            return sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];
        }

        private static string Compact(long value)
        {
            var abs = Math.Abs((double)value);
            if (abs >= 1e12) return (value / 1e12).ToString("0.##") + "T";
            if (abs >= 1e9) return (value / 1e9).ToString("0.##") + "B";
            if (abs >= 1e6) return (value / 1e6).ToString("0.##") + "M";
            if (abs >= 1e3) return (value / 1e3).ToString("0.#") + "K";
            return value.ToString();
        }

        /// <summary>
        /// Pyreals that do not add up, in plain words: the only things that make an account a suspect. Being paid the
        /// designed amount by an NPC or a vendor, however much and however fast, is never one of them (owner
        /// 2026-10-12); that is <see cref="HighlightsFor"/>. Pure; unit tested.
        /// </summary>
        internal static List<string> ReasonsFor(SuspectRow row, SuspectOptions o)
        {
            var reasons = new List<string>();

            if (row.FlaggedAmount >= Math.Max(1, o.MinSurplus))
                reasons.Add($"Flagged gains of {Compact(row.FlaggedAmount)} (dupe, rollback or outside edit)");

            if (row.CurrencySurplus >= Math.Max(1, o.MinSurplus))
                reasons.Add($"Disposed of {Compact(row.CurrencySurplus)} more in coins and notes than it was ever seen receiving");

            if (row.UnattributedIn > 0)
                reasons.Add($"Bank gain of {Compact(row.UnattributedIn)} from a code path nobody has labeled");

            return reasons;
        }

        /// <summary>
        /// What makes an account a heavy earner, stated neutrally: a large share of one NPC or vendor, a very large
        /// hour, earning round the clock. These point at a generous source as often as at a player, and are for
        /// balancing, not for accusing anyone. Pure; unit tested.
        /// </summary>
        internal static List<string> HighlightsFor(Signals s, long medianPeak, int windowHours, SuspectOptions o)
        {
            var highlights = new List<string>();
            if (s == null)
                return highlights;

            if (s.TopSourceAmount >= o.MinAmount && s.TopSourceSharePct >= o.SharePct)
                highlights.Add($"{s.TopSourceSharePct:0}% of everything {s.TopSource} paid out ({Compact(s.TopSourceAmount)} of {Compact(s.TopSourceTotal)}; {s.TopSourceUsers} account{(s.TopSourceUsers == 1 ? " uses" : "s use")} it)");

            if (s.PeakHourAmount >= o.MinAmount && (medianPeak <= 0 || s.PeakHourAmount >= medianPeak * o.PeakTimesMedian))
                highlights.Add($"Best hour {Compact(s.PeakHourAmount)} ({s.PeakHourUtc:yyyy-MM-dd HH}:00 UTC, {s.PeakHourSource}); the typical account's best hour is {Compact(medianPeak)}");

            if (windowHours >= 24 && s.Created >= o.MinAmount && s.ActiveHours * 100.0 / windowHours >= o.ActivePct)
                highlights.Add($"Earned in {s.ActiveHours} of {windowHours} hours");

            return highlights;
        }

        /// <summary>
        /// Every account with its integrity reasons (the "Doesn't add up" list), its heavy-earner highlights, and
        /// whether an admin has marked it reviewed. Staff removed. Ordered by reasons, then by pyreals created.
        /// </summary>
        public static List<SuspectRow> GetSuspects(int days, int limit, SuspectOptions options)
        {
            options ??= new SuspectOptions();
            days = ClampDays(days);
            limit = Math.Clamp(limit <= 0 ? 100 : limit, 1, 500);
            var since = Since(days);

            // the flag, surplus and totals columns as before (every account, so nothing is cut before it is scored)
            var rows = GetSuspects(days, 500);
            var staff = StaffAccounts();
            rows.RemoveAll(r => staff.Contains(r.AccountId));

            var earned = Query(
                $"SELECT `account_id`, `hour_utc`, `source`, {DetailExpr} AS dk, MAX(`detail_name`), SUM(`amount_in`), SUM(`amount_out`) FROM `pyreal_ledger_hourly` " +
                $"WHERE `kind` IN (1, 2, 3) AND `hour_utc` >= @since AND `source` NOT IN ({NotEarnedList}){NotStaffSql()} GROUP BY `account_id`, `hour_utc`, `source`, dk",
                c => P(c, "@since", since),
                r => new EarnRow { AccountId = U(r, 0), Hour = Convert.ToDateTime(r.GetValue(1)), Source = EconomyLedger.NetSource(S(r, 2)), DetailKey = S(r, 3), DetailName = S(r, 4), AmountIn = L(r, 5), AmountOut = L(r, 6) });

            var signals = ComputeSignals(earned);
            var medianPeak = Median(signals.Values.Select(s => s.PeakHourAmount));

            var start = PyrealLedger.LedgerStartUtc.HasValue && PyrealLedger.LedgerStartUtc.Value > since ? PyrealLedger.LedgerStartUtc.Value : since;
            var windowHours = (int)Math.Max(1, Math.Ceiling((DateTime.UtcNow - start).TotalHours));

            var byAccount = rows.ToDictionary(r => r.AccountId);
            foreach (var accountId in signals.Keys)
            {
                if (!byAccount.ContainsKey(accountId) && !staff.Contains(accountId))
                {
                    var row = new SuspectRow { AccountId = accountId, AccountName = AccountName(accountId) };
                    byAccount[accountId] = row;
                    rows.Add(row);
                }
            }

            foreach (var row in rows)
            {
                signals.TryGetValue(row.AccountId, out var s);

                if (s != null)
                {
                    row.Created = s.Created;
                    row.PeakHourAmount = s.PeakHourAmount;
                    row.PeakHourUtc = s.PeakHourUtc;
                    row.PeakHourSource = s.PeakHourSource;
                    row.ActiveHours = s.ActiveHours;
                    row.TopSource = s.TopSource;
                    row.TopSourceAmount = s.TopSourceAmount;
                    row.TopSourceTotal = s.TopSourceTotal;
                    row.TopSourceUsers = s.TopSourceUsers;
                    row.TopSourceSharePct = Math.Round(s.TopSourceSharePct, 1);
                }

                row.WindowHours = windowHours;
                row.Reasons = ReasonsFor(row, options);
                row.Highlights = HighlightsFor(s, medianPeak, windowHours, options);
                row.Score = row.Reasons.Count;
            }

            // an admin's "reviewed, normal" mark; a gain flag raised after the review brings the account back
            var reviewed = LoadReviewed();
            if (reviewed.Count > 0)
            {
                var lastFlag = Query(
                    $"SELECT `account_id`, MAX(`utc`) FROM `pyreal_ledger_flags` WHERE `flag` IN ({string.Join(",", gainFlags.Select(f => $"'{f}'"))}) GROUP BY `account_id`",
                    null,
                    r => (account: U(r, 0), utc: Convert.ToDateTime(r.GetValue(1)))).ToDictionary(x => x.account, x => x.utc);

                foreach (var row in rows)
                {
                    if (!reviewed.TryGetValue(row.AccountId, out var mark))
                        continue;

                    row.Reviewed = true;
                    row.ReviewedNote = mark.Note;
                    row.ReviewedBy = mark.By;
                    row.ReviewedUtc = mark.Utc;
                    row.NewSinceReview = lastFlag.TryGetValue(row.AccountId, out var flagUtc) && flagUtc > mark.Utc;
                }
            }

            // everything with a reason or a highlight is kept; the rest fills up to the limit by pyreals created
            return rows
                .OrderByDescending(r => r.Reasons.Count)
                .ThenByDescending(r => r.Highlights.Count > 0)
                .ThenByDescending(r => r.Created)
                .Take(Math.Max(limit, rows.Count(r => r.Reasons.Count > 0 || r.Highlights.Count > 0)))
                .ToList();
        }

        // ---- "reviewed, normal" marks -----------------------------------------------------------------------------

        public class ReviewMark
        {
            public uint AccountId { get; set; }
            public string Note { get; set; }
            public string By { get; set; }
            public DateTime Utc { get; set; }
        }

        private static bool reviewedTableReady;

        private static void EnsureReviewedTable()
        {
            if (reviewedTableReady)
                return;

            Execute(@"
CREATE TABLE IF NOT EXISTS `pyreal_ledger_reviewed` (
  `account_id`   int unsigned  NOT NULL,
  `note`         varchar(255)  NOT NULL DEFAULT '',
  `reviewed_by`  varchar(64)   NOT NULL DEFAULT '',
  `reviewed_utc` datetime      NOT NULL,
  PRIMARY KEY (`account_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci", null);

            reviewedTableReady = true;
        }

        private static void Execute(string sql, Action<System.Data.Common.DbCommand> bind)
        {
            using var ctx = new ACE.Database.Models.Shard.ShardDbContext();
            var con = Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetDbConnection(ctx.Database);
            if (con.State != System.Data.ConnectionState.Open)
                con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 10;
            bind?.Invoke(cmd);
            cmd.ExecuteNonQuery();
        }

        internal static Dictionary<uint, ReviewMark> LoadReviewed()
        {
            EnsureReviewedTable();

            return Query(
                "SELECT `account_id`, `note`, `reviewed_by`, `reviewed_utc` FROM `pyreal_ledger_reviewed`",
                null,
                r => new ReviewMark { AccountId = U(r, 0), Note = S(r, 1), By = S(r, 2), Utc = Convert.ToDateTime(r.GetValue(3)) }).ToDictionary(m => m.AccountId);
        }

        /// <summary>Marks an account as looked at and normal. The only thing this page writes; it changes no game data.</summary>
        public static ReviewMark SetReviewed(uint accountId, string note, uint? byAccountId)
        {
            if (accountId == 0)
                return null;

            EnsureReviewedTable();

            var text = (note ?? "").Trim();

            var mark = new ReviewMark
            {
                AccountId = accountId,
                Note = text.Length > 255 ? text.Substring(0, 255) : text,
                By = byAccountId.HasValue ? AccountName(byAccountId.Value) : "",
                Utc = DateTime.UtcNow,
            };

            Execute(
                "INSERT INTO `pyreal_ledger_reviewed` (`account_id`,`note`,`reviewed_by`,`reviewed_utc`) VALUES (@a,@n,@b,@u) " +
                "ON DUPLICATE KEY UPDATE `note`=VALUES(`note`),`reviewed_by`=VALUES(`reviewed_by`),`reviewed_utc`=VALUES(`reviewed_utc`)",
                c => { P(c, "@a", accountId); P(c, "@n", mark.Note); P(c, "@b", mark.By); P(c, "@u", mark.Utc); });

            return mark;
        }

        public static bool ClearReviewed(uint accountId)
        {
            EnsureReviewedTable();
            Execute("DELETE FROM `pyreal_ledger_reviewed` WHERE `account_id` = @a", c => P(c, "@a", accountId));
            return true;
        }

        // ---- context for "what if": how much is created and removed per week --------------------------------------

        public class Context
        {
            public int Days { get; set; }
            /// <summary>Days of data actually behind the numbers (the ledger may be younger than the window).</summary>
            public double CoveredDays { get; set; }
            public long Created { get; set; }
            public long Removed { get; set; }
            public long CreatedPerWeek { get; set; }
            public long RemovedPerWeek { get; set; }
            public long NetPerWeek { get; set; }
            /// <summary>Banked pyreals plus coins and notes held, staff left out when a supply snapshot exists.</summary>
            public long Supply { get; set; }
            public double? WeeklyPct { get; set; }
            public double TargetMinWeeklyPct { get; set; }
            public double TargetMaxWeeklyPct { get; set; }
        }

        private static readonly Dictionary<int, (DateTime at, Context data)> contextCache = new();

        /// <summary>
        /// Server-wide pyreals created and removed in the window, net per source and detail (so a trade note bought for
        /// its face value counts as neither), scaled to a week. The page uses it to show what a payout cut would do.
        /// </summary>
        public static Context GetContext(int days)
        {
            days = ClampDays(days);

            lock (contextCache)
            {
                if (contextCache.TryGetValue(days, out var hit) && (DateTime.UtcNow - hit.at).TotalSeconds < 60)
                    return hit.data;
            }

            var since = Since(days);
            var start = PyrealLedger.LedgerStartUtc.HasValue && PyrealLedger.LedgerStartUtc.Value > since ? PyrealLedger.LedgerStartUtc.Value : since;
            var context = new Context
            {
                Days = days,
                CoveredDays = Math.Max(1.0 / 24, (DateTime.UtcNow - start).TotalDays),
                TargetMinWeeklyPct = ServerConfig.economy_target_weekly_min_pct.Value,
                TargetMaxWeeklyPct = ServerConfig.economy_target_weekly_max_pct.Value,
            };

            foreach (var net in Query(
                $"SELECT SUM(`amount_in`) - SUM(`amount_out`) FROM `pyreal_ledger_hourly` " +
                $"WHERE `kind` IN (1, 2, 3) AND `hour_utc` >= @since AND `source` NOT IN ({NotEarnedList}){NotStaffSql()} GROUP BY {EconomyLedger.NetSourceSql}, {DetailExpr}",
                c => P(c, "@since", since),
                r => L(r, 0)))
            {
                if (net > 0)
                    context.Created += net;
                else
                    context.Removed += -net;
            }

            var perWeek = 7.0 / context.CoveredDays;
            context.CreatedPerWeek = (long)(context.Created * perWeek);
            context.RemovedPerWeek = (long)(context.Removed * perWeek);
            context.NetPerWeek = context.CreatedPerWeek - context.RemovedPerWeek;

            context.Supply = Query(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'economy_supply_daily'", null, r => L(r, 0)).FirstOrDefault() > 0
                ? Query("SELECT `banked` + `items` FROM `economy_supply_daily` WHERE `currency` = 'pyreals' ORDER BY `day_utc` DESC LIMIT 1", null, r => L(r, 0)).FirstOrDefault()
                : 0;

            if (context.Supply <= 0)
                context.Supply = Query("SELECT SUM(`live_balance`) + SUM(GREATEST(`currency_position`, 0)) FROM `pyreal_ledger_state`", null, r => L(r, 0)).FirstOrDefault();

            context.WeeklyPct = context.Supply > 0 ? Math.Round(context.NetPerWeek * 100.0 / context.Supply, 3) : null;

            lock (contextCache)
            {
                foreach (var stale in contextCache.Where(kv => (DateTime.UtcNow - kv.Value.at).TotalSeconds >= 60).Select(kv => kv.Key).ToList())
                    contextCache.Remove(stale);

                contextCache[days] = (DateTime.UtcNow, context);
            }

            return context;
        }

        // ---- what was handed to an NPC -----------------------------------------------------------------------------

        public class HandInRow
        {
            public uint ItemWcid { get; set; }
            public string ItemName { get; set; }
            public long Units { get; set; }
            public long Gives { get; set; }
            public long Characters { get; set; }
            public string TopGiver { get; set; }
            public long TopGiverUnits { get; set; }
        }

        /// <summary>
        /// Items players handed to one NPC (recorded since 2026-10-12), most first. Next to what the NPC paid out, this
        /// shows whether the turn-ins behind a payout are plausible: 900 trophies in a day is a different story from 9.
        /// </summary>
        public static List<HandInRow> GetNpcHandIns(uint npcWcid, int days)
        {
            var since = Since(days);
            var suffix = "%:" + npcWcid;

            var rows = Query(
                "SELECT SUBSTRING_INDEX(`detail_key`, ':', 1) AS `wcid`, SUM(`units`), SUM(`events`), COUNT(DISTINCT `char_id`) FROM `pyreal_ledger_hourly` " +
                $"WHERE `kind` = {PyrealLedger.KindHandedIn} AND `hour_utc` >= @since AND `detail_key` LIKE @suffix GROUP BY `wcid` ORDER BY SUM(`units`) DESC LIMIT 200",
                c => { P(c, "@since", since); P(c, "@suffix", suffix); },
                r => new HandInRow { ItemWcid = uint.TryParse(S(r, 0), out var w) ? w : 0, Units = L(r, 1), Gives = L(r, 2), Characters = L(r, 3) });

            if (rows.Count == 0)
                return rows;

            var top = new Dictionary<uint, (string name, long units)>();
            foreach (var (wcid, name, units) in Query(
                "SELECT SUBSTRING_INDEX(`detail_key`, ':', 1) AS `wcid`, MAX(`char_name`), SUM(`units`) FROM `pyreal_ledger_hourly` " +
                $"WHERE `kind` = {PyrealLedger.KindHandedIn} AND `hour_utc` >= @since AND `detail_key` LIKE @suffix GROUP BY `wcid`, `char_id`",
                c => { P(c, "@since", since); P(c, "@suffix", suffix); },
                r => (uint.TryParse(S(r, 0), out var w) ? w : 0, S(r, 1), L(r, 2))))
            {
                if (!top.TryGetValue(wcid, out var best) || units > best.units)
                    top[wcid] = (name, units);
            }

            foreach (var row in rows)
            {
                row.ItemName = ItemName(row.ItemWcid);
                if (top.TryGetValue(row.ItemWcid, out var best))
                {
                    row.TopGiver = best.name;
                    row.TopGiverUnits = best.units;
                }
            }

            return rows;
        }

        // ---- transfers between accounts ---------------------------------------------------------------------------

        public class TransferAccountRow
        {
            public uint AccountId { get; set; }
            public string AccountName { get; set; }
            public string Characters { get; set; }
            public long Sent { get; set; }
            public long Received { get; set; }
            public long Net { get; set; }
            public int Partners { get; set; }
            public long Events { get; set; }
            public bool IsExchange { get; set; }
        }

        public class TransferPairRow
        {
            public uint FromAccountId { get; set; }
            public string FromAccountName { get; set; }
            public uint ToAccountId { get; set; }
            public string ToAccountName { get; set; }
            public long Amount { get; set; }
            public long Events { get; set; }
            /// <summary>One side is an account marked as a player-run exchange.</summary>
            public bool ViaExchange { get; set; }
        }

        public class TransfersReport
        {
            public int Days { get; set; }
            public long CrossAccount { get; set; }
            public long SameAccount { get; set; }
            public long ViaExchanges { get; set; }
            public List<uint> ExchangeAccounts { get; set; } = new();
            public List<TransferAccountRow> Accounts { get; set; } = new();
            public List<TransferPairRow> Pairs { get; set; } = new();
        }

        /// <summary>
        /// Pyreals and currency items moved between accounts by bank transfer, trade and hand-to-hand give. Counted on
        /// the receiving side, whose row names the sender. Staff accounts are left out.
        /// </summary>
        public static TransfersReport GetTransfers(int days, int limit)
        {
            days = ClampDays(days);
            limit = Math.Clamp(limit <= 0 ? 100 : limit, 1, 500);
            var since = Since(days);
            var staff = StaffAccounts();
            var exchanges = ExchangeAccounts();

            var report = new TransfersReport { Days = days, ExchangeAccounts = exchanges.OrderBy(a => a).ToList() };

            var edges = Query(
                $"SELECT s.`account_id`, t.`account_id`, SUM(t.`amount_in`), SUM(t.`events`) FROM `pyreal_ledger_hourly` t " +
                $"JOIN `pyreal_ledger_state` s ON s.`char_id` = CAST(CASE WHEN t.`kind` = 1 THEN t.`detail_key` ELSE SUBSTRING(t.`detail_key`, LOCATE(':', t.`detail_key`) + 1) END AS UNSIGNED) " +
                $"WHERE t.`kind` IN (1, 2) AND t.`hour_utc` >= @since AND t.`amount_in` > 0 " +
                $"AND t.`source` IN ('{PyrealLedger.SrcTransfer}','{PyrealLedger.SrcTrade}','{PyrealLedger.SrcGive}') GROUP BY s.`account_id`, t.`account_id`",
                c => P(c, "@since", since),
                r => (from: U(r, 0), to: U(r, 1), amount: L(r, 2), events: L(r, 3)));

            var accounts = new Dictionary<uint, TransferAccountRow>();
            var partners = new Dictionary<uint, HashSet<uint>>();

            TransferAccountRow Account(uint id)
            {
                if (!accounts.TryGetValue(id, out var row))
                {
                    accounts[id] = row = new TransferAccountRow { AccountId = id, IsExchange = exchanges.Contains(id) };
                    partners[id] = new HashSet<uint>();
                }
                return row;
            }

            foreach (var (from, to, amount, events) in edges)
            {
                if (from == 0 || to == 0 || staff.Contains(from) || staff.Contains(to))
                    continue;

                if (from == to)
                {
                    report.SameAccount += amount;
                    continue;
                }

                report.CrossAccount += amount;
                var viaExchange = exchanges.Contains(from) || exchanges.Contains(to);
                if (viaExchange)
                    report.ViaExchanges += amount;

                var sender = Account(from);
                sender.Sent += amount;
                sender.Events += events;
                partners[from].Add(to);

                var receiver = Account(to);
                receiver.Received += amount;
                receiver.Events += events;
                partners[to].Add(from);

                report.Pairs.Add(new TransferPairRow { FromAccountId = from, ToAccountId = to, Amount = amount, Events = events, ViaExchange = viaExchange });
            }

            var characters = Query(
                "SELECT `account_id`, GROUP_CONCAT(DISTINCT `char_name` ORDER BY `char_name` SEPARATOR ', ') FROM `pyreal_ledger_hourly` " +
                $"WHERE `kind` = 1 AND `hour_utc` >= @since AND `source` = '{PyrealLedger.SrcTransfer}' GROUP BY `account_id`",
                c => P(c, "@since", since),
                r => (account: U(r, 0), names: S(r, 1))).ToDictionary(x => x.account, x => x.names);

            foreach (var row in accounts.Values)
            {
                row.Net = row.Received - row.Sent;
                row.Partners = partners[row.AccountId].Count;
                row.AccountName = AccountName(row.AccountId);
                if (characters.TryGetValue(row.AccountId, out var names))
                    row.Characters = names.Length > 200 ? names.Substring(0, 200) + "..." : names;
            }

            report.Accounts = accounts.Values.OrderByDescending(a => a.Sent + a.Received).Take(limit).ToList();
            report.Pairs = report.Pairs.OrderByDescending(p => p.Amount).Take(limit).ToList();

            foreach (var pair in report.Pairs)
            {
                pair.FromAccountName = accounts.TryGetValue(pair.FromAccountId, out var f) ? f.AccountName : AccountName(pair.FromAccountId);
                pair.ToAccountName = accounts.TryGetValue(pair.ToAccountId, out var t) ? t.AccountName : AccountName(pair.ToAccountId);
            }

            return report;
        }

        // ---- rates on the NPC and vendor lists --------------------------------------------------------------------

        /// <summary>The busiest single character-hour for each NPC, and the top receiver's share.</summary>
        private static void AddNpcRates(List<NpcRow> rows, DateTime since)
        {
            if (rows.Count == 0)
                return;

            var busiest = new Dictionary<uint, (string name, DateTime hour, long value, long gives)>();
            foreach (var (npc, name, hour, value, gives) in Query(
                "SELECT SUBSTRING_INDEX(`detail_key`, ':', -1) AS `npc`, MAX(`char_name`), `hour_utc`, SUM(`amount_in`), SUM(`events`) " +
                $"FROM `pyreal_ledger_hourly` WHERE {NpcRowsWhere} GROUP BY `npc`, `char_id`, `hour_utc`",
                c => P(c, "@since", since),
                r => (uint.TryParse(S(r, 0), out var w) ? w : 0, S(r, 1), Convert.ToDateTime(r.GetValue(2)), L(r, 3), L(r, 4))))
            {
                if (!busiest.TryGetValue(npc, out var best) || value > best.value)
                    busiest[npc] = (name, hour, value, gives);
            }

            var handedIn = Query(
                "SELECT SUBSTRING_INDEX(`detail_key`, ':', -1) AS `npc`, SUM(`units`) FROM `pyreal_ledger_hourly` " +
                $"WHERE `kind` = {PyrealLedger.KindHandedIn} AND `hour_utc` >= @since GROUP BY `npc`",
                c => P(c, "@since", since),
                r => (npc: uint.TryParse(S(r, 0), out var w) ? w : 0, units: L(r, 1))).ToDictionary(x => x.npc, x => x.units);

            var context = GetContext((int)Math.Round((DateTime.UtcNow - since).TotalDays));

            foreach (var row in rows)
            {
                row.PerGive = row.Gives > 0 ? row.Total / row.Gives : 0;
                row.TopReceiverSharePct = row.Total > 0 ? Math.Round(row.TopReceiverValue * 100.0 / row.Total, 1) : 0;
                row.PerWeek = (long)(row.Total * 7.0 / context.CoveredDays);
                row.ShareOfCreationPct = context.Created > 0 ? Math.Round(row.Total * 100.0 / context.Created, 1) : 0;
                row.HandedInUnits = handedIn.TryGetValue(row.NpcWcid, out var units) ? units : 0;

                if (busiest.TryGetValue(row.NpcWcid, out var best))
                {
                    row.BusiestHourChar = best.name;
                    row.BusiestHourUtc = best.hour;
                    row.BusiestHourValue = best.value;
                    row.BusiestHourGives = best.gives;
                }
            }
        }

        /// <summary>The top seller at each vendor, their share, and how many hours they sold in.</summary>
        private static void AddVendorRates(List<VendorRow> rows, DateTime since)
        {
            if (rows.Count == 0)
                return;

            var top = new Dictionary<string, (string name, long payout, long hours)>();
            foreach (var (vendor, name, payout, hours) in Query(
                "SELECT `detail_key`, MAX(`char_name`), SUM(`amount_in`), COUNT(DISTINCT `hour_utc`) FROM `pyreal_ledger_hourly` " +
                $"WHERE `kind` = {KBank} AND `source` = '{VendorSell}' AND `hour_utc` >= @since GROUP BY `detail_key`, `char_id`",
                c => P(c, "@since", since),
                r => (S(r, 0), S(r, 1), L(r, 2), L(r, 3))))
            {
                if (!top.TryGetValue(vendor, out var best) || payout > best.payout)
                    top[vendor] = (name, payout, hours);
            }

            var context = GetContext((int)Math.Round((DateTime.UtcNow - since).TotalDays));

            foreach (var row in rows)
            {
                row.PerSale = row.Sales > 0 ? row.Payout / row.Sales : 0;
                row.PerWeek = (long)(row.Payout * 7.0 / context.CoveredDays);
                row.ShareOfCreationPct = context.Created > 0 ? Math.Round(row.Payout * 100.0 / context.Created, 1) : 0;

                if (top.TryGetValue(row.VendorWcid.ToString(), out var best))
                {
                    row.TopSeller = best.name;
                    row.TopSellerPayout = best.payout;
                    row.TopSellerSharePct = row.Payout > 0 ? Math.Round(best.payout * 100.0 / row.Payout, 1) : 0;
                    row.TopSellerHours = best.hours;
                }
            }
        }

        // ---- balances created by @copychar ------------------------------------------------------------------------

        public class CopyRow
        {
            public DateTime Utc { get; set; }
            public uint CharId { get; set; }
            public string CharName { get; set; }
            public uint AccountId { get; set; }
            public string AccountName { get; set; }
            /// <summary>Banked pyreals the copy was created with.</summary>
            public long CopiedBalance { get; set; }
            /// <summary>What it holds now (0 once the copy is deleted or emptied).</summary>
            public long BalanceNow { get; set; }
            public bool Exists { get; set; }
            public string Detail { get; set; }
        }

        public class CopiesReport
        {
            public long CopiedTotal { get; set; }
            public long StillHeld { get; set; }
            public List<CopyRow> Rows { get; set; } = new();
        }

        /// <summary>
        /// Every character @copychar created with a balance: pyreals that were never earned. Shows what each copy
        /// still holds, so test copies can be emptied or deleted. All time, not limited to the window.
        /// </summary>
        public static CopiesReport GetCopies()
        {
            var report = new CopiesReport();

            report.Rows = Query(
                "SELECT f.`utc`, f.`char_id`, f.`char_name`, f.`account_id`, f.`amount`, f.`detail` FROM `pyreal_ledger_flags` f " +
                $"WHERE f.`flag` = '{PyrealLedger.FlagCopyChar}' ORDER BY f.`utc` DESC LIMIT 500",
                null,
                r => new CopyRow { Utc = Convert.ToDateTime(r.GetValue(0)), CharId = U(r, 1), CharName = S(r, 2), AccountId = U(r, 3), CopiedBalance = L(r, 4), Detail = S(r, 5) });

            foreach (var row in report.Rows)
            {
                var player = PlayerManager.FindByGuid(row.CharId);
                row.Exists = player != null;
                row.BalanceNow = player?.GetProperty(ACE.Entity.Enum.Properties.PropertyInt64.BankedPyreals) ?? 0;
                row.AccountName = AccountName(row.AccountId);
                report.CopiedTotal += row.CopiedBalance;
                report.StillHeld += Math.Min(row.BalanceNow, row.CopiedBalance);
            }

            return report;
        }
    }
}
