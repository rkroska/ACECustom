using ACE.Database;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Timers;
using ACE.Entity.Enum;
using ACE.Server.Network;
using log4net;

namespace ACE.Server.Managers
{
    public static class MemoryMonitor
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static Timer _monitorTimer;
        private static readonly ConcurrentDictionary<uint, Session> _monitoringSessions = new ConcurrentDictionary<uint, Session>();

        private static long _lastTotalMemory;
        private static int _consecutiveIncreases;
        private static DateTime _monitorStartTime;

        public static void Start(Session session)
        {
            _monitoringSessions.TryAdd(session.Player.Guid.Full, session);

            if (_monitorTimer == null)
            {
                _monitorTimer = new Timer(30000); // 30 seconds
                _monitorTimer.Elapsed += MonitorMemory;
                _monitorTimer.Start();
                _monitorStartTime = DateTime.UtcNow;
                _lastTotalMemory = GC.GetTotalMemory(false);

                log.Info("Memory monitor started");
            }
        }

        public static void Stop(Session session)
        {
            _monitoringSessions.TryRemove(session.Player.Guid.Full, out _);

            if (_monitoringSessions.IsEmpty && _monitorTimer != null)
            {
                _monitorTimer.Stop();
                _monitorTimer.Dispose();
                _monitorTimer = null;

                log.Info("Memory monitor stopped");
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
