using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Server.Managers.WeaponScaling;
using ACE.Server.Managers.ZoneControl;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// The "which tier is this player" helper built from the T11+ hit gate's own comparison
    /// (TierHitGate.MeetsTier / HighestTier). Pure counters and tier rows only - no DB, no player.
    /// The default ladder (one stat since the 2026-11-22 Triune retire): Creature 4,000 +500/tier (6,000 at T15, 11,000 at
    /// T25); Item, Life and the best attack school 2,000 +500/tier (4,000 at T15, 4,500 at T16, 9,000 at T25) - all four asked
    /// at every tier (owner 2026-11-22: uniform T11-T25).
    /// </summary>
    [TestClass]
    public class TierHitGateTests
    {
        private static List<WeaponScalingTier> Rows() => WeaponScalingManager.BuildDefaults().Tiers;

        /// <summary>A player exactly at tier t's gates (Life and school on the same +500 ladder).</summary>
        private static int At(int t, int creatureDelta = 0, int itemDelta = 0, int lifeDelta = 0, int schoolDelta = 0, List<WeaponScalingTier> rows = null)
            => TierHitGate.HighestTier(
                WeaponScalingManager.DefaultMinWieldCreature(t) + creatureDelta,
                2000 + 500 * (t - 11) + itemDelta,
                WeaponScalingManager.DefaultMinWieldLife(t) + lifeDelta,
                WeaponScalingManager.DefaultMinWieldSchool(t) + schoolDelta,
                rows ?? Rows());

        [TestMethod]
        public void HighestTier_BelowT11_IsZero()
        {
            Assert.AreEqual(0, TierHitGate.HighestTier(3999, 2000, 0, 0, Rows()), "one Creature aug short of T11");
            Assert.AreEqual(0, TierHitGate.HighestTier(4000, 1999, 2000, 0, Rows()), "one Item aug short of T11");
            Assert.AreEqual(0, TierHitGate.HighestTier(4000, 2000, 1999, 0, Rows()), "one Life aug short of T11");
            Assert.AreEqual(0, TierHitGate.HighestTier(0, 0, 0, 0, Rows()));
        }

        [TestMethod]
        public void HighestTier_ExactBoundaries_T11ThroughT15_AllFourCount()
        {
            Assert.AreEqual(0, TierHitGate.HighestTier(4000, 2000, 2000, 0, Rows()), "the school is asked from T11 (uniform, 2026-11-22)");
            Assert.AreEqual(0, TierHitGate.HighestTier(4000, 2000, 2000, 1999, Rows()), "school one short of T11");
            Assert.AreEqual(11, TierHitGate.HighestTier(4000, 2000, 2000, 2000, Rows()));
            Assert.AreEqual(11, TierHitGate.HighestTier(4500, 2499, 2500, 2500, Rows()), "Item one short of T12");
            Assert.AreEqual(11, TierHitGate.HighestTier(4499, 2500, 2500, 2500, Rows()), "Creature one short of T12");
            Assert.AreEqual(11, TierHitGate.HighestTier(4500, 2500, 2499, 2500, Rows()), "Life one short of T12");
            Assert.AreEqual(11, TierHitGate.HighestTier(4500, 2500, 2500, 2499, Rows()), "school one short of T12");
            Assert.AreEqual(12, TierHitGate.HighestTier(4500, 2500, 2500, 2500, Rows()));
            Assert.AreEqual(13, TierHitGate.HighestTier(5000, 3000, 3000, 3000, Rows()));
            Assert.AreEqual(14, TierHitGate.HighestTier(5500, 3500, 3500, 3500, Rows()));
            Assert.AreEqual(14, TierHitGate.HighestTier(6000, 4000, 3999, 4000, Rows()), "Life one short of T15");
            Assert.AreEqual(12, TierHitGate.HighestTier(6000, 4000, 4000, 2500, Rows()), "a T15 player whose best school is 2,500 is a T12 player");
            Assert.AreEqual(15, TierHitGate.HighestTier(6000, 4000, 4000, 4000, Rows()));
            for (var t = 11; t <= 15; t++)
            {
                Assert.AreEqual(t - 1 < 11 ? 0 : t - 1, At(t, lifeDelta: -1), $"Life one short of T{t}");
                Assert.AreEqual(t - 1 < 11 ? 0 : t - 1, At(t, schoolDelta: -1), $"school one short of T{t}");
            }
        }

        [TestMethod]
        public void SchoolLadder_MatchesItemAndLife_AtEveryTier()
        {
            foreach (var r in Rows())
            {
                Assert.AreEqual(r.MinWieldAugs, r.MinWieldSkillCharm, $"T{r.Tier} school = Item");
                Assert.AreEqual(r.MinWieldLife, r.MinWieldSkillCharm, $"T{r.Tier} school = Life");
            }
            Assert.AreEqual(2000, WeaponScalingManager.DefaultMinWieldSchool(11));
            Assert.AreEqual(4000, WeaponScalingManager.DefaultMinWieldSchool(15));
            Assert.AreEqual(4500, WeaponScalingManager.DefaultMinWieldSchool(16));
            Assert.AreEqual(9000, WeaponScalingManager.DefaultMinWieldSchool(25));
            Assert.AreEqual(0, WeaponScalingManager.DefaultMinWieldSchool(10));
        }

        [TestMethod]
        public void Normalize_SeedsTheSchoolOnT11ToT15Rows_StoredBeforeTheUniformGate()
        {
            // a store saved while only T16+ asked a school: T11-T15 rows hold 0
            var cfg = WeaponScalingManager.BuildDefaults();
            foreach (var r in cfg.Tiers.Where(r => r.Tier < 16))
                r.MinWieldSkillCharm = 0;
            WeaponScalingManager.Normalize(cfg);
            Assert.AreEqual(2000, cfg.Tiers.Single(r => r.Tier == 11).MinWieldSkillCharm);
            Assert.AreEqual(4000, cfg.Tiers.Single(r => r.Tier == 15).MinWieldSkillCharm);
            Assert.AreEqual(4500, cfg.Tiers.Single(r => r.Tier == 16).MinWieldSkillCharm, "a folded T16 row is left as it is");

            // an UNFOLDED store (the old build's): T16's empty charm number folds to the default once, never seeded twice
            var old = WeaponScalingManager.BuildDefaults();
            old.TriuneFolded = false;
            var t16 = old.Tiers.Single(r => r.Tier == 16);
            (t16.MinWieldCreature, t16.MinWieldAugs, t16.MinWieldLife, t16.MinWieldTriune, t16.MinWieldSkillCharm) = (6000, 4000, 4000, 500, 0);
            foreach (var r in old.Tiers.Where(r => r.Tier < 16))
                r.MinWieldSkillCharm = 0;
            WeaponScalingManager.Normalize(old);
            Assert.AreEqual(4500, t16.MinWieldSkillCharm, "T16 with no charm number = the default, once");
            Assert.AreEqual(3500, old.Tiers.Single(r => r.Tier == 14).MinWieldSkillCharm);
        }

        [TestMethod]
        public void HighestTier_FromT16_AllFourCount()
        {
            Assert.AreEqual(16, At(16));
            Assert.AreEqual(15, At(16, creatureDelta: -1), "Creature one short of T16");
            Assert.AreEqual(15, At(16, itemDelta: -1), "Item one short of T16");
            Assert.AreEqual(15, At(16, lifeDelta: -1), "Life one short of T16");
            Assert.AreEqual(15, At(16, schoolDelta: -1), "school one short of T16");
            Assert.AreEqual(20, At(20));
            Assert.AreEqual(24, At(25, schoolDelta: -1));
            Assert.AreEqual(25, At(25));
            Assert.AreEqual(25, TierHitGate.HighestTier(long.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue, Rows()), "never past the top row");
        }

        [TestMethod]
        public void HighestTier_IsAContiguousClimb()
        {
            // T12's Item requirement is missed, so T13+ are never reached even with the Creature count for T15.
            Assert.AreEqual(11, TierHitGate.HighestTier(6000, 2000, 4000, 4000, Rows()));

            // a hole in the table stops the climb at the tier below it
            var gap = Rows().Where(r => r.Tier != 13).ToList();
            Assert.AreEqual(12, At(25, rows: gap));
        }

        [TestMethod]
        public void HighestTier_IgnoresRowsBelowT11_AndRowOrder()
        {
            // the stored table carries a T10 row; it must neither count as a tier nor block the climb
            var rows = Rows();
            rows.Add(new WeaponScalingTier { Tier = 10, Cap = 2000, MinWieldCreature = 999999 });
            rows.Reverse();
            Assert.AreEqual(12, TierHitGate.HighestTier(4500, 2500, 2500, 2500, rows));
        }

        [TestMethod]
        public void HighestTier_IgnoresRowsPastTheTopOfTheLadder()
        {
            // "/weaponscale tier add 26" is accepted; nobody becomes tier 26
            var rows = Rows();
            rows.Add(new WeaponScalingTier { Tier = 26, Cap = 10000, MinWieldCreature = 11500, MinWieldAugs = 9500, MinWieldLife = 9500, MinWieldSkillCharm = 9500 });
            Assert.AreEqual(25, TierHitGate.HighestTier(99999, 99999, 99999, 99999, rows));
        }

        [TestMethod]
        public void HighestTier_NullOrEmptyTable_IsZero()
        {
            Assert.AreEqual(0, TierHitGate.HighestTier(6000, 4000, 4000, 4000, null));
            Assert.AreEqual(0, TierHitGate.HighestTier(6000, 4000, 4000, 4000, new List<WeaponScalingTier>()));
        }

        [TestMethod]
        public void MeetsTier_SkipsRequirementsOfZero_AndRejectsNull()
        {
            var t11 = Rows().Single(r => r.Tier == 11);
            Assert.AreEqual(0, t11.MinWieldTriune, "no row asks for Triune any more");
            Assert.IsTrue(TierHitGate.MeetsTier(4000, 2000, 2000, 2000, t11));
            Assert.IsFalse(TierHitGate.MeetsTier(3999, 2000, 2000, 2000, t11));
            Assert.IsFalse(TierHitGate.MeetsTier(4000, 2000, 1999, 2000, t11), "a Life shortfall alone blocks T11 (parity, 2026-11-22)");
            Assert.IsFalse(TierHitGate.MeetsTier(4000, 2000, 2000, 1999, t11), "a school shortfall alone blocks T11 (uniform, 2026-11-22)");
            Assert.IsFalse(TierHitGate.MeetsTier(4000, 2000, 2000, 2000, null));

            var t16 = Rows().Single(r => r.Tier == 16);
            Assert.AreEqual(0, t16.MinWieldTriune);
            Assert.IsTrue(TierHitGate.MeetsTier(6500, 4500, 4500, 4500, t16));
            Assert.IsFalse(TierHitGate.MeetsTier(6500, 4500, 4499, 4500, t16), "a Life shortfall alone blocks T16");
            Assert.IsFalse(TierHitGate.MeetsTier(6500, 4500, 4500, 4499, t16), "a school shortfall alone blocks T16");

            // a row that asks for nothing is met by anyone
            Assert.IsTrue(TierHitGate.MeetsTier(0, 0, 0, 0, new WeaponScalingTier { Tier = 11 }));
        }

        [TestMethod]
        public void Normalize_SeedsAnUnfoldedStoreOnTheOldLadders_BeforeTheFold()
        {
            // review 2026-11-22: an old-build store saved before MinWieldLife / MinWieldCreature existed holds 0 there. The
            // seed must be the OLD clamped ladder (6,000 / 4,000), so the fold adds the row's Triune only once.
            var old = WeaponScalingManager.BuildDefaults();
            old.TriuneFolded = false;
            var t16 = old.Tiers.Single(r => r.Tier == 16);
            var t25 = old.Tiers.Single(r => r.Tier == 25);
            (t16.MinWieldCreature, t16.MinWieldAugs, t16.MinWieldLife, t16.MinWieldTriune, t16.MinWieldSkillCharm) = (0, 4000, 0, 500, 500);
            (t25.MinWieldCreature, t25.MinWieldAugs, t25.MinWieldLife, t25.MinWieldTriune, t25.MinWieldSkillCharm) = (0, 4000, 0, 5000, 5000);
            WeaponScalingManager.Normalize(old);
            Assert.AreEqual((6500, 4500, 4500, 0, 4500), (t16.MinWieldCreature, t16.MinWieldAugs, t16.MinWieldLife, t16.MinWieldTriune, t16.MinWieldSkillCharm));
            Assert.AreEqual((11000, 9000, 9000, 0, 9000), (t25.MinWieldCreature, t25.MinWieldAugs, t25.MinWieldLife, t25.MinWieldTriune, t25.MinWieldSkillCharm));
        }

        [TestMethod]
        public void Normalize_OfTheRealOldStore_GivesTheUniformLadder_AndUnfoldGivesItBack()
        {
            // review 2026-11-22: the shape of the 11-09 live copy - no Creature / Life at all, Item 2000-4000 then 4000, T16+
            // Triune = school = 500 * (N - 15), T11-T15 school 0
            var old = WeaponScalingManager.BuildDefaults();
            old.TriuneFolded = false;
            foreach (var r in old.Tiers.Where(r => r.Tier >= 11))
            {
                r.MinWieldCreature = 0;
                r.MinWieldLife = 0;
                r.MinWieldAugs = r.Tier <= 15 ? 2000 + 500 * (r.Tier - 11) : 4000;
                r.MinWieldTriune = r.MinWieldSkillCharm = r.Tier <= 15 ? 0 : 500 * (r.Tier - 15);
            }
            WeaponScalingManager.Normalize(old);
            foreach (var r in old.Tiers.Where(r => r.Tier >= 11))
            {
                var step = 500 * (r.Tier - 11);
                Assert.AreEqual((4000 + step, 2000 + step, 2000 + step, 0, 2000 + step),
                    (r.MinWieldCreature, r.MinWieldAugs, r.MinWieldLife, r.MinWieldTriune, r.MinWieldSkillCharm), $"T{r.Tier}");
            }

            WeaponScalingManager.UnfoldTriune(old.Tiers);
            foreach (var r in old.Tiers.Where(r => r.Tier >= 11))
            {
                var step = 500 * (r.Tier - 11);
                var triune = r.Tier <= 15 ? 0 : 500 * (r.Tier - 15);
                Assert.AreEqual((r.Tier <= 15 ? 2000 + step : 4000, triune, triune), (r.MinWieldAugs, r.MinWieldTriune, r.MinWieldSkillCharm), $"T{r.Tier} unfolded");
                Assert.AreEqual((Math.Min(6000, 4000 + step), Math.Min(4000, 2000 + step)), (r.MinWieldCreature, r.MinWieldLife), $"T{r.Tier} unfolded C/L");
            }
        }

        [TestMethod]
        public void SchoolOf_AnAttack_AsksItsSchool_AndNothingElse()
        {
            Assert.AreEqual(TierHitGate.AttackSchool.Melee, TierHitGate.SchoolOf(ACE.Server.WorldObjects.CombatType.Melee));
            Assert.AreEqual(TierHitGate.AttackSchool.Missile, TierHitGate.SchoolOf(ACE.Server.WorldObjects.CombatType.Missile));
            Assert.AreEqual(TierHitGate.AttackSchool.None, TierHitGate.SchoolOf(ACE.Server.WorldObjects.CombatType.Magic), "a spell's school comes from the spell");
            Assert.AreEqual(TierHitGate.AttackSchool.None, TierHitGate.SchoolOf((ACE.Server.Entity.Spell)null, null), "no spell, no item: no school");
        }

        [TestMethod]
        public void FoldTriune_MovesTheTriuneStepOntoAllThree_AndUnfoldRestoresIt()
        {
            // an old-style T16 row: Creature / Item / Life at the caps, 500 Triune, 500 charm
            var old = new WeaponScalingTier { Tier = 16, MinWieldCreature = 6000, MinWieldAugs = 4000, MinWieldLife = 4000, MinWieldTriune = 500, MinWieldSkillCharm = 500 };
            var t15 = new WeaponScalingTier { Tier = 15, MinWieldCreature = 6000, MinWieldAugs = 4000, MinWieldLife = 4000 };
            var noSchool = new WeaponScalingTier { Tier = 20, MinWieldCreature = 6000, MinWieldAugs = 4000, MinWieldLife = 4000, MinWieldTriune = 2500 };
            WeaponScalingManager.FoldTriune(new[] { old, t15, noSchool });

            Assert.AreEqual((6500, 4500, 4500, 0, 4500), (old.MinWieldCreature, old.MinWieldAugs, old.MinWieldLife, old.MinWieldTriune, old.MinWieldSkillCharm));
            Assert.AreEqual((6000, 4000, 4000, 0, 0), (t15.MinWieldCreature, t15.MinWieldAugs, t15.MinWieldLife, t15.MinWieldTriune, t15.MinWieldSkillCharm), "below T16 untouched");
            Assert.AreEqual(WeaponScalingManager.DefaultMinWieldSchool(20), noSchool.MinWieldSkillCharm, "no school number = the default");
            Assert.AreEqual((8500, 6500, 6500), (noSchool.MinWieldCreature, noSchool.MinWieldAugs, noSchool.MinWieldLife));

            Assert.AreEqual(2, WeaponScalingManager.UnfoldTriune(new[] { old, t15, noSchool }), "only T16+ rows change (t15 has no school number)");

            // a T11-T15 row with the uniform school number goes back to none for the old build
            var t14 = new WeaponScalingTier { Tier = 14, MinWieldCreature = 5500, MinWieldAugs = 3500, MinWieldLife = 3500, MinWieldSkillCharm = 3500 };
            Assert.AreEqual(1, WeaponScalingManager.UnfoldTriune(new[] { t14 }));
            Assert.AreEqual((5500, 3500, 3500, 0, 0), (t14.MinWieldCreature, t14.MinWieldAugs, t14.MinWieldLife, t14.MinWieldTriune, t14.MinWieldSkillCharm));
            Assert.AreEqual((6000, 4000, 4000, 500, 500), (old.MinWieldCreature, old.MinWieldAugs, old.MinWieldLife, old.MinWieldTriune, old.MinWieldSkillCharm));
            Assert.AreEqual((6000, 4000, 4000, 2500), (noSchool.MinWieldCreature, noSchool.MinWieldAugs, noSchool.MinWieldLife, noSchool.MinWieldTriune));
        }
    }
}
