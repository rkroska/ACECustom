using ACE.Server.WorldObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    [TestClass]
    public class PetBreedingTests
    {
        [TestMethod]
        [DataRow(50, 50)]
        [DataRow(79, 50)]
        [DataRow(80, 80)]
        [DataRow(90, 80)]
        [DataRow(100, 100)]
        [DataRow(112, 100)]
        [DataRow(125, 125)]
        [DataRow(135, 125)]
        [DataRow(150, 150)]
        [DataRow(165, 150)]
        [DataRow(180, 180)]
        [DataRow(190, 180)]
        [DataRow(200, 200)]
        [DataRow(225, 200)]
        [DataRow(250, 250)]
        [DataRow(275, 250)]
        [DataRow(300, 300)]
        [DataRow(350, 300)]
        public void GetFlooredPetLevel_SnapsToValidLowerTier(int target, int expected)
        {
            Assert.AreEqual(expected, PetDevice.GetFlooredPetLevel(target));
        }
    }
}
