using ACE.Entity.Enum;

namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessageSystemChat : OutboundGameMessage
    {
        public GameMessageSystemChat(string message, ChatMessageType chatMessageType)
            : base(OutboundGameMessageOpcode.ServerMessage, GameMessageGroup.UIQueue)
        {
            Writer.WriteString16L(message);
            Writer.Write((int)chatMessageType);
        }
    }
}
