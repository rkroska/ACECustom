using ACE.Server.Network.Enum;

namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessageCharacterError : OutboundGameMessage
    {
        public GameMessageCharacterError(CharacterError error)
            : base(OutboundGameMessageOpcode.CharacterError, GameMessageGroup.UIQueue)
        {
            Writer.Write((uint)error);
        }
    }
}
