using ACE.Entity.Enum;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>/triune - kept as a quiet alias of /aug since the Triune retire (owner 2026-11-22).</summary>
    public static class TriuneCommands
    {
        [CommandHandler("triune", AccessLevel.Player, CommandHandlerFlag.RequiresWorld,
            "Shows your augmentations and this week's hunting rewards (same as /aug).", "/triune")]
        public static void HandleTriune(Session session, params string[] parameters) => PlayerCommands.HandleAugmentReport(session, parameters);
    }
}
