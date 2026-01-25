using System;

using ACE.Entity.Enum;

namespace ACE.Entity.Models
{
    public class PropertiesBodyPart
    {
        public required DamageType DType { get; set; }
        public required int DVal { get; set; }
        public required float DVar { get; set; }
        public required int BaseArmor { get; set; }
        public required int ArmorVsSlash { get; set; }
        public required int ArmorVsPierce { get; set; }
        public required int ArmorVsBludgeon { get; set; }
        public required int ArmorVsCold { get; set; }
        public required int ArmorVsFire { get; set; }
        public required int ArmorVsAcid { get; set; }
        public required int ArmorVsElectric { get; set; }
        public required int ArmorVsNether { get; set; }
        public required int BH { get; set; }
        public required float HLF { get; set; }
        public required float MLF { get; set; }
        public required float LLF { get; set; }
        public required float HRF { get; set; }
        public required float MRF { get; set; }
        public required float LRF { get; set; }
        public required float HLB { get; set; }
        public required float MLB { get; set; }
        public required float LLB { get; set; }
        public required float HRB { get; set; }
        public required float MRB { get; set; }
        public required float LRB { get; set; }

        public PropertiesBodyPart Clone()
        {
            var result = new PropertiesBodyPart
            {
                DType = DType,
                DVal = DVal,
                DVar = DVar,
                BaseArmor = BaseArmor,
                ArmorVsSlash = ArmorVsSlash,
                ArmorVsPierce = ArmorVsPierce,
                ArmorVsBludgeon = ArmorVsBludgeon,
                ArmorVsCold = ArmorVsCold,
                ArmorVsFire = ArmorVsFire,
                ArmorVsAcid = ArmorVsAcid,
                ArmorVsElectric = ArmorVsElectric,
                ArmorVsNether = ArmorVsNether,
                BH = BH,
                HLF = HLF,
                MLF = MLF,
                LLF = LLF,
                HRF = HRF,
                MRF = MRF,
                LRF = LRF,
                HLB = HLB,
                MLB = MLB,
                LLB = LLB,
                HRB = HRB,
                MRB = MRB,
                LRB = LRB,
            };

            return result;
        }
    }
}
