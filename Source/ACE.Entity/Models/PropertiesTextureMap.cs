using System;

namespace ACE.Entity.Models
{
    public class PropertiesTextureMap
    {
        public required byte PartIndex { get; set; }
        public required uint OldTexture { get; set; }
        public required uint NewTexture { get; set; }

        public PropertiesTextureMap Clone()
        {
            var result = new PropertiesTextureMap
            {
                PartIndex = PartIndex,
                OldTexture = OldTexture,
                NewTexture = NewTexture,
            };

            return result;
        }
    }
}
