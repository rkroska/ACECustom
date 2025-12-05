using ACE.Server.WorldObjects;

namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessageCreateObject : OutboundGameMessage
    {
        public GameMessageCreateObject(WorldObject worldObject, bool adminvision = false, bool adminnodraw = false)
            : base(OutboundGameMessageOpcode.ObjectCreate, GameMessageGroup.SmartboxQueue)
        {
            worldObject.SerializeCreateObject(Writer, adminvision, adminnodraw);
        }
    }
}
