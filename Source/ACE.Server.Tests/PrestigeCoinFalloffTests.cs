using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Server.Managers;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Managers.ZoneScaling;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Prestige Coin falloff (2026-11-01; steps per coin hour 2026-11-17): of a character's T11+ zone coin kills in one coin
    /// hour, kills 1-375 pay in full, then the pay halves every 75 kills (376-450 half ... 676-750 a thirty-second) and 751 on
    /// pay nothing (the zc_pcoin_falloff_* settings). The pure rules (TierRewardScale.FalloffBands / FalloffKill / FalloffHour / FalloffBandAt / DescribeFalloff),
    /// the dial cleaning and zone override (CleanFalloff / FalloffFor), the shipped defaults and the two Zone Control stats.
    /// No DB, no world.
    /// </summary>
    [TestClass]
    public class PrestigeCoinFalloffTests
    {
        /// <summary>The shipped schedule: full to 375, then x0.5 every 75 kills, cap 750.</summary>
        private static readonly TierRewardScale.FalloffDials Shipped = new(375, 0.5, 75, 750);

        /// <summary>
        /// <paramref name="kills"/> kills in one coin hour from a fresh hour (count 0), each worth <paramref name="credit"/>.
        /// Returns the total credit paid.
        /// </summary>
        private static double HourTotal(int kills, TierRewardScale.FalloffDials dials, double credit = 1.0)
        {
            double count = 0, total = 0;
            for (var k = 0; k < kills; k++)
            {
                var (paid, next, _) = TierRewardScale.FalloffKill(count, credit, dials);
                count = next;
                total += paid;
            }
            return total;
        }

        /// <summary>What the <paramref name="n"/>th full-value kill of the hour pays under the shipped schedule.</summary>
        private static double KillPays(int n) => TierRewardScale.FalloffKill(n - 1, 1.0, Shipped).Paid;

        /// <summary>A zone Default row authoring exactly <paramref name="values"/> (case-insensitive stat names).</summary>
        private static EvaluatedProfile Zone(Dictionary<string, double> values)
            => new EvaluatedProfile("test zone", 15, ZoneVariant.Minion,
                new Dictionary<string, double>(values, StringComparer.OrdinalIgnoreCase));

        // -- shipped defaults and registration --

        /// <summary>The shipped settings are the requested schedule, and the server dials read from them equal it.</summary>
        [TestMethod]
        public void Defaults_AreTheRequestedSteps()
        {
            Assert.AreEqual(375.0, ServerConfig.zc_pcoin_falloff_kills_per_hour.Default, 1e-9);
            Assert.AreEqual(0.5, ServerConfig.zc_pcoin_falloff_mult.Default, 1e-9);
            Assert.AreEqual(75.0, ServerConfig.zc_pcoin_falloff_step_kills.Default, 1e-9);
            Assert.AreEqual(750.0, ServerConfig.zc_pcoin_falloff_cap_kills_per_hour.Default, 1e-9);
            Assert.AreEqual(Shipped, TierRewardScale.ServerFalloff());
        }

        /// <summary>Both Zone Control stats are registered once, at the end of the tail they were appended to (after SlayerPoolShare), and are not weapon cards.</summary>
        [TestMethod]
        public void Stats_AreRegisteredOnceAndAppended()
        {
            var stats = new[] { ZoneStat.PcoinFalloffKillsPerHour, ZoneStat.PcoinFalloffMult };
            foreach (var stat in stats)
            {
                Assert.IsTrue(ZoneStat.IsKnownStat(stat), $"/zonecontrol ... set {stat} must be accepted");
                Assert.AreEqual(1, ZoneStat.All.Count(s => string.Equals(s, stat, StringComparison.OrdinalIgnoreCase)), stat);
                Assert.IsFalse(ZoneStat.IsWeaponCardChance(stat), "not a weapon card");
            }

            var start = Array.IndexOf(ZoneStat.All, stats[0]);
            Assert.IsTrue(start > 0);
            Assert.AreEqual(ZoneStat.SlayerPoolShare, ZoneStat.All[start - 1], "appended after the previous tail");
            CollectionAssert.AreEqual(stats, ZoneStat.All.Skip(start).Take(stats.Length).ToArray());
        }

        // -- the requested schedule --

        /// <summary>An hour's total: full to 375, then each 75-kill step adds half what the one before did, and 750 or 9,999 kills pay the same 447.65625.</summary>
        [TestMethod]
        public void HourTotals_MatchTheRequestedSteps()
        {
            Assert.AreEqual(100.0, HourTotal(100, Shipped), 1e-6);
            Assert.AreEqual(375.0, HourTotal(375, Shipped), 1e-6, "full up to and including 375");
            Assert.AreEqual(375.5, HourTotal(376, Shipped), 1e-6);
            Assert.AreEqual(412.5, HourTotal(450, Shipped), 1e-6, "+ 75 x 0.5");
            Assert.AreEqual(412.75, HourTotal(451, Shipped), 1e-6);
            Assert.AreEqual(431.25, HourTotal(525, Shipped), 1e-6, "+ 75 x 0.25");
            Assert.AreEqual(440.625, HourTotal(600, Shipped), 1e-6, "+ 75 x 0.125");
            Assert.AreEqual(445.3125, HourTotal(675, Shipped), 1e-6, "+ 75 x 0.0625");
            Assert.AreEqual(447.65625, HourTotal(750, Shipped), 1e-6, "+ 75 x 0.03125");
            Assert.AreEqual(447.65625, HourTotal(751, Shipped), 1e-6, "hard stop");
            Assert.AreEqual(447.65625, HourTotal(9999, Shipped), 1e-6, "751 to 9999 pay the same as 750");
        }

        /// <summary>Each kill pays the share of its step: the 375th full, then half every 75 kills, and the 751st nothing.</summary>
        [TestMethod]
        public void EachKill_PaysItsStep()
        {
            Assert.AreEqual(1.0, KillPays(1), 1e-12);
            Assert.AreEqual(1.0, KillPays(375), 1e-12);
            Assert.AreEqual(0.5, KillPays(376), 1e-12);
            Assert.AreEqual(0.5, KillPays(450), 1e-12);
            Assert.AreEqual(0.25, KillPays(451), 1e-12);
            Assert.AreEqual(0.25, KillPays(525), 1e-12);
            Assert.AreEqual(0.125, KillPays(526), 1e-12);
            Assert.AreEqual(0.125, KillPays(600), 1e-12);
            Assert.AreEqual(0.0625, KillPays(601), 1e-12);
            Assert.AreEqual(0.0625, KillPays(675), 1e-12);
            Assert.AreEqual(0.03125, KillPays(676), 1e-12);
            Assert.AreEqual(0.03125, KillPays(750), 1e-12);
            Assert.AreEqual(0.0, KillPays(751), 1e-12);
            Assert.AreEqual(0.0, KillPays(9999), 1e-12);

            Assert.IsFalse(TierRewardScale.FalloffKill(374, 1, Shipped).Reduced, "the 375th is full");
            Assert.IsTrue(TierRewardScale.FalloffKill(375, 1, Shipped).Reduced);
            Assert.IsTrue(TierRewardScale.FalloffKill(800, 1, Shipped).Reduced, "a kill past the cap is cut to nothing");
            Assert.AreEqual(801.0, TierRewardScale.FalloffKill(800, 1, Shipped).Count, 1e-9, "and is still counted");
        }

        /// <summary>A below-tier kill uses only its tier-dip credit of the hour and is paid on that credit.</summary>
        [TestMethod]
        public void BelowTierKills_CountAsTheirCredit()
        {
            Assert.AreEqual(375.0, HourTotal(750, Shipped, 0.5), 1e-6, "750 half-credit kills = 375 full-value kills, all full");
            Assert.AreEqual(412.5, HourTotal(900, Shipped, 0.5), 1e-6, "900 = 450 full-value kills");
            Assert.AreEqual(447.65625, HourTotal(1500, Shipped, 0.5), 1e-6, "1500 = 750 full-value kills, the cap");

            var (paid, count, reduced) = TierRewardScale.FalloffKill(374.5, 0.5, Shipped);
            Assert.AreEqual(0.5, paid, 1e-9);
            Assert.AreEqual(375.0, count, 1e-9);
            Assert.IsFalse(reduced);
        }

        /// <summary>A kill that straddles a band limit pays each part at its own band's share.</summary>
        [TestMethod]
        public void SplitKill_EachPartPaysItsBand()
        {
            var (paid, count, reduced) = TierRewardScale.FalloffKill(374.5, 1, Shipped);
            Assert.AreEqual(0.75, paid, 1e-9, "0.5 full + 0.5 x 0.5");
            Assert.AreEqual(375.5, count, 1e-9);
            Assert.IsTrue(reduced);

            Assert.AreEqual(0.3125, TierRewardScale.FalloffKill(449.75, 1, Shipped).Paid, 1e-9, "0.25 x 0.5 + 0.75 x 0.25");
            Assert.AreEqual(0.015625, TierRewardScale.FalloffKill(749.5, 1, Shipped).Paid, 1e-12, "0.5 x 0.03125, the rest past the cap");
        }

        // -- the coin hour --

        /// <summary>The coin hour: kept for 60 minutes from its start, then the next kill starts a fresh one.</summary>
        [TestMethod]
        public void CoinHour_KeepsThenStartsFresh()
        {
            const double now = 1_000_000;

            Assert.AreEqual((0.0, now, true), TierRewardScale.FalloffHour(null, null, now), "nothing saved");
            Assert.AreEqual((600.0, now - 3599, false), TierRewardScale.FalloffHour(600, now - 3599, now), "a second left");
            Assert.AreEqual((0.0, now, true), TierRewardScale.FalloffHour(600, now - 3600, now), "60 minutes on: a new hour");
            Assert.AreEqual((0.0, now, true), TierRewardScale.FalloffHour(600, now - 86400, now), "a day offline");
            Assert.AreEqual((0.0, now, true), TierRewardScale.FalloffHour(600, double.NaN, now), "a bad start");
            Assert.AreEqual((0.0, now - 10, false), TierRewardScale.FalloffHour(double.NaN, now - 10, now), "a bad count reads as 0");
            Assert.AreEqual((0.0, now - 10, false), TierRewardScale.FalloffHour(null, now - 10, now));
            Assert.AreEqual((50.0, now, false), TierRewardScale.FalloffHour(50, now + 500, now), "a clock that went back keeps the count");

            // a capped hour, then the hour runs out: the next kill pays in full again
            var (count, _, fresh) = TierRewardScale.FalloffHour(9999, now - 3600, now);
            Assert.IsTrue(fresh);
            Assert.AreEqual(1.0, TierRewardScale.FalloffKill(count, 1, Shipped).Paid, 1e-9);
        }

        // -- bands, off and bad values --

        /// <summary>The band list: the shipped steps, one step (step 0), no cap, a cap under the full-rate limit, a share of 1, and off.</summary>
        [TestMethod]
        public void Bands_FollowTheSteps()
        {
            var inf = double.PositiveInfinity;
            CollectionAssert.AreEqual(new[] { (375.0, 1.0), (450.0, 0.5), (525.0, 0.25), (600.0, 0.125), (675.0, 0.0625), (750.0, 0.03125), (inf, 0.0) },
                TierRewardScale.FalloffBands(Shipped).ToArray());

            CollectionAssert.AreEqual(new[] { (375.0, 1.0), (750.0, 0.5), (inf, 0.0) },
                TierRewardScale.FalloffBands(Shipped with { StepKills = 0 }).ToArray(), "step 0 = half all the way to the cap");

            CollectionAssert.AreEqual(new[] { (375.0, 1.0), (inf, 0.5) },
                TierRewardScale.FalloffBands(Shipped with { StepKills = 0, CapKillsPerHour = 0 }).ToArray(), "and no cap = half for the rest of the hour");

            CollectionAssert.AreEqual(new[] { (300.0, 1.0), (inf, 0.0) },
                TierRewardScale.FalloffBands(Shipped with { CapKillsPerHour = 300 }).ToArray(), "a cap under the full-rate limit stops everything there");

            CollectionAssert.AreEqual(new[] { (375.0, 1.0), (450.0, 1.0), (525.0, 1.0), (600.0, 1.0), (675.0, 1.0), (750.0, 1.0), (inf, 0.0) },
                TierRewardScale.FalloffBands(Shipped with { Mult = 1 }).ToArray(), "a share of 1 = full pay up to a hard stop");

            CollectionAssert.AreEqual(new[] { (inf, 1.0) }, TierRewardScale.FalloffBands(Shipped with { KillsPerHour = 0 }).ToArray(), "off");

            // no cap: the steps run on to MaxFalloffSteps, and the last one to the end of the hour
            var endless = TierRewardScale.FalloffBands(Shipped with { CapKillsPerHour = 0 });
            Assert.AreEqual(TierRewardScale.MaxFalloffSteps + 1, endless.Count);
            Assert.AreEqual((inf, Math.Pow(0.5, TierRewardScale.MaxFalloffSteps)), endless[^1]);

            // a cap past MaxFalloffSteps steps: the last step runs to the cap, then nothing
            var far = TierRewardScale.FalloffBands(Shipped with { CapKillsPerHour = 1_000_000 });
            Assert.AreEqual(TierRewardScale.MaxFalloffSteps + 2, far.Count);
            Assert.AreEqual((1_000_000.0, Math.Pow(0.5, TierRewardScale.MaxFalloffSteps)), far[^2]);
            Assert.AreEqual((inf, 0.0), far[^1]);
        }

        /// <summary>Off (full-rate limit 0): every kill pays in full and the hour's count is left as it was.</summary>
        [TestMethod]
        public void Off_PaysInFullAndLeavesTheCount()
        {
            var off = Shipped with { KillsPerHour = 0 };
            Assert.IsFalse(off.On);
            Assert.AreEqual((1.0, 700.0, false), TierRewardScale.FalloffKill(700, 1, off));
            Assert.AreEqual(1.0, HourTotal(9999, off) / 9999, 1e-9);
            Assert.IsFalse((Shipped with { Scale = 0 }).On, "a zero stretch is off too");
        }

        /// <summary>A bad credit pays nothing and counts nothing; a bad count reads as 0.</summary>
        [TestMethod]
        public void BadInputs_NeverMintCoins()
        {
            Assert.AreEqual((0.0, 10.0, false), TierRewardScale.FalloffKill(10, double.NaN, Shipped));
            Assert.AreEqual((0.0, 10.0, false), TierRewardScale.FalloffKill(10, -1, Shipped));
            Assert.AreEqual((0.0, 10.0, false), TierRewardScale.FalloffKill(10, double.PositiveInfinity, Shipped));
            Assert.AreEqual((1.0, 1.0, false), TierRewardScale.FalloffKill(double.NaN, 1, Shipped));
            Assert.AreEqual((1.0, 1.0, false), TierRewardScale.FalloffKill(-50, 1, Shipped));
        }

        /// <summary>Each limit and the step are held to 0..1,000,000 and the share to 0..1; a value that is not a number takes the declared default.</summary>
        [TestMethod]
        public void CleanFalloff_HoldsEachDialToItsRange()
        {
            Assert.AreEqual(Shipped, TierRewardScale.CleanFalloff(double.NaN, double.NaN, double.NaN, double.NaN));
            Assert.AreEqual(Shipped, TierRewardScale.CleanFalloff(double.PositiveInfinity, 0.5, 75, 750), "infinity is not a number here");

            var wild = TierRewardScale.CleanFalloff(-5, 3, 2_000_000, -1);
            Assert.AreEqual(0.0, wild.KillsPerHour);
            Assert.AreEqual(1.0, wild.Mult);
            Assert.AreEqual(1_000_000.0, wild.StepKills);
            Assert.AreEqual(0.0, wild.CapKillsPerHour);
            Assert.AreEqual(1.0, wild.Scale);
        }

        // -- zones --

        /// <summary>A zone row: unset = server; its full-rate limit stretches the step and the cap; its mult replaces the step share; 0 = off there.</summary>
        [TestMethod]
        public void FalloffFor_ZoneStretchesTheSteps()
        {
            Assert.AreEqual(Shipped, TierRewardScale.FalloffFor(null, Shipped));
            Assert.AreEqual(Shipped, TierRewardScale.FalloffFor(Zone(new Dictionary<string, double>()), Shipped), "nothing authored");

            var at500 = TierRewardScale.FalloffFor(Zone(new Dictionary<string, double> { [ZoneStat.PcoinFalloffKillsPerHour] = 500 }), Shipped);
            Assert.AreEqual(375.0, at500.KillsPerHour, "limits stay in server kills...");
            Assert.AreEqual(500.0 / 375.0, at500.Scale, 1e-12, "...stretched by 500 / 375");
            Assert.AreEqual(500.0, HourTotal(500, at500), 1e-6, "500 full kills there");
            Assert.AreEqual(596.875, HourTotal(1000, at500), 1e-6, "447.65625 x 4/3 at its cap of 1000");
            Assert.AreEqual(596.875, HourTotal(5000, at500), 1e-6);

            var quarter = TierRewardScale.FalloffFor(Zone(new Dictionary<string, double> { [ZoneStat.PcoinFalloffMult] = 0.25 }), Shipped);
            Assert.AreEqual(Shipped with { Mult = 0.25 }, quarter, "only the step share changes");

            var off = TierRewardScale.FalloffFor(Zone(new Dictionary<string, double> { [ZoneStat.PcoinFalloffKillsPerHour] = 0 }), Shipped);
            Assert.IsFalse(off.On, "0 in a zone = no falloff for kills there");

            var bad = TierRewardScale.FalloffFor(Zone(new Dictionary<string, double>
            {
                [ZoneStat.PcoinFalloffKillsPerHour] = double.NaN,
                [ZoneStat.PcoinFalloffMult] = 3,
            }), Shipped);
            Assert.AreEqual(1.0, bad.Scale, "NaN keeps the server limit");
            Assert.AreEqual(1.0, bad.Mult, "held to 0..1");
        }

        /// <summary>With the server falloff off, a zone that turns it on is stretched from the declared default (375).</summary>
        [TestMethod]
        public void FalloffFor_ServerOff_ZoneStretchesFromTheDefault()
        {
            var serverOff = Shipped with { KillsPerHour = 0 };
            var at500 = TierRewardScale.FalloffFor(Zone(new Dictionary<string, double> { [ZoneStat.PcoinFalloffKillsPerHour] = 500 }), serverOff);
            Assert.IsTrue(at500.On);
            Assert.AreEqual(375.0, at500.KillsPerHour);
            Assert.AreEqual(500.0 / 375.0, at500.Scale, 1e-12);

            Assert.IsFalse(TierRewardScale.FalloffFor(Zone(new Dictionary<string, double>()), serverOff).On, "a zone that does not turn it on stays off");
        }

        /// <summary>The hour is one count in server kills, so hopping to a zone with a higher limit does not hand out a second allowance.</summary>
        [TestMethod]
        public void ZoneHopping_SharesOneHour()
        {
            var at500 = TierRewardScale.FalloffFor(Zone(new Dictionary<string, double> { [ZoneStat.PcoinFalloffKillsPerHour] = 500 }), Shipped);

            double count = 0;
            for (var k = 0; k < 500; k++)
                count = TierRewardScale.FalloffKill(count, 1, at500).Count;
            Assert.AreEqual(375.0, count, 1e-9, "500 kills there use the whole full-rate band");

            Assert.AreEqual(0.5, TierRewardScale.FalloffKill(count, 1, Shipped).Paid, 1e-9, "the next kill in a 375 zone pays half");
        }

        // -- what the player sees --

        /// <summary>The band the next kill starts in, and where it runs from and to.</summary>
        [TestMethod]
        public void BandAt_NamesTheNextKillsBand()
        {
            Assert.AreEqual((1.0, 0.0, 375.0), TierRewardScale.FalloffBandAt(0, Shipped));
            Assert.AreEqual((1.0, 0.0, 375.0), TierRewardScale.FalloffBandAt(374, Shipped));
            Assert.AreEqual((0.5, 375.0, 450.0), TierRewardScale.FalloffBandAt(375, Shipped), "after the 375th kill");
            Assert.AreEqual((0.03125, 675.0, 750.0), TierRewardScale.FalloffBandAt(700, Shipped));
            Assert.AreEqual((0.0, 750.0, double.PositiveInfinity), TierRewardScale.FalloffBandAt(750, Shipped));
            Assert.AreEqual((0.0, 750.0, double.PositiveInfinity), TierRewardScale.FalloffBandAt(9999, Shipped));
            Assert.AreEqual((1.0, 0.0, 375.0), TierRewardScale.FalloffBandAt(double.NaN, Shipped));
        }

        /// <summary>The schedule in a player's words, with a zone's stretched limits, told as its rule when it has many steps, and plain ASCII.</summary>
        [TestMethod]
        public void Describe_ReadsTheSchedule()
        {
            Assert.AreEqual("full for the first 375 kills, then 50 pct up to 450, 25 pct up to 525, 12.5 pct up to 600, 6.25 pct up to 675, 3.125 pct up to 750 and none past 750",
                TierRewardScale.DescribeFalloff(Shipped));
            Assert.AreEqual("full for the first 500 kills, then 50 pct up to 600, 25 pct up to 700, 12.5 pct up to 800, 6.25 pct up to 900, 3.125 pct up to 1000 and none past 1000",
                TierRewardScale.DescribeFalloff(Shipped with { Scale = 500.0 / 375.0 }));
            Assert.AreEqual("full for the first 375 kills, then 50 pct up to 750 and none past 750", TierRewardScale.DescribeFalloff(Shipped with { StepKills = 0 }));
            Assert.AreEqual("full for the first 375 kills, then 50 pct past 375", TierRewardScale.DescribeFalloff(Shipped with { StepKills = 0, CapKillsPerHour = 0 }));
            Assert.AreEqual("full for the first 375 kills, then 50 pct, and 50 pct of that again every 75 kills",
                TierRewardScale.DescribeFalloff(Shipped with { CapKillsPerHour = 0 }), "too many steps to list: told as the rule");
            Assert.AreEqual("full for the first 375 kills, then 50 pct, and 50 pct of that again every 75 kills, and none past 1000000",
                TierRewardScale.DescribeFalloff(Shipped with { CapKillsPerHour = 1_000_000 }));
            Assert.AreEqual("full for every kill", TierRewardScale.DescribeFalloff(Shipped with { KillsPerHour = 0 }));

            Assert.IsTrue(TierRewardScale.DescribeFalloff(Shipped).All(ch => ch < 128), "client text must be 7-bit ASCII");
        }
    }
}
