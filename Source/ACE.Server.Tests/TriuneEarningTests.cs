using System;
using System.Linq;

using ACE.Server.Managers.ZoneControl;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Triune earning (owner 2026-11-01): the pure pace rules (bands, rates, tier caps, the weekly reset). No DB, no world.
    /// </summary>
    [TestClass]
    public class TriuneEarningTests
    {
        private static TriuneTierRow Row() => new TriuneTierRow { Tier = 15 };   // 200 / 400 kph, 6 / 4 weeks, 500 points

        /// <summary>A week of kills at a steady pace, kill by kill, the way the server credits them.</summary>
        private static double Week(TriuneTierRow r, double kph)
        {
            var kills = (int)(kph * TriuneEarning.HoursPerWeek);
            var sum = 0.0;
            for (var k = 0; k < kills; k++)
                sum += TriuneEarning.KillValue(r, k);
            return sum;
        }

        // -- the owner's numbers --

        [TestMethod]
        public void Defaults_Are_200_400_6_4_500_PerTier_T15_To_T25()
        {
            var cfg = TriuneEarning.Defaults();
            Assert.IsFalse(cfg.Enabled, "ships OFF");
            Assert.AreEqual((int)DayOfWeek.Friday, cfg.ResetDay);
            Assert.AreEqual(12, cfg.ResetHour);
            Assert.AreEqual(11, cfg.Tiers.Count);
            Assert.AreEqual(15, cfg.Tiers.First().Tier);
            Assert.AreEqual(25, cfg.Tiers.Last().Tier);
            foreach (var r in cfg.Tiers)
            {
                Assert.AreEqual(200, r.FloorKph); Assert.AreEqual(400, r.CeilingKph);
                Assert.AreEqual(6, r.SlowWeeks); Assert.AreEqual(4, r.FastWeeks); Assert.AreEqual(500, r.Points);
            }
        }

        [TestMethod]
        public void Floor_Pace_Is_Six_Weeks()
        {
            Assert.AreEqual(500.0 / 6, Week(Row(), 200), 1e-6);
        }

        [TestMethod]
        public void Ceiling_Pace_Is_Four_Weeks()
        {
            Assert.AreEqual(500.0 / 4, Week(Row(), 400), 1e-6);
        }

        [TestMethod]
        public void Past_The_Ceiling_Earns_Nothing_More()
        {
            Assert.AreEqual(500.0 / 4, Week(Row(), 1000), 1e-6, "4 weeks is the fastest anyone can go");
        }

        [TestMethod]
        public void Between_Is_Half_Rate()
        {
            var r = Row();
            Assert.AreEqual(TriuneEarning.Rate1(r) / 2, TriuneEarning.Rate2(r), 1e-12, "200/400 with 6/4 weeks = exactly half");
            Assert.AreEqual(500.0 / 6 + (300 - 200) * 168 * TriuneEarning.Rate2(r), Week(r, 300), 1e-6);
        }

        [TestMethod]
        public void KillValue_Bands()
        {
            var r = Row();
            Assert.AreEqual(TriuneEarning.Rate1(r), TriuneEarning.KillValue(r, 0));
            Assert.AreEqual(TriuneEarning.Rate1(r), TriuneEarning.KillValue(r, 33_599));
            Assert.AreEqual(TriuneEarning.Rate2(r), TriuneEarning.KillValue(r, 33_600));
            Assert.AreEqual(TriuneEarning.Rate2(r), TriuneEarning.KillValue(r, 67_199));
            Assert.AreEqual(0.0, TriuneEarning.KillValue(r, 67_200));
            Assert.AreEqual(0.0, TriuneEarning.KillValue(null, 0));
        }

        [TestMethod]
        public void TierCap_Sums_Points_From_T15()
        {
            var cfg = TriuneEarning.Defaults();
            Assert.AreEqual(500, TriuneEarning.TierCap(cfg, 15));
            Assert.AreEqual(1000, TriuneEarning.TierCap(cfg, 16));
            Assert.AreEqual(5500, TriuneEarning.TierCap(cfg, 25));
            Assert.AreEqual(0, TriuneEarning.TierCap(cfg, 14));
        }

        [TestMethod]
        public void Normalize_Fixes_Bad_Rows()
        {
            var cfg = new TriuneEarning.Config();
            cfg.Tiers.Add(new TriuneTierRow { Tier = 15, FloorKph = 300, CeilingKph = 100, SlowWeeks = 3, FastWeeks = 8 });
            cfg.Tiers.Add(new TriuneTierRow { Tier = 99 });
            cfg = TriuneEarning.Normalize(cfg);
            var r = cfg.Tiers.Single(t => t.Tier == 15);
            Assert.IsTrue(r.CeilingKph > r.FloorKph, "ceiling above the floor");
            Assert.IsTrue(r.FastWeeks <= r.SlowWeeks, "fast never slower than slow");
            Assert.AreEqual(11, cfg.Tiers.Count, "T15..T25 only, each once");
        }

        // -- the weekly reset --

        private static TimeZoneInfo Eastern()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
        }

        [TestMethod]
        public void Week_Starts_Friday_Noon_Eastern_Summer_And_Winter()
        {
            var tz = Eastern();
            // Sun 2026-11-01 03:44 UTC (Sat 2026-10-31 23:44 EDT) -> the week began Fri 2026-10-30 12:00 EDT = 16:00 UTC
            Assert.AreEqual(new DateTime(2026, 10, 30, 16, 0, 0, DateTimeKind.Utc),
                TriuneEarning.WeekStartUtc(new DateTime(2026, 11, 1, 3, 44, 0, DateTimeKind.Utc), DayOfWeek.Friday, 12, tz));
            // Wed 2026-12-09 -> Fri 2026-12-04 12:00 EST = 17:00 UTC
            Assert.AreEqual(new DateTime(2026, 12, 4, 17, 0, 0, DateTimeKind.Utc),
                TriuneEarning.WeekStartUtc(new DateTime(2026, 12, 9, 12, 0, 0, DateTimeKind.Utc), DayOfWeek.Friday, 12, tz));
        }

        [TestMethod]
        public void Next_Week_Keeps_Local_Noon_Across_Daylight_Saving()
        {
            // week began Fri 2026-10-30 12:00 EDT; clocks go back Sun 11-01 -> next reset Fri 11-06 12:00 EST = 17:00 UTC (not 16:00)
            Assert.AreEqual(new DateTime(2026, 11, 6, 17, 0, 0, DateTimeKind.Utc),
                TriuneEarning.NextWeekStartUtc(new DateTime(2026, 11, 2, 4, 0, 0, DateTimeKind.Utc), DayOfWeek.Friday, 12, Eastern()));
        }

        [TestMethod]
        public void Week_Turns_Exactly_At_The_Reset()
        {
            var tz = Eastern();
            var reset = new DateTime(2026, 12, 4, 17, 0, 0, DateTimeKind.Utc);   // Fri 12:00 EST
            Assert.AreEqual(reset.AddDays(-7), TriuneEarning.WeekStartUtc(reset.AddSeconds(-1), DayOfWeek.Friday, 12, tz));
            Assert.AreEqual(reset, TriuneEarning.WeekStartUtc(reset, DayOfWeek.Friday, 12, tz));
        }

        [TestMethod]
        public void Week_Across_Year_End_And_Utc()
        {
            Assert.AreEqual(new DateTime(2027, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                TriuneEarning.WeekStartUtc(new DateTime(2027, 1, 3, 0, 0, 0, DateTimeKind.Utc), DayOfWeek.Friday, 12, TimeZoneInfo.Utc));
            Assert.AreEqual(new DateTime(2026, 12, 25, 12, 0, 0, DateTimeKind.Utc),
                TriuneEarning.WeekStartUtc(new DateTime(2027, 1, 1, 11, 59, 0, DateTimeKind.Utc), DayOfWeek.Friday, 12, TimeZoneInfo.Utc));
        }

        [TestMethod]
        public void Reset_Hour_In_A_Spring_Forward_Gap_Moves_To_The_Next_Real_Hour()
        {
            // Sun 2027-03-14 02:00 does not exist in US Eastern (clocks jump 02:00 -> 03:00 EDT = 07:00 UTC)
            Assert.AreEqual(new DateTime(2027, 3, 14, 7, 0, 0, DateTimeKind.Utc),
                TriuneEarning.WeekStartUtc(new DateTime(2027, 3, 15, 0, 0, 0, DateTimeKind.Utc), DayOfWeek.Sunday, 2, Eastern()));
        }
        // -- school gems --

        /// <summary>Expected gems for one school over a steady week, kill by kill.</summary>
        private static double GemWeek(TriuneTierRow r, double kph)
        {
            var kills = (int)(kph * TriuneEarning.HoursPerWeek);
            var sum = 0.0;
            for (var k = 0; k < kills; k++)
                sum += TriuneEarning.GemChance(r, k);
            return sum;
        }

        [TestMethod]
        public void Gems_Four_Weeks_At_The_Floor_TwoAndAHalf_At_The_Ceiling()
        {
            var r = Row();
            Assert.AreEqual(4, r.GemSlowWeeks); Assert.AreEqual(2.5, r.GemFastWeeks);
            Assert.AreEqual(500.0 / 4, GemWeek(r, 200), 1e-6);
            Assert.AreEqual(500.0 / 2.5, GemWeek(r, 400), 1e-6);
            Assert.AreEqual(500.0 / 2.5, GemWeek(r, 900), 1e-6, "nothing past the ceiling");
            Assert.AreEqual(269, Math.Round(1 / TriuneEarning.GemChance1(r)));
            Assert.AreEqual(448, Math.Round(1 / TriuneEarning.GemChance2(r)));
        }

        [TestMethod]
        public void Gem_Ceiling_Is_Base_Plus_Triune_Points_Per_Tier()
        {
            var cfg = TriuneEarning.Defaults();
            Assert.IsFalse(cfg.GemsEnabled, "ships OFF");
            Assert.AreEqual(4000, cfg.GemBaseCap);
            Assert.AreEqual(4500, TriuneEarning.GemCeiling(cfg, 15));
            Assert.AreEqual(5000, TriuneEarning.GemCeiling(cfg, 16));
            Assert.AreEqual(9500, TriuneEarning.GemCeiling(cfg, 25));
        }

        [TestMethod]
        public void Gem_Qualifies_Only_Bought_To_The_Base_And_Under_The_Ceiling()
        {
            Assert.IsFalse(SchoolGems.Qualifies(3999, 4400, 4000, 4500), "under the bought base");
            Assert.IsTrue(SchoolGems.Qualifies(4000, 4000, 4000, 4500));
            Assert.IsTrue(SchoolGems.Qualifies(4000, 4499, 4000, 4500));
            Assert.IsFalse(SchoolGems.Qualifies(4000, 4500, 4000, 4500), "full for the tier");
        }

        [TestMethod]
        public void Gem_Pickup_Room_Counts_Carried_Gems()
        {
            Assert.AreEqual(1, SchoolGems.PickupRoom(4500, 4490, new[] { (15, 4500L, 9) }));
            Assert.AreEqual(0, SchoolGems.PickupRoom(4500, 4490, new[] { (15, 4500L, 10) }), "490 + 10 carried = the 11th crumbles");
            Assert.AreEqual(-5, SchoolGems.PickupRoom(4500, 4505, Array.Empty<(int, long, int)>()));
        }

        [TestMethod]
        public void Gem_Pickup_Spent_Lower_Tier_Gems_Take_No_Room()
        {
            // T15 ceiling 4500, T16 ceiling 5000
            Assert.AreEqual(10, SchoolGems.PickupRoom(5000, 4990, new[] { (15, 4500L, 20) }), "spent T15 gems must not make a fresh T16 gem crumble");
            Assert.AreEqual(1, SchoolGems.PickupRoom(5000, 4990, new[] { (15, 4500L, 20), (16, 5000L, 9) }));
            Assert.AreEqual(0, SchoolGems.PickupRoom(5000, 4990, new[] { (16, 5000L, 10) }));
            // round 2 (2026-11-02): only the part of a T15 pile below its ceiling counts - 4,450 + 50 usable T15 = 4,500, then
            // 450 T16 = 4,950: room 50, not 5000 - 4450 - 100 - 450 = 0
            Assert.AreEqual(50, SchoolGems.PickupRoom(5000, 4450, new[] { (16, 5000L, 450), (15, 4500L, 100) }), "order in = any; used lowest tier first");
        }

        [TestMethod]
        public void Gem_Schools_Map_To_The_Four_Plus_One_Gems()
        {
            CollectionAssert.AreEqual(new uint[] { 777700052, 777700057, 777700062, 777700067 }, SchoolGems.Schools.Select(s => s.GemWcid).ToArray());
            Assert.IsNull(SchoolGems.ForGem(777700053), "the old +10 gem is not an earned gem");
        }
        // -- review round 1 (2026-11-01) --

        [TestMethod]
        public void Normalize_Keeps_One_Row_Per_Tier_The_First_Given()
        {
            var cfg = new TriuneEarning.Config();
            cfg.Tiers.Add(new TriuneTierRow { Tier = 16, Points = 300 });
            cfg.Tiers.Add(new TriuneTierRow { Tier = 16, Points = 900 });
            cfg = TriuneEarning.Normalize(cfg);
            Assert.AreEqual(1, cfg.Tiers.Count(r => r.Tier == 16));
            Assert.AreEqual(300, cfg.Tiers.Single(r => r.Tier == 16).Points);
        }

        [TestMethod]
        public void Normalize_Rejects_Infinite_Tiny_And_Huge_Values()
        {
            var cfg = new TriuneEarning.Config { GemBaseCap = -1 };
            cfg.Tiers.Add(new TriuneTierRow { Tier = 15, FloorKph = double.PositiveInfinity, SlowWeeks = 1e-300, Points = int.MaxValue, GemSlowWeeks = double.NaN });
            cfg = TriuneEarning.Normalize(cfg);
            var r = cfg.Tiers.Single(x => x.Tier == 15);
            Assert.AreEqual(200, r.FloorKph, "Infinity -> default");
            Assert.AreEqual(6, r.SlowWeeks, "a tiny divisor -> default");
            Assert.AreEqual(TriuneEarning.MaxPoints, r.Points, "points capped");
            Assert.AreEqual(4, r.GemSlowWeeks, "NaN -> default");
            Assert.AreEqual(0, cfg.GemBaseCap, "a negative base is never below 0");
            Assert.IsTrue(double.IsFinite(TriuneEarning.Rate1(r)) && TriuneEarning.Rate1(r) < 1);
        }

        [TestMethod]
        public void IsNewWeek_Only_After_A_Full_Week_So_No_Week_Is_Ever_Shorter()
        {
            const long h = 3600, S = 1_000_000;
            Assert.IsTrue(TriuneEarning.IsNewWeek(0, S), "nothing stored");
            Assert.IsTrue(TriuneEarning.IsNewWeek(S, S + 168 * h), "the next week");
            Assert.IsTrue(TriuneEarning.IsNewWeek(S, S + 167 * h), "the spring-forward week is an hour short");
            Assert.IsTrue(TriuneEarning.IsNewWeek(S, S + 169 * h), "the fall-back week is an hour long");
            Assert.IsFalse(TriuneEarning.IsNewWeek(S, S), "the same week");
            // an admin moves Friday's reset to Tuesday: the first Tuesday (S + 96 h) is no new week, the next one (S + 264 h) is
            Assert.IsFalse(TriuneEarning.IsNewWeek(S, S + 96 * h), "moved later: no second band 4 days in");
            Assert.IsTrue(TriuneEarning.IsNewWeek(S, S + 264 * h));
            // moved two days earlier (Wednesday): S + 120 h is still the same week, S + 288 h is the next
            Assert.IsFalse(TriuneEarning.IsNewWeek(S, S - 48 * h), "moved earlier: the count runs on");
            Assert.IsFalse(TriuneEarning.IsNewWeek(S, S + 120 * h), "moved earlier: no second band 5 days in");
            Assert.IsTrue(TriuneEarning.IsNewWeek(S, S + 288 * h));
        }

        [TestMethod]
        public void Reset_Hour_In_A_Fall_Back_Overlap_Turns_At_The_First_One_And_Never_In_The_Future()
        {
            var tz = Eastern();
            // Sun 2026-11-01 01:00 happens twice in US Eastern (05:00 UTC EDT, then 06:00 UTC EST): the week turns at 05:00 UTC
            var at = new DateTime(2026, 11, 1, 5, 30, 0, DateTimeKind.Utc);
            var start = TriuneEarning.WeekStartUtc(at, DayOfWeek.Sunday, 1, tz);
            Assert.AreEqual(new DateTime(2026, 11, 1, 5, 0, 0, DateTimeKind.Utc), start);
            Assert.IsTrue(start <= at);
        }
        // -- review round 2 (2026-11-02) --

        [TestMethod]
        public void A_Stored_Week_A_Full_Week_In_The_Future_Starts_Again()
        {
            const long h = 3600, S = 1_000_000_000;
            Assert.IsTrue(TriuneEarning.IsNewWeek(S + 200 * h, S), "a clock that ran fast left a start in the future");
            Assert.IsFalse(TriuneEarning.IsNewWeek(S + 48 * h, S), "a reset moved earlier is still the same week");
        }

        [TestMethod]
        public void A_Fractional_Band_Pays_Exactly_Its_Share()
        {
            // floor 0.01 kph = 1.68 kills a week at full pace, ceiling 0.02 = 1.68 more: the whole week pays exactly Points / FastWeeks
            var r = new TriuneTierRow { Tier = 15, FloorKph = 0.01, CeilingKph = 0.02 };
            var sum = 0.0;
            for (var k = 0; k < 10; k++)
                sum += TriuneEarning.KillValue(r, k);
            Assert.AreEqual(500.0 / 4, sum, 1e-9);
            Assert.AreEqual(TriuneEarning.Rate1(r) * 0.68 + TriuneEarning.Rate2(r) * 0.32, TriuneEarning.KillValue(r, 1), 1e-6, "the kill across the edge");
        }

        [TestMethod]
        public void Week_Start_In_A_45_Minute_Zone_Overlap_Is_This_Week()
        {
            TimeZoneInfo tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById("Chatham Islands Standard Time"); }
            catch (TimeZoneNotFoundException) { try { tz = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Chatham"); } catch (TimeZoneNotFoundException) { Assert.Inconclusive("no Chatham zone here"); return; } }
            // Chatham falls back Sun 2026-04-05 03:45 -> 02:45 local; a Sunday 03:00 reset first happens at 2026-04-04 13:15 UTC.
            // At 14:05 UTC the wall clock reads 02:50 again ("before" 03:00) - the week has still begun.
            var at = new DateTime(2026, 4, 4, 14, 5, 0, DateTimeKind.Utc);
            var start = TriuneEarning.WeekStartUtc(at, DayOfWeek.Sunday, 3, tz);
            Assert.AreEqual(new DateTime(2026, 4, 4, 13, 15, 0, DateTimeKind.Utc), start);
            Assert.IsTrue(TriuneEarning.NextWeekStartUtc(at, DayOfWeek.Sunday, 3, tz) > at, "the next reset is never in the past");
        }
    }
}
