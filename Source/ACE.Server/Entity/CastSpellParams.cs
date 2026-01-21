using ACE.Entity.Enum;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    public class CastSpellParams(Spell spell, WorldObject casterItem, uint magicSkill, uint manaUsed, WorldObject target, Player.CastingPreCheckStatus status)
    {
        public Spell Spell { get; set; } = spell;
        public WorldObject CasterItem { get; set; } = casterItem;
        public uint MagicSkill { get; set; } = magicSkill;
        public uint ManaUsed { get; set; } = manaUsed;
        public WorldObject Target { get; set; } = target;
        public Player.CastingPreCheckStatus Status { get; set; } = status;

        public bool HasWindupGestures => !Spell.Flags.HasFlag(SpellFlags.FastCast) && CasterItem == null && Spell.Formula.HasWindupGestures;

        public override string ToString()
        {
            var targetName = Target != null ? Target.Name : "null";

            return $"{Spell.Name}, {CasterItem?.Name}, {MagicSkill}, {ManaUsed}, {targetName}, {Status}";
        }
    }
}
