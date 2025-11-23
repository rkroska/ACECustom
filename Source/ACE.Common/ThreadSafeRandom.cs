using System;
using System.Threading;

namespace ACE.Common
{
    // important class, ensure unit tests pass for this
    // todo: implement exactly the way AC handles it.. which we'll never know unless we get original source code
    public static class ThreadSafeRandom
    {
        // A single, thread-safe random to create seeds.
        private static readonly Random _globalRandom = new();
        private static readonly object _seedLock = new();

        // Use the global generator to seed each new thread-local instance
        private static readonly ThreadLocal<Random> _threadRandom = new(() =>
        {
            // Lock to ensure _globalRandom is accessed by only one thread at a time when generating the next seed.
            int seed;
            lock (_seedLock)
            {
                seed = _globalRandom.Next();
            }
            return new Random(seed);
        });

        /// <summary>
        /// Returns a random floating-point number that is greater than or equal to 'min', and less than 'max'.
        /// </summary>
        /// <param name="min">The value returned will be greater than or equal to 'min'</param>
        /// <param name="max">The value returned will be less than 'max'</param>
        public static double Next(float min, float max)
        {
            // for ranges other than 1, (max - upper bound) will be scaled by the range
            return _threadRandom.Value.NextDouble() * (max - min) + min;
        }

        /// <summary>
        /// Returns a random integer between min and max, inclusive
        /// </summary>
        /// <param name="min">The minimum possible value to return</param>
        /// <param name="max">The maximum possible value to return</param>
        public static int Next(int min, int max)
        {
            return _threadRandom.Value.Next(min, max + 1);
        }

        public static double NextInterval(float qualityMod)
        {
            return Math.Max(0.0, _threadRandom.Value.NextDouble() - qualityMod);
        }

        /// <summary>
        /// The maximum possible double < 1.0
        /// </summary>
        private static readonly double maxExclusive = 0.9999999999999999;

        public static double NextIntervalMax(float qualityMod)
        {
            return Math.Min(maxExclusive, _threadRandom.Value.NextDouble() + qualityMod);
        }
    }
}
