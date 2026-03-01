using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    public class TargetDistance(Creature target, float distance)
    {
        public Creature Target = target;
        public float Distance = distance;
    }
}
