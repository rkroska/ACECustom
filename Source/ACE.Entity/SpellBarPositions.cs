
namespace ACE.Entity
{
    public class SpellBarPositions(uint spellBarId, uint spellBarPositionId, uint spellId)
    {
        public uint SpellBarId { get; set; } = spellBarId - 1;

        public uint SpellBarPositionId { get; set; } = spellBarPositionId - 1;

        public uint SpellId { get; set; } = spellId;
    }
}
