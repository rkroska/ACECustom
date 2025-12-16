using ACE.Server.Entity.Actions;
using ACE.Server.Network.Enum;
using ACE.Server.Network.GameAction;
using ACE.Server.Network.GameMessages;
using ACE.Server.Network.Handlers;
using log4net;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ACE.Server.Network.Managers
{
    public static class InboundMessageManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private class ActionHandlerInfo
        {
            public ActionHandler Handler { get; set; }
            public GameActionAttribute Attribute { get; set; }
        }

        public delegate void MessageHandler(ClientMessage message, Session session);

        public delegate void ActionHandler(ClientMessage message, Session session);

        private static Dictionary<GameActionType, ActionHandlerInfo> actionHandlers;

        public static void Initialize()
        {
            DefineActionHandlers();
        }

        private static void DefineActionHandlers()
        {
            actionHandlers = [];

            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                foreach (var methodInfo in type.GetMethods())
                {
                    foreach (var actionHandlerAttribute in methodInfo.GetCustomAttributes<GameActionAttribute>())
                    {
                        var actionhandler = new ActionHandlerInfo()
                        {
                            Handler = (ActionHandler)Delegate.CreateDelegate(typeof(ActionHandler), methodInfo),
                            Attribute = actionHandlerAttribute
                        };

                        actionHandlers[actionHandlerAttribute.Opcode] = actionhandler;
                    }
                }
            }
        }

        public static void HandleClientMessage(ClientMessage message, Session session)
        {
            var opcode = (InboundGameMessageOpcode)message.Opcode;
            ActionType actionType = ActionTypeConverter.FromInboundGameMessageOpCode(opcode);

            switch (opcode)
            {
                case InboundGameMessageOpcode.None:
                    break;

                case InboundGameMessageOpcode.ForceObjectDescSend:
                    EnqueueAction(actionType, session, SessionState.WorldConnected, () => ControlHandler.ControlResponse(message, session));
                    break;

                case InboundGameMessageOpcode.GameAction:
                    // We can derive a more specific ActionType for logging from the nested GameActionType.
                    _ = message.Payload.ReadUInt32(); // sequence number - consumed but not used
                    GameActionType gameActionType = (GameActionType)message.Payload.ReadUInt32();
                    EnqueueAction(ActionTypeConverter.FromGameActionType(gameActionType), session, SessionState.WorldConnected, () => HandleGameAction(gameActionType, message, session));
                    break;

                case InboundGameMessageOpcode.GetServerVersion:
                    EnqueueAction(actionType, session, SessionState.WorldConnected, () => GetServerVersionHandler.GetServerVersion(message, session));
                    break;

                case InboundGameMessageOpcode.FriendsOld:
                    EnqueueAction(actionType, session, SessionState.WorldConnected, () => FriendsOldHandler.FriendsOld(message, session));
                    break;

                case InboundGameMessageOpcode.TurbineChat:
                    EnqueueAction(actionType, session, SessionState.WorldConnected, () => TurbineChatHandler.TurbineChatReceived(message, session));
                    break;

                case InboundGameMessageOpcode.CharacterLogOff:
                    EnqueueAction(actionType, session, SessionState.WorldConnected, () => CharacterHandler.CharacterLogOff(message, session));
                    break;

                case InboundGameMessageOpcode.CharacterDelete:
                    EnqueueAction(actionType, session, SessionState.AuthConnected, () => CharacterHandler.CharacterDelete(message, session));
                    break;

                case InboundGameMessageOpcode.CharacterCreate:
                    EnqueueAction(actionType, session, SessionState.AuthConnected, () => CharacterHandler.CharacterCreate(message, session));
                    break;

                case InboundGameMessageOpcode.CharacterEnterWorld:
                    EnqueueAction(actionType, session, SessionState.AuthConnected, () => CharacterHandler.CharacterEnterWorld(message, session));
                    break;

                case InboundGameMessageOpcode.CharacterEnterWorldRequest:
                    EnqueueAction(actionType, session, SessionState.AuthConnected, () => CharacterHandler.CharacterEnterWorldRequest(message, session));
                    break;

                case InboundGameMessageOpcode.CharacterRestore:
                    EnqueueAction(actionType, session, SessionState.AuthConnected, () => CharacterHandler.CharacterRestore(message, session));
                    break;

                case InboundGameMessageOpcode.DDD_InterrogationResponse:
                    EnqueueAction(actionType, session, SessionState.AuthConnected, () => DDDHandler.DDD_InterrogationResponse(message, session));
                    break;

                case InboundGameMessageOpcode.DDD_RequestDataMessage:
                    EnqueueAction(actionType, session, SessionState.WorldConnected, () => DDDHandler.DDD_RequestDataMessage(message, session));
                    break;

                case InboundGameMessageOpcode.DDD_EndDDD:
                    EnqueueAction(actionType, session, SessionState.AuthConnected, () => DDDHandler.DDD_EndDDD(message, session));
                    break;

                default:
                    log.Warn($"Received unhandled fragment opcode: 0x{(int)opcode:X4} - {opcode}");
                    break;
            }
        }

        private static void EnqueueAction(ActionType actionType, Session session, SessionState required_state, Action actionToRun)
        {
            if (session.State != required_state) return;
            NetworkManager.InboundMessageQueue.EnqueueAction(new ActionEventDelegate(actionType, () =>
            {
                // It's possible that before this work is executed by WorldManager, and after it was enqueued here, the session.Player was set to null
                // To avoid null reference exceptions, we make sure that the player is valid before the message handler is invoked.
                if (required_state == SessionState.WorldConnected && session.Player == null)
                    return;

                try
                {
                    actionToRun();
                }
                catch (Exception ex)
                {
                    log.Error($"Received GameMessage packet that threw an exception from account: {session.AccountId}:{session.Account}, player: {session.Player?.Name}, actionType: {actionType}");
                    log.Error(ex);
                }
            }));
        }

        /// <summary>
        /// The call path for this function is as follows:
        /// InboundMessageManager.HandleClientMessage() queues work into NetworkManager.InboundMessageQueue that is run in WorldManager.UpdateWorld(), which invokes this.
        /// </summary>
        private static void HandleGameAction(GameActionType gameActionType, ClientMessage message, Session session)
        {
            if (actionHandlers.TryGetValue(gameActionType, out var actionHandlerInfo))
            {
                // It's possible that before this work is executed by WorldManager, and after it was enqueued here, the session.Player was set to null
                // To avoid null reference exceptions, we make sure that the player is valid before the message handler is invoked.
                if (session.Player == null)
                    return;

                try
                {
                    actionHandlerInfo.Handler.Invoke(message, session);
                }
                catch (Exception ex)
                {
                    log.Error($"Received GameAction packet that threw an exception from account: {session.AccountId}:{session.Account}, player: {session.Player?.Name}, opcode: 0x{((int)gameActionType):X4}:{gameActionType}");
                    log.Error(ex);
                }
            }
            else
            {
                log.Warn($"Received unhandled GameActionType: 0x{(int)gameActionType:X4} - {gameActionType}");
            }
        }
    }
}
