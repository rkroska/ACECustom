using ACE.Server.Managers;
using ACE.Server.Managers.ZoneControl;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Tier rewards (owner 2026-10-24): the pure tier-dip rules (TierRewardScale) and the shipped defaults of its live
    /// settings. No DB, no world.
    /// </summary>
    [TestClass]
    public class TierRewardScaleTests
    {
        // -- shipped defaults --

        [TestMethod]
        public void Defaults_TierDip()
        {
            Assert.IsTrue(ServerConfig.zc_tier_dip_enabled.Default);
            Assert.AreEqual(0.15, ServerConfig.zc_tier_dip_step.Default, 1e-9);
            Assert.AreEqual(0.0, ServerConfig.zc_tier_dip_floor.Default, 1e-9);
        }

        // -- credit --

        [TestMethod]
        public void Credit_StepsDownPerTierBelow()
        {
            Assert.AreEqual(1.00, TierRewardScale.Credit(13, 13, 0.15, 0), 1e-9);
            Assert.AreEqual(0.85, TierRewardScale.Credit(13, 12, 0.15, 0), 1e-9);
            Assert.AreEqual(0.70, TierRewardScale.Credit(13, 11, 0.15, 0), 1e-9);
            Assert.AreEqual(0.55, TierRewardScale.Credit(14, 11, 0.15, 0), 1e-9);
            Assert.AreEqual(0.40, TierRewardScale.Credit(15, 11, 0.15, 0), 1e-9);
        }

        [TestMethod]
        public void Credit_FullAtOrAboveOwnTier()
        {
            Assert.AreEqual(1.0, TierRewardScale.Credit(13, 14, 0.15, 0), "above own tier (Zone Share)");
            Assert.AreEqual(1.0, TierRewardScale.Credit(13, 25, 0.15, 0));
        }

        [TestMethod]
        public void Credit_NeverBelowT11()
        {
            Assert.AreEqual(1.0, TierRewardScale.Credit(15, 10, 0.15, 0), "a T10 / VoD kill is never cut");
            Assert.AreEqual(1.0, TierRewardScale.Credit(15, 0, 0.15, 0), "retail");
            Assert.AreEqual(1.0, TierRewardScale.Credit(0, 11, 0.15, 0), "a player with no tier");
            Assert.AreEqual(1.0, TierRewardScale.Credit(10, 11, 0.15, 0));
        }

        [TestMethod]
        public void Credit_FloorAndClamp()
        {
            Assert.AreEqual(0.0, TierRewardScale.Credit(25, 11, 0.15, 0), "far below hits the 0 floor, never negative");
            Assert.AreEqual(0.25, TierRewardScale.Credit(25, 11, 0.15, 0.25), 1e-9);
            Assert.AreEqual(0.85, TierRewardScale.Credit(13, 12, 0.15, 0.25), 1e-9, "the floor only applies under it");
            Assert.AreEqual(1.0, TierRewardScale.Credit(25, 11, 0.15, 7), "a floor above 1 is clamped");
            Assert.AreEqual(0.0, TierRewardScale.Credit(25, 11, 0.15, -1), "a negative floor is clamped to 0");
        }

        [TestMethod]
        public void Credit_BadStepNeverCuts()
        {
            Assert.AreEqual(1.0, TierRewardScale.Credit(15, 11, 0, 0));
            Assert.AreEqual(1.0, TierRewardScale.Credit(15, 11, -0.15, 0), "a negative step never raises credit above 1");
            Assert.AreEqual(1.0, TierRewardScale.Credit(15, 11, double.NaN, 0));
            Assert.AreEqual(0.4, TierRewardScale.Credit(15, 11, 0.15, double.NaN), 1e-9, "a NaN floor counts as 0");
        }
    }
}
