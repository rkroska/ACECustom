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
    }
}
