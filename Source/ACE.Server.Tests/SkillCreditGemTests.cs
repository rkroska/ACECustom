using ACE.Entity.Enum.Properties;
using ACE.Server.WorldObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    [TestClass]
    public class SkillCreditGemTests
    {
        [TestMethod]
        public void EachGemGivesItsCredits()
        {
            Assert.AreEqual(4, Player.SkillCreditGems.Count);
            Assert.AreEqual(1, Player.SkillCreditGems[98760540]);
            Assert.AreEqual(10, Player.SkillCreditGems[98760541]);
            Assert.AreEqual(50, Player.SkillCreditGems[98760542]);
            Assert.AreEqual(100, Player.SkillCreditGems[98760543]);
        }

        [TestMethod]
        public void SpecializedSkillAugmentationGemsAreNotSkillCreditGems()
        {
            // 1x, 10x, 50x and 100x Specialized Skill Augmentation Gems: they only raise the specialization limit
            foreach (var wcid in new uint[] { 300021, 81000131, 81000141, 81000151 })
                Assert.IsFalse(Player.SkillCreditGems.ContainsKey(wcid), wcid.ToString());
        }

        [TestMethod]
        public void GemCreditsAddUp()
        {
            Assert.AreEqual(1, Player.AddSkillCreditsFromGems(0, 1));
            Assert.AreEqual(161, Player.AddSkillCreditsFromGems(61, 100));
        }

        [TestMethod]
        public void GemCreditsNeverGoNegativeOrOverflow()
        {
            Assert.AreEqual(10, Player.AddSkillCreditsFromGems(-5, 10));
            Assert.AreEqual(7, Player.AddSkillCreditsFromGems(7, -3));
            Assert.AreEqual(int.MaxValue, Player.AddSkillCreditsFromGems(int.MaxValue - 5, 100));
        }

        [TestMethod]
        public void GemCreditsHaveTheirOwnProperty()
        {
            Assert.AreEqual(50606, (int)PropertyInt.SkillCreditsFromGems);
        }
    }
}
