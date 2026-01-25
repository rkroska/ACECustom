using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACE.Database.Models.Shard
{
    /// <summary>
    /// Creature Blacklist - tracks WCIDs that are blocked from capture and/or shiny variants
    /// </summary>
    [Table("creature_blacklist")]
    public partial class CreatureBlacklist
    {
        /// <summary>
        /// WeenieClassId of the creature to blacklist
        /// </summary>
        [Key]
        [Column("wcid")]
        public uint Wcid { get; set; }

        /// <summary>
        /// If true, this creature cannot be captured
        /// </summary>
        [Column("no_capture")]
        public bool NoCapture { get; set; } = false;

        /// <summary>
        /// If true, this creature cannot spawn as a shiny variant
        /// </summary>
        [Column("no_shiny")]
        public bool NoShiny { get; set; } = false;

        /// <summary>
        /// Optional reason for the blacklist entry
        /// </summary>
        [StringLength(100)]
        [Column("reason")]
        public string Reason { get; set; }

        /// <summary>
        /// Who added this entry
        /// </summary>
        [StringLength(50)]
        [Column("added_by")]
        public string AddedBy { get; set; }

        /// <summary>
        /// When this entry was added
        /// </summary>
        [Column("added_at")]
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}
