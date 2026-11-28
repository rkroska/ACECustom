using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Managers.WeaponScaling;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Managers.ZoneScaling;
using ACE.Server.WorldObjects;

namespace ACE.Server.Tests
{
    /// <summary>Salvage Bags follow-up (2026-11-26): Second Thoughts rerolls the lowest property, Tempering steps the Weapon
    /// Grade one sub-grade, the Bag of Madness redesign and the over-max grades it writes. Pure helpers and in-memory items;
    /// the live ladder is never needed.</summary>
    [TestClass]
    public class GearEssencesFollowupTests
    {
        private static Clothing CreateArmor(string record)
        {
            var weenie = new Weenie { WeenieClassId = 424270, ClassName = "TestBagsFollowupHelm", WeenieType = WeenieType.Clothing };
            var wo = new Clothing(weenie, new ObjectGuid(0xF0004270));
            wo.SetProperty(PropertyString.ZcModifiers, record);
            return wo;
        }

        private static MeleeWeapon CreateWeapon(string record)
        {
            var weenie = new Weenie { WeenieClassId = 424271, ClassName = "TestBagsFollowupSword", WeenieType = WeenieType.MeleeWeapon };
            var wo = new MeleeWeapon(weenie, new ObjectGuid(0xF0004271));
            wo.SetProperty(PropertyString.ZcModifiers, record);
            return wo;
        }

        // ---- Second Thoughts: the lowest property ----

        [TestMethod]
        public void LowestIndex_PicksTheMinimum()
        {
            Assert.AreEqual(2, GearEssences.LowestIndex(new List<int> { 900, 700, 120, 800 }));
            Assert.AreEqual(0, GearEssences.LowestIndex(new List<int> { 0, 1000 }));
            Assert.AreEqual(0, GearEssences.LowestIndex(new List<int> { 555 }));
            Assert.AreEqual(-1, GearEssences.LowestIndex(new List<int>()));
        }

        [TestMethod]
        public void LowestIndex_TieGoesToTheFirstInOrder()
        {
            Assert.AreEqual(1, GearEssences.LowestIndex(new List<int> { 900, 400, 400, 700 }));
            Assert.AreEqual(0, GearEssences.LowestIndex(new List<int> { 1000, 1000, 1000 }));
            Assert.AreEqual(2, GearEssences.LowestIndex(new List<int> { 500, 450, 300, 300 }));
        }

        [TestMethod]
        public void SecondThoughts_ArmourPicksTheLowestChangeableLine_InRecordOrder()
        {
            // 50 (Damage Resist) is an Always Rolled built-in: never picked even though it is the lowest grade
            var helm = CreateArmor("50:100;28:400;19:400;29:700");
            Assert.AreEqual(28, GearEssences.SecondThoughtsKey(helm));   // tie 28 / 19 at 400: the first in record order
            helm.SetProperty(PropertyString.ZcModifiers, "50:100;19:400;28:400;29:700");
            Assert.AreEqual(19, GearEssences.SecondThoughtsKey(helm));
        }

        [TestMethod]
        public void SecondThoughts_SkipsTheLockedLine()
        {
            var helm = CreateArmor("28:100;19:400;29:700");
            helm.SetProperty(PropertyInt.GearEssenceLockedKey, 28);
            Assert.AreEqual(19, GearEssences.SecondThoughtsKey(helm));
        }

        [TestMethod]
        public void SecondThoughts_WeaponComparesCardsByGrade()
        {
            // the armour-style line 28 left on an old weapon is not a card: never touched
            var sword = CreateWeapon("-11:300;-14:300;-16:200;28:10");
            Assert.AreEqual(ZoneStatResolver.WeaponCrushKey, GearEssences.SecondThoughtsKey(sword));
            sword.SetProperty(PropertyString.ZcModifiers, "-11:300;-14:200;-16:200");
            Assert.AreEqual(ZoneStatResolver.WeaponSlayerKey, GearEssences.SecondThoughtsKey(sword));   // tie: record order
        }

        private static EvaluatedProfile Profile(params (string Stat, double Value)[] stats)
        {
            var values = new Dictionary<string, double>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var (stat, value) in stats)
                values[stat] = value;
            return new EvaluatedProfile("T15 Default", 1, ZoneVariant.Minion, values);
        }

        private static int QMid(string grade)
        {
            foreach (var b in WeaponScalingManager.SubGradeBands)
                if (b.Grade == grade)
                    return b.QMid;
            throw new System.ArgumentException(grade);
        }

        private static string Grade(int quality) => WeaponScalingManager.GetQualitySubGrade(quality);

        // ---- Tempering: one sub-grade step ----

