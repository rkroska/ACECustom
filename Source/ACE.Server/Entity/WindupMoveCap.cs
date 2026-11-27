using ACE.Entity.Enum;

namespace ACE.Server.Entity
{
    /// <summary>
    /// How far a caster may move during a spell windup before the cast fizzles ("Your movement disrupted spell casting!").
    /// PKs always get the fixed retail cap. NPKs get the npk_windup_max_move setting, where 0 = no cap (retail ACE: NPKs
    /// moved freely). Added 2026-11-23 because a client that does not stop its character during a windup could run through
    /// every cast: measured 6.7-17 m per Clouded Soul windup against 1.7-9.2 m on the retail client.
    ///
    /// NPK positions come only from the client (no server physics: FastTick is PK only), so three checks work together:
    ///   1. at every accepted position during the cast (catches out-and-back runs, fizzles at the moment of the move);
    ///   2. when the spell lands (the original check);
    ///   3. for a few seconds after it lands, positions must stay within reach of the cast start at run speed. A client that
    ///      stops reporting positions during the windup and catches up afterwards is refused and sent back.
    /// All three measure from the cast start point, which client positions in the first StartSettleSeconds of the cast can move
    /// (the server's idea of where the cast started lags the client).
    /// Kept out of Player so it can be tested without a running server.
    /// </summary>
    public static class WindupMoveCap
    {
        /// <summary>The retail PK cap, in metres.</summary>
        public const float PkCap = 6.0f;

        /// <summary>How long after a cast lands the catch-up guard keeps checking positions.</summary>
        public const double GuardSeconds = 3.0;

        /// <summary>Speed allowance on top of run speed for the catch-up guard (packet timing jitter).</summary>
        public const double GuardSpeedSlack = 1.25;

        /// <summary>The fastest anyone runs: 4.0 m/s x the capped run rate 4.5 (MovementSystem.GetRunRate). The catch-up guard uses
        /// this rather than the player's own rate, which drops when exhausted before the client actually slows down.</summary>
        public const double MaxRunSpeed = 18.0;

        /// <summary>Fixed distance allowance on top of the cap for the catch-up guard, in metres.</summary>
        public const double GuardExtraMetres = 2.0;

        /// <summary>
        /// For this long after an NPK cast starts, each client position moves the cast start point there instead of counting as
        /// windup movement. The server only knows where the client last reported, which can be a second behind; the retail
        /// client sends a correction right as the cast starts (measured 2026-11-23: 2.4 m after 0.04 s and 8.3 m after 0.16 s,
        /// the second one fizzling an honest cast at a 10 m cap). A client that stays quiet past this window gets no new start
        /// point, so withholding positions forgives nothing; reporting inside it forgives at most this long at run speed (~4.5 m).
        /// </summary>
        public const double StartSettleSeconds = 0.25;

        /// <summary>True when a position arriving <paramref name="secondsSinceCastStart"/> after the cast started is still inside
        /// the settle window, so it becomes the new start point rather than windup movement.</summary>
        public static bool MovesStartPoint(double secondsSinceCastStart) =>
            secondsSinceCastStart >= 0 && secondsSinceCastStart <= StartSettleSeconds;

        /// <summary>The cap for this caster in metres; 0 = no cap. Bad setting values (negative, NaN, infinite) mean no cap.</summary>
        public static float Get(PlayerKillerStatus pkStatus, double npkCap)
        {
            if (pkStatus != PlayerKillerStatus.NPK)
                return PkCap;
            return double.IsFinite(npkCap) && npkCap > 0 ? (float)npkCap : 0f;
        }

        /// <summary>True when a windup that moved <paramref name="dist"/> metres breaks <paramref name="cap"/>. Exactly at the cap
        /// is still allowed, as in the original PK check.</summary>
        public static bool MovedTooFar(float dist, float cap) => cap > 0 && dist > cap;

        /// <summary>How far from the cast start (ground distance) a position may be, <paramref name="secondsSinceLand"/> after the
        /// cast landed: the cap, plus run speed for the time since, with slack.</summary>
        public static double CatchUpAllowance(float cap, double runSpeed, double secondsSinceLand) =>
            cap + System.Math.Max(0, runSpeed) * System.Math.Max(0, secondsSinceLand) * GuardSpeedSlack + GuardExtraMetres;

        /// <summary>True when a position <paramref name="groundDist"/> metres from the cast start, arriving
        /// <paramref name="secondsSinceLand"/> after the cast landed, is further than the character could have run.
        /// Never true with no cap or once the guard window is over.</summary>
        public static bool CatchUpTooFar(float groundDist, float cap, double runSpeed, double secondsSinceLand) =>
            cap > 0 && secondsSinceLand <= GuardSeconds && groundDist > CatchUpAllowance(cap, runSpeed, secondsSinceLand);
    }
}
