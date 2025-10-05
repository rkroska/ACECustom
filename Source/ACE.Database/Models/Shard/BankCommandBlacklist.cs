using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACE.Database.Models.Shard
{
    [Table("transfer_blacklist")]
    public partial class BankCommandBlacklist
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string PlayerName { get; set; }

        [StringLength(255)]
        public string AccountName { get; set; }

        [Required]
        [StringLength(255)]
        public string Reason { get; set; }

        [Required]
        [StringLength(255)]
        public string AddedBy { get; set; }

        public DateTime CreatedDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
