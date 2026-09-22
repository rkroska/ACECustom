/* Solidifying (78780262) and Fading (78780263) Tinctures: step a combat pet's translucency down or up
   by a tenth, between 0 (solid) and 0.5. The level is stored on the essence as PetTranslucency
   (float 9062) and replaces the summon template's own translucency when the pet is summoned. Sixteen
   of the 72 combat pet summon templates carry 0.5 (every Maiden and K'nath), so those start at 50%
   and everything else at 0%. A tincture that would go past either end is refused and not consumed.
   Only the translucency property changes: a look whose own textures are see-through stays that way.
   Code: Player_Use.cs (SolidifyingTinctureWcid / FadingTinctureWcid), PetDevice.cs (summon).
   Run against ace_world. Safe to re-run (deletes then inserts). */

/* ========================================================================= */
/* 1. Solidifying Tincture (78780262)                                         */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 78780262;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (78780262, 'ace78780262-solidifyingtincture', 44, '2026-09-22 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 78780262;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (78780262, 11, 1)  /* IgnoreCollisions */
     , (78780262, 13, 1)  /* Ethereal */
     , (78780262, 14, 1); /* GravityStatus */

DELETE FROM weenie_properties_int WHERE object_Id = 78780262;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (78780262,   1,     128)  /* ItemType - Misc */
     , (78780262,   5,      10)  /* EncumbranceVal */
     , (78780262,   8,      10)  /* Mass */
     , (78780262,  11,     100)  /* MaxStackSize - 100 x 12.5M = 1.25B, under the int stack Value limit */
     , (78780262,  12,       1)  /* StackSize */
     , (78780262,  13,       1)  /* StackUnitEncumbrance */
     , (78780262,  14,       1)  /* StackUnitMass */
     , (78780262,  16,  524296)  /* ItemUseable - SourceContainedTargetContained */
     , (78780262,  18,      10)  /* UiEffects - Magical */
     , (78780262,  19, 12500000) /* Value - 12,500,000 Pyreals (50 MMD) */
     , (78780262,  93,    1044)  /* PhysicsState */
     , (78780262,  94,     128); /* TargetType - Misc; Player_Use.cs accepts pet devices explicitly */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 78780262;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (78780262,  1,   33554446) /* Setup - Potion bottle */
     , (78780262,  8,  100670839) /* Icon (0x06001D77) - placeholder, unique combination */
     , (78780262, 52,  100689404); /* IconUnderlay (0x060065FC) */

DELETE FROM weenie_properties_string WHERE object_Id = 78780262;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (78780262,  1, 'Solidifying Tincture')
     , (78780262, 14, 'Use on a combat pet essence to make its pet 10% less see-through, down to fully solid. Maidens and K''nath start at 50%. Summon the pet again to see the change. Does nothing for a look that is itself translucent.')
     , (78780262, 15, 'A thick, cloudy tincture that makes spectral things opaque.')
     , (78780262, 16, 'A thick, cloudy tincture that makes spectral things opaque.');

/* ========================================================================= */
/* 2. Fading Tincture (78780263)                                         */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 78780263;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (78780263, 'ace78780263-fadingtincture', 44, '2026-09-22 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 78780263;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (78780263, 11, 1)  /* IgnoreCollisions */
     , (78780263, 13, 1)  /* Ethereal */
     , (78780263, 14, 1); /* GravityStatus */

DELETE FROM weenie_properties_int WHERE object_Id = 78780263;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (78780263,   1,     128)  /* ItemType - Misc */
     , (78780263,   5,      10)  /* EncumbranceVal */
     , (78780263,   8,      10)  /* Mass */
     , (78780263,  11,     100)  /* MaxStackSize - 100 x 12.5M = 1.25B, under the int stack Value limit */
     , (78780263,  12,       1)  /* StackSize */
     , (78780263,  13,       1)  /* StackUnitEncumbrance */
     , (78780263,  14,       1)  /* StackUnitMass */
     , (78780263,  16,  524296)  /* ItemUseable - SourceContainedTargetContained */
     , (78780263,  18,      10)  /* UiEffects - Magical */
     , (78780263,  19, 12500000) /* Value - 12,500,000 Pyreals (50 MMD) */
     , (78780263,  93,    1044)  /* PhysicsState */
     , (78780263,  94,     128); /* TargetType - Misc; Player_Use.cs accepts pet devices explicitly */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 78780263;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (78780263,  1,   33554446) /* Setup - Potion bottle */
     , (78780263,  8,  100670839) /* Icon (0x06001D77) - placeholder, unique combination */
     , (78780263, 52,  100689403); /* IconUnderlay (0x060065FB) */

DELETE FROM weenie_properties_string WHERE object_Id = 78780263;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (78780263,  1, 'Fading Tincture')
     , (78780263, 14, 'Use on a combat pet essence to make its pet 10% more see-through, up to 50%. Summon the pet again to see the change.')
     , (78780263, 15, 'A thin, pale tincture that lets the light through whatever drinks it.')
     , (78780263, 16, 'A thin, pale tincture that lets the light through whatever drinks it.');

/* ========================================================================= */
/* 3. Stock both on Ivo, Ruggan's Quartermaster (78780201) if present           */
/* ========================================================================= */
DELETE FROM `weenie_properties_create_list` WHERE `object_Id` = 78780201 AND `weenie_Class_Id` = 78780262;
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`)
SELECT 78780201, 4, 78780262, -1, 0, 0, 0
WHERE EXISTS (SELECT 1 FROM `weenie` WHERE `class_Id` = 78780201);
DELETE FROM `weenie_properties_create_list` WHERE `object_Id` = 78780201 AND `weenie_Class_Id` = 78780263;
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`)
SELECT 78780201, 4, 78780263, -1, 0, 0, 0
WHERE EXISTS (SELECT 1 FROM `weenie` WHERE `class_Id` = 78780201);
