using System;

using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    public static class DressingRoomCommands
    {
        // All output goes through DressingRoom, which keeps every line plain ASCII for the game client.
        [CommandHandler("look", AccessLevel.Player, CommandHandlerFlag.RequiresWorld,
            "Shows or manages your Dressing Room look.",
            "(no argument) lists your saved look\n"
            + "hide - show your real gear; the look stays saved\n"
            + "show - show your saved look again\n"
            + "clear - remove your saved look for good (nothing is returned)")]
        public static void HandleLook(Session session, params string[] parameters)
        {
            var player = session?.Player;
            if (player == null)
                return;

            var action = parameters.Length > 0 ? parameters[0] : "";

            if (action.Length == 0)
                DressingRoom.ShowStatus(player);
            else if (action.Equals("hide", StringComparison.OrdinalIgnoreCase))
                DressingRoom.SetHidden(player, true);
            else if (action.Equals("show", StringComparison.OrdinalIgnoreCase))
                DressingRoom.SetHidden(player, false);
            else if (action.Equals("clear", StringComparison.OrdinalIgnoreCase))
                DressingRoom.OfferClear(player);
            else
                CommandHandlerHelper.WriteOutputInfo(session, "Usage: /look, /look hide, /look show, /look clear", ChatMessageType.Broadcast);
        }
    }
}
