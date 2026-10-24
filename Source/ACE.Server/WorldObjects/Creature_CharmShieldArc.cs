using System;

using ACE.Server.Managers;
using ACE.Server.Managers.ZoneControl;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        /// <summary>The arc everybody's shield guards: the front half.</summary>
        public const float ShieldArcDefault = 180.0f;

        /// <summary>
        /// The Shield Arc charm (owner 2026-10-21, Schneebly: sword and shield is the survivor): the arc in degrees, centred
        /// on where this creature faces, in which its shield works against this attacker's melee or missile hit. 180 (the
        /// front half) unless a player with the charm on is hit by a monster (never PvP); with t11only the shield must be
        /// T11+. Never below 180. 360 = every direction.
        /// </summary>
        public float ShieldArcFor(WorldObject shield, WorldObject attacker)
        {
            if (!(this is Player player) || !player.HasShieldArcCharm || shield == null || !(attacker is Creature) || attacker is Player)
                return ShieldArcDefault;

            var cs = CharmSettingsManager.ShieldArc;
            if (!cs.Enabled)
                return ShieldArcDefault;
            if (cs.On("t11only") && ZoneCraftGate.TierOf(shield) < 11)
                return ShieldArcDefault;

            var level = CharmAbilityRegistry.LevelOf(player, CharmAbilityRegistry.ShieldArcAbilityId);
            if (level < 1)
                return ShieldArcDefault;

            return Math.Clamp(cs.NumberAt("angle", level), ShieldArcDefault, 360.0f);
        }

        /// <summary>
        /// The Shield Arc charm's damage (owner 2026-10-22): a player's melee hit on a monster while a shield is in hand
        /// deals 1 + damage times as much. 1 when the charm does not apply - PvP, no shield, a missile or spell attack, no
        /// charm; with t11only the shield must be T11+.
        /// </summary>
        public float ShieldArcDamageMod(WorldObject weapon, Creature defender)
        {
            if (!(this is Player player) || !player.HasShieldArcCharm || defender == null || defender is Player)
                return 1f;
            if (!(weapon is MeleeWeapon))
                return 1f;

            var shield = GetEquippedShield();
            if (shield == null)
                return 1f;

            var cs = CharmSettingsManager.ShieldArc;
            if (!cs.Enabled)
                return 1f;
            if (cs.On("t11only") && ZoneCraftGate.TierOf(shield) < 11)
                return 1f;

            var level = CharmAbilityRegistry.LevelOf(player, CharmAbilityRegistry.ShieldArcAbilityId);
            if (level < 1)
                return 1f;

            var bonus = cs.NumberAt("damage", level);
            return float.IsFinite(bonus) ? 1f + Math.Clamp(bonus, 0f, 5f) : 1f;
        }

        /// <summary>
        /// The T11+ shield cap (owner 2026-10-21): against a Zone Control monster a player's shield may block at most
        /// zc_shield_max_block of the normal damage, so the shield multiplier never drops below 1 - that. Returns the
        /// multiplier unchanged for anything else (retail / T10 monsters, PvP, a cap of 1 or more).
        /// </summary>
        public float CapShieldModVsZoneMonster(float shieldMod, WorldObject attacker)
        {
            if (!(this is Player) || !(attacker is Creature monster) || attacker is Player)
                return shieldMod;
            if (ZoneControlManager.ResolveCombatProfile(monster) == null)
                return shieldMod;

            var maxBlock = ServerConfig.zc_shield_max_block.Value;
            if (!double.IsFinite(maxBlock) || maxBlock >= 1.0)
                return shieldMod;

            var floor = (float)(1.0 - Math.Clamp(maxBlock, 0.0, 1.0));
            return Math.Max(shieldMod, floor);
        }
    }
}
