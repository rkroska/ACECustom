using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.WorldObjects.Managers;

namespace ACE.Server.Tests
{
    [TestClass]
    public class ReceiveDamageEmoteSelectionTests
    {
        private static PropertiesEmote Emote(DamageType? damageType, float probability) =>
            new PropertiesEmote
            {
                Category = EmoteCategory.ReceiveDamage,
                Quest = string.Empty,
                DamageType = damageType,
                Probability = probability
            };

        // -------------------------------------------------------------------
        // Empty / no-op cases
        // -------------------------------------------------------------------

        [TestMethod]
        public void EmptyList_ReturnsNoResults()
        {
            var results = EmoteManager.SelectReceiveDamageEmotes(
                new List<PropertiesEmote>(), DamageType.Slash, () => 0.0);

            Assert.AreEqual(0, results.Count);
        }

        // -------------------------------------------------------------------
        // Basic typed-row matching
        // -------------------------------------------------------------------

        [TestMethod]
        public void TypedMatch_ProbabilityAlwaysFires_ReturnsEmote()
        {
            var emote = Emote(DamageType.Slash, 1.0f);

            var results = EmoteManager.SelectReceiveDamageEmotes(
                new List<PropertiesEmote> { emote }, DamageType.Slash, () => 0.0);

            Assert.AreEqual(1, results.Count);
            Assert.AreSame(emote, results[0]);
        }

