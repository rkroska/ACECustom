namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessageCharacterLogOff : OutboundGameMessage
    {
        public GameMessageCharacterLogOff() : base(OutboundGameMessageOpcode.CharacterLogOff, GameMessageGroup.UIQueue)
        {
        }
    }
}
