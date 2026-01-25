using ACE.Entity.Enum;

namespace ACE.Server.Entity
{
    public class BodyPartProbability(CombatBodyPart bodyPart, float probability)
    {
        public CombatBodyPart BodyPart = bodyPart;
        public float Probability = probability;
    }
}
