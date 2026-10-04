using ACE.Server.Managers;
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

            var level = zp.Get(ZoneStat.SpellArmor);
            return double.IsFinite(level) && level > 0 ? SkillFormula.CalcArmorMod((float)level) : 1.0f;   // non-finite = unset
        }

        /// <summary>
        /// ZONE CONTROL SPELL CRITS MIRROR MELEE (owner 2026-10-04, T11_Release_Readiness_2026-10-04.md 3.2). The monster's
        /// crit_damage_resist_rating rows were solved so a MELEE crit lands at 3x, and melee builds its crit two ways spells
        /// did not: (1) Crit Damage Rating multiplies the crit ON ITS OWN (DamageEvent: DamageBeforeMitigation *= crit DR mod)
        /// where a spell folded it additively into Damage Rating (x1.58 instead of x15.24 at T25), and (2) every melee /
        /// missile aug adds melee_missile_aug_crit_modifier (0.002) to the crit multiplier, which spells never had, and (3) the
        /// crit mod is max(Crushing, the weapon-scaling crit floor kc x 0.002 x augs), whose floor was 0 for a wand
        /// (WeaponScalingCombat.GetSpellCritDamageBonus, added the same day). The same
        /// CDR then divided a ~4-10x spell crit by melee's ~13-533x stack: spell crits landed at 0.98x (T11) .. 0.06x (T25).
        /// True when both melee rules apply to this spell crit: a player HAND-casting with Zone Control gear at a Zone Control
        /// monster. Retail (base world, retail gear), PvP, pets, monster casts and Cast on Strike procs (their own tuning, as
        /// with Spell Armor) keep the stock spell crit.
        /// </summary>
        public static bool ZcSpellCritMirrorsMelee(Creature caster, WorldObject weapon, Creature target, bool fromProc)
        {
            if (fromProc || !(caster is Player) || target == null || target is Player || target is CombatPet)
                return false;

            return ACE.Server.Managers.ZoneControl.ZoneControlManager.EndgameRulesApplyToPlayerGear(weapon)
                && ACE.Server.Managers.ZoneControl.ZoneControlManager.EndgameRulesApplyToMonster(target);
        }

        /// <summary>The melee aug crit term for a war / void / life spell: school aug count x melee_missile_aug_crit_modifier
        /// (the same knob melee and missile read, so the two schools cannot drift). Added to the crit damage mod.</summary>
        public static float ZcSpellAugCritBonus(long schoolAugs)
            => schoolAugs > 0 ? schoolAugs * (float)ServerConfig.melee_missile_aug_crit_modifier.Value : 0f;
    }
}
