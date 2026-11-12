using System;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// /bounty (owner 2026-09-23): any player's view of the bounties where they stand. /bounty list (owner 2026-09-27): every
    /// bounty on the server and the player's progress on each, so they can choose where to go. See BountyManager.
    ///
    /// Item bounties (owner 2026-11-10, ItemBounties; double-click rework 2026-11-12): /bounty also lists the character's
    /// bounties going, #1 to #4 (item_bounty_max_active). A bounty is started by using it on the Bounty Journal, not here; the
    /// journal also shows everything /bounty does, so players need no command at all except to abandon one.
    /// /bounty abandon (remove, drop) asks for a console confirmation (/bounty confirm code, or /bounty cancel to drop the
    /// request). /bounty data and /bounty sync feed the Bounty Tracker window. Admins can view and abandon for another online
    /// player: /bounty Name, /bounty abandon 2 Name. Developers: /bounty make wcid [tier], /bounty droptable.
    /// </summary>
    public static class BountyCommands
    {
        /// <summary>
        /// /bounty and its subcommands. Any player: none (the bounties where they stand, then their own bounties going), list
        /// / all, abandon (remove, drop), confirm, cancel, data, sync. Admin: a player's name (that player's bounties), abandon
        /// # name. Developer: make, droptable. Anything else gets the usage line for the caller's access level.
        /// </summary>
        /// <param name="session">The calling player's session.</param>
        /// <param name="parameters">The words after /bounty.</param>
        [CommandHandler("bounty", AccessLevel.Player, CommandHandlerFlag.RequiresWorld,
            "Shows the bounties where you stand and the bounties you have going (your Bounty Journal shows them too; to start a bounty, use it on the journal). abandon <#> asks for /bounty confirm <code> (cancel drops the request); data / sync feed the Bounty Tracker.",
            Usage)]
        public static void HandleBounty(Session session, params string[] parameters)
        {
            var player = session?.Player;
            if (player == null)
                return;

            if (parameters == null || parameters.Length == 0)
            {
                BountyManager.ShowBounty(player);
                ItemBounties.ShowSlots(player, player);
                return;
            }

            var isAdmin = session.AccessLevel >= AccessLevel.Admin;
            // @ci-level tools (make a bounty, read the drop table) are Developer, like @ci; acting on ANOTHER player is Admin
            var isDev = session.AccessLevel >= AccessLevel.Developer;

            switch (parameters[0].ToLowerInvariant())
            {
                case "list":
                case "all":
                    BountyManager.ShowAllBounties(player);
                    return;

                case "abandon":
                case "remove":
                case "drop":
                    HandleRemove(session, parameters.Skip(1).ToArray(), isAdmin);
                    return;

                case "confirm":
                    ItemBounties.Confirm(player, parameters.Length > 1 ? parameters[1] : null);
                    return;

                case "cancel":
                    ItemBounties.Cancel(player);
                    return;

                case "data":
                    // machine-readable slot lines in chat (the Bounty Tracker's fallback)
                    ItemBounties.SendData(player);
                    return;

                case "sync":
                    // no chat output: re-sends the character's ItemBountyDisplay property for the Bounty Tracker window
                    ItemBounties.Sync(player);
                    return;

                case "droptable":
                    // developer: which bounties drop from monsters, and each one's share per tier and region
                    if (!isDev) break;
                    ItemBounties.ShowDropTable(player);
                    return;

                case "make":
                    // developer: /bounty make <wcid> [tier] - a bounty into your pack, its kill count rolled (a mystery's when
                    // started) and, for a bounty with no tier of its own, its tier set
                    if (!isDev) break;
                    if (parameters.Length < 2 || !uint.TryParse(parameters[1], out var makeWcid))
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, "Usage: /bounty make <wcid> [tier 11-25]  (tier: for a bounty that takes the kill's tier; default where you stand, else 11; a region bounty ignores it)");
                        return;
                    }
                    int? makeTier = parameters.Length > 2 && int.TryParse(parameters[2], out var t) ? t : null;
                    ItemBounties.Make(player, makeWcid, makeTier);
                    return;
            }

            // Admin: /bounty <player name> shows that player's bounties.
            if (isAdmin)
            {
                var other = PlayerManager.GetOnlinePlayer(string.Join(" ", parameters));
                if (other != null)
                {
                    ItemBounties.ShowSlots(player, other);
                    return;
                }
            }

            // a typo says so, not a silent /bounty; staff also see the forms their access adds
            CommandHandlerHelper.WriteOutputInfo(session, "Usage: " + Usage + (isAdmin ? AdminUsage : "") + (isDev ? DeveloperUsage : ""));
        }

        /// <summary>
        /// /bounty abandon 2 (also remove / drop; the slot can be typed 2, #2 or "slot 2", and the names from before the
        /// 2026-11-12 rework still work: r1, ring 1, b2...). An admin may add an online player's name after the slot. Nothing
        /// is abandoned here: ItemBounties asks for /bounty confirm with a code.
        /// </summary>
        /// <param name="session">The caller's session.</param>
        /// <param name="args">The words after "abandon": the slot, then (Admin only) a player's name.</param>
        /// <param name="isAdmin">True when the caller may act on another player's bounties.</param>
        private static void HandleRemove(Session session, string[] args, bool isAdmin)
        {
            var player = session.Player;

            // The slot is one word (2, #2, r1) or two (slot 2, ring 1); whatever follows is a player's name.
            string key = null;
            var used = 0;
            if (args.Length >= 1 && (key = ItemBounties.ParseSlotKey(args[0])) != null) used = 1;
            else if (args.Length >= 2 && (key = ItemBounties.ParseSlotKey(args[0] + args[1])) != null) used = 2;

            if (key == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Which bounty? /bounty abandon <#>, e.g. /bounty abandon 2. Your bounties:");
                ItemBounties.ShowSlots(player, player);
                return;
            }

            var owner = player;
            if (args.Length > used)
            {
                if (!isAdmin)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, "Usage: /bounty abandon <#>, e.g. /bounty abandon 2 - only your own bounties.");
                    return;
                }
                var name = string.Join(" ", args.Skip(used));
                owner = PlayerManager.GetOnlinePlayer(name);
                if (owner == null)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"{name} isn't online.");
                    return;
                }
            }

            ItemBounties.RequestRemove(player, owner, key);
        }

        /// <summary>Every player's /bounty forms (the [CommandHandler] usage, and the reply to a typo).</summary>
        private const string Usage = "/bounty [list|all]  |  /bounty abandon <#>  |  /bounty confirm <code>  |  /bounty cancel  |  /bounty data  |  /bounty sync  (start a bounty by using it on your Bounty Journal)";

        /// <summary>The forms an Admin adds, appended to an Admin's usage reply.</summary>
        private const string AdminUsage = "  |  Admin: /bounty <online player>  |  /bounty abandon <#> <online player>";

        /// <summary>The forms a Developer adds, appended to a Developer's usage reply.</summary>
        private const string DeveloperUsage = "  |  Developer: /bounty make <wcid> [tier 11-25]  |  /bounty droptable";
    }
}
