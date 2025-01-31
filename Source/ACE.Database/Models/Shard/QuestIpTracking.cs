using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ACE.Database.Models.World;

namespace ACE.Database.Models.Shard
{
    [Table("quest_ip_tracking")]
    public class QuestIpTracking
    {
        [Key] // Composite primary key must be defined in OnModelCreating, remove multiple [Key] attributes
        [Column("quest_id")]
        public uint QuestId { get; set; }

        [Column("ip_address")]
        public string IpAddress { get; set; }

        [Column("solves_count")]
        public int SolvesCount { get; set; }

        [Column("last_solve_time")]
        public DateTime? LastSolveTime { get; set; }

        // Foreign Key Relationship to Quest
        [ForeignKey("QuestId")]
        public virtual Quest Quest { get; set; }
    }
}
