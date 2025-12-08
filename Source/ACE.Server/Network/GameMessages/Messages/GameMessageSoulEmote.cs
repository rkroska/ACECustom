namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessageSoulEmote : OutboundGameMessage
    {
        public GameMessageSoulEmote(uint senderId, string senderName, string emoteText)
            : base(OutboundGameMessageOpcode.SoulEmote, GameMessageGroup.UIQueue)
        {
            Writer.Write(senderId);
            Writer.WriteString16L(senderName);
            Writer.WriteString16L(emoteText);
        }
    }
}
