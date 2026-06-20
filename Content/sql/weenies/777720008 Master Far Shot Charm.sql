-- ============================================================
-- Master Far Shot Charm (Tier 3) — WCID 777720008
-- ILT Ability Charm — Ability ID 28 (HasFarShotCharm)
-- While active, your maximum missile attack range is increased
-- by +41% (up to 120 yards) and final missile damage is increased by +20%.
-- ============================================================

DELETE FROM `weenie` WHERE `class_Id` = 777720008;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777720008, 'ilt_farshotcharm_t3', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777720008,    11, 1)  /* IgnoreCollisions */
     , (777720008,    13, 1)  /* Ethereal */
     , (777720008,    14, 1)  /* GravityStatus */
     , (777720008,    63, 1)  /* UnlimitedUse */
     , (777720008,  9040, 1)  /* IsCharm */
     , (777720008, 50000, 1); /* IsAbilityCharm */

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777720008,     1, 2048) /* ItemType - Gem */
     , (777720008,     5,    5) /* EncumbranceVal */
     , (777720008,     8,    5) /* Mass */
     , (777720008,    16,    8) /* ItemUseable - Contained */
     , (777720008,    19,    1) /* UiEffects - Magical */
     , (777720008,    33,    1) /* Bonded */
     , (777720008,    83,    2) /* ActivationResponse - Use */
     , (777720008,    93, 1044) /* PhysicsState */
     , (777720008,   114,    1) /* Attuned */
     , (777720008, 50000,   28) /* CharmGrantsAbility - ID 28 = Far Shot */
     , (777720008, 50005,    3) /* CharmLevel - 3 */
     , (777720008, 50006,    3) /* CharmMaxLevel - 3 tiers */
     , (777720008, 50060, 1780887600); /* ItemExpirationTimestamp - Sunday June 7, 2026 11:00 PM EST */

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777720008,    1, 33554556)  /* Setup - Coffer/Chest */
     , (777720008,    3, 536870932) /* SoundTable */
     , (777720008,    8, 100672653) /* Icon - bow/arrow icon */
     , (777720008,   48, 100676435) /* IconUnderlay */
     , (777720008,   50, 100667552); /* IconOverlay - Tier 3 badge */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777720008,  1, 'Master Far Shot Charm')
     , (777720008, 14, 'Double-click to activate. While active, your maximum missile attack range is increased by +41% (up to 120 yards) and final missile damage is increased by +20%.');
