using System.Globalization;
using System.Text;
using System.Threading;

namespace ACE.Server.Diagnostics
{
    /// <summary>
    /// Lightweight process-wide counters for production triage (dotnet-counters custom sources can wrap these later).
    /// Writers use Interlocked increments; readers should call <see cref="GetSnapshot"/> for a consistent view.
    /// </summary>
    public static class ServerDiagnostics
    {
        public static long SessionLoginRejectedSessionPoolFull;
        public static long SessionPoolRejectEarly;
        public static long SessionCreatedTotal;
        public static long SessionRemovedTotal;
        public static long SessionStaleSweeperRemoved;
        public static long SessionStuckTerminationForced;
        public static long SessionPoolEmergencyShutdownTriggered;
        public static long TrackerPingHandled;
        public static long SessionUnauthenticatedPeak;

        public static readonly long[] SessionRemovedByReason = new long[64];

        public static long GeneratorInitSelectStallAborts;

        /// <summary>Observed when init power-up sees GetTotalProbability()==0 before satisfying InitCreate (often normal for linkable generators; we do not fault).</summary>
        public static long GeneratorInitZeroTotalProbabilityObserved;

        public static long GeneratorInitSelectMaxIterationsHit;
        public static long GeneratorServerFaults;
        public static long GeneratorSpawnFailuresRecorded;

        public static long LogMessagesSuppressedByRateLimiter;

        /// <summary>Atomically read all counters plus rate-limiter key estimate.</summary>
        public static ServerDiagnosticsSnapshot GetSnapshot() => new ServerDiagnosticsSnapshot(
            Interlocked.Read(ref SessionLoginRejectedSessionPoolFull),
            Interlocked.Read(ref SessionPoolRejectEarly),
            Interlocked.Read(ref SessionCreatedTotal),
            Interlocked.Read(ref SessionRemovedTotal),
            Interlocked.Read(ref SessionStaleSweeperRemoved),
            Interlocked.Read(ref SessionStuckTerminationForced),
            Interlocked.Read(ref SessionPoolEmergencyShutdownTriggered),
            Interlocked.Read(ref TrackerPingHandled),
            Interlocked.Read(ref SessionUnauthenticatedPeak),
            Interlocked.Read(ref GeneratorInitSelectStallAborts),
            Interlocked.Read(ref GeneratorInitZeroTotalProbabilityObserved),
            Interlocked.Read(ref GeneratorInitSelectMaxIterationsHit),
            Interlocked.Read(ref GeneratorServerFaults),
            Interlocked.Read(ref GeneratorSpawnFailuresRecorded),
            Interlocked.Read(ref LogMessagesSuppressedByRateLimiter),
            LogRateLimiter.EstimatedEntryCount);

        /// <summary>One-line log-friendly form.</summary>
        public static string FormatSnapshotLogLine(ServerDiagnosticsSnapshot? snapshot = null)
        {
            var s = snapshot ?? GetSnapshot();
            return s.ToString();
        }
    }

