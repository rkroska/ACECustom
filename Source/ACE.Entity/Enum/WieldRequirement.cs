
namespace ACE.Entity.Enum
{
    public enum WieldRequirement
    {
        Invalid,
        Skill,
        RawSkill,
        Attrib,
        RawAttrib,
        SecondaryAttrib,
        RawSecondaryAttrib,
        Level,
        Training,
        IntStat,
        BoolStat,
        CreatureType,
        HeritageType,

        // server-side extension (not a retail value; the client never sees it -- T11 items
        // suppress the appraisal wield lines and display the requirement in their info block):
        // WieldSkillType = a PropertyInt64 id, WieldDifficulty = required minimum
        Int64Stat
    }
}
