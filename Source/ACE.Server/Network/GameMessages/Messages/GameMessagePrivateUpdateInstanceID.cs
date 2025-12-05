using ACE.Entity.Enum.Properties;
using ACE.Server.WorldObjects;

namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessagePrivateUpdateInstanceID : OutboundGameMessage
    {
        public GameMessagePrivateUpdateInstanceID(WorldObject worldObject, PropertyInstanceId property, uint value)
            : base(OutboundGameMessageOpcode.PrivateUpdatePropertyInstanceID, GameMessageGroup.UIQueue)
        {
            Writer.Write(worldObject.Sequences.GetNextSequence(Sequence.SequenceType.UpdatePropertyInstanceID, property));
            Writer.Write((uint)property);
            Writer.Write(value);
        }
    }
}