        [TestMethod]
        public void TemperStep_TableIs60Up10Down30None()
        {
            Assert.AreEqual(GearEssences.TemperStep.Up, GearEssences.RollTemperStep(0.0, 0.6, 0.1));
            Assert.AreEqual(GearEssences.TemperStep.Up, GearEssences.RollTemperStep(0.5999, 0.6, 0.1));
            Assert.AreEqual(GearEssences.TemperStep.Down, GearEssences.RollTemperStep(0.6, 0.6, 0.1));
            Assert.AreEqual(GearEssences.TemperStep.Down, GearEssences.RollTemperStep(0.6999, 0.6, 0.1));
            Assert.AreEqual(GearEssences.TemperStep.None, GearEssences.RollTemperStep(0.7, 0.6, 0.1));
            Assert.AreEqual(GearEssences.TemperStep.None, GearEssences.RollTemperStep(0.9999, 0.6, 0.1));
            // the same table as a share of an even sweep of rolls
            int up = 0, down = 0, none = 0;
            for (var i = 0; i < 10000; i++)
                switch (GearEssences.RollTemperStep((i + 0.5) / 10000.0, 0.6, 0.1))
                {
                    case GearEssences.TemperStep.Up: up++; break;
                    case GearEssences.TemperStep.Down: down++; break;
                    default: none++; break;
                }
            Assert.AreEqual(6000, up);
            Assert.AreEqual(1000, down);
            Assert.AreEqual(3000, none);
        }

        [TestMethod]
        public void Tempering_StepsOneSubGradeAndLandsOnTheMidpoint()
        {
            var top = GearEssences.SubGradeIndexOfRung(GearEssences.TemperingMaxRungDefault);
            Assert.AreEqual(QMid("B-"), GearEssences.TemperedQuality(760, GearEssences.TemperStep.Up, top));     // C+ -> B-
            Assert.AreEqual(QMid("C"), GearEssences.TemperedQuality(760, GearEssences.TemperStep.Down, top));    // C+ -> C
            Assert.AreEqual(760, GearEssences.TemperedQuality(760, GearEssences.TemperStep.None, top));          // untouched
            Assert.AreEqual(QMid("F"), GearEssences.TemperedQuality(10, GearEssences.TemperStep.Up, top));       // F- -> F
            Assert.AreEqual(QMid("D-"), GearEssences.TemperedQuality(400, GearEssences.TemperStep.Up, top));     // F+ -> D-
            Assert.AreEqual(QMid("A+"), GearEssences.TemperedQuality(940, GearEssences.TemperStep.Up, top));     // A -> A+
            // every rung from F- climbs one at a time to A+ and lands on each midpoint on the way
            var q = 0;
            var path = new List<string>();
            for (var i = 0; i < 20; i++)
            {
                q = GearEssences.TemperedQuality(q, GearEssences.TemperStep.Up, top);
                path.Add(Grade(q));
            }
            CollectionAssert.AreEqual(new[] { "F", "F+", "D-", "D", "D+", "C-", "C", "C+", "B-", "B", "B+", "A-", "A", "A+" }, path.GetRange(0, 14));
            Assert.AreEqual(QMid("A+"), q);   // and stops there
        }

        [TestMethod]
        public void Tempering_NeverBelowFMinus()
        {
            var top = GearEssences.SubGradeIndexOfRung(GearEssences.TemperingMaxRungDefault);
            Assert.AreEqual(50, GearEssences.TemperedQuality(50, GearEssences.TemperStep.Down, top));
            Assert.AreEqual(0, GearEssences.TemperedQuality(0, GearEssences.TemperStep.Down, top));
            Assert.AreEqual(QMid("F-"), GearEssences.TemperedQuality(250, GearEssences.TemperStep.Down, top));   // F -> F-
            Assert.IsNull(GearEssences.TemperingRefusal(0, top, "Sword"));   // F- can still be used: it can only go up or stay
        }

        [TestMethod]
        public void Tempering_NeverAboveTheTop_AndTheTopIsRefused()
        {
            var aPlus = GearEssences.SubGradeIndexOfRung(14);
            Assert.AreEqual("A+", WeaponScalingManager.SubGradeBands[aPlus].Grade);
            Assert.IsNotNull(GearEssences.TemperingRefusal(QMid("A+"), aPlus, "Sword"));
            Assert.IsNotNull(GearEssences.TemperingRefusal(999, aPlus, "Sword"));
            Assert.IsNull(GearEssences.TemperingRefusal(QMid("A"), aPlus, "Sword"));
            Assert.AreEqual(QMid("A+"), GearEssences.TemperedQuality(QMid("A+"), GearEssences.TemperStep.Up, aPlus));   // the bound holds

            // an admin top of A- (rung 12): B+ climbs to A- and stops; A- and anything above it is refused
            var aMinus = GearEssences.SubGradeIndexOfRung(12);
            Assert.AreEqual("A-", WeaponScalingManager.SubGradeBands[aMinus].Grade);
            Assert.AreEqual(QMid("A-"), GearEssences.TemperedQuality(QMid("B+"), GearEssences.TemperStep.Up, aMinus));
            Assert.AreEqual(QMid("A-"), GearEssences.TemperedQuality(QMid("A-"), GearEssences.TemperStep.Up, aMinus));
            Assert.IsNotNull(GearEssences.TemperingRefusal(QMid("A-"), aMinus, "Sword"));
            Assert.IsNotNull(GearEssences.TemperingRefusal(QMid("A"), aMinus, "Sword"));
            Assert.IsNull(GearEssences.TemperingRefusal(QMid("B+"), aMinus, "Sword"));
        }

