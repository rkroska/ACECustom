using ACE.Entity;

namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessagePlayerCreate : OutboundGameMessage
    {
        public GameMessagePlayerCreate(ObjectGuid guid) : base(OutboundGameMessageOpcode.PlayerCreate, GameMessageGroup.SmartboxQueue)
        {
            Writer.WriteGuid(guid);
        }
    }
}
