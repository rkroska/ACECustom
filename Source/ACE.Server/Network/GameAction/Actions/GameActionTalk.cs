using System;

using log4net;

using ACE.Common.Extensions;
using ACE.Entity.Enum;
using ACE.Server.Command;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Server.Network.GameEvent.Events;

namespace ACE.Server.Network.GameAction.Actions
{
    public static class GameActionTalk
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        [GameAction(GameActionType.Talk)]
        public static void Handle(ClientMessage clientMessage, Session session)
        {
            var message = clientMessage.Payload.ReadString16L();
            
            // DEBUG LOGGING
            session.Network.EnqueueSend(new GameMessageSystemChat($"[DEBUG GameActionTalk] Msg: '{(message ?? "null")}'", ChatMessageType.Broadcast));

            // Intercept say with content to turn off sticky
            if (message.StartsWith("/s ", StringComparison.OrdinalIgnoreCase) ||
                message.StartsWith("/say ", StringComparison.OrdinalIgnoreCase) ||
                message.StartsWith("/local ", StringComparison.OrdinalIgnoreCase))
            {
                if (session.Player.StickyChatMode != StickyChatType.None)
                {
                    session.Player.StickyChatMode = StickyChatType.None;
                    session.Network.EnqueueSend(new GameMessageSystemChat("Sticky Chat: Disabled. Normal chat will go to local speech.", ChatMessageType.Broadcast));
                }
                var prefixLen = message.StartsWith("/say ", StringComparison.OrdinalIgnoreCase) ? 5 : (message.StartsWith("/local ", StringComparison.OrdinalIgnoreCase) ? 7 : 3);
                session.Player.HandleActionTalk(message.Substring(prefixLen));
                return;
            }

            // Intercept channels with content to trigger sticky
            if (message.StartsWith("/a ", StringComparison.OrdinalIgnoreCase) ||
                message.StartsWith("/v ", StringComparison.OrdinalIgnoreCase) ||
                message.StartsWith("/vassals ", StringComparison.OrdinalIgnoreCase))
            {
                if (session.Player.StickyChatEnabled)
                {
                    if (session.Player.StickyChatMode != StickyChatType.Allegiance)
                    {
                        if (session.Player.Allegiance == null)
                        {
                            session.Network.EnqueueSend(new GameEventWeenieError(session, WeenieError.YouAreNotInAllegiance));
                            return;
                        }
                        session.Player.StickyChatMode = StickyChatType.Allegiance;
                        session.Network.EnqueueSend(new GameMessageSystemChat("Sticky Chat: [Allegiance] is now active. Normal chat will go to Allegiance. Type /say to disable.", ChatMessageType.Broadcast));
                    }
                }
                var prefixLen = message.StartsWith("/vassals ", StringComparison.OrdinalIgnoreCase) ? 9 : 3;
                session.Player.BroadcastToAllegiance(message.Substring(prefixLen));
                return;
            }

            if (message.StartsWith("/f ", StringComparison.OrdinalIgnoreCase) ||
                message.StartsWith("/fellow ", StringComparison.OrdinalIgnoreCase))
            {
                if (session.Player.StickyChatEnabled)
                {
                    if (session.Player.StickyChatMode != StickyChatType.Fellowship)
                    {
                        if (session.Player.Fellowship == null)
                        {
                            session.Network.EnqueueSend(new GameEventWeenieError(session, WeenieError.YouDoNotBelongToAFellowship));
                            return;
                        }
                        session.Player.StickyChatMode = StickyChatType.Fellowship;
                        session.Network.EnqueueSend(new GameMessageSystemChat("Sticky Chat: [Fellowship] is now active. Normal chat will go to Fellowship. Type /say to disable.", ChatMessageType.Broadcast));
                    }
                }
                var prefixLen = message.StartsWith("/fellow ", StringComparison.OrdinalIgnoreCase) ? 8 : 3;
                session.Player.BroadcastToFellowship(message.Substring(prefixLen));
                return;
            }

            var trimmedMsg = message?.Trim();
            if (string.Equals(trimmedMsg, "/a", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmedMsg, "/v", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmedMsg, "/vassals", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmedMsg, "@a", StringComparison.OrdinalIgnoreCase))
            {
                if (session.Player.StickyChatMode == StickyChatType.Allegiance)
                {
                    session.Player.StickyChatMode = StickyChatType.None;
                    session.Network.EnqueueSend(new GameMessageSystemChat("Sticky Chat: Disabled. Normal chat will go to local speech.", ChatMessageType.Broadcast));
                }
                else
                {
                    if (session.Player.Allegiance == null)
                    {
                        session.Network.EnqueueSend(new GameEventWeenieError(session, WeenieError.YouAreNotInAllegiance));
                        return;
                    }
                    session.Player.StickyChatMode = StickyChatType.Allegiance;
                    session.Network.EnqueueSend(new GameMessageSystemChat("Sticky Chat: [Allegiance] is now active. Normal chat will go to Allegiance. Type /say to disable.", ChatMessageType.Broadcast));
                }
                return;
            }
            else if (string.Equals(trimmedMsg, "/f", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(trimmedMsg, "/fellow", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(trimmedMsg, "@f", StringComparison.OrdinalIgnoreCase))
            {
                if (session.Player.StickyChatMode == StickyChatType.Fellowship)
                {
                    session.Player.StickyChatMode = StickyChatType.None;
                    session.Network.EnqueueSend(new GameMessageSystemChat("Sticky Chat: Disabled. Normal chat will go to local speech.", ChatMessageType.Broadcast));
                }
                else
                {
                    if (session.Player.Fellowship == null)
                    {
                        session.Network.EnqueueSend(new GameEventWeenieError(session, WeenieError.YouDoNotBelongToAFellowship));
                        return;
                    }
                    session.Player.StickyChatMode = StickyChatType.Fellowship;
                    session.Network.EnqueueSend(new GameMessageSystemChat("Sticky Chat: [Fellowship] is now active. Normal chat will go to Fellowship. Type /say to disable.", ChatMessageType.Broadcast));
                }
                return;
            }

            if (message.StartsWith("@"))
            {
                string commandRaw = message.Remove(0, 1);
                CommandHandlerResponse response = CommandHandlerResponse.InvalidCommand;
                CommandHandlerInfo commandHandler = null;
                string command = null;
                string[] parameters = null;

                try
                {
                    CommandManager.ParseCommand(message.Remove(0, 1), out command, out parameters);
                }
                catch (Exception ex)
                {
                    log.Error($"Exception while parsing command: {commandRaw}", ex);
                    return;
                }

                try
                {
                    response = CommandManager.GetCommandHandler(session, command, parameters, out commandHandler);
                }
                catch (Exception ex)
                {
                    log.Error($"Exception while getting command handler for: {commandRaw}", ex);
                }

                if (response == CommandHandlerResponse.Ok)
                {
                    try
                    {
                        if (commandHandler.Attribute.IncludeRaw)
                        {
                            parameters = CommandManager.StuffRawIntoParameters(message.Remove(0, 1), command, parameters);
                        }
                        ((CommandHandler)commandHandler.Handler).Invoke(session, parameters);
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Exception while invoking command handler for: {commandRaw}", ex);
                    }
                }
                else if (response == CommandHandlerResponse.SudoOk)
                {
                    string[] sudoParameters = new string[parameters.Length - 1];
                    for (int i = 1; i < parameters.Length; i++)
                        sudoParameters[i - 1] = parameters[i];
                    try
                    {
                        if (commandHandler.Attribute.IncludeRaw)
                        {
                            parameters = CommandManager.StuffRawIntoParameters(message.Remove(0, 1), command, parameters);
                        }
                        ((CommandHandler)commandHandler.Handler).Invoke(session, sudoParameters);
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Exception while invoking command handler for: {commandRaw}", ex);
                    }
                }
                else
                {
                    switch (response)
                    {
                        case CommandHandlerResponse.InvalidCommand:
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Unknown command: {command}", ChatMessageType.Help));
                            break;
                        case CommandHandlerResponse.InvalidParameterCount:
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid parameter count, got {parameters.Length}, expected {commandHandler.Attribute.ParameterCount}!", ChatMessageType.Help));
                            session.Network.EnqueueSend(new GameMessageSystemChat($"@{commandHandler.Attribute.Command} - {commandHandler.Attribute.Description}", ChatMessageType.Broadcast));
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Usage: @{commandHandler.Attribute.Command} {commandHandler.Attribute.Usage}", ChatMessageType.Broadcast));
                            break;
                        default:
                            break;
                    }
                }
            }
            else
            {
                if (message.Equals("/say", StringComparison.OrdinalIgnoreCase) || 
                    message.Equals("/s", StringComparison.OrdinalIgnoreCase) ||
                    message.Equals("/local", StringComparison.OrdinalIgnoreCase) ||
                    message.Equals("@sticky off", StringComparison.OrdinalIgnoreCase))
                {
                    if (session.Player.StickyChatMode != StickyChatType.None)
                    {
                        session.Player.StickyChatMode = StickyChatType.None;
                        session.Network.EnqueueSend(new GameMessageSystemChat("Sticky Chat: Disabled. Normal chat will go to local speech.", ChatMessageType.Broadcast));
                        return;
                    }
                }

                session.Player.HandleActionTalk(message);
            }
        }
    }
}
