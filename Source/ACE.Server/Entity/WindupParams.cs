using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    public class WindupParams(uint targetGuid, uint spellId, WorldObject casterItem)
    {
        public uint TargetGuid = targetGuid;
        public uint SpellId = spellId;
        public WorldObject CasterItem = casterItem;

        public override string ToString()
        {
            return $"TargetGuid: {TargetGuid:X8}, SpellID: {SpellId}, CasterItem: {CasterItem?.Name}";
        }
    }
}
