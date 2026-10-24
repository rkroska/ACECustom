using System;
using System.Collections.Generic;
using System.IO;

using ACE.Server.Factories;
using ACE.Server.Managers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Mundane = ACE.Server.Factories.LootGenerationFactory.ZoneKillMundane;

namespace ACE.Server.Tests
{
    /// <summary>
    /// T11+ zone-kill extras (LootGenerationFactory_ZoneKillExtras.cs): the pure roll - essence level split, mundane
    /// weights, chances - plus the corpse-cap room and the shape of the hook (after the gear set, no lens).
    /// </summary>
    [TestClass]
    public class ZoneKillExtrasTests
    {
        private static Func<double> Rolls(params double[] values)
        {
            var q = new Queue<double>(values);
            return () => q.Dequeue();
        }

        private static LootGenerationFactory.ZoneKillExtrasPlan Plan(Func<double> rng)
            => LootGenerationFactory.PlanZoneKillExtras(rng, 0.01, 0.71, 0.25, 0.25, 0.25, 0.25, 0.25);

        [TestMethod]
        public void Defaults_MatchTheOwnerDecision()
        {
            Assert.AreEqual(0.01, ServerConfig.zc_kill_extra_essence_chance.Default, 1e-9);
            Assert.AreEqual(0.71, ServerConfig.zc_kill_extra_essence_250_share.Default, 1e-9);
            Assert.AreEqual(0.25, ServerConfig.zc_kill_extra_mundane_chance.Default, 1e-9);
            Assert.AreEqual(0.25, ServerConfig.zc_kill_extra_weight_pyreal.Default, 1e-9);
            Assert.AreEqual(0.25, ServerConfig.zc_kill_extra_weight_manastone.Default, 1e-9);
            Assert.AreEqual(0.25, ServerConfig.zc_kill_extra_weight_food.Default, 1e-9);
            Assert.AreEqual(0.25, ServerConfig.zc_kill_extra_weight_lockpick.Default, 1e-9);
            Assert.IsTrue(ServerConfig.zc_kill_extra_aetheria_enabled.Default);
        }

        [TestMethod]
        public void Essence_HitsBelowTheChance_LevelSplitAt071()
        {
            // essence hit, level roll 0.70 -> 250; mundane miss
            var p = Plan(Rolls(0.009, 0.70, 0.99));
            Assert.AreEqual(250, p.EssenceLevel);
            Assert.IsNull(p.Mundane);

            // essence hit, level roll 0.71 -> 300 (0.71 is not below the share)
            p = Plan(Rolls(0.0, 0.71, 0.99));
            Assert.AreEqual(300, p.EssenceLevel);

            // essence miss at exactly the chance: no level draw is taken, the next value goes to the mundane roll
            p = Plan(Rolls(0.01, 0.10, 0.0));
            Assert.IsNull(p.EssenceLevel);
            Assert.AreEqual(Mundane.Pyreal, p.Mundane);
        }

        [TestMethod]
        public void Essence_LevelIsOnly250Or300()
        {
            var rng = new Random(1234);
            int n250 = 0, n300 = 0;
            for (var i = 0; i < 200000; i++)
            {
                var p = LootGenerationFactory.PlanZoneKillExtras(rng.NextDouble, 1.0, 0.71, 0, 1, 1, 1, 1);
                if (p.EssenceLevel == 250) n250++;
                else if (p.EssenceLevel == 300) n300++;
                else Assert.Fail($"unexpected level {p.EssenceLevel}");
            }
            Assert.AreEqual(0.71, n250 / 200000.0, 0.005);
            Assert.AreEqual(0.29, n300 / 200000.0, 0.005);
        }

        [TestMethod]
        public void Rates_OverManyKills_MatchTheChances()
        {
            var rng = new Random(42);
            const int kills = 400000;
            int essences = 0, mundanes = 0;
            var byKind = new Dictionary<Mundane, int>();
            for (var i = 0; i < kills; i++)
            {
                var p = Plan(rng.NextDouble);
                if (p.EssenceLevel.HasValue) essences++;
                if (p.Mundane.HasValue)
                {
                    mundanes++;
                    byKind[p.Mundane.Value] = byKind.GetValueOrDefault(p.Mundane.Value) + 1;
                }
            }
            Assert.AreEqual(0.01, essences / (double)kills, 0.001);
            Assert.AreEqual(0.25, mundanes / (double)kills, 0.005);
            foreach (Mundane kind in Enum.GetValues(typeof(Mundane)))
                Assert.AreEqual(0.25, byKind.GetValueOrDefault(kind) / (double)mundanes, 0.01, kind.ToString());
        }

