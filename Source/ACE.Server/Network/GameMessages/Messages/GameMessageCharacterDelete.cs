namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessageCharacterDelete : OutboundGameMessage
    {
        public GameMessageCharacterDelete()
            : base(OutboundGameMessageOpcode.CharacterDelete, GameMessageGroup.UIQueue)
        {
        }
    }
}
