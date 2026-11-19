using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Server.Managers.ZoneControl;
using ACE.Server.Managers.ZoneScaling;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>The change preview's per-zone comparison (ZoneControlManager.CompareZone): which monsters a Default or zone edit reaches.</summary>
    [TestClass]
    public class ZoneStatPreviewTests
    {
        private const string Stat = "max_health";

        private static ZoneVariantProfile Row(params (string Key, double Value)[] stats)
        {
            var p = new ZoneVariantProfile();
            foreach (var (k, v) in stats)
                p.Stats[k] = new StatCurve { Base = v };
            return p;
        }

        private static Func<int, ZoneVariantProfile> Defaults(params (int Variation, ZoneVariantProfile Row)[] rows)
        {
            var map = rows.ToDictionary(r => r.Variation, r => r.Row);
            return v => map.TryGetValue(v, out var p) ? p : null;
        }

        private static ZoneStatImpactZone Compare(int variation, Func<int, ZoneVariantProfile> before, Func<int, ZoneVariantProfile> after,
            ZoneVariantProfile minion = null, params ZoneControlManager.ImpactBucket[] buckets)
            => ZoneControlManager.CompareZone("Test", variation, true, Stat, before, after, minion, minion, buckets);

        [TestMethod]
        public void TierDefaultEdit_ReachesEveryMonster_WhenTheZoneSetsNothing()
        {
            var z = Compare(14, Defaults((14, Row((Stat, 100)))), Defaults((14, Row((Stat, 200)))));

            Assert.AreEqual("full", z.Reach);
            Assert.AreEqual(4, z.MonsterKinds, "no rank, regular, leader, boss");
            Assert.IsTrue(z.Changed.All(m => m.Before == 100 && m.After == 200));
        }

        [TestMethod]
        public void ZoneBossRow_KeepsItsOwnValue_SoTheZoneIsPartial()
        {
            var zone = Row();
            zone.RankLayer(ZcRank.Boss, create: true).Stats[Stat] = new StatCurve { Base = 999 };

            var z = Compare(14, Defaults((14, Row((Stat, 100)))), Defaults((14, Row((Stat, 200)))), zone);

            Assert.AreEqual("partial", z.Reach);
            Assert.AreEqual(3, z.Changed.Count);
            Assert.IsFalse(z.Changed.Any(m => m.Label == "Boss"));
        }

        [TestMethod]
        public void ZoneDefaultRow_SettingItsOwnValue_IsUnchanged()
        {
            var z = Compare(14, Defaults((14, Row((Stat, 100)))), Defaults((14, Row((Stat, 200)))), Row((Stat, 5)));
            Assert.AreEqual("none", z.Reach);
        }

        [TestMethod]
        public void MonsterOverride_KeepsItsOwnValue()
        {
            var bucket = Row((Stat, 7));
            var z = Compare(14, Defaults((14, Row((Stat, 100)))), Defaults((14, Row((Stat, 200)))), null,
                new ZoneControlManager.ImpactBucket(42, ZcRank.Leader, bucket, bucket));

            Assert.AreEqual("partial", z.Reach);
            Assert.AreEqual(5, z.MonsterKinds);
            Assert.IsFalse(z.Changed.Any(m => m.Label.StartsWith("wcid 42")));
        }

        [TestMethod]
        public void AnchorBoardEdit_ReachesAT14Zone_ThroughItsTier()
        {
            var z = Compare(14, Defaults((11, Row((Stat, 100)))), Defaults((11, Row((Stat, 300)))));
            Assert.AreEqual("full", z.Reach);
        }

        [TestMethod]
        public void AnchorBoardEdit_DoesNotReachATierThatSetsTheStat()
        {
            var t14 = Row((Stat, 50));
            var z = Compare(14, Defaults((11, Row((Stat, 100))), (14, t14)), Defaults((11, Row((Stat, 300))), (14, t14)));
            Assert.AreEqual("none", z.Reach);
        }

        // Adding a T25 anchor bends the line: at T18 the value is half way from the T11 value to the T25 one.
        [TestMethod]
        public void AddingAT25Anchor_ChangesTheValueAtTheZonesTier()
        {
            var z = Compare(18, Defaults((11, Row((Stat, 100)))), Defaults((11, Row((Stat, 100), (Stat + "_t25", 240)))));

            Assert.AreEqual("full", z.Reach);
            Assert.AreEqual(100, z.Changed[0].Before);
            Assert.AreEqual(170, z.Changed[0].After);
        }

        [TestMethod]
        public void ZoneTurningTheStatOff_IsUnchanged()
        {
            var zone = Row();
            zone.StatToggles[Stat] = false;
            var z = Compare(14, Defaults((14, Row((Stat, 100)))), Defaults((14, Row((Stat, 200)))), zone);
            Assert.AreEqual("none", z.Reach);
        }

        [TestMethod]
        public void ClearingTheOnlyValue_ShowsNotSetAfter()
        {
            var z = Compare(14, Defaults((14, Row((Stat, 100)))), Defaults((14, Row())));
            Assert.AreEqual("full", z.Reach);
            Assert.IsTrue(z.Changed.All(m => m.After == null));
        }
    }
}
