using ACE.Server.Entity;
using ACE.Server.Network.Sequence;
using ACE.Server.WorldObjects;
using ACE.Server.Network.Structure;

namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessageUpdateMotion : OutboundGameMessage
    {
        public GameMessageUpdateMotion(WorldObject wo, MovementData movementData)
            : base(OutboundGameMessageOpcode.Motion, GameMessageGroup.SmartboxQueue)
        {
            Send(wo, movementData);
        }

        public GameMessageUpdateMotion(WorldObject wo, Motion motion)
            : base(OutboundGameMessageOpcode.Motion, GameMessageGroup.SmartboxQueue)
        {
            Send(wo, new MovementData(wo, motion));
        }

        public void Send(WorldObject wo, MovementData movementData)
        {
            Writer.WriteGuid(wo.Guid);
            Writer.Write(wo.Sequences.GetCurrentSequence(SequenceType.ObjectInstance));
            Writer.Write(movementData);
        }
    }
}