        [TestMethod]
        public void Tempering_SIsUntouchedAndRefused_AndNeverReached()
        {
            var top = GearEssences.SubGradeIndexOfRung(GearEssences.TemperingMaxRungDefault);
            StringAssert.Contains(GearEssences.TemperingRefusal(1000, top, "Sword"), "S Weapon Grade");
            foreach (var step in new[] { GearEssences.TemperStep.Up, GearEssences.TemperStep.Down, GearEssences.TemperStep.None })
                Assert.AreEqual(1000, GearEssences.TemperedQuality(1000, step, top));
            // even a top beyond A+ never reaches S
            Assert.AreEqual(GearEssences.SubGradeIndexOfRung(14), GearEssences.SubGradeIndexOfRung(99));
            Assert.AreEqual(QMid("A+"), GearEssences.TemperedQuality(QMid("A+"), GearEssences.TemperStep.Up, 0));
            for (var q = 0; q < 1000; q += 7)
                Assert.IsTrue(GearEssences.TemperedQuality(q, GearEssences.TemperStep.Up, 0) < 1000, $"quality {q} reached S");
        }

        [TestMethod]
        public void TemperingSettings_DefaultsWhenUnset()
        {
            var (up, down, top) = GearEssences.TemperingSettings(null, 15);
            Assert.AreEqual(0.60, up, 1e-12);
            Assert.AreEqual(0.10, down, 1e-12);
            Assert.AreEqual("A+", WeaponScalingManager.SubGradeBands[top].Grade);
            (up, down, top) = GearEssences.TemperingSettings(Profile(), 15);
            Assert.AreEqual(0.60, up, 1e-12);
            Assert.AreEqual(0.10, down, 1e-12);
            Assert.AreEqual("A+", WeaponScalingManager.SubGradeBands[top].Grade);
        }

        [TestMethod]
        public void TemperingSettings_ReadsAndBoundsTheStats()
        {
            var (up, down, top) = GearEssences.TemperingSettings(Profile(
                (ZoneStat.BagTemperingUpChance, 0.5), (ZoneStat.BagTemperingDownChance, 0.2), (ZoneStat.BagTemperingMaxSubgrade, 9)), 15);
            Assert.AreEqual(0.5, up, 1e-12);
            Assert.AreEqual(0.2, down, 1e-12);
            Assert.AreEqual("B-", WeaponScalingManager.SubGradeBands[top].Grade);

            (up, down, _) = GearEssences.TemperingSettings(Profile((ZoneStat.BagTemperingUpChance, 0.7), (ZoneStat.BagTemperingDownChance, 0.5)), 15);
            Assert.AreEqual(0.7, up, 1e-12);
            Assert.AreEqual(0.3, down, 1e-12);   // held to 1 - up

            (up, down, top) = GearEssences.TemperingSettings(Profile(
                (ZoneStat.BagTemperingUpChance, double.NaN), (ZoneStat.BagTemperingDownChance, -1), (ZoneStat.BagTemperingMaxSubgrade, 40)), 15);
            Assert.AreEqual(0.60, up, 1e-12);   // not a real number: the default
            Assert.AreEqual(0.0, down, 1e-12);
            Assert.AreEqual("A+", WeaponScalingManager.SubGradeBands[top].Grade);   // never S

            (_, _, top) = GearEssences.TemperingSettings(Profile((ZoneStat.BagTemperingMaxSubgrade, -5)), 15);
            Assert.AreEqual("F-", WeaponScalingManager.SubGradeBands[top].Grade);
        }

        // ---- Bag of Madness ----

        private static GearEssences.MadnessSettings Only(GearEssences.MadnessOutcome o)
        {
            var d = GearEssences.MadnessDefaults;
            return d with
            {
                Nothing = o == GearEssences.MadnessOutcome.Nothing ? 1 : 0,
                OverMax = o == GearEssences.MadnessOutcome.OverMax ? 1 : 0,
                Shift = o == GearEssences.MadnessOutcome.Shift ? 1 : 0,
                Remove = o == GearEssences.MadnessOutcome.Remove ? 1 : 0,
            };
        }

        private static GearEssences.MadnessRoll Roll(System.Random rng, IReadOnlyList<(int Key, int Grade)> eligible, IReadOnlyList<int> removable,
            GearEssences.MadnessSettings s)
            => GearEssences.RollMadness(eligible, removable, s, (lo, hi) => rng.Next(lo, hi + 1), rng.NextDouble);