    /// <summary>Point-in-time copy of <see cref="ServerDiagnostics"/> counters.</summary>
    public readonly struct ServerDiagnosticsSnapshot
    {
        public long SessionLoginRejectedSessionPoolFull { get; }
        public long SessionPoolRejectEarly { get; }
        public long SessionCreatedTotal { get; }
        public long SessionRemovedTotal { get; }
        public long SessionStaleSweeperRemoved { get; }
        public long SessionStuckTerminationForced { get; }
        public long SessionPoolEmergencyShutdownTriggered { get; }
        public long TrackerPingHandled { get; }
        public long SessionUnauthenticatedPeak { get; }
        public long GeneratorInitSelectStallAborts { get; }
        public long GeneratorInitZeroTotalProbabilityObserved { get; }
        public long GeneratorInitSelectMaxIterationsHit { get; }
        public long GeneratorServerFaults { get; }
        public long GeneratorSpawnFailuresRecorded { get; }
        public long LogMessagesSuppressedByRateLimiter { get; }
        public int LogRateLimiterApproximateEntries { get; }

        public ServerDiagnosticsSnapshot(
            long sessionLoginRejectedSessionPoolFull,
            long sessionPoolRejectEarly,
            long sessionCreatedTotal,
            long sessionRemovedTotal,
            long sessionStaleSweeperRemoved,
            long sessionStuckTerminationForced,
            long sessionPoolEmergencyShutdownTriggered,
            long trackerPingHandled,
            long sessionUnauthenticatedPeak,
            long generatorInitSelectStallAborts,
            long generatorInitZeroTotalProbabilityObserved,
            long generatorInitSelectMaxIterationsHit,
            long generatorServerFaults,
            long generatorSpawnFailuresRecorded,
            long logMessagesSuppressedByRateLimiter,
            int logRateLimiterApproximateEntries)
        {
            SessionLoginRejectedSessionPoolFull = sessionLoginRejectedSessionPoolFull;
            SessionPoolRejectEarly = sessionPoolRejectEarly;
            SessionCreatedTotal = sessionCreatedTotal;
            SessionRemovedTotal = sessionRemovedTotal;
            SessionStaleSweeperRemoved = sessionStaleSweeperRemoved;
            SessionStuckTerminationForced = sessionStuckTerminationForced;
            SessionPoolEmergencyShutdownTriggered = sessionPoolEmergencyShutdownTriggered;
            TrackerPingHandled = trackerPingHandled;
            SessionUnauthenticatedPeak = sessionUnauthenticatedPeak;
            GeneratorInitSelectStallAborts = generatorInitSelectStallAborts;
            GeneratorInitZeroTotalProbabilityObserved = generatorInitZeroTotalProbabilityObserved;
            GeneratorInitSelectMaxIterationsHit = generatorInitSelectMaxIterationsHit;
            GeneratorServerFaults = generatorServerFaults;
            GeneratorSpawnFailuresRecorded = generatorSpawnFailuresRecorded;
            LogMessagesSuppressedByRateLimiter = logMessagesSuppressedByRateLimiter;
            LogRateLimiterApproximateEntries = logRateLimiterApproximateEntries;
        }

        /// <summary>Human-readable multi-line text for chat or console.</summary>
        public string ToDisplayString()
        {
            static string L(long v) => v.ToString(CultureInfo.InvariantCulture);
            var sb = new StringBuilder(512);
            sb.AppendLine("[ServerDiagnostics snapshot]");
            sb.AppendLine("SessionCreatedTotal: " + L(SessionCreatedTotal));
            sb.AppendLine("SessionRemovedTotal: " + L(SessionRemovedTotal));
            sb.AppendLine("SessionLoginRejectedSessionPoolFull: " + L(SessionLoginRejectedSessionPoolFull));
            sb.AppendLine("SessionPoolRejectEarly: " + L(SessionPoolRejectEarly));
            sb.AppendLine("SessionStaleSweeperRemoved: " + L(SessionStaleSweeperRemoved));
            sb.AppendLine("SessionStuckTerminationForced: " + L(SessionStuckTerminationForced));
            sb.AppendLine("SessionPoolEmergencyShutdownTriggered: " + L(SessionPoolEmergencyShutdownTriggered));
            sb.AppendLine("TrackerPingHandled: " + L(TrackerPingHandled));
            sb.AppendLine("SessionUnauthenticatedPeak: " + L(SessionUnauthenticatedPeak));
            sb.AppendLine("GeneratorInitSelectStallAborts: " + L(GeneratorInitSelectStallAborts));
            sb.AppendLine("GeneratorInitZeroTotalProbabilityObserved: " + L(GeneratorInitZeroTotalProbabilityObserved));
            sb.AppendLine("GeneratorInitSelectMaxIterationsHit: " + L(GeneratorInitSelectMaxIterationsHit));
            sb.AppendLine("GeneratorServerFaults: " + L(GeneratorServerFaults));
            sb.AppendLine("GeneratorSpawnFailuresRecorded: " + L(GeneratorSpawnFailuresRecorded));
            sb.AppendLine("LogMessagesSuppressedByRateLimiter: " + L(LogMessagesSuppressedByRateLimiter));
            sb.Append("LogRateLimiterApproximateEntries: ").Append(LogRateLimiterApproximateEntries.ToString(CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        public override string ToString() => ToDisplayString().Replace("\r\n", " | ").Trim();
    }
}
