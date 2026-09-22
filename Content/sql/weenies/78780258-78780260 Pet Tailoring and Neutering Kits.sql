/* Pet Tailoring and Neutering kits. Run against ace_world. Safe to re-run (deletes then inserts). */
/* WCIDs 78780258-78780260 (7878 block, beside the breeding consumables). Until 2026-09-21 they were
   98760399-98760401, but production already uses those three ids for other content (Tyrannical Drudge Gen,
   Realm of Woe, Doriathazaar). NEVER delete or reuse 98760399-98760401 in any script that can reach prod.
   The test-only migration that retired the old rows on the test server is not part of this file. */

/* Pet Neutering Kit (78780258) - Pet Tailoring / Neutering tools. Code: Source/ACE.Server/Entity/PetTailoring.cs and Player_Use.cs. */
DELETE FROM `weenie` WHERE `class_Id` = 78780258;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780258, 'ace78780258-petneuteringkit', 44, '2026-09-05 00:00:00');
/* WeenieType 44 = Stackable (same as Savage Echo / Essence Resonator) - required for the "Use On" targeting cursor */

DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780258;
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780258,    11, 1) /* IgnoreCollisions */
     , (78780258,    13, 1) /* Ethereal */
     , (78780258,    14, 1) /* GravityStatus */;

DELETE FROM `weenie_properties_int` WHERE `object_Id` = 78780258;
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780258,     1,    128) /* ItemType - Misc */
     , (78780258,     5,     25) /* EncumbranceVal */
     , (78780258,     8,     25) /* Mass */
     , (78780258,    11,  100) /* MaxStackSize */
     , (78780258,    12,      1) /* StackSize */
     , (78780258,    13,      1) /* StackUnitEncumbrance */
     , (78780258,    14,      1) /* StackUnitMass */
     , (78780258,    16, 524296) /* ItemUseable - SourceContainedTargetContained: "Use On" cursor */
     , (78780258,    18,     10) /* UiEffects - Magical */
     , (78780258,    19, 2500000) /* Value - 2,500,000 Pyreals (10 MMD) */
     , (78780258,    93,   1044) /* PhysicsState */
     , (78780258,    94,    128) /* TargetType - Misc; the server-side check in Player_Use.cs accepts pet devices explicitly */;

DELETE FROM `weenie_properties_d_i_d` WHERE `object_Id` = 78780258;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780258,     1,  33558818) /* Setup (same as Essence Resonator) */
     , (78780258,     6,  67115262) /* PaletteBase */
     , (78780258,     7, 268436836) /* ClothingBase */
     , (78780258,     8, 100671428) /* Icon (0x06001FC4) */
     , (78780258,    22, 872415275) /* PhysicsEffectTable (0x3400002B) - NOT an icon overlay; IconOverlay is DID type 50 and none is assigned to the kits */
     , (78780258,    52, 100676546) /* IconUnderlay (0x060033C2) */;

DELETE FROM `weenie_properties_string` WHERE `object_Id` = 78780258;
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780258,    1, 'Pet Neutering Kit')
     , (78780258,   14, 'Use on a combat pet essence to permanently spay or neuter it. The essence can never be used for breeding again. This cannot be undone.')
     , (78780258,   15, 'A pet neutering kit.')
     , (78780258,   16, 'A pet neutering kit.');

/* Pet Tailoring Kit (78780259) - Pet Tailoring / Neutering tools. Code: Source/ACE.Server/Entity/PetTailoring.cs and Player_Use.cs. */
DELETE FROM `weenie` WHERE `class_Id` = 78780259;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780259, 'ace78780259-pettailoringkit', 44, '2026-09-05 00:00:00');
/* WeenieType 44 = Stackable (same as Savage Echo / Essence Resonator) - required for the "Use On" targeting cursor */

DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780259;
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780259,    11, 1) /* IgnoreCollisions */
     , (78780259,    13, 1) /* Ethereal */
     , (78780259,    14, 1) /* GravityStatus */;