        [TestMethod]
        public void MadnessSettings_DefaultsWhenUnset()
        {
            foreach (var s in new[] { GearEssences.MadnessSettingsFrom(null, 15), GearEssences.MadnessSettingsFrom(Profile(), 15) })
            {
                Assert.AreEqual(0.30, s.Nothing, 1e-12);
                Assert.AreEqual(0.30, s.OverMax, 1e-12);
                Assert.AreEqual(0.30, s.Shift, 1e-12);
                Assert.AreEqual(0.10, s.Remove, 1e-12);
                Assert.AreEqual(0.10, s.OverMaxMin, 1e-12);
                Assert.AreEqual(0.25, s.OverMaxMax, 1e-12);
                Assert.AreEqual(0.15, s.ShiftRange, 1e-12);
            }
        }

        [TestMethod]
        public void MadnessSettings_ReadsAndBoundsTheStats()
        {
            var s = GearEssences.MadnessSettingsFrom(Profile(
                (ZoneStat.BagMadnessNothingChance, 0.5), (ZoneStat.BagMadnessRemoveChance, -2),
                (ZoneStat.BagMadnessOverMaxMin, 0.4), (ZoneStat.BagMadnessOverMaxMax, 0.05), (ZoneStat.BagMadnessShift, double.NaN)), 15);
            Assert.AreEqual(0.5, s.Nothing, 1e-12);
            Assert.AreEqual(0.30, s.OverMax, 1e-12);
            Assert.AreEqual(0.0, s.Remove, 1e-12);       // below 0 is 0
            Assert.AreEqual(0.05, s.OverMaxMin, 1e-12);  // put in order ...
            Assert.AreEqual(0.25, s.OverMaxMax, 1e-12);  // ... and held to the 1250 ceiling
            Assert.AreEqual(0.15, s.ShiftRange, 1e-12);  // not a real number: the default

            var zero = GearEssences.MadnessSettingsFrom(Profile((ZoneStat.BagMadnessNothingChance, 0), (ZoneStat.BagMadnessOverMaxChance, 0),
                (ZoneStat.BagMadnessShiftChance, 0), (ZoneStat.BagMadnessRemoveChance, 0)), 15);
            Assert.AreEqual(GearEssences.MadnessDefaults, zero);   // four zero weights: the defaults
        }

        [TestMethod]
        public void MadnessOutcome_TableIs30_30_30_10()
        {
            var counts = new Dictionary<GearEssences.MadnessOutcome, int>();
            for (var i = 0; i < 10000; i++)
            {
                var o = GearEssences.PickMadnessOutcome((i + 0.5) / 10000.0, GearEssences.MadnessDefaults);   // mid-bin: no float edge
                counts[o] = counts.TryGetValue(o, out var c) ? c + 1 : 1;
            }
            Assert.AreEqual(3000, counts[GearEssences.MadnessOutcome.Nothing]);
            Assert.AreEqual(3000, counts[GearEssences.MadnessOutcome.OverMax]);
            Assert.AreEqual(3000, counts[GearEssences.MadnessOutcome.Shift]);
            Assert.AreEqual(1000, counts[GearEssences.MadnessOutcome.Remove]);
            // weights, not chances: doubling all four changes nothing
            var doubled = new GearEssences.MadnessSettings(0.6, 0.6, 0.6, 0.2, 0.1, 0.25, 0.15);
            Assert.AreEqual(GearEssences.MadnessOutcome.Remove, GearEssences.PickMadnessOutcome(0.95, doubled));
            Assert.AreEqual(GearEssences.MadnessOutcome.OverMax, GearEssences.PickMadnessOutcome(0.45, doubled));
        }

        [TestMethod]
        public void Madness_Nothing_ChangesNothing()
        {
            var r = Roll(new System.Random(1), new[] { (28, 500), (19, 900) }, new[] { 28, 19 }, Only(GearEssences.MadnessOutcome.Nothing));
            Assert.AreEqual(GearEssences.MadnessOutcome.Nothing, r.Outcome);
            Assert.AreEqual(0, r.Grades.Count);
            Assert.IsNull(r.RemovedKey);
        }

        [TestMethod]
        public void Madness_OverMaxText_FollowsTheRoundedGrades()
        {
            GearEssences.MadnessSettings S(double lo, double hi) => GearEssences.MadnessDefaults with { OverMaxMin = lo, OverMaxMax = hi };
            Assert.AreEqual("set past its maximum, by 10% to 25% of its range", GearEssences.MadnessOverMaxText(GearEssences.MadnessDefaults));
            Assert.AreEqual("set to its maximum", GearEssences.MadnessOverMaxText(S(0, 0)));
            // CodeRabbit #629: 0.0001 rounds to 0 grade points, so the roll lands on the maximum itself
            Assert.AreEqual((1000, 1000), GearEssences.MadnessOverMaxGrades(S(0.0001, 0.0001)));
            Assert.AreEqual("set to its maximum", GearEssences.MadnessOverMaxText(S(0.0001, 0.0001)));
            Assert.AreEqual("set to its maximum or past it, by up to 25% of its range", GearEssences.MadnessOverMaxText(S(0.0001, 0.25)));
            Assert.AreEqual("set to its maximum or past it, by up to 25% of its range", GearEssences.MadnessOverMaxText(S(0, 0.25)));
        }

