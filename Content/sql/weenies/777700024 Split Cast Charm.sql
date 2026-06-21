-- ============================================================
-- Split Cast Charm (Tier 1) — WCID 777700024
-- ILT Ability Charm — Ability ID 22 (HasPentaCast)
-- While active, Streak, Arc, and Bolt spells will split to target multiple distinct enemies simultaneously.
-- ============================================================

DELETE FROM `weenie` WHERE `class_Id` = 777700024;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700024, 'ilt_splitcastcharm', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700024,    11, 1)  /* IgnoreCollisions */
     , (777700024,    13, 1)  /* Ethereal */
     , (777700024,    14, 1)  /* GravityStatus */
     , (777700024,    63, 1)  /* UnlimitedUse */
     , (777700024,  9040, 1)  /* IsCharm */
     , (777700024, 50000, 1); /* IsAbilityCharm */

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700024,     1, 2048) /* ItemType - Gem */
     , (777700024,     5,    5) /* EncumbranceVal */
     , (777700024,     8,    5) /* Mass */
     , (777700024,    16,    8) /* ItemUseable - Contained */
     , (777700024,    19,    1) /* UiEffects - Magical */
     , (777700024,    33,    1) /* Bonded */
     , (777700024,    83,    2) /* ActivationResponse - Use */
     , (777700024,    93, 1044) /* PhysicsState */
     , (777700024,   114,    1) /* Attuned */
     , (777700024, 50000,   22) /* CharmGrantsAbility - ID 22 = Penta Cast / Split Cast */
     , (777700024, 50005,    1) /* CharmLevel - 1 */
     , (777700024, 50006,    1); /* CharmMaxLevel - 1 tier */

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700024,    1, 33554556)  /* Setup - Coffer/Chest */
     , (777700024,    3, 536870932) /* SoundTable */
     , (777700024,    8, 100670725) /* Icon - 0x06001D05 (glowing magic/bolt spell icon) */
     , (777700024,   48, 100676435) /* IconUnderlay */
     , (777700024,   50, 100667550); /* IconOverlay - Tier 1 badge */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700024,  1, 'Split Cast Charm')
     , (777700024, 14, 'Double-click to activate. While active, Streak, Arc, and Bolt spells will split to target multiple distinct enemies simultaneously.');
