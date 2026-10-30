using System;
using System.Globalization;
using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.Command.Handlers
{
    public static class NetherFractureCommands
    {
        [CommandHandler("netherfracture", AccessLevel.Developer, CommandHandlerFlag.None, 0,
            "View or adjust the current named ZC's native void damage profile.",
            "Usage: /netherfracture show | set corrosion|corruption|curse|streak|fracture|radius <number|off|on>. Damage: 1 = native, 0 = off. Radius: 2-8m.")]
        public static void Handle(Session session, params string[] args)
        {
            var player = session?.Player;
            if (player == null) return;
            string response;
            try
            {
                if (args.Length == 3 && args[0].Equals("set", StringComparison.OrdinalIgnoreCase))
                {
                    var raw = args[2].ToLowerInvariant();
                    var value = raw == "off" ? 0 : raw == "on" ? 1 : double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
                    NetherFractureManager.SetZone(player, args[1], value);
                }
                else if (args.Length > 0 && !(args.Length == 1 && args[0].Equals("show", StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException("Usage: /netherfracture show | set <source> <number|off|on>.");
                response = NetherFractureManager.DeveloperStatus(player);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is OverflowException || ex is System.IO.IOException || ex is System.IO.InvalidDataException || ex is UnauthorizedAccessException)
            { response = "Nether Fracture settings unchanged: " + ex.Message; }
            session.Network.EnqueueSend(new GameMessageSystemChat(response, ChatMessageType.Broadcast));
        }
    }
}
