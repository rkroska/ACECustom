using System.Linq;

using ACE.Server.Managers;
using ACE.Server.Managers.ZoneControl;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// T11+ rares: the pure rules (ZoneRare) and the shipped defaults of the live settings. No DB, no world.
    /// </summary>
    [TestClass]
    public class ZoneRareTests
    {
        // -- shipped defaults --

        [TestMethod]
        public void Defaults_BothTiersShipOff()
        {
            // .Default, not .Value: "ships off" is about the code default, whatever a test or a database has set since
            Assert.AreEqual(0, ServerConfig.zc_rare_pristine_odds.Default, "Pristine must ship off");
            Assert.AreEqual(0, ServerConfig.zc_rare_ascendant_odds.Default, "Ascendant must ship off");
        }

        [TestMethod]
        public void Defaults_BonusAndBelowTierShares()
        {
            Assert.AreEqual(1, ServerConfig.zc_rare_ascendant_tier_bonus.Default);
            Assert.AreEqual(0.40, ServerConfig.zc_rare_odds_one_tier_below.Default, 1e-9);
            Assert.AreEqual(0.10, ServerConfig.zc_rare_odds_two_tiers_below.Default, 1e-9);
        }

        // -- own tier --

        [TestMethod]
        public void OwnTier_IsHighestEnabledTierAtOrBelowThePlayer()
        {
            var all = Enumerable.Range(11, 15).ToArray();           // 11..25
            Assert.AreEqual(13, ZoneRare.OwnTier(13, all));
            Assert.AreEqual(13, ZoneRare.OwnTier(15, new[] { 11, 12, 13 }), "past the last populated tier");
            Assert.AreEqual(11, ZoneRare.OwnTier(12, new[] { 11, 13 }), "a hole in the zones");
            Assert.AreEqual(0, ZoneRare.OwnTier(10, all), "below T11");
            Assert.AreEqual(0, ZoneRare.OwnTier(15, new int[0]));
            Assert.AreEqual(0, ZoneRare.OwnTier(15, null));
            Assert.AreEqual(0, ZoneRare.OwnTier(15, new[] { 3, 10 }), "non-endgame variations never count");
            Assert.AreEqual(25, ZoneRare.OwnTier(26, new[] { 25, 26 }), "nothing past the top of the ladder counts");
        }

        // -- below-tier share --

        [TestMethod]
        public void BelowTierScale_Steps()
        {
            Assert.AreEqual(1.0, ZoneRare.BelowTierScale(13, 13, 0.4, 0.1));
            Assert.AreEqual(0.4, ZoneRare.BelowTierScale(13, 12, 0.4, 0.1));
            Assert.AreEqual(0.1, ZoneRare.BelowTierScale(13, 11, 0.4, 0.1));
            Assert.AreEqual(0.0, ZoneRare.BelowTierScale(14, 11, 0.4, 0.1), "three below never rolls");
            Assert.AreEqual(0.0, ZoneRare.BelowTierScale(25, 11, 0.4, 0.1));
            Assert.AreEqual(0.0, ZoneRare.BelowTierScale(12, 13, 0.4, 0.1), "a kill above own tier (hit gate off) never rolls");
            Assert.AreEqual(0.0, ZoneRare.BelowTierScale(0, 11, 0.4, 0.1), "a killer below T11 never rolls");
            Assert.AreEqual(0.0, ZoneRare.BelowTierScale(13, 10, 0.4, 0.1), "a kill below T11 never rolls");
        }

        [TestMethod]
        public void BelowTierScale_ClampsBadSettings()
        {
            Assert.AreEqual(1.0, ZoneRare.BelowTierScale(13, 12, 7.0, 0.1), "a share can never raise the odds");
            Assert.AreEqual(0.0, ZoneRare.BelowTierScale(13, 12, -1.0, 0.1));
            Assert.AreEqual(0.0, ZoneRare.BelowTierScale(13, 11, 0.4, double.NaN));
        }

        [TestMethod]
        public void BelowTierScale_OwnTierBeatsFarmingDown_AtTheOwnersKillRates()
        {
            // the point of the defaults: 300 kills an hour in your own tier must beat an overpowered 600 an hour below it
            const double own = 300, overpowered = 600;
            Assert.IsTrue(own * ZoneRare.BelowTierScale(13, 13, 0.40, 0.10) > overpowered * ZoneRare.BelowTierScale(13, 12, 0.40, 0.10));
            Assert.IsTrue(own * ZoneRare.BelowTierScale(13, 13, 0.40, 0.10) > overpowered * ZoneRare.BelowTierScale(13, 11, 0.40, 0.10));
        }

        // -- chance and pick --

        [TestMethod]
        public void ChancePerKill_OffAndScaled()
        {
            Assert.AreEqual(0.0, ZoneRare.ChancePerKill(0, 1.0), "0 = off");
            Assert.AreEqual(0.0, ZoneRare.ChancePerKill(-5, 1.0));
            Assert.AreEqual(0.0, ZoneRare.ChancePerKill(864000, 0.0));
            Assert.AreEqual(1.0 / 864000, ZoneRare.ChancePerKill(864000, 1.0), 1e-15);
            Assert.AreEqual(0.4 / 2160000, ZoneRare.ChancePerKill(2160000, 0.4), 1e-15);
            Assert.AreEqual(1.0, ZoneRare.ChancePerKill(1, 1.0), "1 in 1 always hits - the test setting");
            Assert.AreEqual(0.1, ZoneRare.ChancePerKill(10, 7.0), 1e-15, "a share above 1 never raises the odds");
        }

        [TestMethod]
        public void ChancePerKill_MatchesTheAgreedMonthlyRates()
        {
            // 20 characters x 24 h x 30 days x 300 kills an hour
            const double killsPerMonth = 20 * 24 * 30 * 300;
            Assert.AreEqual(5.0, killsPerMonth * ZoneRare.ChancePerKill(864000, 1.0), 1e-9);
            Assert.AreEqual(2.0, killsPerMonth * ZoneRare.ChancePerKill(2160000, 1.0), 1e-9);
        }

        [TestMethod]
        public void Pick_AscendantOutranksPristine_AndOneKillYieldsOne()
        {
            Assert.AreEqual(ZoneRareTier.None, ZoneRare.Pick(0.5, 0.5, 0.1, 0.1));
            Assert.AreEqual(ZoneRareTier.Pristine, ZoneRare.Pick(0.5, 0.05, 0.1, 0.1));
            Assert.AreEqual(ZoneRareTier.Ascendant, ZoneRare.Pick(0.05, 0.5, 0.1, 0.1));
            Assert.AreEqual(ZoneRareTier.Ascendant, ZoneRare.Pick(0.05, 0.05, 0.1, 0.1), "both hit: the rarer tier wins");
            Assert.AreEqual(ZoneRareTier.None, ZoneRare.Pick(0.0, 0.0, 0.0, 0.0), "off stays off even on a draw of exactly 0");
        }

        // -- the tier an Ascendant item is made at --

        [TestMethod]
        public void AscendantTier_KillTierPlusBonus_CappedAt25()
        {
            Assert.AreEqual(14, ZoneRare.AscendantTier(11, 3));
            Assert.AreEqual(18, ZoneRare.AscendantTier(15, 3), "crosses the T16 line");
            Assert.AreEqual(25, ZoneRare.AscendantTier(22, 3));
            Assert.AreEqual(25, ZoneRare.AscendantTier(24, 3), "never past the top of the ladder");
            Assert.AreEqual(25, ZoneRare.AscendantTier(25, 3));
            Assert.AreEqual(13, ZoneRare.AscendantTier(13, 0), "bonus 0 = the tier it dropped in");
            Assert.AreEqual(13, ZoneRare.AscendantTier(13, -4), "a negative setting never lowers it");
            Assert.AreEqual(25, ZoneRare.AscendantTier(11, 999), "a huge setting stops at 25");
            Assert.AreEqual(25, ZoneRare.AscendantTier(11, long.MaxValue));
            Assert.AreEqual(14, ZoneRare.AscendantTier(0, 3), "a kill tier below the ladder is clamped to T11");
            Assert.AreEqual(25, ZoneRare.AscendantTier(int.MaxValue, 3), "no overflow on a nonsense kill tier");
        }

        // -- the tier whose wield requirements an item carries --

        [TestMethod]
        public void GateTier_OrdinaryItems_UseTheirOwnTier()
        {
            for (var tier = 11; tier <= 25; tier++)
                Assert.AreEqual(tier, ZoneRare.GateTier(null, tier), "no stored gate tier = the item's own tier, exactly as before");
            Assert.AreEqual(0, ZoneRare.GateTier(null, 0));
            Assert.AreEqual(10, ZoneRare.GateTier(null, 10));
        }

        [TestMethod]
        public void GateTier_AscendantItems_UseTheTierTheyDroppedIn()
        {
            Assert.AreEqual(11, ZoneRare.GateTier(11, 14), "a T14 item found in T11 is wielded with T11 requirements");
            Assert.AreEqual(15, ZoneRare.GateTier(15, 18), "T18 item, T15 gate: the item / creature / life aug gates, not Triune");
            Assert.AreEqual(16, ZoneRare.GateTier(16, 19), "T19 item, T16 gate: the first Triune tier");
            Assert.AreEqual(22, ZoneRare.GateTier(22, 25), "T25 item, T22 gate: the T22 Triune gate");
        }

        [TestMethod]
        public void GateTier_CanOnlyLowerTheGate_NeverRaiseOrRemoveIt()
        {
            Assert.AreEqual(25, ZoneRare.GateTier(25, 25), "a T25 kill gives a T25 item: gate = item tier");
            Assert.AreEqual(14, ZoneRare.GateTier(14, 14), "bonus 0");
            Assert.AreEqual(14, ZoneRare.GateTier(20, 14), "a stored tier above the item's own is ignored");
            Assert.AreEqual(14, ZoneRare.GateTier(10, 14), "a stored tier below the ladder is ignored - it would remove the gate");
            Assert.AreEqual(14, ZoneRare.GateTier(0, 14));
            Assert.AreEqual(14, ZoneRare.GateTier(-3, 14));
            Assert.AreEqual(14, ZoneRare.GateTier(26, 14));
        }
    }
}
