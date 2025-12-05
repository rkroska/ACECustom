namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessageEmoteText : OutboundGameMessage
    {
        public GameMessageEmoteText(uint senderId, string senderName, string emoteText)
            : base(OutboundGameMessageOpcode.EmoteText, GameMessageGroup.UIQueue)
        {
            Writer.Write(senderId);
            Writer.WriteString16L(senderName);
            Writer.WriteString16L(emoteText);
        }
    }
}
