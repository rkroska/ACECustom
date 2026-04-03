using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Database.Adapter;
using ACE.Database.Models.Shard;
using ACE.Database.Models.World;
using ACE.Entity.Enum;

namespace ACE.Database.Tests
{
    /// <summary>
    /// Verifies that the damage_type column on emote rows survives the read path through
    /// WeenieConverter (world DB) and BiotaConverter (shard DB), covering null, named enum,
    /// and bitmask-composite values.
    /// </summary>
    [TestClass]
    public class EmoteDamageTypeConverterTests
    {
        // -----------------------------------------------------------------------
        // WeenieConverter (world DB → entity)
        // -----------------------------------------------------------------------

        private static Weenie MakeWeenie(int? damageType)
        {
            var weenie = new Weenie { ClassId = 1, ClassName = "test", Type = 0 };
            weenie.WeeniePropertiesEmote.Add(new WeeniePropertiesEmote
            {
                Category = (uint)EmoteCategory.ReceiveDamage,
                Probability = 1.0f,
                DamageType = damageType
            });
            return weenie;
        }

        [TestMethod]
        public void WeenieConverter_NullDamageType_MapsToNull()
        {
            var entity = WeenieConverter.ConvertToEntityWeenie(MakeWeenie(null));

            var emote = entity.PropertiesEmote.Single();
            Assert.IsNull(emote.DamageType);
        }

        [TestMethod]
        public void WeenieConverter_SlashDamageType_MapsToEnum()
        {
            var entity = WeenieConverter.ConvertToEntityWeenie(MakeWeenie((int)DamageType.Slash));

            var emote = entity.PropertiesEmote.Single();
            Assert.AreEqual(DamageType.Slash, emote.DamageType);
        }

        [TestMethod]
        public void WeenieConverter_BitmaskDamageType_PreservesValue()
        {
            // Slash | Pierce = 3; not a named single value but must round-trip correctly.
            int bitmask = (int)(DamageType.Slash | DamageType.Pierce);
            var entity = WeenieConverter.ConvertToEntityWeenie(MakeWeenie(bitmask));

            var emote = entity.PropertiesEmote.Single();
            Assert.AreEqual((DamageType)bitmask, emote.DamageType);
        }

        [TestMethod]
        public void WeenieConverter_PhysicalHelperBitmask_PreservesValue()
        {
            // DamageType.Physical = Slash | Pierce | Bludgeon, a named composite.
            int bitmask = (int)DamageType.Physical;
            var entity = WeenieConverter.ConvertToEntityWeenie(MakeWeenie(bitmask));

            var emote = entity.PropertiesEmote.Single();
            Assert.AreEqual(DamageType.Physical, emote.DamageType);
        }

        // -----------------------------------------------------------------------
        // BiotaConverter (shard DB → entity)
        // -----------------------------------------------------------------------

        private static Biota MakeBiota(int? damageType)
        {
            var biota = new Biota { Id = 1, WeenieClassId = 1, WeenieType = 0 };
            biota.BiotaPropertiesEmote.Add(new BiotaPropertiesEmote
            {
                Category = (uint)EmoteCategory.ReceiveDamage,
                Probability = 1.0f,
                DamageType = damageType
            });
            return biota;
        }

        [TestMethod]
        public void BiotaConverter_NullDamageType_MapsToNull()
        {
            var entity = BiotaConverter.ConvertToEntityBiota(MakeBiota(null));

            var emote = entity.PropertiesEmote.Single();
            Assert.IsNull(emote.DamageType);
        }

        [TestMethod]
        public void BiotaConverter_SlashDamageType_MapsToEnum()
        {
            var entity = BiotaConverter.ConvertToEntityBiota(MakeBiota((int)DamageType.Slash));

            var emote = entity.PropertiesEmote.Single();
            Assert.AreEqual(DamageType.Slash, emote.DamageType);
        }

        [TestMethod]
        public void BiotaConverter_BitmaskDamageType_PreservesValue()
        {
            int bitmask = (int)(DamageType.Slash | DamageType.Pierce);
            var entity = BiotaConverter.ConvertToEntityBiota(MakeBiota(bitmask));

            var emote = entity.PropertiesEmote.Single();
            Assert.AreEqual((DamageType)bitmask, emote.DamageType);
        }

        // -----------------------------------------------------------------------
        // PropertiesEmote.Clone() preserves DamageType
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Clone_PreservesNullDamageType()
        {
            var entity = BiotaConverter.ConvertToEntityBiota(MakeBiota(null));
            var original = entity.PropertiesEmote.Single();

            var clone = original.Clone();
            Assert.IsNull(clone.DamageType);
        }

        [TestMethod]
        public void Clone_PreservesEnumDamageType()
        {
            var entity = BiotaConverter.ConvertToEntityBiota(MakeBiota((int)DamageType.Fire));
            var original = entity.PropertiesEmote.Single();

            var clone = original.Clone();
            Assert.AreEqual(DamageType.Fire, clone.DamageType);
        }
    }
}
