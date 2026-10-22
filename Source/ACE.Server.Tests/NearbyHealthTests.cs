using System;
using System.Collections.Generic;

using ACE.Server.Entity;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Nearby health updates: the rule that decides whether one health change of a creature is sent to the players who
    /// opted in at once, or held for the creature's next tick. The pure rule only - no world, no sessions.
    /// </summary>
    [TestClass]
    public class NearbyHealthTests
    {
        private static readonly TimeSpan Gap = TimeSpan.FromMilliseconds(400);
        private static readonly DateTime T0 = new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void FirstReading_IsSent()
        {
            Assert.IsTrue(NearbyHealth.SendNow(0.8, DateTime.MinValue, T0, Gap));
        }

        [TestMethod]
        public void ReadingInsideTheGap_IsHeld()
        {
            Assert.IsFalse(NearbyHealth.SendNow(0.8, T0, T0.AddMilliseconds(399), Gap));
        }

        [TestMethod]
        public void ReadingAtTheGap_IsSent()
        {
            Assert.IsTrue(NearbyHealth.SendNow(0.8, T0, T0.AddMilliseconds(400), Gap));
        }

        [TestMethod]
        public void Death_IsNeverHeld()
        {
            Assert.IsTrue(NearbyHealth.SendNow(0.0, T0, T0.AddMilliseconds(1), Gap));
            Assert.IsTrue(NearbyHealth.SendNow(-0.1, T0, T0, Gap));
        }

        [TestMethod]
        public void Nearest_KeepsTheClosest_NearestFirst()
        {
            var candidates = new List<(uint Id, float Distance)> { (1, 30f), (2, 5f), (3, 12f), (4, 50f), (5, 8f) };
            CollectionAssert.AreEqual(new List<uint> { 2, 5, 3 }, NearbyHealth.Nearest(candidates, 3));
        }

        [TestMethod]
        public void Nearest_UnderTheLimit_KeepsAll()
        {
            var candidates = new List<(uint Id, float Distance)> { (1, 30f), (2, 5f) };
            CollectionAssert.AreEquivalent(new List<uint> { 1, 2 }, NearbyHealth.Nearest(candidates, 20));
        }

        [TestMethod]
        public void Nearest_ZeroLimit_MeansNoLimit()
        {
            var candidates = new List<(uint Id, float Distance)>();
            for (uint i = 1; i <= 50; i++) candidates.Add((i, i));
            Assert.AreEqual(50, NearbyHealth.Nearest(candidates, 0).Count);
        }

        [TestMethod]
        public void Nearest_EqualDistances_KeepTheirOrder()
        {
            var candidates = new List<(uint Id, float Distance)> { (7, 6f), (8, 6f), (9, 6f), (10, 6f) };
            CollectionAssert.AreEqual(new List<uint> { 7, 8 }, NearbyHealth.Nearest(candidates, 2));
        }

        [TestMethod]
        public void Nearest_Nothing_GivesNothing()
        {
            Assert.AreEqual(0, NearbyHealth.Nearest(null, 20).Count);
            Assert.AreEqual(0, NearbyHealth.Nearest(new List<(uint Id, float Distance)>(), 20).Count);
        }

        [TestMethod]
        public void FullHealth_IsThrottledLikeAnyOtherReading()
        {
            Assert.IsFalse(NearbyHealth.SendNow(1.0, T0, T0.AddMilliseconds(10), Gap));
        }
    }
}
