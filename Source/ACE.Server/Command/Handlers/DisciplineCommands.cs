using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Command.Handlers
{
    public static class DisciplineCommands
    {
        [CommandHandler("jail", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld,
            "Sends a player to jail.",
            "Usage: /jail [playername]\nIf no name is provided, the currently selected player is used.")]
        public static void HandleJail(Session session, params string[] parameters)
        {
            Player target = CommandHandlerHelper.GetPlayerAsCommandTarget(session, string.Join(" ", parameters));
            if (target == null) return;
            target.SendToJail();
            PlayerManager.BroadcastToAuditChannel(session.Player, $"[Jail] Player {target.Name} was sent to jail by {session.Player.Name}");
        }

        [CommandHandler("jailbreak", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld,
            "Releases a player from jail.",
            "Usage: /jailbreak [playername]\nIf no name is provided, the currently selected player is used.")]
        public static void HandleJailbreak(Session session, params string[] parameters)
        {
            Player target = CommandHandlerHelper.GetPlayerAsCommandTarget(session, string.Join(" ", parameters));
            if (target == null) return;

            if (!target.IsInJail())
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Player {target.Name} is not currently in jail.", ChatMessageType.System));
                return;
            }

            target.ReleaseFromJail();
            PlayerManager.BroadcastToAuditChannel(session.Player, $"[Jail] Player {target.Name} was released from jail by {session.Player.Name}");
        }

        [CommandHandler("ucmcheck", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, "Initiates a UCM check on the selected player.")]
        public static void HandleUCMCheck(Session session, params string[] parameters)
        {
            Player target = CommandHandlerHelper.GetPlayerAsCommandTarget(session, string.Join(" ", parameters));
            if (target == null) return;

            if (target.UCMChecker.IsCheckInProgress())
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"{target.Name} is already undergoing a UCM check.", ChatMessageType.System));
                return;
            }

            bool started = target.UCMChecker.Start();
            if (started)
            {
                PlayerManager.BroadcastToAuditChannel(session.Player, $"Admin {session.Player.Name} initiated a UCM check on {target.Name}.");
            }
            else
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"UCM check on {target.Name} failed to start.", ChatMessageType.System));
            }
        }
    }
}
