using System.Collections.Generic;
using System.Linq;

using ACE.Server.Managers.WeaponScaling;
using ACE.Server.Managers.ZoneControl;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// The "which tier is this player" helper built from the T11+ hit gate's own comparison
    /// (TierHitGate.MeetsTier / HighestTier). Pure counters and tier rows only - no DB, no player.
    /// The default ladder: Creature 4,000 +500/tier to 6,000 at T15; Item 2,000 at T11 then 2,500
    /// +500/tier to 4,000 at T15; Triune 500 at T16, +500/tier to 5,000 at T25.
    /// </summary>
    [TestClass]
    public class TierHitGateTests
    {
        private static List<WeaponScalingTier> Rows() => WeaponScalingManager.BuildDefaults().Tiers;

        [TestMethod]
        public void HighestTier_BelowT11_IsZero()
        {
            Assert.AreEqual(0, TierHitGate.HighestTier(3999, 2000, 0, Rows()), "one Creature aug short of T11");
            Assert.AreEqual(0, TierHitGate.HighestTier(4000, 1999, 0, Rows()), "one Item aug short of T11");
            Assert.AreEqual(0, TierHitGate.HighestTier(0, 0, 0, Rows()));
        }

        [TestMethod]
        public void HighestTier_ExactBoundaries_T11ThroughT15()
        {
            Assert.AreEqual(11, TierHitGate.HighestTier(4000, 2000, 0, Rows()));
            Assert.AreEqual(11, TierHitGate.HighestTier(4500, 2499, 0, Rows()), "Item one short of T12");
            Assert.AreEqual(11, TierHitGate.HighestTier(4499, 2500, 0, Rows()), "Creature one short of T12");
            Assert.AreEqual(12, TierHitGate.HighestTier(4500, 2500, 0, Rows()));
            Assert.AreEqual(13, TierHitGate.HighestTier(5000, 3000, 0, Rows()));
            Assert.AreEqual(14, TierHitGate.HighestTier(5500, 3500, 0, Rows()));
            Assert.AreEqual(15, TierHitGate.HighestTier(6000, 4000, 0, Rows()));
        }

        [TestMethod]
        public void HighestTier_TriuneCarriesTheLadderFromT16()
        {
            Assert.AreEqual(15, TierHitGate.HighestTier(6000, 4000, 499, Rows()), "one Triune short of T16");
            Assert.AreEqual(16, TierHitGate.HighestTier(6000, 4000, 500, Rows()));
            Assert.AreEqual(20, TierHitGate.HighestTier(6000, 4000, 2500, Rows()));
            Assert.AreEqual(24, TierHitGate.HighestTier(6000, 4000, 4999, Rows()));
            Assert.AreEqual(25, TierHitGate.HighestTier(6000, 4000, 5000, Rows()));
            Assert.AreEqual(25, TierHitGate.HighestTier(long.MaxValue, long.MaxValue, long.MaxValue, Rows()), "never past the top row");
        }

        [TestMethod]
        public void HighestTier_IsAContiguousClimb()
        {
            // T12's Item requirement is missed, so T13+ are never reached even with the Creature count for T15.
            Assert.AreEqual(11, TierHitGate.HighestTier(6000, 2000, 0, Rows()));

            // a hole in the table stops the climb at the tier below it
            var gap = Rows().Where(r => r.Tier != 13).ToList();
            Assert.AreEqual(12, TierHitGate.HighestTier(6000, 4000, 5000, gap));
        }

        [TestMethod]
        public void HighestTier_IgnoresRowsBelowT11_AndRowOrder()
        {
            // the stored table carries a T10 row; it must neither count as a tier nor block the climb
            var rows = Rows();
            rows.Add(new WeaponScalingTier { Tier = 10, Cap = 2000, MinWieldCreature = 999999 });
            rows.Reverse();
            Assert.AreEqual(12, TierHitGate.HighestTier(4500, 2500, 0, rows));
        }

        [TestMethod]
        public void HighestTier_IgnoresRowsPastTheTopOfTheLadder()
        {
            // "/weaponscale tier add 26" is accepted; nobody becomes tier 26
            var rows = Rows();
            rows.Add(new WeaponScalingTier { Tier = 26, Cap = 10000, MinWieldCreature = 6000, MinWieldTriune = 5500 });
            Assert.AreEqual(25, TierHitGate.HighestTier(6000, 4000, 9999, rows));
        }

        [TestMethod]
        public void HighestTier_NullOrEmptyTable_IsZero()
        {
            Assert.AreEqual(0, TierHitGate.HighestTier(6000, 4000, 5000, null));
            Assert.AreEqual(0, TierHitGate.HighestTier(6000, 4000, 5000, new List<WeaponScalingTier>()));
        }

        [TestMethod]
        public void MeetsTier_SkipsRequirementsOfZero_AndRejectsNull()
        {
            var t11 = Rows().Single(r => r.Tier == 11);
            Assert.AreEqual(0, t11.MinWieldTriune, "T11 asks for no Triune");
            Assert.IsTrue(TierHitGate.MeetsTier(4000, 2000, 0, t11));
            Assert.IsFalse(TierHitGate.MeetsTier(3999, 2000, 0, t11));
            Assert.IsFalse(TierHitGate.MeetsTier(4000, 2000, 0, null));

            var t16 = Rows().Single(r => r.Tier == 16);
            Assert.IsFalse(TierHitGate.MeetsTier(6000, 4000, 499, t16), "Triune shortfall alone blocks the tier");
            Assert.IsTrue(TierHitGate.MeetsTier(6000, 4000, 500, t16));

            // a row that asks for nothing is met by anyone
            Assert.IsTrue(TierHitGate.MeetsTier(0, 0, 0, new WeaponScalingTier { Tier = 11 }));
        }
    }
}
