using ACE.Entity.Enum.Properties;
using ACE.Server.WorldObjects;

namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessagePublicUpdateVital : OutboundGameMessage
    {
        public GameMessagePublicUpdateVital(WorldObject worldObject, PropertyAttribute2nd attribute, uint ranks, uint baseValue, uint totalInvestment, uint currentValue)
            : base(OutboundGameMessageOpcode.PublicUpdateVital, GameMessageGroup.UIQueue)
        {
            Writer.Write(worldObject.Sequences.GetNextSequence(Sequence.SequenceType.UpdateAttribute2ndLevel, attribute));
            Writer.WriteGuid(worldObject.Guid);
            Writer.Write((uint)attribute);
            Writer.Write(ranks);
            Writer.Write(baseValue);
            Writer.Write(totalInvestment);
            Writer.Write(currentValue);
        }
    }
}
