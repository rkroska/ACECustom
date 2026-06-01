using System;

using ACE.Common.Extensions;
using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.WorldObjects;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.Network.GameAction.Actions
{
    public static class GameActionChatChannel
    {
        [GameAction(GameActionType.ChatChannel)]
        public static void Handle(ClientMessage clientMessage, Session session)
        {
            var groupChatType = (Channel)clientMessage.Payload.ReadUInt32();
            var message = clientMessage.Payload.ReadString16L();

            // DEBUG LOGGING
            session.Network.EnqueueSend(new GameMessageSystemChat($"[DEBUG GameActionChatChannel] Channel: {groupChatType} (0x{(uint)groupChatType:X8}) | Msg: '{(message ?? "null")}' | Len: {message?.Length ?? 0}", ChatMessageType.Broadcast));

            if (message != null)
            {
                // Intercept say with content to turn off sticky
                if (message.StartsWith("/s ", StringComparison.OrdinalIgnoreCase) ||
                    message.StartsWith("/say ", StringComparison.OrdinalIgnoreCase) ||
                    message.StartsWith("/local ", StringComparison.OrdinalIgnoreCase) ||
                    message.StartsWith("@s ", StringComparison.OrdinalIgnoreCase) ||
                    message.StartsWith("@say ", StringComparison.OrdinalIgnoreCase))
                {
                    if (session.Player.StickyChatMode != StickyChatType.None)
                    {
                        session.Player.StickyChatMode = StickyChatType.None;
                        session.Network.EnqueueSend(new GameMessageSystemChat("Sticky Chat: Disabled. Normal chat will go to local speech.", ChatMessageType.Broadcast));
                    }
                    var prefixLen = 3;
                    if (message.StartsWith("/say ", StringComparison.OrdinalIgnoreCase) || message.StartsWith("@say ", StringComparison.OrdinalIgnoreCase))
                        prefixLen = 5;
                    else if (message.StartsWith("/local ", StringComparison.OrdinalIgnoreCase))
                        prefixLen = 7;
                    
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

                // Bypass other commands/slashes entirely to local talk
                if (message.StartsWith("/") || message.StartsWith("@"))
                {
                    session.Player.HandleActionTalk(message);
                    return;
                }
            }

            var trimmedMsg = message?.Trim();

            // Disable check on any channel message
            if (string.Equals(trimmedMsg, "/say", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmedMsg, "/s", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmedMsg, "/local", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmedMsg, "@sticky off", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmedMsg, "@say", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmedMsg, "@s", StringComparison.OrdinalIgnoreCase))
            {
                if (session.Player.StickyChatMode != StickyChatType.None)
                {
                    session.Player.StickyChatMode = StickyChatType.None;
                    session.Network.EnqueueSend(new GameMessageSystemChat("Sticky Chat: Disabled. Normal chat will go to local speech.", ChatMessageType.Broadcast));
                }
                return;
            }

            // Allegiance Sticky Toggle
            bool isAllegianceToggle = string.Equals(trimmedMsg, "/a", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(trimmedMsg, "/v", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(trimmedMsg, "/vassals", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(trimmedMsg, "@a", StringComparison.OrdinalIgnoreCase) ||
                                      (string.IsNullOrEmpty(trimmedMsg) && (groupChatType == Channel.Vassals || groupChatType == Channel.AllegianceBroadcast));

            if (isAllegianceToggle)
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

            // Fellowship Sticky Toggle
            bool isFellowshipToggle = string.Equals(trimmedMsg, "/f", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(trimmedMsg, "/fellow", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(trimmedMsg, "@f", StringComparison.OrdinalIgnoreCase) ||
                                      (string.IsNullOrEmpty(trimmedMsg) && groupChatType == Channel.Fellow);

            if (isFellowshipToggle)
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

            // Automatic Sticky Lock when stickychat is enabled and a message is typed
            if (session.Player.StickyChatEnabled && !string.IsNullOrEmpty(trimmedMsg))
            {
                if (groupChatType == Channel.Vassals || groupChatType == Channel.AllegianceBroadcast)
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
                else if (groupChatType == Channel.Fellow)
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
            }

            switch (groupChatType)
            {
                case Channel.Abuse:
                    {
                        // Should anyone be able to post to this channel? If so should they just a response back stating their message has been posted.
                        // Should messages here also be written to a log?
                        if (session.AccessLevel < AccessLevel.Advocate)
                        {
                            var statusMessage = new GameEventWeenieError(session, WeenieError.YouCantUseThatChannel);
                            session.Network.EnqueueSend(statusMessage);
                            break;
                        }

                        PlayerManager.BroadcastToChannel(groupChatType, session.Player, message, true);
                    }
                    break;
                case Channel.Admin:
                    {
                        if (!session.Player.IsAdmin)
                        {
                            var statusMessage = new GameEventWeenieError(session, WeenieError.YouCantUseThatChannel);
                            session.Network.EnqueueSend(statusMessage);
                            break;
                        }

                        PlayerManager.BroadcastToChannel(groupChatType, session.Player, message, true);
                    }
                    break;
                case Channel.Audit:
                    {
                        if (session.AccessLevel < AccessLevel.Sentinel)
                        {
                            var statusMessage = new GameEventWeenieError(session, WeenieError.YouCantUseThatChannel);
                            session.Network.EnqueueSend(statusMessage);
                            break;
                        }

                        PlayerManager.BroadcastToChannel(groupChatType, session.Player, message, true);
                    }
                    break;
                case Channel.Advocate1:
                case Channel.Advocate2:
                case Channel.Advocate3:
                    {
                        if (session.AccessLevel < AccessLevel.Advocate)
                        {
                            var statusMessage = new GameEventWeenieError(session, WeenieError.YouCantUseThatChannel);
                            session.Network.EnqueueSend(statusMessage);
                            break;
                        }

                        PlayerManager.BroadcastToChannel(groupChatType, session.Player, message, true);
                    }
                    break;
                case Channel.Sentinel:
                    {
                        if (session.AccessLevel < AccessLevel.Sentinel)
                        {
                            var statusMessage = new GameEventWeenieError(session, WeenieError.YouCantUseThatChannel);
                            session.Network.EnqueueSend(statusMessage);
                            break;
                        }

                        PlayerManager.BroadcastToChannel(groupChatType, session.Player, message, true);
                    }
                    break;
                case Channel.Help:
                    {
                        ChatPacket.SendServerMessage(session, "GameActionChatChannel TellHelp Needs work.", ChatMessageType.Broadcast);

                        PlayerManager.BroadcastToChannel(groupChatType, session.Player, message, true);
                    }
                    break;
                case Channel.Fellow:
                    {
                        if (session.Player.Fellowship == null)
                        {
                            var statusMessage = new GameEventWeenieError(session, WeenieError.YouDoNotBelongToAFellowship);
                            session.Network.EnqueueSend(statusMessage);
                            break;
                        }

                        var fellowshipMembers = session.Player.Fellowship.GetFellowshipMembers();

                        foreach (var fellowmember in fellowshipMembers.Values)
                            if (fellowmember.Session != session && !fellowmember.SquelchManager.Squelches.Contains(session.Player, ChatMessageType.Fellowship))
                                fellowmember.Session.Network.EnqueueSend(new GameEventChannelBroadcast(fellowmember.Session, groupChatType, session.Player.Name, message));
                            else
                                session.Network.EnqueueSend(new GameEventChannelBroadcast(session, groupChatType, "", message));
                    }
                    break;
                case Channel.Vassals:
                    {
                        if (!session.Player.HasAllegiance)
                        {
                            var statusMessage = new GameEventWeenieError(session, WeenieError.YouAreNotInAllegiance);
                            session.Network.EnqueueSend(statusMessage);
                            break;
                        }

                        if (session.Player.AllegianceNode.TotalVassals == 0)
                        {
                            var statusMessage = new GameEventWeenieError(session, WeenieError.YouCantUseThatChannel);
                            session.Network.EnqueueSend(statusMessage);
                            break;
                        }

                        foreach (var vassalGuid in session.Player.AllegianceNode.Vassals.Keys)
                        {
                            var vassalPlayer = PlayerManager.GetOnlinePlayer(vassalGuid);

                            if (vassalPlayer != null && !vassalPlayer.SquelchManager.Squelches.Contains(session.Player, ChatMessageType.Allegiance))
                                vassalPlayer.Session.Network.EnqueueSend(new GameEventChannelBroadcast(vassalPlayer.Session, groupChatType, session.Player.Name, message));
                        }
                        session.Network.EnqueueSend(new GameEventChannelBroadcast(session, groupChatType, "", message));
                    }
                    break;
                case Channel.Patron:
                    {
                        if (!session.Player.HasAllegiance)
                        {
                            var statusMessage = new GameEventWeenieError(session, WeenieError.YouAreNotInAllegiance);
                            session.Network.EnqueueSend(statusMessage);
                            break;
                        }

                        if (!session.Player.PatronId.HasValue)
                        {
                            var statusMessage = new GameEventWeenieError(session, WeenieError.YouCantUseThatChannel);
                            session.Network.EnqueueSend(statusMessage);
                            break;
                        }

                        var patronPlayer = PlayerManager.GetOnlinePlayer(session.Player.AllegianceNode.Patron.PlayerGuid);

                        if (patronPlayer != null && !patronPlayer.SquelchManager.Squelches.Contains(session.Player, ChatMessageType.Allegiance))
                            patronPlayer.Session.Network.EnqueueSend(new GameEventChannelBroadcast(patronPlayer.Session, groupChatType, session.Player.Name, message));

                        session.Network.EnqueueSend(new GameEventChannelBroadcast(session, groupChatType, "", message));
                    }
                    break;
                case Channel.Monarch:
                    {
                        if (!session.Player.HasAllegiance)
                        {
                            var statusMessage = new GameEventWeenieError(session, WeenieError.YouAreNotInAllegiance);
                            session.Network.EnqueueSend(statusMessage);
                            break;
                        }

                        if (!session.Player.MonarchId.HasValue)
                        {
                            var statusMessage = new GameEventWeenieError(session, WeenieError.YouCantUseThatChannel);
                            session.Network.EnqueueSend(statusMessage);
                            break;
                        }

                        var monarchPlayer = PlayerManager.GetOnlinePlayer(session.Player.AllegianceNode.Monarch.PlayerGuid);

                        if (monarchPlayer != null && !monarchPlayer.SquelchManager.Squelches.Contains(session.Player, ChatMessageType.Allegiance))
                            monarchPlayer.Session.Network.EnqueueSend(new GameEventChannelBroadcast(monarchPlayer.Session, Channel.Monarch, session.Player.Name, message));

                        session.Network.EnqueueSend(new GameEventChannelBroadcast(session, groupChatType, "", message));
                    }
                    break;
                case Channel.CoVassals:
                    {
                        if (!session.Player.HasAllegiance)
                        {
                            var statusMessage = new GameEventWeenieError(session, WeenieError.YouAreNotInAllegiance);
                            session.Network.EnqueueSend(statusMessage);
                            break;
                        }

                        if (!session.Player.PatronId.HasValue)
                        {
                            var statusMessage = new GameEventWeenieError(session, WeenieError.YouCantUseThatChannel);
                            session.Network.EnqueueSend(statusMessage);
                            break;
                        }

                        var patronPlayer = PlayerManager.GetOnlinePlayer(session.Player.AllegianceNode.Patron.PlayerGuid);

                        if (patronPlayer != null && !patronPlayer.SquelchManager.Squelches.Contains(session.Player, ChatMessageType.Allegiance))
                            patronPlayer.Session.Network.EnqueueSend(new GameEventChannelBroadcast(patronPlayer.Session, Channel.Patron, session.Player.Name, message));

                        foreach (var covassalGuid in session.Player.AllegianceNode.Patron.Vassals.Keys)
                        {
                            if (covassalGuid == session.Player.Guid.Full)
                            {
                                session.Network.EnqueueSend(new GameEventChannelBroadcast(session, groupChatType, "", message));
                            }
                            else
                            {
                                var covassalPlayer = PlayerManager.GetOnlinePlayer(covassalGuid);

                                if (covassalPlayer != null && !covassalPlayer.SquelchManager.Squelches.Contains(session.Player, ChatMessageType.Allegiance))
                                    covassalPlayer.Session.Network.EnqueueSend(new GameEventChannelBroadcast(covassalPlayer.Session, groupChatType, session.Player.Name, message));
                            }
                        }
                    }
                    break;
                case Channel.AllegianceBroadcast:
                    {
                        // The client knows if we're in an allegiance or not, and will throw an error to the user if they try to /ab, and no message will be dispatched to the server.
                        // Check anyway
                        var player = session.Player;
                        if (player.Allegiance == null)
                        {
                            session.Network.EnqueueSend(new GameEventWeenieError(session, WeenieError.YouAreNotInAllegiance));
                            break;
                        }

                        if (player.AllegiancePermissionLevel < AllegiancePermissionLevel.Speaker)
                        {
                            session.Network.EnqueueSend(new GameEventWeenieError(session, WeenieError.YouDoNotHaveAuthorityInAllegiance));
                            break;
                        }

                        // iterate through all allegiance members
                        foreach (var member in player.Allegiance.Members.Keys)
                        {
                            // is this allegiance member online?
                            var online = PlayerManager.GetOnlinePlayer(member);
                            if (online == null || online.SquelchManager.Squelches.Contains(session.Player, ChatMessageType.Allegiance))
                                continue;

                            online.Session.Network.EnqueueSend(new GameEventChannelBroadcast(online.Session, groupChatType, session.Player.Name, message));
                        }
                    }
                    break;
                default:
                    Console.WriteLine($"Unhandled ChatChannel GroupChatType: 0x{(uint)groupChatType:X4}");
                    break;
            }
        }
    }
}
