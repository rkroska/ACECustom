-- ============================================================
-- Gem of Ultimate Blessings (WCID 777700200)
-- Custom Item — Double-click to buff yourself with Level 8s
-- and apply Impen VIII + all 7 Banes to all armor/shields.
-- ============================================================

DELETE FROM `weenie` WHERE `class_Id` = 777700200;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700200, 'ult_buffbanegem', 18, NOW());

DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 777700200;
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700200,    11, 1)  /* IgnoreCollisions */
     , (777700200,    13, 1)  /* Ethereal */
     , (777700200,    14, 1)  /* GravityStatus */
     , (777700200,    63, 1)  /* UnlimitedUse */;

DELETE FROM `weenie_properties_int` WHERE `object_Id` = 777700200;
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700200,     1, 2048) /* ItemType - Gem */
     , (777700200,     5,    5) /* EncumbranceVal */
     , (777700200,     8,    5) /* Mass */
     , (777700200,    16,    8) /* ItemUseable - Contained */
     , (777700200,    19,    1) /* UiEffects - Magical */
     , (777700200,    33,    1) /* Bonded */
     , (777700200,    83,    2) /* ActivationResponse - Use */
     , (777700200,    93, 1044) /* PhysicsState */
     , (777700200,   114,    1) /* Attuned */;

DELETE FROM `weenie_properties_d_i_d` WHERE `object_Id` = 777700200;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700200,    1, 33554975)  /* Setup - Focus Stone */
     , (777700200,    3, 536870932) /* SoundTable */
     , (777700200,    8, 100689503)  /* Icon - Focus Stone */
     , (777700200,   48, 100676435); /* IconUnderlay */

DELETE FROM `weenie_properties_string` WHERE `object_Id` = 777700200;
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700200,  1, 'Gem of Ultimate Blessings')
     , (777700200, 14, 'Double-click to buff yourself with all standard Level 8 self-buffs and apply Impen VIII and all 7 elemental Banes to all equipped armor/shields and all armor/shields in your inventory.');
