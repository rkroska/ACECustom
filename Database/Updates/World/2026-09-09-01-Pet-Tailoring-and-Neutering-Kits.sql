/* Pet Tailoring and Neutering kits. Run against ace_world. Safe to re-run (deletes then inserts). */

/* Pet Neutering Kit (98760399) - Pet Tailoring / Neutering tools. Code: Source/ACE.Server/Entity/PetTailoring.cs and Player_Use.cs. */
DELETE FROM `weenie` WHERE `class_Id` = 98760399;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (98760399, 'ace98760399-petneuteringkit', 44, '2026-09-05 00:00:00');
/* WeenieType 44 = Stackable (same as Savage Echo / Essence Resonator) - required for the "Use On" targeting cursor */

DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 98760399;
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (98760399,    11, 1) /* IgnoreCollisions */
     , (98760399,    13, 1) /* Ethereal */
     , (98760399,    14, 1) /* GravityStatus */;

DELETE FROM `weenie_properties_int` WHERE `object_Id` = 98760399;
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (98760399,     1,    128) /* ItemType - Misc */
     , (98760399,     5,     25) /* EncumbranceVal */
     , (98760399,     8,     25) /* Mass */
     , (98760399,    11,  100) /* MaxStackSize */
     , (98760399,    12,      1) /* StackSize */
     , (98760399,    13,      1) /* StackUnitEncumbrance */
     , (98760399,    14,      1) /* StackUnitMass */
     , (98760399,    16, 524296) /* ItemUseable - SourceContainedTargetContained: "Use On" cursor */
     , (98760399,    18,     10) /* UiEffects - Magical */
     , (98760399,    19,  500) /* Value */
     , (98760399,    93,   1044) /* PhysicsState */
     , (98760399,    94,    128) /* TargetType - Misc; the server-side check in Player_Use.cs accepts pet devices explicitly */;

DELETE FROM `weenie_properties_d_i_d` WHERE `object_Id` = 98760399;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (98760399,     1,  33558818) /* Setup (same as Essence Resonator) */
     , (98760399,     6,  67115262) /* PaletteBase */
     , (98760399,     7, 268436836) /* ClothingBase */
     , (98760399,     8, 100670879) /* Icon */
     , (98760399,    22, 872415275) /* IconOverlay */
     , (98760399,    52, 100667855) /* IconUnderlay */;

DELETE FROM `weenie_properties_string` WHERE `object_Id` = 98760399;
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (98760399,    1, 'Pet Neutering Kit')
     , (98760399,   14, 'Use on a combat pet essence to permanently spay or neuter it. The essence can never be used for breeding again. This cannot be undone.')
     , (98760399,   15, 'A pet neutering kit.')
     , (98760399,   16, 'A pet neutering kit.');

/* Pet Tailoring Kit (98760400) - Pet Tailoring / Neutering tools. Code: Source/ACE.Server/Entity/PetTailoring.cs and Player_Use.cs. */
DELETE FROM `weenie` WHERE `class_Id` = 98760400;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (98760400, 'ace98760400-pettailoringkit', 44, '2026-09-05 00:00:00');
/* WeenieType 44 = Stackable (same as Savage Echo / Essence Resonator) - required for the "Use On" targeting cursor */

DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 98760400;
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (98760400,    11, 1) /* IgnoreCollisions */
     , (98760400,    13, 1) /* Ethereal */
     , (98760400,    14, 1) /* GravityStatus */;

DELETE FROM `weenie_properties_int` WHERE `object_Id` = 98760400;
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (98760400,     1,    128) /* ItemType - Misc */
     , (98760400,     5,     25) /* EncumbranceVal */
     , (98760400,     8,     25) /* Mass */
     , (98760400,    11,  100) /* MaxStackSize */
     , (98760400,    12,      1) /* StackSize */
     , (98760400,    13,      1) /* StackUnitEncumbrance */
     , (98760400,    14,      1) /* StackUnitMass */
     , (98760400,    16, 524296) /* ItemUseable - SourceContainedTargetContained: "Use On" cursor */
     , (98760400,    18,     10) /* UiEffects - Magical */
     , (98760400,    19,  1000) /* Value */
     , (98760400,    93,   1044) /* PhysicsState */
     , (98760400,    94,    128) /* TargetType - Misc; the server-side check in Player_Use.cs accepts pet devices explicitly */;

