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

            var nowTicks = DateTime.UtcNow.Ticks;
            string message;
            int suppressed;

            lock (_lock)
            {
                if (nowTicks < _nextLogUtcTicks)
                {
                    _suppressed++;
                    return;
                }

                suppressed = _suppressed;
                _suppressed = 0;
                _nextLogUtcTicks = DateTime.UtcNow.AddMilliseconds(minIntervalMs).Ticks;
                message = messageFactory();
            }

            if (suppressed > 0)
                Log.Warn($"{message} [suppressed {suppressed} similar in interval]");
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
