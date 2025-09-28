using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ACE.Database.Models.Shard;
using ACE.Server.WorldObjects;
using ACE.Server.Managers;
using log4net;

namespace ACE.Server.Managers
{
    public static class TransferMonitor
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        
        // Simple counters for rate monitoring
        private static int transfersLastMinute = 0;
        private static int suspiciousLastHour = 0;
        private static int highValueLastDay = 0;
        private static DateTime lastMinuteReset = DateTime.UtcNow;
        private static DateTime lastHourReset = DateTime.UtcNow;
        private static DateTime lastDayReset = DateTime.UtcNow;

        public static void RecordTransfer(TransferLog transferLog)
        {
            try
            {
                // Update counters (thread-safe)
                Interlocked.Increment(ref transfersLastMinute);
                // Note: Suspicious and value tracking removed - focusing on account/character age patterns


                // Check for alerts
                CheckForAlerts();
            }
            catch (Exception ex)
            {
                log.Error($"Error recording transfer: {ex.Message}");
            }
        }


        private static void CheckForAlerts()
        {
            var now = DateTime.UtcNow;

            // Reset counters based on time windows (thread-safe)
            if (now - lastMinuteReset >= TimeSpan.FromMinutes(1))
            {
                Interlocked.Exchange(ref transfersLastMinute, 0);
                lastMinuteReset = now;
            }

            if (now - lastHourReset >= TimeSpan.FromHours(1))
            {
                Interlocked.Exchange(ref suspiciousLastHour, 0);
                lastHourReset = now;
            }

            if (now - lastDayReset >= TimeSpan.FromDays(1))
            {
                Interlocked.Exchange(ref highValueLastDay, 0);
                lastDayReset = now;
            }

            // Check for alert conditions (thread-safe reads)
            var currentTransfers = Interlocked.Add(ref transfersLastMinute, 0);
            var currentSuspicious = Interlocked.Add(ref suspiciousLastHour, 0);
            var currentHighValue = Interlocked.Add(ref highValueLastDay, 0);

            if (currentTransfers > 10)
            {
                log.Warn($"High transfer rate detected: {currentTransfers} transfers in the last minute");
            }

            if (currentSuspicious > 5)
            {
                log.Warn($"High suspicious activity detected: {currentSuspicious} suspicious transfers in the last hour");
            }

            if (currentHighValue > 3)
            {
                log.Warn($"High value activity detected: {currentHighValue} high-value transfers in the last day");
            }
        }

        // Public properties for admin commands (thread-safe reads)
        public static int TotalTransfersToday => Interlocked.Add(ref transfersLastMinute, 0);
        public static int SuspiciousTransfersToday => Interlocked.Add(ref suspiciousLastHour, 0);
        public static int TotalValueToday => Interlocked.Add(ref highValueLastDay, 0);

        // Public methods for admin commands (thread-safe reads)
        public static int GetTransferRate()
        {
            return Interlocked.Add(ref transfersLastMinute, 0);
        }

        public static int GetSuspiciousRate()
        {
            return Interlocked.Add(ref suspiciousLastHour, 0);
        }

        public static int GetHighValueRate()
        {
            return Interlocked.Add(ref highValueLastDay, 0);
        }

        public static void ResetCounters()
        {
            Interlocked.Exchange(ref transfersLastMinute, 0);
            Interlocked.Exchange(ref suspiciousLastHour, 0);
            Interlocked.Exchange(ref highValueLastDay, 0);
            lastMinuteReset = DateTime.UtcNow;
            lastHourReset = DateTime.UtcNow;
            lastDayReset = DateTime.UtcNow;
        }
    }
}
