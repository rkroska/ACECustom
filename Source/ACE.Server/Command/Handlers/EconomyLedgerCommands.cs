using System;
using System.Threading.Tasks;

using log4net;

using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    public static class EconomyLedgerCommands
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        [CommandHandler("economyledger", AccessLevel.Admin, CommandHandlerFlag.None, 0,
            "Economy ledger switch: shows whether currency flows are being recorded, turns recording on or off, or takes a supply snapshot.",
            "@economyledger           - is it recording, and when was the last supply snapshot?\n" +
            "@economyledger off       - stop recording flows now (supply snapshots continue)\n" +
            "@economyledger on        - start recording flows again\n" +
            "@economyledger snapshot  - add up the supply of every currency now\n" +
            "To keep a change after a restart also run @modifybool economy_ledger true|false")]
        public static void HandleEconomyLedger(Session session, params string[] parameters)
        {
            var action = parameters.Length == 0 ? "status" : parameters[0].ToLowerInvariant();

            switch (action)
            {
                case "status":
                    var snapshot = EconomyLedger.LastSnapshotUtc.HasValue ? $"{EconomyLedger.LastSnapshotUtc:yyyy-MM-dd HH:mm} UTC" : "never";
                    CommandHandlerHelper.WriteOutputInfo(session, !EconomyLedger.TablesReady
                        ? "The economy ledger is not running (it failed to start - see the server log)."
                        : EconomyLedger.FlowTracking
                            ? $"The economy ledger is recording flows (since {EconomyLedger.FlowTrackingSinceUtc:yyyy-MM-dd HH:mm} UTC). Last supply snapshot: {snapshot}."
                            : $"The economy ledger is NOT recording flows. Last supply snapshot: {snapshot}.");
                    return;

                case "snapshot":
                    CommandHandlerHelper.WriteOutputInfo(session, EconomyLedger.StartSnapshot("command")
                        ? "Supply snapshot started. It takes up to a minute."
                        : "[ERROR] A snapshot is already running, or the economy ledger is not running.");
                    return;

                case "on":
                case "off":
                    var on = action == "on";
                    // switching off writes the pending rows to the database, so keep it off the world thread
                    Task.Run(() =>
                    {
                        try
                        {
                            var changed = EconomyLedger.SetFlowTracking(on);
                            CommandHandlerHelper.WriteOutputInfo(session, changed
                                ? $"Economy ledger flow recording is now {(on ? "ON" : "OFF")}."
                                : $"No change: flow recording was already {(on ? "on" : "off")}, or the economy ledger is not running.");
                        }
                        catch (Exception ex)
                        {
                            log.Error($"[EconomyLedger] @economyledger {action} failed: {ex}");
                        }
                    });
                    return;

                default:
                    CommandHandlerHelper.WriteOutputInfo(session, "Usage: @economyledger | @economyledger on | off | snapshot");
                    return;
            }
        }
    }
}
