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
    /// Prestige Coin falloff (2026-11-01): coins per T11+ zone kill stay as they are up to a pace of
    /// zc_pcoin_falloff_kills_per_hour (375) full-value kills an hour, and every kill past that pace pays zc_pcoin_falloff_mult
    /// (0.5) of its coins. The pure rule (TierRewardScale.Falloff), the dial cleaning and zone override
    /// (TierRewardScale.CleanFalloff / FalloffFor), the saved-seconds form (FalloffOnDebt), the shipped defaults and the two
    /// Zone Control stats. No DB, no world.
    /// </summary>
    [TestClass]
    public class PrestigeCoinFalloffTests
    {
        private const double Pace = 375, Half = 0.5, Reserve = 30;

        /// <summary>
        /// Kills at a steady <paramref name="killsPerHour"/> from a fresh character (count 0), each worth <paramref name="credit"/>.
        /// Returns the credit paid over kills [firstKill, lastKill).
        /// </summary>
        private static double Simulate(double killsPerHour, int firstKill, int lastKill, double credit = 1.0,
            double pace = Pace, double mult = Half, double reserve = Reserve)
        {
            var interval = 3600.0 / killsPerHour;
            double recent = 0, total = 0, last = 0;
            for (var k = 0; k < lastKill; k++)
            {
                var t = k * interval;
                var (paid, count, _) = TierRewardScale.Falloff(recent, k == 0 ? 0 : t - last, credit, pace, mult, reserve);
                recent = count;
                last = t;
                if (k >= firstKill)
                    total += paid;
            }
            return total;
        }

        /// <summary>Paid credit in the third hour of a steady pace (the first hour includes the 30-kill head start).</summary>
        private static double ThirdHour(int killsPerHour, double credit = 1.0)
            => Simulate(killsPerHour, 2 * killsPerHour, 3 * killsPerHour, credit);

        // -- shipped defaults and registration --

        /// <summary>The shipped settings: 375 kills an hour, half pay past it, a 30-kill reserve.</summary>
        [TestMethod]
        public void Defaults_Falloff()
        {
            Assert.AreEqual(375.0, ServerConfig.zc_pcoin_falloff_kills_per_hour.Default, 1e-9);
            Assert.AreEqual(0.5, ServerConfig.zc_pcoin_falloff_mult.Default, 1e-9);
            Assert.AreEqual(30.0, ServerConfig.zc_pcoin_falloff_reserve.Default, 1e-9);
        }

        /// <summary>Both Zone Control stats are registered once, at the end of ZoneStat.All (after SlayerPoolShare), and are not weapon cards.</summary>
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

        // -- a steady pace --

        /// <summary>A steady pace at or under 375 an hour pays every kill in full.</summary>
        [TestMethod]
        public void UnderThePace_PaysInFull()
        {
            Assert.AreEqual(1500.0, Simulate(300, 0, 1500), 1e-9, "300 an hour for 5 hours");
            Assert.AreEqual(1875.0, Simulate(375, 0, 1875), 1e-9, "exactly 375 an hour for 5 hours");
        }

        /// <summary>Past the pace, every extra kill pays half: 500, 750 and 1000 an hour pay 437.5, 562.5 and 687.5.</summary>
        [TestMethod]
        public void PastThePace_EveryExtraKillPaysHalf()
        {
            Assert.AreEqual(437.5, ThirdHour(500), 1e-6, "375 + 125 x 0.5");
            Assert.AreEqual(562.5, ThirdHour(750), 1e-6, "375 + 375 x 0.5");
            Assert.AreEqual(687.5, ThirdHour(1000), 1e-6, "375 + 625 x 0.5");
        }

        /// <summary>A below-tier kill uses only its tier-dip credit of the pace, and is paid on that credit.</summary>
        [TestMethod]
        public void BelowTierKills_CountAsTheirCredit()
        {
            // 750 kills an hour at 0.5 credit is 375 full-value kills an hour: never cut
            Assert.AreEqual(1125.0, Simulate(750, 0, 2250, 0.5), 1e-6);
            // 1000 an hour at 0.5 = 500 full-value: 375 + 125 x 0.5
            Assert.AreEqual(437.5, ThirdHour(1000, 0.5), 1e-6);
        }

        // -- bursts, splits and refills --

        /// <summary>A burst with no time between kills pays the first 30 in full and cuts from the 31st.</summary>
        [TestMethod]
        public void Burst_TheReserveTakesThirtyAtOnce()
        {
            double recent = 0, total = 0;
            for (var i = 0; i < 40; i++)
            {
                var (paid, count, reduced) = TierRewardScale.Falloff(recent, 0, 1, Pace, Half, Reserve);
                Assert.AreEqual(i >= 30, reduced, $"kill {i + 1}");
                recent = count;
                total += paid;
            }
            Assert.AreEqual(30 + 10 * 0.5, total, 1e-9);
            Assert.AreEqual(30.0, recent, 1e-9, "the count never goes past the reserve");
        }

        /// <summary>A kill that only partly fits under the reserve pays the fitting part in full and the rest at half.</summary>
        [TestMethod]
        public void SplitKill_PartFullPartHalf()
        {
            var (paid, count, reduced) = TierRewardScale.Falloff(29.5, 0, 1, Pace, Half, Reserve);
            Assert.AreEqual(0.75, paid, 1e-9, "0.5 in full + 0.5 at half");
            Assert.AreEqual(30.0, count, 1e-9);
            Assert.IsTrue(reduced);
        }

        /// <summary>The count drains at the pace: 9.6 seconds frees one kill, 288 seconds or a day frees them all.</summary>
        [TestMethod]
        public void Refill_DrainsAtThePace()
        {
            // 9.6 s at 375 an hour drains one kill: back to exactly one full-rate kill
            var (paid, count, reduced) = TierRewardScale.Falloff(30, 9.6, 1, Pace, Half, Reserve);
            Assert.AreEqual(1.0, paid, 1e-9);
            Assert.AreEqual(30.0, count, 1e-9);
            Assert.IsFalse(reduced);

            // 288 s drains all 30
            (paid, count, _) = TierRewardScale.Falloff(30, 288, 1, Pace, Half, Reserve);
            Assert.AreEqual(1.0, paid, 1e-9);
            Assert.AreEqual(1.0, count, 1e-9);

            // a day offline: the same, never below 0
            (paid, count, _) = TierRewardScale.Falloff(30, 86400, 1, Pace, Half, Reserve);
            Assert.AreEqual(1.0, paid, 1e-9);
            Assert.AreEqual(1.0, count, 1e-9);
        }

        /// <summary>A kill worth no credit pays nothing and only drains the count to now.</summary>
        [TestMethod]
        public void ZeroCreditKill_OnlyDrains()
        {
            // what /tierdip uses to show the count as of now
            var (paid, count, reduced) = TierRewardScale.Falloff(30, 96, 0, Pace, Half, Reserve);
            Assert.AreEqual(0.0, paid);
            Assert.AreEqual(20.0, count, 1e-9);
            Assert.IsFalse(reduced);
        }

        // -- the share --

        /// <summary>A share of 1 never cuts, 0 pays nothing past the pace, and out-of-range or NaN shares are held to 0..1.</summary>
        [TestMethod]
        public void Mult_OneIsNoFalloff_ZeroPaysNothing()
        {
            var (paid, count, reduced) = TierRewardScale.Falloff(30, 0, 1, Pace, 1.0, Reserve);
            Assert.AreEqual(1.0, paid, 1e-9);
            Assert.AreEqual(30.0, count, 1e-9);
            Assert.IsFalse(reduced, "a share of 1 never counts as cut, so nobody is told");

            (paid, _, reduced) = TierRewardScale.Falloff(30, 0, 1, Pace, 0.0, Reserve);
            Assert.AreEqual(0.0, paid, 1e-9);
            Assert.IsTrue(reduced);

            Assert.AreEqual(1.0, TierRewardScale.Falloff(30, 0, 1, Pace, 7.0, Reserve).Paid, 1e-9, "above 1 is held to 1");
            Assert.AreEqual(0.0, TierRewardScale.Falloff(30, 0, 1, Pace, -1.0, Reserve).Paid, 1e-9, "below 0 is held to 0");
            Assert.AreEqual(1.0, TierRewardScale.Falloff(30, 0, 1, Pace, double.NaN, Reserve).Paid, 1e-9, "NaN never cuts");
        }

        // -- off and bad input --

        /// <summary>A pace of 0, negative, NaN or infinite turns the falloff off: full pay and nothing counted.</summary>
        [TestMethod]
        public void Off_PaysInFullAndKeepsNothing()
        {
            foreach (var pace in new[] { 0.0, -5.0, double.NaN, double.PositiveInfinity })
            {
                var (paid, count, reduced) = TierRewardScale.Falloff(30, 0, 1, pace, Half, Reserve);
                Assert.AreEqual(1.0, paid, 1e-9, $"pace {pace}");
                Assert.AreEqual(0.0, count, $"pace {pace}");
                Assert.IsFalse(reduced, $"pace {pace}");
            }
        }

        /// <summary>Bad inputs (a clock that went back, NaN, a count above the reserve, a bad credit or reserve) never pay more than they should.</summary>
        [TestMethod]
        public void BadInputs_NeverMintCoins()
        {
            Assert.AreEqual(0.5, TierRewardScale.Falloff(30, -100, 1, Pace, Half, Reserve).Paid, 1e-9, "a clock that went back drains nothing");
            Assert.AreEqual(0.5, TierRewardScale.Falloff(30, double.NaN, 1, Pace, Half, Reserve).Paid, 1e-9);
            Assert.AreEqual(1.0, TierRewardScale.Falloff(double.NaN, 0, 1, Pace, Half, Reserve).Paid, 1e-9, "a bad count is 0");
            Assert.AreEqual(0.5, TierRewardScale.Falloff(50, 0, 1, Pace, Half, Reserve).Paid, 1e-9, "a count above a lowered reserve is held to it");
            Assert.AreEqual(30.0, TierRewardScale.Falloff(50, 0, 1, Pace, Half, Reserve).Recent, 1e-9);
            Assert.AreEqual(0.0, TierRewardScale.Falloff(10, 0, double.NaN, Pace, Half, Reserve).Paid, "a bad credit pays nothing");
            Assert.AreEqual(0.0, TierRewardScale.Falloff(10, 0, -1, Pace, Half, Reserve).Paid);

            // a reserve under 1 (or not a number) is 1: one kill at a time still pays in full
            Assert.AreEqual(1.0, TierRewardScale.Falloff(0, 0, 1, Pace, Half, 0).Paid, 1e-9);
            Assert.AreEqual(0.5, TierRewardScale.Falloff(1, 0, 1, Pace, Half, 0).Paid, 1e-9);
            Assert.AreEqual(1.0, TierRewardScale.Falloff(0, 0, 1, Pace, Half, double.NaN).Paid, 1e-9);
        }

        // -- dial cleaning and the Zone Control override --

        /// <summary>Each dial is held to its range, and a NaN takes the declared default.</summary>
        [TestMethod]
        public void CleanFalloff_HoldsEachDialToItsRange()
        {
            var high = TierRewardScale.CleanFalloff(1e9, 5, 1e9);
            Assert.AreEqual(1_000_000.0, high.KillsPerHour);
            Assert.AreEqual(1.0, high.Mult);
            Assert.AreEqual(100_000.0, high.Reserve);

            var low = TierRewardScale.CleanFalloff(-1, -1, -1);
            Assert.AreEqual(0.0, low.KillsPerHour, "a negative pace is off");
            Assert.AreEqual(0.0, low.Mult);
            Assert.AreEqual(1.0, low.Reserve);

            var nan = TierRewardScale.CleanFalloff(double.NaN, double.NaN, double.NaN);
            Assert.AreEqual(375.0, nan.KillsPerHour, "not a number takes the declared default");
            Assert.AreEqual(0.5, nan.Mult);
            Assert.AreEqual(30.0, nan.Reserve);
        }

        /// <summary>A zone Default row authoring exactly <paramref name="values"/> (case-insensitive stat names).</summary>
        private static EvaluatedProfile Zone(Dictionary<string, double> values)
            => new EvaluatedProfile("test zone", 15, ZoneVariant.Minion,
                new Dictionary<string, double>(values, StringComparer.OrdinalIgnoreCase));

        /// <summary>A zone Default row overrides the server per stat, and a zone pace scales the reserve so it holds the same time.</summary>
        [TestMethod]
        public void FalloffFor_ZoneStatsOverrideTheServerPerStat()
        {
            var server = TierRewardScale.FalloffFor(null);
            Assert.AreEqual(375.0, server.KillsPerHour);
            Assert.AreEqual(0.5, server.Mult);
            Assert.AreEqual(30.0, server.Reserve);

            var none = TierRewardScale.FalloffFor(Zone(new Dictionary<string, double>()));
            Assert.AreEqual(server, none, "a zone that authors neither stat = the server settings");

            Assert.AreEqual(288.0, server.WindowSeconds, 1e-9, "30 kills at 375 an hour");

            var pace = TierRewardScale.FalloffFor(Zone(new Dictionary<string, double> { [ZoneStat.PcoinFalloffKillsPerHour] = 500 }));
            Assert.AreEqual(500.0, pace.KillsPerHour);
            Assert.AreEqual(0.5, pace.Mult, "the unset stat keeps the server value");
            Assert.AreEqual(40.0, pace.Reserve, 1e-9, "the reserve scales with the zone's pace...");
            Assert.AreEqual(288.0, pace.WindowSeconds, 1e-9, "...so it holds the same time in every zone");

            var share = TierRewardScale.FalloffFor(Zone(new Dictionary<string, double> { [ZoneStat.PcoinFalloffMult] = 0.25 }));
            Assert.AreEqual(375.0, share.KillsPerHour);
            Assert.AreEqual(0.25, share.Mult);

            var off = TierRewardScale.FalloffFor(Zone(new Dictionary<string, double> { [ZoneStat.PcoinFalloffKillsPerHour] = 0 }));
            Assert.AreEqual(0.0, off.KillsPerHour, "0 in a zone = no falloff for kills there");
            Assert.AreEqual(30.0, off.Reserve);
            Assert.AreEqual(0.0, off.WindowSeconds);
        }

        /// <summary>An authored NaN keeps the server value, and an out-of-range share is held to 0..1.</summary>
        [TestMethod]
        public void FalloffFor_BadZoneValues()
        {
            var nan = TierRewardScale.FalloffFor(Zone(new Dictionary<string, double>
            {
                [ZoneStat.PcoinFalloffKillsPerHour] = double.NaN,
                [ZoneStat.PcoinFalloffMult] = double.NaN,
            }));
            Assert.AreEqual(375.0, nan.KillsPerHour, "an authored NaN keeps the server value");
            Assert.AreEqual(0.5, nan.Mult);

            var wild = TierRewardScale.FalloffFor(Zone(new Dictionary<string, double> { [ZoneStat.PcoinFalloffMult] = 3 }));
            Assert.AreEqual(1.0, wild.Mult, "held to 0..1");
        }

        /// <summary>With the server pace off and a zone turning the falloff on, the zone's reserve is counted at the declared
        /// default pace, so it still holds 288 seconds (CodeRabbit #595).</summary>
        [TestMethod]
        public void FalloffFor_ServerPaceOff_ZoneReserveUsesTheDefaultPace()
        {
            var serverOff = TierRewardScale.CleanFalloff(0, Half, Reserve);

            var at500 = TierRewardScale.FalloffFor(Zone(new Dictionary<string, double> { [ZoneStat.PcoinFalloffKillsPerHour] = 500 }), serverOff);
            Assert.AreEqual(500.0, at500.KillsPerHour);
            Assert.AreEqual(40.0, at500.Reserve, 1e-9);
            Assert.AreEqual(288.0, at500.WindowSeconds, 1e-9);

            var at3750 = TierRewardScale.FalloffFor(Zone(new Dictionary<string, double> { [ZoneStat.PcoinFalloffKillsPerHour] = 3750 }), serverOff);
            Assert.AreEqual(300.0, at3750.Reserve, 1e-9);
            Assert.AreEqual(288.0, at3750.WindowSeconds, 1e-9, "not 28.8 s");

            var at375 = TierRewardScale.FalloffFor(Zone(new Dictionary<string, double> { [ZoneStat.PcoinFalloffKillsPerHour] = 375 }), serverOff);
            Assert.AreEqual(30.0, at375.Reserve, 1e-9);

            var still = TierRewardScale.FalloffFor(Zone(new Dictionary<string, double>()), serverOff);
            Assert.AreEqual(0.0, still.KillsPerHour, "a zone that does not turn it on stays off");
            Assert.AreEqual(serverOff, TierRewardScale.FalloffFor(null, serverOff));
        }

        // -- the saved count, in seconds (FalloffOnDebt) --

        /// <summary>Like <see cref="Simulate"/>, through the saved-seconds form with the given dials. Paid credit over [firstKill, lastKill).</summary>
        private static double SimulateDebt(double killsPerHour, TierRewardScale.FalloffDials dials, int firstKill, int lastKill)
        {
            var interval = 3600.0 / killsPerHour;
            double debt = 0, total = 0, last = 0;
            for (var k = 0; k < lastKill; k++)
            {
                var t = k * interval;
                var (paid, next, _) = TierRewardScale.FalloffOnDebt(debt, k == 0 ? 0 : t - last, 1.0, dials);
                debt = next;
                last = t;
                if (k >= firstKill)
                    total += paid;
            }
            return total;
        }

        /// <summary>The saved-seconds form pays exactly as the kill-count rule at one pace, in a server-pace or a zone-pace zone.</summary>
        [TestMethod]
        public void Debt_AtOnePace_IsTheSameRule()
        {
            var server = TierRewardScale.FalloffFor(null);
            Assert.AreEqual(437.5, SimulateDebt(500, server, 1000, 1500), 1e-6, "500 an hour against 375, third hour");

            var zone500 = TierRewardScale.FalloffFor(Zone(new Dictionary<string, double> { [ZoneStat.PcoinFalloffKillsPerHour] = 500 }));
            Assert.AreEqual(500.0, SimulateDebt(500, zone500, 1000, 1500), 1e-6, "a 500 zone pays 500 an hour in full");
            Assert.AreEqual(625.0, SimulateDebt(750, zone500, 1500, 2250), 1e-6, "750 there: 500 + 250 x 0.5");

            var (paid, debt, reduced) = TierRewardScale.FalloffOnDebt(288, 0, 1, server);
            Assert.AreEqual(0.5, paid, 1e-9, "a full window cuts the next kill");
            Assert.AreEqual(288.0, debt, 1e-9);
            Assert.IsTrue(reduced);
        }

        /// <summary>A kill where the falloff is off pays in full and leaves the saved count as it was.</summary>
        [TestMethod]
        public void Debt_NoFalloffZone_LeavesTheCountAlone()
        {
            // a kill where the falloff is off pays in full and must not empty the count (that would hand back the reserve)
            var off = TierRewardScale.FalloffFor(Zone(new Dictionary<string, double> { [ZoneStat.PcoinFalloffKillsPerHour] = 0 }));
            var (paid, debt, reduced) = TierRewardScale.FalloffOnDebt(288, 5, 1, off);
            Assert.AreEqual(1.0, paid, 1e-9);
            Assert.AreEqual(288.0, debt, 1e-9, "unchanged, not drained, not emptied");
            Assert.IsFalse(reduced);

            Assert.AreEqual(0.0, TierRewardScale.FalloffOnDebt(double.NaN, 5, 1, off).Debt, "a bad count reads as 0");
        }

        /// <summary>A kill in a zone authored at a very high pace cannot empty a count built up at a slower pace.</summary>
        [TestMethod]
        public void Debt_HighPaceZone_CannotEmptyACountBuiltElsewhere()
        {
            var server = TierRewardScale.FalloffFor(null);
            var fast = TierRewardScale.FalloffFor(Zone(new Dictionary<string, double> { [ZoneStat.PcoinFalloffKillsPerHour] = 100_000 }));
            Assert.AreEqual(8000.0, fast.Reserve, 1e-9);

            // full window (288 s) from the 375 zone, one kill in the 100,000 zone a second later: it costs 0.036 s and fits...
            var (paid, debt, reduced) = TierRewardScale.FalloffOnDebt(288, 1, 1, fast);
            Assert.AreEqual(1.0, paid, 1e-9);
            Assert.AreEqual(287.036, debt, 1e-9, "...but drains only the one second that passed");
            Assert.IsFalse(reduced);

            // ...so back in the 375 zone a second later the count is still nearly full and the kill is cut
            (paid, debt, reduced) = TierRewardScale.FalloffOnDebt(debt, 1, 1, server);
            Assert.IsTrue(reduced, "the trip did not hand back the reserve");
            Assert.AreEqual(0.6022916666666667, paid, 1e-9);
            Assert.AreEqual(288.0, debt, 1e-9);
        }
    }
}