        [TestMethod]
        public void TypedMatch_ProbabilityNeverFires_ReturnsNothing()
        {
            // Probability 0.0 never beats any roll > 0.
            var emote = Emote(DamageType.Slash, 0.0f);

            var results = EmoteManager.SelectReceiveDamageEmotes(
                new List<PropertiesEmote> { emote }, DamageType.Slash, () => 0.5);

            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void TypedNoMatch_ReturnsNothing_NoFallback()
        {
            var emote = Emote(DamageType.Slash, 1.0f);

            var results = EmoteManager.SelectReceiveDamageEmotes(
                new List<PropertiesEmote> { emote }, DamageType.Pierce, () => 0.0);

            Assert.AreEqual(0, results.Count);
        }

        // -------------------------------------------------------------------
        // Bitmask matching
        // -------------------------------------------------------------------

        [TestMethod]
        public void BitmaskRow_MatchesSingleIncomingType()
        {
            // Row stores Slash|Pierce (3); incoming is Slash only — the AND is non-zero, should fire.
            var emote = Emote(DamageType.Slash | DamageType.Pierce, 1.0f);

            var results = EmoteManager.SelectReceiveDamageEmotes(
                new List<PropertiesEmote> { emote }, DamageType.Slash, () => 0.0);

            Assert.AreEqual(1, results.Count);
        }

        [TestMethod]
        public void BitmaskRow_DoesNotMatchUnrelatedType()
        {
            var emote = Emote(DamageType.Slash | DamageType.Pierce, 1.0f);

            var results = EmoteManager.SelectReceiveDamageEmotes(
                new List<PropertiesEmote> { emote }, DamageType.Fire, () => 0.0);

            Assert.AreEqual(0, results.Count);
        }

        // -------------------------------------------------------------------
        // Fallback (null DamageType) behaviour
        // -------------------------------------------------------------------

        [TestMethod]
        public void TypedNoMatch_FallbackFires()
        {
            var typedEmote = Emote(DamageType.Slash, 1.0f);
            var fallback   = Emote(null, 1.0f);

            var results = EmoteManager.SelectReceiveDamageEmotes(
                new List<PropertiesEmote> { typedEmote, fallback }, DamageType.Pierce, () => 0.0);

            Assert.AreEqual(1, results.Count);
            Assert.AreSame(fallback, results[0]);
        }

        [TestMethod]
        public void TypedMatch_FallbackSuppressed_EvenWhenRngSkipsTyped()
        {
            // The typed row matches but its RNG roll fails.
            // Fallback must NOT fire — anyTypedMaskMatchesIncoming is true regardless of RNG outcome.
            var typedEmote = Emote(DamageType.Slash, 0.0f); // probability always fails any roll > 0
            var fallback   = Emote(null, 1.0f);

            var results = EmoteManager.SelectReceiveDamageEmotes(
                new List<PropertiesEmote> { typedEmote, fallback }, DamageType.Slash, () => 0.5);

            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void OnlyFallbackRows_TypedDamage_FallbackFires()
        {
            // No typed rows at all — fallback should fire for any non-Undef incoming type.
            var fallback = Emote(null, 1.0f);

            var results = EmoteManager.SelectReceiveDamageEmotes(
                new List<PropertiesEmote> { fallback }, DamageType.Slash, () => 0.0);

            Assert.AreEqual(1, results.Count);
        }

        [TestMethod]
        public void OnlyFallbackRows_UndefDamageType_FallbackFires()
        {
            var fallback = Emote(null, 1.0f);

            var results = EmoteManager.SelectReceiveDamageEmotes(
                new List<PropertiesEmote> { fallback }, DamageType.Undef, () => 0.0);

            Assert.AreEqual(1, results.Count);
        }

        // -------------------------------------------------------------------
        // Undef incoming type
        // -------------------------------------------------------------------

        [TestMethod]
        public void UndefDamageType_AllTypedGroupsSkipped_FallbackFires()
        {
            var typedEmote = Emote(DamageType.Slash, 1.0f);
            var fallback   = Emote(null, 1.0f);

            var results = EmoteManager.SelectReceiveDamageEmotes(
                new List<PropertiesEmote> { typedEmote, fallback }, DamageType.Undef, () => 0.0);

            Assert.AreEqual(1, results.Count);
            Assert.AreSame(fallback, results[0]);
        }

        [TestMethod]
        public void UndefDamageType_NoFallback_ReturnsNothing()
        {
            var typedEmote = Emote(DamageType.Slash, 1.0f);

            var results = EmoteManager.SelectReceiveDamageEmotes(
                new List<PropertiesEmote> { typedEmote }, DamageType.Undef, () => 0.0);

            Assert.AreEqual(0, results.Count);
        }

        // -------------------------------------------------------------------
        // Multiple independent typed groups
        // -------------------------------------------------------------------

        [TestMethod]
        public void MultipleTypedGroups_BothMatch_BothFire()
        {
            var slashEmote = Emote(DamageType.Slash, 1.0f);
            var fireEmote  = Emote(DamageType.Fire,  1.0f);

            // Incoming hits both groups — each should produce one winner.
            var results = EmoteManager.SelectReceiveDamageEmotes(
                new List<PropertiesEmote> { slashEmote, fireEmote },
                DamageType.Slash | DamageType.Fire, () => 0.0);

            Assert.AreEqual(2, results.Count);
        }

        [TestMethod]
        public void MultipleTypedGroups_OnlyMatchingGroupFires()
        {
            var slashEmote = Emote(DamageType.Slash, 1.0f);
            var fireEmote  = Emote(DamageType.Fire,  1.0f);

            var results = EmoteManager.SelectReceiveDamageEmotes(
                new List<PropertiesEmote> { slashEmote, fireEmote }, DamageType.Slash, () => 0.0);

            Assert.AreEqual(1, results.Count);
            Assert.AreSame(slashEmote, results[0]);
        }

        // -------------------------------------------------------------------
        // RNG winner selection within a group
        // -------------------------------------------------------------------

        [TestMethod]
        public void SameGroup_TwoRows_RngPicksLowestProbabilityWinner()
        {
            // Roll = 0.3: both qualify (0.4 > 0.3, 0.8 > 0.3).
            // OrderBy(Probability) picks the smallest winner = rowA.
            var rowA = Emote(DamageType.Slash, 0.4f);
            var rowB = Emote(DamageType.Slash, 0.8f);

            var results = EmoteManager.SelectReceiveDamageEmotes(
                new List<PropertiesEmote> { rowA, rowB }, DamageType.Slash, () => 0.3);

            Assert.AreEqual(1, results.Count);
            Assert.AreSame(rowA, results[0]);
        }

        [TestMethod]
        public void SameGroup_TwoRows_HighRollSkipsLowProbabilityRow()
        {
            // Roll = 0.5: rowA (0.4) fails, rowB (0.8) wins.
            var rowA = Emote(DamageType.Slash, 0.4f);
            var rowB = Emote(DamageType.Slash, 0.8f);

            var results = EmoteManager.SelectReceiveDamageEmotes(
                new List<PropertiesEmote> { rowA, rowB }, DamageType.Slash, () => 0.5);

            Assert.AreEqual(1, results.Count);
            Assert.AreSame(rowB, results[0]);
        }

        [TestMethod]
        public void SameGroup_TwoRows_RollTooHigh_NothingFires()
        {
            var rowA = Emote(DamageType.Slash, 0.4f);
            var rowB = Emote(DamageType.Slash, 0.8f);

            var results = EmoteManager.SelectReceiveDamageEmotes(
                new List<PropertiesEmote> { rowA, rowB }, DamageType.Slash, () => 0.9);

            Assert.AreEqual(0, results.Count);
        }
    }
}
