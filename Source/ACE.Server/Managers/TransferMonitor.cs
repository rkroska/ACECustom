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
        
        // Lock to guard DateTime field access and reset logic
        private static readonly object resetLock = new object();
        
        // Alert thresholds
        private const int HighTransferRateThreshold = 10;

        public static void RecordTransfer(TransferLog transferLog)
        {
            try
            {
                // Align windows first to avoid dropping the first event of a fresh window
                CheckForAlerts();
                
                // Update counters (thread-safe)
                Interlocked.Increment(ref transfersLastMinute);
                
                // Evaluate alerts including this event
                var currentTransfers = Interlocked.Add(ref transfersLastMinute, 0);
                if (currentTransfers > HighTransferRateThreshold)
                {
                    log.Warn($"High transfer rate detected: {currentTransfers} transfers in the last minute");
                }
                
                // Note: Suspicious and value tracking removed - focusing on account/character age patterns
            }
            catch (Exception ex)
            {
                log.Error($"Error recording transfer: {ex.Message}");
            }
        }


        private static void CheckForAlerts()
        {
            var now = DateTime.UtcNow;

            // Reset counters based on time windows (atomic check-and-reset)
            lock (resetLock)
            {
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
            }

            // Alert evaluation handled in RecordTransfer() after increment
            // Note: Suspicious and high-value alerting disabled pending reimplementation
            // The counters are never incremented, so these alerts would be misleading
        }

        // Public properties for admin commands (thread-safe reads)
        public static int TransfersLastMinute => Interlocked.Add(ref transfersLastMinute, 0);
        public static int SuspiciousLastHour => Interlocked.Add(ref suspiciousLastHour, 0);
        public static int HighValueLastDay => Interlocked.Add(ref highValueLastDay, 0);

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
            lock (resetLock)
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
}
