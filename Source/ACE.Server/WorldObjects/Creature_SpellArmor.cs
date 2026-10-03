using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// SPELL ARMOR (owner 2026-10-04, ZoneControl\BenchMagic_VulnPlan_2026-10-03.md section 2c): the spell counterpart to Armor.
    /// Monster armor cuts a melee hit to ~6 pct while spells skip it, so a hand-cast streak / arc did 134x (T11) .. 1,298x (T25)
    /// a melee hit. Spell Armor is a LEVEL on the same curve as armor (SkillFormula.CalcArmorMod: 66.67 / (66.67 + level)),
    /// set per zone / tier / rank like any zone stat (plugin Defense tab, `spell_armor`). Unset or 0 = full damage.
    ///
    /// Rules: player HAND casts only - Cast on Strike procs keep their own tuning (owner 10-04); only reduces damage, never a
    /// miss (landing stays magic_defense); Imperil and Armor Rend never touch it; players, pets and monster-on-monster never.
    /// Wired into SpellProjectile.CalculateDamage (both paths), Player_Magic ring damage and the DoT tick.
    /// </summary>
    partial class Creature
    {
        /// <summary>x damage this monster takes from a spell of <paramref name="source"/>; 1.0 when Spell Armor does not apply.</summary>
        public float GetZcSpellArmorMod(WorldObject source, bool fromProc)
        {
            if (fromProc || !(source is Player) || this is Player || this is CombatPet)
                return 1.0f;

            var zp = ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveForCreature(this);
            if (zp == null || !zp.Has(ZoneStat.SpellArmor))
                return 1.0f;

            var level = (float)zp.Get(ZoneStat.SpellArmor);
            return level > 0 ? SkillFormula.CalcArmorMod(level) : 1.0f;
        }
    }
}
