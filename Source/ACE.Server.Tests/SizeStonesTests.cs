using System;
using System.IO;

using ACE.Server.Entity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// The stepping and drawing rules of the Enlarging and Shrinking Stones (SizeStones). Pure: no database, no world.
    /// </summary>
    [TestClass]
    public class SizeStonesTests
    {
        [TestMethod]
        public void Step_MovesOneTenthEachWay()
        {
            Assert.IsTrue(SizeStones.TryStep(1.0, enlarge: true, out var up));
            Assert.AreEqual(1.1, up, 1e-9);

            Assert.IsTrue(SizeStones.TryStep(1.0, enlarge: false, out var down));
            Assert.AreEqual(0.9, down, 1e-9);
        }

        [TestMethod]
        public void Step_IsRefusedAtEitherLimit()
        {
            Assert.IsFalse(SizeStones.TryStep(SizeStones.MaxLevel, enlarge: true, out _));
            Assert.IsFalse(SizeStones.TryStep(SizeStones.MinLevel, enlarge: false, out _));

            // and the last step onto each limit is allowed
            Assert.IsTrue(SizeStones.TryStep(1.3, enlarge: true, out var top));
            Assert.AreEqual(SizeStones.MaxLevel, top, 1e-9);
            Assert.IsTrue(SizeStones.TryStep(0.8, enlarge: false, out var bottom));
            Assert.AreEqual(SizeStones.MinLevel, bottom, 1e-9);
        }

        [TestMethod]
        public void Step_WalksTheWholeRangeAndBackWithoutDrift()
        {
            var level = SizeStones.MinLevel;
            var steps = 0;
            while (SizeStones.TryStep(level, enlarge: true, out var next))
            {
                level = next;
                steps++;
            }
            Assert.AreEqual(7, steps, "0.7 to 1.4 is seven stones");
            Assert.AreEqual(SizeStones.MaxLevel, level, 1e-9);

            for (var i = 0; i < 4; i++)
            {
                Assert.IsTrue(SizeStones.TryStep(level, enlarge: false, out var next));
                level = next;
            }
            // exactly 1.0, so the handler removes the property and the item is truly back to normal
            Assert.AreEqual(1.0, level, 0.0);
        }

        [TestMethod]
        public void Step_FromAStoredLevelOutsideTheLimits_StartsAtTheNearestLimit()
        {
            Assert.IsFalse(SizeStones.TryStep(9.0, enlarge: true, out _));
            Assert.IsTrue(SizeStones.TryStep(9.0, enlarge: false, out var fromTooBig));
            Assert.AreEqual(1.3, fromTooBig, 1e-9);

            Assert.IsFalse(SizeStones.TryStep(-3.0, enlarge: false, out _));
            Assert.IsTrue(SizeStones.TryStep(double.NaN, enlarge: true, out var fromNaN));
            Assert.AreEqual(1.1, fromNaN, 1e-9);
        }

        [TestMethod]
        public void ShownScale_WithNoLevel_IsTheItemsOwnScaleUntouched()
        {
            Assert.IsNull(SizeStones.ShownScale(null, null));
            Assert.AreEqual(0.6f, SizeStones.ShownScale(0.6f, null));
            Assert.AreEqual(0f, SizeStones.ShownScale(0f, null));

            // a level of exactly normal changes nothing either
            Assert.IsNull(SizeStones.ShownScale(null, 1.0));
            Assert.AreEqual(2.5f, SizeStones.ShownScale(2.5f, 1.0));
        }

        [TestMethod]
        public void ShownScale_MultipliesTheItemsOwnScale()
        {
            Assert.AreEqual(1.3f, SizeStones.ShownScale(null, 1.3).Value, 1e-5f);
            Assert.AreEqual(0.6f * 1.3f, SizeStones.ShownScale(0.6f, 1.3).Value, 1e-5f);
            Assert.AreEqual(2.0f * 0.7f, SizeStones.ShownScale(2.0f, 0.7).Value, 1e-5f);

            // an own scale too small to be sent is drawn by the client at 1.0
            Assert.AreEqual(1.2f, SizeStones.ShownScale(0f, 1.2).Value, 1e-5f);
        }

        [TestMethod]
        public void ShownScale_NeverLeavesTheLimits()
        {
            Assert.AreEqual((float)SizeStones.MaxLevel, SizeStones.ShownScale(1.0f, 50.0).Value, 1e-5f);
            Assert.AreEqual((float)SizeStones.MinLevel, SizeStones.ShownScale(1.0f, 0.0).Value, 1e-5f);
            Assert.AreEqual((float)SizeStones.MinLevel, SizeStones.ShownScale(1.0f, -2.0).Value, 1e-5f);
            Assert.AreEqual(1.0f, SizeStones.ShownScale(1.0f, double.NaN).Value, 1e-5f);
        }

        [TestMethod]
        public void SqlPatch_CreatesTheStonesTheCodeLooksFor()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Database", "Updates", "World")))
                dir = dir.Parent;
            Assert.IsNotNull(dir, "Could not locate repository root directory.");

            var sql = File.ReadAllText(Path.Combine(dir.FullName, "Database", "Updates", "World", "2026-10-10-00-Size-Stones.sql"));
            StringAssert.Contains(sql, $"VALUES ({SizeStones.EnlargingStoneWcid}, 'ace{SizeStones.EnlargingStoneWcid}-enlargingstone'");
            StringAssert.Contains(sql, $"VALUES ({SizeStones.ShrinkingStoneWcid}, 'ace{SizeStones.ShrinkingStoneWcid}-shrinkingstone'");
        }
    }
}
