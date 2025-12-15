namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessageCharacterEnterWorldServerReady : OutboundGameMessage
    {
        public GameMessageCharacterEnterWorldServerReady()
            : base(OutboundGameMessageOpcode.CharacterEnterWorldServerReady, GameMessageGroup.UIQueue)
        {
        }
    }
}
