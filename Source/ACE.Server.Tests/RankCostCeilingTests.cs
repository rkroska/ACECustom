using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Server.WorldObjects;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Rank cost ceiling (2026-10-19): the attribute / vital cost functions count up to the rank they price, so a wrapped
    /// rank (a lower rank than 0, or one past int.MaxValue, asked for by /attr or /xp) never ended them and hung the server.
    /// Past Player.MaxPricedRank they answer at once with an unaffordable cost.
    /// </summary>
    [TestClass]
    public class RankCostCeilingTests
    {
        /// <summary>A wrapped destination rank is refused at once instead of looping.</summary>
        [TestMethod]
        public void AttributeCost_WrappedDestinationRefused()
        {
            Assert.AreEqual(ulong.MaxValue, Player.GetXPDeltaCostByRank(uint.MaxValue, 0));
            Assert.AreEqual(ulong.MaxValue, Player.GetXPDeltaCostByRank(2_147_484_092, 445));
        }

        /// <summary>A wrapped current rank (a staff-set or corrupt rank) is refused at once instead of looping.</summary>
        [TestMethod]
        public void AttributeCost_WrappedCurrentRefused()
        {
            Assert.AreEqual(ulong.MaxValue, Player.GetXPDeltaCostByRank(1, uint.MaxValue));
            Assert.AreEqual(ulong.MaxValue, Player.GetXPDeltaCostByRank(Player.MaxPricedRank + 1, 0));
        }

        /// <summary>The vital cost function refuses wrapped ranks the same way.</summary>
        [TestMethod]
        public void VitalCost_WrappedRanksRefused()
        {
            Assert.AreEqual(ulong.MaxValue, Player.GetXPDeltaCostByRankForSecondary(uint.MaxValue, 0));
            Assert.AreEqual(ulong.MaxValue, Player.GetXPDeltaCostByRankForSecondary(1, uint.MaxValue));
            Assert.AreEqual(ulong.MaxValue, Player.GetXPDeltaCostByRankForSecondary(Player.MaxPricedRank + 1, 196));
        }
    }
}
