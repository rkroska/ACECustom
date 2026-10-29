using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.WorldObjects;

namespace ACE.Server.Tests
{
    /// <summary>Loot properties (2026-10-26): the tier 11+ panel numbers sent as ints / floats for loot rules. The resolve itself
    /// (ZoneStatResolver.Compute) needs the live ladder, so the gear cases hand Collect a resolved record built here.</summary>
    [TestClass]
    public class ZoneLootPropsTests
    {
        private static MeleeWeapon CreateWeapon()
        {
            var weenie = new Weenie { WeenieClassId = 424250, ClassName = "TestLootPropsSword", WeenieType = WeenieType.MeleeWeapon };
            return new MeleeWeapon(weenie, new ObjectGuid(0xF0004250));
        }

        private static Clothing CreateArmor()
        {
            var weenie = new Weenie { WeenieClassId = 424251, ClassName = "TestLootPropsHelm", WeenieType = WeenieType.Clothing };
            return new Clothing(weenie, new ObjectGuid(0xF0004251));
        }

        private static ZoneStatResolver.ResolvedLine Line(int key, int grade, int min, int max, int value)
        {
            ZoneModifiers.TryGet(key, out var def);
            return new ZoneStatResolver.ResolvedLine
            {
                Record = new ZoneStatResolver.LineRecord { Key = key, Grade = grade },
                Def = def, Min = min, Max = max, Value = value,
            };
        }

        private static int Int(Dictionary<PropertyInt, int> d, int id) => d[(PropertyInt)id];
        private static bool HasInt(Dictionary<PropertyInt, int> d, int id) => d.ContainsKey((PropertyInt)id);

        [TestMethod]
        public void IdBlock_CollidesWithNoDefinedProperty()
        {
            var enums = new[] { typeof(PropertyInt), typeof(PropertyFloat), typeof(PropertyInt64), typeof(PropertyBool),
                                typeof(PropertyString), typeof(PropertyDataId), typeof(PropertyInstanceId) };
            foreach (var t in enums)
                foreach (var v in Enum.GetValues(t))
                {
                    var n = Convert.ToInt64(v);
                    Assert.IsFalse(n >= ZoneLootProps.BlockMin && n <= ZoneLootProps.BlockMax, $"{t.Name}.{v} = {n} sits in the loot property block");
                }
        }

        [TestMethod]
        public void IdRanges_NeverOverlap()
        {
            var ids = new HashSet<int>();
            foreach (var key in ZoneModifiers.Catalog.Keys)
            {
                Assert.IsTrue(ids.Add(ZoneLootProps.GearLineValueId(key)));
                Assert.IsTrue(ids.Add(ZoneLootProps.GearLineGradeId(key)));
            }
            for (var key = -11; key >= -30; key--)
            {
                Assert.IsTrue(ids.Add(ZoneLootProps.WeaponValueId(key)));
                Assert.IsTrue(ids.Add(ZoneLootProps.WeaponGradeId(key)));
            }
            foreach (var id in new[] { ZoneLootProps.GearGradeId, ZoneLootProps.PropertiesUsedId, ZoneLootProps.PropertiesLimitId,
                                       ZoneLootProps.SlayerAllCreaturesId, ZoneLootProps.CloakProcId, ZoneLootProps.VersionId })
                Assert.IsTrue(ids.Add(id));
            Assert.IsTrue(ids.All(id => id >= ZoneLootProps.BlockMin && id <= ZoneLootProps.BlockMax));
        }

        [TestMethod]
        public void GearKey_LegacyCoreResistsReportAsTheirAlwaysRolledTwins()
        {
            Assert.AreEqual(50, ZoneLootProps.GearKey(ZoneStatResolver.CoreDamageResist));
            Assert.AreEqual(51, ZoneLootProps.GearKey(ZoneStatResolver.CoreCritDamageResist));
            Assert.AreEqual(52, ZoneLootProps.GearKey(ZoneStatResolver.CoreCritResist));
            Assert.AreEqual(53, ZoneLootProps.GearKey(ZoneStatResolver.CoreNetherResist));
            Assert.AreEqual(28, ZoneLootProps.GearKey(28));
            Assert.IsNull(ZoneLootProps.GearKey(ZoneStatResolver.WeaponBiteKey));
        }

