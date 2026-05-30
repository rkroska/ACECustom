using System;
using System.Globalization;

using ACE.Entity.Enum;
using ACE.Server.Command;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Server.Managers;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Developer command handler for smart ring settings.
    /// All commands require AccessLevel.Developer.
    /// </summary>
    public static class RingCommands
    {
        [CommandHandler("smartring", AccessLevel.Developer, CommandHandlerFlag.None, 0,
            "Dev: view or tune global smart ring spell defaults.",
            "Usage:\n" +
            "  /smartring                          — view current global settings\n" +
            "  /smartring radius <value>           — adjust default radius\n" +
            "  /smartring height <value>           — adjust default height\n" +
            "  /smartring double <value>           — adjust double proc chance (0.0 to 1.0)\n" +
            "  /smartring triple <value>           — adjust triple proc chance (0.0 to 1.0)\n")]
        public static void HandleSmartRing(Session session, params string[] parameters)
        {
            if (parameters.Length == 0)
            {
                var dump = SmartRingSettingsManager.Dump();
                var help = SmartRingSettingsManager.Help();
                Reply(session, "\u200B\n" + dump + "\n" + help);
                return;
            }

            var key = parameters[0].ToLower();

            if (parameters.Length >= 2)
            {
                var value = parameters[1];
                var (success, found, message) = SmartRingSettingsManager.TrySet(key, value);

                if (!found)
                {
                    Reply(session, $"Unknown key '{key}' for /smartring. Valid keys: radius, height, double, triple.");
                    return;
                }

                if (!success)
                {
                    Reply(session, $"[Smart Ring Error] {message}");
                    return;
                }

                Broadcast(session, $"[Smart Ring] {message}");
                return;
            }

            Reply(session, "Usage:\n" +
                           "  /smartring                          — view current global settings\n" +
                           "  /smartring radius <value>           — adjust default radius\n" +
                           "  /smartring height <value>           — adjust default height\n" +
                           "  /smartring double <value>           — adjust double proc chance (0.0 to 1.0)\n" +
                           "  /smartring triple <value>           — adjust triple proc chance (0.0 to 1.0)");
        }

        private static void Reply(Session session, string msg)
        {
            if (session == null) { Console.WriteLine(msg); return; }
            session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        }

        private static void Broadcast(Session session, string msg)
        {
            PlayerManager.BroadcastToAuditChannel(session?.Player, msg);

            if (session != null)
                session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        }
    }
}
