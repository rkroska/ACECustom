DELETE FROM `weenie` WHERE `class_Id` = 4395100;

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (4395100, 'infdeadlyprismaticarrow', 5, '2021-11-01 00:00:00') /* Ammunition */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (4395100,   1,        256) /* ItemType - MissileWeapon */
     , (4395100,   3,         20) /* PaletteTemplate - Silver */
     , (4395100,   5,          1) /* EncumbranceVal */
     , (4395100,   8,          2) /* Mass */
     , (4395100,   9,    8388608) /* ValidLocations - MissileAmmo */
     , (4395100,  11,          1) /* MaxStackSize */
     , (4395100,  12,          1) /* StackSize */
     , (4395100,  13,          1) /* StackUnitEncumbrance */
     , (4395100,  14,          2) /* StackUnitMass */
     , (4395100,  15,       1500) /* StackUnitValue */
     , (4395100,  16,    2097124) /* ItemUseable - 2097124 */
     , (4395100,  17,       1630) /* RareId */
     , (4395100,  18,          1) /* UiEffects - Magical */
     , (4395100,  19,       1500) /* Value */
     , (4395100,  44,         40) /* Damage */
     , (4395100,  45,  268435456) /* DamageType - Base */
     , (4395100,  50,          1) /* AmmoType - Arrow */
     , (4395100,  51,          3) /* CombatUse - Ammo */
     , (4395100,  93,     132116) /* PhysicsState - Ethereal, IgnoreCollisions, Gravity, Inelastic */
     , (4395100, 150,        103) /* HookPlacement - Hook */
     , (4395100, 151,          2) /* HookType - Wall */
     , (4395100, 158,          8) /* WieldRequirements - Training */
     , (4395100, 159,         37) /* WieldSkillType - Fletching */
     , (4395100, 160,          3) /* WieldDifficulty */
     , (4395100, 270,          2) /* WieldRequirements2 - RawSkill */
     , (4395100, 271,         37) /* WieldSkillType2 - Fletching */
     , (4395100, 272,        375) /* WieldDifficulty2 */
     , (4395100, 273,          2) /* WieldRequirements3 - RawSkill */
     , (4395100, 274,         47) /* WieldSkillType3 - MissileWeapons */
     , (4395100, 275,        300) /* WieldDifficulty3 */;

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (4395100,  17, True ) /* Inelastic */
     , (4395100,  63, True ) /* UnlimitedUse */
     , (4395100,  69, False) /* IsSellable */;

INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`)
VALUES (4395100,  21,       0) /* WeaponLength */
     , (4395100,  22,     0.3) /* DamageVariance */
     , (4395100,  26,       0) /* MaximumVelocity */
     , (4395100,  29,       1) /* WeaponDefense */
     , (4395100,  62,       1) /* WeaponOffense */
     , (4395100,  63,       1) /* DamageMod */
     , (4395100,  78,       1) /* Friction */
     , (4395100,  79,       0) /* Elasticity */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (4395100,   1, 'Infinite Deadly Prismatic Arrow') /* Name */
     , (4395100,  14, 'You must be a specialized fletcher of great skill to use these potentially volatile arrows.') /* Use */
     , (4395100,  16, 'An Everlasting Deadly, crystalline arrow that draws the elemental energies from elementally attuned bows to damage their target.') /* LongDesc */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (4395100,   1, 0x02001A87) /* Setup */
     , (4395100,   3, 0x20000014) /* SoundTable */
     , (4395100,   6, 0x04000BEF) /* PaletteBase */
     , (4395100,   7, 0x10000352) /* ClothingBase */
     , (4395100,   8, 0x06006FC7) /* Icon */
     , (4395100,  22, 0x3400002B) /* PhysicsEffectTable */
     , (4395100,  52, 0x060033C3) /* IconUnderlay */;

