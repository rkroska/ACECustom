using ACE.Database;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Timers;
using ACE.Entity.Enum;
using ACE.Server.Network;
using log4net;

namespace ACE.Server.Managers
{
    public static class MemoryMonitor
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static System.Timers.Timer _monitorTimer;
        private static readonly ConcurrentDictionary<uint, Session> _monitoringSessions = new ConcurrentDictionary<uint, Session>();

        private static long _lastTotalMemory;
        private static int _consecutiveIncreases;
        private static DateTime _monitorStartTime;

        public static void Start(Session session)
        {
            // Null check - return immediately if session or player is null
            if (session?.Player == null)
                return;

            // Use AddOrUpdate to ensure new session replaces any existing entry for the same player GUID
            // This handles cases where a player disconnects and reconnects
            _monitoringSessions.AddOrUpdate(
                session.Player.Guid.Full,
                session,
                (key, existingSession) => session);

            // Thread-safe timer creation using Interlocked.CompareExchange
            if (_monitorTimer == null)
            {
                // Create a new timer instance
                var newTimer = new System.Timers.Timer(30000); // 30 seconds
                newTimer.Elapsed += MonitorMemory;

                // Atomically set _monitorTimer only if it's still null
                var existingTimer = Interlocked.CompareExchange(ref _monitorTimer, newTimer, null);

                if (existingTimer == null)
                {
                    // We successfully set the timer, so start it and initialize state
                    newTimer.Start();
                    _monitorStartTime = DateTime.UtcNow;
                    _lastTotalMemory = GC.GetTotalMemory(false);
                    log.Info("Memory monitor started");
                }
                else
                {
                    // Another thread already created the timer, dispose our unused instance
                    newTimer.Dispose();
                }
            }
        }

        public static void Stop(Session session)
        {
            // Null check - return immediately if session or player is null
            if (session?.Player == null)
                return;

            // Remove the entry by GUID to clear stale entries
            _monitoringSessions.TryRemove(session.Player.Guid.Full, out _);

            if (_monitoringSessions.IsEmpty)
            {
                // Capture the current timer reference
                var currentTimer = _monitorTimer;

                if (currentTimer != null)
                {
                    // Atomically set _monitorTimer to null only if it still references the same timer
                    var replacedTimer = Interlocked.CompareExchange(ref _monitorTimer, null, currentTimer);

                    if (replacedTimer == currentTimer)
                    {
                        // We successfully cleared the timer, so stop and dispose it
                        currentTimer.Stop();
                        currentTimer.Dispose();
                        log.Info("Memory monitor stopped");
                    }
                    // If replacedTimer != currentTimer, another thread already replaced it,
                    // so we don't dispose (that thread is responsible for cleanup)
                }
            }
        }

        private static void MonitorMemory(object sender, ElapsedEventArgs e)
        {
            try
            {
                var gcMemInfo = GC.GetGCMemoryInfo();
                var currentMemory = GC.GetTotalMemory(false);
                var process = Process.GetCurrentProcess();

                var sb = new StringBuilder();
                sb.AppendLine("=== Memory Monitor Report ===");
                sb.AppendLine($"Runtime: {(DateTime.UtcNow - _monitorStartTime).TotalMinutes:N1} minutes");
                sb.AppendLine($"Memory: {currentMemory / 1024 / 1024:N2} MB");
                sb.AppendLine($"Working Set: {process.WorkingSet64 / 1024 / 1024:N2} MB");
                sb.AppendLine($"Gen 2 Collections: {GC.CollectionCount(2):N0}");

                // Detect steady growth
                if (currentMemory > _lastTotalMemory)
                {
                    _consecutiveIncreases++;
                    var growth = currentMemory - _lastTotalMemory;
                    sb.AppendLine($"Growth: +{growth / 1024 / 1024:N2} MB (consecutive: {_consecutiveIncreases})");

                    if (_consecutiveIncreases >= 6) // 3 minutes of growth
                    {
                        sb.AppendLine("⚠️ WARNING: Sustained memory growth detected!");
                        log.Warn($"Memory leak suspected - consecutive increases: {_consecutiveIncreases}");
                    }
                }
                else
                {
                    _consecutiveIncreases = 0;
                    sb.AppendLine("Growth: Stable or decreasing");
                }

                _lastTotalMemory = currentMemory;

                // Cache statistics
                sb.AppendLine();
                sb.AppendLine("=== Cache Status ===");
                try
                {
                    sb.AppendLine($"Weenies: {DatabaseManager.World.GetWeenieCacheCount():N0}");
                    sb.AppendLine($"Landblocks: {LandblockManager.GetLoadedLandblocks().Count:N0}");
                    sb.AppendLine($"Players: {PlayerManager.GetOnlineCount():N0}");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Error: {ex.Message}");
                }

                // Send to all monitoring sessions
                var message = sb.ToString();
                log.Info(message);

                foreach (var session in _monitoringSessions.Values)
                {
                    try
                    {
                        if (session?.Player != null)
                        {
                            session.Player.SendMessage(message, ChatMessageType.System);
                        }
                    }
                    catch
                    {
                        // Session may have disconnected
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error in memory monitor: {ex}");
            }
        }
    }
}
