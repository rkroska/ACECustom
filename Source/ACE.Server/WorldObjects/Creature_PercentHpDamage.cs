using System;

using ACE.Common;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        // ────────────────────────────────────────────────────────────────────────────────────────
        // Zone Control monster damage that defenses do not cut (owner 2026-10-01):
        // - TRUE DAMAGE (the main mechanic): a fixed amount added to every landed monster hit after all
        //   mitigation. Only life augs (incl. Triune Weave) reduce it, through the aug relief curve.
        // - %HP FLOOR (an option): a hit does at least a share of the player's max health. Off unless a
        //   zone authors percent_hp_base (or the server sets zc_pcthp_base).
        // Relief curves (2026-07-27, owner design): each axis is a straight line - 0% reduction at start,
        // rising to cap at max, clamped both ends. Anchors come from the attacker's zone profile (relief_*
        // stats) else the zc_relief_* server defaults. The %HP floor takes life augs x Damage Resist
        // (GetZoneReliefMultiplier); True Damage takes life augs only (GetZoneAugReliefMultiplier); Crit
        // Damage Resist shrinks only the crit BONUS of either (GetZoneCritBonusRelief).

        /// <summary>Relief curve: 0 at/below start, cap at/above max. Between them relief =
        /// cap * t^bend (t = progress 0-1): bend 1 = straight line, &lt;1 = strong early relief
        /// that tapers off, &gt;1 = slow start that ramps late.</summary>
        public static double GetLinearRelief(double value, double start, double max, double cap, double bend = 1.0)
        {
            cap = Math.Clamp(cap, 0.0, 1.0);
            if (cap <= 0.0 || value <= start)
                return 0.0;
            if (max <= start)
                return cap;
            var t = Math.Min(1.0, (value - start) / (max - start));
            if (bend > 0.0 && Math.Abs(bend - 1.0) > 0.0001)
                t = Math.Pow(t, bend);
            return Math.Min(cap, cap * t);
        }

        private static double ReliefAnchor(ACE.Server.Managers.ZoneScaling.EvaluatedProfile zp, string stat, double dflt)
            => zp != null && zp.Has(stat) ? zp.Get(stat) : dflt;

        // Mid-point stat keys per axis, as (x,y) pairs in slot order. Precomputed so the per-hit
        // path never builds key strings.
        private static readonly string[] AugPointKeys =
        {
            ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefAugX1, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefAugY1,
            ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefAugX2, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefAugY2,
            ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefAugX3, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefAugY3,
            ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefAugX4, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefAugY4,
        };
        private static readonly string[] DrPointKeys =
        {
            ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefDrX1, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefDrY1,
            ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefDrX2, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefDrY2,
            ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefDrX3, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefDrY3,
            ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefDrX4, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefDrY4,
        };
        private static readonly string[] CritDrPointKeys =
        {
            ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefCritDrX1, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefCritDrY1,
            ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefCritDrX2, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefCritDrY2,
            ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefCritDrX3, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefCritDrY3,
            ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefCritDrX4, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefCritDrY4,
        };

        /// <summary>
        /// One relief axis with optional zone-authored mid-points ("multiple bends"): any defined
        /// (x,y) pairs REPLACE the bend shape - the curve runs piecewise-linear through
        /// (start,0) -> sorted points -> (max,cap). No points = the bend curve.
        /// </summary>
        private static double GetAxisRelief(ACE.Server.Managers.ZoneScaling.EvaluatedProfile zp, string[] ptKeys,
            double value, double start, double max, double cap, double bend)
        {
            cap = Math.Clamp(cap, 0.0, 1.0);
            if (cap <= 0.0 || value <= start)
                return 0.0;
            if (max <= start || value >= max)
                return cap;

            if (zp != null)
            {
                Span<double> px = stackalloc double[4];
                Span<double> py = stackalloc double[4];
                var n = 0;
                for (int i = 0; i < ptKeys.Length; i += 2)
                {
                    if (!zp.Has(ptKeys[i]) || !zp.Has(ptKeys[i + 1]))
                        continue;
                    var x = zp.Get(ptKeys[i]);
                    if (x <= start || x >= max)
                        continue;   // out-of-range points are author errors - ignore
                    px[n] = x;
                    py[n] = Math.Clamp(zp.Get(ptKeys[i + 1]), 0.0, 1.0);
                    n++;
                }
                if (n > 0)
                {
                    for (int i = 1; i < n; i++)              // insertion sort by x (n <= 4)
                        for (int j = i; j > 0 && px[j] < px[j - 1]; j--)
                        {
                            (px[j], px[j - 1]) = (px[j - 1], px[j]);
                            (py[j], py[j - 1]) = (py[j - 1], py[j]);
                        }

                    var ax = start;
                    var ay = 0.0;
                    for (int i = 0; i < n; i++)
                    {
                        if (value <= px[i])
                            return ay + (py[i] - ay) * (value - ax) / Math.Max(1e-9, px[i] - ax);
                        ax = px[i];
                        ay = py[i];
                    }
                    return ay + (cap - ay) * (value - ax) / Math.Max(1e-9, max - ax);
                }
            }

            return GetLinearRelief(value, start, max, cap, bend);
        }

        /// <summary>
        /// Life-aug relief alone (True Damage): 1.0 = no relief, (1 - augCap) at the cap. Counts Triune Weave with
        /// the life augs (owner 2026-10-01: Triune is the only way to raise augs past the caps - always included).
        /// </summary>
        public static double GetZoneAugReliefMultiplier(Creature attacker, Player defender, ACE.Server.Managers.ZoneScaling.EvaluatedProfile zp)
        {
            if (attacker == null || defender == null)
                return 1.0;
            return 1.0 - GetAugRelief(zp, defender);
        }

        private static double GetAugRelief(ACE.Server.Managers.ZoneScaling.EvaluatedProfile zp, Player defender) =>
            GetAxisRelief(zp, AugPointKeys, defender.EffectiveLifeAugCount,
                ReliefAnchor(zp, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefAugStart, ServerConfig.zc_relief_aug_start.Value),
                ReliefAnchor(zp, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefAugMax, ServerConfig.zc_relief_aug_max.Value),
                ReliefAnchor(zp, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefAugCap, ServerConfig.zc_relief_aug_cap.Value),
                ReliefAnchor(zp, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefAugBend, ServerConfig.zc_relief_aug_bend.Value));

        /// <summary>
        /// Combined life-aug x Damage-Resist relief multiplier for the %HP floor against this
        /// player (1.0 = no relief, floor at (1-augCap)*(1-drCap) = never immune).
        /// </summary>
        public static double GetZoneReliefMultiplier(Creature attacker, Player defender)
            => GetZoneReliefMultiplier(attacker, defender, ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveForCreature(attacker));

        /// <summary>Same, with the attacker's zone profile already resolved by the caller (one resolve per hit).</summary>
        public static double GetZoneReliefMultiplier(Creature attacker, Player defender, ACE.Server.Managers.ZoneScaling.EvaluatedProfile zp)
        {
            if (attacker == null || defender == null)
                return 1.0;

            // PER-CREATURE merged profile since 2026-08-29 (release audit blocker 4). This read used
            // the zone-Default resolve on the stale theory that "a per-WCID override REPLACES the
            // whole default profile" - the merge has been PER-STAT since 2026-07-30, so an override
            // mob inherits the zone's curves unless it authors its own, and authoring relief_* on a
            // --wcid bucket (a boss that respects player DR less, say) now actually works instead of
            // being silently ignored while the sim and the display both claimed it did.

            var augRelief = GetAugRelief(zp, defender);   // life augs incl. Triune Weave (owner 2026-10-01)

            // aggregate rating (gear + enchantments + augs + lum augs) — the 400-750 scale the owner tunes by
            var drRelief = GetAxisRelief(zp, DrPointKeys, defender.GetDamageResistRating(null),
                ReliefAnchor(zp, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefDrStart, ServerConfig.zc_relief_dr_start.Value),
                ReliefAnchor(zp, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefDrMax, ServerConfig.zc_relief_dr_max.Value),
                ReliefAnchor(zp, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefDrCap, ServerConfig.zc_relief_dr_cap.Value),
                ReliefAnchor(zp, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefDrBend, ServerConfig.zc_relief_dr_bend.Value));

            return (1.0 - augRelief) * (1.0 - drRelief);
        }

        /// <summary>
        /// Fraction of a crit's BONUS damage that remains against this player's Crit Damage Resist
        /// (1.0 = full bonus; at the cap a 2x crit lands as 1.5x).
        /// </summary>
        public static double GetZoneCritBonusRelief(Creature attacker, Player defender)
            => GetZoneCritBonusRelief(attacker, defender, ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveForCreature(attacker));

        /// <summary>Same, with the attacker's zone profile already resolved by the caller (one resolve per hit).</summary>
        public static double GetZoneCritBonusRelief(Creature attacker, Player defender, ACE.Server.Managers.ZoneScaling.EvaluatedProfile zp)
        {
            if (attacker == null || defender == null)
                return 1.0;

            // per-creature merged profile — same 2026-08-29 fix as GetZoneReliefMultiplier

            var relief = GetAxisRelief(zp, CritDrPointKeys, defender.GetCritDamageResistRating(),
                ReliefAnchor(zp, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefCritDrStart, ServerConfig.zc_relief_critdr_start.Value),
                ReliefAnchor(zp, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefCritDrMax, ServerConfig.zc_relief_critdr_max.Value),
                ReliefAnchor(zp, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefCritDrCap, ServerConfig.zc_relief_critdr_cap.Value),
                ReliefAnchor(zp, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefCritDrBend, ServerConfig.zc_relief_critdr_bend.Value));

            return 1.0 - relief;
        }

        public static float GetPercentHpFloorDamage(Creature attacker, Player defender, bool isCrit = false)
        {
            if (attacker == null || defender == null)
                return 0f;

            if (!ServerConfig.zc_pcthp_enabled.Value)
                return 0f;

            var variation = VariationManager.GetEffectiveEndgameVariation(attacker);
            var minVariation = ACE.Server.Managers.VariationManager.EndgameGate(ServerConfig.zc_pcthp_min_variation.Value);

            // floor fraction P: per-weenie override wins; otherwise tier-scaled (+ boss multiplier)
            double p;
            var pOverride = attacker.GetProperty(PropertyFloat.PercentHpDamageOverride);
            var zoneProfile = ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveForCreature(attacker);

            // Gate: endgame monsters (variation >= min, while zc_combat_rules_enabled is on - zc_pcthp_base defaults to
            // 0 since 2026-10-01, so this path deals nothing unless the server sets it) OR a controlled area that
            // authored percent_hp_base. Retail mobs with no such area keep the old behavior (no %HP floor).
            var variationEligible = ServerConfig.zc_combat_rules_enabled.Value && variation >= minVariation;
            if (!variationEligible
                && !(zoneProfile != null && zoneProfile.Has(ACE.Server.Managers.ZoneScaling.ZoneStat.PercentHpBase)))
                return 0f;
            if (pOverride.HasValue)
            {
                p = pOverride.Value;
            }
            else if (zoneProfile != null && zoneProfile.Has(ACE.Server.Managers.ZoneScaling.ZoneStat.PercentHpBase))
            {
                // Zone profile value is per-variant (boss/minion) and per-tier already -> use directly, no boss mult.
                p = zoneProfile.Get(ACE.Server.Managers.ZoneScaling.ZoneStat.PercentHpBase);
            }
            else
            {
                // geometric growth per tier so the base outpaces the (accelerating) life-aug reduction -> rising endgame
                p = ServerConfig.zc_pcthp_base.Value
                    * Math.Pow(ServerConfig.zc_pcthp_tier_growth.Value, variation - minVariation);

                if (attacker.GetProperty(PropertyBool.IsEmpowerSource) == true)
                    p *= ServerConfig.zc_pcthp_boss_mult.Value;
            }

            if (p <= 0.0)
                return 0f;

            var maxHealth = defender.Health?.MaxValue ?? 0;
            if (maxHealth == 0)
                return 0f;

            // relief curves (2026-07-27): linear life-aug + gear Damage-Resist reduction replaces
            // the old exponential aug-only curve (zc_pcthp_aug_threshold/reduction_r/reduction_cap and
            // the per-weenie PercentHpReduction*Override props retired - no longer read here).
            var floor = p * maxHealth * GetZoneReliefMultiplier(attacker, defender, zoneProfile);

            // Crit multiplies the floor too — otherwise it vanishes once the floor dominates
            // the (heavily mitigated) normal damage component. (The old Empower floor mult was
            // REMOVED 2026-08-02 with the rest of the empowered-boss damage bonuses - dead system.)

            if (isCrit)
            {
                // zone-authored crit_damage_rating IS the final crit multiplier - it governs the floor's
                // crit too, so "Crit Damage 4" means 4x floor crits, not just the normal path.
                // The defender's Crit Damage Resist shrinks only the BONUS part (relief curve).
                var critMult = ServerConfig.zc_pcthp_crit_mult.Value;
                if (zoneProfile != null && zoneProfile.Has(ACE.Server.Managers.ZoneScaling.ZoneStat.CritDamageRating))
                    critMult = zoneProfile.Get(ACE.Server.Managers.ZoneScaling.ZoneStat.CritDamageRating);
                floor *= 1.0 + (critMult - 1.0) * GetZoneCritBonusRelief(attacker, defender, zoneProfile);
            }

            // per-hit random spread so damage isn't identical every swing
            var variance = ServerConfig.zc_pcthp_variance.Value;
            if (variance > 0.0)
                floor *= 1.0 + ThreadSafeRandom.Next((float)-variance, (float)variance);
            if (floor <= 0.0 || double.IsNaN(floor) || double.IsInfinity(floor))
                return 0f;

            return (float)floor;
        }

        /// <summary>
        /// TRUE DAMAGE (owner 2026-10-01, the main Zone Control monster damage): the zone's true_damage amount, added to a
        /// LANDED hit after every defense step - armor, resists, Damage Resist, shields, Crit Damage Resist on the normal
        /// part, the cloak proc and Mana Barrier never touch it. Only life augs (incl. Triune Weave) reduce it, through
        /// the aug relief curve. A crit multiplies it like the %HP floor: the zone's crit_damage_rating, else
        /// zc_true_damage_crit_mult, with Crit Damage Resist shrinking only the bonus. Then +/- true_damage_variance.
        /// 0 = the attacker's zone authors no true_damage (or the inputs are invalid).
        /// </summary>
        public static float GetTrueDamage(Creature attacker, Player defender, bool isCrit, bool isSpell = false)
        {
            if (attacker == null || defender == null || attacker is Player)
                return 0f;

            var zp = ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveForCreature(attacker);
            // a spell uses true_damage_spell when the tier authors it (owner 2026-10-02: spells ~3x a melee hit), else true_damage
            var amountStat = isSpell && zp != null && zp.Has(ACE.Server.Managers.ZoneScaling.ZoneStat.TrueDamageSpell)
                ? ACE.Server.Managers.ZoneScaling.ZoneStat.TrueDamageSpell : ACE.Server.Managers.ZoneScaling.ZoneStat.TrueDamage;
            if (zp == null || !zp.Has(amountStat))
                return 0f;
            var amount = zp.Get(amountStat);
            if (amount <= 0.0)
                return 0f;

            amount *= GetZoneAugReliefMultiplier(attacker, defender, zp);

            if (isCrit)
            {
                var critMult = zp.Has(ACE.Server.Managers.ZoneScaling.ZoneStat.CritDamageRating)
                    ? zp.Get(ACE.Server.Managers.ZoneScaling.ZoneStat.CritDamageRating)
                    : ServerConfig.zc_true_damage_crit_mult.Value;
                amount *= 1.0 + (critMult - 1.0) * GetZoneCritBonusRelief(attacker, defender, zp);
            }

            var variance = isSpell && zp.Has(ACE.Server.Managers.ZoneScaling.ZoneStat.TrueDamageSpellVariance)
                ? zp.Get(ACE.Server.Managers.ZoneScaling.ZoneStat.TrueDamageSpellVariance)
                : zp.Has(ACE.Server.Managers.ZoneScaling.ZoneStat.TrueDamageVariance)
                ? zp.Get(ACE.Server.Managers.ZoneScaling.ZoneStat.TrueDamageVariance)
                : ServerConfig.zc_true_damage_variance.Value;
            variance = Math.Clamp(variance, 0.0, 1.0);
            if (variance > 0.0)
                amount *= 1.0 + ThreadSafeRandom.Next((float)-variance, (float)variance);

            if (amount <= 0.0 || double.IsNaN(amount) || double.IsInfinity(amount))
                return 0f;
            return (float)Math.Min(amount, float.MaxValue);   // a finite double above float range would cast to +Infinity
        }

        /// <summary>
        /// v11+ attack-skill floor: the minimum effective attack skill a monster uses against a PLAYER
        /// defender, so endgame mobs can land hits against very high Effective Melee/Missile Defense.
        /// Variation >= zc_pcthp_min_variation uses the zc_min_attack_skill config, while zc_combat_rules_enabled is on. (The zone min_attack_skill stat was REMOVED 2026-08-02
        /// — redundant with attack_skill's absolute replace; zones tune accuracy via attack_skill.)
        /// Returns 0 when it doesn't apply, in which case callers keep the monster's normal attack skill.
        /// </summary>
        public static uint GetZoneAttackSkillFloor(Creature attacker, Player defender)
        {
            if (attacker == null || defender == null)
                return 0;

            // Variation-triggered path - fully off with zc_combat_rules_enabled.
            if (!ServerConfig.zc_combat_rules_enabled.Value)
                return 0;

            var floor = ServerConfig.zc_min_attack_skill.Value;
            if (floor <= 0)
                return 0;

            var variation = VariationManager.GetEffectiveEndgameVariation(attacker);
            if (variation < ACE.Server.Managers.VariationManager.EndgameGate(ServerConfig.zc_pcthp_min_variation.Value))
                return 0;

            return (uint)floor;
        }
    }
}
