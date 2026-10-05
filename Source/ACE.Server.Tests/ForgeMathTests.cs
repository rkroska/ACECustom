using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Server.Entity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using static ACE.Server.Entity.ForgeMath;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Pure-math contract of ForgeMath (blacksmithing). The website simulator will mirror these rules, so
    /// any change here is a change to the design model.
    /// </summary>
    [TestClass]
    public class ForgeMathTests
    {
        private const double Better = 0.10;   // < 0.55: the better weapon's value
        private const double Worse = 0.90;    // >= 0.55: the worse weapon's value
        private const double NoSpark = 0.99;
        private const double MainElement = 0.10;   // < 0.5: the main weapon's element
        private const double FeederElement = 0.90;

        private const int Fire = 16, Frost = 8;

        private static ForgeConfig Config() => new()
        {
            HigherParentChance = 0.55,
            SparkChance = 0.02,
            HoneBaseCost = 50_000_000,
            HoneCostGrowth = 2.0,
            HoneBaseChance = 0.60,
            HoneChanceStep = 0.05,
            HoneMinChance = 0.15,
            HoneMisfortuneStep = 0.05,
            HoneMaxLevels = 10,
            HoneStep = new Dictionary<ForgeLine, double>
            {
                [ForgeLine.MaxDamage] = 0.02,
                [ForgeLine.Variance] = 0.05,
                [ForgeLine.Speed] = 0.05,
                [ForgeLine.AttackMod] = 0.05,
                [ForgeLine.MeleeDefense] = 0.05,
                [ForgeLine.MissileDefense] = 0.05,
                [ForgeLine.MagicDefense] = 0.05,
                [ForgeLine.DamageMod] = 0.02,
            },
            UnbindFraction = 0.25,
            UnbindMinFee = 1_000_000_000,
        };

        /// <summary>Draw source that hands out a script and fails the test if the code draws more than expected.</summary>
        private static Func<double> Script(params double[] draws)
        {
            var queue = new Queue<double>(draws);
            return () => queue.Count > 0 ? queue.Dequeue() : throw new AssertFailedException("ForgeMath consumed more draws than scripted.");
        }

        /// <summary>One draw per line, then quality, then the element, then the spark.</summary>
        private static double[] Rolls(double lineRoll, double qualityRoll = Better, double spark = NoSpark, double element = MainElement)
            => Enumerable.Repeat(lineRoll, InheritOrder.Length).Append(qualityRoll).Append(element).Append(spark).ToArray();

        /// <summary>Lines and quality at Better, then <paramref name="middle"/> (grade / spell / package draws), the element, the spark.</summary>
        private static double[] With(double[] middle, double element = MainElement, params double[] spark)
            => Enumerable.Repeat(Better, InheritOrder.Length).Append(Better).Concat(middle).Append(element)
               .Concat(spark.Length == 0 ? new[] { NoSpark } : spark).ToArray();

        private static ForgeWeapon Dagger(double damage, double variance, double attack)
        {
            var w = new ForgeWeapon { Wcid = 30310, Group = "Finesse Weapons dagger (multi-strike)", Element = Fire, Tier = 8 };
            w.Lines[ForgeLine.MaxDamage] = damage;
            w.Lines[ForgeLine.Variance] = variance;
            w.Lines[ForgeLine.AttackMod] = attack;
            return w;
        }

        // ------------------------------------------------------------ the 55/45 rule

        [TestMethod]
        public void PicksMain_BetterBranchTakesBetterValue_WorseBranchTakesWorse()
        {
            Assert.IsTrue(PicksMain(true, true, 100, 50, false, 0.55, Better));
            Assert.IsFalse(PicksMain(true, true, 100, 50, false, 0.55, Worse));
            Assert.IsFalse(PicksMain(true, true, 50, 100, false, 0.55, Better));
            Assert.IsTrue(PicksMain(true, true, 50, 100, false, 0.55, Worse));
        }

        [TestMethod]
        public void PicksMain_Tie_CountsMainAsBetter()
        {
            Assert.IsTrue(PicksMain(true, true, 70, 70, false, 0.55, Better));
            Assert.IsFalse(PicksMain(true, true, 70, 70, false, 0.55, Worse));
        }

        [TestMethod]
        public void PicksMain_LowerIsBetter_InvertsTheComparison()
        {
            Assert.IsTrue(PicksMain(true, true, 0.3, 0.5, true, 0.55, Better));
            Assert.IsFalse(PicksMain(true, true, 0.5, 0.3, true, 0.55, Better));
        }

        [TestMethod]
        public void PicksMain_ValueOnOnlyOneSide_IsTheBetterOne()
        {
            Assert.IsFalse(PicksMain(false, true, 0, 5, false, 0.55, Better));
            Assert.IsTrue(PicksMain(false, true, 0, 5, false, 0.55, Worse));
            // Even for a lower-is-better line, having the value beats not having it.
            Assert.IsTrue(PicksMain(true, false, 9, 0, true, 0.55, Better));
        }

        [TestMethod]
        public void PicksMain_Boundary_0_55_IsWorseBranch()
        {
            Assert.IsTrue(PicksMain(true, true, 100, 50, false, 0.55, 0.549));
            Assert.IsFalse(PicksMain(true, true, 100, 50, false, 0.55, 0.55));
        }

        // ------------------------------------------------------------ forging

        [TestMethod]
        public void Forge_DifferentGroups_Throws()
        {
            var main = Dagger(20, 0.5, 1.1);
            var feeder = Dagger(20, 0.5, 1.1);
            feeder.Group = "Finesse Weapons sword (multi-strike)";
            Assert.ThrowsExactly<ArgumentException>(() => Forge(main, feeder, Config(), Script(Rolls(Better))));
        }

        [TestMethod]
        public void Forge_DifferentModelsOfOneGroup_AreAllowed()
        {
            var main = Dagger(20, 0.5, 1.1);
            var feeder = Dagger(22, 0.4, 1.2);
            feeder.Wcid = 31794;   // a Lancet next to a Knife: same group, different model
            var o = Forge(main, feeder, Config(), Script(Rolls(Better)));
            Assert.AreEqual(30310u, o.Result.Wcid, "the result keeps the main weapon's model");
        }

        [TestMethod]
        public void Forge_WeaponWithNoGroup_Throws()
        {
            var main = Dagger(20, 0.5, 1.1);
            main.Group = null;
            var feeder = Dagger(20, 0.5, 1.1);
            feeder.Group = null;
            Assert.ThrowsExactly<ArgumentException>(() => Forge(main, feeder, Config(), Script(Rolls(Better))));
        }

        [TestMethod]
        public void Forge_Element_Is5050_BetweenTheTwo()
        {
            var main = Dagger(20, 0.5, 1.1);
            var feeder = Dagger(20, 0.5, 1.1);
            feeder.Element = Frost;

            Assert.AreEqual(Fire, Forge(main, feeder, Config(), Script(Rolls(Better, element: 0.499))).Result.Element);
            Assert.AreEqual(Frost, Forge(main, feeder, Config(), Script(Rolls(Better, element: 0.5))).Result.Element);
        }

        [TestMethod]
        public void Forge_Element_DrawIsConsumedEvenWhenBothMatch()
        {
            var o = Forge(Dagger(20, 0.5, 1.1), Dagger(20, 0.5, 1.1), Config(), Script(Rolls(Better, element: FeederElement)));
            Assert.AreEqual(Fire, o.Result.Element);
            Assert.AreEqual(InheritOrder.Length + 3, o.RngDraws.Count);
        }

        [TestMethod]
        public void Forge_BetterBranch_TakesEachLinesBetterValue_FromEitherWeapon()
        {
            var main = Dagger(damage: 30, variance: 0.60, attack: 1.05);
            var feeder = Dagger(damage: 25, variance: 0.40, attack: 1.15);

            var r = Forge(main, feeder, Config(), Script(Rolls(Better))).Result;

            Assert.AreEqual(30, r.Lines[ForgeLine.MaxDamage]);     // main higher
            Assert.AreEqual(0.40, r.Lines[ForgeLine.Variance]);    // feeder lower = better
            Assert.AreEqual(1.15, r.Lines[ForgeLine.AttackMod]);   // feeder higher
        }

        [TestMethod]
        public void Forge_WorseBranch_TakesEachLinesWorseValue()
        {
            var main = Dagger(damage: 30, variance: 0.60, attack: 1.05);
            var feeder = Dagger(damage: 25, variance: 0.40, attack: 1.15);

            var r = Forge(main, feeder, Config(), Script(Rolls(Worse))).Result;

            Assert.AreEqual(25, r.Lines[ForgeLine.MaxDamage]);
            Assert.AreEqual(0.60, r.Lines[ForgeLine.Variance]);
            Assert.AreEqual(1.05, r.Lines[ForgeLine.AttackMod]);
        }

        [TestMethod]
        public void Forge_MainWeaponAlwaysKeepsHonesTinkersImbueAndDye()
        {
            var main = Dagger(10, 0.9, 1.0);
            main.HoneLevels[ForgeLine.MaxDamage] = 3;
            main.HoneMisfortune = 2;
            main.TinkerCount = 7;
            main.TinkerLog = "20,20,21";
            main.ImbuedEffect = 4;
            main.DyePalette = 0x040013B8;

            var feeder = Dagger(40, 0.2, 1.3);
            feeder.HoneLevels[ForgeLine.AttackMod] = 5;
            feeder.TinkerCount = 10;
            feeder.TinkerLog = "30,30";
            feeder.ImbuedEffect = 1;
            feeder.DyePalette = 0x04001DCD;

            // Every roll takes the feeder's (better) stats; none of the owned state may follow.
            var r = Forge(main, feeder, Config(), Script(Rolls(Better))).Result;

            Assert.AreEqual(40, r.Lines[ForgeLine.MaxDamage]);
            Assert.AreEqual(3, r.HoneLevel(ForgeLine.MaxDamage));
            Assert.AreEqual(0, r.HoneLevel(ForgeLine.AttackMod));
            Assert.AreEqual(2, r.HoneMisfortune);
            Assert.AreEqual(7, r.TinkerCount);
            Assert.AreEqual("20,20,21", r.TinkerLog);
            Assert.AreEqual(4, r.ImbuedEffect);
            Assert.AreEqual(0x040013B8, r.DyePalette);
        }

        [TestMethod]
        public void Forge_TakesHigherTierAndStricterRequirements_WithoutRolling()
        {
            var main = Dagger(20, 0.5, 1.1);
            main.Tier = 9;
            main.WieldDifficulty[1] = 300;
            main.WieldDifficulty[2] = 150;
            var feeder = Dagger(20, 0.5, 1.1);
            feeder.Tier = 11;
            feeder.WieldDifficulty[1] = 400;
            feeder.WieldDifficulty[3] = 500;

            // Worse branch everywhere: requirements must still come out at the maximum.
            var o = Forge(main, feeder, Config(), Script(Rolls(Worse)));

            Assert.AreEqual(11, o.Result.Tier);
            Assert.AreEqual(400, o.Result.WieldDifficulty[1]);
            Assert.AreEqual(150, o.Result.WieldDifficulty[2]);
            Assert.AreEqual(500, o.Result.WieldDifficulty[3]);
            Assert.AreEqual(InheritOrder.Length + 3, o.RngDraws.Count, "requirements and tier must not consume draws");
        }

        [TestMethod]
        public void Forge_QualityMissingOnOneParent_CountsAsZero()
        {
            var main = Dagger(20, 0.5, 1.1);
            main.Quality = 900;              // T11 A-
            var feeder = Dagger(20, 0.5, 1.1);
            feeder.Quality = null;           // T9: no quality

            Assert.AreEqual(900, Forge(main, feeder, Config(), Script(Rolls(Better, qualityRoll: Better))).Result.Quality);
            Assert.AreEqual(0, Forge(main, feeder, Config(), Script(Rolls(Better, qualityRoll: Worse))).Result.Quality);
        }

        [TestMethod]
        public void Forge_NeitherParentHasQuality_StaysNull()
        {
            var r = Forge(Dagger(20, 0.5, 1.1), Dagger(20, 0.5, 1.1), Config(), Script(Rolls(Better))).Result;
            Assert.IsNull(r.Quality);
        }

        [TestMethod]
        public void Forge_ZcGradeOnlyOnOneParent_LandsAsZeroOnTheWorseBranch()
        {
            var main = Dagger(20, 0.5, 1.1);
            main.ZcGrades[-11] = 700;
            var feeder = Dagger(20, 0.5, 1.1);

            var r = Forge(main, feeder, Config(), Script(With(new[] { Worse }))).Result;

            Assert.AreEqual(0, r.ZcGrades[-11]);
        }

        [TestMethod]
        public void Forge_SpellFamily_HigherLevelIsBetter_AndOneSidedFamilyCanBeLost()
        {
            const uint BloodDrinker = 154, HeartSeeker = 152;
            var main = Dagger(20, 0.5, 1.1);
            main.Spells[BloodDrinker] = new SpellEntry { Family = BloodDrinker, SpellId = 1612, Level = 5 };
            main.Spells[HeartSeeker] = new SpellEntry { Family = HeartSeeker, SpellId = 1592, Level = 6 };
            var feeder = Dagger(20, 0.5, 1.1);
            feeder.Spells[BloodDrinker] = new SpellEntry { Family = BloodDrinker, SpellId = 4395, Level = 8 };

            // Families are drawn in ascending order: Heart Seeker (152), then Blood Drinker (154).
            var kept = Forge(main, feeder, Config(), Script(With(new[] { Better, Better }))).Result;
            Assert.AreEqual(4395u, kept.Spells[BloodDrinker].SpellId, "better branch takes BD8");
            Assert.IsTrue(kept.Spells.ContainsKey(HeartSeeker), "a one-sided family survives the better branch");

            var lost = Forge(main, feeder, Config(), Script(With(new[] { Worse, Worse }))).Result;
            Assert.AreEqual(1612u, lost.Spells[BloodDrinker].SpellId, "worse branch takes BD5");
            Assert.IsFalse(lost.Spells.ContainsKey(HeartSeeker), "a one-sided family is lost on the worse branch");
        }

        [TestMethod]
        public void Forge_Package_TravelsWholeFromOneParent()
        {
            var main = Dagger(20, 0.5, 1.1);
            main.Packages[1] = new TraitPackage { Key = 1, Score = 1.5, Value = "slayer:undead:1.5" };
            var feeder = Dagger(20, 0.5, 1.1);
            feeder.Packages[1] = new TraitPackage { Key = 1, Score = 2.0, Value = "slayer:olthoi:2.0" };

            Assert.AreEqual("slayer:olthoi:2.0", Forge(main, feeder, Config(), Script(With(new[] { Better }))).Result.Packages[1].Value);
        }

        [TestMethod]
        public void Forge_Spark_AddsOneHoneLevelOnAPresentLine()
        {
            var main = Dagger(20, 0.5, 1.1);
            // Eligible lines in order: MaxDamage, Variance, AttackMod. 0.5 * 3 = 1.5 -> index 1 = Variance.
            var o = Forge(main, Dagger(20, 0.5, 1.1), Config(), Script(With(new double[0], MainElement, 0.0, 0.5)));

            Assert.IsTrue(o.Spark);
            Assert.AreEqual(ForgeLine.Variance, o.SparkLine);
            Assert.AreEqual(1, o.Result.HoneLevel(ForgeLine.Variance));
        }

        [TestMethod]
        public void Forge_Spark_AtHoneCap_DoesNothingAndDrawsNoLine()
        {
            var main = Dagger(20, 0.5, 1.1);
            main.HoneLevels[ForgeLine.MaxDamage] = 10;

            var o = Forge(main, Dagger(20, 0.5, 1.1), Config(), Script(Rolls(Better, spark: 0.0)));

            Assert.IsFalse(o.Spark);
            Assert.AreEqual(10, o.Result.HoneTotal);
        }

        [TestMethod]
        public void Forge_CountsForgesFromTheMoreForgedParent()
        {
            var main = Dagger(20, 0.5, 1.1);
            main.ForgeCount = 2;
            var feeder = Dagger(20, 0.5, 1.1);
            feeder.ForgeCount = 5;
            Assert.AreEqual(6, Forge(main, feeder, Config(), Script(Rolls(Better))).Result.ForgeCount);
        }

        [TestMethod]
        public void Forge_ReplayingTheDraws_ReproducesTheOutcome()
        {
            var main = Dagger(30, 0.6, 1.05);
            main.Quality = 850;
            main.ZcGrades[-12] = 400;
            var feeder = Dagger(25, 0.4, 1.15);
            feeder.Quality = 950;

            var rng = new Random(1234);
            var first = Forge(main, feeder, Config(), () => rng.NextDouble());
            var replay = Forge(main, feeder, Config(), Script(first.RngDraws.ToArray()));

            CollectionAssert.AreEqual(first.RngDraws, replay.RngDraws);
            CollectionAssert.AreEquivalent(first.Result.Lines.ToList(), replay.Result.Lines.ToList());
            Assert.AreEqual(first.Result.Quality, replay.Result.Quality);
            Assert.AreEqual(first.Result.ZcGrades[-12], replay.Result.ZcGrades[-12]);
        }

        [TestMethod]
        public void Forge_DoesNotModifyItsInputs()
        {
            var main = Dagger(30, 0.6, 1.05);
            var feeder = Dagger(25, 0.4, 1.15);
            Forge(main, feeder, Config(), Script(Rolls(Worse, spark: 0.0).Append(0.0).ToArray()));

            Assert.AreEqual(30, main.Lines[ForgeLine.MaxDamage]);
            Assert.AreEqual(0, main.HoneTotal);
            Assert.AreEqual(0, main.ForgeCount);
        }

        // ------------------------------------------------------------ roll modes

        [TestMethod]
        public void ParseRollMode_ReadsTheThreeModes_AndDefaultsToPick()
        {
            Assert.AreEqual(RollMode.Between, ParseRollMode("between"));
            Assert.AreEqual(RollMode.BestOfTwo, ParseRollMode("best-of-two"));
            Assert.AreEqual(RollMode.BestOfTwo, ParseRollMode(" BestOfTwo "));
            Assert.AreEqual(RollMode.Pick, ParseRollMode("pick"));
            Assert.AreEqual(RollMode.Pick, ParseRollMode(null));
            Assert.AreEqual(RollMode.Pick, ParseRollMode("full@typo"));
        }

        [TestMethod]
        public void Forge_Between_LandsBetweenTheTwoValues_RollIsTheShareTowardTheBetter()
        {
            var c = Config();
            c.RollMode = RollMode.Between;
            var main = Dagger(damage: 30, variance: 0.60, attack: 1.00);
            var feeder = Dagger(damage: 20, variance: 0.40, attack: 1.20);

            var half = Forge(main, feeder, c, Script(Rolls(0.5))).Result;
            Assert.AreEqual(25, half.Lines[ForgeLine.MaxDamage], "whole-number line, rounded");
            Assert.AreEqual(0.50, half.Lines[ForgeLine.Variance], 1e-9);
            Assert.AreEqual(1.10, half.Lines[ForgeLine.AttackMod], 1e-9);

            var low = Forge(main, feeder, c, Script(Rolls(0.0))).Result;
            Assert.AreEqual(20, low.Lines[ForgeLine.MaxDamage], "roll 0 = the worse value");
            Assert.AreEqual(0.60, low.Lines[ForgeLine.Variance], 1e-9, "for variance the worse value is the higher one");

            var high = Forge(main, feeder, c, Script(Rolls(0.999999))).Result;
            Assert.AreEqual(30, high.Lines[ForgeLine.MaxDamage]);
            Assert.AreEqual(0.40, high.Lines[ForgeLine.Variance], 1e-4);
        }

        [TestMethod]
        public void Forge_BestOfTwo_UsesTheSquareRootOfTheRoll()
        {
            var c = Config();
            c.RollMode = RollMode.BestOfTwo;
            var main = Dagger(damage: 30, variance: 0.60, attack: 1.00);
            var feeder = Dagger(damage: 20, variance: 0.40, attack: 1.20);

            // sqrt(0.25) = 0.5: half way, where Between would be a quarter of the way
            var r = Forge(main, feeder, c, Script(Rolls(0.25))).Result;
            Assert.AreEqual(25, r.Lines[ForgeLine.MaxDamage]);
            Assert.AreEqual(1.10, r.Lines[ForgeLine.AttackMod], 1e-9);
        }

        [TestMethod]
        public void Forge_Between_BlendsQualityAndGrades_ButStillPicksSpells()
        {
            var c = Config();
            c.RollMode = RollMode.Between;
            const uint BloodDrinker = 154;
            var main = Dagger(20, 0.5, 1.1);
            main.Quality = 900;
            main.ZcGrades[-11] = 800;
            main.Spells[BloodDrinker] = new SpellEntry { Family = BloodDrinker, SpellId = 1612, Level = 5 };
            var feeder = Dagger(20, 0.5, 1.1);
            feeder.ZcGrades[-11] = 400;
            feeder.Spells[BloodDrinker] = new SpellEntry { Family = BloodDrinker, SpellId = 4395, Level = 8 };

            // lines, quality 0.5, grade 0.5, spell family (better branch), element, spark
            var draws = Enumerable.Repeat(0.5, InheritOrder.Length).Append(0.5).Append(0.5).Append(Better).Append(MainElement).Append(NoSpark).ToArray();
            var o = Forge(main, feeder, c, Script(draws));

            Assert.AreEqual(450, o.Result.Quality, "missing quality counts as 0, so half way is 450");
            Assert.AreEqual(600, o.Result.ZcGrades[-11]);
            Assert.AreEqual(4395u, o.Result.Spells[BloodDrinker].SpellId, "a spell cannot be blended: the higher level is picked");
            Assert.IsTrue(o.Picks.First(p => p.Kind == PickKind.Quality).Blended);
            Assert.IsFalse(o.Picks.First(p => p.Kind == PickKind.Spell).Blended);
        }

        [TestMethod]
        public void Forge_Between_LineOnOnlyOneWeapon_IsKeptOrLost_NotBlended()
        {
            var c = Config();
            c.RollMode = RollMode.Between;
            var main = Dagger(20, 0.5, 1.1);
            main.Lines[ForgeLine.MagicDefense] = 1.05;
            var feeder = Dagger(20, 0.5, 1.1);

            Assert.AreEqual(1.05, Forge(main, feeder, c, Script(Rolls(Better))).Result.Lines[ForgeLine.MagicDefense]);
            Assert.IsFalse(Forge(main, feeder, c, Script(Rolls(Worse))).Result.Lines.ContainsKey(ForgeLine.MagicDefense));
        }

        [TestMethod]
        public void Forge_AnyRollMode_ConsumesTheSameNumberOfDraws()
        {
            foreach (var mode in new[] { RollMode.Pick, RollMode.Between, RollMode.BestOfTwo })
            {
                var c = Config();
                c.RollMode = mode;
                var o = Forge(Dagger(30, 0.6, 1.0), Dagger(20, 0.4, 1.2), c, Script(Rolls(0.3)));
                Assert.AreEqual(InheritOrder.Length + 3, o.RngDraws.Count, mode.ToString());
            }
        }

        // ------------------------------------------------------------ honing

        [TestMethod]
        public void HoneCost_DoublesPerTotalLevel()
        {
            Assert.AreEqual(50_000_000L, HoneCost(0, Config()));
            Assert.AreEqual(100_000_000L, HoneCost(1, Config()));
            Assert.AreEqual(25_600_000_000L, HoneCost(9, Config()));
        }

        [TestMethod]
        public void HoneChance_StepsDownToTheFloor_ThenMisfortuneAndFluxAddOnTop()
        {
            Assert.AreEqual(0.60, HoneChance(0, 0, 0, Config()), 1e-9);
            Assert.AreEqual(0.40, HoneChance(4, 0, 0, Config()), 1e-9);
            Assert.AreEqual(0.15, HoneChance(9, 0, 0, Config()), 1e-9);
            Assert.AreEqual(0.15, HoneChance(20, 0, 0, Config()), 1e-9, "floor");
            Assert.AreEqual(0.30, HoneChance(9, 3, 0, Config()), 1e-9, "3 failures = +15%");
            Assert.AreEqual(0.40, HoneChance(9, 3, 0.10, Config()), 1e-9, "flux");
            Assert.AreEqual(1.0, HoneChance(0, 50, 0, Config()), 1e-9, "clamped");
        }

        [TestMethod]
        public void Hone_Success_AddsALevelAndResetsMisfortune()
        {
            var w = Dagger(20, 0.5, 1.1);
            w.HoneMisfortune = 3;

            var o = Hone(w, ForgeLine.AttackMod, 0, Config(), Script(0.0));

            Assert.IsTrue(o.Success);
            Assert.AreEqual(50_000_000L, o.Cost);
            Assert.AreEqual(1, o.Result.HoneLevel(ForgeLine.AttackMod));
            Assert.AreEqual(0, o.Result.HoneMisfortune);
            Assert.AreEqual(3, w.HoneMisfortune, "input untouched");
        }

        [TestMethod]
        public void Hone_Failure_KeepsTheWeaponAndAddsMisfortune()
        {
            var w = Dagger(20, 0.5, 1.1);
            var o = Hone(w, ForgeLine.AttackMod, 0, Config(), Script(0.99));

            Assert.IsFalse(o.Success);
            Assert.AreEqual(0, o.Result.HoneTotal);
            Assert.AreEqual(1, o.Result.HoneMisfortune);
            Assert.AreEqual(20, o.Result.Lines[ForgeLine.MaxDamage]);
        }

        [TestMethod]
        public void Hone_Refusals_CostNothingAndDrawNothing()
        {
            var capped = Dagger(20, 0.5, 1.1);
            capped.HoneLevels[ForgeLine.MaxDamage] = 10;

            Assert.AreEqual(HoneRefusal.Capped, Hone(capped, ForgeLine.AttackMod, 0, Config(), Script()).Refused);
            Assert.AreEqual(HoneRefusal.NotHonable, Hone(Dagger(20, 0.5, 1.1), ForgeLine.Spellcraft, 0, Config(), Script()).Refused);
            Assert.AreEqual(HoneRefusal.LineMissing, Hone(Dagger(20, 0.5, 1.1), ForgeLine.MagicDefense, 0, Config(), Script()).Refused);
        }

        [TestMethod]
        public void HonedValue_IsMeasuredAgainstTheBaseRoll_NotCompounded()
        {
            Assert.AreEqual(110.0, HonedValue(ForgeLine.MaxDamage, 100, 5, Config()), 1e-9);
            Assert.AreEqual(1.25 * 1.2, HonedValue(ForgeLine.AttackMod, 1.2, 5, Config()), 1e-9);
        }

        [TestMethod]
        public void HonedValue_LowerIsBetterLines_Shrink_AndNeverGoNegative()
        {
            Assert.AreEqual(0.4, HonedValue(ForgeLine.Variance, 0.5, 4, Config()), 1e-9);
            Assert.AreEqual(0.0, HonedValue(ForgeLine.Speed, 40, 25, Config()), 1e-9);
        }

        [TestMethod]
        public void ExpectedAttempts_CountsMisfortune()
        {
            var c = Config();
            c.HoneBaseChance = 0.5;
            c.HoneMisfortuneStep = 0.5;
            // p0 = 0.5, p1 = 1.0: E = 1 + 0.5.
            Assert.AreEqual(1.5, ExpectedAttempts(0, c), 1e-9);

            c.HoneMisfortuneStep = 0;
            c.HoneMinChance = 0;
            c.HoneBaseChance = 0;
            Assert.IsTrue(double.IsPositiveInfinity(ExpectedAttempts(0, c)));
        }

        [TestMethod]
        public void UnbindFee_IsAFractionOfExpectedHoneSpend_WithAFloor()
        {
            var c = Config();
            var fresh = Dagger(20, 0.5, 1.1);
            Assert.AreEqual(1_000_000_000L, UnbindFee(fresh, c), "no hones: the floor");

            var maxed = Dagger(20, 0.5, 1.1);
            maxed.HoneLevels[ForgeLine.MaxDamage] = 10;
            var expected = (long)Math.Round(0.25 * ExpectedCostToReach(10, c), MidpointRounding.AwayFromZero);
            Assert.AreEqual(expected, UnbindFee(maxed, c));
            Assert.IsTrue(expected > 1_000_000_000L);
        }
    }
}
