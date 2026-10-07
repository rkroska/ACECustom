using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Entity.Enum.Properties;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Read-only queries over the pyreal ledger tables for the admin portal page and @pyrealaudit.
    /// All amounts are pyreals. Currency items are valued at face value.
    /// </summary>
    public static class PyrealLedgerReports
    {
        public const int MaxDays = 365;

        // ---- DTOs ------------------------------------------------------------------------------------------------

        public class SourceTotal
        {
            public string Source { get; set; }
            public long AmountIn { get; set; }
            public long AmountOut { get; set; }
            public long Events { get; set; }
        }

        public class ItemTotal
        {
            public string Direction { get; set; }
            public string Source { get; set; }
            public long Value { get; set; }
            public long Units { get; set; }
        }

        public class FlagCount
        {
            public string Flag { get; set; }
            public long Count { get; set; }
            public long Amount { get; set; }
        }

        public class Overview
        {
            public DateTime? LedgerStartUtc { get; set; }
            public bool LedgerRunning { get; set; }
            public int Days { get; set; }
            public List<SourceTotal> BankTotals { get; set; } = new();
            public List<ItemTotal> ItemTotals { get; set; } = new();
            public List<FlagCount> FlagCounts { get; set; } = new();
        }

        public class SuspectRow
        {
            public uint AccountId { get; set; }
            public string AccountName { get; set; }
            public string Characters { get; set; }
            public long BankIn { get; set; }
            public long BankOut { get; set; }
            public long VendorSellIn { get; set; }
            public string TopVendor { get; set; }
            public long TopVendorPayout { get; set; }
            public long UnattributedIn { get; set; }
            public long CurrencyIn { get; set; }
            public long CurrencyOut { get; set; }
            public long CurrencyDeposited { get; set; }
            public long CurrencySurplus { get; set; }
            public Dictionary<string, long> Flags { get; set; } = new();
            public long FlaggedAmount { get; set; }
            public long Score { get; set; }
        }

        public class EarnerRow
        {
            public uint Id { get; set; }
            public string Name { get; set; }
            public string AccountName { get; set; }
            public long BankIn { get; set; }
            public long BankOut { get; set; }
            public long Net { get; set; }
            public List<SourceTotal> BySource { get; set; } = new();
        }

        public class VendorRow
        {
            public uint VendorWcid { get; set; }
            public string VendorName { get; set; }
            public double? BuyPriceNow { get; set; }
            public long Payout { get; set; }
            public long Sales { get; set; }
            public long Characters { get; set; }
            public long Accounts { get; set; }
        }

        public class VendorSellerRow
        {
            public uint CharId { get; set; }
            public string CharName { get; set; }
            public uint AccountId { get; set; }
            public string AccountName { get; set; }
            public long Payout { get; set; }
            public long Sales { get; set; }
            public DateTime FirstHourUtc { get; set; }
            public DateTime LastHourUtc { get; set; }
        }

        public class FlagRow
        {
            public long Id { get; set; }
            public DateTime Utc { get; set; }
            public uint CharId { get; set; }
            public string CharName { get; set; }
            public uint AccountId { get; set; }
            public string AccountName { get; set; }
            public string Flag { get; set; }
            public long Amount { get; set; }
            public long Expected { get; set; }
            public long Actual { get; set; }
            public string Detail { get; set; }
        }

        public class DetailRow
        {
            public string Source { get; set; }
            public string DetailKey { get; set; }
            public string DetailName { get; set; }
            public long AmountIn { get; set; }
            public long AmountOut { get; set; }
            public long Events { get; set; }
        }

        public class ItemDetailRow
        {
            public string Direction { get; set; }
            public string Source { get; set; }
            public uint Wcid { get; set; }
            public string ItemName { get; set; }
            public string DetailName { get; set; }
            public long Units { get; set; }
            public long Value { get; set; }
        }

        public class HourRow
        {
            public DateTime HourUtc { get; set; }
            public long BankIn { get; set; }
            public long BankOut { get; set; }
        }

        public class CharacterSummary
        {
            public uint CharId { get; set; }
            public string Name { get; set; }
            public long Balance { get; set; }
            public long BankIn { get; set; }
            public long BankOut { get; set; }
        }

        public class StateRow
        {
            public long LiveBalance { get; set; }
            public long SavedBalance { get; set; }
            public long CurrencyPosition { get; set; }
            public DateTime? SavedUtc { get; set; }
            public string PendingNotes { get; set; }
            public DateTime UpdatedUtc { get; set; }
        }

        public class Detail
        {
            public string Kind { get; set; }
            public uint Id { get; set; }
            public string Name { get; set; }
            public uint AccountId { get; set; }
            public string AccountName { get; set; }
            public int Days { get; set; }
            public long? Balance { get; set; }
            /// <summary>
            /// Face value of currency this character (or account) should be holding: held when tracking began + seen
            /// arriving - seen leaving, all time. Negative means they got rid of currency never seen arriving.
            /// </summary>
            public long CurrencyPosition { get; set; }
            public StateRow State { get; set; }
            public List<CharacterSummary> Characters { get; set; } = new();
            public List<DetailRow> Bank { get; set; } = new();
            public List<ItemDetailRow> Items { get; set; } = new();
            public List<HourRow> Hours { get; set; } = new();
            public List<SoldRow> Sold { get; set; } = new();
            public List<FlagRow> Flags { get; set; } = new();
        }

        public class ItemSaleRow
        {
            public uint Wcid { get; set; }
            public string ItemName { get; set; }
            public long Units { get; set; }
            public long Payout { get; set; }
            public long Characters { get; set; }
            public long Accounts { get; set; }
            public string TopSeller { get; set; }
            public uint TopSellerCharId { get; set; }
            public long TopSellerUnits { get; set; }
        }

        public class ItemSellerRow
        {
            public uint CharId { get; set; }
            public string CharName { get; set; }
            public uint AccountId { get; set; }
            public string AccountName { get; set; }
            public long Units { get; set; }
            public long Payout { get; set; }
            public string Vendors { get; set; }
            public DateTime FirstHourUtc { get; set; }
            public DateTime LastHourUtc { get; set; }
        }

        public class SoldRow
        {
            public uint Wcid { get; set; }
            public string ItemName { get; set; }
            public uint VendorWcid { get; set; }
            public string VendorName { get; set; }
            public long Units { get; set; }
            public long Payout { get; set; }
        }

        public class NpcRow
        {
            public uint NpcWcid { get; set; }
            public string NpcName { get; set; }
            /// <summary>Face value of coins, notes and peas handed out.</summary>
            public long CurrencyValue { get; set; }
            public long Units { get; set; }
            /// <summary>Pyreals credited straight to the bank by this NPC's emotes.</summary>
            public long BankIn { get; set; }
            public long Total { get; set; }
            public long Gives { get; set; }
            public long Characters { get; set; }
            public long Accounts { get; set; }
            public string TopReceiver { get; set; }
            public uint TopReceiverCharId { get; set; }
            public long TopReceiverValue { get; set; }
        }

        public class NpcReceiverRow
        {
            public uint CharId { get; set; }
            public string CharName { get; set; }
            public uint AccountId { get; set; }
            public string AccountName { get; set; }
            public long CurrencyValue { get; set; }
            public long Units { get; set; }
            public long BankIn { get; set; }
            public long Total { get; set; }
            public long Gives { get; set; }
            public DateTime FirstHourUtc { get; set; }
            public DateTime LastHourUtc { get; set; }
        }

        public class SearchRow
        {
            public uint CharId { get; set; }
            public string Name { get; set; }
            public uint AccountId { get; set; }
            public string AccountName { get; set; }
            public long Balance { get; set; }
        }

        // ---- helpers ---------------------------------------------------------------------------------------------

        // The SQL filters use the same constants the C# comparisons do. These are compile-time constants, not input.
        private const int KBank = PyrealLedger.KindBank;
        private const int KItemIn = PyrealLedger.KindItemIn;
        private const int KItemOut = PyrealLedger.KindItemOut;
        private const int KSold = PyrealLedger.KindSold;
        private const string VendorSell = PyrealLedger.SrcVendorSell;

        public static int ClampDays(int days) => Math.Clamp(days <= 0 ? 7 : days, 1, MaxDays);

        private static DateTime Since(int days) => DateTime.UtcNow.AddDays(-ClampDays(days));

        private static List<T> Query<T>(string sql, Action<DbCommand> bind, Func<DbDataReader, T> map)
        {
            using var ctx = new ShardDbContext();
            var con = ctx.Database.GetDbConnection();
            if (con.State != ConnectionState.Open)
                con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 60;
            bind?.Invoke(cmd);

            var result = new List<T>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(map(reader));
            return result;
        }

        private static void P(DbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        private static long L(DbDataReader r, int i) => r.IsDBNull(i) ? 0 : Convert.ToInt64(r.GetValue(i));
        private static uint U(DbDataReader r, int i) => r.IsDBNull(i) ? 0 : Convert.ToUInt32(r.GetValue(i));
        private static string S(DbDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetValue(i).ToString();
        private static DateTime D(DbDataReader r, int i) => DateTime.SpecifyKind(Convert.ToDateTime(r.GetValue(i)), DateTimeKind.Utc);

        private static readonly Dictionary<uint, string> accountNameCache = new();

        public static string AccountName(uint accountId)
        {
            if (accountId == 0)
                return "";

            lock (accountNameCache)
            {
                if (accountNameCache.TryGetValue(accountId, out var cached))
                    return cached;
            }

            string name;
            try
            {
                name = DatabaseManager.Authentication.GetAccountById(accountId)?.AccountName ?? $"#{accountId}";
            }
            catch
            {
                // not cached: the auth database may answer next time
                return $"#{accountId}";
            }

            lock (accountNameCache)
                accountNameCache[accountId] = name;

            return name;
        }

        private static string ItemName(uint wcid)
        {
            try
            {
                var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
                if (weenie?.PropertiesString != null && weenie.PropertiesString.TryGetValue(PropertyString.Name, out var n))
                    return n;
            }
            catch
            {
            }
            return $"wcid {wcid}";
        }

        private static double? VendorBuyPrice(uint wcid)
        {
            try
            {
                var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
                if (weenie?.PropertiesFloat != null && weenie.PropertiesFloat.TryGetValue(PropertyFloat.BuyPrice, out var v))
                    return v;
            }
            catch
            {
            }
            return null;
        }

        private static (uint wcid, string rest) SplitItemKey(string detailKey)
        {
            var i = detailKey.IndexOf(':');
            var wcidText = i >= 0 ? detailKey.Substring(0, i) : detailKey;
            uint.TryParse(wcidText, out var wcid);
            return (wcid, i >= 0 ? detailKey.Substring(i + 1) : "");
        }

        private static string Dir(long kind) => kind == PyrealLedger.KindItemIn ? "in" : "out";

        // ---- reports ---------------------------------------------------------------------------------------------

        public static Overview GetOverview(int days)
        {
            days = ClampDays(days);
            var since = Since(days);
            var result = new Overview { Days = days, LedgerStartUtc = PyrealLedger.LedgerStartUtc, LedgerRunning = PyrealLedger.IsInitialized };

            result.BankTotals = Query(
                "SELECT `source`, SUM(`amount_in`), SUM(`amount_out`), SUM(`events`) FROM `pyreal_ledger_hourly` " +
                $"WHERE `kind` = {KBank} AND `hour_utc` >= @since GROUP BY `source` ORDER BY SUM(`amount_in`) DESC",
                c => P(c, "@since", since),
                r => new SourceTotal { Source = S(r, 0), AmountIn = L(r, 1), AmountOut = L(r, 2), Events = L(r, 3) });

            result.ItemTotals = Query(
                "SELECT `kind`, `source`, SUM(`amount_in` + `amount_out`), SUM(`units`) FROM `pyreal_ledger_hourly` " +
                $"WHERE `kind` IN ({KItemIn}, {KItemOut}) AND `hour_utc` >= @since GROUP BY `kind`, `source` ORDER BY SUM(`amount_in` + `amount_out`) DESC",
                c => P(c, "@since", since),
                r => new ItemTotal { Direction = Dir(L(r, 0)), Source = S(r, 1), Value = L(r, 2), Units = L(r, 3) });

            result.FlagCounts = Query(
                "SELECT `flag`, COUNT(*), SUM(`amount`) FROM `pyreal_ledger_flags` WHERE `utc` >= @since GROUP BY `flag` ORDER BY COUNT(*) DESC",
                c => P(c, "@since", since),
                r => new FlagCount { Flag = S(r, 0), Count = L(r, 1), Amount = L(r, 2) });

            return result;
        }

        /// <summary>Flags whose amount is pyreals a character gained without a recorded reason.</summary>
        private static readonly string[] gainFlags = { PyrealLedger.FlagLikelyDupe, PyrealLedger.FlagRollbackGain, PyrealLedger.FlagUnexplained, PyrealLedger.FlagTampered, PyrealLedger.FlagBypass };

        /// <summary>
        /// Accounts ranked by pyreals that do not add up: flagged gains (dupes, rollbacks, outside edits, tampered
        /// deposits) plus currency items they disposed of beyond what they were recorded receiving.
        /// </summary>
        public static List<SuspectRow> GetSuspects(int days, int limit)
        {
            days = ClampDays(days);
            limit = Math.Clamp(limit <= 0 ? 100 : limit, 1, 500);
            var since = Since(days);
            var rows = new Dictionary<uint, SuspectRow>();

            SuspectRow Row(uint accountId)
            {
                if (!rows.TryGetValue(accountId, out var row))
                    rows[accountId] = row = new SuspectRow { AccountId = accountId };
                return row;
            }

            foreach (var (acct, src, kind, amtIn, amtOut) in Query(
                "SELECT `account_id`, `source`, `kind`, SUM(`amount_in`), SUM(`amount_out`) FROM `pyreal_ledger_hourly` " +
                "WHERE `hour_utc` >= @since GROUP BY `account_id`, `source`, `kind`",
                c => P(c, "@since", since),
                r => (U(r, 0), S(r, 1), L(r, 2), L(r, 3), L(r, 4))))
            {
                var row = Row(acct);
                if (kind == PyrealLedger.KindBank)
                {
                    row.BankIn += amtIn;
                    row.BankOut += amtOut;
                    if (src == PyrealLedger.SrcVendorSell)
                        row.VendorSellIn += amtIn;
                    if (src == PyrealLedger.SrcUnattributed)
                        row.UnattributedIn += amtIn;
                }
                else if (kind == PyrealLedger.KindItemIn)
                    row.CurrencyIn += amtIn;
                else if (kind == PyrealLedger.KindItemOut)
                {
                    row.CurrencyOut += amtOut;
                    if (src == PyrealLedger.SrcDeposit)
                        row.CurrencyDeposited += amtOut;
                }
            }

            foreach (var (acct, vendor, payout) in Query(
                "SELECT `account_id`, `detail_name`, SUM(`amount_in`) FROM `pyreal_ledger_hourly` " +
                $"WHERE `kind` = {KBank} AND `source` = '{VendorSell}' AND `hour_utc` >= @since GROUP BY `account_id`, `detail_key`, `detail_name`",
                c => P(c, "@since", since),
                r => (U(r, 0), S(r, 1), L(r, 2))))
            {
                var row = Row(acct);
                if (payout > row.TopVendorPayout)
                {
                    row.TopVendorPayout = payout;
                    row.TopVendor = vendor;
                }
            }

            foreach (var (acct, flag, count, amount) in Query(
                "SELECT `account_id`, `flag`, COUNT(*), SUM(GREATEST(`amount`, 0)) FROM `pyreal_ledger_flags` WHERE `utc` >= @since GROUP BY `account_id`, `flag`",
                c => P(c, "@since", since),
                r => (U(r, 0), S(r, 1), L(r, 2), L(r, 3))))
            {
                var row = Row(acct);
                row.Flags[flag] = count;
                if (gainFlags.Contains(flag))
                    row.FlaggedAmount += amount;
            }

            foreach (var (acct, names) in Query(
                "SELECT `account_id`, GROUP_CONCAT(DISTINCT `char_name` ORDER BY `char_name` SEPARATOR ', ') FROM `pyreal_ledger_hourly` " +
                "WHERE `hour_utc` >= @since GROUP BY `account_id`",
                c => P(c, "@since", since),
                r => (U(r, 0), S(r, 1))))
            {
                if (rows.TryGetValue(acct, out var row))
                    row.Characters = names.Length > 200 ? names.Substring(0, 200) + "..." : names;
            }

            // Surplus is all-time, not per period: an account's running currency position (held when tracking began +
            // seen arriving - seen leaving) below zero means it got rid of currency it was never seen receiving.
            foreach (var (acct, position) in Query(
                "SELECT `account_id`, SUM(`currency_position`) FROM `pyreal_ledger_state` GROUP BY `account_id` HAVING SUM(`currency_position`) < 0",
                null,
                r => (U(r, 0), L(r, 1))))
            {
                Row(acct).CurrencySurplus = -position;
            }

            foreach (var row in rows.Values)
            {
                row.AccountName = AccountName(row.AccountId);
                row.Score = row.FlaggedAmount + Math.Max(0, row.CurrencySurplus) + row.UnattributedIn;
            }

            return rows.Values
                .OrderByDescending(r => r.Score)
                .ThenByDescending(r => r.BankIn)
                .Take(limit)
                .ToList();
        }

        public static List<EarnerRow> GetEarners(int days, bool byAccount, int limit)
        {
            days = ClampDays(days);
            limit = Math.Clamp(limit <= 0 ? 100 : limit, 1, 500);
            var since = Since(days);
            var idCol = byAccount ? "`account_id`" : "`char_id`";

            var rows = new Dictionary<uint, EarnerRow>();
            foreach (var (id, name, acct, src, amtIn, amtOut, events) in Query(
                $"SELECT {idCol}, MAX(`char_name`), MAX(`account_id`), `source`, SUM(`amount_in`), SUM(`amount_out`), SUM(`events`) FROM `pyreal_ledger_hourly` " +
                $"WHERE `kind` = {KBank} AND `hour_utc` >= @since GROUP BY {idCol}, `source`",
                c => P(c, "@since", since),
                r => (U(r, 0), S(r, 1), U(r, 2), S(r, 3), L(r, 4), L(r, 5), L(r, 6))))
            {
                if (!rows.TryGetValue(id, out var row))
                    rows[id] = row = new EarnerRow { Id = id, Name = name, AccountName = AccountName(acct) };

                row.BankIn += amtIn;
                row.BankOut += amtOut;
                row.BySource.Add(new SourceTotal { Source = src, AmountIn = amtIn, AmountOut = amtOut, Events = events });
            }

            foreach (var row in rows.Values)
            {
                row.Net = row.BankIn - row.BankOut;
                row.BySource = row.BySource.OrderByDescending(s => s.AmountIn).ToList();
                if (byAccount)
                    row.Name = row.AccountName;
            }

            return rows.Values.OrderByDescending(r => r.BankIn).Take(limit).ToList();
        }

        public static List<VendorRow> GetVendors(int days)
        {
            var since = Since(days);

            var rows = Query(
                "SELECT `detail_key`, MAX(`detail_name`), SUM(`amount_in`), SUM(`events`), COUNT(DISTINCT `char_id`), COUNT(DISTINCT `account_id`) FROM `pyreal_ledger_hourly` " +
                $"WHERE `kind` = {KBank} AND `source` = '{VendorSell}' AND `hour_utc` >= @since GROUP BY `detail_key` ORDER BY SUM(`amount_in`) DESC",
                c => P(c, "@since", since),
                r => new VendorRow
                {
                    VendorWcid = uint.TryParse(S(r, 0), out var w) ? w : 0,
                    VendorName = S(r, 1),
                    Payout = L(r, 2),
                    Sales = L(r, 3),
                    Characters = L(r, 4),
                    Accounts = L(r, 5),
                });

            foreach (var row in rows)
                row.BuyPriceNow = VendorBuyPrice(row.VendorWcid);

            return rows;
        }

        public static List<VendorSellerRow> GetVendorSellers(uint vendorWcid, int days)
        {
            var since = Since(days);

            var rows = Query(
                "SELECT `char_id`, MAX(`char_name`), MAX(`account_id`), SUM(`amount_in`), SUM(`events`), MIN(`hour_utc`), MAX(`hour_utc`) FROM `pyreal_ledger_hourly` " +
                $"WHERE `kind` = {KBank} AND `source` = '{VendorSell}' AND `detail_key` = @v AND `hour_utc` >= @since GROUP BY `char_id` ORDER BY SUM(`amount_in`) DESC LIMIT 500",
                c => { P(c, "@since", since); P(c, "@v", vendorWcid.ToString()); },
                r => new VendorSellerRow { CharId = U(r, 0), CharName = S(r, 1), AccountId = U(r, 2), Payout = L(r, 3), Sales = L(r, 4), FirstHourUtc = D(r, 5), LastHourUtc = D(r, 6) });

            foreach (var row in rows)
                row.AccountName = AccountName(row.AccountId);

            return rows;
        }

        public static List<FlagRow> GetFlags(int days, string flag, uint? charId, uint? accountId, int limit)
        {
            var since = Since(days);
            limit = Math.Clamp(limit <= 0 ? 200 : limit, 1, 1000);

            var where = "`utc` >= @since";
            if (!string.IsNullOrWhiteSpace(flag)) where += " AND `flag` = @flag";
            if (charId.HasValue) where += " AND `char_id` = @char";
            if (accountId.HasValue) where += " AND `account_id` = @acct";

            var rows = Query(
                $"SELECT `id`, `utc`, `char_id`, `char_name`, `account_id`, `flag`, `amount`, `expected`, `actual`, `detail` FROM `pyreal_ledger_flags` WHERE {where} ORDER BY `utc` DESC LIMIT {limit}",
                c =>
                {
                    P(c, "@since", since);
                    if (!string.IsNullOrWhiteSpace(flag)) P(c, "@flag", flag);
                    if (charId.HasValue) P(c, "@char", charId.Value);
                    if (accountId.HasValue) P(c, "@acct", accountId.Value);
                },
                r => new FlagRow { Id = L(r, 0), Utc = D(r, 1), CharId = U(r, 2), CharName = S(r, 3), AccountId = U(r, 4), Flag = S(r, 5), Amount = L(r, 6), Expected = L(r, 7), Actual = L(r, 8), Detail = S(r, 9) });

            foreach (var row in rows)
                row.AccountName = AccountName(row.AccountId);

            return rows;
        }

        public static Detail GetCharacter(uint charId, int days)
        {
            days = ClampDays(days);
            var since = Since(days);
            var player = PlayerManager.FindByGuid(charId);

            var detail = new Detail { Kind = "character", Id = charId, Days = days, Name = player?.Name ?? "", AccountId = player?.Account?.AccountId ?? 0 };
            detail.Balance = player?.GetProperty(PropertyInt64.BankedPyreals) ?? 0;

            FillDetail(detail, "`char_id` = @id", charId, since);

            var state = Query(
                "SELECT `live_balance`, `saved_balance`, `saved_utc`, `pending_notes`, `updated_utc`, `account_id`, `char_name`, `currency_position` FROM `pyreal_ledger_state` WHERE `char_id` = @id",
                c => P(c, "@id", charId),
                r => (new StateRow { LiveBalance = L(r, 0), SavedBalance = L(r, 1), SavedUtc = r.IsDBNull(2) ? null : D(r, 2), PendingNotes = S(r, 3), UpdatedUtc = D(r, 4), CurrencyPosition = L(r, 7) }, U(r, 5), S(r, 6)))
                .FirstOrDefault();

            if (state.Item1 != null)
            {
                detail.State = state.Item1;
                detail.CurrencyPosition = state.Item1.CurrencyPosition;
                if (detail.AccountId == 0) detail.AccountId = state.Item2;
                if (string.IsNullOrEmpty(detail.Name)) detail.Name = state.Item3;
            }

            detail.AccountName = AccountName(detail.AccountId);
            return detail;
        }

        public static Detail GetAccount(uint accountId, int days)
        {
            days = ClampDays(days);
            var since = Since(days);

            var detail = new Detail { Kind = "account", Id = accountId, AccountId = accountId, AccountName = AccountName(accountId), Days = days };
            detail.Name = detail.AccountName;

            FillDetail(detail, "`account_id` = @id", accountId, since);

            var perChar = Query(
                $"SELECT `char_id`, MAX(`char_name`), SUM(`amount_in`), SUM(`amount_out`) FROM `pyreal_ledger_hourly` WHERE `account_id` = @id AND `kind` = {KBank} AND `hour_utc` >= @since GROUP BY `char_id`",
                c => { P(c, "@id", accountId); P(c, "@since", since); },
                r => (U(r, 0), S(r, 1), L(r, 2), L(r, 3)))
                .ToDictionary(x => x.Item1);

            foreach (var player in PlayerManager.GetAllPlayers().Where(p => p.Account?.AccountId == accountId))
            {
                perChar.TryGetValue(player.Guid.Full, out var flows);
                detail.Characters.Add(new CharacterSummary
                {
                    CharId = player.Guid.Full,
                    Name = player.Name,
                    Balance = player.GetProperty(PropertyInt64.BankedPyreals) ?? 0,
                    BankIn = flows.Item3,
                    BankOut = flows.Item4,
                });
            }

            detail.Characters = detail.Characters.OrderByDescending(c => c.Balance).ToList();
            detail.Balance = detail.Characters.Sum(c => c.Balance);
            detail.CurrencyPosition = Query(
                "SELECT COALESCE(SUM(`currency_position`), 0) FROM `pyreal_ledger_state` WHERE `account_id` = @id",
                c => P(c, "@id", accountId),
                r => L(r, 0)).FirstOrDefault();
            return detail;
        }

        private static void FillDetail(Detail detail, string idWhere, uint id, DateTime since)
        {
            detail.Bank = Query(
                $"SELECT `source`, `detail_key`, MAX(`detail_name`), SUM(`amount_in`), SUM(`amount_out`), SUM(`events`) FROM `pyreal_ledger_hourly` " +
                $"WHERE {idWhere} AND `kind` = {KBank} AND `hour_utc` >= @since GROUP BY `source`, `detail_key` ORDER BY SUM(`amount_in`) + SUM(`amount_out`) DESC LIMIT 300",
                c => { P(c, "@id", id); P(c, "@since", since); },
                r => new DetailRow { Source = S(r, 0), DetailKey = S(r, 1), DetailName = S(r, 2), AmountIn = L(r, 3), AmountOut = L(r, 4), Events = L(r, 5) });

            detail.Items = Query(
                $"SELECT `kind`, `source`, `detail_key`, MAX(`detail_name`), SUM(`units`), SUM(`amount_in` + `amount_out`) FROM `pyreal_ledger_hourly` " +
                $"WHERE {idWhere} AND `kind` IN ({KItemIn}, {KItemOut}) AND `hour_utc` >= @since GROUP BY `kind`, `source`, `detail_key` ORDER BY SUM(`amount_in` + `amount_out`) DESC LIMIT 300",
                c => { P(c, "@id", id); P(c, "@since", since); },
                r =>
                {
                    var (wcid, _) = SplitItemKey(S(r, 2));
                    return new ItemDetailRow { Direction = Dir(L(r, 0)), Source = S(r, 1), Wcid = wcid, ItemName = ItemName(wcid), DetailName = S(r, 3), Units = L(r, 4), Value = L(r, 5) };
                });

            detail.Sold = Query(
                $"SELECT `detail_key`, MAX(`detail_name`), SUM(`units`), SUM(`amount_in`) FROM `pyreal_ledger_hourly` " +
                $"WHERE {idWhere} AND `kind` = {KSold} AND `source` = '{VendorSell}' AND `hour_utc` >= @since GROUP BY `detail_key` ORDER BY SUM(`amount_in`) DESC LIMIT 300",
                c => { P(c, "@id", id); P(c, "@since", since); },
                r =>
                {
                    var (wcid, rest) = SplitItemKey(S(r, 0));
                    uint.TryParse(rest, out var vendorWcid);
                    return new SoldRow { Wcid = wcid, ItemName = ItemName(wcid), VendorWcid = vendorWcid, VendorName = S(r, 1), Units = L(r, 2), Payout = L(r, 3) };
                });

            detail.Hours = Query(
                $"SELECT `hour_utc`, SUM(`amount_in`), SUM(`amount_out`) FROM `pyreal_ledger_hourly` WHERE {idWhere} AND `kind` = {KBank} AND `hour_utc` >= @since GROUP BY `hour_utc` ORDER BY `hour_utc`",
                c => { P(c, "@id", id); P(c, "@since", since); },
                r => new HourRow { HourUtc = D(r, 0), BankIn = L(r, 1), BankOut = L(r, 2) });

            detail.Flags = Query(
                $"SELECT `id`, `utc`, `char_id`, `char_name`, `account_id`, `flag`, `amount`, `expected`, `actual`, `detail` FROM `pyreal_ledger_flags` WHERE {idWhere} AND `utc` >= @since ORDER BY `utc` DESC LIMIT 200",
                c => { P(c, "@id", id); P(c, "@since", since); },
                r => new FlagRow { Id = L(r, 0), Utc = D(r, 1), CharId = U(r, 2), CharName = S(r, 3), AccountId = U(r, 4), Flag = S(r, 5), Amount = L(r, 6), Expected = L(r, 7), Actual = L(r, 8), Detail = S(r, 9) });

            foreach (var f in detail.Flags)
                f.AccountName = AccountName(f.AccountId);
        }

        /// <summary>Items sold to vendors, ranked by payout, with the character who sold the most of each.</summary>
        public static List<ItemSaleRow> GetItemSales(int days, int limit)
        {
            var since = Since(days);
            limit = Math.Clamp(limit <= 0 ? 200 : limit, 1, 1000);

            var rows = Query(
                "SELECT SUBSTRING_INDEX(`detail_key`, ':', 1) AS `wcid`, SUM(`units`), SUM(`amount_in`), COUNT(DISTINCT `char_id`), COUNT(DISTINCT `account_id`) FROM `pyreal_ledger_hourly` " +
                $"WHERE `kind` = {KSold} AND `source` = '{VendorSell}' AND `hour_utc` >= @since GROUP BY `wcid` ORDER BY SUM(`amount_in`) DESC LIMIT {limit}",
                c => P(c, "@since", since),
                r => new ItemSaleRow { Wcid = uint.TryParse(S(r, 0), out var w) ? w : 0, Units = L(r, 1), Payout = L(r, 2), Characters = L(r, 3), Accounts = L(r, 4) });

            if (rows.Count == 0)
                return rows;

            // the top seller of each listed item
            var top = new Dictionary<uint, (uint charId, string name, long units)>();
            foreach (var (wcid, charId, name, units) in Query(
                "SELECT SUBSTRING_INDEX(`detail_key`, ':', 1) AS `wcid`, `char_id`, MAX(`char_name`), SUM(`units`) FROM `pyreal_ledger_hourly` " +
                $"WHERE `kind` = {KSold} AND `source` = '{VendorSell}' AND `hour_utc` >= @since GROUP BY `wcid`, `char_id`",
                c => P(c, "@since", since),
                r => (uint.TryParse(S(r, 0), out var w) ? w : 0, U(r, 1), S(r, 2), L(r, 3))))
            {
                if (!top.TryGetValue(wcid, out var best) || units > best.units)
                    top[wcid] = (charId, name, units);
            }

            foreach (var row in rows)
            {
                row.ItemName = ItemName(row.Wcid);
                if (top.TryGetValue(row.Wcid, out var best))
                {
                    row.TopSeller = best.name;
                    row.TopSellerCharId = best.charId;
                    row.TopSellerUnits = best.units;
                }
            }

            return rows;
        }

        /// <summary>Who sold one item type to vendors, most units first.</summary>
        public static List<ItemSellerRow> GetItemSellers(uint wcid, int days)
        {
            var since = Since(days);

            var rows = Query(
                "SELECT `char_id`, MAX(`char_name`), MAX(`account_id`), SUM(`units`), SUM(`amount_in`), GROUP_CONCAT(DISTINCT `detail_name` SEPARATOR ', '), MIN(`hour_utc`), MAX(`hour_utc`) " +
                $"FROM `pyreal_ledger_hourly` WHERE `kind` = {KSold} AND `source` = '{VendorSell}' AND `detail_key` LIKE @prefix AND `hour_utc` >= @since GROUP BY `char_id` ORDER BY SUM(`units`) DESC LIMIT 500",
                c => { P(c, "@since", since); P(c, "@prefix", wcid + ":%"); },
                r => new ItemSellerRow { CharId = U(r, 0), CharName = S(r, 1), AccountId = U(r, 2), Units = L(r, 3), Payout = L(r, 4), Vendors = S(r, 5), FirstHourUtc = D(r, 6), LastHourUtc = D(r, 7) });

            foreach (var row in rows)
                row.AccountName = AccountName(row.AccountId);

            return rows;
        }

        // An NPC's emote rows: currency items it gave (detail_key "itemWcid:npcWcid") and pyreals it credited straight to
        // the bank (detail_key "npcWcid"). SUBSTRING_INDEX(key, ':', -1) is the NPC wcid for both shapes.
        private static readonly string NpcRowsWhere =
            $"`source` = '{PyrealLedger.SrcEmote}' AND `kind` IN ({KBank}, {KItemIn}) AND `hour_utc` >= @since";

        /// <summary>NPCs ranked by the currency their quest rewards handed out, with the character who received the most.</summary>
        public static List<NpcRow> GetNpcs(int days, int limit)
        {
            var since = Since(days);
            limit = Math.Clamp(limit <= 0 ? 200 : limit, 1, 1000);

            var rows = Query(
                "SELECT SUBSTRING_INDEX(`detail_key`, ':', -1) AS `npc`, MAX(`detail_name`), " +
                $"SUM(CASE WHEN `kind` = {KItemIn} THEN `amount_in` ELSE 0 END), SUM(CASE WHEN `kind` = {KItemIn} THEN `units` ELSE 0 END), " +
                $"SUM(CASE WHEN `kind` = {KBank} THEN `amount_in` ELSE 0 END), SUM(`events`), COUNT(DISTINCT `char_id`), COUNT(DISTINCT `account_id`) " +
                $"FROM `pyreal_ledger_hourly` WHERE {NpcRowsWhere} GROUP BY `npc` ORDER BY SUM(`amount_in`) DESC LIMIT {limit}",
                c => P(c, "@since", since),
                r => new NpcRow
                {
                    NpcWcid = uint.TryParse(S(r, 0), out var w) ? w : 0,
                    NpcName = S(r, 1),
                    CurrencyValue = L(r, 2),
                    Units = L(r, 3),
                    BankIn = L(r, 4),
                    Gives = L(r, 5),
                    Characters = L(r, 6),
                    Accounts = L(r, 7),
                });

            if (rows.Count == 0)
                return rows;

            var top = new Dictionary<uint, (uint charId, string name, long value)>();
            foreach (var (npc, charId, name, value) in Query(
                "SELECT SUBSTRING_INDEX(`detail_key`, ':', -1) AS `npc`, `char_id`, MAX(`char_name`), SUM(`amount_in`) " +
                $"FROM `pyreal_ledger_hourly` WHERE {NpcRowsWhere} GROUP BY `npc`, `char_id`",
                c => P(c, "@since", since),
                r => (uint.TryParse(S(r, 0), out var w) ? w : 0, U(r, 1), S(r, 2), L(r, 3))))
            {
                if (!top.TryGetValue(npc, out var best) || value > best.value)
                    top[npc] = (charId, name, value);
            }

            foreach (var row in rows)
            {
                row.Total = row.CurrencyValue + row.BankIn;
                if (string.IsNullOrEmpty(row.NpcName))
                    row.NpcName = ItemName(row.NpcWcid);
                if (top.TryGetValue(row.NpcWcid, out var best))
                {
                    row.TopReceiver = best.name;
                    row.TopReceiverCharId = best.charId;
                    row.TopReceiverValue = best.value;
                }
            }

            return rows;
        }

        /// <summary>Who received currency from one NPC, most first.</summary>
        public static List<NpcReceiverRow> GetNpcReceivers(uint npcWcid, int days)
        {
            var since = Since(days);

            var rows = Query(
                "SELECT `char_id`, MAX(`char_name`), MAX(`account_id`), " +
                $"SUM(CASE WHEN `kind` = {KItemIn} THEN `amount_in` ELSE 0 END), SUM(CASE WHEN `kind` = {KItemIn} THEN `units` ELSE 0 END), " +
                $"SUM(CASE WHEN `kind` = {KBank} THEN `amount_in` ELSE 0 END), SUM(`events`), MIN(`hour_utc`), MAX(`hour_utc`) " +
                $"FROM `pyreal_ledger_hourly` WHERE {NpcRowsWhere} AND SUBSTRING_INDEX(`detail_key`, ':', -1) = @npc " +
                "GROUP BY `char_id` ORDER BY SUM(`amount_in`) DESC LIMIT 500",
                c => { P(c, "@since", since); P(c, "@npc", npcWcid.ToString()); },
                r => new NpcReceiverRow { CharId = U(r, 0), CharName = S(r, 1), AccountId = U(r, 2), CurrencyValue = L(r, 3), Units = L(r, 4), BankIn = L(r, 5), Gives = L(r, 6), FirstHourUtc = D(r, 7), LastHourUtc = D(r, 8) });

            foreach (var row in rows)
            {
                row.Total = row.CurrencyValue + row.BankIn;
                row.AccountName = AccountName(row.AccountId);
            }

            return rows;
        }

        public static List<SearchRow> Search(string text, int limit = 50)
        {
            text = (text ?? "").Trim();
            if (text.Length < 2)
                return new List<SearchRow>();

            var rows = new List<SearchRow>();

            foreach (var player in PlayerManager.GetAllPlayers())
            {
                var accountName = player.Account?.AccountName ?? "";
                if (player.Name.Contains(text, StringComparison.OrdinalIgnoreCase) || accountName.Contains(text, StringComparison.OrdinalIgnoreCase))
                {
                    rows.Add(new SearchRow
                    {
                        CharId = player.Guid.Full,
                        Name = player.Name,
                        AccountId = player.Account?.AccountId ?? 0,
                        AccountName = accountName,
                        Balance = player.GetProperty(PropertyInt64.BankedPyreals) ?? 0,
                    });
                }
            }

            return rows.OrderBy(r => r.Name.Length).ThenBy(r => r.Name).Take(limit).ToList();
        }
    }
}
