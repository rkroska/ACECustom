using ACE.Entity.Enum.Properties;
using ACE.Server.Managers.ZoneControl;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Reflection boss property ids (moved 2026-11-10 out of the Zone loot property block 58000-58999). Live event weenies set
    /// these by number, so a renumbering must be deliberate: it needs the weenies changed in the same update.
    /// </summary>
    [TestClass]
    public class ReflectionPropertyIdsTests
    {
        /// <summary>The ids are exactly the numbers the live weenies use.</summary>
        [TestMethod]
        public void ReflectionIds_AreTheNumbersWeeniesUse()
        {
            Assert.AreEqual(59200, (int)PropertyInt.ReflectionLives);
            Assert.AreEqual(59201, (int)PropertyInt.ReflectionLife);
            Assert.AreEqual(59202, (int)PropertyInt.ReflectionWcid);
            Assert.AreEqual(59203, (int)PropertyInt.ReflectionDamageRatingStep);
            Assert.AreEqual(59204, (int)PropertyInt.ReflectionPick);
            Assert.AreEqual(59200, (int)PropertyFloat.ReflectionHealthStep);
            Assert.AreEqual(59201, (int)PropertyFloat.ReflectionRewardStep);
            Assert.AreEqual(59200, (int)PropertyString.ReflectionCopied);
            Assert.AreEqual(59201, (int)PropertyString.ReflectionNameFormat);
        }

        /// <summary>None of them is back inside the Zone loot property block.</summary>
        [TestMethod]
        public void ReflectionIds_AreOutsideTheLootBlock()
        {
            foreach (var id in new[]
            {
                (int)PropertyInt.ReflectionLives, (int)PropertyInt.ReflectionLife, (int)PropertyInt.ReflectionWcid,
                (int)PropertyInt.ReflectionDamageRatingStep, (int)PropertyInt.ReflectionPick,
                (int)PropertyFloat.ReflectionHealthStep, (int)PropertyFloat.ReflectionRewardStep,
                (int)PropertyString.ReflectionCopied, (int)PropertyString.ReflectionNameFormat,
            })
                Assert.IsFalse(id >= ZoneLootProps.BlockMin && id <= ZoneLootProps.BlockMax, $"{id} is in the loot property block");
        }
    }
}
