using System;
using System.Collections.Concurrent;
using System.Threading;

namespace ACE.Server.Diagnostics
{
    /// <summary>
    /// Per-key rate limiting for hot paths that would otherwise spam log4net and worsen lock/log contention.
    /// </summary>
    public static class LogRateLimiter
    {
        private sealed class Entry
        {
            public long WindowStartTicks;
            public int SuppressedInWindow;
        }

        private static readonly ConcurrentDictionary<string, Entry> Entries = new ConcurrentDictionary<string, Entry>();

        /// <summary>
        /// Returns true if the caller should emit the full log line now.
        /// When false, <paramref name="suppressedThisWindow"/> is incremented for diagnostics.
        /// </summary>
        public static bool ShouldEmit(string key, TimeSpan window, out int suppressedThisWindow)
        {
            suppressedThisWindow = 0;
            var now = DateTime.UtcNow.Ticks;
            var e = Entries.GetOrAdd(key, _ => new Entry { WindowStartTicks = now });

            lock (e)
            {
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
