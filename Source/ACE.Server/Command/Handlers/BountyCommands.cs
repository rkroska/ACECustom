using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>/bounty (owner 2026-09-23): any player's view of the Bounty bounties where they stand. See BountyManager.</summary>
    public static class BountyCommands
    {
        [CommandHandler("bounty", AccessLevel.Player, CommandHandlerFlag.RequiresWorld,
            "Shows the bounties where you stand: kills so far, or how long until the next one unlocks.")]
        public static void HandleBounty(Session session, params string[] parameters)
        {
            BountyManager.ShowBounty(session?.Player);
        }
    }
}
