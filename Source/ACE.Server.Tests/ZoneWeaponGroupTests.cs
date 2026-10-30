using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using ACE.Server.Factories;
using ACE.Server.Factories.Tables.Wcids;
using ACE.Server.Managers.ZoneScaling;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using DamageType = ACE.Entity.Enum.DamageType;
using Variant = ACE.Server.Factories.LootGenerationFactory.ZoneSetWeaponVariant;
using WeaponType = ACE.Entity.Enum.WeaponType;
using WeenieClassName = ACE.Server.Factories.Enum.WeenieClassName;
using Weights = ACE.Server.Factories.LootGenerationFactory.WeaponGroupWeights;

namespace ACE.Server.Tests
{
    /// <summary>
    /// T11+ weapon groups (2026-10-30): the budget sampler draws each weapon pick by the loot_weight_melee_1h / _melee_2h /
    /// _missile / _war / _void weights, then a family inside the group. Shares, off switches, bad values, the melee
    /// free-lists, the variant routing, the filtered caster roll and the stat registry.
    /// </summary>
    [TestClass]
    public class ZoneWeaponGroupTests
    {
        private static readonly int MissileFamily = LootGenerationFactory.ZoneSetWeaponPickFor("missile").Value.Family;
        private static readonly int CasterFamily = LootGenerationFactory.ZoneSetWeaponPickFor("caster").Value.Family;

        /// <summary>Weapons only: every budget item is a weapon pick.</summary>
        private static List<LootGenerationFactory.ZoneSetWeaponPick> Picks(Weights groups, int budget, int corpses)
        {
            var all = new List<LootGenerationFactory.ZoneSetWeaponPick>();
            var slots = LootGenerationFactory.ZoneLootSetCounts.TierDefault(11);
            for (var i = 0; i < corpses; i++)
                all.AddRange(LootGenerationFactory.RollBudgetedCounts(slots, budget, 1.0, 0, 0, 0, 0, groups).WeaponPicks);
            return all;
        }

        private static string Group(LootGenerationFactory.ZoneSetWeaponPick p) => p.Variant switch
        {
            Variant.OneHanded => "1h",
            Variant.TwoHanded => "2h",
            Variant.War => "war",
            Variant.Void => "void",
            _ => p.Family == MissileFamily ? "missile" : "other",
        };

        private static void AssertShares(List<LootGenerationFactory.ZoneSetWeaponPick> picks, Dictionary<string, double> expected)
        {
            var counts = picks.GroupBy(Group).ToDictionary(g => g.Key, g => g.Count());
            Assert.IsFalse(counts.ContainsKey("other"), "every pick belongs to one of the five groups");
            foreach (var (group, share) in expected)
            {
                var got = counts.TryGetValue(group, out var c) ? (double)c / picks.Count : 0.0;
                Assert.AreEqual(share, got, share == 0 ? 0 : 0.006, $"{group} share");
            }
        }

        [TestMethod]
        public void Defaults_FourEqualGroups_VoidIsWarOverSeven()
        {
            Assert.AreEqual(1.0, Weights.Default.Melee1H);
            Assert.AreEqual(1.0, Weights.Default.Melee2H);
            Assert.AreEqual(1.0, Weights.Default.Missile);
            Assert.AreEqual(1.0, Weights.Default.War);
            Assert.AreEqual(1.0 / 7.0, Weights.Default.Void, 1e-12);

            AssertShares(Picks(Weights.Default, 10, 20_000), new Dictionary<string, double>
            {
                ["1h"] = 7.0 / 29, ["2h"] = 7.0 / 29, ["missile"] = 7.0 / 29, ["war"] = 7.0 / 29, ["void"] = 1.0 / 29,
            });
        }

        [TestMethod]
        public void NonDefaultWeights_AreNormalized()
        {
            AssertShares(Picks(new Weights(1, 5, 1, 1, 2), 10, 20_000), new Dictionary<string, double>
            {
                ["1h"] = 0.1, ["2h"] = 0.5, ["missile"] = 0.1, ["war"] = 0.1, ["void"] = 0.2,
            });
        }

