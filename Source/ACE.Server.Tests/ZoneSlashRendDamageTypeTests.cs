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

        // ---- swing animation: a thrust plays as a slash (owner 2026-10-28)

        [TestMethod]
        public void SwingsAsSlash_BeforeAndAfterTheRewrite()
        {
            var w = Sword(SlashPierce, ImbuedEffectType.SlashRending);
            w.W_AttackType = AttackType.Thrust | AttackType.Slash;
            Assert.IsTrue(ZoneSlashRendDamageType.SwingsAsSlash(w), "Slash|Pierce, not yet rewritten");

            ZoneSlashRendDamageType.Normalize(w, existingItem: true);
            Assert.AreEqual(DamageType.Slash, w.W_DamageType);
            Assert.IsTrue(ZoneSlashRendDamageType.SwingsAsSlash(w), "still slashes once its damage type reads Slash");

            var multi = Sword(DamageType.Slash, ImbuedEffectType.SlashRending, weaponTier: 20);
            multi.W_AttackType = AttackType.DoubleThrust | AttackType.DoubleSlash;
            Assert.IsTrue(ZoneSlashRendDamageType.SwingsAsSlash(multi));
        }

        [TestMethod]
        public void SwingsAsSlash_NeverForOtherWeapons()
        {
            MeleeWeapon W(DamageType dt, ImbuedEffectType imbue, AttackType at, int? tier = 11)
            {
                var w = Sword(dt, imbue, tier);
                w.W_AttackType = at;
                return w;
            }

            var thrustSlash = AttackType.Thrust | AttackType.Slash;
            Assert.IsFalse(ZoneSlashRendDamageType.SwingsAsSlash(W(SlashPierce, ImbuedEffectType.SlashRending, thrustSlash, tier: 10)), "tier 10");
            Assert.IsFalse(ZoneSlashRendDamageType.SwingsAsSlash(W(SlashPierce, ImbuedEffectType.PierceRending, thrustSlash)), "pierce rend");
            Assert.IsFalse(ZoneSlashRendDamageType.SwingsAsSlash(W(SlashPierce, ImbuedEffectType.Undef, thrustSlash)), "no rend");
            Assert.IsFalse(ZoneSlashRendDamageType.SwingsAsSlash(W(DamageType.Pierce, ImbuedEffectType.SlashRending, thrustSlash)), "pierce damage");
            Assert.IsFalse(ZoneSlashRendDamageType.SwingsAsSlash(W(DamageType.Slash | DamageType.Bludgeon, ImbuedEffectType.SlashRending, thrustSlash)), "slash/bludgeon damage");
            Assert.IsFalse(ZoneSlashRendDamageType.SwingsAsSlash(W(DamageType.Slash, ImbuedEffectType.SlashRending, AttackType.Thrust)), "a thrust-only weapon is never made to slash");
        }

        [TestMethod]
        public void SlashFor_KeepsHandAndHitCount()
        {
            Assert.AreEqual(AttackType.Slash, ZoneSlashRendDamageType.SlashFor(AttackType.Thrust));
            Assert.AreEqual(AttackType.DoubleSlash, ZoneSlashRendDamageType.SlashFor(AttackType.DoubleThrust));
            Assert.AreEqual(AttackType.TripleSlash, ZoneSlashRendDamageType.SlashFor(AttackType.TripleThrust));
            Assert.AreEqual(AttackType.OffhandSlash, ZoneSlashRendDamageType.SlashFor(AttackType.OffhandThrust));
            Assert.AreEqual(AttackType.OffhandDoubleSlash, ZoneSlashRendDamageType.SlashFor(AttackType.OffhandDoubleThrust));
            Assert.AreEqual(AttackType.OffhandTripleSlash, ZoneSlashRendDamageType.SlashFor(AttackType.OffhandTripleThrust));

            // anything that is not a thrust comes back unchanged
            foreach (var other in new[] { AttackType.Slash, AttackType.DoubleSlash, AttackType.OffhandSlash, AttackType.Punch, AttackType.Kick })
                Assert.AreEqual(other, ZoneSlashRendDamageType.SlashFor(other));
        }

        [TestMethod]
        public void CanReplace_RefusesInvalidOrIdenticalMotions()
        {
            // no motion table (id 0) and the CMT's "not found" answer are both refusals - no dat needed for these
            Assert.IsFalse(ZoneSlashRendDamageType.CanReplace(0, MotionStance.SwordCombat, MotionCommand.Invalid, MotionCommand.Invalid));
            Assert.IsFalse(ZoneSlashRendDamageType.CanReplace(0, MotionStance.SwordCombat, MotionCommand.ThrustMed, MotionCommand.Invalid));
            Assert.IsFalse(ZoneSlashRendDamageType.CanReplace(0, MotionStance.SwordCombat, MotionCommand.ThrustMed, MotionCommand.ThrustMed));
            Assert.IsFalse(ZoneSlashRendDamageType.CanReplace(0, MotionStance.SwordCombat, MotionCommand.ThrustMed, MotionCommand.SlashMed), "motion table 0 has no lengths");
        }
    }
}
