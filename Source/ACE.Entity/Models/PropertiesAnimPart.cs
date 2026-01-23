using System;

namespace ACE.Entity.Models
{
    public class PropertiesAnimPart
    {
        public required byte Index { get; set; }
        public required uint AnimationId { get; set; }

        public PropertiesAnimPart Clone()
        {
            var result = new PropertiesAnimPart
            {
                Index = Index,
                AnimationId = AnimationId,
            };

            return result;
        }
    }
}
