using ACE.Server.Command.Handlers;
using ACE.Server.Entity;
using ACE.Server.Managers;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// A Level Retention Stone makes the next enlightenment keep the character's level (up to enl_keeps_level_up_to, 700)
    /// instead of returning it to 1. Skills reset either way, and the credit pool is rebuilt for the kept level.
    /// Pins the pure rules (Enlightenment.KeptLevel, PlannedOutcome, LevelSkillCredits, LevelRetentionStoneRefusal) and the
    /// settings' code defaults. No DB, no world.
    /// </summary>
    [TestClass]
    public class LevelRetentionStoneTests
    {
        [TestMethod]
        public void KeptLevel_UpToTheCapIsKept()
        {
            Assert.AreEqual(325, Enlightenment.KeptLevel(325, 700));
            Assert.AreEqual(700, Enlightenment.KeptLevel(700, 700));
        }

        [TestMethod]
        public void KeptLevel_AboveTheCapDropsToIt()
        {
            Assert.AreEqual(700, Enlightenment.KeptLevel(701, 700));
            Assert.AreEqual(700, Enlightenment.KeptLevel(1500, 700));
        }

        [TestMethod]
        public void KeptLevel_CapZeroKeepsAnyLevel()
        {
            Assert.AreEqual(1500, Enlightenment.KeptLevel(1500, 0));
            Assert.AreEqual(1500, Enlightenment.KeptLevel(1500, -1));
        }

        private static Enlightenment.Outcome Planned(int level, bool stone = true, long keepUpTo = 700) =>
            Enlightenment.PlannedOutcome(level, stone, keepUpTo);

        [TestMethod]
        public void Outcome_WithoutAStone_TheLevelReturnsTo1()
        {
            Assert.AreEqual(new Enlightenment.Outcome(false, null), Planned(700, stone: false));
            Assert.AreEqual(new Enlightenment.Outcome(false, null), Planned(1500, stone: false));
        }

        [TestMethod]
        public void Outcome_WithAStone_KeepsTheLevelOrDropsItTo700()
        {
            Assert.AreEqual(new Enlightenment.Outcome(true, null), Planned(700));
            Assert.AreEqual(new Enlightenment.Outcome(true, 700), Planned(701));
            Assert.AreEqual(new Enlightenment.Outcome(true, null), Planned(1500, keepUpTo: 0));
        }

        [TestMethod]
        public void Outcome_ComparesThePromiseNotTheLevel()
        {
            // the confirmation can stay open while the character levels: only a different promise stops the ritual
            Assert.AreEqual(Planned(690), Planned(699));                        // still "the stone keeps your level"
            Assert.AreEqual(Planned(900), Planned(905));                        // still "your level will drop to 700"
            Assert.AreEqual(Planned(300, stone: false), Planned(900, stone: false));
            Assert.AreNotEqual(Planned(699), Planned(701));                     // "keep" became "drop to 700"
            Assert.AreNotEqual(Planned(690), Planned(690, stone: false));
        }

        [TestMethod]
        public void LevelSkillCredits_MatchTheRetailChartThrough275()
        {
            // verify-skill-credits' retail table (level => level credits so far), read back as credits per level
            uint Chart(int level) => (uint)(DeveloperFixCommands.GetAdditionalCredits(level) - DeveloperFixCommands.GetAdditionalCredits(level - 1));

            for (var level = 1; level <= 275; level++)
                Assert.AreEqual(DeveloperFixCommands.GetAdditionalCredits(level), Enlightenment.LevelSkillCredits(level, Chart), $"level {level}");

            Assert.AreEqual(46, Enlightenment.LevelSkillCredits(275, Chart));
        }

        [TestMethod]
        public void LevelSkillCredits_OneEveryFifthLevelFrom275()
        {
            uint NoChart(int level) => 0;

            Assert.AreEqual(0, Enlightenment.LevelSkillCredits(274, NoChart));
            Assert.AreEqual(1, Enlightenment.LevelSkillCredits(275, NoChart));
            Assert.AreEqual(1, Enlightenment.LevelSkillCredits(279, NoChart));
            Assert.AreEqual(2, Enlightenment.LevelSkillCredits(280, NoChart));
            Assert.AreEqual(86, Enlightenment.LevelSkillCredits(700, NoChart));   // 275, 280, ... 700
        }

        [TestMethod]
        public void LevelSkillCredits_Level1HasNone()
        {
            Assert.AreEqual(0, Enlightenment.LevelSkillCredits(1, _ => 1));
        }

        [TestMethod]
        public void Stone_NeedsTheMinimumEnlightenment()
        {
            Assert.IsNotNull(Enlightenment.LevelRetentionStoneRefusal(49, 50, false));
            Assert.IsNull(Enlightenment.LevelRetentionStoneRefusal(50, 50, false));
            Assert.IsNull(Enlightenment.LevelRetentionStoneRefusal(0, 0, false));   // 0: any character
        }

        [TestMethod]
        public void Stone_OnePerEnlightenment()
        {
            Assert.IsNotNull(Enlightenment.LevelRetentionStoneRefusal(60, 50, true));   // already used for the next one
        }

        [TestMethod]
        public void Stone_Wcid()
        {
            Assert.AreEqual(98760530u, Enlightenment.LevelRetentionStoneWcid);
        }

        [TestMethod]
        public void Defaults_FromEnlightenment50_UpToLevel700()
        {
            Assert.AreEqual(50L, ServerConfig.enl_level_retention_from.Default);
            Assert.AreEqual(700L, ServerConfig.enl_keeps_level_up_to.Default);
        }
    }
}
