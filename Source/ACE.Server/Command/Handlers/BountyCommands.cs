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
    /// Item bounties (owner 2026-11-10, ItemBounties): /bounty also lists the character's bounty slots (R1, R2, B1, B2);
    /// /bounty remove asks for a console confirmation (/bounty confirm code, or /bounty cancel to drop the request), and
    /// /bounty confirm also confirms applying a bounty outside the place it counts in. /bounty data and /bounty sync feed
    /// the Bounty Tracker window. Admins can view and remove for another online player: /bounty Name, /bounty remove r1
    /// Name. Developers: /bounty make wcid [tier], /bounty droptable.
    /// </summary>
    public static class BountyCommands
    {
        /// <summary>
        /// /bounty and its subcommands. Any player: none (the bounties where they stand, then their item bounty slots),
        /// list / all, remove, confirm, cancel, data, sync. Admin: a player's name (that player's slots), remove slot name.
        /// Developer: make, droptable. Anything else gets the usage line for the caller's access level.
        /// </summary>
        /// <param name="session">The calling player's session.</param>
        /// <param name="parameters">The words after /bounty.</param>
        [CommandHandler("bounty", AccessLevel.Player, CommandHandlerFlag.RequiresWorld,
            "Shows the bounties where you stand and your item bounty slots. remove <slot> asks for /bounty confirm <code> (cancel drops the request); data / sync feed the Bounty Tracker.",
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

                case "remove":
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
                    // applied) and, for a bounty with no tier of its own, its tier set
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

            // Admin: /bounty <player name> shows that player's slots.
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
        /// /bounty remove r1 (the slot can also be typed "ring 1", "ring1", "bracelet 2"...). An admin may add an online
        /// player's name after the slot. Nothing is removed here: ItemBounties asks for /bounty confirm with a code.
        /// </summary>
        /// <param name="session">The caller's session.</param>
        /// <param name="args">The words after "remove": the slot, then (Admin only) a player's name.</param>
        /// <param name="isAdmin">True when the caller may act on another player's slots.</param>
        private static void HandleRemove(Session session, string[] args, bool isAdmin)
        {
            var player = session.Player;

            // The slot is one word (r1, ring1) or two (ring 1); whatever follows is a player's name.
            string key = null;
            var used = 0;
            if (args.Length >= 1 && (key = ItemBounties.ParseSlotKey(args[0])) != null) used = 1;
            else if (args.Length >= 2 && (key = ItemBounties.ParseSlotKey(args[0] + args[1])) != null) used = 2;

            if (key == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Which slot? /bounty remove r1 (or r2, b1, b2). Your slots:");
                ItemBounties.ShowSlots(player, player);
                return;
            }

            var owner = player;
            if (args.Length > used)
            {
                if (!isAdmin)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, "Usage: /bounty remove r1 (or r2, b1, b2) - only your own slots.");
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
        private const string Usage = "/bounty [list|all]  |  /bounty remove r1 (r2, b1, b2)  |  /bounty confirm <code>  |  /bounty cancel  |  /bounty data  |  /bounty sync";

        /// <summary>The forms an Admin adds, appended to an Admin's usage reply.</summary>
        private const string AdminUsage = "  |  Admin: /bounty <online player>  |  /bounty remove r1 <online player>";

        /// <summary>The forms a Developer adds, appended to a Developer's usage reply.</summary>
        private const string DeveloperUsage = "  |  Developer: /bounty make <wcid> [tier 11-25]  |  /bounty droptable";
    }
}
