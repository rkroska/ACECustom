using ACE.Entity.Enum;
using ACE.Server.Command;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Command.Handlers
{
    public static class ILTCommands
    {
        [CommandHandler("manabarrier", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 0,
            "Toggle Mana Barrier ability on/off for the current player.",
            "Usage: /manabarrier [on|off]")]
        public static void HandleManaBarrier(Session session, params string[] parameters)
        {
            var player = session.Player;
            if (player == null) return;

            bool enable = parameters.Length == 0 || parameters[0].ToLower() != "off";
            player.HasManaBarrier = enable;

            session.Network.EnqueueSend(new GameMessageSystemChat(
                $"Mana Barrier {(enable ? "enabled" : "disabled")}.", ChatMessageType.System));
        }

        [CommandHandler("ilt", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Displays ILT custom server commands and features.",
            "Usage: /ilt [help|features]")]
        public static void HandleILT(Session session, params string[] parameters)
        {
            var sub = parameters.Length > 0 ? parameters[0].ToLower() : "help";

            if (sub == "features")
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("=== ILT Custom Features ===", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat("  Coming Soon", ChatMessageType.System));
            }
            else
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("=== ILT Custom Commands ===", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat("  /ilt features           View a list of custom ILT server features.", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat("  /damagenums             Toggle between full and short (K/M/B/T/Q) damage numbers.", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat("  /damagenums short       Enable short damage number format (K/M/B/T/Q).", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat("  /damagenums default     Restore full damage numbers.", ChatMessageType.System));
            }
        }

        [CommandHandler("damagenums", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Toggle between full and truncated (K/M) damage numbers in combat chat.",
            "Usage: /damagenums [short|default]")]
        public static void HandleDamageNums(Session session, params string[] parameters)
        {
            var player = session.Player;
            if (player == null) return;

            bool truncated;
            if (parameters.Length == 0)
                truncated = !player.UseTruncatedDamageNumbers;  // toggle
            else
                truncated = parameters[0].ToLower() == "short";

            player.UseTruncatedDamageNumbers = truncated;

            session.Network.EnqueueSend(new GameMessageSystemChat(
                $"Damage numbers set to {(truncated ? "short (K/M/B/T/Q)" : "default (full)")}.", ChatMessageType.System));
        }
    }
}
