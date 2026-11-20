using System.Linq;

using ACE.Server.WorldObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// The Universal Quiver charm (WCID 78780032, ability 36) is wired into the ability-charm registry the same way as
    /// the other toggle charms, and is not a levelled charm (so it works in every tier). Pure: no database, no world.
    /// </summary>
    [TestClass]
    public class UniversalQuiverCharmTests
    {
        /// <summary>The charm WCID maps to the Universal Quiver ability, which is registered with its name.</summary>
        [TestMethod]
        public void CharmWcid_MapsToUniversalQuiverAbility()
        {
            Assert.AreEqual(CharmAbilityRegistry.UniversalQuiverAbilityId, CharmAbilityRegistry.GetAbilityIdForWCID(78780032));
            Assert.IsTrue(CharmAbilityRegistry.RegisteredIds.Contains(CharmAbilityRegistry.UniversalQuiverAbilityId));
            Assert.AreEqual("Universal Quiver", CharmAbilityRegistry.GetDisplayName(CharmAbilityRegistry.UniversalQuiverAbilityId));
        }

        /// <summary>No other ability constant uses the Universal Quiver's id.</summary>
        [TestMethod]
        public void AbilityId_IsNotShared()
        {
            // another ability on the same id would toggle the wrong player property
            var sameId = typeof(CharmAbilityRegistry).GetFields()
                .Where(f => f.IsLiteral && f.FieldType == typeof(int) && f.Name.EndsWith("AbilityId"))
                .Count(f => (int)f.GetRawConstantValue() == CharmAbilityRegistry.UniversalQuiverAbilityId);
            Assert.AreEqual(1, sameId);
        }

        /// <summary>The charm is not a levelled charm, so no tier rule limits it.</summary>
        [TestMethod]
        public void NotALevelledCharm_WorksInEveryTier()
        {
            Assert.IsNull(CharmSettingsManager.LeveledBlockFor(CharmAbilityRegistry.UniversalQuiverAbilityId));
        }
    }
}
