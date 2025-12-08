
using ACE.Server.WorldObjects;

namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessageInventoryRemoveObject : OutboundGameMessage
    {
        public GameMessageInventoryRemoveObject(WorldObject worldObject)
            : base(OutboundGameMessageOpcode.InventoryRemoveObject, GameMessageGroup.UIQueue)
        {
            Writer.WriteGuid(worldObject.Guid);
        }
    }
}
