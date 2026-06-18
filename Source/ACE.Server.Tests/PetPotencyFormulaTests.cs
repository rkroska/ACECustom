using ACE.Server.Entity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    [TestClass]
    public class PetPotencyFormulaTests
    {
        [DataTestMethod]
        [DataRow(0, 0, 0)]
        [DataRow(0, 344, 0)]
        [DataRow(100, 0, 0)]
        [DataRow(100, 1, 1)]
        [DataRow(100, 9, 1)]
        [DataRow(100, 10, 1)]
        [DataRow(100, 11, 2)]
        [DataRow(100, 344, 35)]
        [DataRow(200, 344, 35)]
        [DataRow(200, 1500, 150)]
        public void GetActivePotency_RespectsBondGateAndCap(int stored, int bond, int expectedActive)
        {
            Assert.AreEqual(expectedActive, PetPotencyMath.GetActivePotency(stored, bond));
        }

        [TestMethod]
        public void GetActivePotency_Disabled_ReturnsZero()
        {
            Assert.AreEqual(0, PetPotencyMath.GetActivePotency(100, 344, potencyEnabled: false));
        }

        [DataTestMethod]
        [DataRow(0, 1.0f)]
        [DataRow(35, 1.7f)]
        [DataRow(50, 2.0f)]
        [DataRow(150, 4.0f)]
        public void GetBodyPartDamageMult_MatchesSpec(int active, float expectedMult)
        {
            Assert.AreEqual(expectedMult, PetPotencyMath.GetBodyPartDamageMult(active), 0.001f);
        }

        [DataTestMethod]
        [DataRow(0, 10)]
        [DataRow(1, 20)]
        [DataRow(49, 500)]
        public void GetUpgradeCost_IsQuadraticAtDefaultExponent(int currentStored, long expectedCost)
        {
            Assert.AreEqual(expectedCost, PetPotencyMath.GetUpgradeCost(currentStored));
        }


        [DataTestMethod]
        [DataRow(50, 0)]
        [DataRow(75, 25)]
        [DataRow(100, 50)]
        [DataRow(150, 100)]
        public void GetBondStrainRating_WhenEnabled(int active, int expectedStrain)
        {
            Assert.AreEqual(expectedStrain, PetPotencyMath.GetBondStrainRating(active, strainEnabled: true));
        }

        [TestMethod]
        public void GetExpectedResiduePerKill_T10TenPercentShare()
        {
            // 10% pet share × 1.5 T10 tier default ≈ 0.15 expected echoes/kill
            Assert.AreEqual(0.15, PetPotencyMath.GetExpectedResiduePerKill(0.10, 1.5), 0.0001);
        }

        [DataTestMethod]
        [DataRow(0.0, 0.0, 0)]
        [DataRow(0.35, 0.2, 1)]   // roll < 0.35 → +1
        [DataRow(0.35, 0.5, 0)]   // roll >= 0.35 → 0
        [DataRow(2.4, 0.3, 3)]    // 2 + (0.3 < 0.4)
        [DataRow(2.4, 0.5, 2)]    // 2 + (0.5 >= 0.4)
        [DataRow(3.0, 0.99, 3)]
        public void RoundResidueDropAmount_ProbabilisticRounding(double expected, double roll, int awarded)
        {
            Assert.AreEqual(awarded, PetPotencyMath.RoundResidueDropAmount(expected, roll));
        }

        [TestMethod]
        public void GetTotalUpgradeCost_Potency50_AtDefaultCostBase()
        {
            Assert.AreEqual(12_750, PetPotencyMath.GetTotalUpgradeCost(50, costBase: 10));
        }

        [TestMethod]
        public void GetTotalUpgradeCost_Potency50_AtLaunchCostBase()
        {
            Assert.AreEqual(25_500, PetPotencyMath.GetTotalUpgradeCost(50, costBase: 20));
        }

        [DataTestMethod]
        [DataRow(100, false, false, 5.0)]   // (5 + 5) * 0.5
        [DataRow(200, false, false, 7.5)]   // (5 + 10) * 0.5
        [DataRow(200, true, false, 5.625)]  // 7.5 * 0.75 hollow
        [DataRow(200, false, true, 37.5)]   // 7.5 * 5 shiny
        public void GetSalvageExpectedAmount_MatchesSpec(int level, bool hollow, bool shiny, double expected)
        {
            Assert.AreEqual(expected, PetPotencyMath.GetSalvageExpectedAmount(level, hollow, shiny), 0.0001);
        }

        [DataTestMethod]
        [DataRow(7.5, 0.3, 8)]
        [DataRow(2.4, 0.5, 2)]
        public void RoundResidueDropAmount_SalvageFractions(double expected, double roll, int awarded)
        {
            Assert.AreEqual(awarded, PetPotencyMath.RoundResidueDropAmount(expected, roll));
        }
    }
}
