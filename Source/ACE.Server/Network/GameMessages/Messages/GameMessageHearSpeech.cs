using ACE.Entity.Enum;

namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessageHearSpeech : OutboundGameMessage
    {
        public GameMessageHearSpeech(string messageText, string senderName, uint senderID, ChatMessageType chatMessageType)
            : base(OutboundGameMessageOpcode.HearSpeech, GameMessageGroup.UIQueue)
        {
            Writer.WriteString16L(messageText);
            Writer.WriteString16L(senderName);
            Writer.Write(senderID);
            Writer.Write((uint)chatMessageType);
        }
    }
}
