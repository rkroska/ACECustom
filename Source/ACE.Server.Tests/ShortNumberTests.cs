using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Server.Command;

namespace ACE.Server.Tests
{
    /// <summary>ShortNumber (2026-09-27): the K / M / B / T / Q amounts in audit lines and admin grant commands.</summary>
    [TestClass]
    public class ShortNumberTests
    {
        [TestMethod]
        public void Format_ExactBelowTenThousand_ShortAbove()
        {
            Assert.AreEqual("9,999", ShortNumber.Format(9_999));
            Assert.AreEqual("-42", ShortNumber.Format(-42));
            Assert.AreEqual("10K", ShortNumber.Format(10_000));
            Assert.AreEqual("1.5M", ShortNumber.Format(1_500_000));
            Assert.AreEqual("5B", ShortNumber.Format(5_000_000_000));
            Assert.AreEqual("-2.5T", ShortNumber.Format(-2_500_000_000_000));
            Assert.IsTrue(ShortNumber.Format(long.MinValue).StartsWith('-'));   // no overflow in Math.Abs
        }

        [TestMethod]
        public void TryParse_AcceptsPlainCommasAndSuffixes()
        {
            Assert.IsTrue(ShortNumber.TryParse("5B", out var v)); Assert.AreEqual(5_000_000_000L, v);
            Assert.IsTrue(ShortNumber.TryParse("1.5m", out v)); Assert.AreEqual(1_500_000L, v);
            Assert.IsTrue(ShortNumber.TryParse("5,000,000,000", out v)); Assert.AreEqual(5_000_000_000L, v);
            Assert.IsTrue(ShortNumber.TryParse("250K", out v)); Assert.AreEqual(250_000L, v);
        }

        [TestMethod]
        public void TryParse_RejectsNamesDecimalCommasFractionsAndOverflow()
        {
            Assert.IsFalse(ShortNumber.TryParse("Grumpy", out _));
            Assert.IsFalse(ShortNumber.TryParse("1,5M", out _));          // a decimal comma, not a thousands separator
            Assert.IsFalse(ShortNumber.TryParse("1.2345K", out _));       // not a whole number
            Assert.IsFalse(ShortNumber.TryParse("99999999Q", out _));     // past long.MaxValue
            Assert.IsFalse(ShortNumber.TryParse("", out _));
            Assert.IsFalse(ShortNumber.TryParse("K", out _));
        }

        [TestMethod]
        public void ExpandLastAmount_RewritesOnlyAShapedAmountWithinTheCeiling()
        {
            CollectionAssert.AreEqual(new[] { "Bob", "5000000000" }, ShortNumber.ExpandLastAmount(new[] { "Bob", "5B" }));
            CollectionAssert.AreEqual(new[] { "Bob", "123" }, ShortNumber.ExpandLastAmount(new[] { "Bob", "123" }));      // plain digits stay
            CollectionAssert.AreEqual(new[] { "Bob", "5T" }, ShortNumber.ExpandLastAmount(new[] { "Bob", "5T" }));        // over the ceiling: usage message
            CollectionAssert.AreEqual(new[] { "Bob", "Smith" }, ShortNumber.ExpandLastAmount(new[] { "Bob", "Smith" }));  // a name
        }
    }
}
