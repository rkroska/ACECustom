using System;

namespace ACE.Entity.Models
{
    public class PropertiesPosition
    {
        public required uint ObjCellId { get; set; }
        public required float PositionX { get; set; }
        public required float PositionY { get; set; }
        public required float PositionZ { get; set; }
        public required float RotationW { get; set; }
        public required float RotationX { get; set; }
        public required float RotationY { get; set; }
        public required float RotationZ { get; set; }

        public int? VariationId { get; set; }

        public PropertiesPosition Clone()
        {
            var result = new PropertiesPosition
            {
                ObjCellId = ObjCellId,
                PositionX = PositionX,
                PositionY = PositionY,
                PositionZ = PositionZ,
                RotationW = RotationW,
                RotationX = RotationX,
                RotationY = RotationY,
                RotationZ = RotationZ,
                VariationId = VariationId,
            };

            return result;
        }
    }
}
