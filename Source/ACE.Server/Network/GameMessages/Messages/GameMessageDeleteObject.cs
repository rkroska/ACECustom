using ACE.Server.Network.Sequence;
using ACE.Server.WorldObjects;

namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessageDeleteObject : OutboundGameMessage
    {
        public GameMessageDeleteObject(WorldObject worldObject) : base(OutboundGameMessageOpcode.ObjectDelete, GameMessageGroup.SmartboxQueue)
        {
            Writer.WriteGuid(worldObject.Guid);
            Writer.Write(worldObject.Sequences.GetCurrentSequence(SequenceType.ObjectInstance));
            Writer.Align();
        }
    }
}
