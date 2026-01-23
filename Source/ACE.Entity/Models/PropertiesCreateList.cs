using System;

using ACE.Entity.Enum;

namespace ACE.Entity.Models
{
    public class PropertiesCreateList
    {
        /// <summary>
        /// This is only used to tie this property back to a specific database row
        /// </summary>
        public uint? DatabaseRecordId { get; set; }

        public required DestinationType DestinationType { get; set; }
        public required uint WeenieClassId { get; set; }
        public required int StackSize { get; set; }
        public required sbyte Palette { get; set; }
        public required float Shade { get; set; }
        public required bool TryToBond { get; set; }

        public PropertiesCreateList Clone()
        {
            var result = new PropertiesCreateList
            {
                DestinationType = DestinationType,
                WeenieClassId = WeenieClassId,
                StackSize = StackSize,
                Palette = Palette,
                Shade = Shade,
                TryToBond = TryToBond,
            };

            return result;
        }
    }
}
