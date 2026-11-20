using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Server.Managers.ZoneControl;
using ACE.Server.Managers.ZoneScaling;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>The portal's "where the value comes from" ladder (ZoneStatLadderBuilder) must say what the game's merge does.</summary>
    [TestClass]
    public class ZoneStatLadderTests
    {
        private const string Stat = "max_health";
        private const string Twin = "max_health_t25";

        private static ZoneVariantProfile Row(params (string Key, double Value)[] stats)
        {
            var p = new ZoneVariantProfile();
            foreach (var (k, v) in stats)
                p.Stats[k] = new StatCurve { Base = v };
            return p;
        }

        private static ZoneStatLadder ZoneLadder(ZoneVariantProfile anchor, ZoneVariantProfile tier, ZoneVariantProfile zone, ZoneVariantProfile bucket = null,
            ZcRank rank = ZcRank.None, int variation = 14, uint? wcid = null)
            => ZoneStatLadderBuilder.Build(Stat, ZoneStatLadderBuilder.ZoneStack("Test", variation, rank, wcid ?? (bucket != null ? 1u : null), anchor, tier, zone, bucket));

        // What the game runs: each layer flattened for the rank, v11 anchor under the tier, then zone, then bucket (ZoneControlManager).
        private static ZoneVariantProfile GameMerge(ZoneVariantProfile anchor, ZoneVariantProfile tier, ZoneVariantProfile zone, ZoneVariantProfile bucket, ZcRank rank)
        {
            var def = ZoneVariantProfile.Merge(anchor?.ForRank(rank), tier?.ForRank(rank));
            var merged = ZoneVariantProfile.Merge(def, zone?.ForRank(rank), bucket);
            foreach (var kv in merged.StatToggles.Where(kv => !kv.Value).ToList())
                merged.Stats.Remove(kv.Key);
            return merged;
        }

        private static ZoneStatRung Winner(ZoneStatLadder l) => l.Rungs.SingleOrDefault(r => r.State is ZoneStatRungState.Wins or ZoneStatRungState.Off);

        [TestMethod]
        public void Rungs_AreMostSpecificFirst_AndTheClosestRowWins()
        {
            var anchor = Row((Stat, 1));
            var tier = Row((Stat, 2));
            tier.RankLayer(ZcRank.Boss, create: true).Stats[Stat] = new StatCurve { Base = 3 };
            var zone = Row((Stat, 4));
            zone.RankLayer(ZcRank.Boss, create: true).Stats[Stat] = new StatCurve { Base = 5 };

            var ladder = ZoneLadder(anchor, tier, zone, rank: ZcRank.Boss);

            CollectionAssert.AreEqual(new double?[] { 5, 4, 3, 2, null, 1 }, ladder.Rungs.Select(r => r.Value).ToArray());
            Assert.AreEqual(ZoneStatRungState.Wins, ladder.Rungs[0].State);
            Assert.IsTrue(ladder.Rungs.Skip(1).All(r => r.State == (r.Value == null ? ZoneStatRungState.NotSet : ZoneStatRungState.Overridden)));
            Assert.IsTrue(ladder.Rungs[0].IsTarget, "the zone's Boss row is the one being edited");
            Assert.IsTrue(ladder.InEffect);
        }

        // A zone's Default row beats a tier's rank row (owner D2: local beats global).
        [TestMethod]
        public void ZoneDefaultRow_BeatsTierRankRow()
        {
            var tier = Row();
            tier.RankLayer(ZcRank.Boss, create: true).Stats[Stat] = new StatCurve { Base = 3 };
            var ladder = ZoneLadder(null, tier, Row((Stat, 4)), rank: ZcRank.Boss);

            Assert.AreEqual(4, Winner(ladder).Value);
            Assert.AreEqual(4, GameMerge(null, tier, Row((Stat, 4)), null, ZcRank.Boss).Stats[Stat].Base);
        }

        [TestMethod]
        public void TierRowSettingT11Alone_DropsTheAnchorBoardsT25()
        {
            var anchor = Row((Stat, 100), (Twin, 900));
            var tier = Row((Stat, 150));

            var ladder = ZoneLadder(anchor, tier, Row());

            var anchorRung = ladder.Rungs.Single(r => r.Layer == "anchor" && r.Rank == null);
            Assert.AreEqual(ZoneStatRungState.Shadowed, anchorRung.AnchorState);
            Assert.AreEqual(ZoneStatRungState.Overridden, anchorRung.State);
            Assert.IsFalse(ladder.Anchored);
            Assert.IsFalse(GameMerge(anchor, tier, Row(), null, ZcRank.None).Stats.ContainsKey(Twin));
        }

        [TestMethod]
        public void AnchorSetAboveTheShadow_WinsAgain()
        {
            var anchor = Row((Stat, 100), (Twin, 900));
            var tier = Row((Stat, 150));
            var zone = Row((Twin, 700));

            var ladder = ZoneLadder(anchor, tier, zone);

            Assert.AreEqual(ZoneStatRungState.Wins, ladder.Rungs.Single(r => r.Layer == "zone").AnchorState);
            Assert.IsTrue(ladder.Anchored);
            Assert.AreEqual(700, GameMerge(anchor, tier, zone, null, ZcRank.None).Stats[Twin].Base);
        }

        [TestMethod]
        public void ToggleOff_MarksTheWinnerOff_AndACloserToggleOnRestoresIt()
        {
            var tier = Row((Stat, 150));
            var zone = Row();
            zone.StatToggles[Stat] = false;

            var off = ZoneLadder(null, tier, zone);
            Assert.IsTrue(off.TurnedOff);
            Assert.IsFalse(off.InEffect);
            Assert.AreEqual(ZoneStatRungState.Off, Winner(off).State);
            Assert.AreEqual(false, off.Rungs.Single(r => r.Layer == "zone").Toggle);

            var bucket = Row();
            bucket.StatToggles[Stat] = true;
            var on = ZoneLadder(null, tier, zone, bucket);
            Assert.IsTrue(on.InEffect);
            Assert.AreEqual(ZoneStatRungState.Wins, Winner(on).State);
            Assert.IsTrue(GameMerge(null, tier, zone, bucket, ZcRank.None).Stats.ContainsKey(Stat));
        }

        [TestMethod]
        public void T25Key_ShowsItsBaseStatsLadder()
        {
            var ladder = ZoneStatLadderBuilder.Build(Twin, ZoneStatLadderBuilder.TierStack(14, ZcRank.None, null, Row((Stat, 1), (Twin, 2))));
            Assert.AreEqual(Stat, ladder.Stat);
            Assert.AreEqual(2, ladder.Rungs[0].Anchor);
        }

        [TestMethod]
        public void TierStack_HasNoAnchorRows_AtOrBelowT11()
        {
            Assert.IsFalse(ZoneStatLadderBuilder.TierStack(11, ZcRank.Boss, Row(), Row()).Any(l => l.Kind == "anchor"));
            Assert.AreEqual(2, ZoneStatLadderBuilder.TierStack(12, ZcRank.Boss, Row(), Row()).Count(l => l.Kind == "anchor"));
        }

        [TestMethod]
        public void NothingSet_IsNotInEffect()
        {
            var ladder = ZoneLadder(Row(), Row(), Row());
            Assert.IsFalse(ladder.InEffect);
            Assert.IsFalse(ladder.TurnedOff);
            Assert.IsNull(Winner(ladder));
        }

        // Many random stacks: the ladder's winner, anchor and on/off must match the real merge every time.
        [TestMethod]
        public void RandomStacks_AgreeWithTheGameMerge()
        {
            var rng = new Random(20261111);
            for (var n = 0; n < 3000; n++)
            {
                var rank = (ZcRank)rng.Next(0, 4);
                ZoneVariantProfile MakeLayer(bool ranks)
                {
                    if (rng.Next(5) == 0)
                        return null;
                    var p = RandomRow();
                    if (ranks && rank != ZcRank.None && rng.Next(2) == 0)
                        p.Ranks[ZoneRank.Key(rank)] = RandomRow();
                    return p;
                }
                ZoneVariantProfile RandomRow()
                {
                    var p = new ZoneVariantProfile();
                    if (rng.Next(2) == 0) p.Stats[Stat] = new StatCurve { Base = rng.Next(1, 1000) };
                    if (rng.Next(3) == 0) p.Stats[Twin] = new StatCurve { Base = rng.Next(1, 1000) };
                    if (rng.Next(5) == 0) p.StatToggles[Stat] = rng.Next(2) == 0;
                    if (rng.Next(8) == 0) p.StatToggles[Twin] = rng.Next(2) == 0;
                    return p;
                }

                var anchor = MakeLayer(true);
                var tier = MakeLayer(true);
                var zone = MakeLayer(true);
                var bucket = rng.Next(2) == 0 ? MakeLayer(false) : null;
                var ladder = ZoneStatLadderBuilder.Build(Stat, ZoneStatLadderBuilder.ZoneStack("Test", 14, rank, bucket != null ? 1u : null, anchor, tier, zone, bucket));
                var game = GameMerge(anchor, tier, zone, bucket, rank);

                var where = $"case {n}";
                Assert.AreEqual(game.Stats.ContainsKey(Stat), ladder.InEffect, where);
                if (ladder.InEffect)
                    Assert.AreEqual(game.Stats[Stat].Base, Winner(ladder).Value, where);
                // and the OFF state: set somewhere and turned off by the merged toggle must read as turned off, not as unset
                var raw = ZoneVariantProfile.Merge(ZoneVariantProfile.Merge(anchor?.ForRank(rank), tier?.ForRank(rank)), zone?.ForRank(rank), bucket);
                var off = raw.Stats.ContainsKey(Stat) && raw.StatToggles.TryGetValue(Stat, out var on) && !on;
                Assert.AreEqual(off, ladder.TurnedOff, where + " turned off");
                if (off)
                    Assert.AreEqual(ZoneStatRungState.Off, Winner(ladder).State, where + " winner state");
                var twinWins = ladder.Rungs.SingleOrDefault(r => r.AnchorState == ZoneStatRungState.Wins);
                Assert.AreEqual(game.Stats.ContainsKey(Twin), twinWins != null, where);
                if (twinWins != null)
                    Assert.AreEqual(game.Stats[Twin].Base, twinWins.Anchor, where);
            }
        }
    }
}
