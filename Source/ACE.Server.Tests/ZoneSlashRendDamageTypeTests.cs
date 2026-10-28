using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.WorldObjects;

namespace ACE.Server.Tests
{
    /// <summary>Tier 11+ melee weapons with Slash Rending on a Slashing/Piercing base deal Slashing only (owner 2026-10-28).</summary>
    [TestClass]
    public class ZoneSlashRendDamageTypeTests
    {
        private const DamageType SlashPierce = DamageType.Slash | DamageType.Pierce;

        private static uint nextGuid = 0xF0005750;

        private static MeleeWeapon Sword(DamageType damageType, ImbuedEffectType imbue, int? weaponTier = 11, int? zcTier = null)
        {
            var weenie = new Weenie { WeenieClassId = 457500, ClassName = "TestSlashRendSword", WeenieType = WeenieType.MeleeWeapon };
            var w = new MeleeWeapon(weenie, new ObjectGuid(nextGuid++));
            w.W_DamageType = damageType;
            w.ImbuedEffect = imbue;
            if (weaponTier.HasValue) w.SetProperty(PropertyInt.WeaponAugScaleTier, weaponTier.Value);
            if (zcTier.HasValue) w.SetProperty(PropertyInt.ZcTier, zcTier.Value);
            return w;
        }

        [TestMethod]
        public void T11_SlashPierce_WithSlashRend_Applies()
        {
            Assert.IsTrue(ZoneSlashRendDamageType.Applies(Sword(SlashPierce, ImbuedEffectType.SlashRending)));
            Assert.IsTrue(ZoneSlashRendDamageType.Applies(Sword(SlashPierce, ImbuedEffectType.SlashRending, weaponTier: 25)));
            // tier read the same way the rest of the T11 gates read it: ZcTier OR the legacy WeaponAugScaleTier
            Assert.IsTrue(ZoneSlashRendDamageType.Applies(Sword(SlashPierce, ImbuedEffectType.SlashRending, weaponTier: null, zcTier: 12)));
            // the rend sharing the slot with another imbue still counts
            Assert.IsTrue(ZoneSlashRendDamageType.Applies(Sword(SlashPierce, ImbuedEffectType.SlashRending | ImbuedEffectType.ArmorRending)));
        }

        [TestMethod]
        public void Tier10AndBelow_NeverApplies()
        {
            Assert.IsFalse(ZoneSlashRendDamageType.Applies(Sword(SlashPierce, ImbuedEffectType.SlashRending, weaponTier: 10)));
            Assert.IsFalse(ZoneSlashRendDamageType.Applies(Sword(SlashPierce, ImbuedEffectType.SlashRending, weaponTier: null)));
        }

        [TestMethod]
        public void OtherRendsAndTypes_NeverApply()
        {
            Assert.IsFalse(ZoneSlashRendDamageType.Applies(Sword(SlashPierce, ImbuedEffectType.PierceRending)), "pierce rend");
            Assert.IsFalse(ZoneSlashRendDamageType.Applies(Sword(SlashPierce, ImbuedEffectType.Undef)), "no rend");
            Assert.IsFalse(ZoneSlashRendDamageType.Applies(Sword(SlashPierce, ImbuedEffectType.ArmorRending)), "armor rend only");
            Assert.IsFalse(ZoneSlashRendDamageType.Applies(Sword(DamageType.Slash, ImbuedEffectType.SlashRending)), "already slash only");
            Assert.IsFalse(ZoneSlashRendDamageType.Applies(Sword(DamageType.Slash | DamageType.Bludgeon, ImbuedEffectType.SlashRending)), "not slash/pierce");
        }

        [TestMethod]
        public void NotAMeleeWeapon_NeverApplies()
        {
            var weenie = new Weenie { WeenieClassId = 457501, ClassName = "TestSlashRendBow", WeenieType = WeenieType.MissileLauncher };
            var bow = new MissileLauncher(weenie, new ObjectGuid(nextGuid++));
            bow.W_DamageType = SlashPierce;
            bow.ImbuedEffect = ImbuedEffectType.SlashRending;
            bow.SetProperty(PropertyInt.WeaponAugScaleTier, 15);
            Assert.IsFalse(ZoneSlashRendDamageType.Applies(bow));
            Assert.IsFalse(ZoneSlashRendDamageType.Normalize(bow, existingItem: true));
            Assert.AreEqual(SlashPierce, bow.W_DamageType);
        }

        [TestMethod]
        public void Effective_IsSlash_OnlyForCoveredWeapons()
        {
            var covered = Sword(SlashPierce, ImbuedEffectType.SlashRending);
            Assert.AreEqual(DamageType.Slash, ZoneSlashRendDamageType.Effective(covered, covered.W_DamageType));
            Assert.AreEqual(SlashPierce, covered.W_DamageType, "Effective never writes the item");

            var t10 = Sword(SlashPierce, ImbuedEffectType.SlashRending, weaponTier: 10);
            Assert.AreEqual(SlashPierce, ZoneSlashRendDamageType.Effective(t10, t10.W_DamageType));

            var pierceRend = Sword(SlashPierce, ImbuedEffectType.PierceRending);
            Assert.AreEqual(SlashPierce, ZoneSlashRendDamageType.Effective(pierceRend, pierceRend.W_DamageType));
        }

        [TestMethod]
        public void Normalize_WritesSlash_DropsPiercingTint_AndIsIdempotent()
        {
            var w = Sword(SlashPierce, ImbuedEffectType.SlashRending);
            w.UiEffects = UiEffects.Slashing | UiEffects.Piercing | UiEffects.Magical;

            Assert.IsTrue(ZoneSlashRendDamageType.Normalize(w, existingItem: false));
            Assert.AreEqual(DamageType.Slash, w.W_DamageType);
            Assert.AreEqual(UiEffects.Slashing | UiEffects.Magical, w.UiEffects);
            Assert.IsTrue(w.HasImbuedEffect(ImbuedEffectType.SlashRending), "the rend stays");

            Assert.IsFalse(ZoneSlashRendDamageType.Normalize(w, existingItem: false), "second call is a no-op");
        }

        [TestMethod]
        public void Normalize_LeavesTier10Alone()
        {
            var w = Sword(SlashPierce, ImbuedEffectType.SlashRending, weaponTier: 10);
            w.UiEffects = UiEffects.Slashing | UiEffects.Piercing;

            Assert.IsFalse(ZoneSlashRendDamageType.Normalize(w, existingItem: true));
            Assert.AreEqual(SlashPierce, w.W_DamageType);
            Assert.AreEqual(UiEffects.Slashing | UiEffects.Piercing, w.UiEffects);
        }

        [TestMethod]
        public void LoadFromBiota_RewritesAndMarksForSave()
        {
            var saved = Sword(SlashPierce, ImbuedEffectType.SlashRending, weaponTier: 14);

            var loaded = new MeleeWeapon(saved.Biota);   // the restore-from-database ctor

            Assert.AreEqual(DamageType.Slash, loaded.W_DamageType);
            Assert.AreEqual((int)DamageType.Slash, loaded.Biota.PropertiesInt[PropertyInt.DamageType], "written to the biota, not an ephemeral copy");
            Assert.IsTrue(loaded.ChangesDetected, "the rewrite must be saved with the owner's next save");
        }

        [TestMethod]
        public void LoadFromBiota_Tier10_Untouched()
        {
            var saved = Sword(SlashPierce, ImbuedEffectType.SlashRending, weaponTier: 10);

            var loaded = new MeleeWeapon(saved.Biota);

            Assert.AreEqual(SlashPierce, loaded.W_DamageType);
        }
    }
}
