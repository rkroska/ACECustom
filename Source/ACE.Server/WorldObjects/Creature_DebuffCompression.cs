using System;

using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Debuff compression for T11+ monsters (owner 2026-10-04, ZoneControl\DebuffCompression_Plan_2026-10-04.md).
    ///
    /// On these monsters a life VULN and IMPERIL stop working the retail way - the vuln multiplied the resist slot (MAX'd with
    /// the weapon's rend, so a rend weapon got nothing from it) and Imperil subtracted armor, which went NEGATIVE: the armor
    /// curve's negative side has no ceiling, so Imperil was x312 (T11) .. x2,150 (T25) melee damage. Each becomes one capped
    /// damage BONUS that ramps on the caster's life augs (Triune counts):
    ///
    ///     bonus = floor + (cap - floor) x clamp((lifeAugs - entry) / ramp, 0, 1), x the spell level's share
    ///
    /// melee / missile x (1 + vuln + imperil), spells x (1 + vuln) - Imperil never touches spells (they skip armor). The vuln
    /// stacks ON TOP of rend (owner 10-04, reversing the 08-27 "never stack" ruling). Below the tier's entry life augs = the
    /// floor, at entry + ramp and above = the cap. A proc'd debuff counts like a cast with the wielder's life augs
    /// (EnchantmentManager.Add stamps them). Every number is a per-tier zone setting (plugin Defense tab):
    /// vuln_bonus_floor 0.25 / vuln_bonus_cap 0.50 / imperil_bonus_floor 0.25 / imperil_bonus_cap 0.50 /
    /// debuff_entry_life_augs (unset = 2,000 + 500 x (tier - 11)) / debuff_ramp_augs (unset = 500).
    ///
    /// Wiring: GetResistanceMod drops the vuln from the rend slot and multiplies (1 + vuln) for every damage path (melee,
    /// missile, spells, ring, DoTs); DamageEvent adds Imperil on top (additive with the vuln); the body-armor sum counts only
    /// positive armor enchantments, so Imperil no longer lowers armor.
    /// </summary>
    partial class Creature
    {
        /// <summary>Same gate as the retail-vuln compression it replaces: a non-player, non-pet monster at endgame variation
        /// zc_vuln_min_variation (11) and up, with zc_vuln_enabled and zc_combat_rules_enabled on.</summary>
        public bool ZcDebuffCompressed =>
            !(this is Player) && !(this is CombatPet)
            && ServerConfig.zc_vuln_enabled.Value && ServerConfig.zc_combat_rules_enabled.Value
            && VariationManager.GetEffectiveEndgameVariation(this) >= ServerConfig.zc_vuln_min_variation.Value;

        public const double DebuffDefaultFloor = 0.25;
        public const double DebuffDefaultCap = 0.50;
        public const double DebuffDefaultRamp = 500;

        /// <summary>Incantation of X Vulnerability Other = x3.10 raw; a lower level gets its share of the bonus.</summary>
        private const double VulnIncantationRaw = 3.10;
        /// <summary>Incantation of Imperil Other = -225 armor raw.</summary>
        private const double ImperilIncantationRaw = 225.0;

        /// <summary>
        /// The vuln bonus for this resistance type and (withImperil) the Imperil bonus, as fractions (0.5 = +50 pct). Both 0 when
        /// the monster is not gated or carries no such debuff. The strongest entry of each kind counts (top layer per category,
        /// so a proc'd and a cast debuff of one kind never add up).
        /// </summary>
        public (float Vuln, float Imperil) GetZcDebuffBonus(DamageType resistanceType, bool withImperil)
        {
            if (!ZcDebuffCompressed)
                return (0f, 0f);

            var zp = ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveForCreature(this);
            var tier = VariationManager.GetEffectiveEndgameVariation(this);
            double Get(string key, double fallback) => zp != null && zp.Has(key) ? zp.Get(key) : fallback;

            var entry = Get(ZoneStat.DebuffEntryLifeAugs, 2000 + 500 * Math.Max(0, tier - 11));
            var ramp = Math.Max(1.0, Get(ZoneStat.DebuffRampAugs, DebuffDefaultRamp));

            double vuln = 0;
            foreach (var e in EnchantmentManager.GetVulnerabilityEntries(resistanceType))
            {
                var share = SpellShare((uint)e.SpellId, s => (s.StatModVal - 1.0) / (VulnIncantationRaw - 1.0));
                var b = Ramp(Get(ZoneStat.VulnBonusFloor, DebuffDefaultFloor), Get(ZoneStat.VulnBonusCap, DebuffDefaultCap), e.AugmentationLevelWhenCast ?? 0, entry, ramp) * share;
                if (b > vuln) vuln = b;
            }

            double imperil = 0;
            if (withImperil)
            {
                foreach (var e in EnchantmentManager.GetImperilEntries())
                {
                    var share = SpellShare((uint)e.SpellId, s => -s.StatModVal / ImperilIncantationRaw);
                    var b = Ramp(Get(ZoneStat.ImperilBonusFloor, DebuffDefaultFloor), Get(ZoneStat.ImperilBonusCap, DebuffDefaultCap), e.AugmentationLevelWhenCast ?? 0, entry, ramp) * share;
                    if (b > imperil) imperil = b;
                }
            }

            return ((float)Math.Max(0.0, vuln), (float)Math.Max(0.0, imperil));
        }

        private static double Ramp(double floor, double cap, double lifeAugs, double entry, double ramp)
            => floor + (cap - floor) * Math.Clamp((lifeAugs - entry) / ramp, 0.0, 1.0);

        /// <summary>The spell level's share of the bonus (Incantation = 1.0), clamped 0..1; 1.0 when the spell is unknown.</summary>
        private static double SpellShare(uint spellId, Func<ACE.Server.Entity.Spell, double> share)
        {
            var spell = new ACE.Server.Entity.Spell(spellId);
            return spell.NotFound ? 1.0 : Math.Clamp(share(spell), 0.0, 1.0);
        }
    }
}
