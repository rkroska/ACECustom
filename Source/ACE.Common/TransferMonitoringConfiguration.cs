using System;
using System.Collections.Generic;

namespace ACE.Server.Managers
{
    public class TransferMonitoringConfig
    {
        // Configuration constants
        private const int DEFAULT_SUSPICIOUS_TRANSFER_THRESHOLD = 100000; // 100k value threshold
        private const int DEFAULT_TIME_WINDOW_HOURS = 24; // 24 hour time window
        private const int DEFAULT_PATTERN_DETECTION_THRESHOLD = 10; // 10 transfers to same player
        
        public static TransferMonitoringConfig Default => new TransferMonitoringConfig();

        // Suspicious transfer detection settings
        public int SuspiciousTransferThreshold { get; set; } = DEFAULT_SUSPICIOUS_TRANSFER_THRESHOLD;
        public int TimeWindowHours { get; set; } = DEFAULT_TIME_WINDOW_HOURS;
        public int PatternDetectionThreshold { get; set; } = DEFAULT_PATTERN_DETECTION_THRESHOLD;

        // Monitoring settings
        public bool EnableTransferLogging { get; set; } = true;
        public bool EnableSuspiciousDetection { get; set; } = true;
        public bool EnableAdminNotifications { get; set; } = true;

        // Database settings
        public bool EnableTransferSummaries { get; set; } = true;
        public bool EnableTransferLogs { get; set; } = true;

        // Item tracking settings
        public bool EnableItemTracking { get; set; } = true;
        public List<string> TrackedItems { get; set; } = new List<string>();
        public bool TrackAllItems { get; set; } = false; // If true, track all items regardless of list

        public TransferMonitoringConfig()
        {
            // Initialize with default values
        }

        public TransferMonitoringConfig(
            int suspiciousTransferThreshold = 100000,
            int timeWindowHours = 24,
            int patternDetectionThreshold = 10,
            bool enableTransferLogging = true,
            bool enableSuspiciousDetection = true,
            bool enableAdminNotifications = true,
            bool enableTransferSummaries = true,
            bool enableTransferLogs = true,
            bool enableItemTracking = true,
            List<string> trackedItems = null,
            bool trackAllItems = false)
        {
            SuspiciousTransferThreshold = suspiciousTransferThreshold;
            TimeWindowHours = timeWindowHours;
            PatternDetectionThreshold = patternDetectionThreshold;
            EnableTransferLogging = enableTransferLogging;
            EnableSuspiciousDetection = enableSuspiciousDetection;
            EnableAdminNotifications = enableAdminNotifications;
            EnableTransferSummaries = enableTransferSummaries;
            EnableTransferLogs = enableTransferLogs;
            EnableItemTracking = enableItemTracking;
            TrackedItems = trackedItems ?? new List<string>();
            TrackAllItems = trackAllItems;
        }
    }
}