        [TestMethod]
        public void Madness_OverMax_SetsOnePropertyToMaxPlus10To25PctOfRange()
        {
            Assert.AreEqual((1100, 1250), GearEssences.MadnessOverMaxGrades(GearEssences.MadnessDefaults));
            var rng = new System.Random(7);
            var eligible = new[] { (28, 10), (19, 1000), (ZoneStatResolver.WeaponSlayerKey, 400) };
            var seen = new HashSet<int>();
            int lo = int.MaxValue, hi = 0;
            for (var i = 0; i < 3000; i++)
            {
                var r = Roll(rng, eligible, new int[0], Only(GearEssences.MadnessOutcome.OverMax));
                Assert.AreEqual(1, r.Grades.Count);   // exactly one property, whatever it was before (a junk 10 included)
                var (key, grade) = r.Grades.Single();
                Assert.IsTrue(grade >= 1100 && grade <= 1250, $"grade {grade}");
                seen.Add(key);
                lo = System.Math.Min(lo, grade);
                hi = System.Math.Max(hi, grade);
            }
            Assert.AreEqual(3, seen.Count);   // any eligible property can be the one
            Assert.AreEqual(1100, lo);
            Assert.AreEqual(1250, hi);
        }

        [TestMethod]
        public void Madness_Shift_MovesEveryPropertyFromWhereItIs()
        {
            var rng = new System.Random(11);
            var eligible = new[] { (28, 500), (19, 1000), (29, 50), (43, 1200), (25, 1240) };
            for (var i = 0; i < 3000; i++)
            {
                var r = Roll(rng, eligible, new int[0], Only(GearEssences.MadnessOutcome.Shift));
                Assert.IsTrue(r.Grades.Count <= eligible.Length);   // every eligible property that moved; an unmoved one gets no op
                foreach (var (key, before) in eligible)
                {
                    if (!r.Grades.TryGetValue(key, out var after))
                        continue;
                    Assert.AreNotEqual(before, after, $"{key}: an unchanged grade must not be planned");
                    Assert.IsTrue(after >= 0 && after <= ZoneStatResolver.GradeOverMaxCeiling, $"{key}: {after}");
                    var lowest = System.Math.Max(0, before - 150);
                    var highest = System.Math.Min(ZoneStatResolver.GradeOverMaxCeiling, before + 150);
                    Assert.IsTrue(after >= lowest && after <= highest, $"{key}: {before} -> {after}");
                }
            }
            // 50% of the range lands between 35% and 65%; max can pass max by up to 15%; nothing below 0 or past 1250
            Assert.AreEqual(350, GearEssences.MadnessShifted(500, -150));
            Assert.AreEqual(1150, GearEssences.MadnessShifted(1000, 150));
            Assert.AreEqual(0, GearEssences.MadnessShifted(50, -150));
            Assert.AreEqual(1250, GearEssences.MadnessShifted(1240, 150));
        }

        [TestMethod]
        public void Madness_Shift_ZeroRange_PlansNothing()
        {
            // CodeRabbit 2026-11-26: a shift that moves nothing must not plan regrades (they would re-resolve the cards)
            var rng = new System.Random(5);
            var s = Only(GearEssences.MadnessOutcome.Shift) with { ShiftRange = 0.0 };
            var r = Roll(rng, new[] { (28, 500), (19, 1000) }, new int[0], s);
            Assert.AreEqual(GearEssences.MadnessOutcome.Shift, r.Outcome);
            Assert.AreEqual(0, r.Grades.Count);
        }

        [TestMethod]
        public void Madness_HugeWeights_StillSplitTheOdds()
        {
            // CodeRabbit 2026-11-26: 1e308 + 1e308 overflowed the sum; the weights are scaled by the largest first
            var s = GearEssences.MadnessDefaults with { Nothing = 1e308, OverMax = 0, Shift = 0, Remove = 1e308 };
            Assert.AreEqual(GearEssences.MadnessOutcome.Nothing, GearEssences.PickMadnessOutcome(0.25, s));
            Assert.AreEqual(GearEssences.MadnessOutcome.Remove, GearEssences.PickMadnessOutcome(0.75, s));
        }

        [TestMethod]
        public void ValueFor_OverMax_SaturatesInsteadOfOverflowing()
        {
            // CodeRabbit 2026-11-26: max + 25% of an extreme band passed int.MaxValue
            Assert.AreEqual(int.MaxValue, ZoneStatResolver.ValueFor(1_700_000_000, 2_100_000_000, ZoneStatResolver.GradeOverMaxCeiling));
            Assert.AreEqual(2_100_000_000, ZoneStatResolver.ValueFor(1_700_000_000, 2_100_000_000, ZoneStatResolver.GradeMax));
        }