DELETE FROM `weenie_properties_d_i_d` WHERE `object_Id` = 98760400;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (98760400,     1,  33558818) /* Setup (same as Essence Resonator) */
     , (98760400,     6,  67115262) /* PaletteBase */
     , (98760400,     7, 268436836) /* ClothingBase */
     , (98760400,     8, 100670879) /* Icon */
     , (98760400,    22, 872415275) /* IconOverlay */
     , (98760400,    52, 100667855) /* IconUnderlay */;

DELETE FROM `weenie_properties_string` WHERE `object_Id` = 98760400;
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (98760400,    1, 'Pet Tailoring Kit')
     , (98760400,   14, 'Use on a combat pet essence to extract its entire appearance into this kit. The source essence is consumed. The filled kit can then be applied to another combat pet essence, which keeps its own stats, potency, bond and lineage and takes on only the look. Dismiss the pet first.')
     , (98760400,   15, 'A pet tailoring kit.')
     , (98760400,   16, 'A pet tailoring kit.');

/* Pet Tailoring Kit (Filled) (98760401) - Pet Tailoring / Neutering tools. Code: Source/ACE.Server/Entity/PetTailoring.cs and Player_Use.cs. */
DELETE FROM `weenie` WHERE `class_Id` = 98760401;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (98760401, 'ace98760401-pettailoringkitfilled', 44, '2026-09-05 00:00:00');
/* WeenieType 44 = Stackable (same as Savage Echo / Essence Resonator) - required for the "Use On" targeting cursor */

DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 98760401;
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (98760401,    11, 1) /* IgnoreCollisions */
     , (98760401,    13, 1) /* Ethereal */
     , (98760401,    14, 1) /* GravityStatus */;

DELETE FROM `weenie_properties_int` WHERE `object_Id` = 98760401;
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (98760401,     1,    128) /* ItemType - Misc */
     , (98760401,     5,     25) /* EncumbranceVal */
     , (98760401,     8,     25) /* Mass */
     , (98760401,    11,  1) /* MaxStackSize */
     , (98760401,    12,      1) /* StackSize */
     , (98760401,    13,      1) /* StackUnitEncumbrance */
     , (98760401,    14,      1) /* StackUnitMass */
     , (98760401,    16, 524296) /* ItemUseable - SourceContainedTargetContained: "Use On" cursor */
     , (98760401,    18,     10) /* UiEffects - Magical */
     , (98760401,    19,  1000) /* Value */
     , (98760401,    93,   1044) /* PhysicsState */
     , (98760401,    94,    128) /* TargetType - Misc; the server-side check in Player_Use.cs accepts pet devices explicitly */;

DELETE FROM `weenie_properties_d_i_d` WHERE `object_Id` = 98760401;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (98760401,     1,  33558818) /* Setup (same as Essence Resonator) */
     , (98760401,     6,  67115262) /* PaletteBase */
     , (98760401,     7, 268436836) /* ClothingBase */
     , (98760401,     8, 100670879) /* Icon */
     , (98760401,    22, 872415275) /* IconOverlay */
     , (98760401,    52, 100667855) /* IconUnderlay */;

DELETE FROM `weenie_properties_string` WHERE `object_Id` = 98760401;
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (98760401,    1, 'Pet Tailoring Kit (Filled)')
     , (98760401,   14, 'Holds the complete appearance of a combat pet: model, colours, size, name and equipment. Use on a combat pet essence to tailor that look onto it. Only the appearance changes. Dismiss the pet first. Consumed on use.')
     , (98760401,   15, 'A filled pet tailoring kit.')
     , (98760401,   16, 'A filled pet tailoring kit.');

