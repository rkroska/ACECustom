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
        /// <summary>DOT ARMOR x DOT DAMAGE MULT (owner 2026-10-04): x a PLAYER's DoT tick on this T11+ monster, 66.67 / (66.67 +
        /// dot_armor) x dot_damage_mult. Replaces
        /// Spell Armor + Damage Resist + DoT Resist + nether resist rating for that tick. 1.0 when unset / not a gated monster.
        /// Corruption and Destructive Curse ticks also take their own dot_mult_corruption / dot_mult_destructive (owner 2026-10-04
        /// night: Corrosion is the main DoT, the other two ~10 pct of it).</summary>
        public float GetZcDotArmorMod(uint spellId = 0)
        {
            if (!ZcDebuffCompressed) return 1.0f;
            var zp = ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveCombatProfile(this);
            if (zp == null) return 1.0f;
            var mod = 1.0f;
            // a non-finite stat counts as unset (a NaN here would make every tick NaN)
            if (zp.Has(ZoneStat.DotArmor))
            {
                var level = zp.Get(ZoneStat.DotArmor);
                if (double.IsFinite(level) && level > 0) mod *= SkillFormula.CalcArmorMod((float)level);
            }
            mod *= FiniteMult(zp, ZoneStat.DotDamageMult);
            var own = IsCorruptionSpell(spellId) ? ZoneStat.DotMultCorruption : IsDestructiveCurseSpell(spellId) ? ZoneStat.DotMultDestructive : null;
            if (own != null)
                mod *= FiniteMult(zp, own);
            return mod;
        }

        /// <summary>A multiplier stat, at least 0; 1 when unset or not finite.</summary>
        private static float FiniteMult(EvaluatedProfile zp, string stat)
        {
            if (!zp.Has(stat)) return 1.0f;
            var v = zp.Get(stat);
            // capped at float.MaxValue: a finite double above float range (1e39) became +Infinity, and 0 x Infinity = NaN
            return double.IsFinite(v) ? (float)Math.Min(Math.Max(0.0, v), float.MaxValue) : 1.0f;
        }

        /// <summary>Corruption I-VII (5395-5401) + Incantation of Corruption (5402).</summary>
        public static bool IsCorruptionSpell(uint spellId) => spellId >= 5395 && spellId <= 5402;

        /// <summary>Destructive Curse VII (5337), Incantation (5338), I-VI (5339-5344).</summary>
        public static bool IsDestructiveCurseSpell(uint spellId) => spellId >= 5337 && spellId <= 5344;

        /// <summary>Same gate as the retail-vuln compression it replaces: a non-player, non-pet monster at endgame variation
        /// zc_vuln_min_variation (11) and up, with zc_vuln_enabled and zc_combat_rules_enabled on - and Zone Control itself on
        /// (RULING 1 "fully inert", CodeRabbit #539).</summary>
        public bool ZcDebuffCompressed =>
            !(this is Player) && !(this is CombatPet)
            && ServerConfig.zonecontrol_enabled.Value
            && ServerConfig.zc_vuln_enabled.Value && ServerConfig.zc_combat_rules_enabled.Value
            && VariationManager.GetEffectiveEndgameVariation(this) >= ACE.Server.Managers.VariationManager.EndgameGate(ServerConfig.zc_vuln_min_variation.Value);

        public const double DebuffDefaultFloor = 0.25;
        public const double DebuffDefaultCap = 0.50;
        public const double DebuffDefaultRamp = 500;

        /// <summary>The most any one debuff bonus can be (x11 damage) - far past any authored cap (0.5 default).</summary>
        private const double MaxDebuffBonus = 10.0;

        /// <summary>Incantation of X Vulnerability Other = x3.10 raw; a lower level gets its share of the bonus.</summary>
        private const double VulnIncantationRaw = 3.10;
        /// <summary>Incantation of Imperil Other = -225 armor raw.</summary>
        private const double ImperilIncantationRaw = 225.0;

        /// <summary>
        /// The vuln bonus for this resistance type and (withImperil) the Imperil bonus, as fractions (0.5 = +50 pct). Both 0 when
        /// the monster is not gated or carries no such debuff. The strongest entry of each kind counts (top layer per category,
        /// so a proc'd and a cast debuff of one kind never add up).
        /// </summary>
        public (float Vuln, float Imperil, float Dot) GetZcDebuffBonus(DamageType resistanceType, bool withImperil)
        {
            if (!ZcDebuffCompressed)
                return (0f, 0f, 0f);

            var zp = ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveCombatProfile(this);
            var tier = VariationManager.GetEffectiveEndgameVariation(this);
            double Get(string key, double fallback) => zp != null && zp.Has(key) ? zp.Get(key) : fallback;

            var entry = Get(ZoneStat.DebuffEntryLifeAugs, 2000 + 500 * Math.Max(0, tier - 11));
            var ramp = Math.Max(1.0, Get(ZoneStat.DebuffRampAugs, DebuffDefaultRamp));

            double vuln = 0;
            foreach (var e in EnchantmentManager.GetVulnerabilityEntries(resistanceType))
            {
                var share = SpellShare((uint)e.SpellId, ShareKind.Vuln);
                var b = Ramp(Get(ZoneStat.VulnBonusFloor, DebuffDefaultFloor), Get(ZoneStat.VulnBonusCap, DebuffDefaultCap), e.AugmentationLevelWhenCast ?? 0, entry, ramp) * share;
                if (b > vuln) vuln = b;
            }

            double imperil = 0;
            if (withImperil)
            {
                foreach (var e in EnchantmentManager.GetImperilEntries())
                {
                    var share = SpellShare((uint)e.SpellId, ShareKind.Imperil);
                    var b = Ramp(Get(ZoneStat.ImperilBonusFloor, DebuffDefaultFloor), Get(ZoneStat.ImperilBonusCap, DebuffDefaultCap), e.AugmentationLevelWhenCast ?? 0, entry, ramp) * share;
                    if (b > imperil) imperil = b;
                }
            }

            // NETHER DoTs (owner 2026-10-04): the strongest void DoT on the monster gives the vuln-like bonus, ramped on the
            // caster's VOID augs (EnchantmentManager.Add stamps them); a lower level spell gets its level's share. Any school.
            double dot = 0;
            var dotFlags = ACE.Entity.Enum.EnchantmentTypeFlags.Int | ACE.Entity.Enum.EnchantmentTypeFlags.SingleStat | ACE.Entity.Enum.EnchantmentTypeFlags.Additive;
            foreach (var e in EnchantmentManager.GetEnchantments_TopLayer(dotFlags, (uint)ACE.Entity.Enum.Properties.PropertyInt.NetherOverTime))
            {
                var share = SpellShare((uint)e.SpellId, ShareKind.Dot);
                var b = Ramp(Get(ZoneStat.DotBonusFloor, DebuffDefaultFloor), Get(ZoneStat.DotBonusCap, DebuffDefaultCap), e.AugmentationLevelWhenCast ?? 0, entry, ramp) * share;
                if (b > dot) dot = b;
            }

            // clamped: a zone floor / cap typed absurdly high would otherwise reach float infinity, and the Imperil ratio
            // (1 + v + d + i) / (1 + v + d) in DamageEvent would turn Inf / Inf = NaN
            return ((float)Math.Clamp(vuln, 0.0, MaxDebuffBonus), (float)Math.Clamp(imperil, 0.0, MaxDebuffBonus), (float)Math.Clamp(dot, 0.0, MaxDebuffBonus));
        }

        private static double Ramp(double floor, double cap, double lifeAugs, double entry, double ramp)
            => floor + (cap - floor) * Math.Clamp((lifeAugs - entry) / ramp, 0.0, 1.0);

        private enum ShareKind { Vuln, Imperil, Dot }

        /// <summary>Spell data never changes at runtime: each spell's share is worked out once (this runs on every hit).</summary>
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<(uint SpellId, ShareKind Kind), double> SpellShares = new();

        /// <summary>The spell level's share of the bonus (Incantation = 1.0), clamped 0..1; 1.0 when the spell is unknown.</summary>
        private static double SpellShare(uint spellId, ShareKind kind)
            => SpellShares.GetOrAdd((spellId, kind), k =>
            {
                var spell = new ACE.Server.Entity.Spell(k.SpellId);
                if (spell.NotFound) return 1.0;
                var share = k.Kind switch
                {
                    ShareKind.Vuln => (spell.StatModVal - 1.0) / (VulnIncantationRaw - 1.0),
                    ShareKind.Imperil => -spell.StatModVal / ImperilIncantationRaw,
                    _ => spell.Level / 8.0,
                };
                return Math.Clamp(share, 0.0, 1.0);
            });
    }
}
