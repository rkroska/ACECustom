using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Database.Models.Shard
{
    public struct LoginCharacter
    {
        public uint Id { get; set; }
        public uint AccountId { get; set; }
        public string Name { get; set; }
        public bool IsPlussed { get; set; }
        public bool IsDeleted { get; set; }
        public ulong DeleteTime { get; set; }
        public double LastLoginTimestamp { get; set; }


    }
}
