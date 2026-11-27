using System;
using System.Linq;

using ACE.Server.Managers.ZoneControl;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// T11-T14 aug drip (owner 2026-11-08, decided design in C:\AI\Tier_Progression_Lum_Plan_2026-11-07.md section 9):
    /// the pace, the ladder, the gem floor / ceiling and the price floor. Pure rules - no DB, no world.
    /// </summary>
    [TestClass]
    public class AugDripTests
    {
        private static double Week(TriuneTierRow r, double kph)
        {
            var kills = (int)(kph * TriuneEarning.HoursPerWeek);
            var sum = 0.0;
            for (var k = 0; k < kills; k++)
                sum += TriuneEarning.KillValue(r, k);
            return sum;
        }

        [TestMethod]
        public void A_Drip_Row_Edit_Survives_The_Store_Round_Trip()
        {
            // what Mutate does: clone through JSON, edit, normalize
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(TriuneEarning.Defaults());
            var clone = TriuneEarning.Normalize(Newtonsoft.Json.JsonConvert.DeserializeObject<TriuneEarning.Config>(json));
            foreach (var r in clone.Tiers.Where(r => r.Tier == 11)) { r.Points = 499; r.FloorKph = 0.05; r.CeilingKph = 0.1; }
            clone = TriuneEarning.Normalize(clone);
            var row = clone.Tiers.Single(r => r.Tier == 11);
            Assert.AreEqual(499, row.Points);
            Assert.AreEqual(0.05, row.FloorKph, 1e-9);
            Assert.AreEqual(0.1, row.CeilingKph, 1e-9);
        }

        [TestMethod]
        public void Drip_Tiers_Are_11_To_14()
        {
            Assert.IsFalse(TriuneEarning.IsDripTier(10));
            Assert.IsTrue(TriuneEarning.IsDripTier(11));
            Assert.IsTrue(TriuneEarning.IsDripTier(14));
            Assert.IsFalse(TriuneEarning.IsDripTier(15));
        }

        [TestMethod]
        public void Default_Drip_Weeks_Match_The_Plan()
        {
            var cfg = TriuneEarning.Defaults();
            var expected = new[] { (11, 3.63, 2.42), (12, 5.30, 3.53), (13, 11.41, 7.61), (14, 16.83, 11.22) };
            foreach (var (tier, slow, fast) in expected)
            {
                var r = cfg.Tiers.Single(t => t.Tier == tier);
                Assert.AreEqual(200, r.FloorKph); Assert.AreEqual(400, r.CeilingKph); Assert.AreEqual(500, r.Points);
                Assert.AreEqual(slow, r.SlowWeeks, 1e-9); Assert.AreEqual(fast, r.FastWeeks, 1e-9);
                Assert.AreEqual(slow, r.GemSlowWeeks, 1e-9, "attack school gems keep the drip's pace");
                Assert.AreEqual(fast, r.GemFastWeeks, 1e-9);
                // a week at 200 kph = Points / slow; a week at 400 kph = Points / fast
                Assert.AreEqual(500 / slow, Week(r, 200), 1e-6);
                Assert.AreEqual(500 / fast, Week(r, 400), 1e-6);
            }
        }

        [TestMethod]
        public void Kills_Per_Point_Match_The_Plan()
        {
            var cfg = TriuneEarning.Defaults();
            // plan section 9: band 1 = 1 point per 244 / 356 / 767 / 1,131 kills; band 2 = twice that
            // (the plan's weeks are rounded to 2 decimals, so allow 1%)
            var expected = new[] { (11, 244.0, 488.0), (12, 356.0, 712.0), (13, 767.0, 1534.0), (14, 1131.0, 2262.0) };
            foreach (var (tier, b1, b2) in expected)
            {
                var r = cfg.Tiers.Single(t => t.Tier == tier);
                Assert.AreEqual(b1, 1 / TriuneEarning.Rate1(r), b1 * 0.01, $"T{tier} band 1");
                Assert.AreEqual(b2, 1 / TriuneEarning.Rate2(r), b2 * 0.01, $"T{tier} band 2");
            }
        }

        [TestMethod]
        public void Ladder_Entry_And_Ceiling_Follow_The_Aug_Ladder()
        {
            var cfg = TriuneEarning.Defaults();
            // owner's ladder: T11 4000/2000 ... T15 6000/4000; each tier's ceiling = the next tier's entry
            var ladder = new[] { (11, 4000L, 2000L), (12, 4500L, 2500L), (13, 5000L, 3000L), (14, 5500L, 3500L) };
            foreach (var (tier, c, i) in ladder)
            {
                Assert.AreEqual(c, TriuneEarning.DripEntry(cfg, tier, cfg.DripBaseCreature), $"T{tier} creature entry");
                Assert.AreEqual(i, TriuneEarning.DripEntry(cfg, tier, cfg.DripBaseItem), $"T{tier} item entry");
                Assert.AreEqual(c + 500, TriuneEarning.DripCeiling(cfg, tier, cfg.DripBaseCreature), $"T{tier} creature ceiling");
                Assert.AreEqual(i + 500, TriuneEarning.DripCeiling(cfg, tier, cfg.DripBaseItem), $"T{tier} item ceiling");
            }
            // the T14 drip ends exactly at the luminance caps (6,000 / 4,000)
            Assert.AreEqual(6000, TriuneEarning.DripCeiling(cfg, 14, cfg.DripBaseCreature));
            Assert.AreEqual(4000, TriuneEarning.DripCeiling(cfg, 14, cfg.DripBaseItem));
        }

        [TestMethod]
        public void Triune_Caps_Ignore_The_Drip_Rows()
        {
            var cfg = TriuneEarning.Defaults();
            Assert.AreEqual(0, TriuneEarning.TierCap(cfg, 14));
            Assert.AreEqual(500, TriuneEarning.TierCap(cfg, 15));
            Assert.AreEqual(5500, TriuneEarning.TierCap(cfg, 25));
        }

        [TestMethod]
        public void School_Gem_Floor_And_Ceiling_Per_Tier()
        {
            var cfg = TriuneEarning.Defaults();
            // T11-T14: the attack school rides the Item / Life ladder
            Assert.AreEqual(2000, TriuneEarning.GemFloor(cfg, 11));
            Assert.AreEqual(2500, TriuneEarning.GemCeiling(cfg, 11));
            Assert.AreEqual(3500, TriuneEarning.GemFloor(cfg, 14));
            Assert.AreEqual(4000, TriuneEarning.GemCeiling(cfg, 14));
            // T15+: unchanged (base cap 4,000, ceiling base + Triune points)
            Assert.AreEqual(4000, TriuneEarning.GemFloor(cfg, 15));
            Assert.AreEqual(4500, TriuneEarning.GemCeiling(cfg, 15));
            Assert.AreEqual(9500, TriuneEarning.GemCeiling(cfg, 25));
            // ceilings never go down tier to tier (the pickup-room rule orders carried gems by tier)
            for (var t = TriuneEarning.DripMinTier; t < TriuneEarning.MaxTier; t++)
                Assert.IsTrue(TriuneEarning.GemCeiling(cfg, t + 1) >= TriuneEarning.GemCeiling(cfg, t), $"T{t} -> T{t + 1}");
        }

        [TestMethod]
        public void A_Reward_Picks_One_School_Evenly_From_Those_With_Rolls_Left()
        {
            // all three open: the roll splits [0, 1) in thirds
            Assert.AreEqual(0, TriuneEarning.PickSchool(new long[] { 0, 0, 0 }, 500, 0.0));
            Assert.AreEqual(0, TriuneEarning.PickSchool(new long[] { 0, 0, 0 }, 500, 0.333));
            Assert.AreEqual(1, TriuneEarning.PickSchool(new long[] { 0, 0, 0 }, 500, 0.334));
            Assert.AreEqual(2, TriuneEarning.PickSchool(new long[] { 0, 0, 0 }, 500, 0.999999));
            Assert.AreEqual(2, TriuneEarning.PickSchool(new long[] { 0, 0, 0 }, 500, 1.0), "a roll of 1 stays in range");
            // Item done: Creature and Life share it in halves
            Assert.AreEqual(0, TriuneEarning.PickSchool(new long[] { 10, 500, 10 }, 500, 0.49));
            Assert.AreEqual(2, TriuneEarning.PickSchool(new long[] { 10, 500, 10 }, 500, 0.51));
            // one open school takes every roll
            Assert.AreEqual(1, TriuneEarning.PickSchool(new long[] { 500, 499, 600 }, 500, 0.9));
            // every school done: nothing left in this tier
            Assert.AreEqual(-1, TriuneEarning.PickSchool(new long[] { 500, 500, 500 }, 500, 0.5));
        }

        [TestMethod]
        public void A_Roll_On_A_Full_School_Is_Lost()
        {
            Assert.IsTrue(TriuneEarning.RollAdds(4499, 4500));
            Assert.IsFalse(TriuneEarning.RollAdds(4500, 4500), "at the tier's end: the roll is used, nothing added");
            Assert.IsFalse(TriuneEarning.RollAdds(4600, 4500), "past it (bought or gem): nothing added");
        }

        [TestMethod]
        public void The_Move_Seeds_The_Own_Tier_Rolls_From_Triune()
        {
            var cfg = TriuneEarning.Defaults();
            // T15: nothing below asked Triune, so 141 Triune = 141 rolls taken
            Assert.AreEqual(141, TriuneEarning.SeedRolls(cfg, 15, 141));
            // T16: T15 asked 500, so 600 Triune = 100 taken
            Assert.AreEqual(100, TriuneEarning.SeedRolls(cfg, 16, 600));
            Assert.AreEqual(0, TriuneEarning.SeedRolls(cfg, 16, 300), "behind the tier: 0, never negative");
            Assert.AreEqual(500, TriuneEarning.SeedRolls(cfg, 15, 9999), "never more than the tier's rolls");
            Assert.AreEqual(0, TriuneEarning.SeedRolls(cfg, 14, 500), "drip tiers never earned Triune");
        }

        [TestMethod]
        public void The_Seed_Per_School_Never_Passes_How_Far_That_School_Climbed()
        {
            var cfg = TriuneEarning.Defaults();
            // review 2026-11-22: Life 3,900 + Triune 300 moved to 4,200 at T15 (entry 4,000): 200 rolls taken, not 300 -
            // the 300 left carry Life to the tier's end (4,500); a 300 seed would leave it stuck at 4,400 for good
            Assert.AreEqual(200, TriuneEarning.SeedRolls(cfg, 15, 300, 4200, cfg.DripBaseItem));
            Assert.AreEqual(300, TriuneEarning.SeedRolls(cfg, 15, 300, 4300, cfg.DripBaseItem), "a capped school: the Triune seed");
            Assert.AreEqual(300, TriuneEarning.SeedRolls(cfg, 15, 300, 6300, cfg.DripBaseCreature), "Creature from its own ladder (entry 6,000)");
            Assert.AreEqual(0, TriuneEarning.SeedRolls(cfg, 15, 300, 3900, cfg.DripBaseItem), "below the entry: 0, never negative");
            Assert.AreEqual(0, TriuneEarning.SeedRolls(cfg, 14, 300, 3800, cfg.DripBaseItem), "drip tiers never earned Triune");
        }

        [TestMethod]
        public void Kills_Per_Reward_Is_A_Third_Of_Kills_Per_Point()
        {
            // one reward = one point on each of three schools' worth of pace
            Assert.AreEqual(134, TriuneEarning.KillsPerReward(1.0 / 402));
            Assert.AreEqual(0, TriuneEarning.KillsPerReward(0));
        }

        [TestMethod]
        public void Kills_Per_Reward_Is_Bounded_For_A_Tiny_Paying_Rate()
        {
            // two almost equal week values: a rate so small its reciprocal does not fit in a long - still a paying band
            Assert.AreEqual(TriuneEarning.MaxKillsPerReward, TriuneEarning.KillsPerReward(1e-300));
            Assert.AreEqual(TriuneEarning.MaxKillsPerReward, TriuneEarning.KillsPerReward(double.Epsilon));
            Assert.AreEqual(1, TriuneEarning.KillsPerReward(1e300), "a huge rate is still 1, never 0");
            Assert.AreEqual(0, TriuneEarning.KillsPerReward(double.NaN));
        }

        [TestMethod]
        public void Left_Text_Reads_Like_The_Owners_Block()
        {
            Assert.AreEqual("2 days 4 hours", TriuneEarning.LeftText(new TimeSpan(2, 4, 30, 0)));
            Assert.AreEqual("1 day", TriuneEarning.LeftText(TimeSpan.FromDays(1)));
            Assert.AreEqual("3 hours 12 minutes", TriuneEarning.LeftText(new TimeSpan(3, 12, 0)));
            Assert.AreEqual("1 hour", TriuneEarning.LeftText(TimeSpan.FromHours(1)));
            Assert.AreEqual("8 minutes", TriuneEarning.LeftText(TimeSpan.FromMinutes(7.2)));
            Assert.AreEqual("1 minute", TriuneEarning.LeftText(TimeSpan.Zero));
            Assert.AreEqual("1 minute", TriuneEarning.LeftText(TimeSpan.FromMinutes(-5)));
        }

        [TestMethod]
        public void Price_Floor_Is_Max_Of_Used_And_Entry()
        {
            // owner's example: 2,000 used Life at T11 (entry 2,000) -> priced from 2,000, the drip never moves it
            Assert.AreEqual(2000, TriuneEarning.PriceFloor(2000, 2000));
            // a T13 player with 2,200 used Life pays from the T13 entry (3,000)
            Assert.AreEqual(3000, TriuneEarning.PriceFloor(2200, 3000));
            // used past the entry: priced from what was used
            Assert.AreEqual(3200, TriuneEarning.PriceFloor(3200, 3000));
        }

        [TestMethod]
        public void Gem_Price_Rules()
        {
            // rule 1 only (2026-11-22, one stat): max(count, own-tier entry) - a reward never makes the next buy cheaper or dearer
            Assert.AreEqual(2000, TriuneEarning.GemPriceFrom(2000, 2000), "owner's example: 2000 Life at T11 buys from 2000");
            Assert.AreEqual(3000, TriuneEarning.GemPriceFrom(2200, 3000), "T13 player behind: from the entry");
            Assert.AreEqual(3200, TriuneEarning.GemPriceFrom(3200, 3000), "past the entry: from the count");
            Assert.AreEqual(0, TriuneEarning.GemPriceFrom(0, 0), "no entry (an untouched attack school): from 0");
        }

        [TestMethod]
        public void A_Stored_T15_Only_Config_Gains_Default_Drip_Rows()
        {
            // what live / TEST store today: T15-T25 rows only, an edited T15 row
            var cfg = new TriuneEarning.Config { Enabled = true };
            cfg.Tiers.Add(new TriuneTierRow { Tier = 15, Points = 400, SlowWeeks = 5, FastWeeks = 3 });
            cfg = TriuneEarning.Normalize(cfg);
            Assert.AreEqual(15, cfg.Tiers.Count);
            Assert.IsFalse(cfg.DripEnabled, "the drip stays OFF until switched on");
            Assert.AreEqual(400, cfg.Tiers.Single(t => t.Tier == 15).Points, "the T15 edit is kept");
            Assert.AreEqual(5, cfg.Tiers.Single(t => t.Tier == 15).SlowWeeks);
            Assert.AreEqual(3.63, cfg.Tiers.Single(t => t.Tier == 11).SlowWeeks, 1e-9);
            Assert.AreEqual(4000, cfg.DripBaseCreature);
            Assert.AreEqual(2000, cfg.DripBaseItem);
        }
    }
}
