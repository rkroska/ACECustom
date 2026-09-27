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
            "/bounty [list]  (list: every bounty on the server, and your progress on each)")]
        public static void HandleBounty(Session session, params string[] parameters)
        {
            if (parameters?.Length > 0 && (parameters[0].Equals("list", System.StringComparison.OrdinalIgnoreCase)
                || parameters[0].Equals("all", System.StringComparison.OrdinalIgnoreCase)))
                BountyManager.ShowAllBounties(session?.Player);
            else
                BountyManager.ShowBounty(session?.Player);
        }
    }
}
