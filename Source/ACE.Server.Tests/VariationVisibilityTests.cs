using System.Reflection;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Managers;
using ACE.Server.Physics;
using ACE.Server.WorldObjects;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    [TestClass]
    public class VariationVisibilityTests
    {
        private static void AssignPhysicsObj(WorldObject wo, PhysicsObj physicsObj)
        {
            var prop = typeof(WorldObject).GetProperty(nameof(WorldObject.PhysicsObj), BindingFlags.Instance | BindingFlags.Public);
            var setter = prop?.GetSetMethod(nonPublic: true);
            Assert.IsNotNull(setter, "WorldObject.PhysicsObj should expose a setter for tests.");
            setter.Invoke(wo, new object[] { physicsObj });
        }

        private static GenericObject CreateTestGenericObject()
        {
            var weenie = new Weenie
            {
                WeenieClassId = 424242,
                ClassName = "TestVariationVisibilityWeenie",
                WeenieType = WeenieType.Generic
            };
            return new GenericObject(weenie, new ObjectGuid(0xF0004242));
        }

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

        [TestMethod]
        public void GetEffectiveVariationForVisibility_LocationVariationTakesPrecedenceOverPhysics()
        {
            const int locationVar = 7;
            const int physicsVar = 99;

            var wo = CreateTestGenericObject();
            wo.SetPosition(PositionType.Location, new Position(0x00A80101, 12f, 12f, 12f, 0f, 0f, 0f, 1f, false, locationVar));
            AssignPhysicsObj(wo, new PhysicsObj(physicsVar));

            Assert.AreEqual(locationVar, PrestigeManager.GetEffectiveVariationForVisibility(wo));
        }

        [TestMethod]
        public void GetEffectiveVariationForVisibility_FallsBackToPhysicsWhenLocationVariationUnset()
        {
            const int physicsVar = 5;

            var wo = CreateTestGenericObject();
            wo.SetPosition(PositionType.Location, new Position(0x00A80101, 12f, 12f, 12f, 0f, 0f, 0f, 1f, false, VariationId: null));
            AssignPhysicsObj(wo, new PhysicsObj(physicsVar));

            Assert.AreEqual(physicsVar, PrestigeManager.GetEffectiveVariationForVisibility(wo));
        }
    }
}
