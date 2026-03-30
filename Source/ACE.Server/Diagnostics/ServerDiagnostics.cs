using System.Threading;

namespace ACE.Server.Diagnostics
{
    /// <summary>
    /// Lightweight process-wide counters for production triage (dotnet-counters custom sources can wrap these later).
    /// </summary>
    public static class ServerDiagnostics
    {
        public static long SessionLoginRejectedSessionPoolFull;

        public static long GeneratorInitSelectStallAborts;
        /// <summary>Observed when init power-up sees GetTotalProbability()==0 before satisfying InitCreate (often normal for linkable generators; we do not fault).</summary>
        public static long GeneratorInitZeroTotalProbabilityObserved;
        public static long GeneratorInitSelectMaxIterationsHit;
        public static long GeneratorServerFaults;
        public static long GeneratorSpawnFailuresRecorded;

        public static long LogMessagesSuppressedByRateLimiter;
    }
}
