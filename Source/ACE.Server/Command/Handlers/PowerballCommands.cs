using System.Linq;
using System.Text;

using ACE.Entity.Enum;
using ACE.Server.Command;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Command.Handlers
{
    public static class PowerballCommands
    {
        // ─────────────────────────────────────────────────────────────
        //  Internal helper
        // ─────────────────────────────────────────────────────────────
        private static void Reply(Session session, string msg)
        {
            session.Network.EnqueueSend(
                new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        }

        // ─────────────────────────────────────────────────────────────
        //  /powerball  (player-facing, many subcommands)
        // ─────────────────────────────────────────────────────────────
        [CommandHandler("powerball", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "View Powerball info, buy tickets, and check your tickets.",
            "/powerball               - show Powerball info\n" +
            "/powerball buy <qty>     - buy <qty> quick-pick tickets\n" +
            "/powerball ticket buy <qty> - same as /powerball buy\n" +
            "/powerball tickets       - view your tickets for this draw\n" +
            "/powerball help          - show this help")]
        public static void HandlePowerball(Session session, params string[] parameters)
        {
            var player = session.Player;
            if (player == null) return;

            var sub = parameters.Length > 0 ? parameters[0].ToLowerInvariant() : "";

            switch (sub)
            {
                case "":
                case "info":
                    Reply(session, PowerballManager.GetInfoBlock(player));
                    return;

                case "help":
                    var price = PowerballManager.FormatLum(ServerConfig.powerball_ticket_price.Value);
                    Reply(session,
                        "[Powerball] Commands:\n" +
                        $"  /pb                       - Show Powerball info\n" +
                        $"  /pb buy <qty>             - Buy <qty> quick-pick tickets ({price} each)\n" +
                        $"  /pb ticket buy <qty>      - Same as /pb buy\n" +
                        $"  /pb tickets               - View your tickets for this draw\n" +
                        $"  /pb help                  - Show this help");
                    return;

                case "tickets":
                    // Could be "/pb tickets" or "/pb ticket buy <qty>"
                    if (parameters.Length >= 3
                        && parameters[1].ToLowerInvariant() == "buy")
                    {
                        HandleBuySubcommand(session, player, parameters[2]);
                        return;
                    }
                    Reply(session, PowerballManager.BuildTicketDisplay(player.Guid.Full));
                    return;

                case "ticket":
                    // "/pb ticket buy <qty>"
                    if (parameters.Length >= 3
                        && parameters[1].ToLowerInvariant() == "buy")
                    {
                        HandleBuySubcommand(session, player, parameters[2]);
                        return;
                    }
                    // "/pb ticket" alone → show ticket display
                    Reply(session, PowerballManager.BuildTicketDisplay(player.Guid.Full));
                    return;

                case "buy":
                    // "/pb buy <qty>"
                    HandleBuySubcommand(session, player, parameters.Length >= 2 ? parameters[1] : "");
                    return;

                default:
                    // Maybe they typed "/pb 5" as shorthand for buy
                    if (int.TryParse(sub, out _))
                    {
                        HandleBuySubcommand(session, player, sub);
                        return;
                    }
                    Reply(session, PowerballManager.GetInfoBlock(player));
                    return;
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  /pb  (alias — forwards to /powerball handler)
        // ─────────────────────────────────────────────────────────────
        [CommandHandler("pb", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Alias for /powerball — Powerball lottery commands.",
            "/pb                    - show Powerball info\n" +
            "/pb buy <qty>          - buy tickets\n" +
            "/pb ticket buy <qty>   - buy tickets\n" +
            "/pb tickets            - view your tickets\n" +
            "/pb help               - show help")]
        public static void HandlePb(Session session, params string[] parameters)
            => HandlePowerball(session, parameters);

        // ─────────────────────────────────────────────────────────────
        //  Buy logic (shared between all entry points)
        // ─────────────────────────────────────────────────────────────
        private static void HandleBuySubcommand(Session session, Player player, string qtyStr)
        {
            if (string.IsNullOrWhiteSpace(qtyStr))
            {
                Reply(session, "[Powerball] Usage: /pb buy <quantity>   e.g. /pb buy 10");
                return;
            }

            if (!int.TryParse(qtyStr, out int qty) || qty < 1 || qty > 500)
            {
                Reply(session, "[Powerball] Please enter a quantity between 1 and 500.");
                return;
            }

            var (ok, msg) = PowerballManager.BuyTickets(player, qty);
            if (!ok)
            {
                Reply(session, msg);
                return;
            }

            var price    = ServerConfig.powerball_ticket_price.Value;
            var total    = price * qty;
            var myCount  = PowerballManager.GetPlayerTickets(player.Guid.Full).Count;

            Reply(session,
                $"[Powerball] You bought {qty} ticket{(qty == 1 ? "" : "s")} " +
                $"for {PowerballManager.FormatLum(total)}.");
            Reply(session,
                $"[Powerball] Draw #{PowerballManager.GetCurrentDrawId()} | " +
                $"Jackpot: {PowerballManager.FormatLum(PowerballManager.GetJackpotPool())} | " +
                $"Your tickets: {myCount}");
        }

        // ─────────────────────────────────────────────────────────────
        //  /dev powerball  — developer test commands
        // ─────────────────────────────────────────────────────────────
        [CommandHandler("dev", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1,
            "Developer utility commands — use 'powerball' or 'pb' subcommand for Powerball testing.",
            "/dev pb testdraw  - force a test draw (no payouts; adds the simulated NPC test pool first)\n" +
            "/dev pb test      - add the simulated NPC test pool\n" +
            "/dev pb clear     - clear all test tickets\n" +
            "/dev pb status    - show current pool status")]
        public static void HandleDevPowerball(Session session, params string[] parameters)
        {
            // Only intercept if first param is "powerball" or "pb"
            if (parameters.Length == 0
                || (parameters[0].ToLowerInvariant() != "powerball" && parameters[0].ToLowerInvariant() != "pb"))
            {
                Reply(session, "[dev] Unknown dev subcommand. Use /dev pb <subcommand>.");
                return;
            }

            var sub = parameters.Length >= 2 ? parameters[1].ToLowerInvariant() : "";

            switch (sub)
            {
                case "testdraw":
                {
                    Reply(session, "[Powerball] Adding the simulated NPC test pool and running a test draw...");
                    PowerballManager.AddTestTickets(150);
                    var result = PowerballManager.ExecuteDraw(isTestMode: true);
                    if (result == null)
                    {
                        Reply(session, "[Powerball] Test draw failed — no tickets.");
                        return;
                    }
                    Reply(session, PowerballManager.BuildDrawResultText(result));
                    break;
                }

                case "test":
                {
                    PowerballManager.AddTestTickets(150);
                    Reply(session,
                        $"[Powerball] Added the simulated NPC test pool. " +
                        $"Total test tickets: {PowerballManager.GetTestTicketCount()}");
                    break;
                }

                case "clear":
                {
                    PowerballManager.ClearTestTickets();
                    Reply(session, "[Powerball] Cleared all test tickets.");
                    break;
                }

                case "status":
                {
                    var sb = new StringBuilder();
                    sb.Append("[Powerball] Status:\n");
                    sb.Append($"  Draw ID    : {PowerballManager.GetCurrentDrawId()}\n");
                    sb.Append($"  Pool       : {PowerballManager.FormatLum(PowerballManager.GetJackpotPool())}\n");
                    sb.Append($"  Tickets    : {PowerballManager.GetTicketCount()} real  +  {PowerballManager.GetTestTicketCount()} test\n");
                    sb.Append($"  Next Draw  : {PowerballManager.GetNextDrawTimeFormatted()}");
                    Reply(session, sb.ToString());
                    break;
                }

                default:
                    Reply(session,
                        "[Powerball] Dev subcommands:\n" +
                        "  /dev pb testdraw  - force test draw (adds the simulated NPC test pool)\n" +
                        "  /dev pb test      - add the simulated NPC test pool\n" +
                        "  /dev pb clear     - clear test tickets\n" +
                        "  /dev pb status    - show pool status");
                    break;
            }
        }
    }
}
