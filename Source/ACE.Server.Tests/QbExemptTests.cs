using ACE.Server.Managers;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>The [NoQB] marker on a world quest row's message keeps bookkeeping quests out of the quest bonus.</summary>
    [TestClass]
    public class QbExemptTests
    {
        [TestMethod]
        public void MarkedMessage_IsExempt()
        {
            Assert.IsTrue(QuestManager.MessageIsQbExempt("[NoQB] Pilfer's Hoard: wrong searches this hunt"));
        }

        [TestMethod]
        public void MarkerIgnoresCaseAndLeadingSpaces()
        {
            Assert.IsTrue(QuestManager.MessageIsQbExempt("  [noqb]timer"));
        }

        [TestMethod]
        public void UnmarkedOrEmpty_EarnsQb()
        {
            Assert.IsFalse(QuestManager.MessageIsQbExempt("Pilfer's Hoard: started"));
            Assert.IsFalse(QuestManager.MessageIsQbExempt(""));
            Assert.IsFalse(QuestManager.MessageIsQbExempt(null));
        }

        [TestMethod]
        public void MarkerMustLead()
        {
            Assert.IsFalse(QuestManager.MessageIsQbExempt("A timer [NoQB]"));
        }
    }
}
