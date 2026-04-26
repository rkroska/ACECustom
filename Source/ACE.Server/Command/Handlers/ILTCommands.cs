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
                session.Network.EnqueueSend(new GameMessageSystemChat("  /damagenumbers             Cycle through damage number modes (default → commas → short).", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat("  /damagenumbers default     Vanilla damage numbers (no formatting).", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat("  /damagenumbers commas      Enable comma-separated numbers (1,247).", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat("  /damagenumbers short       Enable short format (K/M/B/T/Q).", ChatMessageType.System));
            }
        }

        [CommandHandler("damagenumbers", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Set combat damage number format. Cycles through modes if no argument given.",
            "Usage: /damagenumbers [default|commas|short]")]
        public static void HandleDamageNums(Session session, params string[] parameters)
        {
            var player = session.Player;
            if (player == null) return;

            int mode;
            if (parameters.Length == 0)
                mode = (player.DamageNumberFormat + 1) % 3;  // cycle 0 → 1 → 2 → 0
            else
                mode = parameters[0].ToLower() switch
                {
                    "commas"  => 1,
                    "short"   => 2,
                    _         => 0   // "default" or anything else
                };

            player.DamageNumberFormat = mode;

            var label = mode switch { 1 => "commas (1,247)", 2 => "short (K/M/B/T/Q)", _ => "default (vanilla)" };
            session.Network.EnqueueSend(new GameMessageSystemChat(
                $"Damage numbers set to {label}.", ChatMessageType.System));
        }
    }
}