        [TestMethod]
        public void Weapon_CardsInDisplaySpaceWithGrades()
        {
            var w = CreateWeapon();
            w.SetProperty(PropertyInt.WeaponAugScaleTier, 12);
            w.SetProperty(PropertyString.ZcModifiers, "-11:500;-16:900");
            w.SetProperty(PropertyFloat.CriticalFrequency, 0.3);
            w.SetProperty(PropertyFloat.CriticalMultiplier, 0.85);
            w.SetProperty(PropertyFloat.SlayerDamageBonus, 4.0);   // a slayer with no card: not a tier 11+ roll, not sent
            w.SetProperty(PropertyBool.SlayerAllCreatures, true);

            var ints = new Dictionary<PropertyInt, int>();
            var floats = new Dictionary<PropertyFloat, double>();
            ZoneLootProps.Collect(w, null, (2, 6), false, ints, floats);

            Assert.AreEqual(ZoneLootProps.FormatVersion, Int(ints, ZoneLootProps.VersionId));
            Assert.AreEqual(0.3, floats[(PropertyFloat)ZoneLootProps.WeaponValueId(ZoneStatResolver.WeaponBiteKey)], 1e-9);
            Assert.AreEqual(1.85, floats[(PropertyFloat)ZoneLootProps.WeaponValueId(ZoneStatResolver.WeaponCrushKey)], 1e-9, "Crushing Blow is sent as the panel shows it, stored + 1");
            Assert.AreEqual(500, Int(ints, ZoneLootProps.WeaponGradeId(ZoneStatResolver.WeaponBiteKey)));
            Assert.AreEqual(900, Int(ints, ZoneLootProps.WeaponGradeId(ZoneStatResolver.WeaponCrushKey)));
            Assert.IsFalse(floats.ContainsKey((PropertyFloat)ZoneLootProps.WeaponValueId(ZoneStatResolver.WeaponSlayerKey)));
            Assert.AreEqual(2, Int(ints, ZoneLootProps.PropertiesUsedId));
            Assert.AreEqual(6, Int(ints, ZoneLootProps.PropertiesLimitId));
            Assert.AreEqual(1, Int(ints, ZoneLootProps.SlayerAllCreaturesId));
            Assert.IsFalse(HasInt(ints, ZoneLootProps.GearGradeId), "weapons show Weapon Grade (9060), not Gear Grade");
        }

        [TestMethod]
        public void Gear_LinesGradesGearGradeAndReinforced()
        {
            var a = CreateArmor();
            a.SetProperty(PropertyInt.ZcTier, 13);
            // Damage Rating 490, legacy Crit Damage Resist 800, Cheat Death (boots special, on/off), Spell Duration (Trash)
            a.SetProperty(PropertyString.ZcModifiers, "28:490;c2:800;45:1000;32:300;19:700");
            a.SetProperty((PropertyInt)ZoneModifiers.ReinforcedRank, 2);
            a.SetProperty(PropertyString.ZcTinkerBonus, $"{(int)PropertyInt.GearMaxHealth}:5");   // e.g. Hematite on the Max Health line

            var r = new ZoneStatResolver.Resolved { Tier = 13 };
            r.Lines.Add(Line(28, 490, 14, 69, 41));
            r.Lines.Add(Line(ZoneStatResolver.CoreCritDamageResist, 800, 21, 42, 38));
            r.Lines.Add(Line(45, 1000, 1, 1, 1));
            r.Lines.Add(Line(32, 300, 1, 3, 2));
            r.Lines.Add(Line(19, 700, 14, 69, 53));

            var ints = new Dictionary<PropertyInt, int>();
            var floats = new Dictionary<PropertyFloat, double>();
            ZoneLootProps.Collect(a, r, (3, int.MaxValue), false, ints, floats);

            Assert.AreEqual(41, Int(ints, ZoneLootProps.GearLineValueId(28)));
            Assert.AreEqual(490, Int(ints, ZoneLootProps.GearLineGradeId(28)));
            Assert.AreEqual(38, Int(ints, ZoneLootProps.GearLineValueId(51)), "legacy c2 reports as Crit Damage Resist (51)");
            Assert.AreEqual(800, Int(ints, ZoneLootProps.GearLineGradeId(51)));
            Assert.AreEqual(1, Int(ints, ZoneLootProps.GearLineValueId(45)));
            Assert.IsFalse(HasInt(ints, ZoneLootProps.GearLineGradeId(45)), "an on/off special has no grade");
            Assert.AreEqual(58, Int(ints, ZoneLootProps.GearLineValueId(19)), "the tinker on the line's own prop is included, as the panel shows it");
            Assert.AreEqual(700, Int(ints, ZoneLootProps.GearLineGradeId(19)));
            Assert.AreEqual(2, Int(ints, ZoneLootProps.GearLineValueId(ZoneModifiers.ReinforcedKey)));
            Assert.IsFalse(HasInt(ints, ZoneLootProps.GearLineGradeId(ZoneModifiers.ReinforcedKey)));
            // Gear Grade leaves out Trash (32) and slot specials (45): floor((490 + 800 + 700) / 3) = 663
            Assert.AreEqual(663, Int(ints, ZoneLootProps.GearGradeId));
            Assert.AreEqual(663, ZoneStatResolver.GearGrade(a).Value.Grade, "same number the panel's Gear Grade line is built from");
            Assert.AreEqual(3, Int(ints, ZoneLootProps.PropertiesUsedId));
            Assert.IsFalse(HasInt(ints, ZoneLootProps.PropertiesLimitId), "no tier limit, no limit property");
            Assert.AreEqual(0, floats.Count);
        }

