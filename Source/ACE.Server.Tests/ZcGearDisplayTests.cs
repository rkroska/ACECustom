using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Network.Structure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// The T11+ gear-line panel display (ZcGearDisplay): which entries are sent for which worn lines, and the exact bytes the
    /// client receives. No world, no database - the reads are stubbed.
    /// </summary>
    [TestClass]
    public class ZcGearDisplayTests
    {
        private static List<ZcGearDisplay.Entry> Compute(Dictionary<int, int> bonus, Dictionary<int, int> max = null, IEnumerable<int> skills = null)
            => ZcGearDisplay.Compute(p => bonus.TryGetValue(p, out var v) ? v : 0, p => max != null && max.TryGetValue(p, out var v) ? v : 0, skills);

        private static Dictionary<int, int> AllAttributes(int value)
            => ZcGearDisplay.Attributes.ToDictionary(a => ZoneModifiers.AttrBonusBase + (int)a, _ => value);

        [TestMethod]
        public void NoGear_SendsNothing()
        {
            Assert.AreEqual(0, Compute(new Dictionary<int, int>()).Count);
        }

        [TestMethod]
        public void AllAttributes_OneAdditiveEntryPerAttribute_CarryingTheTotal()
        {
            // two pieces of +70 and +10 arrive already summed (GetZoneModifierBonus) - the entry carries the TOTAL, never a delta
            var entries = Compute(AllAttributes(80));

            Assert.AreEqual(6, entries.Count);
            foreach (var attr in ZcGearDisplay.Attributes)
            {
                var e = entries.Single(x => x.StatModKey == (uint)attr);
                Assert.AreEqual(ZcGearDisplay.AttributeAdditive, e.StatModType);
                Assert.AreEqual(80f, e.StatModValue);
                Assert.AreEqual((ushort)(ZcGearDisplay.AttributeLayerBase + (int)attr), e.Layer);
            }
        }

        [TestMethod]
        public void Upgrade_SameLayerNewValue_SoTheClientReplacesNotAdds()
        {
            var at70 = Compute(AllAttributes(70)).ToDictionary(e => e.Layer);
            var at80 = Compute(AllAttributes(80)).ToDictionary(e => e.Layer);

            CollectionAssert.AreEquivalent(at70.Keys.ToList(), at80.Keys.ToList());
            foreach (var layer in at70.Keys)
            {
                Assert.AreEqual(70f, at70[layer].StatModValue);
                Assert.AreEqual(80f, at80[layer].StatModValue);
            }
        }

        [TestMethod]
        public void Fortify_MultipliesAllThreeVitals_PctAddsToHealthOnly()
        {
            var entries = Compute(
                new Dictionary<int, int> { [ZoneModifiers.PctMaxHealthPct] = 9 },
                new Dictionary<int, int> { [ZoneModifiers.FortifyVitalsPct] = 25 });

            Assert.AreEqual(3, entries.Count);
            Assert.IsTrue(entries.All(e => e.StatModType == ZcGearDisplay.VitalMultiplier));
            Assert.AreEqual(1.34f, entries.Single(e => e.StatModKey == (uint)PropertyAttribute2nd.MaxHealth).StatModValue, 1e-6f);
            Assert.AreEqual(1.25f, entries.Single(e => e.StatModKey == (uint)PropertyAttribute2nd.MaxStamina).StatModValue, 1e-6f);
            Assert.AreEqual(1.25f, entries.Single(e => e.StatModKey == (uint)PropertyAttribute2nd.MaxMana).StatModValue, 1e-6f);
        }

        [TestMethod]
        public void PctMaxHealthAlone_OnlyHealth()
        {
            var entries = Compute(new Dictionary<int, int> { [ZoneModifiers.PctMaxHealthPct] = 3 });

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual((uint)PropertyAttribute2nd.MaxHealth, entries[0].StatModKey);
            Assert.AreEqual(1.03f, entries[0].StatModValue, 1e-6f);
        }

        [TestMethod]
        public void VitalMultiplier_MatchesTheServerFormula()
        {
            // CreatureVital.GetMaxValue: fTotal * (1.0 + pts / 100.0), pts = Fortify (max-wins) + Pct (health only)
            foreach (var (fortify, pct) in new[] { (5, 0), (25, 54), (0, 1), (17, 3) })
            {
                var e = Compute(new Dictionary<int, int> { [ZoneModifiers.PctMaxHealthPct] = pct },
                                new Dictionary<int, int> { [ZoneModifiers.FortifyVitalsPct] = fortify })
                        .Single(x => x.StatModKey == (uint)PropertyAttribute2nd.MaxHealth);
                Assert.AreEqual((float)(1.0 + (fortify + pct) / 100.0), e.StatModValue);
            }
        }

        [TestMethod]
        public void ZoneLockOrRemoved_GivesNoEntries_SoTheSyncRemovesThem()
        {
            // the lock makes GetZoneModifierBonus / Max return 0: a neutral state sends nothing, and the sync dispels what it sent
            Assert.AreEqual(0, Compute(AllAttributes(0), new Dictionary<int, int> { [ZoneModifiers.FortifyVitalsPct] = 0 }).Count);
        }

        [TestMethod]
        public void SkillLines_PositiveOnly_KeyIsTheSkill()
        {
            var war = ZoneModifiers.SkillBonusBase + (int)Skill.WarMagic;
            var melee = ZoneModifiers.SkillBonusBase + (int)Skill.HeavyWeapons;
            var entries = Compute(new Dictionary<int, int> { [war] = 12, [melee] = -4 }, skills: new[] { war, melee });

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(ZcGearDisplay.SkillAdditive, entries[0].StatModType);
            Assert.AreEqual((uint)Skill.WarMagic, entries[0].StatModKey);
            Assert.AreEqual(12f, entries[0].StatModValue);
        }

        [TestMethod]
        public void LayersAndCategories_UniqueAndOutsideRealCategories()
        {
            var skills = Enum.GetValues(typeof(Skill)).Cast<int>().Where(s => s > 0).Select(s => ZoneModifiers.SkillBonusBase + s).Where(p => p <= ZoneModifiers.PropMax).ToList();
            var bonus = AllAttributes(1);
            bonus[ZoneModifiers.PctMaxHealthPct] = 1;
            foreach (var p in skills)
                bonus[p] = 1;
            var entries = Compute(bonus, new Dictionary<int, int> { [ZoneModifiers.FortifyVitalsPct] = 1 }, skills);

            Assert.AreEqual(entries.Count, entries.Select(e => e.Layer).Distinct().Count(), "layers must be unique: one entry per (spell, layer)");
            Assert.AreEqual(entries.Count, entries.Select(e => e.Category).Distinct().Count(), "categories must be unique: entries must never compete with each other");

            var lastRealCategory = Enum.GetValues(typeof(SpellCategory)).Cast<SpellCategory>().Max(c => (uint)c);
            foreach (var e in entries)
            {
                Assert.IsTrue(e.Category > lastRealCategory, $"category {e.Category} collides with a real spell category");
                Assert.IsTrue(e.Category < 0x8000, $"category {e.Category} reaches the server's cooldown category");
            }
        }

        [TestMethod]
        public void SpellId_AboveRetailAndClearOfKnownUse()
        {
            // read through a method so the analyzer does not fold the constant (MSTEST0032)
            static int Id() => ZcGearDisplay.SpellId;

            Assert.IsTrue(Id() > 6340, "must be above the last retail spell");
            Assert.AreNotEqual(8185, Id(), "8185 is referenced by world content");
            Assert.IsFalse(Enum.IsDefined(typeof(SpellId), (uint)Id()), "a named SpellId is a real spell");
        }

        /// <summary>The client's own gate (CEnchantmentRegistry::UpdateEnchantment, decompiled): v = t ^ ((t ^ (t >> 9)) >> 1);
        /// the entry is dropped unless bit 14 of v is set - i.e. exactly one of Multiplicative / Additive on a non-cooldown.</summary>
        private static bool ClientAccepts(EnchantmentTypeFlags type)
        {
            var t = (uint)type;
            var v = t ^ ((t ^ (t >> 9)) >> 1);
            return (v & 0x4000) != 0;
        }

        [TestMethod]
        public void ClientGate_AcceptsEveryShapeWeSend_AndTheGateIsReadRight()
        {
            Assert.IsTrue(ClientAccepts(ZcGearDisplay.AttributeAdditive));
            Assert.IsTrue(ClientAccepts(ZcGearDisplay.SkillAdditive));
            Assert.IsTrue(ClientAccepts(ZcGearDisplay.VitalMultiplier));

            // sanity of the re-implementation against retail shapes: Legendary Strength 0x9001, the max-vital curses 0x5002
            Assert.IsTrue(ClientAccepts((EnchantmentTypeFlags)0x9001));
            Assert.IsTrue(ClientAccepts((EnchantmentTypeFlags)0x5002));
            Assert.IsFalse(ClientAccepts(EnchantmentTypeFlags.SingleStat | EnchantmentTypeFlags.Attribute), "neither add nor multiply");
            Assert.IsFalse(ClientAccepts(EnchantmentTypeFlags.Additive | EnchantmentTypeFlags.Multiplicative | EnchantmentTypeFlags.Attribute), "both");
        }

        [TestMethod]
        public void Packet_IsTheRetailEnchantmentLayout_ItemStyle()
        {
            var entry = new ZcGearDisplay.Entry(2, ZcGearDisplay.AttributeAdditive, (uint)PropertyAttribute.Endurance, 69);
            var enchantment = ZcGearDisplay.ToEnchantment(entry, 0x50000001);

            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
                w.Write(enchantment);

            ms.Position = 0;
            using var r = new BinaryReader(ms);
            Assert.AreEqual(ZcGearDisplay.SpellId, r.ReadUInt16(), "spell id");
            Assert.AreEqual((ushort)2, r.ReadUInt16(), "layer");
            Assert.AreEqual(entry.Category, r.ReadUInt16(), "category");
            Assert.AreEqual((ushort)1, r.ReadUInt16(), "has spell set id");
            Assert.AreEqual(1u, r.ReadUInt32(), "power level");
            Assert.AreEqual(0.0, r.ReadDouble(), "start time");
            Assert.AreEqual(-1.0, r.ReadDouble(), "duration -1: item-style, survives the client's purges");
            Assert.AreEqual(0x50000001u, r.ReadUInt32(), "caster");
            Assert.AreEqual(0f, r.ReadSingle(), "degrade modifier");
            Assert.AreEqual(0f, r.ReadSingle(), "degrade limit");
            Assert.AreEqual(0.0, r.ReadDouble(), "last time degraded");
            Assert.AreEqual(0x9001u, r.ReadUInt32(), "stat mod type");
            Assert.AreEqual((uint)PropertyAttribute.Endurance, r.ReadUInt32(), "stat mod key");
            Assert.AreEqual(69f, r.ReadSingle(), "stat mod value");
            Assert.AreEqual(0u, r.ReadUInt32(), "spell set id");
            Assert.AreEqual(ms.Length, ms.Position, "nothing extra");
        }
    }
}
