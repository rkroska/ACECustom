using ACE.Server.WorldObjects;

namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessageUpdateObject : OutboundGameMessage
    {
        public GameMessageUpdateObject(WorldObject worldObject, bool adminvision = false, bool changenodraw = false)
            : base(OutboundGameMessageOpcode.UpdateObject, GameMessageGroup.SmartboxQueue)
        {
            worldObject.SerializeUpdateObject(Writer, adminvision, changenodraw);
        }
    }
}
