using System;

namespace ACE.Entity.Enum
{
    [Flags]
    public enum ImbuedEffectType: uint
    {
        Undef                           = 0,
        CriticalStrike                  = 0x0001,
        CripplingBlow                   = 0x0002,
        ArmorRending                    = 0x0004,
        SlashRending                    = 0x0008,
        PierceRending                   = 0x0010,
        BludgeonRending                 = 0x0020,
        AcidRending                     = 0x0040,
        ColdRending                     = 0x0080,
        ElectricRending                 = 0x0100,
        FireRending                     = 0x0200,
        MeleeDefense                    = 0x0400,
        MissileDefense                  = 0x0800,
        MagicDefense                    = 0x1000,
        Spellbook                       = 0x2000,
        NetherRending                   = 0x4000,

        IgnoreSomeMagicProjectileDamage = 0x20000000,
        AlwaysCritical                  = 0x40000000,
        IgnoreAllArmor                  = 0x80000000
    }
    public static class ImbuedEffectTypeExtensions
    {
        public static string DisplayName(this ImbuedEffectType effectType)
        {
            switch (effectType)
            {
                case ImbuedEffectType.CriticalStrike:
                    return "Critical Strike";
                case ImbuedEffectType.CripplingBlow:
                    return "Crippling Blow";
                case ImbuedEffectType.ArmorRending:
                    return "Armor Rending";
                case ImbuedEffectType.SlashRending:
                    return $"{DamageType.Slash.DisplayName()} Rending";
                case ImbuedEffectType.PierceRending:
                    return $"{DamageType.Pierce.DisplayName()} Rending";
                case ImbuedEffectType.BludgeonRending:
                    return $"{DamageType.Bludgeon.DisplayName()} Rending";
                case ImbuedEffectType.AcidRending:
                    return $"{DamageType.Acid.DisplayName()} Rending";
                case ImbuedEffectType.ColdRending:
                    return $"{DamageType.Cold.DisplayName()} Rending";
                case ImbuedEffectType.ElectricRending:
                    return $"{DamageType.Electric.DisplayName()} Rending";
                case ImbuedEffectType.FireRending:
                    return $"{DamageType.Fire.DisplayName()} Rending";
                case ImbuedEffectType.NetherRending:
                    return $"{DamageType.Nether.DisplayName()} Rending";
                case ImbuedEffectType.MeleeDefense:
                    return "Melee Defense";
                case ImbuedEffectType.MissileDefense:
                    return "Missile Defense";
                case ImbuedEffectType.Spellbook:
                    return "Spellbook";
                case ImbuedEffectType.IgnoreSomeMagicProjectileDamage:
                    return "Magic Absorbing";
                case ImbuedEffectType.AlwaysCritical:
                    return "Always Crit";
                case ImbuedEffectType.IgnoreAllArmor:
                    return "Ignore Armor";
                default:
                    return effectType.ToString();
            }
        }
    }
}
