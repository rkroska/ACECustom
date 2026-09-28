using ACE.Entity.Enum;
using ACE.Server.Entity;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Captured essences from creatures with no CreatureType are registered under CreatureType.Unknown,
    /// so they earn CapturedEssenceUnknown / ShinyEssenceUnknown instead of skipping the species stamps.
    /// </summary>
    [TestClass]
    public class PetRegistrySpeciesTests
    {
        [TestMethod]
        public void ResolveSpeciesType_NoCreatureType_IsUnknown()
        {
            // Exhumed Bones Piles (35251) has no PropertyInt.CreatureType
            Assert.AreEqual((uint)CreatureType.Unknown, PetRegistryManager.ResolveSpeciesType(null));
            Assert.AreEqual(40u, PetRegistryManager.ResolveSpeciesType(null));
        }

        [TestMethod]
        public void ResolveSpeciesType_KeepsRealSpecies()
        {
            Assert.AreEqual((uint)CreatureType.Undead, PetRegistryManager.ResolveSpeciesType((int)CreatureType.Undead));
            Assert.AreEqual((uint)CreatureType.Wall, PetRegistryManager.ResolveSpeciesType((int)CreatureType.Wall));
        }

        [TestMethod]
        public void UnknownSpecies_StampNamesMatchExistingStamps()
        {
            // 25 accounts already hold CapturedEssenceUnknown from real CreatureType.Unknown captures
            var typeName = ((CreatureType)PetRegistryManager.ResolveSpeciesType(null)).ToString();

            Assert.AreEqual("CapturedEssenceUnknown", $"CapturedEssence{typeName}");
            Assert.AreEqual("ShinyEssenceUnknown", $"ShinyEssence{typeName}");
        }

        [TestMethod]
        public void DisplayName_ShinyWithPrefix_IsNotDoubled()
        {
            Assert.AreEqual("Shiny Exhumed Bones Piles", PetRegistryManager.GetRegistryDisplayName("Shiny Exhumed Bones Piles", true));
        }

        [TestMethod]
        public void DisplayName_ShinyWithoutPrefix_GetsPrefix()
        {
            Assert.AreEqual("Shiny Drudge Skulker", PetRegistryManager.GetRegistryDisplayName("Drudge Skulker", true));
        }

        [TestMethod]
        public void DisplayName_Normal_IsUnchanged()
        {
            Assert.AreEqual("Drudge Skulker", PetRegistryManager.GetRegistryDisplayName("Drudge Skulker", false));
            Assert.AreEqual("Shiny Rock", PetRegistryManager.GetRegistryDisplayName("Shiny Rock", false));
        }
    }
}
