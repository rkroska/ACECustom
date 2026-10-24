using System;
using System.Linq;

using ACE.Entity.Enum.Properties;
using ACE.Server.WorldObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// The 2026-10-21 charm design (Schneebly) on top of the levelled charm framework: the default numbers of each charm
    /// at levels 1 / 50 / 100, the Improved Cleave "own plus charm" rule, and the Armor Breaker effective-rend formula.
    /// Pure: no database, no world. The blocks are reset first, so a /charm change elsewhere never leaks in.
    /// </summary>
    [TestClass]
    public class LeveledCharmDesignTests
    {
        [TestInitialize]
        public void ResetBlocks()
        {
            foreach (var block in CharmSettingsManager.LeveledBlocks.Values)
                block.Reset();
        }

        [TestMethod]
        public void SplitArrow_ConeNeverWidens_ArrowsQuarterToHalf()
        {
            var b = CharmSettingsManager.SplitArrow;
            foreach (var level in new[] { 1, 25, 50, 75, 100 })
                Assert.AreEqual(60.0, b.At("angle", level), 1e-9, $"angle at level {level}");
            Assert.AreEqual(0.25, b.At("damage", 1), 1e-9);
            Assert.AreEqual(0.5, b.At("damage", 100), 1e-9);
            Assert.AreEqual(2, b.CountAt("arrows", 1));
            Assert.AreEqual(6, b.CountAt("arrows", 100));
        }

        [TestMethod]
        public void Pandemic_OnlyTheCastDotSpreads_CopiesQuarterToHalf()
        {
            var b = CharmSettingsManager.Pandemic;
            Assert.AreEqual(1, b.CountAt("generations", 1));
            Assert.AreEqual(1, b.CountAt("generations", 100));
            Assert.AreEqual(0.25, b.At("damage", 1), 1e-9);
            Assert.AreEqual(0.5, b.At("damage", 100), 1e-9);
            Assert.IsTrue(b.On("exclusive"));
        }

        [TestMethod]
        public void Chain_ReplacesForkAndSplitCastByDefault()
        {
            Assert.IsTrue(CharmSettingsManager.Chain.On("exclusive"));
        }

        [TestMethod]
        public void ImprovedCleave_TwoHandersOnly_AddedTargets_MeleeReach()
        {
            var b = CharmSettingsManager.ImprovedCleave;
            Assert.IsTrue(b.On("twohandedonly"));
            Assert.AreEqual(1, b.CountAt("targets", 1));
            Assert.AreEqual(5, b.CountAt("targets", 100));
            Assert.AreEqual(2.0, b.At("range", 1), 1e-9);
            Assert.AreEqual(5.0, b.At("range", 100), 1e-9);
            Assert.AreEqual(200.0, b.At("angle", 1), 1e-9);
            Assert.AreEqual(360.0, b.At("angle", 100), 1e-9);
            Assert.AreEqual(1.0, b.At("damage", 1), 1e-9);
            Assert.AreEqual(1.0, b.At("damage", 100), 1e-9);
            Assert.AreEqual(10, b.CountAt("maxtargets", 50));
        }

        [TestMethod]
        public void ImprovedCleave_TargetsAddToTheWeaponsOwn_HeldToTheCap()
        {
            // a typical two-hander cleaves 1 of its own
            Assert.AreEqual(2, Creature.CharmCleaveTotal(1, 1, 1));
            Assert.AreEqual(6, Creature.CharmCleaveTotal(1, 5, 100));
            // a weapon that does not cleave gets the charm's alone
            Assert.AreEqual(5, Creature.CharmCleaveTotal(0, 5, 100));
            // a T25 Cleave card (9) plus the level 100 charm (5) is held to 10
            Assert.AreEqual(10, Creature.CharmCleaveTotal(9, 5, 100));
        }

        [TestMethod]
        public void ArmorBreaker_ShareOfTheArmorLeft()
        {
            var b = CharmSettingsManager.ArmorBreaker;
            Assert.AreEqual(0.20, b.At("share", 1), 1e-9);
            Assert.AreEqual(0.70, b.At("share", 100), 1e-9);
            Assert.IsTrue(b.On("nocleave"));

            // effective rend = rend + share x (1 - rend): a 60 pct weapon at level 100 ignores 88 pct, and nothing reaches 100
            double Effective(double rend, double share) => 1 - (1 - rend) * (1 - share);
            Assert.AreEqual(0.88, Effective(0.60, b.At("share", 100)), 1e-9);
            Assert.IsTrue(Effective(0.99, b.At("share", 100)) < 1.0);
        }

        [TestMethod]
        public void ShieldArc_FrontHalfToAllAround()
        {
            var b = CharmSettingsManager.ShieldArc;
            Assert.AreEqual(200.0, b.At("angle", 1), 1e-9);
            Assert.AreEqual(360.0, b.At("angle", 100), 1e-9);
        }

        [TestMethod]
        public void NewCharms_AreWiredLikeTheOthers()
        {
            Assert.AreEqual(CharmAbilityRegistry.ArmorBreakerAbilityId, CharmSettingsManager.LeveledAbilityId("armorbreaker"));
            Assert.AreEqual(CharmAbilityRegistry.ShieldArcAbilityId, CharmSettingsManager.LeveledAbilityId("shieldarc"));
            Assert.AreEqual(CharmAbilityRegistry.ArmorBreakerAbilityId, CharmAbilityRegistry.GetAbilityIdForWCID(777700039));
            Assert.AreEqual(CharmAbilityRegistry.ShieldArcAbilityId, CharmAbilityRegistry.GetAbilityIdForWCID(777700040));
            Assert.AreEqual(PropertyInt64.ArmorBreakerCharmCoins, Player.CharmCoinsProperty(CharmAbilityRegistry.ArmorBreakerAbilityId));
            Assert.AreEqual(PropertyInt64.ShieldArcCharmCoins, Player.CharmCoinsProperty(CharmAbilityRegistry.ShieldArcAbilityId));
            Assert.IsTrue(CharmAbilityRegistry.RegisteredIds.Contains(CharmAbilityRegistry.ArmorBreakerAbilityId));
            Assert.IsTrue(CharmAbilityRegistry.RegisteredIds.Contains(CharmAbilityRegistry.ShieldArcAbilityId));
        }

        [TestMethod]
        public void Preset_EntryAugs_AreTheTiersWieldRequirements()
        {
            // T16 as the weapon scaling defaults build it: item 4000 (frozen at the buy cap), creature 6000, life 4000,
            // Triune + skill charms 500 each
            var row = new ACE.Server.Managers.WeaponScaling.WeaponScalingTier
            {
                Tier = 16, MinWieldAugs = 4000, MinWieldCreature = 6000, MinWieldLife = 4000, MinWieldTriune = 500, MinWieldSkillCharm = 500,
            };
            var (augs, charms) = ACE.Server.Command.Handlers.TestCharacterCommands.PresetAugs(16, row, max: false);
            // creature,item,life,war,void,duration,specialize,summon,melee,missile
            Assert.AreEqual("6000,4000,4000,4000,4000,4000,0,0,4000,4000", augs);
            // weave,wrath,nether,steel,trueshot
            Assert.AreEqual("500,500,500,500,500", charms);

            // T12: item 2500, creature 4500, life 2500, no Triune / skill charms yet
            var t12 = new ACE.Server.Managers.WeaponScaling.WeaponScalingTier
            {
                Tier = 12, MinWieldAugs = 2500, MinWieldCreature = 4500, MinWieldLife = 2500,
            };
            var (augs12, charms12) = ACE.Server.Command.Handlers.TestCharacterCommands.PresetAugs(12, t12, max: false);
            Assert.AreEqual("4500,2500,2500,2500,2500,2500,0,0,2500,2500", augs12);
            Assert.AreEqual("0,0,0,0,0", charms12);
        }

        [TestMethod]
        public void PlayerTexts_ArePlainAscii()
        {
            foreach (var block in CharmSettingsManager.LeveledBlocks.Values)
                foreach (var level in new[] { 1, 50, 100 })
                {
                    var text = block.PlayerText(level);
                    Assert.IsFalse(string.IsNullOrEmpty(text), $"{block.Name} level {level}");
                    Assert.IsTrue(text.All(c => c < 128), $"{block.Name} level {level} has non-ASCII text: {text}");
                }
        }
    }
}
