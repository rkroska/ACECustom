using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ACE.Database.Models.Shard
{
    [Table("transfer_logs")]
    public partial class TransferLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string TransferType { get; set; }

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
        public string ItemName { get; set; }

        public long Quantity { get; set; }

        public DateTime Timestamp { get; set; }

        public DateTime? FromAccountCreatedDate { get; set; }

        public DateTime? ToAccountCreatedDate { get; set; }

        public DateTime? FromCharacterCreatedDate { get; set; }

        public DateTime? ToCharacterCreatedDate { get; set; }

        [StringLength(1000)]
        public string AdditionalData { get; set; }

        [StringLength(45)]
        public string FromPlayerIP { get; set; }

        [StringLength(45)]
        public string ToPlayerIP { get; set; }
    }
}