        [TestMethod]
        public void Madness_Remove_TakesOneRemovableProperty()
        {
            var rng = new System.Random(3);
            var eligible = new[] { (ZoneStatResolver.WeaponBiteKey, 500), (ZoneStatResolver.WeaponCrushKey, 500), (ZoneStatResolver.WeaponSlayerKey, 500) };
            var removable = new[] { ZoneStatResolver.WeaponSlayerKey };   // a Bandit Hilt keeps Bite and Crush
            for (var i = 0; i < 200; i++)
            {
                var r = Roll(rng, eligible, removable, Only(GearEssences.MadnessOutcome.Remove));
                Assert.AreEqual(ZoneStatResolver.WeaponSlayerKey, r.RemovedKey);
                Assert.AreEqual(0, r.Grades.Count);
            }
            var none = Roll(rng, eligible, new int[0], Only(GearEssences.MadnessOutcome.Remove));
            Assert.AreEqual(GearEssences.MadnessOutcome.Remove, none.Outcome);
            Assert.IsNull(none.RemovedKey);   // nothing it may take: nothing changes (the item is still Tainted)
        }

        [TestMethod]
        public void Madness_EligibleIsTheMadnessListOnly()
        {
            // 19, 25, 28, 29, 31, 43 in; 32 / 33 / 47 / 48 / 54 (range 2, Chase), 50 (built-in), 41 (slot special) out
            var helm = CreateArmor("19:500;25:500;28:500;29:500;31:500;43:500;32:500;33:500;47:500;48:500;54:500;50:500;41:500");
            var (eligible, removable) = GearEssences.MadnessKeys(helm);
            CollectionAssert.AreEquivalent(new[] { 19, 25, 28, 29, 31, 43 }, eligible);
            CollectionAssert.AreEquivalent(eligible, removable);

            // weapons: Biting Strike, Rend Power, Slayer, Crushing Blow in; Armor Rend, Shield Cleaving, Spell Armor Rend, Cast on Strike out
            var sword = CreateWeapon("-11:500;-12:500;-13:500;-14:500;-15:500;-16:500;-17:500;-19:500;28:500");
            (eligible, _) = GearEssences.MadnessKeys(sword);
            CollectionAssert.AreEquivalent(new[] { ZoneStatResolver.WeaponBiteKey, ZoneStatResolver.WeaponRendPowerKey,
                ZoneStatResolver.WeaponSlayerKey, ZoneStatResolver.WeaponCrushKey }, eligible);

            Assert.AreEqual(0, GearEssences.MadnessKeys(CreateArmor("32:500;33:2;50:900")).Eligible.Count);   // refused: nothing to change
        }

        [TestMethod]
        public void Madness_LockedPropertyIsSkippedByEveryOutcome()
        {
            var helm = CreateArmor("19:500;28:1000;29:500");
            helm.SetProperty(PropertyInt.GearEssenceLockedKey, 28);
            var (eligible, removable) = GearEssences.MadnessKeys(helm);
            CollectionAssert.AreEquivalent(new[] { 19, 29 }, eligible);
            CollectionAssert.AreEquivalent(new[] { 19, 29 }, removable);
            var units = eligible.Select(k => (k, 500)).ToList();
            var rng = new System.Random(5);
            foreach (GearEssences.MadnessOutcome o in System.Enum.GetValues(typeof(GearEssences.MadnessOutcome)))
                for (var i = 0; i < 100; i++)
                {
                    var r = Roll(rng, units, removable, Only(o));
                    Assert.IsFalse(r.Grades.ContainsKey(28));
                    Assert.AreNotEqual(28, r.RemovedKey);
                }
        }

        [TestMethod]
        public void Madness_BanditHiltKeepsBiteAndCrushFromRemoval()
        {
            var sword = CreateWeapon("-11:500;-14:500;-16:500");
            sword.SetProperty(PropertyFloat.ManaStoneDestroyChance, 0.01);
            sword.SetProperty(PropertyBool.Ivoryable, true);
            sword.SetProperty(PropertyInt.WieldSkillType2, 46);
            sword.SetProperty(PropertyInt.WieldRequirements2, 8);
            Assert.IsTrue(ZoneLootMutator.HasBanditHilt(sword));
            var (eligible, removable) = GearEssences.MadnessKeys(sword);
            Assert.AreEqual(3, eligible.Count);   // still shifted or pushed over max
            CollectionAssert.AreEquivalent(new[] { ZoneStatResolver.WeaponSlayerKey }, removable);
        }

