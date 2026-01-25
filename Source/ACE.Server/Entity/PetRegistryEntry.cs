using System;
using ACE.Entity.Enum;

namespace ACE.Server.Entity
{
    public class PetRegistryEntry
    {
        public uint Wcid { get; set; }
        public string CreatureName { get; set; }
        public CreatureType? CreatureType { get; set; }
        public bool IsShiny { get; set; }
        public DateTime RegisteredAt { get; set; }
    }
}
