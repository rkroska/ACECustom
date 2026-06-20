-- ============================================================
-- Greater Far Shot Charm (Tier 2) — WCID 777710008
-- ILT Ability Charm — Ability ID 28 (HasFarShotCharm)
-- While active, your maximum missile attack range is increased
-- by +30% and final missile damage is increased by +10%.
-- ============================================================

DELETE FROM `weenie` WHERE `class_Id` = 777710008;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777710008, 'ilt_farshotcharm_t2', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777710008,    11, 1)  /* IgnoreCollisions */
     , (777710008,    13, 1)  /* Ethereal */
     , (777710008,    14, 1)  /* GravityStatus */
     , (777710008,    63, 1)  /* UnlimitedUse */
     , (777710008,  9040, 1)  /* IsCharm */
     , (777710008, 50000, 1); /* IsAbilityCharm */

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777710008,     1, 2048) /* ItemType - Gem */
     , (777710008,     5,    5) /* EncumbranceVal */
     , (777710008,     8,    5) /* Mass */
     , (777710008,    16,    8) /* ItemUseable - Contained */
     , (777710008,    19,    1) /* UiEffects - Magical */
     , (777710008,    33,    1) /* Bonded */
     , (777710008,    83,    2) /* ActivationResponse - Use */
     , (777710008,    93, 1044) /* PhysicsState */
     , (777710008,   114,    1) /* Attuned */
     , (777710008, 50000,   28) /* CharmGrantsAbility - ID 28 = Far Shot */
     , (777710008, 50005,    2) /* CharmLevel - 2 */
     , (777710008, 50006,    3) /* CharmMaxLevel - 3 tiers */
     , (777710008, 50060, 1780887600); /* ItemExpirationTimestamp - Sunday June 7, 2026 11:00 PM EST */

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777710008,    1, 33554556)  /* Setup - Coffer/Chest */
     , (777710008,    3, 536870932) /* SoundTable */
     , (777710008,    8, 100672653) /* Icon - bow/arrow icon */
     , (777710008,   48, 100676435) /* IconUnderlay */
     , (777710008,   50, 100667551); /* IconOverlay - Tier 2 badge */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777710008,  1, 'Greater Far Shot Charm')
     , (777710008, 14, 'Double-click to activate. While active, your maximum missile attack range is increased by +30% and final missile damage is increased by +10%.');
