using System;
using System.Linq;

using ACE.Server.Entity;
using ACE.Server.Managers;
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
        /// which is also the |cloakdefs= order. Later stats append after them, so check the run, not the very end.</summary>
        [TestMethod]
        public void CloakStats_AreAppendedInOrder()
        {
            var start = Array.IndexOf(ZoneStat.All, CloakStats[0]);
            Assert.IsTrue(start > 0, "cloak dials are registered");
            Assert.AreEqual(ZoneStat.CharmLevelCap, ZoneStat.All[start - 1], "appended after the previous tail");
            CollectionAssert.AreEqual(CloakStats, ZoneStat.All.Skip(start).Take(CloakStats.Length).ToArray());
        }

        /// <summary>Out-of-range values are held to the documented ranges.</summary>
        [TestMethod]
        public void Clamped_HoldsEveryDialToItsRange()
        {
            var high = ZcCloakProcs.Dials.Clamped(5.0, 99999.0, 1e12, 5000.0, 99999.0, 5000.0, 99999.0);
            Assert.AreEqual(1.0, high.Chance);
            Assert.AreEqual(3600.0, high.Cooldown);
            Assert.AreEqual(int.MaxValue, high.BreezeHeal);
            Assert.AreEqual(1000, high.DodgerHits);
            Assert.AreEqual(3600.0, high.DodgerWindow);
            Assert.AreEqual(1000.0, high.DodgerStaminaMult);
            Assert.AreEqual(3600.0, high.GraceSeconds);

            var low = ZcCloakProcs.Dials.Clamped(-1.0, -3.0, -5.0, -2.0, -1.0, 0.0, -1.0);
            Assert.AreEqual(0.0, low.Chance);
            Assert.AreEqual(0.0, low.Cooldown);
            Assert.AreEqual(0, low.BreezeHeal);
            Assert.AreEqual(0, low.DodgerHits);
            Assert.AreEqual(0.0, low.DodgerWindow);
            Assert.AreEqual(0.01, low.DodgerStaminaMult);
            Assert.AreEqual(0.0, low.GraceSeconds);
        }

        /// <summary>In-range values pass through; the whole-number dials round (half away from zero); the origin is kept.</summary>
        [TestMethod]
        public void Clamped_KeepsInRangeValues()
        {
            var d = ZcCloakProcs.Dials.Clamped(0.03, 5.0, 800.4, 2.5, 9.0, 4.0, 1.5, ZcCloakProcs.DialSource.TierDefault, 14);
            Assert.AreEqual(0.03, d.Chance);
            Assert.AreEqual(5.0, d.Cooldown);
            Assert.AreEqual(800, d.BreezeHeal);
            Assert.AreEqual(3, d.DodgerHits);
            Assert.AreEqual(9.0, d.DodgerWindow);
            Assert.AreEqual(4.0, d.DodgerStaminaMult);
            Assert.AreEqual(1.5, d.GraceSeconds);
            Assert.AreEqual(ZcCloakProcs.DialSource.TierDefault, d.Origin);
            Assert.AreEqual(14, d.Tier);
            Assert.AreEqual("Zone Control - T14 Default", d.Source);
        }

        /// <summary>A value that is not a number takes the setting's declared default, never NaN; the origin defaults to the
        /// server settings.</summary>
        [TestMethod]
        public void Clamped_NotANumber_FallsBackToDeclaredDefaults()
        {
            var d = ZcCloakProcs.Dials.Clamped(double.NaN, double.PositiveInfinity, double.NaN, double.NegativeInfinity, double.NaN,
                double.NaN, double.PositiveInfinity);
            Assert.AreEqual(ServerConfig.zc_cloak_proc_chance.Default, d.Chance);
            Assert.AreEqual(ServerConfig.zc_cloak_proc_cooldown.Default, d.Cooldown);
            Assert.AreEqual((int)ServerConfig.zc_cloak_breeze_heal.Default, d.BreezeHeal);
            Assert.AreEqual((int)ServerConfig.zc_cloak_dodger_hits.Default, d.DodgerHits);
            Assert.AreEqual(ServerConfig.zc_cloak_dodger_window.Default, d.DodgerWindow);
            Assert.AreEqual(ServerConfig.zc_cloak_dodger_stamina_mult.Default, d.DodgerStaminaMult);
            Assert.AreEqual(ServerConfig.zc_cloak_grace_seconds.Default, d.GraceSeconds);
            Assert.AreEqual(ZcCloakProcs.DialSource.Server, d.Origin);
            Assert.AreEqual("server settings", d.Source);
        }
    }
}
