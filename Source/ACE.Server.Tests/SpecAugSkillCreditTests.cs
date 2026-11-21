using ACE.Server.Command.Handlers;
using ACE.Server.WorldObjects;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Specialized Skill Augmentations give a skill credit each (Player.SpecAugSkillCreditsOwed), and the
    /// verify-skill-credits level credits follow the server's every-5th-level rule past 275
    /// </summary>
    [TestClass]
    public class SpecAugSkillCreditTests
    {
        [TestMethod]
        public void EveryAugNotYetPaidIsOwedOneCredit()
        {
            Assert.AreEqual(266, Player.SpecAugSkillCreditsOwed(266, 0));
            Assert.AreEqual(10, Player.SpecAugSkillCreditsOwed(276, 266));
            Assert.AreEqual(1, Player.SpecAugSkillCreditsOwed(1, 0));
        }

        [TestMethod]
        public void NothingIsOwedOncePaid()
        {
            Assert.AreEqual(0, Player.SpecAugSkillCreditsOwed(0, 0));
            Assert.AreEqual(0, Player.SpecAugSkillCreditsOwed(266, 266));
        }

        [TestMethod]
        public void FewerAugsThanCreditsPaidOwesNothing()
        {
            // the credits paid over a lower count are SpecAugSkillCreditsOver, which /testchar set augs takes back
            Assert.AreEqual(0, Player.SpecAugSkillCreditsOwed(100, 266));
            Assert.AreEqual(0, Player.SpecAugSkillCreditsOwed(-5, 0));
        }

        [TestMethod]
        public void CreditsPaidOverTheAugCountAreTheOnesTakenBack()
        {
            Assert.AreEqual(166, Player.SpecAugSkillCreditsOver(100, 266));
            Assert.AreEqual(266, Player.SpecAugSkillCreditsOver(0, 266));
            Assert.AreEqual(0, Player.SpecAugSkillCreditsOver(266, 266));
            Assert.AreEqual(0, Player.SpecAugSkillCreditsOver(276, 266));
        }

        [TestMethod]
        public void NegativeCountsAreTreatedAsZeroWhenTakingBack()
        {
            Assert.AreEqual(3, Player.SpecAugSkillCreditsOver(-5, 3));
            Assert.AreEqual(0, Player.SpecAugSkillCreditsOver(5, -10));
            Assert.AreEqual(int.MaxValue, Player.SpecAugSkillCreditsOver(0, long.MaxValue));
        }

        [TestMethod]
        public void ANegativePaidCountIsTreatedAsNothingPaid()
        {
            Assert.AreEqual(266, Player.SpecAugSkillCreditsOwed(266, -10));
        }

        [TestMethod]
        public void OwedCreditsNeverOverflowAnInt()
        {
            Assert.AreEqual(int.MaxValue, Player.SpecAugSkillCreditsOwed(long.MaxValue, 0));
        }

        [TestMethod]
        public void LevelCreditsFollowTheTableThrough274()
        {
            Assert.AreEqual(0, DeveloperFixCommands.GetAdditionalCredits(1));
            Assert.AreEqual(1, DeveloperFixCommands.GetAdditionalCredits(2));
            Assert.AreEqual(19, DeveloperFixCommands.GetAdditionalCredits(35));
            Assert.AreEqual(45, DeveloperFixCommands.GetAdditionalCredits(274));
        }

        [TestMethod]
        public void LevelCreditsPast275AddOneEveryFifthLevel()
        {
            Assert.AreEqual(46, DeveloperFixCommands.GetAdditionalCredits(275));
            Assert.AreEqual(46, DeveloperFixCommands.GetAdditionalCredits(279));
            Assert.AreEqual(47, DeveloperFixCommands.GetAdditionalCredits(280));
            Assert.AreEqual(191, DeveloperFixCommands.GetAdditionalCredits(1000));
        }
    }
}
