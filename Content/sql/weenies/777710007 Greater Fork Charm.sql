-- ============================================================
-- Greater Fork Charm (Tier 2) — WCID 777710007
-- ILT Ability Charm — Ability ID 27 (HasForkCharm)
-- While active, Streak, Arc, and Bolt projectiles fork to nearby
-- enemies on hit, dealing 75% of the original spell's damage.
-- ============================================================

DELETE FROM `weenie` WHERE `class_Id` = 777710007;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777710007, 'ilt_forkcharm_t2', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777710007,    11, 1)  /* IgnoreCollisions */
     , (777710007,    13, 1)  /* Ethereal */
     , (777710007,    14, 1)  /* GravityStatus */
     , (777710007,    63, 1)  /* UnlimitedUse */
     , (777710007,  9040, 1)  /* IsCharm */
     , (777710007, 50000, 1); /* IsAbilityCharm */

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777710007,     1, 2048) /* ItemType - Gem */
     , (777710007,     5,    5) /* EncumbranceVal */
     , (777710007,     8,    5) /* Mass */
     , (777710007,    16,    8) /* ItemUseable - Contained */
     , (777710007,    19,    1) /* UiEffects - Magical */
     , (777710007,    33,    1) /* Bonded */
     , (777710007,    83,    2) /* ActivationResponse - Use */
     , (777710007,    93, 1044) /* PhysicsState */
     , (777710007,   114,    1) /* Attuned */
     , (777710007, 50000,   27) /* CharmGrantsAbility - ID 27 = Fork */
     , (777710007, 50005,    2) /* CharmLevel - 2 */
     , (777710007, 50006,    3); /* CharmMaxLevel - 3 tiers */

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777710007,    1, 33554556)  /* Setup - Coffer/Chest */
     , (777710007,    3, 536870932) /* SoundTable */
     , (777710007,    8, 100670725) /* Icon - bolt spell icon */
     , (777710007,   48, 100676435) /* IconUnderlay */
     , (777710007,   50, 100667551); /* IconOverlay - Tier 2 badge */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777710007,  1, 'Greater Fork Charm')
     , (777710007, 14, 'Double-click to activate. While active, your Streak, Arc, and Bolt spells will fork to nearby enemies on hit, dealing 75% of the original spell''s damage.');
