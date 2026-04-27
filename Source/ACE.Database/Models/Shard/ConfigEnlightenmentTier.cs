using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACE.Database.Models.Shard
{
    /// <summary>
    /// Defines luminance cost, consumable item, and optional quest gate for a band of target enlightenment levels (T = current enlightenment + 1).
    /// Rows must partition [1, ∞) with no gaps or overlaps; only the last row may use MaxTargetEnl = NULL (open-ended).
    /// </summary>
    [Table("config_enlightenment_tier")]
    public partial class ConfigEnlightenmentTier
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; }

        [Column("min_target_enl")]
        public int MinTargetEnl { get; set; }

        /// <summary>NULL = no upper bound (only valid on the final row).</summary>
        [Column("max_target_enl")]
        public int? MaxTargetEnl { get; set; }

        [Column("lum_base_per_target")]
        public long LumBasePerTarget { get; set; }

        [Column("lum_step_anchor")]
        public int? LumStepAnchor { get; set; }

        [Column("lum_step_every")]
        public int? LumStepEvery { get; set; }

        /// <summary>Added per step to a 1.0 base multiplier (e.g. 0.5 matches legacy 301+ scaling).</summary>
        [Column("lum_step_increment", TypeName = "decimal(10,4)")]
        public decimal? LumStepIncrement { get; set; }

        [Column("item_wcid")]
        public int? ItemWcid { get; set; }

        /// <summary>Required stack count = max(0, T - ItemCountTargetMinus). Required when ItemWcid is set.</summary>
        [Column("item_count_target_minus")]
        public int? ItemCountTargetMinus { get; set; }

        [StringLength(80)]
        [Column("item_label")]
        public string ItemLabel { get; set; }

        [StringLength(100)]
        [Column("quest_stamp")]
        public string QuestStamp { get; set; }

        [StringLength(255)]
        [Column("quest_failure_message")]
        public string QuestFailureMessage { get; set; }
    }
}
