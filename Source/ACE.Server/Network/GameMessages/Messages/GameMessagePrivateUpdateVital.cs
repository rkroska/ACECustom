using ACE.Server.WorldObjects;
using ACE.Server.WorldObjects.Entity;

namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessagePrivateUpdateVital : OutboundGameMessage
    {
        public GameMessagePrivateUpdateVital(WorldObject worldObject, CreatureVital creatureVital)
            : base(OutboundGameMessageOpcode.PrivateUpdateVital, GameMessageGroup.UIQueue)
        {
            Writer.Write(worldObject.Sequences.GetNextSequence(Sequence.SequenceType.UpdateAttribute2ndLevel, creatureVital.Vital));

            Writer.Write((uint)creatureVital.Vital);
            Writer.Write(creatureVital.Ranks);
            Writer.Write(creatureVital.StartingValue);
            Writer.Write(creatureVital.ExperienceSpent);
            Writer.Write(creatureVital.Current);
        }
    }
}
