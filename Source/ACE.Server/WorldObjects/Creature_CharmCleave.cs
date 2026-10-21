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
            if (cs.On("t11only") && (locked || ZoneCraftGate.TierOf(weapon) < 11))
                return (false, 0, 0f, 0f, false);
            var level = CharmAbilityRegistry.LevelOf(player, CharmAbilityRegistry.ImprovedCleaveAbilityId);
            var targets = cs.CountAt("targets", level);
            if (level < 1 || targets <= 0)
                return (false, 0, 0f, 0f, false);
            // never worse than the cleave everybody has: 2 m, the front half circle
            return (true, targets, Math.Max(CleaveCylRange, cs.NumberAt("range", level)), Math.Max(CleaveAngle, cs.NumberAt("angle", level)), cs.On("los"));
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