        [TestMethod]
        public void MundanePick_FollowsTheWeights()
        {
            Assert.AreEqual(Mundane.Pyreal, LootGenerationFactory.PickZoneKillMundane(0.00, 1, 1, 1, 1));
            Assert.AreEqual(Mundane.ManaStone, LootGenerationFactory.PickZoneKillMundane(0.25, 1, 1, 1, 1));
            Assert.AreEqual(Mundane.Food, LootGenerationFactory.PickZoneKillMundane(0.50, 1, 1, 1, 1));
            Assert.AreEqual(Mundane.Lockpick, LootGenerationFactory.PickZoneKillMundane(0.99, 1, 1, 1, 1));

            // a zero (or negative) weight is never picked, whatever the roll
            for (var r = 0.0; r < 1.0; r += 0.01)
            {
                Assert.AreNotEqual(Mundane.ManaStone, LootGenerationFactory.PickZoneKillMundane(r, 1, 0, 1, 1));
                Assert.AreNotEqual(Mundane.Pyreal, LootGenerationFactory.PickZoneKillMundane(r, -5, 1, 1, 1));
            }
            Assert.AreEqual(Mundane.Lockpick, LootGenerationFactory.PickZoneKillMundane(0.5, 0, 0, 0, 3));
            // top-edge roll lands on the last kind that has weight
            Assert.AreEqual(Mundane.Food, LootGenerationFactory.PickZoneKillMundane(1.0, 1, 1, 1, 0));
            // all off = nothing
            Assert.IsNull(LootGenerationFactory.PickZoneKillMundane(0.3, 0, 0, 0, 0));
        }

        [TestMethod]
        public void Chances_AreClamped_AndZeroMeansOff()
        {
            for (var i = 0; i < 1000; i++)
            {
                var p = LootGenerationFactory.PlanZoneKillExtras(() => 0.0, 0, 0.71, 0, 1, 1, 1, 1);
                Assert.IsNull(p.EssenceLevel);
                Assert.IsNull(p.Mundane);
            }
            // above 1 = every kill; NaN = off
            var hit = LootGenerationFactory.PlanZoneKillExtras(Rolls(0.999, 0.5, 0.999, 0.1), 5, 0.71, 2, 1, 1, 1, 1);
            Assert.AreEqual(250, hit.EssenceLevel);
            Assert.AreEqual(Mundane.Pyreal, hit.Mundane);
            var nan = LootGenerationFactory.PlanZoneKillExtras(() => 0.0, double.NaN, 0.71, double.NaN, 1, 1, 1, 1);
            Assert.IsNull(nan.EssenceLevel);
            Assert.IsNull(nan.Mundane);
        }

        [TestMethod]
        public void CorpseRoom_RespectsTheCap()
        {
            Assert.AreEqual(3, LootGenerationFactory.ZoneKillExtrasRoom(97, 100));
            Assert.AreEqual(0, LootGenerationFactory.ZoneKillExtrasRoom(100, 100));
            Assert.AreEqual(0, LootGenerationFactory.ZoneKillExtrasRoom(115, 100));
            Assert.AreEqual(100, LootGenerationFactory.ZoneKillExtrasRoom(-1, 100));
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

        [TestMethod]
        public void Extras_NeverRollASiphonLens()
        {
            var src = Source("ACE.Server", "Factories", "LootGenerationFactory_ZoneKillExtras.cs");
            var body = src.Substring(src.IndexOf("public static List<WorldObject> CreateZoneKillExtras", StringComparison.Ordinal));
            StringAssert.Contains(body, "TryRollAetheria(profile)");
            Assert.IsFalse(body.Contains("TryRollSiphonLens"), "extras must not roll a siphon lens");
            Assert.IsFalse(body.Contains("TryRollMundaneAddon"), "TryRollMundaneAddon rolls a lens first");
        }

        [TestMethod]
        public void Extras_AreAddedAfterTheGearSet_OutsideTheBudget()
        {
            var src = Source("ACE.Server", "WorldObjects", "Creature_Death.cs");
            var budget = src.IndexOf("LootGenerationFactory.RollBudgetedCounts(", StringComparison.Ordinal);
            var gearSet = src.IndexOf("items.AddRange(LootGenerationFactory.CreateZoneLootSet(effectiveTreasure, slotCounts))", StringComparison.Ordinal);
            var sweep = src.IndexOf("foreach (WorldObject wo in items)", StringComparison.Ordinal);
            var hook = src.IndexOf("AddZoneKillExtras(effectiveTreasure, corpse, droppedItems)", StringComparison.Ordinal);
            Assert.IsTrue(budget > 0 && gearSet > 0 && sweep > 0 && hook > 0);
            Assert.IsTrue(hook > budget && hook > gearSet && hook > sweep, "extras must be rolled after the budget, the gear set and its sweep");

            // the extras never join the gear list the budget and the sweep work on
            var helper = src.Substring(src.IndexOf("private void AddZoneKillExtras", StringComparison.Ordinal));
            helper = helper.Substring(0, helper.IndexOf("private static ACE.Database.Models.World.TreasureDeath CloneTreasureDeath", StringComparison.Ordinal));
            Assert.IsFalse(helper.Contains("items."), "extras must not be added to the gear list");
            StringAssert.Contains(helper, "ZoneCorpseItemCap");
        }
    }
}
