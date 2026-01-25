using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    public class CastQueue(CastQueueType type, uint targetGuid, uint spellId, WorldObject casterItem)
    {
        public CastQueueType Type = type;
        public uint TargetGuid = targetGuid;
        public uint SpellId = spellId;
        public WorldObject CasterItem = casterItem;
    }

    public enum CastQueueType
    {
        Targeted,
        Untargeted
    }
}
