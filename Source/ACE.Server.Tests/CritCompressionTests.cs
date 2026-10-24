using System;

using ACE.Server.Entity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Crit compression (owner 2026-10-21): a crit on a Zone Control monster lands at raw^p times a normal hit, p read from
    /// the monster's Crit Damage Resist so the stack its row was solved for crits at exactly the target ratio, never below the
    /// floor. Numbers are the live T15 Regular row shape (DRR mod 0.25 normal; CDR solved for a ~281x BiS stack).
    /// </summary>
    [TestClass]
    public class CritCompressionTests
    {
        private const double Target = 3.0;
        private const double Floor = 1.5;
        private const double Normal = 0.25;        // damage resist modifier on a normal hit
        private const double BisStack = 281.0;     // the stack the row was solved for
        private static readonly double Crit = Normal * Target / BisStack;   // the crit modifier that row gives today

        private static double TodayRatio(double raw) => raw * Crit / Normal;
        private static double NewRatio(double raw) => CritCompression.Ratio(raw, Normal, Crit, Target, Floor).Value;

        [TestMethod]
        public void BisStack_StillCritsAtTheTarget()
        {
            Assert.AreEqual(Target, NewRatio(BisStack), 1e-9);
            Assert.AreEqual(Target, TodayRatio(BisStack), 1e-9);
        }

        [TestMethod]
        public void BelowBis_CritIsAboveANormalHit_WhereToday_ItWasNot()
        {
            // a third of the BiS stack crit at exactly 1x before - the live screenshot
            var raw = BisStack / 3;
            Assert.AreEqual(1.0, TodayRatio(raw), 1e-9);
            Assert.IsTrue(NewRatio(raw) > 2.0, $"got {NewRatio(raw)}");
            Assert.IsTrue(NewRatio(raw) < Target);
        }

        [TestMethod]
        public void SmallStack_TakesTheFloor()
        {
            Assert.AreEqual(Floor, NewRatio(2.0), 1e-9);
            Assert.IsTrue(TodayRatio(2.0) < 0.05);
        }

        [TestMethod]
        public void Floor_NeverAboveTheUnresistedCrit()
        {
            // a retail-path crit only ~1% above a normal hit stays that, it is not lifted to 1.5x
            Assert.AreEqual(1.011, CritCompression.Ratio(1.011, Normal, Crit, Target, Floor).Value, 1e-3);
        }

        [TestMethod]
        public void Floor_NeverAboveTheTarget()
        {
            Assert.AreEqual(Target, CritCompression.Ratio(60, Normal, Crit, Target, 1000).Value, 1e-9);
            Assert.AreEqual(Target, CritCompression.Ratio(60, Normal, Crit, Target, double.PositiveInfinity).Value, 1e-9);
            Assert.AreEqual(NewRatio(60), CritCompression.Ratio(60, Normal, Crit, Target, double.NaN).Value, 1e-9);
        }

        [TestMethod]
        public void NoFloor_StillAlwaysABonusAboveRawOne()
        {
            var ratio = CritCompression.Ratio(2.0, Normal, Crit, Target, 0).Value;
            Assert.IsTrue(ratio > 1.0 && ratio < Floor, $"got {ratio}");
        }

        [TestMethod]
        public void EveryStepOfCritGearStillAdds()
        {
            var last = 0.0;
            foreach (var raw in new[] { 10.0, 30, 60, 111, 200, 281, 400, 650, 2000 })
            {
                var ratio = NewRatio(raw);
                Assert.IsTrue(ratio >= last, $"raw {raw}: {ratio} < {last}");
                last = ratio;
            }
        }

        [TestMethod]
        public void AboveBis_NeverLowerThanToday()
        {
            foreach (var raw in new[] { BisStack, BisStack * 2, BisStack * 10 })
                Assert.AreEqual(TodayRatio(raw), NewRatio(raw), 1e-9, $"raw {raw}");
        }

        [TestMethod]
        public void NoStack_EverCritsBelowToday()
        {
            foreach (var raw in new[] { 1.0, 2, 10, 30, 60, 111, 200, 281, 400, 650, 2000 })
                Assert.IsTrue(NewRatio(raw) >= TodayRatio(raw) - 1e-9, $"raw {raw}: {NewRatio(raw)} < {TodayRatio(raw)}");
        }

        [TestMethod]
        public void ResistMod_GivesTheRatioTimesANormalHit()
        {
            var raw = 60.0;
            var mod = CritCompression.ResistMod(raw, Normal, Crit, Target, Floor);
            // crit damage = max normal x raw x mod; a normal hit = max normal x Normal
            Assert.AreEqual(NewRatio(raw), raw * mod / Normal, 1e-4);
        }

        [TestMethod]
        public void NoCritDamageResist_LeavesTheCritAsItWas()
        {
            Assert.IsNull(CritCompression.Ratio(60, Normal, Normal, Target, Floor));
            Assert.AreEqual((float)Normal, CritCompression.ResistMod(60, Normal, Normal, Target, Floor));
        }

        [TestMethod]
        public void BadNumbers_LeaveTheCritAsItWas()
        {
            foreach (var raw in new[] { 0.0, -1, double.NaN, double.PositiveInfinity })
                Assert.AreEqual((float)Crit, CritCompression.ResistMod(raw, Normal, Crit, Target, Floor), $"raw {raw}");
            Assert.AreEqual((float)Crit, CritCompression.ResistMod(60, double.NaN, Crit, Target, Floor));
            Assert.AreEqual((float)Crit, CritCompression.ResistMod(60, Normal, Crit, 1.0, Floor));
            Assert.AreEqual((float)Crit, CritCompression.ResistMod(60, Normal, Crit, double.NaN, Floor));
        }

        [TestMethod]
        public void TheTierRowSetsTheCurve_EachTiersBisLandsOnTarget()
        {
            foreach (var bis in new[] { 118.5, 193.0, 281.0, 650.0 })
            {
                var crit = Normal * Target / bis;
                Assert.AreEqual(Target, CritCompression.Ratio(bis, Normal, crit, Target, Floor).Value, 1e-9, $"bis {bis}");
            }
        }
    }
}
