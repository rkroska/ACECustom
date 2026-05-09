-- ============================================================
-- ILT Test: Bludgeon Wand — WCID 777700023
-- Basic bludgeon-element caster for testing the Shrapnel Charm.
-- Spawn in-game: /ci 777700023
-- ============================================================

DELETE FROM `weenie` WHERE `class_Id` = 777700023;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700023, 'ilt_test_bludge_wand', 2, NOW()); /* type 2 = Caster */

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700023,   1,  128)  /* ItemType - Caster */
     , (777700023,   5,   30)  /* EncumbranceVal */
     , (777700023,   8,   30)  /* Mass */
     , (777700023,  16,    6)  /* ItemUseable - Wielded */
     , (777700023,  22,    4)  /* DamageType - Bludgeoning */
     , (777700023,  26,   15)  /* WeaponTime */
     , (777700023,  27,   34)  /* WeaponSkill - War Magic */
     , (777700023,  33,    0)  /* Bonded - Destroy on death */
     , (777700023,  81,    2)  /* Locations - Held/Wand slot */
     , (777700023, 150,    0)  /* CurrentWieldedLocation */
     , (777700023, 158,   30); /* ItemWorkmanship */

INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`)
VALUES (777700023,  93, 1.0)   /* WeaponOffense */
     , (777700023,  94, 1.0)   /* WeaponDefense */
     , (777700023,  54, 1.0);  /* DefaultScale */

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700023,   1, 33559564)  /* Setup - basic wand model */
     , (777700023,   3, 536870932) /* SoundTable */
     , (777700023,   8, 100667750) /* Icon - wand icon */
     , (777700023,  22, 536870932); /* PhysicsEffectTable */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700023,  1, 'Bludgeon Wand')
     , (777700023, 14, 'A test wand attuned to earth magic.');
