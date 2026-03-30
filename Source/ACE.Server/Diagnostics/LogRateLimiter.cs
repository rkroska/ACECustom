using System;
using System.Collections.Concurrent;
using System.Threading;

namespace ACE.Server.Diagnostics
{
    /// <summary>
    /// Per-key rate limiting for hot paths that would otherwise spam log4net and worsen lock/log contention.
    /// Inactive keys are pruned on a timer to bound memory use.
    /// </summary>
    public static class LogRateLimiter
    {
        /// <summary>Remove keys with no activity for at least this long.</summary>
        public static TimeSpan PruneAfterIdle { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>How often the prune scan runs.</summary>
        public static TimeSpan PruneTimerPeriod { get; set; } = TimeSpan.FromMinutes(5);

        private sealed class Entry
        {
            public long WindowStartTicks;
            public int SuppressedInWindow;
            /// <summary>UTC ticks updated on every <see cref="ShouldEmit"/> use.</summary>
            public long LastActivityTicks;
        }

        private static readonly ConcurrentDictionary<string, Entry> Entries = new ConcurrentDictionary<string, Entry>();

        private static readonly Timer PruneTimer;

        static LogRateLimiter()
        {
            PruneTimer = new Timer(PruneStaleEntries, null, PruneTimerPeriod, PruneTimerPeriod);
        }

        /// <summary>Approximate number of rate-limit keys currently tracked.</summary>
        public static int EstimatedEntryCount => Entries.Count;

        private static void PruneStaleEntries(object state)
        {
            try
            {
                var idleTicks = PruneAfterIdle.Ticks;
                if (idleTicks <= 0)
                    return;

                var now = DateTime.UtcNow.Ticks;
                foreach (var kv in Entries.ToArray())
                {
                    var entry = kv.Value;
                    lock (entry)
                    {
                        if (now - entry.LastActivityTicks >= idleTicks)
                            Entries.TryRemove(kv.Key, out _);
                    }
                }
            }
            catch
            {
                // Avoid crashing timer thread; next tick will retry.
            }
        }

        /// <summary>
        /// Returns true if the caller should emit the full log line now.
        /// When false, <paramref name="suppressedThisWindow"/> is incremented for diagnostics.
        /// </summary>
        public static bool ShouldEmit(string key, TimeSpan window, out int suppressedThisWindow)
        {
            suppressedThisWindow = 0;
            var now = DateTime.UtcNow.Ticks;
            var e = Entries.GetOrAdd(key, _ => new Entry { WindowStartTicks = now, LastActivityTicks = now });

            lock (e)
            {
                e.LastActivityTicks = now;

                if (now - e.WindowStartTicks >= window.Ticks)
                {
                    suppressedThisWindow = e.SuppressedInWindow;
                    e.SuppressedInWindow = 0;
                    e.WindowStartTicks = now;
                    return true;
                }

                e.SuppressedInWindow++;
                Interlocked.Increment(ref ServerDiagnostics.LogMessagesSuppressedByRateLimiter);
                suppressedThisWindow = e.SuppressedInWindow;
                return false;
            }
        }
    }
}
