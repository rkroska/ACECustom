/* Pet Mutagenic Serum (78780257): re-rolls a combat pet essence's colour from the master
   mutation palette pool. Appearance only - stats, mutation counts and potency are untouched.
   Run against ace_world. Safe to re-run (deletes then inserts).
   78780256 (Ancestral Gene Re-roller) stays reserved and unbuilt; this patch does not touch it. */

/* ========================================================================= */
/* 1. Mutagenic Serum (78780257) - colour-only re-roll                        */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 78780257;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (78780257, 'ace78780257-mutagenicserum', 44, '2026-09-19 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 78780257;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (78780257, 11, 1)  /* IgnoreCollisions */
     , (78780257, 13, 1)  /* Ethereal */
     , (78780257, 14, 1); /* GravityStatus */

DELETE FROM weenie_properties_int WHERE object_Id = 78780257;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (78780257,   1,    128)  /* ItemType - Misc */
     , (78780257,   5,     10)  /* EncumbranceVal */
     , (78780257,   8,     10)  /* Mass */
     , (78780257,  11,      1)  /* MaxStackSize - 1: a 100-stack at 25M each overflows the int stack Value and the vendor charges 1 pyreal */
     , (78780257,  12,      1)  /* StackSize */
     , (78780257,  13,      1)  /* StackUnitEncumbrance */
     , (78780257,  14,      1)  /* StackUnitMass */
     , (78780257,  16, 524296)  /* ItemUseable - SourceContainedTargetContained */
     , (78780257,  18,     10)  /* UiEffects - Magical */
     , (78780257,  19, 25000000)  /* Value - 25,000,000 Pyreals (100 MMD) */
     , (78780257,  93,   1044)  /* PhysicsState */
     , (78780257,  94,    128); /* TargetType - Misc (PetDevice) */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 78780257;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (78780257,  1,   33554446) /* Setup - Potion bottle */
     , (78780257,  8,  100672518) /* Icon (0x06002406) */
     , (78780257, 52,  100689403); /* IconUnderlay (0x060065FB) */

DELETE FROM weenie_properties_string WHERE object_Id = 78780257;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (78780257,  1, 'Mutagenic Serum')
     , (78780257, 14, 'Use on any combat pet essence to re-roll its colour from the full mutation palette pool. Changes appearance only: stats, mutations and potency are untouched. Re-summon the pet to see its new colour.')
     , (78780257, 15, 'A cloudy serum that shifts a creature''s colouring without touching its nature.')
     , (78780257, 16, 'A cloudy serum that shifts a creature''s colouring without touching its nature.');

/* ========================================================================= */
/* 2. Stock it on Ivo, Ruggan's Quartermaster (78780201) if present           */
/* ========================================================================= */
DELETE FROM `weenie_properties_create_list` WHERE `object_Id` = 78780201 AND `weenie_Class_Id` = 78780257;
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`)
SELECT 78780201, 4, 78780257, -1, 0, 0, 0
WHERE EXISTS (SELECT 1 FROM `weenie` WHERE `class_Id` = 78780201);
