using System;

namespace ACE.Database.Models.World
{
    public class QuestIpTracking
    {
        public uint QuestId { get; set; }
        public string IpAddress { get; set; }
        public int SolvesCount { get; set; }
        public DateTime? LastSolveTime { get; set; }

        public Quest Quest { get; set; }
    }


}
