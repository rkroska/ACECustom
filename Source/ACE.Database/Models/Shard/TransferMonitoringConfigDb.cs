using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACE.Database.Models.Shard
{
    [Table("transfer_monitoring_configs")]
    public partial class TransferMonitoringConfigDb
    {
        [Key]
        public int Id { get; set; }

        // Suspicious transfer detection settings
        public int SuspiciousTransferThreshold { get; set; } = 100000;
        public int TimeWindowHours { get; set; } = 24;
        public int PatternDetectionThreshold { get; set; } = 10;

        // Monitoring settings
        public bool EnableTransferLogging { get; set; } = true;
        public bool EnableSuspiciousDetection { get; set; } = true;
        public bool EnableAdminNotifications { get; set; } = true;

        // Database settings
        public bool EnableTransferSummaries { get; set; } = true;
        public bool EnableTransferLogs { get; set; } = true;

        // Item tracking settings
        public bool EnableItemTracking { get; set; } = true;
        public bool TrackAllItems { get; set; } = false;

        // Timestamps
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
    }
}

