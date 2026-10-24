using System;

using ACE.Server.Managers.ZoneControl;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        /// <summary>
        /// The Armor Breaker charm (owner 2026-10-21, Schneebly: dual wield is the boss style): the multiplier on the armor
        /// a monster has LEFT after this hit's Armor Rend / Armor Cleaving - 1 - share at the charm's level, 1 when it does
        /// not apply. Applies only to a player swinging in the dual wield stance at a monster (never PvP), with the charm
        /// on and, with t11only, a T11+ weapon whose power is live here. Effective rend = rend + share x (1 - rend), so it
        /// is worth about the same on every weapon and can never reach 100 pct armor ignored.
        /// </summary>
        public float ArmorBreakerArmorMod(WorldObject weapon, Creature defender)
        {
            if (!(this is Player player) || !player.HasArmorBreakerCharm || defender == null || defender is Player || !IsDualWieldAttack)
                return 1f;
            if (!(weapon is MeleeWeapon))
                return 1f;

            var cs = CharmSettingsManager.ArmorBreaker;
            if (!cs.Enabled)
                return 1f;
            if (cs.On("t11only") && (ZoneCraftGate.TierOf(weapon) < 11 || ZoneControlManager.WeaponPowerSuppressed(weapon, this)))
                return 1f;

            var level = CharmAbilityRegistry.LevelOf(player, CharmAbilityRegistry.ArmorBreakerAbilityId);
            if (level < 1)
                return 1f;

            var share = Math.Clamp(cs.NumberAt("share", level), 0f, 0.95f);
            return 1f - share;
        }

        /// <summary>
        /// True while the Armor Breaker charm takes cleave away (setting nocleave): a player swinging in the dual wield stance
        /// with the charm on. The trade for its single-target damage - the weapons' own cleave, Cleave cards and Improved
        /// Cleave all stop until the charm is switched off or the player stops dual wielding.
        /// </summary>
        public bool ArmorBreakerBlocksCleave()
        {
            if (!(this is Player player) || !player.HasArmorBreakerCharm || !IsDualWieldAttack)
                return false;
            var cs = CharmSettingsManager.ArmorBreaker;
            return cs.Enabled && cs.On("nocleave") && CharmAbilityRegistry.LevelOf(player, CharmAbilityRegistry.ArmorBreakerAbilityId) >= 1;
        }
    }
}
