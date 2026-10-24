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
        // -- Prestige Coins per kill --

        [TestMethod]
        public void Defaults_PrestigeCoinsPerKill()
        {
            Assert.AreEqual(0.05, ServerConfig.zc_pcoin_kill_base.Default, 1e-9);
            Assert.AreEqual(1.20, ServerConfig.zc_pcoin_kill_tier_step.Default, 1e-9);
        }

        [TestMethod]
        public void ZoneValue_PlusTwentyPctPerTier()
        {
            Assert.AreEqual(0.050, TierRewardScale.ZoneValue(11, 0.05, 1.2), 1e-9);
            Assert.AreEqual(0.060, TierRewardScale.ZoneValue(12, 0.05, 1.2), 1e-9);
            Assert.AreEqual(0.072, TierRewardScale.ZoneValue(13, 0.05, 1.2), 1e-9);
            Assert.AreEqual(0.0864, TierRewardScale.ZoneValue(14, 0.05, 1.2), 1e-9);
            Assert.AreEqual(0.10368, TierRewardScale.ZoneValue(15, 0.05, 1.2), 1e-9);
        }

        [TestMethod]
        public void ZoneValue_OnlyT11ToT25()
        {
            Assert.AreEqual(0.0, TierRewardScale.ZoneValue(10, 0.05, 1.2), "T10 / Valley of Death pays nothing");
            Assert.AreEqual(0.0, TierRewardScale.ZoneValue(0, 0.05, 1.2));
            Assert.AreEqual(0.0, TierRewardScale.ZoneValue(26, 0.05, 1.2));
            Assert.AreEqual(0.0, TierRewardScale.ZoneValue(13, 0, 1.2), "a base of 0 is off");
            Assert.AreEqual(0.0, TierRewardScale.ZoneValue(13, double.NaN, 1.2));
            Assert.AreEqual(0.05, TierRewardScale.ZoneValue(13, 0.05, 0), 1e-9, "a bad step counts as 1");
        }

        [TestMethod]
        public void AddCoins_CarriesTheFraction()
        {
            var (whole, frac) = TierRewardScale.AddCoins(0, 0.05);
            Assert.AreEqual(0, whole);
            Assert.AreEqual(0.05, frac, 1e-12);

            (whole, frac) = TierRewardScale.AddCoins(0.97, 0.05);
            Assert.AreEqual(1, whole);
            Assert.AreEqual(0.02, frac, 1e-9);

            (whole, frac) = TierRewardScale.AddCoins(0.5, 2.75);
            Assert.AreEqual(3, whole);
            Assert.AreEqual(0.25, frac, 1e-9);
        }

        [TestMethod]
        public void AddCoins_TwentyKillsMakeOneCoin()
        {
            double carried = 0;
            long banked = 0;
            for (var i = 0; i < 20; i++)
            {
                var (whole, frac) = TierRewardScale.AddCoins(carried, 0.05);
                banked += whole;
                carried = frac;
            }
            // 20 x 0.05 in floating point may land a hair under 1; the 21st kill must then finish the coin
            if (banked == 0)
                banked += TierRewardScale.AddCoins(carried, 0.05).Whole;
            Assert.AreEqual(1, banked);
        }

        [TestMethod]
        public void AddCoins_BadInputsMintNothing()
        {
            Assert.AreEqual((0L, 0.0), TierRewardScale.AddCoins(5.0, 0), "a hand-edited carry of 5 is not 5 coins");
            Assert.AreEqual((0L, 0.0), TierRewardScale.AddCoins(double.NaN, 0));
            Assert.AreEqual((0L, 0.0), TierRewardScale.AddCoins(-3, 0));
            Assert.AreEqual(0L, TierRewardScale.AddCoins(0.5, -10).Whole);
            Assert.AreEqual(0L, TierRewardScale.AddCoins(0.5, double.PositiveInfinity).Whole);
        }
    }
}
