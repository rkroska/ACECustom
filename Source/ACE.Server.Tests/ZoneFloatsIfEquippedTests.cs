using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.WorldObjects;

namespace ACE.Server.Tests
{
    /// <summary>ZoneStatResolver.FloatsIfEquipped (2026-10-29): the weapon card floats an unworn weapon would get on its next equip,
    /// shown by the appraisal panel and the loot properties. The nerf rule is passed in, so no live ladder is needed.</summary>
    [TestClass]
    public class ZoneFloatsIfEquippedTests
    {
        private static MeleeWeapon CreateWeapon()
        {
            var weenie = new Weenie { WeenieClassId = 424260, ClassName = "TestFloatsIfEquippedSword", WeenieType = WeenieType.MeleeWeapon };
            return new MeleeWeapon(weenie, new ObjectGuid(0xF0004260));
        }

        private static ZoneStatResolver.Resolved Slayer(double value)
        {
            var r = new ZoneStatResolver.Resolved { Tier = 15 };
            r.Floats[PropertyFloat.SlayerDamageBonus] = value;
            return r;
        }

        [TestMethod]
        public void LoweredBand_NerfAllowed_ShowsTheResolvedValue()
        {
            var w = CreateWeapon();
            w.SetProperty(PropertyFloat.SlayerDamageBonus, 2.67);   // stamped before the band came down
            var f = ZoneStatResolver.FloatsIfEquipped(w, Slayer(2.14), allowNerf: true);
            Assert.AreEqual(2.14, f[PropertyFloat.SlayerDamageBonus], 1e-9);
            Assert.AreEqual(2.67, w.GetProperty(PropertyFloat.SlayerDamageBonus).Value, 1e-9, "the item itself is never written");
        }

        [TestMethod]
        public void LoweredBand_NerfNotAllowed_KeepsTheStampedValue()
        {
            var w = CreateWeapon();
            w.SetProperty(PropertyFloat.SlayerDamageBonus, 2.67);
            var f = ZoneStatResolver.FloatsIfEquipped(w, Slayer(2.14), allowNerf: false);
            Assert.AreEqual(2.67, f[PropertyFloat.SlayerDamageBonus], 1e-9);
        }

        [TestMethod]
        public void RaisedBand_ShowsTheResolvedValueEitherWay()
        {
            var w = CreateWeapon();
            w.SetProperty(PropertyFloat.SlayerDamageBonus, 2.0);
            Assert.AreEqual(2.14, ZoneStatResolver.FloatsIfEquipped(w, Slayer(2.14), allowNerf: false)[PropertyFloat.SlayerDamageBonus], 1e-9);
            Assert.AreEqual(2.14, ZoneStatResolver.FloatsIfEquipped(w, Slayer(2.14), allowNerf: true)[PropertyFloat.SlayerDamageBonus], 1e-9);
        }

        [TestMethod]
        public void NothingStamped_ShowsTheResolvedValue()
        {
            var w = CreateWeapon();
            var f = ZoneStatResolver.FloatsIfEquipped(w, Slayer(2.14), allowNerf: false);
            Assert.AreEqual(2.14, f[PropertyFloat.SlayerDamageBonus], 1e-9);
        }

        [TestMethod]
        public void NoRecord_IsEmpty()
        {
            var w = CreateWeapon();
            Assert.AreEqual(0, ZoneStatResolver.FloatsIfEquipped(w, null).Count);
            Assert.AreEqual(0, ZoneStatResolver.FloatsIfEquipped(w, null, allowNerf: true).Count);
            Assert.AreEqual(0, ZoneStatResolver.FloatsIfEquipped(null, Slayer(2.14), allowNerf: true).Count);
        }
    }
}
