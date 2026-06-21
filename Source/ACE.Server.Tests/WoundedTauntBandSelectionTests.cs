using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.WorldObjects.Managers;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Tests for the health-banded WoundedTaunt phase selection used by multi-phase bosses
    /// (Aerbax shadows, Galivak, Rajael, etc.). The bug being guarded against: a large or lethal
    /// hit would skip phase bands because selection only looked at *current* health. The fix fires
    /// every band CROSSED by the damage, highest-band-first, including on a one-shot to 0%.
    /// </summary>
    [TestClass]
    public class WoundedTauntBandSelectionTests
    {
        private static PropertiesEmote Band(float maxHealth, float minHealth, float probability = 1.0f) =>
            new PropertiesEmote
            {
                Category = EmoteCategory.WoundedTaunt,
                Quest = string.Empty,
                MinHealth = minHealth,
                MaxHealth = maxHealth,
                Probability = probability
            };

        // -------------------------------------------------------------------
        // No-op cases
        // -------------------------------------------------------------------

        [TestMethod]
        public void EmptyList_ReturnsNoResults()
        {
            var results = EmoteManager.SelectWoundedTauntBands(
                new List<PropertiesEmote>(), 1.0, 0.5, () => 0.0);

            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void HealthRose_ReturnsNothing()
        {
            // A heal (curr > prev) must not fire any band.
            var band = Band(0.6f, 0.01f);

            var results = EmoteManager.SelectWoundedTauntBands(
                new List<PropertiesEmote> { band }, 0.3, 0.8, () => 0.0);

            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void NonWoundedTauntRows_Ignored()
        {
            var other = new PropertiesEmote
            {
                Category = EmoteCategory.ReceiveDamage,
                Quest = string.Empty,
                MinHealth = 0.01f,
                MaxHealth = 0.98f,
                Probability = 1.0f
            };

            var results = EmoteManager.SelectWoundedTauntBands(
                new List<PropertiesEmote> { other }, 1.0, 0.0, () => 0.0);

            Assert.AreEqual(0, results.Count);
        }

        // -------------------------------------------------------------------
        // Gradual damage — one band crossed at a time
        // -------------------------------------------------------------------

        [TestMethod]
        public void GradualCross_EntersBand_Fires()
        {
            // Aerbax shadow 36951 phase 1 band: 75%-98%. Hit 100% -> 90% enters it.
            var phase1 = Band(0.98f, 0.75f);

            var results = EmoteManager.SelectWoundedTauntBands(
                new List<PropertiesEmote> { phase1 }, 1.0, 0.90, () => 0.0);

            Assert.AreEqual(1, results.Count);
            Assert.AreSame(phase1, results[0]);
        }

        [TestMethod]
        public void StayingAboveBand_DoesNotFire()
        {
            var phase1 = Band(0.98f, 0.75f);

            // 100% -> 99%: never dropped to/below the band's max (0.98).
            var results = EmoteManager.SelectWoundedTauntBands(
                new List<PropertiesEmote> { phase1 }, 1.0, 0.99, () => 0.0);

            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void AlreadyBelowBand_DoesNotRefire()
        {
            var phase1 = Band(0.98f, 0.75f);

            // Already inside/below the band last time (prev 0.90) and dropping further (0.80):
            // prev is not above max, so this band is not newly crossed.
            var results = EmoteManager.SelectWoundedTauntBands(
                new List<PropertiesEmote> { phase1 }, 0.90, 0.80, () => 0.0);

            Assert.AreEqual(0, results.Count);
        }

        // -------------------------------------------------------------------
        // The core bug: overkill / lethal hits
        // -------------------------------------------------------------------

        [TestMethod]
        public void OneShotToZero_FiresAllBands_HighestFirst()
        {
            // 36951: phase1 (75-98) and phase2 (1-60). A one-shot from full to 0 must fire BOTH,
            // phase1 before phase2 (so StartEvent master1 precedes phase2's StopEvent master1).
            var phase1 = Band(0.98f, 0.75f);
            var phase2 = Band(0.60f, 0.01f);

            var results = EmoteManager.SelectWoundedTauntBands(
                new List<PropertiesEmote> { phase2, phase1 }, 1.0, 0.0, () => 0.0);

            Assert.AreEqual(2, results.Count);
            Assert.AreSame(phase1, results[0], "highest band must fire first");
            Assert.AreSame(phase2, results[1]);
        }

        [TestMethod]
        public void OverkillSkippingBand_StillFiresSkippedBand()
        {
            // Hit jumps 98% -> 5%: lands in phase2's range but must NOT skip phase1.
            var phase1 = Band(0.98f, 0.75f);
            var phase2 = Band(0.60f, 0.01f);

            var results = EmoteManager.SelectWoundedTauntBands(
                new List<PropertiesEmote> { phase1, phase2 }, 0.98001, 0.05, () => 0.0);

            Assert.AreEqual(2, results.Count);
            Assert.AreSame(phase1, results[0]);
            Assert.AreSame(phase2, results[1]);
        }

        [TestMethod]
        public void ThreeNarrowBands_OneShot_FiresAllInOrder()
        {
            // 37378 toggle bands: 86-90, 70-75, 20-25. A one-shot crosses all three; highest first.
            var b90 = Band(0.90f, 0.86f);
            var b75 = Band(0.75f, 0.70f);
            var b25 = Band(0.25f, 0.20f);

            var results = EmoteManager.SelectWoundedTauntBands(
                new List<PropertiesEmote> { b25, b90, b75 }, 1.0, 0.0, () => 0.0);

            Assert.AreEqual(3, results.Count);
            Assert.AreSame(b90, results[0]);
            Assert.AreSame(b75, results[1]);
            Assert.AreSame(b25, results[2]);
        }

        [TestMethod]
        public void NarrowBandWithGap_DamageThroughGap_FiresCrossedBand()
        {
            // 37378 has gaps between bands. A hit 92% -> 80% passes entirely through the 86-90 band.
            var b90 = Band(0.90f, 0.86f);

            var results = EmoteManager.SelectWoundedTauntBands(
                new List<PropertiesEmote> { b90 }, 0.92, 0.80, () => 0.0);

            Assert.AreEqual(1, results.Count);
            Assert.AreSame(b90, results[0]);
        }

        // -------------------------------------------------------------------
        // Probability handling
        // -------------------------------------------------------------------

        [TestMethod]
        public void CrossedBand_ProbabilityFails_DoesNotFire()
        {
            var band = Band(0.60f, 0.01f, probability: 0.10f);

            // roll 0.5 >= probability 0.10 -> fails.
            var results = EmoteManager.SelectWoundedTauntBands(
                new List<PropertiesEmote> { band }, 1.0, 0.0, () => 0.5);

            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void CrossedBand_ProbabilityPasses_Fires()
        {
            var band = Band(0.60f, 0.01f, probability: 0.10f);

            // roll 0.05 < probability 0.10 -> fires.
            var results = EmoteManager.SelectWoundedTauntBands(
                new List<PropertiesEmote> { band }, 1.0, 0.0, () => 0.05);

            Assert.AreEqual(1, results.Count);
        }

        // -------------------------------------------------------------------
        // Null bound guard
        // -------------------------------------------------------------------

        [TestMethod]
        public void NullMaxHealth_NeverCrossed()
        {
            var band = new PropertiesEmote
            {
                Category = EmoteCategory.WoundedTaunt,
                Quest = string.Empty,
                MinHealth = null,
                MaxHealth = null,
                Probability = 1.0f
            };

            var results = EmoteManager.SelectWoundedTauntBands(
                new List<PropertiesEmote> { band }, 1.0, 0.0, () => 0.0);

            Assert.AreEqual(0, results.Count);
        }
    }
}
