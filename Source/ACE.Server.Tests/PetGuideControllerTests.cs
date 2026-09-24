using System.Linq;
using System.Reflection;

using ACE.Server.Controllers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    [TestClass]
    public class PetGuideControllerTests
    {
        /// <summary>
        /// Static fields initialise in declaration order. A GuideNpcs entry that points at a static array
        /// declared further down the class is captured as null, and /api/pet-guide then throws for everyone.
        /// </summary>
        [TestMethod]
        public void GuideNpcs_EveryEntryHasWcids()
        {
            var field = typeof(PetGuideController).GetField("GuideNpcs", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "GuideNpcs field not found");

            var npcs = ((string Key, uint[] Wcids)[])field.GetValue(null);
            Assert.IsNotNull(npcs);

            foreach (var (key, wcids) in npcs)
            {
                Assert.IsNotNull(wcids, $"GuideNpcs entry '{key}' has a null wcid list");
                Assert.IsTrue(wcids.Length > 0 && wcids.All(w => w != 0), $"GuideNpcs entry '{key}' has no usable wcids");
            }
        }

        [TestMethod]
        public void GuideItems_EveryEntryHasAWcid()
        {
            var field = typeof(PetGuideController).GetField("GuideItems", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "GuideItems field not found");

            var items = ((string Key, uint Wcid)[])field.GetValue(null);
            Assert.IsNotNull(items);
            foreach (var (key, wcid) in items)
                Assert.AreNotEqual(0u, wcid, $"GuideItems entry '{key}' has no wcid");
        }
    }
}
