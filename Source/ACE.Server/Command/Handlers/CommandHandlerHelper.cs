
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using log4net;

namespace ACE.Server.Command.Handlers
{
    internal static class CommandHandlerHelper
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// This will determine where a command handler should output to, the console or a client session.<para />
        /// If the session is null, the output will be sent to the console. If the session is not null, and the session.Player is in the world, it will be sent to the session.<para />
        /// Messages sent to the console will be sent using log.Info()
        /// </summary>
        public static void WriteOutputInfo(Session session, string output, ChatMessageType chatMessageType = ChatMessageType.Broadcast)
        {
            if (session != null)
            {
                if (session.State == Network.Enum.SessionState.WorldConnected && session.Player != null)
                    ChatPacket.SendServerMessage(session, output, chatMessageType);
            }
            else
                log.Info(output);
        }

        /// <summary>
        /// This will determine where a command handler should output to, the console or a client session.<para />
        /// If the session is null, the output will be sent to the console. If the session is not null, and the session.Player is in the world, it will be sent to the session.<para />
        /// Messages sent to the console will be sent using log.Debug()
        /// </summary>
        public static void WriteOutputDebug(Session session, string output, ChatMessageType chatMessageType = ChatMessageType.Broadcast)
        {
            if (session != null)
            {
                if (session.State == Network.Enum.SessionState.WorldConnected && session.Player != null)
                    ChatPacket.SendServerMessage(session, output, chatMessageType);
            }
            else
                log.Debug(output);
        }

        /// <summary>
        /// This will determine where a command handler should output to, the console or a client session.<para />
        /// If the session is null, the output will be sent to the console. If the session is not null, and the session.Player is in the world, it will be sent to the session.<para />
        /// Messages sent to the console will be sent using log.Debug()
        /// </summary>
        public static void WriteOutputError(Session session, string output, ChatMessageType chatMessageType = ChatMessageType.Broadcast)
        {
            if (session != null)
            {
                if (session.State == Network.Enum.SessionState.WorldConnected && session.Player != null)
                    ChatPacket.SendServerMessage(session, output, chatMessageType);
            }
            else
                log.Error(output);
        }

        /// <summary>
        /// Returns the last appraised WorldObject
        /// </summary>
        public static WorldObject GetLastAppraisedObject(Session session)
        {
            var targetID = session.Player.RequestedAppraisalTarget;
            if (targetID == null)
            {
                WriteOutputInfo(session, "GetLastAppraisedObject() - no appraisal target");
                return null;
            }

            var target = session.Player.FindObject(targetID.Value, Player.SearchLocations.Everywhere, out _, out _, out _);
            if (target == null)
            {
                WriteOutputInfo(session, $"GetLastAppraisedObject() - couldn't find {targetID:X8}");
                return null;
            }
            return target;
        }

        /// <summary>
        /// Returns the currently selected WorldObject, checking health/mana query targets
        /// before falling back to appraisal targets.
        /// </summary>
        public static WorldObject GetSelected(Session session)
        {
            uint? targetID;
            if (session.Player.HealthQueryTarget.HasValue)
                targetID = session.Player.HealthQueryTarget;
            else if (session.Player.ManaQueryTarget.HasValue)
                targetID = session.Player.ManaQueryTarget;
            else if (session.Player.CurrentAppraisalTarget.HasValue)
                targetID = session.Player.CurrentAppraisalTarget;
            else 
                targetID = session.Player.RequestedAppraisalTarget;

            if (targetID == null) return null;
            return session.Player.FindObject(targetID.Value, Player.SearchLocations.Everywhere, out _, out _, out _);
        }

        /// <summary>
        /// Gets the player to be used as the command target based on the current selection or a specified player name.
        ///
        /// If no player is found, a system chat message is sent to the session indicating the reason. The caller need not send any additional messages.
        ///
        /// Intended use:
        /// ```
        ///     Player target = CommandHandlerHelper.GetPlayerAsCommandTarget(session, string.Join(" ", parameters));
        ///     if (target == null) return;
        ///     ...
        /// ```
        /// </summary>
        /// <param name="session">The session from which to determine the selected player and to send system chat messages if needed.</param>
        /// <param name="playerName">The name of the player to target. If empty, the currently selected player in the session is used.</param>
        /// <param name="fallbackToSelf">A boolean representing whether the command should fall back to the session player if no valid target is found.</param>
        /// <returns>The player to be used as the command target, or null if no suitable player is found.</returns>
        public static Player GetPlayerAsCommandTarget(Session session, string playerName = "", bool fallbackToSelf = false)
        {
            Player target = null;
            if (playerName.Length == 0)
            {
                WorldObject wo = GetSelected(session);
                if (wo is Player p) target = p;
            }
            else
            {
                target = PlayerManager.GetOnlinePlayer(playerName);
            }

            if (target == null)
            {
                if (fallbackToSelf)
                {
                    return session.Player;
                }
                if (playerName.Length == 0)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"You must select a player or pass one to the command.", ChatMessageType.System));
                    return null;
                }
                else
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Player {playerName} was not found.", ChatMessageType.System));
                    return null;
                }
            }
            return target;
        }

        public static WorldObject GetWorldObjectByGuid(Session session, uint guid)
        {
            var target = session.Player.FindObject(guid, Player.SearchLocations.Everywhere, out _, out _, out _);
            if (target == null)
            {
                WriteOutputInfo(session, $"GetWorldObjectByGuid() - couldn't find {guid:X8}");
                return null;
            }
            return target;
        }
    }
}
