using ACE.Server.WorldObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Pure-math contract of PetDevice.BreedingMath. The owner's website simulator mirrors these
    /// rules exactly, so any change here is a change to the design model.
    /// </summary>
    [TestClass]
    public class PetBreedingInheritanceTests
    {
        private const double HigherRoll = 0.10;   // < 0.55: pick the higher-effective parent
        private const double LowerRoll = 0.90;    // >= 0.55: pick the lower-effective parent

        [TestMethod]
        public void PicksParent1_HigherRoll_PicksHigherEffectiveParent()
        {
            Assert.IsTrue(PetDevice.BreedingMath.PicksParent1(100, 50, HigherRoll));
            Assert.IsFalse(PetDevice.BreedingMath.PicksParent1(50, 100, HigherRoll));
        }

        [TestMethod]
        public void PicksParent1_LowerRoll_PicksLowerEffectiveParent()
        {
            Assert.IsFalse(PetDevice.BreedingMath.PicksParent1(100, 50, LowerRoll));
            Assert.IsTrue(PetDevice.BreedingMath.PicksParent1(50, 100, LowerRoll));
        }

        [TestMethod]
        public void PicksParent1_Tie_TreatsDevice1AsHigher()
        {
            // Ties -> device1 counts as the "higher" parent: the 55% branch picks it, the 45% branch picks device2.
            Assert.IsTrue(PetDevice.BreedingMath.PicksParent1(70, 70, HigherRoll));
            Assert.IsFalse(PetDevice.BreedingMath.PicksParent1(70, 70, LowerRoll));
        }

        [TestMethod]
        public void PicksParent1_Boundary_0_55_IsLowerBranch()
        {
            Assert.IsTrue(PetDevice.BreedingMath.PicksParent1(100, 50, 0.549));
            Assert.IsFalse(PetDevice.BreedingMath.PicksParent1(100, 50, 0.55));
        }

        [TestMethod]
        public void Effective_IsGearPlusCountTimesStep()
        {
            Assert.AreEqual(30 + 3 * 10, PetDevice.BreedingMath.Effective(30, 3, 10));
            Assert.AreEqual(0, PetDevice.BreedingMath.Effective(0, 0, 10));
        }

        [TestMethod]
        public void InheritLine_ComparesOnEffective_AndIsAPackageDeal()
        {
            // Parent 1: gear 40, 0 muts (effective 40). Parent 2: gear 10, 4 muts x 10 (effective 50).
            // Parent 2 is higher on effective even though its gear is lower; the baby gets BOTH its
            // gear value and its count, never a mix.
            var (gear, count) = PetDevice.BreedingMath.InheritLine(40, 0, 10, 4, 10, HigherRoll);
            Assert.AreEqual(10, gear);
            Assert.AreEqual(4, count);

            (gear, count) = PetDevice.BreedingMath.InheritLine(40, 0, 10, 4, 10, LowerRoll);
            Assert.AreEqual(40, gear);
            Assert.AreEqual(0, count);
        }

        [TestMethod]
        public void InheritLine_Tie_PrefersDevice1OnHigherBranch()
        {
            var (gear, count) = PetDevice.BreedingMath.InheritLine(20, 1, 30, 0, 10, HigherRoll);
            Assert.AreEqual(20, gear);
            Assert.AreEqual(1, count);
        }

        [TestMethod]
        public void InheritGearOnly_UsesGearValueOnly()
        {
            Assert.AreEqual(25, PetDevice.BreedingMath.InheritGearOnly(25, 15, HigherRoll));
            Assert.AreEqual(15, PetDevice.BreedingMath.InheritGearOnly(25, 15, LowerRoll));
        }

        [TestMethod]
        public void InheritPotency_MissingStoredIsZero_AndCountTravelsWithStored()
        {
            // A parent with no stored potency is compared as 0, not as a default of 150.
            var (stored, count) = PetDevice.BreedingMath.InheritPotency(0, 0, 120, 2, HigherRoll);
            Assert.AreEqual(120, stored);
            Assert.AreEqual(2, count);

            (stored, count) = PetDevice.BreedingMath.InheritPotency(0, 0, 120, 2, LowerRoll);
            Assert.AreEqual(0, stored);
            Assert.AreEqual(0, count);
        }

        [TestMethod]
        public void InheritPotency_DoesNotAddCountToStored()
        {
            // Stored is already the complete effective value: 100 stored / 5 muts must NOT beat 110 stored / 0 muts.
            var (stored, count) = PetDevice.BreedingMath.InheritPotency(100, 5, 110, 0, HigherRoll);
            Assert.AreEqual(110, stored);
            Assert.AreEqual(0, count);
        }

        [TestMethod]
        [DataRow(0L, 0L, 0)]
        [DataRow(500L, 0L, 500)]
        [DataRow(0L, 300L, 300)]
        [DataRow(500L, 300L, 300)]
        [DataRow(200L, 300L, 200)]
        [DataRow(-5L, 300L, 300)]
        [DataRow(-5L, -1L, 0)]
        public void ResolvePotencyHardCap_IsSmallestPositive(long breedingCap, long maxStored, int expected)
        {
            Assert.AreEqual(expected, PetDevice.BreedingMath.ResolvePotencyHardCap(breedingCap, maxStored));
        }

        [TestMethod]
        public void PotencyMutationStep_BelowSoftCap_UsesFullStep()
        {
            Assert.AreEqual(25, PetDevice.BreedingMath.PotencyMutationStep(25, 100, 1000, 0));
        }

        [TestMethod]
        public void PotencyMutationStep_AtOrAboveSoftCap_QuartersStepMin1()
        {
            Assert.AreEqual(6, PetDevice.BreedingMath.PotencyMutationStep(25, 1000, 1000, 0));
            Assert.AreEqual(6, PetDevice.BreedingMath.PotencyMutationStep(25, 1500, 1000, 0));
            Assert.AreEqual(1, PetDevice.BreedingMath.PotencyMutationStep(3, 1000, 1000, 0));
        }

        [TestMethod]
        public void PotencyMutationStep_SoftCapZero_NeverDiminishes()
        {
            Assert.AreEqual(25, PetDevice.BreedingMath.PotencyMutationStep(25, 5000, 0, 0));
        }

        [TestMethod]
        public void PotencyMutationStep_ClampsToHardCap()
        {
            Assert.AreEqual(10, PetDevice.BreedingMath.PotencyMutationStep(25, 290, 1000, 300));
            Assert.AreEqual(0, PetDevice.BreedingMath.PotencyMutationStep(25, 300, 1000, 300));
            Assert.AreEqual(0, PetDevice.BreedingMath.PotencyMutationStep(25, 400, 1000, 300));
        }

        [TestMethod]
        public void PotencyMutationStep_SoftCapThenHardCap()
        {
            // Above the soft cap the step is 25/4 = 6, and a hard cap 2 above current clamps it to 2.
            Assert.AreEqual(2, PetDevice.BreedingMath.PotencyMutationStep(25, 1198, 1000, 1200));
        }

        [TestMethod]
        public void MutCritDerivations_MatchSummonRules()
        {
            Assert.AreEqual(8, PetDevice.BreedingMath.MutCritDamage(10));
            Assert.AreEqual(16, PetDevice.BreedingMath.MutCritResist(20));
            Assert.AreEqual(12, PetDevice.BreedingMath.MutCritDamageResist(20));
        }

        [TestMethod]
        public void MatchesBreedingArea_ZeroMeansAnywhere()
        {
            Assert.IsTrue(PetDevice.MatchesBreedingArea(0x013A02AE, 3, 0, -1));
            Assert.IsTrue(PetDevice.MatchesBreedingArea(0xA9B40021, 0, 0, -1));
        }

        [TestMethod]
        public void MatchesBreedingArea_16BitValueMatchesWholeLandblock()
        {
            Assert.IsTrue(PetDevice.MatchesBreedingArea(0x013A02AE, 3, 0x013A, -1));
            Assert.IsTrue(PetDevice.MatchesBreedingArea(0x013A0101, 3, 0x013A, -1));
            Assert.IsFalse(PetDevice.MatchesBreedingArea(0x016C02AE, 3, 0x013A, -1));
        }

        [TestMethod]
        public void MatchesBreedingArea_FullCellMatchesExactCellOnly()
        {
            Assert.IsTrue(PetDevice.MatchesBreedingArea(0x013A02AE, 3, 0x013A02AE, -1));
            Assert.IsFalse(PetDevice.MatchesBreedingArea(0x013A0101, 3, 0x013A02AE, -1));
        }

        [TestMethod]
        public void MatchesBreedingArea_VariantFilter()
        {
            Assert.IsTrue(PetDevice.MatchesBreedingArea(0x013A02AE, 3, 0x013A, 3));
            Assert.IsFalse(PetDevice.MatchesBreedingArea(0x013A02AE, 0, 0x013A, 3));
            Assert.IsFalse(PetDevice.MatchesBreedingArea(0x013A02AE, -1, 0x013A, 3));
            Assert.IsTrue(PetDevice.MatchesBreedingArea(0x013A02AE, -1, 0x013A, -1));
        }
    }
}