DELETE FROM `weenie_properties_int` WHERE `object_Id` = 78780259;
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780259,     1,    128) /* ItemType - Misc */
     , (78780259,     5,     25) /* EncumbranceVal */
     , (78780259,     8,     25) /* Mass */
     , (78780259,    11,    1) /* MaxStackSize - 1: an 18+ stack at 125M each overflows the int stack Value and the vendor charges 1 pyreal */
     , (78780259,    12,      1) /* StackSize */
     , (78780259,    13,      1) /* StackUnitEncumbrance */
     , (78780259,    14,      1) /* StackUnitMass */
     , (78780259,    16, 524296) /* ItemUseable - SourceContainedTargetContained: "Use On" cursor */
     , (78780259,    18,     10) /* UiEffects - Magical */
     , (78780259,    19, 125000000) /* Value - 125,000,000 Pyreals (500 MMD) */
     , (78780259,    93,   1044) /* PhysicsState */
     , (78780259,    94,    128) /* TargetType - Misc; the server-side check in Player_Use.cs accepts pet devices explicitly */;

DELETE FROM `weenie_properties_d_i_d` WHERE `object_Id` = 78780259;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780259,     1,  33558818) /* Setup (same as Essence Resonator) */
     , (78780259,     6,  67115262) /* PaletteBase */
     , (78780259,     7, 268436836) /* ClothingBase */
     , (78780259,     8, 100690891) /* Icon (0x06006BCB) */
     , (78780259,    22, 872415275) /* PhysicsEffectTable (0x3400002B) - NOT an icon overlay; IconOverlay is DID type 50 and none is assigned to the kits */
     , (78780259,    52, 100689403) /* IconUnderlay (0x060065FB) */;

DELETE FROM `weenie_properties_string` WHERE `object_Id` = 78780259;
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780259,    1, 'Pet Tailoring Kit')
     , (78780259,   14, 'Use on a combat pet essence to extract its entire appearance into this kit. The source essence is consumed. The filled kit can then be applied to another combat pet essence, which keeps its own stats, potency, bond and lineage and takes on only the look. Dismiss the pet first.')
     , (78780259,   15, 'A pet tailoring kit.')
     , (78780259,   16, 'A pet tailoring kit.');

/* Pet Tailoring Kit (Filled) (78780260) - Pet Tailoring / Neutering tools. Code: Source/ACE.Server/Entity/PetTailoring.cs and Player_Use.cs. */
DELETE FROM `weenie` WHERE `class_Id` = 78780260;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780260, 'ace78780260-pettailoringkitfilled', 44, '2026-09-05 00:00:00');
/* WeenieType 44 = Stackable (same as Savage Echo / Essence Resonator) - required for the "Use On" targeting cursor */

DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780260;
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780260,    11, 1) /* IgnoreCollisions */
     , (78780260,    13, 1) /* Ethereal */
     , (78780260,    14, 1) /* GravityStatus */;

DELETE FROM `weenie_properties_int` WHERE `object_Id` = 78780260;
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780260,     1,    128) /* ItemType - Misc */
     , (78780260,     5,     25) /* EncumbranceVal */
     , (78780260,     8,     25) /* Mass */
     , (78780260,    11,  1) /* MaxStackSize */
     , (78780260,    12,      1) /* StackSize */
     , (78780260,    13,      1) /* StackUnitEncumbrance */
     , (78780260,    14,      1) /* StackUnitMass */
     , (78780260,    16, 524296) /* ItemUseable - SourceContainedTargetContained: "Use On" cursor */
     , (78780260,    18,     10) /* UiEffects - Magical */
     , (78780260,    19, 125000000) /* Value - 125,000,000 Pyreals (500 MMD) */
     , (78780260,    93,   1044) /* PhysicsState */
     , (78780260,    94,    128) /* TargetType - Misc; the server-side check in Player_Use.cs accepts pet devices explicitly */;

DELETE FROM `weenie_properties_d_i_d` WHERE `object_Id` = 78780260;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780260,     1,  33558818) /* Setup (same as Essence Resonator) */
     , (78780260,     6,  67115262) /* PaletteBase */
     , (78780260,     7, 268436836) /* ClothingBase */
     , (78780260,     8, 100693217) /* Icon (0x060074E1) */
     , (78780260,    22, 872415275) /* PhysicsEffectTable (0x3400002B) - NOT an icon overlay; IconOverlay is DID type 50 and none is assigned to the kits */
     , (78780260,    52, 100689403) /* IconUnderlay (0x060065FB) */;

DELETE FROM `weenie_properties_string` WHERE `object_Id` = 78780260;
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780260,    1, 'Pet Tailoring Kit (Filled)')
     , (78780260,   14, 'Holds the complete appearance of a combat pet: model, colours, size, name and equipment. Use on a combat pet essence to tailor that look onto it. Only the appearance changes. Dismiss the pet first. Consumed on use.')
     , (78780260,   15, 'A filled pet tailoring kit.')
     , (78780260,   16, 'A filled pet tailoring kit.');

