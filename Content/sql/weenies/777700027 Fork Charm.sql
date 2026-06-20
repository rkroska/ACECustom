-- ============================================================
-- Fork Charm (Tier 1) — WCID 777700027
-- ILT Ability Charm — Ability ID 27 (HasForkCharm)
-- While active, Streak, Arc, and Bolt projectiles fork to nearby
-- enemies on hit, dealing 50% of the original spell's damage.
-- ============================================================

DELETE FROM `weenie` WHERE `class_Id` = 777700027;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700027, 'ilt_forkcharm_t1', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700027,    11, 1)  /* IgnoreCollisions */
     , (777700027,    13, 1)  /* Ethereal */
     , (777700027,    14, 1)  /* GravityStatus */
     , (777700027,    63, 1)  /* UnlimitedUse */
     , (777700027,  9040, 1)  /* IsCharm */
     , (777700027, 50000, 1); /* IsAbilityCharm */

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700027,     1, 2048) /* ItemType - Gem */
     , (777700027,     5,    5) /* EncumbranceVal */
     , (777700027,     8,    5) /* Mass */
     , (777700027,    16,    8) /* ItemUseable - Contained */
     , (777700027,    19,    1) /* UiEffects - Magical */
     , (777700027,    33,    1) /* Bonded */
     , (777700027,    83,    2) /* ActivationResponse - Use */
     , (777700027,    93, 1044) /* PhysicsState */
     , (777700027,   114,    1) /* Attuned */
     , (777700027, 50000,   27) /* CharmGrantsAbility - ID 27 = Fork */
     , (777700027, 50005,    1) /* CharmLevel - 1 */
     , (777700027, 50006,    3); /* CharmMaxLevel - 3 tiers */

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700027,    1, 33554556)  /* Setup - Coffer/Chest */
     , (777700027,    3, 536870932) /* SoundTable */
     , (777700027,    8, 100670725) /* Icon - bolt spell icon */
     , (777700027,   48, 100676435) /* IconUnderlay */
     , (777700027,   50, 100667550); /* IconOverlay - Tier 1 badge */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700027,  1, 'Fork Charm')
     , (777700027, 14, 'Double-click to activate. While active, your Streak, Arc, and Bolt spells will fork to nearby enemies on hit, dealing 50% of the original spell''s damage.');