        [TestMethod]
        public void GroupAtZero_NeverDrops()
        {
            var picks = Picks(new Weights(1, 0, 1, 1, 0), 10, 2_000);
            Assert.IsFalse(picks.Any(p => p.Variant == Variant.TwoHanded || p.Variant == Variant.Void));
            Assert.IsTrue(picks.Count == 20_000);
        }

        [TestMethod]
        public void AllGroupsAtZero_NoWeapons_BudgetGoesElsewhere()
        {
            var slots = LootGenerationFactory.ZoneLootSetCounts.TierDefault(11);
            var counts = LootGenerationFactory.RollBudgetedCounts(slots, 10, 1.0, 1.0, 0, 0, 0, new Weights(0, 0, 0, 0, 0));
            Assert.AreEqual(0, counts.WeaponPicks.Count);
            var armor = counts.Helm + counts.Chest + counts.Shoulder + counts.Bracer + counts.Glove + counts.Girth
                + counts.UpperLeg + counts.LowerLeg + counts.Boot + counts.Shield;
            Assert.AreEqual(10, armor, "the weapon picks go to armor instead of being lost");
        }

        [TestMethod]
        public void BadWeights_CountAsZero()
        {
            var w = new Weights(double.NaN, -3, double.PositiveInfinity, 1, double.NegativeInfinity);
            Assert.AreEqual(0, w.Melee1H);
            Assert.AreEqual(0, w.Melee2H);
            Assert.AreEqual(0, w.Missile);
            Assert.AreEqual(1, w.War);
            Assert.AreEqual(0, w.Void);
            Assert.IsTrue(Picks(w, 10, 500).All(p => p.Variant == Variant.War), "only the one valid group drops");
        }

        [TestMethod]
        public void OneHanded_NoRepeatUntilAllSevenDrop_ThenRefills()
        {
            var slots = LootGenerationFactory.ZoneLootSetCounts.TierDefault(11);
            var only1H = new Weights(1, 0, 0, 0, 0);
            for (var i = 0; i < 200; i++)
            {
                var seven = LootGenerationFactory.RollBudgetedCounts(slots, 7, 1.0, 0, 0, 0, 0, only1H).WeaponPicks;
                Assert.AreEqual(7, seven.Select(p => p.Family).Distinct().Count());
                Assert.IsFalse(seven.Any(p => p.Family == MissileFamily || p.Family == CasterFamily));

                var fourteen = LootGenerationFactory.RollBudgetedCounts(slots, 14, 1.0, 0, 0, 0, 0, only1H).WeaponPicks;
                Assert.AreEqual(14, fourteen.Count, "the group keeps its share past seven picks");
                Assert.IsTrue(fourteen.GroupBy(p => p.Family).All(g => g.Count() == 2));
            }
        }

        [TestMethod]
        public void TwoHanded_UsesTheFourTwoHandedFamilies()
        {
            var slots = LootGenerationFactory.ZoneLootSetCounts.TierDefault(11);
            var only2H = new Weights(0, 1, 0, 0, 0);
            var families = new HashSet<int>();
            for (var i = 0; i < 200; i++)
            {
                var four = LootGenerationFactory.RollBudgetedCounts(slots, 4, 1.0, 0, 0, 0, 0, only2H).WeaponPicks;
                Assert.AreEqual(4, four.Select(p => p.Family).Distinct().Count());
                families.UnionWith(four.Select(p => p.Family));
            }
            CollectionAssert.AreEquivalent(new[] { 0, 2, 3, 5 }, families.ToArray(), "Axe, Mace, Spear, Sword");
        }