        [TestMethod]
        public void Madness_PromptKeysChangeWhenABanditHiltLands()
        {
            // CodeRabbit #629: a hilt applied while the Madness prompt is open shrinks the removable list; the prompt state
            // (MadnessPromptKeys is part of it) must differ, so the use is refused rather than applied to an unseen set
            var sword = CreateWeapon("-11:500;-14:500;-16:500");
            var before = GearEssences.MadnessPromptKeys(sword);
            sword.SetProperty(PropertyFloat.ManaStoneDestroyChance, 0.01);
            sword.SetProperty(PropertyBool.Ivoryable, true);
            sword.SetProperty(PropertyInt.WieldSkillType2, 46);
            sword.SetProperty(PropertyInt.WieldRequirements2, 8);
            Assert.IsTrue(ZoneLootMutator.HasBanditHilt(sword));
            Assert.AreNotEqual(before, GearEssences.MadnessPromptKeys(sword));
            Assert.AreEqual(GearEssences.MadnessPromptKeys(sword), GearEssences.MadnessPromptKeys(sword));   // stable while nothing changes
        }

        // ---- over-max grades: honoured only where intended ----

        [TestMethod]
        public void ValueFor_HonoursOverMaxUpToTheCeiling()
        {
            Assert.AreEqual(69, ZoneStatResolver.ValueFor(14, 69, 1000));
            Assert.AreEqual(80, ZoneStatResolver.ValueFor(14, 69, 1200));   // max + 20% of 55
            Assert.AreEqual(83, ZoneStatResolver.ValueFor(14, 69, 1250));   // max + 25%
            Assert.AreEqual(83, ZoneStatResolver.ValueFor(14, 69, 5000));   // never past the ceiling
            Assert.AreEqual(14, ZoneStatResolver.ValueFor(14, 69, -50));
            Assert.AreEqual(0.625, ZoneStatResolver.ValueForD(0.50, 0.60, 1250), 1e-12);
            Assert.AreEqual(0.60, ZoneStatResolver.ValueForD(0.50, 0.60, 1000), 1e-12);
        }

        [TestMethod]
        public void Record_HoldsOverMaxOnlyOnMadnessKeys()
        {
            var wo = CreateArmor("28:1200;50:1200;32:1300;19:9999;-11:1200;-12:1200;-15:1250");
            var g = ZoneStatResolver.Read(wo).ToDictionary(l => l.Key, l => l.Grade);
            Assert.AreEqual(1200, g[28]);
            Assert.AreEqual(1000, g[50]);    // a built-in resist never goes over max
            Assert.AreEqual(1000, g[32]);    // nor does a range-2 line
            Assert.AreEqual(1250, g[19]);    // the ceiling
            Assert.AreEqual(1200, g[ZoneStatResolver.WeaponBiteKey]);
            Assert.AreEqual(1000, g[ZoneStatResolver.WeaponArmorRendKey]);
            Assert.AreEqual(1000, g[ZoneStatResolver.WeaponShieldCleaveKey]);
            // Format round-trips exactly what Read allows
            Assert.AreEqual("28:1200;50:1000;32:1000;19:1250;-11:1200;-12:1000;-15:1000", ZoneStatResolver.Format(ZoneStatResolver.Read(wo)));
        }

        [TestMethod]
        public void Producers_NeverWriteOverMax()
        {
            var wo = CreateArmor("19:500");
            ZoneStatResolver.AddLine(wo, 28, 1200);   // drops, bag adds and the forge add through here
            Assert.AreEqual(1000, ZoneStatResolver.Read(wo).Single(l => l.Key == 28).Grade);
            for (var i = 0; i < 2000; i++)
                Assert.IsTrue(ZoneStatResolver.RollGrade(25) <= ZoneStatResolver.GradeMax);
            Assert.AreEqual(ZoneStatResolver.GradeMax, ZoneStatResolver.RollGrade(25, forceMax: true));
            Assert.AreEqual(1000, ZoneStatResolver.GradeFor(14, 69, 90));   // migration: above max is not over-max
            Assert.AreEqual(ZoneStatResolver.GradeMax, ZoneStatResolver.GradeCap(50));
            Assert.AreEqual(ZoneStatResolver.GradeOverMaxCeiling, ZoneStatResolver.GradeCap(28));
        }

        [TestMethod]
        public void CardValue_KeepsTheHardClampAndBitesOwnCeiling()
        {
            var bite = ZoneStatResolver.SpecBite;
            Assert.AreEqual(0.60, ZoneStatResolver.CardValue(bite, 0.50, 0.60, 1000), 1e-12);
            Assert.AreEqual(0.61, ZoneStatResolver.CardValue(bite, 0.50, 0.60, 1100), 1e-12);
            Assert.AreEqual(0.625, ZoneStatResolver.CardValue(bite, 0.50, 0.60, 1250), 1e-12);
            Assert.AreEqual(0.625, ZoneStatResolver.CardValue(bite, 0.10, 0.60, 1250), 1e-12);   // a wide pin: held to 0.625, not 0.725
            Assert.AreEqual(0.90, ZoneStatResolver.CardValue(bite, 0.33, 0.90, 1250), 1e-12);    // a max already above 0.625 stays
            Assert.AreEqual(0.70, ZoneStatResolver.CardValue(bite, 0.40, 0.70, 1250), 1e-12);    // never below what max gives
            // Rend Power at T25 sits on its 5.0 hard clamp already
            Assert.AreEqual(5.0, ZoneStatResolver.CardValue(ZoneStatResolver.SpecRendPower, 3.75, 5.00, 1250), 1e-12);
            Assert.AreEqual(4.375, ZoneStatResolver.CardValue(ZoneStatResolver.SpecSlayer, 3.00, 4.10, 1250), 1e-9);
            Assert.AreEqual(7.125, ZoneStatResolver.CardValue(ZoneStatResolver.SpecCrush, 5.50, 6.80, 1250), 1e-9);
            Assert.AreEqual(10.0, ZoneStatResolver.CardValue(ZoneStatResolver.SpecSlayer, 5.0, 9.5, 1250), 1e-9);   // Slayer Hi 10
            // in-band grades are unchanged by the over-max path
            Assert.AreEqual(0.55, ZoneStatResolver.CardValue(bite, 0.50, 0.60, 500), 1e-12);
        }

