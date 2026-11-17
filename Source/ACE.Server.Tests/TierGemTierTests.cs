using ACE.Server.Managers.ZoneControl;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Reward tier (2026-11-17): a player is rewarded no higher than the highest Tou Tou Endgame Portal Gem they own.
    /// These cover the gem id -> tier map the heartbeat uses (v11 777700029, v12-v25 777705120-777705133).
    /// </summary>
    [TestClass]
    public class TierGemTierTests
    {
        [TestMethod]
        public void GemTier_MapsEveryTierGem()
        {
            Assert.AreEqual(11, TierHitGate.GemTier(777700029));
            Assert.AreEqual(12, TierHitGate.GemTier(777705120));
            Assert.AreEqual(13, TierHitGate.GemTier(777705121));
            Assert.AreEqual(14, TierHitGate.GemTier(777705122));
            Assert.AreEqual(16, TierHitGate.GemTier(777705124));
            Assert.AreEqual(25, TierHitGate.GemTier(777705133));
        }

        [TestMethod]
        public void GemTier_IgnoresOtherItems()
        {
            Assert.AreEqual(0, TierHitGate.GemTier(777705119), "just below the v12 gem");
            Assert.AreEqual(0, TierHitGate.GemTier(777705134), "just past the v25 gem");
            Assert.AreEqual(0, TierHitGate.GemTier(777705110), "Tou Tou Tier 22 is not a gem");
            Assert.AreEqual(0, TierHitGate.GemTier(777707002), "Lesser Effort Valley of Death gem");
            Assert.AreEqual(0, TierHitGate.GemTier(0));
        }

        [TestMethod]
        public void RewardTier_CappedAtGem_FlooredAtT11()
        {
            Assert.AreEqual(13, TierHitGate.RewardTier(13, 13), "Reawakener: hits T13, owns the T13 gem");
            Assert.AreEqual(12, TierHitGate.RewardTier(14, 12), "hits T14, best gem T12");
            Assert.AreEqual(11, TierHitGate.RewardTier(13, 0), "no gem: T11 needs none");
            Assert.AreEqual(11, TierHitGate.RewardTier(11, 0));
            Assert.AreEqual(13, TierHitGate.RewardTier(13, 16), "a gem above the hit tier never raises it");
            Assert.AreEqual(0, TierHitGate.RewardTier(0, 0), "below T11 stays below");
            Assert.AreEqual(14, TierHitGate.RewardTier(14, -1), "not looked yet: the hit tier");
        }

        [TestMethod]
        public void HighestGemTier_TakesTheBestGem()
        {
            Assert.AreEqual(0, TierHitGate.HighestGemTier(null));
            Assert.AreEqual(0, TierHitGate.HighestGemTier(new uint[] { 1, 2, 777707001 }));
            Assert.AreEqual(13, TierHitGate.HighestGemTier(new uint[] { 777700029, 777705121, 777705120, 12345 }));
        }
    }
}
