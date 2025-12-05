using ACE.Server.Network.Sequence;
using ACE.Server.WorldObjects;

namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessagePickupEvent : OutboundGameMessage
    {
        public GameMessagePickupEvent(WorldObject targetItem)
            : base(OutboundGameMessageOpcode.PickupEvent, GameMessageGroup.SmartboxQueue)
        {
            Writer.Write(targetItem.Guid.Full);
            Writer.Write(targetItem.Sequences.GetCurrentSequence(SequenceType.ObjectInstance));
            Writer.Write(targetItem.Sequences.GetNextSequence(SequenceType.ObjectPosition));
        }
    }
}
