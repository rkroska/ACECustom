-- ============================================================
-- Master Fork Charm (Tier 3) — WCID 777720007
-- ILT Ability Charm — Ability ID 27 (HasForkCharm)
-- While active, Streak, Arc, and Bolt projectiles fork to nearby
-- enemies on hit, dealing 100% of the original spell's damage.
-- ============================================================

DELETE FROM `weenie` WHERE `class_Id` = 777720007;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777720007, 'ilt_forkcharm_t3', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777720007,    11, 1)  /* IgnoreCollisions */
     , (777720007,    13, 1)  /* Ethereal */
     , (777720007,    14, 1)  /* GravityStatus */
     , (777720007,    63, 1)  /* UnlimitedUse */
     , (777720007,  9040, 1)  /* IsCharm */
     , (777720007, 50000, 1); /* IsAbilityCharm */

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777720007,     1, 2048) /* ItemType - Gem */
     , (777720007,     5,    5) /* EncumbranceVal */
     , (777720007,     8,    5) /* Mass */
     , (777720007,    16,    8) /* ItemUseable - Contained */
     , (777720007,    19,    1) /* UiEffects - Magical */
     , (777720007,    33,    1) /* Bonded */
     , (777720007,    83,    2) /* ActivationResponse - Use */
     , (777720007,    93, 1044) /* PhysicsState */
     , (777720007,   114,    1) /* Attuned */
     , (777720007, 50000,   27) /* CharmGrantsAbility - ID 27 = Fork */
     , (777720007, 50005,    3) /* CharmLevel - 3 */
     , (777720007, 50006,    3); /* CharmMaxLevel - 3 tiers */

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777720007,    1, 33554556)  /* Setup - Coffer/Chest */
     , (777720007,    3, 536870932) /* SoundTable */
     , (777720007,    8, 100670725) /* Icon - bolt spell icon */
     , (777720007,   48, 100676435) /* IconUnderlay */
     , (777720007,   50, 100667552); /* IconOverlay - Tier 3 badge */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777720007,  1, 'Master Fork Charm')
     , (777720007, 14, 'Double-click to activate. While active, your Streak, Arc, and Bolt spells will fork to nearby enemies on hit, dealing full damage.');
