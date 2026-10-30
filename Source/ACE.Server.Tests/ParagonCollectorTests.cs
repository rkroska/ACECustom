using System;

using ACE.Entity.Enum;
using ACE.Server.Entity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Which line a handed-in item is on, and when it counts as finished (ParagonCollectorService). Pure: no database, no world.
    /// The Use texts are the ones the world DB recipes write (scan of 2026-10-29).
    /// </summary>
    [TestClass]
    public class ParagonCollectorTests
    {
        private const string ArmorUse50 = "Enhanced with the powers of the Paragon through level 50.";
        private const string IlluminatedUse20 = "Illuminated from the craftsmanship of the Tumerok through level 20.";
        private const string IlluminatedSetUse15 = "Illuminated from the craftsmanship of the Tumerok through level 15. This item has the powers of the Heroic Destroyer Set.";
        private const string TrinketUse6 = "Quiddity Essence strength is level 6.";

        /// <summary>A stand-in for the cook_book lookup: the item counts as a target of exactly the given lines.</summary>
        private static Func<ParagonTrackRule, bool> TargetOf(params ParagonTrack[] tracks) => rule => Array.IndexOf(tracks, rule.Track) >= 0;

        /// <summary>Classifies an item described by its weenie type, item type, EquipmentSetId and Use text.</summary>
        private static ParagonTrack Classify(WeenieType wt, ItemType it, int? set, string use, params ParagonTrack[] targetOf)
            => ParagonCollectorService.Classify(wt, it, set, use, TargetOf(targetOf));

        [TestMethod]
        public void Armor_NeedsTheParagonTextAndAnArmorTarget()
        {
            Assert.AreEqual(ParagonTrack.Armor, Classify(WeenieType.Clothing, ItemType.Armor, null, ArmorUse50, ParagonTrack.Armor));
            // boots and gloves are ItemType Clothing but on the armor line
            Assert.AreEqual(ParagonTrack.Armor, Classify(WeenieType.Clothing, ItemType.Clothing, null, ArmorUse50, ParagonTrack.Armor));
            // a shield is a Generic armor item, not a weapon
            Assert.AreEqual(ParagonTrack.Armor, Classify(WeenieType.Generic, ItemType.Armor, null, ArmorUse50, ParagonTrack.Armor));
            Assert.AreEqual(ParagonTrack.None, Classify(WeenieType.Clothing, ItemType.Armor, null, ArmorUse50));
            Assert.AreEqual(ParagonTrack.None, Classify(WeenieType.Clothing, ItemType.Armor, null, null, ParagonTrack.Armor));
        }

        [TestMethod]
        public void Weapon_NeedsAParagonEquipmentSet()
        {
            Assert.AreEqual(ParagonTrack.Weapon, Classify(WeenieType.MeleeWeapon, ItemType.MeleeWeapon, 140, null));
            Assert.AreEqual(ParagonTrack.Weapon, Classify(WeenieType.Caster, ItemType.Caster, 139, null));
            Assert.AreEqual(ParagonTrack.Weapon, Classify(WeenieType.MissileLauncher, ItemType.MissileWeapon, 138, null));
            Assert.AreEqual(ParagonTrack.Weapon, Classify(WeenieType.Generic, ItemType.MissileWeapon, 138, null));
            Assert.AreEqual(ParagonTrack.None, Classify(WeenieType.MeleeWeapon, ItemType.MeleeWeapon, null, null));
            Assert.AreEqual(ParagonTrack.None, Classify(WeenieType.MeleeWeapon, ItemType.MeleeWeapon, 137, null));
            // a weapon never falls through to the text checks
            Assert.AreEqual(ParagonTrack.None, Classify(WeenieType.MeleeWeapon, ItemType.MeleeWeapon, null, ArmorUse50, ParagonTrack.Armor));
        }

        [TestMethod]
        public void Illuminated_SplitsJewelryFromClothingByTarget()
        {
            Assert.AreEqual(ParagonTrack.Clothing, Classify(WeenieType.Clothing, ItemType.Clothing, null, IlluminatedUse20, ParagonTrack.Clothing));
            Assert.AreEqual(ParagonTrack.Jewelry, Classify(WeenieType.Generic, ItemType.Jewelry, null, IlluminatedSetUse15, ParagonTrack.Jewelry));
            Assert.AreEqual(ParagonTrack.None, Classify(WeenieType.Generic, ItemType.Jewelry, null, IlluminatedSetUse15));
            // the Tumerok text alone never makes an armor-line item count as armor
            Assert.AreEqual(ParagonTrack.None, Classify(WeenieType.Clothing, ItemType.Armor, null, IlluminatedUse20, ParagonTrack.Armor));
        }

        [TestMethod]
        public void Trinket_AcceptsBothQuiddityTexts()
        {
            Assert.AreEqual(ParagonTrack.Trinket, Classify(WeenieType.Generic, ItemType.Jewelry, null, TrinketUse6, ParagonTrack.Trinket));
            Assert.AreEqual(ParagonTrack.Trinket, Classify(WeenieType.Generic, ItemType.Jewelry, null, ParagonCollectorService.TrinketAlignedUse, ParagonTrack.Trinket));
            Assert.AreEqual(ParagonTrack.None, Classify(WeenieType.Generic, ItemType.Jewelry, null, TrinketUse6));
        }

        [TestMethod]
        public void Finished_IsTheTopTierOfEachLine()
        {
            Assert.AreEqual(50, ParagonCollectorService.Armor.RequiredLevel);
            Assert.AreEqual(50, ParagonCollectorService.Weapon.RequiredLevel);
            Assert.AreEqual(20, ParagonCollectorService.Clothing.RequiredLevel);
            Assert.AreEqual(15, ParagonCollectorService.Jewelry.RequiredLevel);
            Assert.AreEqual(6, ParagonCollectorService.Trinket.RequiredLevel);

            Assert.IsTrue(ParagonCollectorService.IsFinished(ParagonCollectorService.Armor, 50));
            Assert.IsFalse(ParagonCollectorService.IsFinished(ParagonCollectorService.Armor, 49));
            Assert.IsFalse(ParagonCollectorService.IsFinished(ParagonCollectorService.Armor, null));
            Assert.IsTrue(ParagonCollectorService.IsFinished(ParagonCollectorService.Trinket, 6));
            Assert.IsFalse(ParagonCollectorService.IsFinished(ParagonCollectorService.Trinket, 5));
            Assert.IsFalse(ParagonCollectorService.IsFinished(null, 50));
        }

        [TestMethod]
        public void Rules_RequireTheCompletionStamps()
        {
            Assert.AreEqual("ParagonArmorCompleted", ParagonCollectorService.Armor.Stamp);
            Assert.AreEqual("ParagonEnlCompleted", ParagonCollectorService.Weapon.Stamp);
            Assert.AreEqual("IlluminatedClothingCompleted", ParagonCollectorService.Clothing.Stamp);
            Assert.AreEqual("IlluminatedJewelryCompleted", ParagonCollectorService.Jewelry.Stamp);
            Assert.AreEqual("AlignMyQuiddity", ParagonCollectorService.Trinket.Stamp);
        }

        /// <summary>Every refusal and thanks fragment reaches the AC client, so it must be printable 7-bit ASCII.</summary>
        [TestMethod]
        public void PlayerText_IsPlainAscii()
        {
            foreach (var rule in new[] { ParagonCollectorService.Armor, ParagonCollectorService.Weapon, ParagonCollectorService.Clothing, ParagonCollectorService.Jewelry, ParagonCollectorService.Trinket })
                foreach (var s in new[] { rule.Plural, rule.Singular, rule.MissingStamp })
                    foreach (var c in s)
                        Assert.IsTrue(c >= 32 && c < 127, $"{rule.Track}: non-ASCII or control character in \"{s}\"");
        }
    }
}
