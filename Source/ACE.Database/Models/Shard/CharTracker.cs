using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACE.Database.Models.Shard
{
	[Table("char_tracker")]
	public partial class CharTracker
	{
		[Key]
		public int Id { get; set; }

		[Required]
		public uint CharacterId { get; set; }

		[StringLength(255)]
		public string AccountName { get; set; }

		[StringLength(255)]
		public string CharacterName { get; set; }

		[StringLength(50)]
		public string LoginIP { get; set; }

		public DateTime LoginTimestamp { get; set; }

		public int ConnectionDuration { get; set; }

		[StringLength(50)]
		public string Landblock { get; set; }
	}
}


