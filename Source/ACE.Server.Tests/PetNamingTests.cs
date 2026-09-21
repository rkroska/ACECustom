using ACE.Server.WorldObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Naming contract for a summoned combat pet.
    ///
    /// PetDevice.ResolveSummonedPetBaseName produces the base name; Pet.Init then prepends
    /// "Owner's ". The rule that matters: a staff-approved PetCustomName is the owner's own text and
    /// is used verbatim, while a capture-derived VisualOverrideName still has its chained "Owner's "
    /// prefixes stripped. Conflating the two turned "Flaw's Dingleberry" into "Dingleberry".
    /// </summary>
    [TestClass]
    public class PetNamingTests
    {
        // ---- growth-stage tag: removed only where ApplyMaturity puts it ------------------

        private static readonly string[] Stages = { "Newborn", "Whelp", "Juvenile", "Adolescent", "Young Adult", "Stage 1" };

        [TestMethod]
        public void StageTag_AfterOwnerPrefix_IsRemoved()
        {
            Assert.AreEqual("+Bob's Drudge", CombatPet.StripMaturityStageTag("+Bob's Whelp Drudge", Stages));
        }

        [TestMethod]
        public void StageWord_ElsewhereInTheName_IsKept()
        {
            // The old global Replace turned this into "+Bob's Gromnie" on every summon.
            Assert.AreEqual("+Bob's Gromnie Whelp", CombatPet.StripMaturityStageTag("+Bob's Gromnie Whelp", Stages));
        }

        [TestMethod]
        public void StackedTagsFromOlderBuilds_AreAllRemoved()
        {
            Assert.AreEqual("+Bob's Drudge", CombatPet.StripMaturityStageTag("+Bob's Adolescent Whelp Drudge", Stages));
        }

        [TestMethod]
        public void MultiWordStage_IsRemoved()
        {
            Assert.AreEqual("+Bob's Drudge", CombatPet.StripMaturityStageTag("+Bob's Young Adult Drudge", Stages));
        }

        [TestMethod]
        public void NoOwnerPrefix_TagAtStart_IsRemoved()
        {
            Assert.AreEqual("Drudge", CombatPet.StripMaturityStageTag("Newborn Drudge", Stages));
        }

        [TestMethod]
        public void UntaggedName_IsUnchanged()
        {
            Assert.AreEqual("+Bob's Flaw's Dingleberry", CombatPet.StripMaturityStageTag("+Bob's Flaw's Dingleberry", Stages));
        }

        // ---- approved custom names are verbatim ----------------------------------------

        [TestMethod]
        public void CustomName_WithPossessive_IsNotStripped()
        {
            Assert.AreEqual("Bob's Burgers",
                PetDevice.ResolveSummonedPetBaseName("Bob's Burgers", "Lightning Phyntos Browerk Essence"));
        }

        [TestMethod]
        public void CustomName_ReportedRegression_SurvivesIntact()
        {
            // The name that regressed in-game: everything before "'s " was being discarded.
            Assert.AreEqual("Flaw's Dingleberry",
                PetDevice.ResolveSummonedPetBaseName("Flaw's Dingleberry", "Lightning Phyntos Browerk Essence"));
        }

        [TestMethod]
        public void CustomName_WithSeveralPossessives_IsNotStripped()
        {
            Assert.AreEqual("Bob's Bill's Burgers",
                PetDevice.ResolveSummonedPetBaseName("Bob's Bill's Burgers", null));
        }

        [TestMethod]
        public void CustomName_WithoutPossessive_IsVerbatim()
        {
            Assert.AreEqual("Sparky", PetDevice.ResolveSummonedPetBaseName("Sparky", "Owner's Drudge"));
        }

        [TestMethod]
        public void CustomName_IsTrimmed()
        {
            Assert.AreEqual("Sparky", PetDevice.ResolveSummonedPetBaseName("  Sparky  ", null));
        }

        [TestMethod]
        public void CustomName_TakesPrecedenceOverVisualOverride()
        {
            Assert.AreEqual("Mr. Bitey", PetDevice.ResolveSummonedPetBaseName("Mr. Bitey", "Someone's Tusker"));
        }

        // ---- no custom name: legacy capture cleanup still applies -----------------------

        [TestMethod]
        public void NoCustomName_StripsOwnerPrefix()
        {
            Assert.AreEqual("Drudge", PetDevice.ResolveSummonedPetBaseName(null, "Schneebly's Drudge"));
        }

        [TestMethod]
        public void NoCustomName_StripsChainedOwnerPrefixes()
        {
            Assert.AreEqual("Drudge", PetDevice.ResolveSummonedPetBaseName(null, "A's B's Drudge"));
        }

        [TestMethod]
        public void NoCustomName_PlainNameUnchanged()
        {
            Assert.AreEqual("Lightning Phyntos Browerk Essence",
                PetDevice.ResolveSummonedPetBaseName(null, "Lightning Phyntos Browerk Essence"));
        }

        [TestMethod]
        public void EmptyCustomName_FallsBackToVisualOverride()
        {
            Assert.AreEqual("Drudge", PetDevice.ResolveSummonedPetBaseName("", "Schneebly's Drudge"));
            Assert.AreEqual("Drudge", PetDevice.ResolveSummonedPetBaseName("   ", "Schneebly's Drudge"));
        }

        [TestMethod]
        public void NothingSet_ReturnsNull()
        {
            Assert.IsNull(PetDevice.ResolveSummonedPetBaseName(null, null));
            Assert.IsNull(PetDevice.ResolveSummonedPetBaseName(null, ""));
        }

        // ---- the maturity stage tag inserts after the OWNER prefix, not inside the name --

        [TestMethod]
        public void StageTagInsertsAfterOwnerPrefix_NotInsideACustomName()
        {
            // CombatPet.ApplyMaturity inserts the stage tag at the first "'s ", which is the owner
            // prefix Pet.Init added. A possessive later in the name must not attract it.
            const string summoned = "Schneebly's Bob's Burgers";
            var idx = summoned.IndexOf("'s ", System.StringComparison.Ordinal);
            var tagged = summoned.Insert(idx + 3, "Whelp ");

            Assert.AreEqual("Schneebly's Whelp Bob's Burgers", tagged);
        }
    }
}
