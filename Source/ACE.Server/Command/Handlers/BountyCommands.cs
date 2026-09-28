using System;

using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// /bounty (owner 2026-09-23): any player's view of the bounties where they stand. /bounty list (owner 2026-09-27): every
    /// bounty on the server and the player's progress on each, so they can choose where to go. See BountyManager.
    /// </summary>
    public static class BountyCommands
    {
        [CommandHandler("bounty", AccessLevel.Player, CommandHandlerFlag.RequiresWorld,
            "Shows the bounties where you stand: kills so far, or how long until the next one unlocks.",
            Usage)]
        public static void HandleBounty(Session session, params string[] parameters)
        {
            if (parameters == null || parameters.Length == 0)
                BountyManager.ShowBounty(session?.Player);
            else if (parameters[0].Equals("list", StringComparison.OrdinalIgnoreCase) || parameters[0].Equals("all", StringComparison.OrdinalIgnoreCase))
                BountyManager.ShowAllBounties(session?.Player);
            else
                CommandHandlerHelper.WriteOutputInfo(session, "Usage: " + Usage);   // a typo says so, not a silent /bounty
        }

        private const string Usage = "/bounty [list|all]  (list or all: every bounty on the server, and your progress on each)";
    }
}
