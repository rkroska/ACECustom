using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Server.WorldObjects;

namespace ACE.Server.Tests
{
    /// <summary>
    /// XP overflow (2026-10-19): XP totals stop at the edge of a long instead of wrapping negative, and unassigned XP past
    /// the cap is saved in OverflowExperience (a long, exact) and moved back as XP is spent. (The rank cost ceiling has its
    /// own RankCostCeilingTests.)
    /// </summary>
    [TestClass]
    public class XpOverflowTests
    {
        /// <summary>Ordinary adds and subtracts are unchanged; null counts as 0.</summary>
        [TestMethod]
        public void AddXpClamped_NormalAdds()
        {
            Assert.AreEqual(150L, Player.AddXpClamped(100, 50));
            Assert.AreEqual(50L, Player.AddXpClamped(100, -50));
            Assert.AreEqual(100L, Player.AddXpClamped(100, 0));
            Assert.AreEqual(25L, Player.AddXpClamped(null, 25));
        }

        /// <summary>An add past long.MaxValue stops there instead of wrapping negative.</summary>
        [TestMethod]
        public void AddXpClamped_StopsAtMaxValue()
        {
            Assert.AreEqual(long.MaxValue, Player.AddXpClamped(long.MaxValue, 1));
            Assert.AreEqual(long.MaxValue, Player.AddXpClamped(long.MaxValue - 10, 10));
            Assert.AreEqual(long.MaxValue, Player.AddXpClamped(long.MaxValue - 10, 11));
            Assert.AreEqual(long.MaxValue, Player.AddXpClamped(long.MaxValue, long.MaxValue));

            // the live case: 8.99 quintillion unspent plus a 3.59 quintillion gain used to wrap to -5.87 quintillion
            Assert.AreEqual(long.MaxValue, Player.AddXpClamped(8_990_943_188_421_035_757, 3_585_148_986_649_039_951));
        }

        /// <summary>A subtract past long.MinValue stops there instead of wrapping positive.</summary>
        [TestMethod]
        public void AddXpClamped_StopsAtMinValue()
        {
            Assert.AreEqual(long.MinValue, Player.AddXpClamped(long.MinValue, -1));
            Assert.AreEqual(long.MinValue, Player.AddXpClamped(long.MinValue + 10, -11));
            Assert.AreEqual(long.MinValue + 1, Player.AddXpClamped(long.MinValue + 10, -9));
        }

        /// <summary>A gain that fits under the cap goes to the balance whole.</summary>
        [TestMethod]
        public void SplitXpAtCap_AllFitsBelowCap()
        {
            Assert.AreEqual((500L, 0L), Player.SplitXpAtCap(1_000, 500));
            Assert.AreEqual((10L, 0L), Player.SplitXpAtCap(long.MaxValue - 10, 10));
        }

        /// <summary>The part of a gain past the cap is split off for the bank, nothing lost.</summary>
        [TestMethod]
        public void SplitXpAtCap_ExtraGoesToOverflow()
        {
            Assert.AreEqual((10L, 5L), Player.SplitXpAtCap(long.MaxValue - 10, 15));
            Assert.AreEqual((0L, 42L), Player.SplitXpAtCap(long.MaxValue, 42));

            // the live case: the 3.59 quintillion gain fills to the cap and the rest is saved
            var (toAvailable, toOverflow) = Player.SplitXpAtCap(8_990_943_188_421_035_757, 3_585_148_986_649_039_951);
            Assert.AreEqual(long.MaxValue - 8_990_943_188_421_035_757, toAvailable);
            Assert.AreEqual(3_585_148_986_649_039_951 - toAvailable, toOverflow);
        }

        /// <summary>A negative (already wrapped) balance has room for any gain, without overflowing the room sum.</summary>
        [TestMethod]
        public void SplitXpAtCap_NegativeBalanceDoesNotOverflow()
        {
            Assert.AreEqual((long.MaxValue, 0L), Player.SplitXpAtCap(-5, long.MaxValue));
        }

        /// <summary>A zero or negative amount is passed through whole, with nothing for the bank.</summary>
        [TestMethod]
        public void SplitXpAtCap_NonPositiveAmountPassesThrough()
        {
            Assert.AreEqual((0L, 0L), Player.SplitXpAtCap(long.MaxValue, 0));
            Assert.AreEqual((-7L, 0L), Player.SplitXpAtCap(long.MaxValue, -7));
        }

