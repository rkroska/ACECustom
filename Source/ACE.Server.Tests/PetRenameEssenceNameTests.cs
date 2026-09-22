using ACE.Server.Controllers;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// An approved @pet-name keeps the essence's damage word and tier: only the creature part of
    /// "&lt;damage&gt; &lt;creature&gt; Essence (&lt;tier&gt;)" is replaced.
    /// </summary>
    [TestClass]
    public class PetRenameEssenceNameTests
    {
        [TestMethod]
        public void Rename_CapturedLook_KeepsDamageWord()
        {
            // the two renames approved on the test server before this fix
            Assert.AreEqual("Slash Sir Fluffington Essence",
                PetNamingController.ComposeRenamedEssenceName("Slash Spectral Nanjou Shou-jen Essence", "Spectral Nanjou Shou-jen", "Maiden", "Sir Fluffington"));
            Assert.AreEqual("Acid Nerd Parade Essence",
                PetNamingController.ComposeRenamedEssenceName("Acid Bandit Marv Essence", "Bandit Marv", "Moar", "Nerd Parade"));
        }

        [TestMethod]
        public void Rename_KeepsTier()
        {
            Assert.AreEqual("Fire Bob Essence (250)",
                PetNamingController.ComposeRenamedEssenceName("Fire Skeleton Samurai Essence (250)", "Skeleton Samurai", "Skeleton Samurai", "Bob"));
        }

        [TestMethod]
        public void Rename_NeverCaptured_UsesSummonTemplateName()
        {
            // no captured creature name: the template's two-word creature must not leave "Skeleton" behind
            Assert.AreEqual("Fire Bob Essence (250)",
                PetNamingController.ComposeRenamedEssenceName("Fire Skeleton Samurai Essence (250)", null, "Skeleton Samurai", "Bob"));
        }

        [TestMethod]
        public void Rename_Again_ReplacesThePreviousCustomName()
        {
            Assert.AreEqual("Slash Lord Fluff Essence",
                PetNamingController.ComposeRenamedEssenceName("Slash Sir Fluffington Essence", "Sir Fluffington", "Maiden", "Lord Fluff"));
        }

        [TestMethod]
        public void Rename_KeepsPossessiveInTheChosenName()
        {
            Assert.AreEqual("Fire Bob's Dog Essence (250)",
                PetNamingController.ComposeRenamedEssenceName("Fire Maiden Essence (250)", null, "Maiden", "Bob's Dog"));
        }

        [TestMethod]
        public void Rename_NameWithoutEssenceShape_BecomesTheNewName()
        {
            Assert.AreEqual("New Name",
                PetNamingController.ComposeRenamedEssenceName("Nerd Parade", "Nerd Parade", "Moar", "New Name"));
        }
    }
}
