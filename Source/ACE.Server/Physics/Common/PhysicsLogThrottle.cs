using System;
using log4net;

namespace ACE.Server.Physics.Common
{
    /// <summary>
    /// Rate-limited Warn for hot physics / cell.dat paths. Avoids log4net buffer blowups and console spam.
    /// </summary>
    public sealed class PhysicsLogThrottle
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(PhysicsLogThrottle));

        private long _nextLogUtcTicks;
        private int _suppressed;
        private readonly object _lock = new();

        /// <summary>
        /// Emit at most one warning per <paramref name="minIntervalMs"/>, aggregating suppressed duplicates.
        /// </summary>
        public void Warn(Func<string> messageFactory, int minIntervalMs = 10_000)
        {
            if (!Log.IsWarnEnabled)
                return;

            // Single clock read for admission vs. window extension (avoids mixed UtcNow instants).
            var utcNow = DateTime.UtcNow;
            var nowTicks = utcNow.Ticks;

            int suppressedSnapshot;

            lock (_lock)
            {
                if (nowTicks < _nextLogUtcTicks)
                {
                    _suppressed++;
                    return;
                }

                suppressedSnapshot = _suppressed;
                _suppressed = 0;
            }

            string message;
            try
            {
                // Outside lock: message builders may be slow; failures must not leave counters wedged.
                message = messageFactory();
            }
            catch
            {
                lock (_lock)
                {
                    _suppressed += suppressedSnapshot;
                }
                throw;
            }

            var nextTicks = utcNow.AddMilliseconds(minIntervalMs).Ticks;

            lock (_lock)
            {
                // Monotonic: concurrent admissions use their own utcNow; never move the window backward.
                _nextLogUtcTicks = Math.Max(_nextLogUtcTicks, nextTicks);
            }

            if (suppressedSnapshot > 0)
                Log.Warn($"{message} [suppressed {suppressedSnapshot} similar in interval]");
            else
                Log.Warn(message);
        }
    }

    /// <summary>Shared throttle gates (separate counters per category).</summary>
    public static class PhysicsLogGates
    {
        public static readonly PhysicsLogThrottle InvalidEnvCellDat = new();
        public static readonly PhysicsLogThrottle MissingCellStructure = new();
        public static readonly PhysicsLogThrottle BuildVisibleDepth = new();
        public static readonly PhysicsLogThrottle GetLandcellOutdoorMiss = new();
        public static readonly PhysicsLogThrottle GetLandcellVariantRecover = new();
        public static readonly PhysicsLogThrottle AddVisibleCellNull = new();
        public static readonly PhysicsLogThrottle PostInitSkipped = new();
    }
}
