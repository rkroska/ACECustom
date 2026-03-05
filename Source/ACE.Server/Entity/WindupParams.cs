using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    public class WindupParams(uint targetGuid, Spell spell, WorldObject casterItem)
    {
        public uint TargetGuid = targetGuid;
        public Spell Spell = spell;
        public WorldObject CasterItem = casterItem;

        public override string ToString()
        {
            return $"TargetGuid: {TargetGuid:X8}, SpellID: {Spell.Id}, CasterItem: {CasterItem?.Name}";
        }
    }
}
