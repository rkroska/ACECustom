using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACE.Database.Models.Shard
{
    /// <summary>
    /// Pet Registry - tracks unique creature essences registered per account
    /// </summary>
    [Table("pet_registry")]
    public partial class PetRegistry
    {
        /// <summary>
        /// Account ID of the player who registered this essence
        /// </summary>
        [Column("account_id")]
        public uint AccountId { get; set; }

        /// <summary>
        /// WeenieClassId of the captured creature
        /// </summary>
        [Column("wcid")]
        public uint Wcid { get; set; }

        /// <summary>
        /// Name of the creature for display purposes
        /// </summary>
        [Required]
        [StringLength(100)]
        [Column("creature_name")]
        public string CreatureName { get; set; }

        /// <summary>
        /// Creature Type (Drudge, Banderling, etc.) for tracking first-of-type QB rewards
        /// </summary>
        [Column("creature_type")]
        public uint? CreatureType { get; set; }

        /// <summary>
        /// When this creature was registered
        /// </summary>
        [Column("registered_at")]
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether this was a shiny variant capture
        /// </summary>
        [Column("is_shiny")]
        public bool IsShiny { get; set; } = false;
    }
}
