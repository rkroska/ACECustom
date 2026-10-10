/* Enlarging (78780265) and Shrinking (78780266) Stones: step how big something held in the hands is
   drawn - a weapon, shield, wand or orb - up or down by a tenth, between 70% and 140% of its normal
   size. The level is stored on the item as ItemSizeLevel (float 54000) and is multiplied into the
   scale sent to clients; the item's own DefaultScale is never changed. A stone that would go past
   either end is refused and not consumed. Stacks (thrown weapons) are refused.
   Code: Source/ACE.Server/Entity/SizeStones.cs, hooked in Player_Use.cs.
   Built like the Solidifying / Fading Tinctures (2026-09-22-06-Pet-Translucency-Tinctures.sql).
   All text is 7-bit ASCII per CLAUDE.md.
   Run against ace_world. Safe to re-run (deletes then inserts). */

/* ========================================================================= */
/* 1. Enlarging Stone (78780265)                                              */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 78780265;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (78780265, 'ace78780265-enlargingstone', 44, '2026-10-10 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 78780265;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (78780265, 11, 1)  /* IgnoreCollisions */
     , (78780265, 13, 1)  /* Ethereal */
     , (78780265, 14, 1); /* GravityStatus */

DELETE FROM weenie_properties_int WHERE object_Id = 78780265;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (78780265,   1,     2048)  /* ItemType - Gem */
     , (78780265,   5,       10)  /* EncumbranceVal */
     , (78780265,   8,       10)  /* Mass */
     , (78780265,  11,      100)  /* MaxStackSize - 100 x 12.5M = 1.25B, under the int stack Value limit */
     , (78780265,  12,        1)  /* StackSize */
     , (78780265,  13,       10)  /* StackUnitEncumbrance */
     , (78780265,  14,       10)  /* StackUnitMass */
     , (78780265,  16,   524296)  /* ItemUseable - SourceContainedTargetContained */
     , (78780265,  18,       10)  /* UiEffects - Magical */
     , (78780265,  19, 12500000)  /* Value - 12,500,000 Pyreals (50 MMD) */
     , (78780265,  93,     1044)  /* PhysicsState */
     , (78780265,  94,    35203); /* TargetType - MeleeWeapon, Armor (shields), Misc, MissileWeapon, Gem, Caster; SizeStones.cs then accepts only what is held in the hands */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 78780265;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (78780265,  1,   33556407) /* Setup (0x020007B7) - stone */
     , (78780265,  8,  100670494) /* Icon (0x06001C1E) */
     , (78780265, 52,  100689404); /* IconUnderlay (0x060065FC) */

DELETE FROM weenie_properties_string WHERE object_Id = 78780265;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (78780265,  1, 'Enlarging Stone')
     , (78780265, 14, 'Use on a weapon, shield, wand or orb to make it look 10% larger, up to 140% of its normal size. Only its looks change. Wield it again to see the change.')
     , (78780265, 15, 'A heavy stone that swells whatever it is rubbed against.')
     , (78780265, 16, 'A heavy stone that swells whatever it is rubbed against.')
     , (78780265, 20, 'Enlarging Stones');

/* ========================================================================= */
/* 2. Shrinking Stone (78780266)                                              */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 78780266;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (78780266, 'ace78780266-shrinkingstone', 44, '2026-10-10 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 78780266;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (78780266, 11, 1)  /* IgnoreCollisions */
     , (78780266, 13, 1)  /* Ethereal */
     , (78780266, 14, 1); /* GravityStatus */

DELETE FROM weenie_properties_int WHERE object_Id = 78780266;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (78780266,   1,     2048)  /* ItemType - Gem */
     , (78780266,   5,       10)  /* EncumbranceVal */
     , (78780266,   8,       10)  /* Mass */
     , (78780266,  11,      100)  /* MaxStackSize - 100 x 12.5M = 1.25B, under the int stack Value limit */
     , (78780266,  12,        1)  /* StackSize */
     , (78780266,  13,       10)  /* StackUnitEncumbrance */
     , (78780266,  14,       10)  /* StackUnitMass */
     , (78780266,  16,   524296)  /* ItemUseable - SourceContainedTargetContained */
     , (78780266,  18,       10)  /* UiEffects - Magical */
     , (78780266,  19, 12500000)  /* Value - 12,500,000 Pyreals (50 MMD) */
     , (78780266,  93,     1044)  /* PhysicsState */
     , (78780266,  94,    35203); /* TargetType - MeleeWeapon, Armor (shields), Misc, MissileWeapon, Gem, Caster; SizeStones.cs then accepts only what is held in the hands */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 78780266;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (78780266,  1,   33556407) /* Setup (0x020007B7) - stone */
     , (78780266,  8,  100670494) /* Icon (0x06001C1E) */
     , (78780266, 52,  100689403); /* IconUnderlay (0x060065FB) */

DELETE FROM weenie_properties_string WHERE object_Id = 78780266;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (78780266,  1, 'Shrinking Stone')
     , (78780266, 14, 'Use on a weapon, shield, wand or orb to make it look 10% smaller, down to 70% of its normal size. Only its looks change. Wield it again to see the change.')
     , (78780266, 15, 'A light, pitted stone that draws in whatever it is rubbed against.')
     , (78780266, 16, 'A light, pitted stone that draws in whatever it is rubbed against.')
     , (78780266, 20, 'Shrinking Stones');

/* ========================================================================= */
/* 3. Stock both on Ivo, Ruggan's Quartermaster (78780201) if present        */
/* ========================================================================= */
DELETE FROM `weenie_properties_create_list` WHERE `object_Id` = 78780201 AND `weenie_Class_Id` = 78780265;
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`)
SELECT 78780201, 4, 78780265, -1, 0, 0, 0
WHERE EXISTS (SELECT 1 FROM `weenie` WHERE `class_Id` = 78780201);
DELETE FROM `weenie_properties_create_list` WHERE `object_Id` = 78780201 AND `weenie_Class_Id` = 78780266;
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`)
SELECT 78780201, 4, 78780266, -1, 0, 0, 0
WHERE EXISTS (SELECT 1 FROM `weenie` WHERE `class_Id` = 78780201);
