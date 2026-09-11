using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Coverage for <see cref="VariationManager.GetEffectiveEndgameVariation"/> — the v11+ endgame-content
    /// layer resolver behind the %HP floor, vuln compression, damage-taken mitigation, the attack-skill floor
    /// and Zone Control's zone matching.
    ///
    /// This function had ZERO coverage until 2026-07-30, which is how it stayed broken for weeks: it used to
    /// floor the ForceEndgameSystems test hook on PRESTIGE_BASE_VARIATION, so when PRESTIGE_VAR_OFFSET moved
    /// 10 -> 1000 every test dummy silently reported variation 1001. Downstream, the %HP floor computes
    /// growth^(variation - min), and 1.22^990 overflows to Infinity — the NaN/Infinity guard then returned
    /// 0 damage, i.e. the dummy looked alive and well while dealing nothing.
    /// </summary>
    [TestClass]
    public class EndgameVariationTests
    {
        private static GenericObject CreateTestObject(int? locationVariation)
        {
            var weenie = new Weenie
            {
                WeenieClassId = 424243,
                ClassName = "TestEndgameVariationWeenie",
                WeenieType = WeenieType.Generic
            };
            var wo = new GenericObject(weenie, new ObjectGuid(0xF0004243));
            wo.SetPosition(PositionType.Location,
                new Position(0x00A80101, 12f, 12f, 12f, 0f, 0f, 0f, 1f, false, locationVariation));
            return wo;
        }

        // ── real mobs: the flag is absent, so the real variation always passes through ──

        [TestMethod]
        public void RealEndgameVariation_PassesThrough()
        {
            Assert.AreEqual(11, VariationManager.GetEffectiveEndgameVariation(CreateTestObject(11)));
            Assert.AreEqual(25, VariationManager.GetEffectiveEndgameVariation(CreateTestObject(25)));
        }

        [TestMethod]
        public void RealNonEndgameVariation_PassesThroughUnchanged()
        {
            Assert.AreEqual(0, VariationManager.GetEffectiveEndgameVariation(CreateTestObject(0)));
            Assert.AreEqual(5, VariationManager.GetEffectiveEndgameVariation(CreateTestObject(5)));
            Assert.AreEqual(10, VariationManager.GetEffectiveEndgameVariation(CreateTestObject(10)));
        }

        [TestMethod]
        public void BaseWorld_NullVariation_ResolvesToZero()
        {
            Assert.AreEqual(0, VariationManager.GetEffectiveEndgameVariation(CreateTestObject(null)));
        }

        [TestMethod]
        public void RiftVariation_NegativePassesThroughUnchanged()
        {
            // rift run instances live at <= -1000; RiftScaling strips ForceEndgameSystems from rift
            // creatures, so the flag branch is unreachable there and the negative must survive intact
            Assert.AreEqual(-1000, VariationManager.GetEffectiveEndgameVariation(CreateTestObject(-1000)));
            Assert.AreEqual(-7, VariationManager.GetEffectiveEndgameVariation(CreateTestObject(-7)));
        }

        [TestMethod]
        public void NullWorldObject_ResolvesToZero()
        {
            Assert.AreEqual(0, VariationManager.GetEffectiveEndgameVariation(null));
        }

        // ── ForceEndgameSystems test hook ──

        [TestMethod]
        public void Forced_Unset_ResolvesToEndgameMinimum()
        {
            var wo = CreateTestObject(0);
            wo.SetProperty(PropertyBool.ForceEndgameSystems, true);

            Assert.AreEqual(VariationManager.EndgameMinVariation,
                VariationManager.GetEffectiveEndgameVariation(wo));
            Assert.AreEqual(11, VariationManager.GetEffectiveEndgameVariation(wo));
        }

        [TestMethod]
        public void Forced_ExplicitVariation_IsHonoured()
        {
            var wo = CreateTestObject(0);
            wo.SetProperty(PropertyBool.ForceEndgameSystems, true);
            wo.SetProperty(PropertyInt.EndgameForcedVariation, 20);

            // REGRESSION: this returned PRESTIGE_BASE_VARIATION (1001) while the offset was 1000,
            // making the documented "simulate a deeper tier" knob completely inert.
            Assert.AreEqual(20, VariationManager.GetEffectiveEndgameVariation(wo));
        }

        [TestMethod]
        public void Forced_BelowMinimum_IsFlooredNotPassedThrough()
        {
            var wo = CreateTestObject(0);
            wo.SetProperty(PropertyBool.ForceEndgameSystems, true);
            wo.SetProperty(PropertyInt.EndgameForcedVariation, 5);

            Assert.AreEqual(11, VariationManager.GetEffectiveEndgameVariation(wo));
        }

        [TestMethod]
        public void Forced_DoesNotOverrideARealEndgameVariation()
        {
            // a real v11+ mob short-circuits before the flag is ever read
            var wo = CreateTestObject(14);
            wo.SetProperty(PropertyBool.ForceEndgameSystems, true);
            wo.SetProperty(PropertyInt.EndgameForcedVariation, 20);

            Assert.AreEqual(14, VariationManager.GetEffectiveEndgameVariation(wo));
        }

        [TestMethod]
        public void FlagFalse_IsIgnored()
        {
            var wo = CreateTestObject(0);
            wo.SetProperty(PropertyBool.ForceEndgameSystems, false);
            wo.SetProperty(PropertyInt.EndgameForcedVariation, 20);

            Assert.AreEqual(0, VariationManager.GetEffectiveEndgameVariation(wo));
        }

        // ── the decouple invariant ──

        [TestMethod]
        public void EndgameMinimum_IsIndependentOfPrestigeTiering()
        {
            // The whole point of the 2026-07-30 decouple: moving PRESTIGE_VAR_OFFSET must never move the
            // endgame content floor again. If someone re-derives one from the other, this fails.
            // (Read through locals so the analyzer doesn't fold the const comparison away — the guard is
            // meant to trip at COMPILE time on the next edit, which it still does.)
            var endgameMin = VariationManager.EndgameMinVariation;
            var prestigeBase = PrestigeManager.PRESTIGE_BASE_VARIATION;

            Assert.AreEqual(11, endgameMin);
            Assert.AreNotEqual(prestigeBase, endgameMin);
        }

        [TestMethod]
        public void ZoneControlDelegatesToTheSharedResolver()
        {
            // ZoneControlManager.GetEffectiveVariation kept its name but must not keep its own copy —
            // the duplicate is what drifted and broke the test hook.
            var wo = CreateTestObject(0);
            wo.SetProperty(PropertyBool.ForceEndgameSystems, true);
            wo.SetProperty(PropertyInt.EndgameForcedVariation, 20);

            Assert.AreEqual(VariationManager.GetEffectiveEndgameVariation(wo),
                ACE.Server.Managers.ZoneControl.ZoneControlManager.GetEffectiveVariation(wo));
        }
    }
}
