using System;
using System.Collections.Generic;
using System.Linq;
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
                // Update counters
                transfersLastMinute++;
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

            // Reset counters based on time windows
            if (now - lastMinuteReset >= TimeSpan.FromMinutes(1))
            {
                transfersLastMinute = 0;
                lastMinuteReset = now;
            }

            if (now - lastHourReset >= TimeSpan.FromHours(1))
            {
                suspiciousLastHour = 0;
                lastHourReset = now;
            }

            if (now - lastDayReset >= TimeSpan.FromDays(1))
            {
                highValueLastDay = 0;
                lastDayReset = now;
            }

            // Check for alert conditions
            if (transfersLastMinute > 10)
            {
                log.Warn($"High transfer rate detected: {transfersLastMinute} transfers in the last minute");
            }

            if (suspiciousLastHour > 5)
            {
                log.Warn($"High suspicious activity detected: {suspiciousLastHour} suspicious transfers in the last hour");
            }

            if (highValueLastDay > 3)
            {
                log.Warn($"High value activity detected: {highValueLastDay} high-value transfers in the last day");
            }
        }

        // Public properties for admin commands
        public static int TotalTransfersToday => transfersLastMinute;
        public static int SuspiciousTransfersToday => suspiciousLastHour;
        public static int TotalValueToday => highValueLastDay;

        // Public methods for admin commands
        public static int GetTransferRate()
        {
            return transfersLastMinute;
        }

        public static int GetSuspiciousRate()
        {
            return suspiciousLastHour;
        }

        public static int GetHighValueRate()
        {
            return highValueLastDay;
        }

        public static void ResetCounters()
        {
            transfersLastMinute = 0;
            suspiciousLastHour = 0;
            highValueLastDay = 0;
            lastMinuteReset = DateTime.UtcNow;
            lastHourReset = DateTime.UtcNow;
            lastDayReset = DateTime.UtcNow;
        }
    }
}
