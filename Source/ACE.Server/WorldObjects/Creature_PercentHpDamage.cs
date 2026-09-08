using System;

using ACE.Common;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        /// <summary>
        /// v11+ Percent-HP floor damage system.
        /// Returns the minimum damage (a fraction of the defender's max health) that a high-variation
        /// monster should deal to a player, bypassing the life-aug protection reduction that otherwise
        /// pushes normal damage toward zero. Callers apply this as a floor: finalDamage = Max(normal, floor).
        /// The floor itself is reduced by a separate, gentler life-aug curve (capped well below 100%).
        /// Returns 0 when the system is disabled, the attacker is below the variation threshold, or inputs are invalid.
        /// See T10/V11_PercentHP_Damage_Plan.md.
        /// </summary>
        // ────────────────────────────────────────────────────────────────────────────────────────
        // v11+ relief curves (2026-07-27, owner design): player progression vs AUTHORED endgame
        // damage. Each axis is a straight line: 0% reduction at start, rising to cap at max,
        // clamped both ends. Anchors come from the attacker's zone profile (relief_* stats) else
        // the v11_relief_* server defaults. Life augs + Damage Resist multiply into
        // GetV11ReliefMultiplier (applied to the %HP floor AND WYSIWYG spell_damage);
        // Crit Damage Resist shrinks only the crit BONUS via GetV11CritBonusRelief.

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
        /// Combined life-aug x Damage-Resist relief multiplier for authored v11 damage against this
        /// player (1.0 = no relief, floor at (1-augCap)*(1-drCap) = never immune).
        /// </summary>
        public static double GetV11ReliefMultiplier(Creature attacker, Player defender)
            => GetV11ReliefMultiplier(attacker, defender, ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveForCreature(attacker));

        /// <summary>Same, with the attacker's zone profile already resolved by the caller (one resolve per hit).</summary>
        public static double GetV11ReliefMultiplier(Creature attacker, Player defender, ACE.Server.Managers.ZoneScaling.EvaluatedProfile zp)
        {
            if (attacker == null || defender == null)
                return 1.0;

            // PER-CREATURE merged profile since 2026-08-29 (release audit blocker 4). This read used
            // the zone-Default resolve on the stale theory that "a per-WCID override REPLACES the
            // whole default profile" - the merge has been PER-STAT since 2026-07-30, so an override
            // mob inherits the zone's curves unless it authors its own, and authoring relief_* on a
            // --wcid bucket (a boss that respects player DR less, say) now actually works instead of
            // being silently ignored while the sim and the display both claimed it did.

            var augRelief = GetAxisRelief(zp, AugPointKeys, defender.LuminanceAugmentLifeCount ?? 0,
                ReliefAnchor(zp, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefAugStart, ServerConfig.v11_relief_aug_start.Value),
                ReliefAnchor(zp, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefAugMax, ServerConfig.v11_relief_aug_max.Value),
                ReliefAnchor(zp, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefAugCap, ServerConfig.v11_relief_aug_cap.Value),
                ReliefAnchor(zp, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefAugBend, ServerConfig.v11_relief_aug_bend.Value));

            // aggregate rating (gear + enchantments + augs + lum augs) — the 400-750 scale the owner tunes by
            var drRelief = GetAxisRelief(zp, DrPointKeys, defender.GetDamageResistRating(null),
                ReliefAnchor(zp, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefDrStart, ServerConfig.v11_relief_dr_start.Value),
                ReliefAnchor(zp, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefDrMax, ServerConfig.v11_relief_dr_max.Value),
                ReliefAnchor(zp, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefDrCap, ServerConfig.v11_relief_dr_cap.Value),
                ReliefAnchor(zp, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefDrBend, ServerConfig.v11_relief_dr_bend.Value));

            return (1.0 - augRelief) * (1.0 - drRelief);
        }

        /// <summary>
        /// Fraction of a crit's BONUS damage that remains against this player's Crit Damage Resist
        /// (1.0 = full bonus; at the cap a 2x crit lands as 1.5x).
        /// </summary>
        public static double GetV11CritBonusRelief(Creature attacker, Player defender)
            => GetV11CritBonusRelief(attacker, defender, ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveForCreature(attacker));

        /// <summary>Same, with the attacker's zone profile already resolved by the caller (one resolve per hit).</summary>
        public static double GetV11CritBonusRelief(Creature attacker, Player defender, ACE.Server.Managers.ZoneScaling.EvaluatedProfile zp)
        {
            if (attacker == null || defender == null)
                return 1.0;

            // per-creature merged profile — same 2026-08-29 fix as GetV11ReliefMultiplier

            var relief = GetAxisRelief(zp, CritDrPointKeys, defender.GetCritDamageResistRating(),
                ReliefAnchor(zp, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefCritDrStart, ServerConfig.v11_relief_critdr_start.Value),
                ReliefAnchor(zp, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefCritDrMax, ServerConfig.v11_relief_critdr_max.Value),
                ReliefAnchor(zp, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefCritDrCap, ServerConfig.v11_relief_critdr_cap.Value),
                ReliefAnchor(zp, ACE.Server.Managers.ZoneScaling.ZoneStat.ReliefCritDrBend, ServerConfig.v11_relief_critdr_bend.Value));

            return 1.0 - relief;
        }

        public static float GetPercentHpFloorDamage(Creature attacker, Player defender, bool isCrit = false)
        {
            if (attacker == null || defender == null)
                return 0f;

            if (!ServerConfig.v11_pcthp_enabled.Value)
                return 0f;

            var variation = VariationManager.GetEffectiveEndgameVariation(attacker);
            var minVariation = ServerConfig.v11_pcthp_min_variation.Value;

            // floor fraction P: per-weenie override wins; otherwise tier-scaled (+ boss multiplier)
            double p;
            var pOverride = attacker.GetProperty(PropertyFloat.PercentHpDamageOverride);
            var zoneProfile = ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveForCreature(attacker);

            // Gate: fire for prestige mobs (variation >= min, only while the prestige master switch is on)
            // OR a controlled area that authored percent_hp_base (Zone Control path — never gated by prestige).
            // Retail mobs with no such area keep the old behavior (no %HP floor).
            var prestigeEligible = PrestigeManager.SystemsEnabled && variation >= minVariation;
            if (!prestigeEligible
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
                p = ServerConfig.v11_pcthp_base.Value
                    * Math.Pow(ServerConfig.v11_pcthp_tier_growth.Value, variation - minVariation);

                if (attacker.GetProperty(PropertyBool.IsEmpowerSource) == true)
                    p *= ServerConfig.v11_pcthp_boss_mult.Value;
            }

            if (p <= 0.0)
                return 0f;

            var maxHealth = defender.Health?.MaxValue ?? 0;
            if (maxHealth == 0)
                return 0f;

            // v11+ relief curves (2026-07-27): linear life-aug + gear Damage-Resist reduction replaces
            // the old exponential aug-only curve (v11_pcthp_aug_threshold/reduction_r/reduction_cap and
            // the per-weenie PercentHpReduction*Override props retired - no longer read here).
            var floor = p * maxHealth * GetV11ReliefMultiplier(attacker, defender, zoneProfile);

            // Crit multiplies the floor too — otherwise it vanishes once the floor dominates
            // the (heavily mitigated) normal damage component. (The old Empower floor mult was
            // REMOVED 2026-08-02 with the rest of the empowered-boss damage bonuses - dead system.)

            if (isCrit)
            {
                // zone-authored crit_damage_rating IS the final crit multiplier - it governs the floor's
                // crit too, so "Crit Damage 4" means 4x floor crits, not just the normal path.
                // The defender's Crit Damage Resist shrinks only the BONUS part (relief curve).
                var critMult = ServerConfig.v11_pcthp_crit_mult.Value;
                if (zoneProfile != null && zoneProfile.Has(ACE.Server.Managers.ZoneScaling.ZoneStat.CritDamageRating))
                    critMult = zoneProfile.Get(ACE.Server.Managers.ZoneScaling.ZoneStat.CritDamageRating);
                floor *= 1.0 + (critMult - 1.0) * GetV11CritBonusRelief(attacker, defender, zoneProfile);
            }

            // per-hit random spread so damage isn't identical every swing
            var variance = ServerConfig.v11_pcthp_variance.Value;
            if (variance > 0.0)
                floor *= 1.0 + ThreadSafeRandom.Next((float)-variance, (float)variance);
            if (floor <= 0.0 || double.IsNaN(floor) || double.IsInfinity(floor))
                return 0f;

            return (float)floor;
        }

        /// <summary>
        /// v11+ attack-skill floor: the minimum effective attack skill a monster uses against a PLAYER
        /// defender, so endgame mobs can land hits against very high Effective Melee/Missile Defense.
        /// Prestige-only: variation >= v11_pcthp_min_variation uses the v11_min_attack_skill config,
        /// while prestige_systems_enabled is on. (The zone min_attack_skill stat was REMOVED 2026-08-02
        /// — redundant with attack_skill's absolute replace; zones tune accuracy via attack_skill.)
        /// Returns 0 when it doesn't apply, in which case callers keep the monster's normal attack skill.
        /// </summary>
        public static uint GetV11AttackSkillFloor(Creature attacker, Player defender)
        {
            if (attacker == null || defender == null)
                return 0;

            // Variation-triggered path — fully off with the prestige master switch.
            if (!PrestigeManager.SystemsEnabled)
                return 0;

            var floor = ServerConfig.v11_min_attack_skill.Value;
            if (floor <= 0)
                return 0;

            var variation = VariationManager.GetEffectiveEndgameVariation(attacker);
            if (variation < ServerConfig.v11_pcthp_min_variation.Value)
                return 0;

            return (uint)floor;
        }
    }
}
