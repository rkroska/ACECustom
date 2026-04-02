using System;

namespace ACE.Entity.Enum
{
    /// <summary>
    /// This data is sent as loc in the player description message F7B0 -0013
    /// </summary>
    [Flags]
    public enum EquipMask : uint
    {
        None                = 0x00000000,
        HeadWear            = 0x00000001,
        ChestWear           = 0x00000002,
        AbdomenWear         = 0x00000004,
        UpperArmWear        = 0x00000008,
        LowerArmWear        = 0x00000010,
        HandWear            = 0x00000020,
        UpperLegWear        = 0x00000040,
        LowerLegWear        = 0x00000080,
        FootWear            = 0x00000100,
        ChestArmor          = 0x00000200,
        AbdomenArmor        = 0x00000400,
        UpperArmArmor       = 0x00000800,
        LowerArmArmor       = 0x00001000,
        UpperLegArmor       = 0x00002000,
        LowerLegArmor       = 0x00004000,
        NeckWear            = 0x00008000,
        WristWearLeft       = 0x00010000,
        WristWearRight      = 0x00020000,
        FingerWearLeft      = 0x00040000,
        FingerWearRight     = 0x00080000,
        MeleeWeapon         = 0x00100000,
        Shield              = 0x00200000,
        MissileWeapon       = 0x00400000,
        MissileAmmo         = 0x00800000,
        Held                = 0x01000000,
        TwoHanded           = 0x02000000,
        TrinketOne          = 0x04000000,
        Cloak               = 0x08000000,
        SigilOne            = 0x10000000,
        SigilTwo            = 0x20000000,
        SigilThree          = 0x40000000,
        Clothing            = 0x80000000 | HeadWear | ChestWear | AbdomenWear | UpperArmWear | LowerArmWear | HandWear | UpperLegWear | LowerLegWear | FootWear,
        Armor               = ChestArmor | AbdomenArmor | UpperArmArmor | LowerArmArmor | UpperLegArmor | LowerLegArmor | FootWear,
        ArmorExclusive      = ChestArmor | AbdomenArmor | UpperArmArmor | LowerArmArmor | UpperLegArmor | LowerLegArmor,
        Extremity           = HeadWear | HandWear | FootWear,
        Jewelry             = NeckWear | WristWearLeft | WristWearRight | FingerWearLeft | FingerWearRight | TrinketOne | Cloak | SigilOne | SigilTwo | SigilThree,
        WristWear           = WristWearLeft | WristWearRight,
        FingerWear          = FingerWearLeft | FingerWearRight,
        Sigil               = SigilOne | SigilTwo | SigilThree,
        ReadySlot           = Held | TwoHanded | TrinketOne | Cloak | SigilOne | SigilTwo,
        Weapon              = SigilTwo | TrinketOne | Held,
        WeaponReadySlot     = SigilOne | SigilTwo | TrinketOne | Held,
        Selectable          = MeleeWeapon | Shield | MissileWeapon | Held | TwoHanded,
        SelectablePlusAmmo  = Selectable | MissileAmmo,
        All                 = 0x7FFFFFFF,
        CanGoInReadySlot    = 0x7FFFFFFF
    }

    public static class EquipMaskExtensions
    {
        /// <summary>
        /// Returns a human-readable name for common equipment slots.
        /// </summary>
        public static string ToFriendlyString(this EquipMask mask)
        {
            return mask switch
            {
                EquipMask.None => "nothing",
                EquipMask.HeadWear => "Head",
                EquipMask.ChestWear => "Chest Clothing",
                EquipMask.AbdomenWear => "Abdomen Clothing",
                EquipMask.UpperArmWear => "Upper Arms Clothing",
                EquipMask.LowerArmWear => "Lower Arms Clothing",
                EquipMask.HandWear => "Hands Clothing",
                EquipMask.UpperLegWear => "Upper Legs Clothing",
                EquipMask.LowerLegWear => "Lower Legs Clothing",
                EquipMask.FootWear => "Feet",
                EquipMask.ChestArmor => "Chest Armor",
                EquipMask.AbdomenArmor => "Abdomen Armor",
                EquipMask.UpperArmArmor => "Upper Arms Armor",
                EquipMask.LowerArmArmor => "Lower Arms Armor",
                EquipMask.UpperLegArmor => "Upper Legs Armor",
                EquipMask.LowerLegArmor => "Lower Legs Armor",
                EquipMask.NeckWear => "Neck",
                EquipMask.WristWearLeft => "Left Wrist",
                EquipMask.WristWearRight => "Right Wrist",
                EquipMask.FingerWearLeft => "Left Finger",
                EquipMask.FingerWearRight => "Right Finger",
                EquipMask.MeleeWeapon => "Melee Weapon",
                EquipMask.Shield => "Shield",
                EquipMask.MissileWeapon => "Missile Weapon",
                EquipMask.MissileAmmo => "Missile Ammo",
                EquipMask.Held => "Main Hand",
                EquipMask.TwoHanded => "Two Handed",
                EquipMask.TrinketOne => "Trinket",
                EquipMask.Cloak => "Cloak",
                EquipMask.SigilOne => "First Sigil",
                EquipMask.SigilTwo => "Second Sigil",
                EquipMask.SigilThree => "Third Sigil",
                _ => mask.ToString()
            };
        }
    }
}
