using ACE.Entity.Enum;

namespace ACE.Server.Entity
{
    /// <summary>
    /// This Class is used to add children
    /// </summary>
    public class HeldItem(uint guid, int locationId)
    {
        public uint Guid { get; } = guid;

        public int LocationId { get; } = locationId;
    }
}
