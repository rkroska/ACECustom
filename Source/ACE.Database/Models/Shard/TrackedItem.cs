using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACE.Database.Models.Shard
{
    [Table("tracked_items")]
    public partial class TrackedItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string ItemName { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime UpdatedDate { get; set; }

        public bool IsActive { get; set; } = true;
    }
}