        /// <summary>Earning below the cap leaves the bank alone; earning past it fills the balance and banks the rest.</summary>
        [TestMethod]
        public void ApplyXpGain_FillsBalanceThenBank()
        {
            Assert.AreEqual((1_500L, 0L), Player.ApplyXpGain(1_000, 0, 500));
            Assert.AreEqual((long.MaxValue, 905L), Player.ApplyXpGain(long.MaxValue - 100, 5, 1_000));
        }

        /// <summary>The bank stops at long.MaxValue too, and a hand-edited negative bank counts as 0.</summary>
        [TestMethod]
        public void ApplyXpGain_BankClampsAndIgnoresNegative()
        {
            Assert.AreEqual((long.MaxValue, long.MaxValue), Player.ApplyXpGain(long.MaxValue, long.MaxValue - 1, 10));
            Assert.AreEqual((long.MaxValue, 10L), Player.ApplyXpGain(long.MaxValue, -42, 10));
        }

        /// <summary>A refill moves what fits under the cap and leaves the rest in the bank.</summary>
        [TestMethod]
        public void TakeFromOverflow_MovesWhatFits()
        {
            // plenty saved: fills exactly to the cap, the rest stays to the XP
            Assert.AreEqual((1_000L, 4_999_999_999_999_999_000L), Player.TakeFromOverflow(long.MaxValue - 1_000, 5_000_000_000_000_000_000));

            // less saved than the room: all of it moves
            Assert.AreEqual((1_234L, 0L), Player.TakeFromOverflow(0, 1_234));

            // a full bank into an empty pool: fills to the cap, nothing left
            Assert.AreEqual((long.MaxValue, 0L), Player.TakeFromOverflow(0, long.MaxValue));

            // a negative balance (an old wrap): room for anything, no overflow working it out
            Assert.AreEqual((long.MaxValue, 0L), Player.TakeFromOverflow(-5_870_651_898_639_475_908, long.MaxValue));
        }

        /// <summary>A full balance, an empty bank or a negative bank moves nothing.</summary>
        [TestMethod]
        public void TakeFromOverflow_NothingToDo()
        {
            Assert.AreEqual((0L, 500L), Player.TakeFromOverflow(long.MaxValue, 500));   // pool already full
            Assert.AreEqual((0L, 0L), Player.TakeFromOverflow(100, 0));                 // nothing saved
            Assert.AreEqual((0L, 0L), Player.TakeFromOverflow(100, -42));               // a hand-edited negative bank pays nothing
        }

        /// <summary>Over a grid of balances and banks, a refill never creates or loses XP and never overflows the balance.</summary>
        [TestMethod]
        public void TakeFromOverflow_NeverCreatesOrLosesXp()
        {
            long[] pools = { 0, 1, 500, long.MaxValue - 511, long.MaxValue - 1, long.MaxValue, -1, -5_870_651_898_639_475_908 };
            long[] banks = { 1, 300, 511, 1_024, 5_000_000_000_000_000_000, long.MaxValue - 1, long.MaxValue };
            foreach (var pool in pools)
                foreach (var bank in banks)
                {
                    var (moved, remaining) = Player.TakeFromOverflow(pool, bank);
                    Assert.AreEqual(bank, moved + remaining, $"pool {pool} bank {bank}");
                    Assert.IsGreaterThanOrEqualTo(0L, moved, $"pool {pool} bank {bank}");
                    Assert.IsGreaterThanOrEqualTo(0L, remaining, $"pool {pool} bank {bank}");
                    Assert.AreEqual(pool < long.MaxValue ? System.Math.Min(bank, Player.RoomBelowCap(pool)) : 0L, moved, $"pool {pool} bank {bank}");
                    _ = checked(pool + moved);   // filling the pool never overflows (throws here if it would)
                }
        }

        /// <summary>Earn past the cap, spend, refill - through the same functions the Player uses - and every XP is accounted for.</summary>
        [TestMethod]
        public void EarnSpendRefill_RoundTripsExactly()
        {
            var (available, bank) = Player.ApplyXpGain(long.MaxValue - 100, 0, 1_000);
            Assert.AreEqual(long.MaxValue, available);
            Assert.AreEqual(900L, bank);

            available -= 300;   // a raise
            var (moved, remaining) = Player.TakeFromOverflow(available, bank);
            Assert.AreEqual(long.MaxValue, available + moved);
            Assert.AreEqual(600L, remaining);
        }

    }
}
