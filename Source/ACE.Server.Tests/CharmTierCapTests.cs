using System.Linq;

using ACE.Server.Managers.ZoneScaling;
using ACE.Server.WorldObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// The charm level cap per tier (owner 2026-10-25): levelled charms do nothing below T11, and in T11+ work at no
    /// more than the tier's charm_level_cap - never below level 1, never above their own level. Pure: no database, no world.
    /// </summary>
    [TestClass]
    public class CharmTierCapTests
    {
        [TestMethod]
        public void CapLevel_NoCap_KeepsOwnLevel()
        {
            Assert.AreEqual(74, CharmAbilityRegistry.CapLevel(74, null));
            Assert.AreEqual(1, CharmAbilityRegistry.CapLevel(1, null));
        }

        [TestMethod]
        public void CapLevel_AboveCap_WorksAtCap()
        {
            Assert.AreEqual(49, CharmAbilityRegistry.CapLevel(100, 49));
            Assert.AreEqual(10, CharmAbilityRegistry.CapLevel(74, 10));
        }

        [TestMethod]
        public void CapLevel_BelowCap_KeepsOwnLevel()
        {
            Assert.AreEqual(12, CharmAbilityRegistry.CapLevel(12, 49));
            Assert.AreEqual(49, CharmAbilityRegistry.CapLevel(49, 49));
        }

        [TestMethod]
        public void BelowT11_LevelledCharmsDoNothing()
        {
            Assert.AreEqual(0, CharmAbilityRegistry.EffectiveLeveledLevel(100, 10, null), "T10: no charm at all");
            Assert.AreEqual(0, CharmAbilityRegistry.EffectiveLeveledLevel(1, 0, null), "the base world: no charm at all");
            Assert.AreEqual(0, CharmAbilityRegistry.EffectiveLeveledLevel(50, 10, 49), "a cap below T11 changes nothing");
        }

        [TestMethod]
        public void T11Plus_WorksAtTheCap()
        {
            Assert.AreEqual(10, CharmAbilityRegistry.EffectiveLeveledLevel(100, 11, 10));
            Assert.AreEqual(49, CharmAbilityRegistry.EffectiveLeveledLevel(100, 15, 49));
            Assert.AreEqual(30, CharmAbilityRegistry.EffectiveLeveledLevel(30, 15, 49));
            Assert.AreEqual(100, CharmAbilityRegistry.EffectiveLeveledLevel(100, 16, null), "no cap set: the full level");
        }

        [TestMethod]
        public void CapFromSetting_Values()
        {
            Assert.AreEqual(1, CharmAbilityRegistry.CapFromSetting(0), "0 never means no cap");
            Assert.AreEqual(1, CharmAbilityRegistry.CapFromSetting(-5));
            Assert.AreEqual(1, CharmAbilityRegistry.CapFromSetting(0.5));
            Assert.AreEqual(49, CharmAbilityRegistry.CapFromSetting(49.9), "fractions round down");
            Assert.AreEqual(35, CharmAbilityRegistry.CapFromSetting(35));
            Assert.IsNull(CharmAbilityRegistry.CapFromSetting(100), "100 = the highest level = no cap");
            Assert.IsNull(CharmAbilityRegistry.CapFromSetting(250));
            Assert.IsNull(CharmAbilityRegistry.CapFromSetting(double.NaN));
            Assert.IsNull(CharmAbilityRegistry.CapFromSetting(double.PositiveInfinity));
        }

        [TestMethod]
        public void CharmLevelCap_IsAKnownZoneStat()
        {
            Assert.AreEqual("charm_level_cap", ZoneStat.CharmLevelCap);
            Assert.IsTrue(ZoneStat.All.Contains(ZoneStat.CharmLevelCap), "/zonecontrol ... set charm_level_cap must be accepted");
        }

        [TestMethod]
        public void OwnerCaps_KeepTheBalancedCounts()
        {
            // owner caps 2026-10-25: T11 10, T12 15, T13 20, T14 35, T15 49. Each keeps every COUNT (jumps, targets, arrows,
            // spread targets) of the level its tier was balanced at (T11-T12 level 1, T13 15, T14 30, T15 45): only the slow
            // numbers (damage, range, arc) creep up. Checked against the live default curves, whatever their step points are.
            var caps = new[] { (cap: 10, balanced: 1), (cap: 15, balanced: 1), (cap: 20, balanced: 15), (cap: 35, balanced: 30), (cap: 49, balanced: 45) };
            var counts = new[] { (CharmSettingsManager.Chain, "jumps"), (CharmSettingsManager.ImprovedCleave, "targets"),
                                 (CharmSettingsManager.SplitArrow, "arrows"), (CharmSettingsManager.Pandemic, "targets"),
                                 (CharmSettingsManager.Pandemic, "maxinfected") };
            foreach (var (block, key) in counts)
            {
                block.Reset();
                foreach (var (cap, balanced) in caps)
                    Assert.AreEqual(block.CountAt(key, balanced), block.CountAt(key, cap), $"{block.Name}.{key}: cap {cap} vs balanced level {balanced}");
            }
        }
    }
}
