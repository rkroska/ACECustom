using System;

namespace ACE.Entity.Models
{
    public class PropertiesPalette
    {
        public required uint SubPaletteId { get; set; }
        public required ushort Offset { get; set; }
        public required ushort Length { get; set; }

        public PropertiesPalette Clone()
        {
            var result = new PropertiesPalette
            {
                SubPaletteId = SubPaletteId,
                Offset = Offset,
                Length = Length,
            };

            return result;
        }
    }
}
