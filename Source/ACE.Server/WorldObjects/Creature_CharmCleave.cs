using System;

using ACE.Server.Managers.ZoneControl;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        /// <summary>
        /// The Improved Cleave charm (owner 2026-10-13: "Melee Only. Increase cleave range, makes cleave 360. +1 cleave
        /// targets" and "Gives +2 cleave if no cleave exists, otherwise use the higher of the 2 like split"): what the
        /// charm gives THIS attacker with this weapon - its extra targets, its reach, its arc - at the charm's level.
        /// On = false when it does not apply: not a player, no active charm, charm switched off, not a melee weapon, the
        /// level gives no targets, or the T11 rule says no (setting t11only: a T11+ weapon whose power is live here).
        /// Monsters never get it. The picker (GetCleaveTarget) takes the higher of these and the weapon's own, never the sum.
        /// </summary>
        public (bool On, int Targets, float Range, float Angle, bool Los) CharmCleaveFor(WorldObject weapon, bool locked)
        {
            var cs = CharmSettingsManager.ImprovedCleave;
            if (!(this is Player player) || !(weapon is MeleeWeapon) || !cs.Enabled || !player.HasImprovedCleaveCharm)
                return (false, 0, 0f, 0f, false);
            // owner 2026-10-21 (Schneebly): the pack-clearing charm is the TWO-HANDER's - dual wield is the boss style and
            // sword and shield the survivor, so their weapons keep only their own cleave (setting twohandedonly)
            if (cs.On("twohandedonly") && weapon.WeaponSkill != ACE.Entity.Enum.Skill.TwoHandedCombat)
                return (false, 0, 0f, 0f, false);
            if (cs.On("t11only") && (locked || ZoneCraftGate.TierOf(weapon) < 11))
                return (false, 0, 0f, 0f, false);
            var level = CharmAbilityRegistry.LevelOf(player, CharmAbilityRegistry.ImprovedCleaveAbilityId);
            var targets = cs.CountAt("targets", level);
            if (level < 1 || targets <= 0)
                return (false, 0, 0f, 0f, false);
            // never worse than the cleave everybody has: 2 m, the front half circle
            return (true, targets, Math.Max(CleaveCylRange, cs.NumberAt("range", level)), Math.Max(CleaveAngle, cs.NumberAt("angle", level)), cs.On("los"));
        }

        /// <summary>
        /// The Improved Cleave charm's damage bonus (owner 2026-10-25, mirrors ShieldArcDamageMod): a player's two-handed melee hit
        /// on a monster - the main target and every cleaved one - deals 1 + bonus times as much. 1 when the charm does not apply:
        /// PvP, not melee, no charm, switched off, not two-handed (twohandedonly), below T11 (t11only), or a bonus of 0.
        /// </summary>
        public float ImprovedCleaveDamageMod(WorldObject weapon, Creature defender)
        {
            if (!(this is Player player) || !player.HasImprovedCleaveCharm || defender == null || defender is Player)
                return 1f;
            if (!(weapon is MeleeWeapon))
                return 1f;

            var cs = CharmSettingsManager.ImprovedCleave;
            if (!cs.Enabled)
                return 1f;
            if (cs.On("twohandedonly") && weapon.WeaponSkill != ACE.Entity.Enum.Skill.TwoHandedCombat)
                return 1f;
            if (cs.On("t11only") && ZoneCraftGate.TierOf(weapon) < 11)
                return 1f;

            var level = CharmAbilityRegistry.LevelOf(player, CharmAbilityRegistry.ImprovedCleaveAbilityId);
            if (level < 1)
                return 1f;

            var bonus = cs.NumberAt("bonus", level);
            return float.IsFinite(bonus) ? 1f + Math.Clamp(bonus, 0f, 5f) : 1f;
        }

        /// <summary>
        /// How many extra monsters a swing cleaves with the charm on (owner 2026-10-21): the weapon's own cleave PLUS the
        /// charm's targets, held to the charm's 'maxtargets' (0 = no cap). Most two-handers cleave only 1 monster of their
        /// own, so the charm adds to it rather than taking the higher of the two. The one rule the picker and the panel share.
        /// </summary>
        public static int CharmCleaveTotal(int ownCount, int charmTargets, int level)
        {
            var total = Math.Max(0, ownCount) + Math.Max(0, charmTargets);
            var cap = CharmSettingsManager.ImprovedCleave.CountAt("maxtargets", level);
            return cap > 0 ? Math.Min(total, cap) : total;
        }

        /// <summary>How many monsters the WEAPON's own cleave takes (0 = it does not cleave here): its cleave targets, held to
        /// the T10 ceiling while the zone lock holds its power back, none while locked with the fallback off. The one rule
        /// GetCleaveTarget and the cleave damage share both use.</summary>
        public static int OwnCleaveCount(WorldObject weapon, bool locked)
        {
            if (weapon == null || !weapon.IsCleaving || (locked && !ZoneLockFallback.Active))
                return 0;
            return locked ? Math.Min(weapon.CleaveTargets, ZoneLockFallback.CleaveCap) : weapon.CleaveTargets;
        }

        /// <summary>
        /// The Improved Cleave charm's damage share for a cleaved hit (owner 2026-10-14: "Add a 50% damage setting to cleave,
        /// use it as a tuning tool"): the charm's 'damage' at its level while the charm is on, else 1. The melee path gives it
        /// only to the hits the charm ADDS: the first OwnCleaveCount targets are the weapon's own and keep full damage (or the
        /// share, if a levelled share is above 1) - review 2026-10-20 rounds 1 and 2.
        /// </summary>
        public float CharmCleaveDamageScale(WorldObject weapon, bool locked)
        {
            var charm = CharmCleaveFor(weapon, locked);
            if (!charm.On || !(this is Player player))
                return 1f;
            var level = CharmAbilityRegistry.LevelOf(player, CharmAbilityRegistry.ImprovedCleaveAbilityId);
            return Math.Clamp((float)CharmSettingsManager.ImprovedCleave.NumberAt("damage", level), 0f, 2f);
        }

        /// <summary>Set by the melee path only while it deals one cleaved hit; DamageEvent scales that hit by it.</summary>
        internal float? PendingCleaveDamageScale;
    }
}
