using ACE.Server.Managers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    [TestClass]
    public class VariationVisibilityTests
    {
        [TestMethod]
        public void SameVariationForVisibility_RetailNullAndZero_AreEqual()
        {
            Assert.IsTrue(PrestigeManager.SameVariationForVisibility(null, 0));
            Assert.IsTrue(PrestigeManager.SameVariationForVisibility(0, null));
            Assert.IsTrue(PrestigeManager.SameVariationForVisibility(null, null));
            Assert.IsTrue(PrestigeManager.SameVariationForVisibility(0, 0));
        }

        [TestMethod]
        public void SameVariationForVisibility_ExplicitRetailLayers_DistinctFromBaseAndEachOther()
        {
            Assert.IsFalse(PrestigeManager.SameVariationForVisibility(null, 2));
            Assert.IsFalse(PrestigeManager.SameVariationForVisibility(0, 2));
            Assert.IsFalse(PrestigeManager.SameVariationForVisibility(2, 3));
            Assert.IsTrue(PrestigeManager.SameVariationForVisibility(2, 2));
            Assert.IsTrue(PrestigeManager.SameVariationForVisibility(10, 10));
        }

        [TestMethod]
        public void SameVariationForVisibility_Prestige_RequiresExactMatch()
        {
            Assert.IsTrue(PrestigeManager.SameVariationForVisibility(11, 11));
            Assert.IsFalse(PrestigeManager.SameVariationForVisibility(11, 12));
            Assert.IsFalse(PrestigeManager.SameVariationForVisibility(11, null));
            Assert.IsFalse(PrestigeManager.SameVariationForVisibility(11, 0));
            Assert.IsFalse(PrestigeManager.SameVariationForVisibility(11, 10));
        }

        [TestMethod]
        public void GetEffectiveVariationForVisibility_NullWorldObject_ReturnsNull()
        {
            Assert.IsNull(PrestigeManager.GetEffectiveVariationForVisibility(null));
        }
    }
}
