using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Managers.ZoneControl;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        /// <summary>
        /// The levelled caster charms REPLACE the older multi-target charms instead of stacking on them (owner 2026-10-21,
        /// Schneebly): with Fork and Split Cast on top, a Chain bolt or a Pandemic Corrosion hit 5-25 monsters a cast.
        /// True when this spell, cast with this weapon, belongs to an active levelled charm whose 'exclusive' switch is on:
        ///   - Chain: a war spell (the war arcs / bolts / streaks Fork and Split Cast work on);
        ///   - Pandemic: a void damage-over-time spell (Corrosion / Corruption ride Fork and Split Cast as bolts).
        /// Fork and Split Cast then step aside for that cast. A charm that would not work here (T11 rule: no live T11+ wand)
        /// does not take their place, so they work as before.
        /// </summary>
        public bool LeveledCharmReplacesForkAndSplit(Spell spell, WorldObject weapon)
        {
            if (spell == null)
                return false;

            if (spell.School == MagicSchool.WarMagic && HasChainCharm)
                return Exclusive(CharmSettingsManager.Chain, CharmAbilityRegistry.ChainAbilityId, weapon);

            if (spell.School == MagicSchool.VoidMagic && spell.DotDuration > 0 && HasPandemicCharm)
                return Exclusive(CharmSettingsManager.Pandemic, CharmAbilityRegistry.PandemicAbilityId, weapon);

            return false;
        }

        private bool Exclusive(CharmSettingsManager.LeveledCharmBlock block, int abilityId, WorldObject weapon)
        {
            if (!block.Enabled || !block.On("exclusive") || CharmAbilityRegistry.LevelOf(this, abilityId) < 1)
                return false;

            // the same T11 rule the charm itself applies: no live T11+ wand, no charm - so Fork / Split Cast stay
            if (block.On("t11only") && (!(weapon is Caster) || ZoneCraftGate.TierOf(weapon) < 11 || ZoneControlManager.WeaponPowerSuppressed(weapon, this)))
                return false;

            return true;
        }
    }
}