        [TestMethod]
        public void BelowTier11WithoutRecord_AddsNothing()
        {
            var a = CreateArmor();
            a.SetProperty(PropertyInt.ZcTier, 10);
            var ints = new Dictionary<PropertyInt, int>();
            var floats = new Dictionary<PropertyFloat, double>();
            ZoneLootProps.Collect(a, null, null, false, ints, floats);
            Assert.AreEqual(0, ints.Count);
            Assert.AreEqual(0, floats.Count);
        }

        [TestMethod]
        public void LegacyGearWithoutRecord_SendsOnlyTheVersion()
        {
            var a = CreateArmor();
            a.SetProperty(PropertyInt.ZcTier, 12);
            var ints = new Dictionary<PropertyInt, int>();
            var floats = new Dictionary<PropertyFloat, double>();
            ZoneLootProps.Collect(a, null, (2, 5), true, ints, floats);
            Assert.AreEqual(1, ints.Count, "a text-only piece: no lines to send, and the panel shows no Properties count for it");
            Assert.IsTrue(HasInt(ints, ZoneLootProps.VersionId));
        }

        [TestMethod]
        public void RecordlessPieceWithOnlyReinforced_SendsRankAndCount()
        {
            var a = CreateArmor();
            a.SetProperty(PropertyInt.ZcTier, 12);
            a.SetProperty((PropertyInt)ZoneModifiers.ReinforcedRank, 3);
            var ints = new Dictionary<PropertyInt, int>();
            var floats = new Dictionary<PropertyFloat, double>();
            ZoneLootProps.Collect(a, null, (1, 5), false, ints, floats);
            Assert.AreEqual(3, Int(ints, ZoneLootProps.GearLineValueId(ZoneModifiers.ReinforcedKey)));
            Assert.AreEqual(1, Int(ints, ZoneLootProps.PropertiesUsedId), "the panel counts Reinforced, so it shows the count here");
            Assert.AreEqual(5, Int(ints, ZoneLootProps.PropertiesLimitId));
            Assert.IsFalse(HasInt(ints, ZoneLootProps.GearGradeId));
        }

        [TestMethod]
        public void PctHpDamageSpecial_IsGradedAndSentInTenths()
        {
            var a = CreateArmor();
            a.SetProperty(PropertyInt.ZcTier, 11);
            a.SetProperty(PropertyString.ZcModifiers, "44:500");
            var r = new ZoneStatResolver.Resolved { Tier = 11 };
            r.Lines.Add(Line(44, 500, 40, 60, 50));
            var ints = new Dictionary<PropertyInt, int>();
            ZoneLootProps.Collect(a, r, (0, 3), false, ints, new Dictionary<PropertyFloat, double>());
            Assert.AreEqual(50, Int(ints, ZoneLootProps.GearLineValueId(44)), "5.0 pct of max HP, stored and sent in tenths");
            Assert.AreEqual(500, Int(ints, ZoneLootProps.GearLineGradeId(44)));
        }

        [TestMethod]
        public void Weapon_RendCardsAsPrintedAndProcDamageStored()
        {
            var w = CreateWeapon();
            w.SetProperty(PropertyInt.WeaponAugScaleTier, 14);
            w.SetProperty(PropertyString.ZcModifiers, "-12:1000;-13:400;-15:200;-17:600");
            w.SetProperty((PropertyFloat)ZoneLootMutator.ArmorRendOverridePropId, 1.2);   // over-range: the panel clamps it to 100%
            w.SetProperty((PropertyFloat)ZoneLootMutator.RendingModOverridePropId, 2.5);
            w.SetProperty(PropertyFloat.IgnoreShield, 0.4);
            w.SetProperty((PropertyFloat)ZoneLootMutator.ProcArcDamagePropId, 900.0);
            var ints = new Dictionary<PropertyInt, int>();
            var floats = new Dictionary<PropertyFloat, double>();
            ZoneLootProps.Collect(w, null, (4, 7), false, ints, floats);
            Assert.AreEqual(1.0, floats[(PropertyFloat)ZoneLootProps.WeaponValueId(ZoneStatResolver.WeaponArmorRendKey)], 1e-9);
            Assert.AreEqual(2.5, floats[(PropertyFloat)ZoneLootProps.WeaponValueId(ZoneStatResolver.WeaponRendPowerKey)], 1e-9);
            Assert.AreEqual(0.4, floats[(PropertyFloat)ZoneLootProps.WeaponValueId(ZoneStatResolver.WeaponShieldCleaveKey)], 1e-9);
            Assert.AreEqual(900.0, floats[(PropertyFloat)ZoneLootProps.WeaponValueId(ZoneStatResolver.WeaponProcArcDamageKey)], 1e-9);
            Assert.AreEqual(1000, Int(ints, ZoneLootProps.WeaponGradeId(ZoneStatResolver.WeaponArmorRendKey)));
            Assert.AreEqual(600, Int(ints, ZoneLootProps.WeaponGradeId(ZoneStatResolver.WeaponProcArcDamageKey)));
        }
    }
}
