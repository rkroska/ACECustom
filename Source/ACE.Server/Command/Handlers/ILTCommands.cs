using ACE.Entity.Enum;
using ACE.Server.Command;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Command.Handlers
{
    public static class ILTCommands
    {
        [CommandHandler("ilt", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "ILT custom server commands and player preferences.",
            "Usage: /ilt [help|features|dmgformat|showoverkill]")]
        public static void HandleILT(Session session, params string[] parameters)
        {
            var player = session.Player;
            if (player == null) return;

            var sub = parameters.Length > 0 ? parameters[0].ToLower() : "help";

            if (sub == "features")
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("=== ILT Custom Features ===", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat("  Coming Soon", ChatMessageType.System));
            }
            else if (sub == "dmgformat")
            {
                // Cycle or set damage number format
                int mode;
                if (parameters.Length >= 2)
                    mode = parameters[1].ToLower() switch
                    {
                        "commas"  => 1,
                        "short"   => 2,
                        _         => 0  // "default" or anything else
                    };
                else
                    mode = (player.DamageNumberFormat + 1) % 3;  // cycle 0 → 1 → 2 → 0

                player.DamageNumberFormat = mode;
                var label = mode switch { 1 => "commas (1,247)", 2 => "short (K/M/B/T/Q)", _ => "default (vanilla)" };
                session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"Damage format set to {label}.", ChatMessageType.System));
            }
            else if (sub == "showoverkill")
            {
                // Toggle or set ShowOverkill (controls both kill and death overkill suffixes)
                bool newValue;
                if (parameters.Length >= 2 && parameters[1].ToLower() == "on")
                    newValue = true;
                else if (parameters.Length >= 2 && parameters[1].ToLower() == "off")
                    newValue = false;
                else
                    newValue = !player.ShowOverkill; // toggle

                player.ShowOverkill = newValue;
                var state = newValue ? "ON" : "OFF";
                session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"Show Overkill: {state}. The [Overkill] suffix will {(newValue ? "now" : "no longer")} appear on kill and death messages.",
                    ChatMessageType.System));
            }
            else
            {
                var dmgLabel  = player.DamageNumberFormat switch { 1 => "commas", 2 => "short", _ => "default" };
                var okLabel   = player.ShowOverkill ? "ON" : "OFF";
                session.Network.EnqueueSend(new GameMessageSystemChat("=== ILT Custom Commands ===", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat("  /ilt features                      View a list of custom ILT server features.", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"  /ilt dmgformat [default|commas|short]  Set damage number style. (currently: {dmgLabel})", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"  /ilt showoverkill [on|off]          Toggle [Overkill] on kill/death messages. (currently: {okLabel})", ChatMessageType.System));
            }
        }
    }
}
