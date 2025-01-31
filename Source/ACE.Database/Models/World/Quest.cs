using System;
using System.Collections.Generic;
using ACE.Database.Models.Shard;

#nullable disable

namespace ACE.Database.Models.World
{
    public partial class Quest
    {
        public uint Id { get; set; }
        public string Name { get; set; }
        public uint MinDelta { get; set; }
        public int MaxSolves { get; set; }
        public string Message { get; set; }
        public DateTime LastModified { get; set; }
        public bool IsIpRestricted { get; set; }
        public int? IpLootLimit { get; set; }

        public virtual ICollection<QuestIpTracking> QuestIpTrackings { get; set; } = new HashSet<QuestIpTracking>();
    }
}
