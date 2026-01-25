using System.Collections.Generic;
using System.IO;

using ACE.Common.Extensions;
using ACE.Entity.Enum;

namespace ACE.Entity
{
    public class CharacterCreateInfo
    {
        public HeritageGroup Heritage { get; set; }
        public uint Gender { get; set; }

        public Appearance Appearance { get; init; }

        public int TemplateOption { get; init; }

        public uint StrengthAbility { get; set; }
        public uint EnduranceAbility { get; set; }
        public uint CoordinationAbility { get; set; }
        public uint QuicknessAbility { get; set; }
        public uint FocusAbility { get; set; }
        public uint SelfAbility { get; set; }

        public uint CharacterSlot { get; init; }
        public uint ClassId { get; init; }

        public List<SkillAdvancementClass> SkillAdvancementClasses;

        public string Name { get; set; }

        public uint StartArea { get; init; }

        public bool IsAdmin { get; init; }
        public bool IsSentinel { get; init; }

        public static CharacterCreateInfo Unpack(BinaryReader reader)
        {
            reader.BaseStream.Position += 4;   /* Unknown constant (1) */
            HeritageGroup heritage = (HeritageGroup)reader.ReadUInt32();
            uint gender = reader.ReadUInt32();
            Appearance Appearance = Appearance.Unpack(reader);
            int templateOption = reader.ReadInt32();
            uint strengthAbility = reader.ReadUInt32();
            uint enduranceAbility = reader.ReadUInt32();
            uint coordinationAbility = reader.ReadUInt32();
            uint quicknessAbility = reader.ReadUInt32();
            uint focusAbility = reader.ReadUInt32();
            uint selfAbility = reader.ReadUInt32();
            uint characterSlot = reader.ReadUInt32();
            uint classId = reader.ReadUInt32();
            uint numOfSkills = reader.ReadUInt32();
            List<SkillAdvancementClass> skillAdvancementClasses = [];
            for (uint i = 0; i < numOfSkills; i++)
                skillAdvancementClasses.Add((SkillAdvancementClass)reader.ReadUInt32());
            string name = reader.ReadString16L();
            uint startArea = reader.ReadUInt32();
            bool isAdmin = (reader.ReadUInt32() == 1);
            bool isSentinel = (reader.ReadUInt32() == 1);

            return new CharacterCreateInfo()
            {
                Heritage = heritage,
                Gender = gender,
                Appearance = Appearance,
                TemplateOption = templateOption,
                StrengthAbility = strengthAbility,
                EnduranceAbility = enduranceAbility,
                CoordinationAbility = coordinationAbility,
                QuicknessAbility = quicknessAbility,
                FocusAbility = focusAbility,
                SelfAbility = selfAbility,
                CharacterSlot = characterSlot,
                ClassId = classId,
                SkillAdvancementClasses = skillAdvancementClasses,
                Name = name,
                StartArea = startArea,
                IsAdmin = isAdmin,
                IsSentinel = isSentinel,
            };
        }
    }
}