        [TestMethod]
        public void MeleeSkill_FollowsTheVariant()
        {
            for (var i = 0; i < 2_000; i++)
            {
                var oneH = LootGenerationFactory.RollZoneSetMeleeSkill(Variant.OneHanded, canTwoHand: true);
                Assert.IsTrue(oneH >= 1 && oneH <= 3, "one-handed never rolls Two-handed");
                Assert.AreEqual(4, LootGenerationFactory.RollZoneSetMeleeSkill(Variant.TwoHanded, canTwoHand: true));
                var noLine = LootGenerationFactory.RollZoneSetMeleeSkill(Variant.TwoHanded, canTwoHand: false);
                Assert.IsTrue(noLine >= 1 && noLine <= 3, "a family without a 2H line falls back to one-handed");
            }

            var any = Enumerable.Range(0, 4_000).Select(_ => LootGenerationFactory.RollZoneSetMeleeSkill(Variant.Any, canTwoHand: true)).ToList();
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4 }, any.Distinct().ToArray(), "Any keeps the pre-group roll");
        }

        [TestMethod]
        public void AscendantMapping_KeepsTheKind()
        {
            Assert.AreEqual(Variant.TwoHanded, LootGenerationFactory.MeleeVariantLike(WeaponType.TwoHanded));
            Assert.AreEqual(Variant.OneHanded, LootGenerationFactory.MeleeVariantLike(WeaponType.Sword));
            Assert.AreEqual(Variant.OneHanded, LootGenerationFactory.MeleeVariantLike(WeaponType.Unarmed));
            Assert.AreEqual(Variant.Void, LootGenerationFactory.CasterVariantLike(DamageType.Nether));
            Assert.AreEqual(Variant.Void, LootGenerationFactory.CasterVariantLike(DamageType.Nether | DamageType.Fire));
            Assert.AreEqual(Variant.War, LootGenerationFactory.CasterVariantLike(DamageType.Fire));
            Assert.AreEqual(Variant.War, LootGenerationFactory.CasterVariantLike(DamageType.Undef));
        }

        [TestMethod]
        public void CasterFilter_SplitsNetherFromWar()
        {
            var nether = new[] { WeenieClassName.ace43381_nethersceptre, WeenieClassName.ace43382_netherbaton, WeenieClassName.ace43383_netherstaff };
            var seenVoid = new HashSet<WeenieClassName>();
            for (var i = 0; i < 3_000; i++)
            {
                var v = CasterWcids.RollFiltered(10, nether: true);
                Assert.IsTrue(nether.Contains(v));
                seenVoid.Add(v);
                var w = CasterWcids.RollFiltered(10, nether: false);
                Assert.AreNotEqual(WeenieClassName.undef, w);
                Assert.IsFalse(nether.Contains(w));
            }
            Assert.AreEqual(3, seenVoid.Count, "all three nether casters roll");
            Assert.AreEqual(WeenieClassName.undef, CasterWcids.RollFiltered(1, nether: true), "T1-T2 have no nether caster");
            Assert.IsTrue(nether.Contains(CasterWcids.RollFiltered(25, nether: true)), "tiers above 10 clamp to the T10 row, which has nether");
        }

        [TestMethod]
        public void LegacyMode_StillNineFamilies()
        {
            var familyEnum = typeof(LootGenerationFactory).GetNestedType("ZoneSetFamily", BindingFlags.NonPublic);
            Assert.IsNotNull(familyEnum);
            Assert.AreEqual(9, Enum.GetValues(familyEnum).Length, "legacy mode makes one weapon per family - do not extend the enum");
        }

        [TestMethod]
        public void Stats_AreRegistered_AtTheTailOfAll()
        {
            var tail = ZoneStat.All.Skip(ZoneStat.All.Length - 5).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                "loot_weight_melee_1h", "loot_weight_melee_2h", "loot_weight_missile", "loot_weight_war", "loot_weight_void",
            }, tail);
            Assert.AreEqual(ZoneStat.CharmLevelCap, ZoneStat.All[ZoneStat.All.Length - 6], "appended after the previous tail");
        }

        [TestMethod]
        public void CreatureDeath_PassesTheGroupWeights()
        {
            var src = Source("ACE.Server", "WorldObjects", "Creature_Death.cs");
            foreach (var stat in new[] { "LootWeightMelee1H", "LootWeightMelee2H", "LootWeightMissile", "LootWeightWar", "LootWeightVoid" })
                StringAssert.Contains(src, "ZoneStat." + stat);
            StringAssert.Contains(src, "LootGenerationFactory.WeaponGroupWeights.Default");
        }

        private static string Source(params string[] parts)
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "ACE.sln")))
                dir = Path.GetDirectoryName(dir);
            if (dir == null)
                Assert.Inconclusive("source tree not found next to the test binaries");
            return File.ReadAllText(Path.Combine(dir, Path.Combine(parts)));
        }
    }
}
