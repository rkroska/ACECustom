-- ============================================================
-- Far Shot Charm (Tier 1) — WCID 777700028
-- ILT Ability Charm — Ability ID 28 (HasFarShotCharm)
-- While active, your maximum missile attack range is increased
-- by +15% and final missile damage is increased by +5%.
-- ============================================================

DELETE FROM `weenie` WHERE `class_Id` = 777700028;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700028, 'ilt_farshotcharm_t1', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700028,    11, 1)  /* IgnoreCollisions */
     , (777700028,    13, 1)  /* Ethereal */
     , (777700028,    14, 1)  /* GravityStatus */
     , (777700028,    63, 1)  /* UnlimitedUse */
     , (777700028,  9040, 1)  /* IsCharm */
     , (777700028, 50000, 1); /* IsAbilityCharm */

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700028,     1, 2048) /* ItemType - Gem */
     , (777700028,     5,    5) /* EncumbranceVal */
     , (777700028,     8,    5) /* Mass */
     , (777700028,    16,    8) /* ItemUseable - Contained */
     , (777700028,    19,    1) /* UiEffects - Magical */
     , (777700028,    33,    1) /* Bonded */
     , (777700028,    83,    2) /* ActivationResponse - Use */
     , (777700028,    93, 1044) /* PhysicsState */
     , (777700028,   114,    1) /* Attuned */
     , (777700028, 50000,   28) /* CharmGrantsAbility - ID 28 = Far Shot */
     , (777700028, 50005,    1) /* CharmLevel - 1 */
     , (777700028, 50006,    3) /* CharmMaxLevel - 3 tiers */
     , (777700028, 50060, 1780887600); /* ItemExpirationTimestamp - Sunday June 7, 2026 11:00 PM EST */

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700028,    1, 33554556)  /* Setup - Coffer/Chest */
     , (777700028,    3, 536870932) /* SoundTable */
     , (777700028,    8, 100672653) /* Icon - bow/arrow icon */
     , (777700028,   48, 100676435) /* IconUnderlay */
     , (777700028,   50, 100667550); /* IconOverlay - Tier 1 badge */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700028,  1, 'Far Shot Charm')
     , (777700028, 14, 'Double-click to activate. While active, your maximum missile attack range is increased by +15% and final missile damage is increased by +5%.');
