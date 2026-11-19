using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// From Enlightenment enl_keeps_level_and_skills_from (50 by default) on, enlightening no longer resets skills, unless
    /// enl_resets_level_and_skills forces the old reset for everyone. The level returns to 1, or is kept (up to
    /// enl_keeps_level_up_to, 700) after a Level Retention Stone.
    /// Pins the pure rules (Enlightenment.ResetsLevelAndSkills, UnlocksKeepingLevelAndSkills, KeptLevel, PlannedOutcome,
    /// LevelRetentionStoneRefusal, Player.LevelSkillCreditAlreadyGiven) and the settings' code defaults.
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
        public void FromTheThresholdOn_DoesNotReset()
        {
            Assert.IsFalse(Resets(50));  // 50 -> 51 is the first enlightenment that keeps the skills
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
            // a character that enlightened at 1,000 earns no credits on the way back up to 1,000, from 700 (with a stone) or
            // from 1 (without one)
            Assert.IsTrue(Player.LevelSkillCreditAlreadyGiven(705, 1000));
            Assert.IsTrue(Player.LevelSkillCreditAlreadyGiven(5, 1000));
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

        private static Enlightenment.Outcome Planned(int enlightenment, int level, bool alwaysReset = false, long keepFrom = 50, long keepUpTo = 700, bool stone = true) =>
            Enlightenment.PlannedOutcome(enlightenment, level, alwaysReset, keepFrom, keepUpTo, stone);

        [TestMethod]
        public void Outcome_BelowTheThresholdOrForced_IsTheReset()
        {
            Assert.AreEqual(new Enlightenment.Outcome(true, null), Planned(49, 1200));
            Assert.AreEqual(new Enlightenment.Outcome(true, null), Planned(60, 1200, alwaysReset: true));
        }

        [TestMethod]
        public void Outcome_WithAStone_KeepsTheLevelOrDropsItTo700()
        {
            Assert.AreEqual(new Enlightenment.Outcome(false, null), Planned(60, 700));
            Assert.AreEqual(new Enlightenment.Outcome(false, 700), Planned(60, 701));
            Assert.AreEqual(new Enlightenment.Outcome(false, null), Planned(60, 1500, keepUpTo: 0));
        }

        [TestMethod]
        public void Outcome_ComparesThePromiseNotTheLevel()
        {
            // the confirmation can stay open while the character levels: only a different promise stops the ritual
            Assert.AreEqual(Planned(60, 690), Planned(60, 699));      // still "you keep your level"
            Assert.AreEqual(Planned(60, 900), Planned(60, 905));      // still "your level will drop to 700"
            Assert.AreNotEqual(Planned(60, 699), Planned(60, 701));   // "keep" became "drop to 700"
        }

        [TestMethod]
        public void Outcome_WithoutAStone_TheLevelReturnsTo1AndSkillsStay()
        {
            Assert.AreEqual(new Enlightenment.Outcome(false, 1), Planned(60, 700, stone: false));
            Assert.AreEqual(new Enlightenment.Outcome(false, 1), Planned(60, 1500, stone: false));
            Assert.AreNotEqual(Planned(60, 690, stone: true), Planned(60, 690, stone: false));   // using a stone changes the promise
        }

        [TestMethod]
        public void Stone_NeedsTheThresholdEnlightenment()
        {
            Assert.IsNotNull(Enlightenment.LevelRetentionStoneRefusal(49, false, 50, false));
            Assert.IsNull(Enlightenment.LevelRetentionStoneRefusal(50, false, 50, false));
            Assert.IsNull(Enlightenment.LevelRetentionStoneRefusal(0, false, 0, false));   // 0: everyone keeps skills
        }

        [TestMethod]
        public void Stone_OnePerEnlightenment_AndNothingWhileEveryoneResets()
        {
            Assert.IsNotNull(Enlightenment.LevelRetentionStoneRefusal(60, false, 50, true));   // already used for the next one
            Assert.IsNotNull(Enlightenment.LevelRetentionStoneRefusal(60, true, 50, false));   // enl_resets_level_and_skills
        }

        [TestMethod]
        public void Stone_Wcid()
        {
            Assert.AreEqual(98760530u, Enlightenment.LevelRetentionStoneWcid);
        }
    }
}
