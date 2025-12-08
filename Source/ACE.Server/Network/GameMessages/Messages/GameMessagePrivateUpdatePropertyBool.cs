using System;
using ACE.Entity.Enum.Properties;
using ACE.Server.WorldObjects;

namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessagePrivateUpdatePropertyBool : OutboundGameMessage
    {
        public GameMessagePrivateUpdatePropertyBool(WorldObject worldObject, PropertyBool property, bool value) : base(OutboundGameMessageOpcode.PrivateUpdatePropertyBool, GameMessageGroup.UIQueue)
        {
            Writer.Write(worldObject.Sequences.GetNextSequence(Sequence.SequenceType.UpdatePropertyBool, property));
            Writer.Write((uint)property);
            Writer.Write(Convert.ToUInt32(value));
        }
    }
}
