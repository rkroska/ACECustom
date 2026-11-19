using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Enlightenment keeps level and skills from Enlightenment enl_keeps_level_and_skills_from (50 by default) on, unless
    /// enl_resets_level_and_skills forces the old reset for everyone; the kept level stops at enl_keeps_level_up_to (700).
    /// Pins the pure rules (Enlightenment.ResetsLevelAndSkills, UnlocksKeepingLevelAndSkills, KeptLevel,
    /// Player.LevelSkillCreditAlreadyGiven) and the settings' code defaults.
    /// No DB, no world.
    /// </summary>
    [TestClass]
    public class EnlightenmentLevelResetTests
    {
        private static bool Resets(int currentEnlightenment, bool alwaysReset = false, long keepFrom = 50) =>
            Enlightenment.ResetsLevelAndSkills(currentEnlightenment, alwaysReset, keepFrom);

        private static bool Unlocks(int newEnlightenment, bool alwaysReset = false, long keepFrom = 50) =>
            Enlightenment.UnlocksKeepingLevelAndSkills(newEnlightenment, alwaysReset, keepFrom);

        [TestMethod]
        public void BelowTheThreshold_StillResets()
        {
            Assert.IsTrue(Resets(0));
            Assert.IsTrue(Resets(49));   // 49 -> 50 is the last enlightenment that resets
        }

        [TestMethod]
        public void FromTheThresholdOn_KeepsLevelAndSkills()
        {
            Assert.IsFalse(Resets(50));  // 50 -> 51 is the first enlightenment that keeps them
            Assert.IsFalse(Resets(51));
            Assert.IsFalse(Resets(325));
        }

        [TestMethod]
        public void AlwaysReset_OverridesTheThreshold()
        {
            Assert.IsTrue(Resets(50, alwaysReset: true));
            Assert.IsTrue(Resets(325, alwaysReset: true));
        }

        [TestMethod]
        public void ThresholdZero_EveryoneKeeps()
        {
            Assert.IsFalse(Resets(0, keepFrom: 0));
            Assert.IsFalse(Resets(1, keepFrom: 0));
        }

        [TestMethod]
        public void ThresholdMovesTheLine()
        {
            // 49 makes 49 -> 50 the first enlightenment that keeps them
            Assert.IsTrue(Resets(48, keepFrom: 49));
            Assert.IsFalse(Resets(49, keepFrom: 49));
        }

        [TestMethod]
        public void OnlyTheEnlightenmentReachingTheThreshold_AnnouncesIt()
        {
            Assert.IsTrue(Unlocks(50));
            Assert.IsFalse(Unlocks(49));
            Assert.IsFalse(Unlocks(51));
            Assert.IsFalse(Unlocks(50, alwaysReset: true));
            Assert.IsFalse(Unlocks(1, keepFrom: 0));   // with 0 everyone keeps them: there is nothing to unlock
        }

        [TestMethod]
        public void UnlockAndReset_Agree()
        {
            // the enlightenment that announces it still resets (it starts below the threshold); the next one keeps them
            for (var current = 0; current < 400; current++)
            {
                if (Unlocks(current + 1))
                {
                    Assert.IsTrue(Resets(current));
                    Assert.IsFalse(Resets(current + 1));
                }
            }
        }

        [TestMethod]
        public void Defaults_KeepFromFifty_WithoutAForcedReset()
        {
            Assert.AreEqual(50L, ServerConfig.enl_keeps_level_and_skills_from.Default);
            Assert.IsFalse(ServerConfig.enl_resets_level_and_skills.Default);
        }

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

        [TestMethod]
        public void LevelCreditsAlreadyGiven_AreNotGivenAgain()
        {
            // a character that enlightened at 1,000 and dropped to 700 earns no credits on the way back up to 1,000
            Assert.IsTrue(Player.LevelSkillCreditAlreadyGiven(705, 1000));
            Assert.IsTrue(Player.LevelSkillCreditAlreadyGiven(1000, 1000));
            Assert.IsFalse(Player.LevelSkillCreditAlreadyGiven(1005, 1000));

            // never lowered, or fully reset since: every level's credit is earned
            Assert.IsFalse(Player.LevelSkillCreditAlreadyGiven(705, null));
        }

        [TestMethod]
        public void Default_KeepsLevelUpTo700()
        {
            Assert.AreEqual(700L, ServerConfig.enl_keeps_level_up_to.Default);
        }
    }
}
