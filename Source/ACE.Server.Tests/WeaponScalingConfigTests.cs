using System.Linq;

using ACE.Server.Managers.WeaponScaling;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Newtonsoft.Json;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Weapon aug-scaling config (T11_WeaponRelevance_Plan_2026-07-31.md): the locked launch
    /// defaults, the quality->k lerp the combat wire-in will lean on, and store round-trip safety.
    /// Pure config/math only — no DB, no combat (the wire-in ships in a later step).
    /// </summary>
    [TestClass]
    public class WeaponScalingConfigTests
    {
        // ── Locked defaults (plan §4 / §7) ──

        [TestMethod]
        public void Defaults_SystemStartsDisabled()
        {
            Assert.IsFalse(WeaponScalingManager.BuildDefaults().Enabled,
                "The master switch must default OFF — deploy-cold guarantee.");
        }

        [TestMethod]
        public void Defaults_TierLadder_CapAndWieldFloorRule()
        {
            var cfg = WeaponScalingManager.BuildDefaults();

            var t11 = cfg.Tiers.Single(t => t.Tier == 11);
            Assert.AreEqual(2500, t11.Cap);
            Assert.AreEqual(2000, t11.MinWieldAugs,
                "T11's floor = the pre-existing live gate (ZoneLootSetWieldItemAugs), not 0.");

            // +500 cap per tier; each tier's wield floor = previous tier's cap (the economy joint:
            // your T(n) weapon plateaus at exactly the count where T(n+1) becomes wieldable).
            for (var tier = 12; tier <= 25; tier++)
            {
                var row = cfg.Tiers.Single(t => t.Tier == tier);
                var prev = cfg.Tiers.Single(t => t.Tier == tier - 1);
                Assert.AreEqual(prev.Cap + 500, row.Cap, $"T{tier} cap");
                Assert.AreEqual(prev.Cap, row.MinWieldAugs, $"T{tier} minWieldAugs");
            }
        }

        [TestMethod]
        public void Defaults_LockedKRanges()
        {
            var cfg = WeaponScalingManager.BuildDefaults();

            // One row per weapon family, weight subtypes merged; k EQUAL WITHIN MECHANICS GROUPS
            // (owner 2026-08-01) — the discount rule follows what the weapon GIVES UP: multi-strike
            // gives up nothing -> discounted by strike count for per-swing parity; two-handed gives
            // up a SHIELD -> NO discount (k = singles per hit; both strikes carry it = the premium).
            Assert.AreEqual(16, cfg.Scripts.Count);
            var expected = new (string Key, double KMin, double KMax)[]
            {
                ("sword", 0.90, 1.15), ("axe", 0.90, 1.15), ("dagger", 0.90, 1.15),
                ("mace", 0.90, 1.15), ("spear", 0.90, 1.15), ("staff", 0.90, 1.15),
                ("unarmed", 0.90, 1.15),
                ("sword_ms", 0.40, 0.51), ("dagger_ms", 0.40, 0.51),
                ("cleaver", 0.90, 1.15), ("two_handed_spear", 0.90, 1.15),
                // Launchers: kMin/kMax = the EFFECTIVE DAMAGE MODIFIER band (replace semantics,
                // owner 2026-08-01), not a flat-term coefficient. Band FINAL 2026-08-02: S = 4.00
                // (owner pick), KMin = 4.00 x (0.90/1.15) = 3.13 so grades ladder at the same
                // F->S ratio as melee; floor still clears the real T10 roll ceiling (3.08).
                ("bow", 3.13, 4.00), ("crossbow", 3.13, 4.00), ("atlatl", 3.13, 4.00),
                // Casters: same reinterpretation, on ELEMENTAL damage mod (owner 2026-08-06,
                // "keep bow and caster weapons scaling identical"). S = 1.58 = the real T10
                // ceiling 1.22 x the launcher's own +29.9 pct bump; KMin = 1.58 x (0.90/1.15).
                // NON-elemental stays on the old band ON PURPOSE — a plain caster has no element
                // to match, so its modifier is pinned at 1.0 and nothing there can scale.
                ("caster_elemental", 1.24, 1.58), ("caster_non_elemental", 0.90, 1.15),
            };
            foreach (var e in expected)
            {
                Assert.AreEqual(e.KMin, cfg.Scripts[e.Key].KMin, 1e-9, e.Key);
                Assert.AreEqual(e.KMax, cfg.Scripts[e.Key].KMax, 1e-9, e.Key);
            }
            Assert.AreEqual(0.60, cfg.KcMin, 1e-9);
            Assert.AreEqual(0.80, cfg.KcMax, 1e-9);
        }

        // ── Grade ladder (owner 2026-08-03, WeaponGradeLadder_Plan) ──

        [TestMethod]
        public void Ladder_SeededOnMeleeFamiliesOnly()
        {
            var cfg = WeaponScalingManager.BuildDefaults();

            foreach (var melee in new[] { "sword", "axe", "dagger", "mace", "spear", "staff",
                                          "unarmed", "cleaver", "two_handed_spear", "sword_ms", "dagger_ms" })
                Assert.IsTrue(cfg.Scripts[melee].HasLadder, $"{melee} should resolve off the authored ladder.");

            // Launchers resolve their rows as a damage MOD band and casters are inert — both stay
            // on the KMin/KMax lerp until the missile/magic passes.
            foreach (var lerp in new[] { "bow", "crossbow", "atlatl", "caster_elemental", "caster_non_elemental" })
                Assert.IsFalse(cfg.Scripts[lerp].HasLadder, $"{lerp} must stay on the lerp for now.");
        }

        // ── Launcher tier scaling (owner 2026-08-06, LauncherTierMod_Plan) ──
        //
        // Replaced the missile aug cap, which produced bow tier growth by clawing BACK over-cap
        // Blood Drinker and so froze a T11 bow user's BD contribution no matter how many item
        // augs they bought. BD is never touched now; T11 is the baseline and tiers only ADD.

        [TestMethod]
        public void LauncherTierSteps_TrackMinAugsAndCap_InheritingMeleesDeadZone()
        {
            var cfg = WeaponScalingManager.BuildDefaults();
            WeaponScalingTier Row(int t) => cfg.Tiers.Single(x => x.Tier == t);

            // A 3,500-aug player. Each tier whose cap they can actually FILL adds a step, and the
            // count freezes the moment a tier's cap climbs past their augs.
            Assert.AreEqual(0, WeaponScalingCombat.LauncherTierSteps(cfg, Row(11), 3500));
            Assert.AreEqual(1, WeaponScalingCombat.LauncherTierSteps(cfg, Row(12), 3500));
            Assert.AreEqual(2, WeaponScalingCombat.LauncherTierSteps(cfg, Row(13), 3500));
            Assert.AreEqual(2, WeaponScalingCombat.LauncherTierSteps(cfg, Row(14), 3500));
            Assert.AreEqual(2, WeaponScalingCombat.LauncherTierSteps(cfg, Row(25), 3500),
                "A T25 bow in 3,500-aug hands must be worth exactly what a T13 is — the same way a "
                + "T25 melee weapon already is, both sharing min(augs, cap). A tier you cannot fill "
                + "is worth nothing in either lane (owner: 'Bows should track min(augs, cap) like "
                + "melee does').");
        }

        [TestMethod]
        public void LauncherTierSteps_BaselineTierNeverAdds_AtAnyAugCount()
        {
            var cfg = WeaponScalingManager.BuildDefaults();
            var t11 = cfg.Tiers.Single(x => x.Tier == 11);

            foreach (var augs in new[] { 0, 2000, 2500, 9500, 100000 })
                Assert.AreEqual(0, WeaponScalingCombat.LauncherTierSteps(cfg, t11, augs),
                    "T11 is the baseline and must stay 1.0x at every aug count. This is WHY the "
                    + "bow/melee gap at T11 (melee ~20-25 pct ahead, the owner's stated target) "
                    + "cannot be tuned with LauncherTierStep — that needs a kMin/kMax edit.");
        }

        [TestMethod]
        public void LauncherTierSteps_AtOwnCap_IsDistanceFromBaselineTier()
        {
            var cfg = WeaponScalingManager.BuildDefaults();

            // The progression case: a player whose augs sit at their tier's cap unlocks every step.
            foreach (var row in cfg.Tiers)
                Assert.AreEqual(row.Tier - 11, WeaponScalingCombat.LauncherTierSteps(cfg, row, row.Cap),
                    $"T{row.Tier} at its own cap");
        }

        [TestMethod]
        public void LauncherTierSteps_CountsAuthoredCaps_NotTheSeeds500Spacing()
        {
            // Caps are hand-authored and the store is authoritative — the live grade weights
            // already differ from the code seed — so the step count must follow the rows AS
            // WRITTEN. Deliberately irregular spacing here.
            var json = @"{""Enabled"":true,
                ""Tiers"":[{""Tier"":11,""Cap"":2500,""MinWieldAugs"":2000},
                           {""Tier"":12,""Cap"":9000,""MinWieldAugs"":2500},
                           {""Tier"":13,""Cap"":9400,""MinWieldAugs"":9000}],
                ""Scripts"":{""bow"":{""KMin"":3.13,""KMax"":4.00}},
                ""KcMin"":0.6,""KcMax"":0.8}";
            var cfg = WeaponScalingManager.Normalize(JsonConvert.DeserializeObject<WeaponScalingConfig>(json));
            var t13 = cfg.Tiers.Single(t => t.Tier == 13);

            Assert.AreEqual(0, WeaponScalingCombat.LauncherTierSteps(cfg, t13, 8999));
            Assert.AreEqual(1, WeaponScalingCombat.LauncherTierSteps(cfg, t13, 9000));
            Assert.AreEqual(2, WeaponScalingCombat.LauncherTierSteps(cfg, t13, 9400));
        }

        [TestMethod]
        public void LauncherTierStep_DefaultsOnOlderStores_ButAnExplicitZeroDisablesIt()
        {
            // Tuned on T11-T15, the tiers players are actually in — NOT on the T25 asymptote
            // (owner 2026-08-06: "T25 is years away, don't need to plan around that").
            Assert.AreEqual(0.06, WeaponScalingManager.BuildDefaults().LauncherTierStep, 1e-9);

            var legacy = @"{""Enabled"":true,""Tiers"":[],""Scripts"":{},""KcMin"":0.6,""KcMax"":0.8}";
            Assert.AreEqual(0.06, WeaponScalingManager.Normalize(
                    JsonConvert.DeserializeObject<WeaponScalingConfig>(legacy)).LauncherTierStep, 1e-9,
                "A store written before this field existed must still get launcher tier scaling — "
                + "the live store predates it and would otherwise leave bows with no tier term.");

            var off = @"{""Enabled"":true,""LauncherTierStep"":0,""Tiers"":[],""Scripts"":{},""KcMin"":0.6,""KcMax"":0.8}";
            Assert.AreEqual(0.0, WeaponScalingManager.Normalize(
                    JsonConvert.DeserializeObject<WeaponScalingConfig>(off)).LauncherTierStep, 1e-9,
                "An explicit 0 is the off switch.");
        }

        // ── Caster scaling (owner 2026-08-06, CasterScaling_Plan) ──
        //
        // "Lets keep bow and caster weapons scaling identical. We will tune vs magic on mobs."
        // Same mechanism on ElementalDamageMod instead of DamageMod, own step knob so the two
        // lanes CAN be parted later without a code change.

        [TestMethod]
        public void CasterTierStep_DefaultsEqualToTheLauncherStep_SoBothLanesStartIdentical()
        {
            var cfg = WeaponScalingManager.BuildDefaults();

            Assert.AreEqual(cfg.LauncherTierStep, cfg.CasterTierStep, 1e-9,
                "The lanes must START identical (owner: 'keep bow and caster weapons scaling "
                + "identical'). The knob exists only so they can be parted DELIBERATELY later "
                + "('incase we want to tune caster different later') — never by drift.");
            Assert.AreEqual(0.06, cfg.CasterTierStep, 1e-9);
        }

        [TestMethod]
        public void CasterTierStep_DefaultsOnOlderStores_ButAnExplicitZeroDisablesIt()
        {
            // Same legacy-store contract the launcher step has: the LIVE store predates this
            // field, so a missing value must inherit the default rather than silently zero out.
            var legacy = @"{""Enabled"":true,""Tiers"":[],""Scripts"":{},""KcMin"":0.6,""KcMax"":0.8}";
            Assert.AreEqual(0.06, WeaponScalingManager.Normalize(
                    JsonConvert.DeserializeObject<WeaponScalingConfig>(legacy)).CasterTierStep, 1e-9,
                "The live store was written before this field existed and would otherwise leave "
                + "casters with no tier term at all.");

            var off = @"{""Enabled"":true,""CasterTierStep"":0,""Tiers"":[],""Scripts"":{},""KcMin"":0.6,""KcMax"":0.8}";
            Assert.AreEqual(0.0, WeaponScalingManager.Normalize(
                    JsonConvert.DeserializeObject<WeaponScalingConfig>(off)).CasterTierStep, 1e-9,
                "An explicit 0 is the off switch, independently of the launcher lane.");

            var negative = @"{""Enabled"":true,""CasterTierStep"":-5,""Tiers"":[],""Scripts"":{},""KcMin"":0.6,""KcMax"":0.8}";
            Assert.AreEqual(0.0, WeaponScalingManager.Normalize(
                    JsonConvert.DeserializeObject<WeaponScalingConfig>(negative)).CasterTierStep, 1e-9,
                "Hand-edited negatives are repaired, not honored — a negative step would SHRINK "
                + "the mod as tiers rise.");
        }

        [TestMethod]
        public void CasterBand_PortsTheLauncherConstruction_OverTheRealT10Ceiling()
        {
            var cfg = WeaponScalingManager.BuildDefaults();
            var caster = cfg.Scripts["caster_elemental"];

            // Real T10 casters roll 1.20-1.22: every wield >= 500 falls through to
            // RollElementalDamageMod's "// 550" default, so T9/T10/T11 all roll the SAME value —
            // casters had no generational scaling whatsoever before this wire-in.
            const double t10CasterCeiling = 1.22;
            const double t10BowCeiling = 3.08;   // launcher reference, Handoff_2026-08-03
            const double bowKMax = 4.00;

            Assert.AreEqual(bowKMax / t10BowCeiling, caster.KMax / t10CasterCeiling, 5e-3,
                "S grade must clear the T10 ceiling by the SAME percentage in both lanes — that is "
                + "what 'identical scaling' means when the two lanes drive different properties "
                + "off different baselines. Both mods multiply their lane's whole damage "
                + "expression, so equal percentages ARE equal damage.");

            Assert.IsTrue(caster.KMin > t10CasterCeiling,
                $"Even the WORST T11 caster ({caster.KMin}) must beat the BEST T10 roll "
                + $"({t10CasterCeiling}) — the launcher floor 3.13 clears its 3.08 ceiling the "
                + "same way. Otherwise a T11 drop can be a downgrade.");
        }

        [TestMethod]
        public void CasterBand_KeepsTheSameFtoSSpreadAsEveryOtherFamily()
        {
            var cfg = WeaponScalingManager.BuildDefaults();
            var caster = cfg.Scripts["caster_elemental"];

            // The house ratio: kMin = kMax x 0.90/1.15, the melee F->S spread (+27.8 pct),
            // which the launcher band also follows.
            Assert.AreEqual(0.90 / 1.15, caster.KMin / caster.KMax, 5e-3,
                "A caster's grade must be worth the same relative jump as a bow's or a sword's.");
        }

        // ── Caster damage share (owner GO 2026-08-06, CasterDamageShare_Plan) ──
        //
        // "The plan needs to solve the same thing we solved for melee and bow. Making the
        // weapon itself contribute roughly 25-35% of total damage." Composition change:
        // modifier = wandMod x (1 + rescale x enchantments), replacing stock wandMod + ench.

        [TestMethod]
        public void CasterAuraRescale_IsTheReciprocalOfTheBandTop_SoSGradeAnchorsExactly()
        {
            var cfg = WeaponScalingManager.BuildDefaults();

            Assert.AreEqual(1.0 / cfg.Scripts["caster_elemental"].KMax, cfg.CasterAuraRescale, 1e-3,
                "r = 1/kMax is the anchor derivation: kMax x (1 + r x A) = kMax + A for EVERY "
                + "aura size A, so an S-grade wand's total is unchanged from the old additive "
                + "math at every aug count. Retune this only in step with kMax or the anchor "
                + "property breaks.");
        }

        [TestMethod]
        public void ComposeCasterModifier_SGradeMatchesTheOldAdditiveMath_AtEveryAuraSize()
        {
            var cfg = WeaponScalingManager.BuildDefaults();
            var kMax = cfg.Scripts["caster_elemental"].KMax;   // 1.58, the S-grade resolve at T11

            // Spirit Drinker at 3,500 / 2,000 / 0 item augs (+0.005 per aug), plus a no-buff case.
            foreach (var aura in new[] { 17.5, 10.0, 2.5, 0.0 })
            {
                var composed = WeaponScalingCombat.ComposeCasterModifier(kMax, aura, cfg.CasterAuraRescale);
                var stockAdditive = kMax + aura;
                Assert.AreEqual(stockAdditive, composed, 0.005,
                    $"aura={aura}: the migration must be INVISIBLE to the reference build (S "
                    + "grade, matching element). Everyone else re-grades around this anchor.");
            }
        }

        [TestMethod]
        public void ComposeCasterModifier_MakesTiersAndGradesFinallyMoveTheTotal()
        {
            var cfg = WeaponScalingManager.BuildDefaults();
            var s = cfg.Scripts["caster_elemental"].KMax;      // 1.58
            var fMinus = cfg.Scripts["caster_elemental"].KMin; // 1.24
            const double aura = 17.5;                          // 3,500-aug Spirit Drinker
            var r = cfg.CasterAuraRescale;

            // Under multiplicative composition the wandMod ratio IS the damage ratio.
            var t14 = WeaponScalingCombat.ComposeCasterModifier(s * 1.12, aura, r);   // 2 steps at 0.06
            var t11 = WeaponScalingCombat.ComposeCasterModifier(s, aura, r);
            Assert.AreEqual(1.12, t14 / t11, 1e-6,
                "T11 vs T14 measured ~1 pct apart in game under additive composition — the "
                + "owner's original complaint. Multiplicative composition must restore the full "
                + "+12 pct the tier steps grant.");

            Assert.AreEqual(s / fMinus, t11 / WeaponScalingCombat.ComposeCasterModifier(fMinus, aura, r), 1e-6,
                "S vs F- was +1.8 pct under additive (melee's same spread is 2.288x); now it is "
                + "the full band ratio 1.27x.");

            // The owner's stated target: the weapon carries 25-35 pct of total damage. Share =
            // what you lose dropping to a wandless/mod-1.0 baseline. S lands just above the
            // window (36.7 pct), F- just below (19.4) — same shape as melee, where better
            // weapons carry more.
            var share = 1.0 - WeaponScalingCombat.ComposeCasterModifier(1.0, aura, r) / t11;
            Assert.IsTrue(share > 0.30 && share < 0.40, $"S-grade weapon share {share:F3}");
        }

        [TestMethod]
        public void CasterAuraRescale_DefaultsOnOlderStores_AndNegativesAreRepaired()
        {
            // Same legacy-store contract as both tier steps: the live store predates the field.
            var legacy = @"{""Enabled"":true,""Tiers"":[],""Scripts"":{},""KcMin"":0.6,""KcMax"":0.8}";
            Assert.AreEqual(0.6329, WeaponScalingManager.Normalize(
                    JsonConvert.DeserializeObject<WeaponScalingConfig>(legacy)).CasterAuraRescale, 1e-9);

            var negative = @"{""Enabled"":true,""CasterAuraRescale"":-1,""Tiers"":[],""Scripts"":{},""KcMin"":0.6,""KcMax"":0.8}";
            Assert.AreEqual(0.0, WeaponScalingManager.Normalize(
                    JsonConvert.DeserializeObject<WeaponScalingConfig>(negative)).CasterAuraRescale, 1e-9,
                "A negative rescale would turn buffs into damage PENALTIES; repaired to 0 "
                + "(= enchantments contribute nothing while the system is on — wrong, but "
                + "visibly wrong instead of subtly inverted).");
        }

        [TestMethod]
        public void CasterNonElemental_StaysInert_BecauseItHasNoLeverToScale()
        {
            var cfg = WeaponScalingManager.BuildDefaults();

            // A plain Orb/Sceptre/Staff/Wand (the wield==0 loot branch) has no element, so
            // GetCasterElementalDamageModifier's damage-type match gate returns a flat 1.0 no
            // matter what we resolve. Scaling it would be theatre. Documented as inert here so a
            // future reader does not "fix" the band and expect an effect.
            Assert.AreNotEqual(cfg.Scripts["caster_elemental"].KMax,
                               cfg.Scripts["caster_non_elemental"].KMax,
                "caster_non_elemental is deliberately NOT on the tuned band — it cannot scale. "
                + "If plain casters should stop dropping, that is a LOOT change (they are 25 pct "
                + "of T10/T11 caster drops), not a scaling one.");
        }

        [TestMethod]
        public void Ladder_EveryStepIsEvenInDEALTDamage()
        {
            var cfg = WeaponScalingManager.BuildDefaults();

            // The rung is stored with EV normalization divided out, so evenness must be asserted
            // on k x EvNormalization — the quantity that actually reaches the damage envelope.
            // Asserting on raw k would pass a ladder that visibly steps +5.6 pct at the top and
            // +4.8 pct at the bottom, which is the exact defect this system removes.
            foreach (var kv in cfg.Scripts.Where(s => s.Value.HasLadder))
            {
                double? prev = null;
                foreach (var b in WeaponScalingManager.SubGradeBands)
                {
                    var k = kv.Value.Grades[b.Grade];
                    var vEff = WeaponScalingManager.EffectiveVariance(kv.Value.Variance, cfg.TightenStrength, b.QMid);
                    var dealt = k * WeaponScalingManager.EvNormalization(vEff);

                    if (prev.HasValue)
                        Assert.AreEqual(WeaponScalingManager.LadderStep, prev.Value / dealt, 1e-3,
                            $"{kv.Key} step into {b.Grade}");
                    prev = dealt;
                }
            }
        }

        [TestMethod]
        public void Ladder_FullGradeStepIs18Percent()
        {
            var cfg = WeaponScalingManager.BuildDefaults();
            var s = cfg.Scripts["unarmed"];

            double Dealt(string grade)
            {
                var b = WeaponScalingManager.SubGradeBands.Single(x => x.Grade == grade);
                return s.Grades[grade] * WeaponScalingManager.EvNormalization(
                    WeaponScalingManager.EffectiveVariance(s.Variance, cfg.TightenStrength, b.QMid));
            }

            Assert.AreEqual(1.18, Dealt("A") / Dealt("B"), 1e-3, "A -> B is one full grade");
            Assert.AreEqual(1.18, Dealt("B") / Dealt("C"), 1e-3, "B -> C is one full grade");
            // S vs B was the presenting complaint: +7.5 pct under the old lerp, +32 pct now.
            Assert.AreEqual(1.318, Dealt("S") / Dealt("B"), 5e-3, "S vs B");
        }

        [TestMethod]
        public void Ladder_InterpolatesBetweenRungs_RungsStayExact()
        {
            var cfg = WeaponScalingManager.BuildDefaults();
            var s = cfg.Scripts["unarmed"];

            // REPLACES Ladder_ResolvesFlatPerSubGrade_NoInterpolation (owner 2026-08-06: "those 2
            // weapons should not be identical damage"). k now interpolates BETWEEN rungs so every
            // distinct roll deals distinct damage and the appraisal percent means something.

            // 1. Each rung is still EXACT at its own sub-grade midpoint — the authored values are
            //    what the Damage Chart shows and what the owner tuned.
            foreach (var b in WeaponScalingManager.SubGradeBands)
                Assert.AreEqual(s.Grades[b.Grade], WeaponScalingManager.ResolveScriptK(s, b.QMid), 1e-12,
                    $"rung {b.Grade} must be exact at its midpoint");

            // 2. Inside a band, k now VARIES — this is the reversal.
            var atLow = WeaponScalingManager.ResolveScriptK(s, 867);
            var atMid = WeaponScalingManager.ResolveScriptK(s, 883);
            Assert.IsTrue(atMid > atLow, "a higher roll inside B+ must deal more than a lower one");

            // 3. ...and it is MONOTONIC across the whole range, with no discontinuity at any band
            //    edge (a jump there would let one quality point be worth a whole sub-grade).
            var prev = -1.0;
            for (var q = 0; q <= WeaponScalingManager.QualityMax; q++)
            {
                var k = WeaponScalingManager.ResolveScriptK(s, q);
                Assert.IsTrue(k >= prev - 1e-12, $"k must never decrease as quality rises (q{q})");
                prev = k;
            }

            // 4. Endpoints clamp to the S and F- rungs rather than running off the ladder.
            Assert.AreEqual(s.Grades["S"], WeaponScalingManager.ResolveScriptK(s, 1000), 1e-12);
            Assert.AreEqual(s.Grades["F-"], WeaponScalingManager.ResolveScriptK(s, 0), 1e-12);
        }

        [TestMethod]
        public void RelativeDamagePercent_MeasuresDamageNotQualityPercentile()
        {
            var cfg = WeaponScalingManager.BuildDefaults();
            var s = cfg.Scripts["unarmed"];

            int Pct(int q) => WeaponScalingManager.RelativeDamagePercent(s, cfg.TightenStrength, q);

            // A perfect roll is by definition 100 pct of max.
            Assert.AreEqual(100, Pct(1000));

            // The old display printed quality/10, so F- (q0) read "0 pct of max" while dealing a
            // large share of an S weapon's damage. The number must reflect DAMAGE.
            Assert.IsTrue(Pct(0) > 35 && Pct(0) < 50, $"F- deals a real share of max, got {Pct(0)} pct");

            // The two weapons that started this: q867 and q883 are both B+ and must now differ.
            Assert.AreNotEqual(Pct(867), Pct(883), "identical-looking B+ rolls must read differently");

            // Monotonic, and never above 100.
            var prev = -1;
            for (var q = 0; q <= WeaponScalingManager.QualityMax; q++)
            {
                var p = Pct(q);
                Assert.IsTrue(p >= prev, $"percent must not decrease as quality rises (q{q})");
                Assert.IsTrue(p <= 100, $"percent must never exceed 100 (q{q})");
                prev = p;
            }
        }

        [TestMethod]
        public void Ladder_LerpFamiliesStillUseKMinKMax()
        {
            var cfg = WeaponScalingManager.BuildDefaults();
            var bow = cfg.Scripts["bow"];

            Assert.AreEqual(4.00, WeaponScalingManager.ResolveScriptK(bow, 1000), 1e-9);
            Assert.AreEqual(3.13, WeaponScalingManager.ResolveScriptK(bow, 0), 1e-9);
            // Continuous, not stepped — proof the ladder path is genuinely bypassed.
            Assert.AreNotEqual(WeaponScalingManager.ResolveScriptK(bow, 500),
                               WeaponScalingManager.ResolveScriptK(bow, 600), 1e-6);
        }

        [TestMethod]
        public void SubGrade_BandLabelsAndEdges()
        {
            Assert.AreEqual("S", WeaponScalingManager.GetQualitySubGrade(1000));
            Assert.AreEqual("A+", WeaponScalingManager.GetQualitySubGrade(999));
            Assert.AreEqual("A+", WeaponScalingManager.GetQualitySubGrade(967));
            Assert.AreEqual("A", WeaponScalingManager.GetQualitySubGrade(966));
            Assert.AreEqual("A-", WeaponScalingManager.GetQualitySubGrade(900));
            Assert.AreEqual("B+", WeaponScalingManager.GetQualitySubGrade(899));
            Assert.AreEqual("B-", WeaponScalingManager.GetQualitySubGrade(800));
            Assert.AreEqual("C+", WeaponScalingManager.GetQualitySubGrade(799));
            Assert.AreEqual("D-", WeaponScalingManager.GetQualitySubGrade(500));
            Assert.AreEqual("F+", WeaponScalingManager.GetQualitySubGrade(499));
            Assert.AreEqual("F-", WeaponScalingManager.GetQualitySubGrade(0));

            // Sub-grades must TILE their parent grade exactly — a gap or overlap would mean a
            // drop rolled inside GradeWeights lands on a rung from a different letter.
            foreach (var g in WeaponScalingManager.GradeBands)
            {
                var subs = WeaponScalingManager.SubGradeBands.Where(b => b.Grade.TrimEnd('+', '-') == g.Grade).ToList();
                Assert.AreEqual(g.QMin, subs.Min(b => b.QMin), $"{g.Grade} sub-grades must start at the grade floor");
                Assert.AreEqual(g.QMax, subs.Max(b => b.QMax), $"{g.Grade} sub-grades must end at the grade ceiling");
            }
        }

        [TestMethod]
        public void Normalize_CompletesAPartialLadderFromTheLerp()
        {
            var cfg = new WeaponScalingConfig { TightenStrength = 0.7 };
            cfg.Scripts["x"] = new WeaponScalingScript
            {
                KMin = 0.40,
                KMax = 0.90,
                Grades = new System.Collections.Generic.Dictionary<string, double> { ["S"] = 0.90 },
            };

            WeaponScalingManager.Normalize(cfg);

            // A half-authored ladder would silently mix ladder rungs with lerp rungs.
            Assert.AreEqual(WeaponScalingManager.SubGradeBands.Length, cfg.Scripts["x"].Grades.Count);
            Assert.AreEqual(0.90, cfg.Scripts["x"].Grades["S"], 1e-9, "authored rung preserved");
        }

        [TestMethod]
        public void Normalize_EmptyLadderBecomesNoLadder()
        {
            var cfg = new WeaponScalingConfig();
            cfg.Scripts["x"] = new WeaponScalingScript
            {
                KMin = 0.40,
                KMax = 0.90,
                Grades = new System.Collections.Generic.Dictionary<string, double>(),
            };

            WeaponScalingManager.Normalize(cfg);

            Assert.IsFalse(cfg.Scripts["x"].HasLadder, "An empty dictionary must read as 'no ladder'.");
        }

        [TestMethod]
        public void RebaseLadder_HoldsDealtDamageFlatAcrossAVarianceEdit()
        {
            var cfg = WeaponScalingManager.BuildDefaults();
            var s = cfg.Scripts["unarmed"];
            const double oldVar = 0.55, newVar = 0.20;

            double Dealt(double variance, string grade)
            {
                var b = WeaponScalingManager.SubGradeBands.Single(x => x.Grade == grade);
                return s.Grades[grade] * WeaponScalingManager.EvNormalization(
                    WeaponScalingManager.EffectiveVariance(variance, cfg.TightenStrength, b.QMid));
            }

            var before = WeaponScalingManager.SubGradeBands.Select(b => Dealt(oldVar, b.Grade)).ToList();
            WeaponScalingManager.RebaseLadder(s, oldVar, cfg.TightenStrength, newVar, cfg.TightenStrength);
            var after = WeaponScalingManager.SubGradeBands.Select(b => Dealt(newVar, b.Grade)).ToList();

            // The owner's standing invariant: editing a Variance knob rebalances rather than
            // silently changing how hard the family hits.
            for (var i = 0; i < before.Count; i++)
                Assert.AreEqual(before[i], after[i], before[i] * 1e-3,
                    $"{WeaponScalingManager.SubGradeBands[i].Grade} dealt damage moved on a variance edit");
        }

        [TestMethod]
        public void Defaults_ScriptLookupIsCaseInsensitive()
        {
            var cfg = WeaponScalingManager.BuildDefaults();
            Assert.IsTrue(cfg.Scripts.ContainsKey("Sword"));
        }

        // ── Quality lerp (the swing-time resolve) ──

        [TestMethod]
        public void ResolveFromQuality_EndpointsAndMidpoint()
        {
            Assert.AreEqual(0.90, WeaponScalingManager.ResolveFromQuality(0.90, 1.15, 0), 1e-9);
            Assert.AreEqual(1.15, WeaponScalingManager.ResolveFromQuality(0.90, 1.15, 1000), 1e-9);
            Assert.AreEqual(1.025, WeaponScalingManager.ResolveFromQuality(0.90, 1.15, 500), 1e-9);
        }

        [TestMethod]
        public void ResolveFromQuality_OutOfRangeQualityClamps()
        {
            Assert.AreEqual(0.90, WeaponScalingManager.ResolveFromQuality(0.90, 1.15, -50), 1e-9);
            Assert.AreEqual(1.15, WeaponScalingManager.ResolveFromQuality(0.90, 1.15, 99999), 1e-9);
        }

        [TestMethod]
        public void QualityGrade_SchoolStyleBandsAndEdges()
        {
            // S = a literally perfect roll only (1 in 1,001); the rest are school-style.
            Assert.AreEqual("S", WeaponScalingManager.GetQualityGrade(1000));
            Assert.AreEqual("A", WeaponScalingManager.GetQualityGrade(999));
            Assert.AreEqual("A", WeaponScalingManager.GetQualityGrade(900));
            Assert.AreEqual("B", WeaponScalingManager.GetQualityGrade(899));
            Assert.AreEqual("B", WeaponScalingManager.GetQualityGrade(800));
            Assert.AreEqual("C", WeaponScalingManager.GetQualityGrade(799));
            Assert.AreEqual("C", WeaponScalingManager.GetQualityGrade(650));
            Assert.AreEqual("D", WeaponScalingManager.GetQualityGrade(649));
            Assert.AreEqual("D", WeaponScalingManager.GetQualityGrade(500));
            Assert.AreEqual("F", WeaponScalingManager.GetQualityGrade(499));
            Assert.AreEqual("F", WeaponScalingManager.GetQualityGrade(0));
        }

        // ── Normalize (hand-edited / legacy store repair) ──

        [TestMethod]
        public void Normalize_RepairsInvertedRangesAndNegatives()
        {
            var cfg = new WeaponScalingConfig
            {
                KcMin = 0.9,
                KcMax = 0.5,
            };
            cfg.Scripts["x"] = new WeaponScalingScript { KMin = 1.2, KMax = 0.8 };
            cfg.Tiers.Add(new WeaponScalingTier { Tier = 11, Cap = -5, MinWieldAugs = -1 });

            WeaponScalingManager.Normalize(cfg);

            Assert.AreEqual(0.5, cfg.KcMin, 1e-9);
            Assert.AreEqual(0.9, cfg.KcMax, 1e-9);
            Assert.AreEqual(0.8, cfg.Scripts["x"].KMin, 1e-9);
            Assert.AreEqual(1.2, cfg.Scripts["x"].KMax, 1e-9);
            Assert.AreEqual(0, cfg.Tiers[0].Cap);
            Assert.AreEqual(0, cfg.Tiers[0].MinWieldAugs);
        }

        [TestMethod]
        public void Normalize_DedupesTiersAndRestoresCaseInsensitivity()
        {
            var json = @"{""Enabled"":true,
                ""Tiers"":[{""Tier"":12,""Cap"":3000,""MinWieldAugs"":2500},{""Tier"":12,""Cap"":9999,""MinWieldAugs"":0},{""Tier"":11,""Cap"":2500,""MinWieldAugs"":0}],
                ""Scripts"":{""heavy_sword"":{""KMin"":0.9,""KMax"":1.15}},
                ""KcMin"":0.6,""KcMax"":0.8}";
            var cfg = WeaponScalingManager.Normalize(JsonConvert.DeserializeObject<WeaponScalingConfig>(json));

            Assert.AreEqual(2, cfg.Tiers.Count);
            Assert.AreEqual(11, cfg.Tiers[0].Tier, "tiers sort ascending");
            Assert.AreEqual(3000, cfg.Tiers.Single(t => t.Tier == 12).Cap, "first entry wins on dupes");
            Assert.IsTrue(cfg.Scripts.ContainsKey("HEAVY_SWORD"), "deserialized dictionary must regain case-insensitive lookup");
        }

        [TestMethod]
        public void Normalize_MigratesFlatEraLauncherRowsToModifierBand()
        {
            // Pre-2026-08-01 stores carry launcher rows as flat-term coefficients (~0.9-1.15);
            // under replace semantics those would resolve AS the damage modifier and ~quarter
            // launcher damage. Normalize must bump any launcher row entirely below 2.0 to the
            // modifier-band defaults, while respecting deliberate values >= 2.0.
            var cfg = new WeaponScalingConfig();
            cfg.Scripts["bow"] = new WeaponScalingScript { KMin = 0.90, KMax = 1.15 };
            cfg.Scripts["crossbow"] = new WeaponScalingScript { KMin = 0.40, KMax = 0.51 };
            cfg.Scripts["atlatl"] = new WeaponScalingScript { KMin = 2.80, KMax = 3.10 };
            cfg.Scripts["sword"] = new WeaponScalingScript { KMin = 0.90, KMax = 1.15 };

            WeaponScalingManager.Normalize(cfg);

            // Migration target follows the CURRENT band (retuned to 3.13/4.00 on 2026-08-02).
            Assert.AreEqual(3.13, cfg.Scripts["bow"].KMin, 1e-9);
            Assert.AreEqual(4.00, cfg.Scripts["bow"].KMax, 1e-9);
            Assert.AreEqual(3.13, cfg.Scripts["crossbow"].KMin, 1e-9);
            Assert.AreEqual(4.00, cfg.Scripts["crossbow"].KMax, 1e-9);
            Assert.AreEqual(2.80, cfg.Scripts["atlatl"].KMin, 1e-9, "deliberate >= 2.0 band must survive");
            Assert.AreEqual(3.10, cfg.Scripts["atlatl"].KMax, 1e-9);
            Assert.AreEqual(0.90, cfg.Scripts["sword"].KMin, 1e-9, "melee rows untouched");
            Assert.AreEqual(1.15, cfg.Scripts["sword"].KMax, 1e-9);
        }

        // ── Store round-trip ──

        [TestMethod]
        public void Store_JsonRoundTripPreservesEverything()
        {
            var cfg = WeaponScalingManager.BuildDefaults();
            cfg.Enabled = true;

            var back = WeaponScalingManager.Normalize(
                JsonConvert.DeserializeObject<WeaponScalingConfig>(JsonConvert.SerializeObject(cfg)));

            Assert.IsTrue(back.Enabled);
            Assert.AreEqual(cfg.Tiers.Count, back.Tiers.Count);
            Assert.AreEqual(cfg.Scripts.Count, back.Scripts.Count);
            foreach (var t in cfg.Tiers)
            {
                var bt = back.Tiers.Single(x => x.Tier == t.Tier);
                Assert.AreEqual(t.Cap, bt.Cap);
                Assert.AreEqual(t.MinWieldAugs, bt.MinWieldAugs);
            }
            foreach (var s in cfg.Scripts)
            {
                Assert.AreEqual(s.Value.KMin, back.Scripts[s.Key].KMin, 1e-9);
                Assert.AreEqual(s.Value.KMax, back.Scripts[s.Key].KMax, 1e-9);
            }
            Assert.AreEqual(cfg.KcMin, back.KcMin, 1e-9);
            Assert.AreEqual(cfg.KcMax, back.KcMax, 1e-9);
        }
    }
}