        [TestMethod]
        public void AppraisalLine_MarksOverMaxBeforeTheBand()
        {
            ZoneModifiers.TryGet(28, out var def);
            var over = new ZoneStatResolver.ResolvedLine { Record = new ZoneStatResolver.LineRecord { Key = 28, Grade = 1200 }, Def = def, Min = 14, Max = 69, Value = 80 };
            Assert.IsTrue(over.IsOverMax);
            Assert.AreEqual("Damage Rating +80 (over max) [14-69]", over.Text);
            Assert.AreEqual("Damage Rating +0 [14-69]", over.TextAt(0));   // the zone lock's value is never marked
            var max = new ZoneStatResolver.ResolvedLine { Record = new ZoneStatResolver.LineRecord { Key = 28, Grade = 1000 }, Def = def, Min = 14, Max = 69, Value = 69 };
            Assert.IsFalse(max.IsOverMax);
            Assert.AreEqual("Damage Rating +69 [14-69]", max.Text);
        }

        [TestMethod]
        public void NewStats_AreAppendedAtTheEndOfAll()
        {
            var tail = new[]
            {
                ZoneStat.BagTemperingUpChance, ZoneStat.BagTemperingDownChance, ZoneStat.BagTemperingMaxSubgrade,
                ZoneStat.BagMadnessNothingChance, ZoneStat.BagMadnessOverMaxChance, ZoneStat.BagMadnessShiftChance, ZoneStat.BagMadnessRemoveChance,
                ZoneStat.BagMadnessOverMaxMin, ZoneStat.BagMadnessOverMaxMax, ZoneStat.BagMadnessShift,
            };
            CollectionAssert.AreEqual(tail, ZoneStat.All.Skip(ZoneStat.All.Length - tail.Length).ToArray());
            foreach (var s in tail)
                Assert.AreEqual("Salvage bags", ZoneStatCatalog.GroupOf(s), s);
        }

        [TestMethod]
        public void SecondThoughts_NothingToChange_IsNull()
        {
            Assert.IsNull(GearEssences.SecondThoughtsKey(CreateArmor("50:100;51:200")));
            var one = CreateArmor("28:100");
            one.SetProperty(PropertyInt.GearEssenceLockedKey, 28);
            Assert.IsNull(GearEssences.SecondThoughtsKey(one));
        }

        /// <summary>CodeRabbit #629 (2026-11-27): a rend bit in ImbuedEffect2-5 blocks the bags that clear ImbuedEffect, also
        /// when the same bit is in ImbuedEffect too (the old union-vs-first-slot comparison let that one through).</summary>
        [TestMethod]
        public void ImbueOutsideFirstSlot_SeesLaterSlots_EvenWhenDuplicated()
        {
            var weenie = new Weenie { WeenieClassId = 457501, ClassName = "TestRendSlots", WeenieType = WeenieType.MeleeWeapon };
            var w = new MeleeWeapon(weenie, new ObjectGuid(0xF0005790));
            w.ImbuedEffect = ImbuedEffectType.FireRending;
            Assert.IsFalse(GearEssences.ImbueOutsideFirstSlot(w, ImbuedEffectType.FireRending), "first slot only");

            foreach (var slot in new[] { PropertyInt.ImbuedEffect2, PropertyInt.ImbuedEffect3, PropertyInt.ImbuedEffect4, PropertyInt.ImbuedEffect5 })
            {
                w.SetProperty(slot, (int)ImbuedEffectType.FireRending);
                Assert.IsTrue(GearEssences.ImbueOutsideFirstSlot(w, ImbuedEffectType.FireRending), $"{slot}: duplicated");
                w.ImbuedEffect = ImbuedEffectType.Undef;
                Assert.IsTrue(GearEssences.ImbueOutsideFirstSlot(w, ImbuedEffectType.FireRending), $"{slot}: later slot only");
                Assert.IsFalse(GearEssences.ImbueOutsideFirstSlot(w, ImbuedEffectType.ArmorRending), $"{slot}: other mask");
                w.RemoveProperty(slot);
                w.ImbuedEffect = ImbuedEffectType.FireRending;
            }
        }
    }
}
