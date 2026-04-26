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
    }
}
