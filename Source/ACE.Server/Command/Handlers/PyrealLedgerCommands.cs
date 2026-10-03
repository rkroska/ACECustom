using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using log4net;

using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// @pyrealaudit: a short text version of the admin Pyreal Ledger page. Queries run off the world thread.
    /// Output is plain ASCII with explicit "\n" (the client draws other characters as garbage).
    /// </summary>
    public static class PyrealLedgerCommands
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static string N(long value) => value.ToString("N0");

        [CommandHandler("pyrealaudit", AccessLevel.Developer, CommandHandlerFlag.None, 0,
            "Pyreal ledger summary: suspects, vendor payouts and flags. The web portal Pyreal Ledger page has the full view.",
            "@pyrealaudit [days]              - suspects, top vendors and flags (default 7 days)\n" +
            "@pyrealaudit char <name> [days]  - one character: bank changes by source, currency items, flags")]
        public static void HandlePyrealAudit(Session session, params string[] parameters)
        {
            if (!PyrealLedger.IsInitialized)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "The pyreal ledger is not running (ServerConfig pyreal_ledger is off, or it failed to start - see the server log).");
                return;
            }

            var isChar = parameters.Length > 0 && parameters[0].Equals("char", StringComparison.OrdinalIgnoreCase);
            var days = 7;
            string name = null;

            if (isChar)
            {
                if (parameters.Length < 2)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, "Usage: @pyrealaudit char <name> [days]");
                    return;
                }

                // a trailing number is the day count, the rest is the name (names can have spaces)
                var parts = parameters.Skip(1).ToList();
                if (parts.Count > 1 && int.TryParse(parts[^1], out var d))
                {
                    days = d;
                    parts.RemoveAt(parts.Count - 1);
                }
                name = string.Join(" ", parts);
            }
            else if (parameters.Length > 0 && !int.TryParse(parameters[0], out days))
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Usage: @pyrealaudit [days] | @pyrealaudit char <name> [days]");
                return;
            }

            days = PyrealLedgerReports.ClampDays(days);

            Task.Run(() =>
            {
                try
                {
                    var text = isChar ? CharacterReport(name, days) : SummaryReport(days);
                    CommandHandlerHelper.WriteOutputInfo(session, text);
                }
                catch (Exception ex)
                {
                    log.Error($"[PyrealLedger] @pyrealaudit failed: {ex}");
                    try
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, "[ERROR] The pyreal ledger query failed - see the server log.");
                    }
                    catch
                    {
                        // the session went away while the query ran
                    }
                }
            });
        }

        [CommandHandler("pyrealledger", AccessLevel.Admin, CommandHandlerFlag.None, 0,
            "Pyreal ledger switch: shows whether it is recording, or turns it off without a restart.",
            "@pyrealledger       - is the ledger recording?\n" +
            "@pyrealledger off   - stop recording now. It stays off until the next server start; to keep it off after a restart also run @modifybool pyreal_ledger false")]
        public static void HandlePyrealLedger(Session session, params string[] parameters)
        {
            if (parameters.Length == 0 || parameters[0].Equals("status", StringComparison.OrdinalIgnoreCase))
            {
                CommandHandlerHelper.WriteOutputInfo(session, PyrealLedger.IsInitialized
                    ? $"The pyreal ledger is recording (since {PyrealLedger.LedgerStartUtc:yyyy-MM-dd HH:mm} UTC)."
                    : "The pyreal ledger is not recording.");
                return;
            }

            if (!parameters[0].Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Usage: @pyrealledger | @pyrealledger off");
                return;
            }

            // the final flush writes to the database, so keep it off the world thread
            Task.Run(() =>
            {
                try
                {
                    var stopped = PyrealLedger.StopLive();
                    CommandHandlerHelper.WriteOutputInfo(session, stopped
                        ? "[OK] The pyreal ledger is off. It starts again at the next server start unless you also run @modifybool pyreal_ledger false."
                        : "The pyreal ledger was already off.");
                }
                catch (Exception ex)
                {
                    log.Error($"[PyrealLedger] @pyrealledger off failed: {ex}");
                }
            });
        }

        private static string SummaryReport(int days)
        {
            var sb = new StringBuilder();
            var overview = PyrealLedgerReports.GetOverview(days);

            sb.Append($"Pyreal ledger - last {days} day(s), recording since {overview.LedgerStartUtc:yyyy-MM-dd HH:mm} UTC\n");

            if (overview.FlagCounts.Count > 0)
                sb.Append("Flags: " + string.Join(", ", overview.FlagCounts.Select(f => $"{f.Flag} {f.Count}")) + "\n");
            else
                sb.Append("Flags: none\n");

            var suspects = PyrealLedgerReports.GetSuspects(days, 5).Where(s => s.Score > 0).ToList();
            sb.Append("Top suspects (flagged gains + currency deposited beyond what they received):\n");
            if (suspects.Count == 0)
                sb.Append("  none\n");
            foreach (var s in suspects)
                sb.Append($"  {s.AccountName}: score {N(s.Score)}, flagged {N(s.FlaggedAmount)}, surplus {N(s.CurrencySurplus)}, bank in {N(s.BankIn)} ({s.Characters})\n");

            var items = PyrealLedgerReports.GetItemSales(days, 5);
            sb.Append("Top items sold to vendors:\n");
            if (items.Count == 0)
                sb.Append("  none\n");
            foreach (var i in items)
                sb.Append($"  {i.ItemName} (wcid {i.Wcid}): {N(i.Units)} sold for {N(i.Payout)}, most by {i.TopSeller} ({N(i.TopSellerUnits)})\n");

            var vendors = PyrealLedgerReports.GetVendors(days).Take(5).ToList();
            sb.Append("Top vendor payouts:\n");
            if (vendors.Count == 0)
                sb.Append("  none\n");
            foreach (var v in vendors)
                sb.Append($"  {v.VendorName} (wcid {v.VendorWcid}): {N(v.Payout)} to {v.Characters} character(s)\n");

            sb.Append("Full view: web portal > Monitoring > Pyreal Ledger");
            return sb.ToString();
        }

        private static string CharacterReport(string name, int days)
        {
            var player = PlayerManager.FindByName(name);
            if (player == null)
                return $"No character named {name}.";

            var detail = PyrealLedgerReports.GetCharacter(player.Guid.Full, days);
            var sb = new StringBuilder();

            sb.Append($"{detail.Name} ({detail.AccountName}) - banked {N(detail.Balance ?? 0)} - last {days} day(s)\n");

            sb.Append("Bank changes by source:\n");
            if (detail.Bank.Count == 0)
                sb.Append("  none\n");
            foreach (var row in detail.Bank.Take(10))
                sb.Append($"  {row.Source} {row.DetailName}: +{N(row.AmountIn)} / -{N(row.AmountOut)} ({row.Events} change(s))\n");

            sb.Append("Sold to vendors (top 5 by payout):\n");
            if (detail.Sold.Count == 0)
                sb.Append("  none\n");
            foreach (var sold in detail.Sold.Take(5))
                sb.Append($"  {N(sold.Units)} x {sold.ItemName} to {sold.VendorName}: {N(sold.Payout)}\n");

            var itemsIn = detail.Items.Where(i => i.Direction == "in").Sum(i => i.Value);
            var itemsOut = detail.Items.Where(i => i.Direction == "out").Sum(i => i.Value);
            sb.Append($"Currency items: received {N(itemsIn)}, gave up {N(itemsOut)} (face value)\n");

            sb.Append("Flags:\n");
            if (detail.Flags.Count == 0)
                sb.Append("  none\n");
            foreach (var f in detail.Flags.Take(5))
                sb.Append($"  {f.Utc:yyyy-MM-dd HH:mm} {f.Flag} {N(f.Amount)} - {f.Detail}\n");

            return sb.ToString().TrimEnd();
        }
    }
}
