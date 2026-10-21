using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using ACE.Database.Models.Shard;
using ACE.Entity.Enum.Properties;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Stalled exporters (owner 2026-10-12): accounts that are established (enlightenment), have not gained an
    /// enlightenment or luminance augmentation for a while, and still send currency to OTHER accounts. Earning,
    /// not spending on yourself and sending it away is what a farmer or feeder account looks like.
    ///
    /// Read-only. Progress dates come from economy_progress, transfers from the economy ledger's flow rows and the
    /// pyreal ledger's hourly rows. Every threshold is a parameter, so the page can adjust them.
    /// </summary>
    public static class EconomyStalledReport
    {
        private const int MaxRows = 200;
        private const int MaxRecipients = 8;
        private const int MaxCharacters = 8;

        // ---- DTOs ------------------------------------------------------------------------------------------------

        public class CharacterRow
        {
            public uint CharId { get; set; }
            public string CharName { get; set; }
            public int Enlightenment { get; set; }
            public long Augs { get; set; }
        }

        public class CurrencyRow
        {
            public string Key { get; set; }
            public string Name { get; set; }
            public long Earned { get; set; }
            public long SpentOnSelf { get; set; }
            public long Exported { get; set; }
        }

        public class RecipientRow
        {
            public uint AccountId { get; set; }
            public string AccountName { get; set; }
            public string CharName { get; set; }
            public string Currency { get; set; }
            public string CurrencyName { get; set; }
            public long Amount { get; set; }
        }

        public class Row
        {
            public uint AccountId { get; set; }
            public string AccountName { get; set; }
            public int MaxEnlightenment { get; set; }
            public long Augs { get; set; }
            public int StalledDays { get; set; }
            /// <summary>No progress has been seen since tracking began, so the real stall is at least this long.</summary>
            public bool StallIsMinimum { get; set; }
            public DateTime? LastProgressUtc { get; set; }
            public DateTime? LastLoginUtc { get; set; }
            public List<CharacterRow> Characters { get; set; } = new();
            public List<CurrencyRow> Currencies { get; set; } = new();
            public List<RecipientRow> Recipients { get; set; } = new();
        }

        public class Report
        {
            public int Days { get; set; }
            public int StallDays { get; set; }
            public int MinEnlightenment { get; set; }
            public int MaxEnlightenment { get; set; }
            public string Currency { get; set; }
            public long MinExport { get; set; }
            public DateTime? ProgressSinceUtc { get; set; }
            public DateTime? FlowTrackingSinceUtc { get; set; }
            public List<Row> Rows { get; set; } = new();
            public List<string> Notes { get; set; } = new();
        }

        // ---- pure parts (unit tested) ----------------------------------------------------------------------------

        internal sealed class ProgressChar
        {
            public uint CharId;
            public uint AccountId;
            public string Name;
            public int Enlightenment;
            public long Augs;
            public DateTime FirstSeenUtc;
            public DateTime? LastChangeUtc;
        }

        /// <summary>
        /// An account's stall: days since any of its characters last progressed. A character that has never been seen
        /// to progress counts from the day it was first recorded, and the result is then only a minimum.
        /// </summary>
        internal static (int days, bool isMinimum, DateTime reference) StallOf(IEnumerable<ProgressChar> characters, DateTime nowUtc)
        {
            // the date and the "only a minimum" flag come from the same character: the one with the latest date,
            // and on a tie the one whose date is a recorded change
            var reference = DateTime.MinValue;
            ProgressChar selected = null;

            foreach (var c in characters)
            {
                var date = c.LastChangeUtc ?? c.FirstSeenUtc;
                if (selected == null || date > reference || (date == reference && c.LastChangeUtc.HasValue && !selected.LastChangeUtc.HasValue))
                {
                    selected = c;
                    reference = date;
                }
            }

            var isMinimum = selected == null || !selected.LastChangeUtc.HasValue;
            return ((int)Math.Max(0, Math.Floor((nowUtc - reference).TotalDays)), isMinimum, reference);
        }

        /// <summary>
        /// True when an account belongs in the report: established, stalled long enough, and it sent enough away.
        /// With one currency selected the export threshold applies to it; with all currencies any export counts.
        /// </summary>
        internal static bool Matches(int maxEnlightenment, int stalledDays, IReadOnlyDictionary<string, long> exported,
            int minEnlightenment, int maxEnlightenmentFilter, int stallDays, string currency, long minExport)
        {
            if (maxEnlightenment < minEnlightenment)
                return false;
            if (maxEnlightenmentFilter > 0 && maxEnlightenment > maxEnlightenmentFilter)
                return false;
            if (stalledDays < stallDays)
                return false;

            var floor = Math.Max(1, minExport);

            if (currency != null)
                return exported.TryGetValue(currency, out var amount) && amount >= floor;

            return exported.Values.Any(v => v >= floor);
        }

        // ---- query -----------------------------------------------------------------------------------------------

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

        private static bool TableExists(string table) =>
            Query("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = @t", c => Param(c, "@t", table), r => L(r, 0)).FirstOrDefault() > 0;

        private static readonly string InternalList = string.Join(",", EconomyLedger.InternalSources.Select(s => $"'{s}'"));

        /// <summary>Sources where the receiving character's row names the sending character in detail_key.</summary>
        private const string TransferSources = "'Bank','Transfer','Trade','Give'";

        private const string PyrealDetail = "CASE WHEN `kind` = 1 THEN `detail_key` ELSE SUBSTRING(`detail_key`, LOCATE(':', `detail_key`) + 1) END";

        private sealed class Who
        {
            public uint AccountId;
            public string AccountName;
            public string CharName;
            public DateTime? LastLoginUtc;
        }

        public static Report Get(int days, int stallDays, int minEnlightenment, int maxEnlightenment, string currencyKey, long minExport)
        {
            days = EconomyReports.ClampDays(days);
            stallDays = Math.Clamp(stallDays, 0, 3650);
            minEnlightenment = Math.Max(0, minEnlightenment);
            maxEnlightenment = Math.Max(0, maxEnlightenment);
            minExport = Math.Max(0, minExport);

            var all = string.IsNullOrEmpty(currencyKey) || string.Equals(currencyKey, "all", StringComparison.OrdinalIgnoreCase);
            var selected = all ? null : EconomyLedger.Find(currencyKey);
            if (!all && selected == null)
                return null;

            var now = DateTime.UtcNow;
            var since = now.Date.AddDays(-(days - 1));

            var report = new Report
            {
                Days = days, StallDays = stallDays, MinEnlightenment = minEnlightenment, MaxEnlightenment = maxEnlightenment,
                Currency = selected?.Key ?? "all", MinExport = minExport, FlowTrackingSinceUtc = EconomyLedger.FlowTrackingSinceUtc,
            };

            report.Notes.Add("An account is listed when its highest enlightenment is in range, none of its characters gained an enlightenment or luminance augmentation for the chosen number of days, and it sent currency to another account in the window.");
            report.Notes.Add("Sent to other accounts = bank transfers, trades and hand-to-hand gives received by a character on a different account. Moves between characters on the same account are not counted. Items other than currency are not counted.");
            report.Notes.Add("Transfers to or from an account marked as a player-run exchange (ServerConfig pyreal_ledger_exchange_accounts, a list of character names) are not counted: the sender gets something back.");
            report.Notes.Add("Earned and Spent on self leave out bank, trade and give moves. Staff characters are left out of everything except pyreals.");
            report.Notes.Add("Progress is checked once a day with the supply snapshot. \"At least\" means no progress has been seen since tracking began, so the real stall may be longer.");

            if (!TableExists("economy_progress"))
            {
                report.Notes.Add("Progress tracking has not started yet: it begins with the first supply snapshot.");
                return report;
            }

            // ---- who is who (current account of every character) -----------------------------------------------

            var who = new Dictionary<uint, Who>();
            foreach (var player in PlayerManager.GetAllPlayers())
            {
                var login = player.GetProperty(PropertyFloat.LoginTimestamp);
                who[player.Guid.Full] = new Who
                {
                    AccountId = player.Account?.AccountId ?? 0,
                    AccountName = player.Account?.AccountName ?? "",
                    CharName = player.Name ?? "",
                    LastLoginUtc = login.HasValue && login.Value > 0 ? DateTimeOffset.FromUnixTimeSeconds((long)login.Value).UtcDateTime : null,
                };
            }

            // ---- progress per account ---------------------------------------------------------------------------

            var progress = Query(
                "SELECT `char_id`, `account_id`, `char_name`, `enlightenment`, `augs`, `first_seen_utc`, `last_change_utc` FROM `economy_progress`",
                null,
                r => new ProgressChar
                {
                    CharId = Convert.ToUInt32(r.GetValue(0)), AccountId = Convert.ToUInt32(r.GetValue(1)), Name = Convert.ToString(r.GetValue(2)),
                    Enlightenment = Convert.ToInt32(r.GetValue(3)), Augs = Convert.ToInt64(r.GetValue(4)),
                    FirstSeenUtc = DateTime.SpecifyKind(Convert.ToDateTime(r.GetValue(5)), DateTimeKind.Utc),
                    LastChangeUtc = r.IsDBNull(6) ? null : DateTime.SpecifyKind(Convert.ToDateTime(r.GetValue(6)), DateTimeKind.Utc),
                });

            if (progress.Count == 0)
            {
                report.Notes.Add("Progress tracking has not started yet: it begins with the first supply snapshot.");
                return report;
            }

            report.ProgressSinceUtc = progress.Min(p => p.FirstSeenUtc);

            // a character that was deleted or moved keeps its stored account; a live one uses its current account
            var byAccount = progress
                .Where(p => !who.TryGetValue(p.CharId, out var w) || w.AccountId != 0)
                .GroupBy(p => who.TryGetValue(p.CharId, out var w) ? w.AccountId : p.AccountId)
                .Where(g => g.Key != 0)
                .ToDictionary(g => g.Key, g => g.ToList());

            // ---- transfers between accounts: (currency, sending character) -> receiving character --------------

            var exchanges = PyrealLedgerReports.ExchangeAccounts();
            var exported = new Dictionary<uint, Dictionary<string, long>>();
            var recipients = new Dictionary<uint, Dictionary<(uint account, string currency), (long amount, string charName)>>();

            void AddEdge(string currency, uint fromChar, uint toChar, uint toAccountStored, string toName, long amount)
            {
                if (amount <= 0 || !who.TryGetValue(fromChar, out var from) || from.AccountId == 0)
                    return;

                var toAccount = who.TryGetValue(toChar, out var to) ? to.AccountId : toAccountStored;
                if (toAccount == 0 || toAccount == from.AccountId)
                    return;

                // a marked player-run exchange gives something back: selling to it is a trade, not an export
                if (exchanges.Contains(toAccount) || exchanges.Contains(from.AccountId))
                    return;

                if (!exported.TryGetValue(from.AccountId, out var byCurrency))
                    exported[from.AccountId] = byCurrency = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                byCurrency[currency] = byCurrency.TryGetValue(currency, out var e) ? e + amount : amount;

                if (!recipients.TryGetValue(from.AccountId, out var byRecipient))
                    recipients[from.AccountId] = byRecipient = new Dictionary<(uint, string), (long, string)>();
                var key = (toAccount, currency);
                byRecipient[key] = byRecipient.TryGetValue(key, out var r) ? (r.amount + amount, r.charName) : (amount, to?.CharName ?? toName ?? "");
            }

            if (TableExists("economy_flow_daily"))
            {
                foreach (var row in Query(
                    "SELECT `currency`, `detail_key`, `char_id`, MAX(`account_id`), MAX(`char_name`), SUM(`amount_in`) FROM `economy_flow_daily` " +
                    $"WHERE `day_utc` >= @since AND `source` IN ({TransferSources}) AND `amount_in` > 0 AND `detail_key` REGEXP '^[0-9]+$' GROUP BY `currency`, `detail_key`, `char_id`",
                    c => Param(c, "@since", since),
                    r => (currency: Convert.ToString(r.GetValue(0)), from: Convert.ToString(r.GetValue(1)), to: Convert.ToUInt32(r.GetValue(2)), account: Convert.ToUInt32(L(r, 3)), name: Convert.ToString(r.GetValue(4)), amount: L(r, 5))))
                {
                    if (uint.TryParse(row.from, out var fromChar))
                        AddEdge(row.currency, fromChar, row.to, row.account, row.name, row.amount);
                }
            }

            var pyrealTable = TableExists("pyreal_ledger_hourly");
            if (pyrealTable)
            {
                foreach (var row in Query(
                    $"SELECT {PyrealDetail} AS dk, `char_id`, MAX(`account_id`), MAX(`char_name`), SUM(`amount_in`) FROM `pyreal_ledger_hourly` " +
                    $"WHERE `kind` IN (1, 2) AND `hour_utc` >= @since AND `source` IN ({TransferSources}) AND `amount_in` > 0 GROUP BY dk, `char_id` HAVING dk REGEXP '^[0-9]+$'",
                    c => Param(c, "@since", since),
                    r => (from: Convert.ToString(r.GetValue(0)), to: Convert.ToUInt32(r.GetValue(1)), account: Convert.ToUInt32(L(r, 2)), name: Convert.ToString(r.GetValue(3)), amount: L(r, 4))))
                {
                    if (uint.TryParse(row.from, out var fromChar))
                        AddEdge("pyreals", fromChar, row.to, row.account, row.name, row.amount);
                }
            }

            // ---- pick the accounts ------------------------------------------------------------------------------

            var picked = new List<(uint accountId, List<ProgressChar> chars, int stalled, bool isMinimum, DateTime reference)>();
            var none = new Dictionary<string, long>();

            foreach (var (accountId, chars) in byAccount)
            {
                var (stalled, isMinimum, reference) = StallOf(chars, now);
                var sent = exported.TryGetValue(accountId, out var e) ? e : none;

                if (Matches(chars.Max(c => c.Enlightenment), stalled, sent, minEnlightenment, maxEnlightenment, stallDays, selected?.Key, minExport))
                    picked.Add((accountId, chars, stalled, isMinimum, reference));
            }

            if (picked.Count == 0)
                return report;

            // ---- earned and spent on self, per account and currency ---------------------------------------------

            var earned = new Dictionary<(uint account, string currency), (long amountIn, long amountOut)>();

            void AddEarned(string currency, uint charId, uint accountStored, long amountIn, long amountOut)
            {
                var account = who.TryGetValue(charId, out var w) ? w.AccountId : accountStored;
                var key = (account, currency.ToLowerInvariant());
                earned[key] = earned.TryGetValue(key, out var v) ? (v.amountIn + amountIn, v.amountOut + amountOut) : (amountIn, amountOut);
            }

            if (TableExists("economy_flow_daily"))
            {
                foreach (var row in Query(
                    "SELECT `currency`, `char_id`, MAX(`account_id`), SUM(`amount_in`), SUM(`amount_out`) FROM `economy_flow_daily` " +
                    $"WHERE `day_utc` >= @since AND `source` NOT IN ({InternalList}) GROUP BY `currency`, `char_id`",
                    c => Param(c, "@since", since),
                    r => (currency: Convert.ToString(r.GetValue(0)), charId: Convert.ToUInt32(r.GetValue(1)), account: Convert.ToUInt32(L(r, 2)), amountIn: L(r, 3), amountOut: L(r, 4))))
                    AddEarned(row.currency, row.charId, row.account, row.amountIn, row.amountOut);
            }

            if (pyrealTable)
            {
                foreach (var row in Query(
                    "SELECT `char_id`, MAX(`account_id`), SUM(`amount_in`), SUM(`amount_out`) FROM `pyreal_ledger_hourly` " +
                    $"WHERE `kind` IN (1, 2, 3) AND `hour_utc` >= @since AND `source` NOT IN ({PyrealLedgerReports.NotEarnedList}) GROUP BY `char_id`",
                    c => Param(c, "@since", since),
                    r => (charId: Convert.ToUInt32(r.GetValue(0)), account: Convert.ToUInt32(L(r, 1)), amountIn: L(r, 2), amountOut: L(r, 3))))
                    AddEarned("pyreals", row.charId, row.account, row.amountIn, row.amountOut);
            }

            // ---- rows -------------------------------------------------------------------------------------------

            var accountNames = who.Values.Where(w => w.AccountId != 0).GroupBy(w => w.AccountId).ToDictionary(g => g.Key, g => g.First().AccountName);

            foreach (var (accountId, chars, stalled, isMinimum, reference) in picked)
            {
                var sent = exported[accountId];

                var row = new Row
                {
                    AccountId = accountId,
                    AccountName = accountNames.TryGetValue(accountId, out var name) ? name : "",
                    MaxEnlightenment = chars.Max(c => c.Enlightenment),
                    Augs = chars.Sum(c => c.Augs),
                    StalledDays = stalled,
                    StallIsMinimum = isMinimum,
                    LastProgressUtc = isMinimum ? null : reference,
                    LastLoginUtc = who.Values.Where(w => w.AccountId == accountId && w.LastLoginUtc.HasValue).Select(w => w.LastLoginUtc).DefaultIfEmpty(null).Max(),
                    Characters = chars.OrderByDescending(c => c.Enlightenment).ThenByDescending(c => c.Augs).Take(MaxCharacters)
                        .Select(c => new CharacterRow { CharId = c.CharId, CharName = who.TryGetValue(c.CharId, out var w) ? w.CharName : c.Name, Enlightenment = c.Enlightenment, Augs = c.Augs }).ToList(),
                };

                foreach (var currency in EconomyLedger.Currencies)
                {
                    sent.TryGetValue(currency.Key, out var out_);
                    earned.TryGetValue((accountId, currency.Key), out var flow);

                    if (out_ == 0 && flow.amountIn == 0 && flow.amountOut == 0)
                        continue;

                    row.Currencies.Add(new CurrencyRow { Key = currency.Key, Name = currency.Name, Earned = flow.amountIn, SpentOnSelf = flow.amountOut, Exported = out_ });
                }

                if (recipients.TryGetValue(accountId, out var to))
                {
                    row.Recipients = to
                        .Where(kv => selected == null || string.Equals(kv.Key.currency, selected.Key, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(kv => kv.Value.amount).Take(MaxRecipients)
                        .Select(kv => new RecipientRow
                        {
                            AccountId = kv.Key.account,
                            AccountName = accountNames.TryGetValue(kv.Key.account, out var n) ? n : "",
                            CharName = kv.Value.charName,
                            Currency = kv.Key.currency,
                            CurrencyName = EconomyLedger.Find(kv.Key.currency)?.Name ?? kv.Key.currency,
                            Amount = kv.Value.amount,
                        }).ToList();
                }

                report.Rows.Add(row);
            }

            // one currency: biggest sender first. All currencies: amounts are not comparable, so longest stall first.
            report.Rows = (selected != null
                    ? report.Rows.OrderByDescending(r => r.Currencies.FirstOrDefault(c => c.Key == selected.Key)?.Exported ?? 0)
                    : report.Rows.OrderByDescending(r => r.StalledDays).ThenByDescending(r => r.MaxEnlightenment))
                .Take(MaxRows).ToList();

            return report;
        }
    }
}
