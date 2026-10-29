using System;
using System.Linq;

using ACE.Server.Entity;
using ACE.Server.Managers.ZoneScaling;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// The T11+ cloak proc Zone Control dials (owner 2026-10-29): the seven cloak_* stats are registered (so /zonecontrol set
    /// and the plugin accept them) as the append-only tail of ZoneStat.All, and every value - a server setting or a zone stat -
    /// is held to its range by ZcCloakProcs.Dials.Clamped. Pure: no database, no world.
    /// </summary>
    [TestClass]
    public class ZcCloakDialsTests
    {
        private static readonly string[] CloakStats =
        {
            ZoneStat.CloakProcChance, ZoneStat.CloakProcCooldown, ZoneStat.CloakBreezeHeal, ZoneStat.CloakDodgerHits,
            ZoneStat.CloakDodgerWindow, ZoneStat.CloakDodgerStaminaMult, ZoneStat.CloakGraceSeconds,
        };

        /// <summary>Each dial is a known stat, listed once.</summary>
        [TestMethod]
        public void CloakStats_AreRegisteredOnce()
        {
            foreach (var stat in CloakStats)
            {
                Assert.IsTrue(ZoneStat.IsKnownStat(stat), $"/zonecontrol ... set {stat} must be accepted");
                Assert.AreEqual(1, ZoneStat.All.Count(s => string.Equals(s, stat, StringComparison.OrdinalIgnoreCase)), stat);
            }
        }

        /// <summary>The plugin matches stats by name and mirrors the tail of All: the dials were appended there, in this order,
        /// which is also the |cloakdefs= order.</summary>
        [TestMethod]
        public void CloakStats_AreTheTailOfAll()
        {
            var tail = ZoneStat.All.Skip(ZoneStat.All.Length - CloakStats.Length).ToArray();
            CollectionAssert.AreEqual(CloakStats, tail);
        }

        /// <summary>Out-of-range values are held to the documented ranges.</summary>
        [TestMethod]
        public void Clamped_HoldsEveryDialToItsRange()
        {
            var high = ZcCloakProcs.Dials.Clamped(5.0, 99999.0, 1e12, 5000.0, 99999.0, 5000.0, 99999.0, "test");
            Assert.AreEqual(1.0, high.Chance);
            Assert.AreEqual(3600.0, high.Cooldown);
            Assert.AreEqual(int.MaxValue, high.BreezeHeal);
            Assert.AreEqual(1000, high.DodgerHits);
            Assert.AreEqual(3600.0, high.DodgerWindow);
            Assert.AreEqual(1000.0, high.DodgerStaminaMult);
            Assert.AreEqual(3600.0, high.GraceSeconds);

            var low = ZcCloakProcs.Dials.Clamped(-1.0, -3.0, -5.0, -2.0, -1.0, 0.0, -1.0, "test");
            Assert.AreEqual(0.0, low.Chance);
            Assert.AreEqual(0.0, low.Cooldown);
            Assert.AreEqual(0, low.BreezeHeal);
            Assert.AreEqual(0, low.DodgerHits);
            Assert.AreEqual(0.0, low.DodgerWindow);
            Assert.AreEqual(0.01, low.DodgerStaminaMult);
            Assert.AreEqual(0.0, low.GraceSeconds);
        }

        /// <summary>In-range values pass through; the whole-number dials round.</summary>
        [TestMethod]
        public void Clamped_KeepsInRangeValues()
        {
            var d = ZcCloakProcs.Dials.Clamped(0.03, 5.0, 800.4, 2.6, 9.0, 4.0, 1.5, "Zone Control (this zone)");
            Assert.AreEqual(0.03, d.Chance);
            Assert.AreEqual(5.0, d.Cooldown);
            Assert.AreEqual(800, d.BreezeHeal);
            Assert.AreEqual(3, d.DodgerHits);
            Assert.AreEqual(9.0, d.DodgerWindow);
            Assert.AreEqual(4.0, d.DodgerStaminaMult);
            Assert.AreEqual(1.5, d.GraceSeconds);
            Assert.AreEqual("Zone Control (this zone)", d.Source);
        }

        /// <summary>A value that is not a number falls back to the shipped default, never to NaN.</summary>
        [TestMethod]
        public void Clamped_NotANumber_FallsBackToShippedDefaults()
        {
            var d = ZcCloakProcs.Dials.Clamped(double.NaN, double.PositiveInfinity, double.NaN, double.NegativeInfinity, double.NaN,
                double.NaN, double.PositiveInfinity, "test");
            Assert.AreEqual(0.02, d.Chance);
            Assert.AreEqual(7.0, d.Cooldown);
            Assert.AreEqual(500, d.BreezeHeal);
            Assert.AreEqual(3, d.DodgerHits);
            Assert.AreEqual(7.0, d.DodgerWindow);
            Assert.AreEqual(10.0, d.DodgerStaminaMult);
            Assert.AreEqual(2.0, d.GraceSeconds);
        }
    }
}
