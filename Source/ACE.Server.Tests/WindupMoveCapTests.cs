using ACE.Entity.Enum;
using ACE.Server.Entity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Windup move cap (2026-11-23): PKs keep the fixed 6 m cap; NPKs use npk_windup_max_move, where 0 = no cap (the
    /// retail ACE default). Distances are from a local capture of 5 Clouded Soul casts per client while moving:
    /// retail 1.7, 3.1, 3.3, 3.8, 9.2 m; a client that ignores windups 6.7, 10.0, 11.8, 15.1, 17.0 m.
    /// </summary>
    [TestClass]
    public class WindupMoveCapTests
    {
        private static readonly float[] Retail = { 1.7f, 3.1f, 3.3f, 3.8f, 9.2f };
        private static readonly float[] RunThrough = { 6.7f, 10.0f, 11.8f, 15.1f, 17.0f };

        // run speed at the capped run rate (4.0 m/s x 4.5)
        private const double MaxRunSpeed = WindupMoveCap.MaxRunSpeed;

        [TestMethod]
        public void NpkIsUncappedWhenTheSettingIsZero()
        {
            var cap = WindupMoveCap.Get(PlayerKillerStatus.NPK, 0);
            Assert.AreEqual(0f, cap);
            Assert.IsFalse(WindupMoveCap.MovedTooFar(17f, cap), "default must keep today's behavior: NPKs never fizzle for moving");
            Assert.IsFalse(WindupMoveCap.MovedTooFar(1000f, cap));
            Assert.IsFalse(WindupMoveCap.CatchUpTooFar(1000f, cap, MaxRunSpeed, 0), "no cap means no catch-up guard either");
        }

        [TestMethod]
        public void NpkCapAtTenMetresSparesRetailButIsOnlyADamper()
        {
            var cap = WindupMoveCap.Get(PlayerKillerStatus.NPK, 10);
            Assert.AreEqual(10f, cap);
            foreach (var d in Retail)
                Assert.IsFalse(WindupMoveCap.MovedTooFar(d, cap), $"retail {d} m");

            // honest about the overlap: 2 of the 5 run-through casts stay within 10 m and still land
            var fizzled = 0;
            foreach (var d in RunThrough)
                if (WindupMoveCap.MovedTooFar(d, cap))
                    fizzled++;
            Assert.AreEqual(3, fizzled, "10 m stops 11.8 / 15.1 / 17.0, not 6.7 or 10.0 (exactly at the cap is allowed)");
        }

        [TestMethod]
        public void NpkCapAtSixMetresMatchesThePkCap()
        {
            var cap = WindupMoveCap.Get(PlayerKillerStatus.NPK, 6);
            Assert.AreEqual(WindupMoveCap.PkCap, cap);
            foreach (var d in RunThrough)
                Assert.IsTrue(WindupMoveCap.MovedTooFar(d, cap), $"run-through {d} m");
            Assert.IsTrue(WindupMoveCap.MovedTooFar(9.2f, cap), "6 m would also fizzle the longest retail cast measured");
        }

        [TestMethod]
        public void PkAlwaysUsesTheFixedCapWhateverTheNpkSetting()
        {
            foreach (var pk in new[] { PlayerKillerStatus.PK, PlayerKillerStatus.PKLite })
                foreach (var setting in new[] { 0.0, 3.0, 10.0, 100.0 })
                    Assert.AreEqual(WindupMoveCap.PkCap, WindupMoveCap.Get(pk, setting), $"{pk} with setting {setting}");
        }

        [TestMethod]
        public void BadSettingValuesMeanNoCap()
        {
            foreach (var bad in new[] { -5.0, double.NaN, double.PositiveInfinity })
                Assert.AreEqual(0f, WindupMoveCap.Get(PlayerKillerStatus.NPK, bad), $"setting {bad}");
        }

        [TestMethod]
        public void CatchUpGuardRefusesAClientThatHeldItsPositionThroughTheWindup()
        {
            // the server still had the caster at the start when the spell landed; 0.1 s later the client reports where it really
            // is after running the whole 2.7 s windup at full speed (~48 m)
            Assert.IsTrue(WindupMoveCap.CatchUpTooFar(48f, 10f, MaxRunSpeed, 0.1));
            // even a short hidden run is refused right after landing: allowance at 0.1 s = 10 + 18 x 0.1 x 1.25 + 2 = 14.25 m
            Assert.AreEqual(14.25, WindupMoveCap.CatchUpAllowance(10f, MaxRunSpeed, 0.1), 1e-9);
            Assert.IsTrue(WindupMoveCap.CatchUpTooFar(15f, 10f, MaxRunSpeed, 0.1));
        }

        [TestMethod]
        public void CatchUpGuardNeverRefusesSomeoneRunningAtRunSpeed()
        {
            // an honest caster ends within the cap and then runs away at full speed: always inside the allowance
            const float cap = 10f;
            for (var t = 0.0; t <= WindupMoveCap.GuardSeconds; t += 0.05)
            {
                var dist = (float)(cap + MaxRunSpeed * t);
                Assert.IsFalse(WindupMoveCap.CatchUpTooFar(dist, cap, MaxRunSpeed, t), $"t={t:0.00} dist={dist:0.0}");
            }
        }

        [TestMethod]
        public void CatchUpGuardStopsAfterItsWindow()
        {
            Assert.IsTrue(WindupMoveCap.CatchUpTooFar(500f, 10f, MaxRunSpeed, WindupMoveCap.GuardSeconds));
            Assert.IsFalse(WindupMoveCap.CatchUpTooFar(500f, 10f, MaxRunSpeed, WindupMoveCap.GuardSeconds + 0.01));
        }

        [TestMethod]
        public void CatchUpGuardHandlesOddInputs()
        {
            // negative time or speed (clock skew, bad run rate) never shrinks the allowance below cap + extra
            Assert.AreEqual(12.0, WindupMoveCap.CatchUpAllowance(10f, -5, -1), 1e-9);
            Assert.IsFalse(WindupMoveCap.CatchUpTooFar(12f, 10f, -5, -1));
        }

        /// <summary>Replays client positions (seconds since cast start, x, y) through the mid-cast check the way
        /// Player.CheckWindupMoveDuringCast applies it: inside the settle window a position becomes the start point, after it the
        /// distance from the start point is checked. Returns the time of the fizzle, or null when the cast survives.
        /// This mirrors the Player method rather than calling it (Player cannot be built without a running server), so it proves
        /// the rule, not the wiring: StartPosTime being set on both cast-start paths is covered only by live testing.</summary>
        private static double? Replay(float cap, float startX, float startY, params (double t, float x, float y)[] positions)
        {
            float sx = startX, sy = startY;
            foreach (var (t, x, y) in positions)
            {
                if (WindupMoveCap.MovesStartPoint(t)) { sx = x; sy = y; continue; }
                var dist = (float)System.Math.Sqrt((x - sx) * (x - sx) + (y - sy) * (y - sy));
                if (WindupMoveCap.MovedTooFar(dist, cap))
                    return t;
            }
            return null;
        }

        [TestMethod]
        public void SettleWindowIsTheFirstQuarterSecond()
        {
            Assert.IsTrue(WindupMoveCap.MovesStartPoint(0));
            Assert.IsTrue(WindupMoveCap.MovesStartPoint(0.25));
            Assert.IsFalse(WindupMoveCap.MovesStartPoint(0.26));
            Assert.IsFalse(WindupMoveCap.MovesStartPoint(-0.01)); // clock oddity: never moves the start
            Assert.IsFalse(WindupMoveCap.MovesStartPoint(double.NaN));
        }

        [TestMethod]
        public void RetailCorrectionAtCastStartNoLongerFizzles()
        {
            // 2026-11-23 13:19:22, retail client, Clouded Soul, cap 10 m: an 8.3 m correction 0.16 s after the cast started, then
            // ~4 m of real creeping. Measured from the server's start this was 11.8 m and fizzled an honest cast.
            var positions = new (double, float, float)[]
            {
                (0.165, 27.6f, 129.9f), (0.706, 26.6f, 130.1f), (1.697, 26.6f, 131.4f), (1.753, 26.6f, 132.3f),
                (2.288, 26.7f, 132.2f), (2.292, 25.0f, 133.5f),
            };
            Assert.IsNull(Replay(10f, 35.8f, 128.8f, positions));

            // without the settle window (start = the server's position) it fizzles, as it did live
            float sx = 35.8f, sy = 128.8f;
            Assert.IsTrue(WindupMoveCap.MovedTooFar((float)System.Math.Sqrt((25.0f - sx) * (25.0f - sx) + (133.5f - sy) * (133.5f - sy)), 10f));
        }

        [TestMethod]
        public void RetailCorrectionRightAfterAnUnchangedReportStillMovesTheStart()
        {
            // 13:14:14 retail: a report at the same spot 1 ms after the cast, then a 2.4 m correction at 0.04 s. Every report in
            // the window moves the start, not just the first, so the correction is not counted as movement.
            Assert.IsNull(Replay(10f, 17.2f, 119.5f,
                (0.001, 17.2f, 119.5f), (0.038, 14.8f, 119.8f), (1.455, 9.7f, 116.2f), (1.537, 9.2f, 115.9f),
                (1.609, 9.1f, 115.8f), (1.748, 8.7f, 115.8f)));
        }

        [TestMethod]
        public void UnrealRunningThroughTheCastStillFizzles()
        {
            // 14:10:32 Unreal client: first report 0.58 s into the cast, steady running to 13.4 m. Nothing lands in the settle
            // window, so the start stays where the server had it and the cast fizzles at 1.475 s, as it did live.
            Assert.AreEqual(1.475, Replay(10f, 47.7f, 119.0f,
                (0.579, 46.5f, 119.6f), (1.100, 44.6f, 120.0f), (1.460, 39.0f, 121.2f), (1.475, 34.8f, 122.1f)).Value, 1e-9);
        }

        [TestMethod]
        public void WithholdingTheFirstReportForgivesNothing()
        {
            // a client that stays quiet past the window and then reports where it ran to: the start does not move
            Assert.IsNotNull(Replay(10f, 0f, 0f, (0.26, 4.7f, 0f), (0.9, 10.5f, 0f)));
            // reporting inside the window forgives at most 0.25 s of running; everything after still counts
            Assert.IsNotNull(Replay(10f, 0f, 0f, (0.25, 4.5f, 0f), (1.5, 14.6f, 0f)));
            Assert.IsNull(Replay(10f, 0f, 0f, (0.25, 4.5f, 0f), (1.5, 14.4f, 0f)));
        }
    }
}
