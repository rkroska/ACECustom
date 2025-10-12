using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACE.Database.Models.Shard
{
    [Table("transfer_summaries")]
    public partial class TransferSummary
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string FromPlayerName { get; set; }

        [StringLength(255)]
        public string FromPlayerAccount { get; set; }

        [Required]
        [StringLength(255)]
        public string ToPlayerName { get; set; }

        [StringLength(255)]
        public string ToPlayerAccount { get; set; }

        [Required]
        [StringLength(255)]
        public string TransferType { get; set; }

        public int TotalTransfers { get; set; }

        public long TotalQuantity { get; set; }

        public long TotalValue { get; set; }

        public DateTime FirstTransfer { get; set; }

        public DateTime LastTransfer { get; set; }

        public int SuspiciousTransfers { get; set; }

        public bool IsSuspicious { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime UpdatedDate { get; set; }
    }
}

