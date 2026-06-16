-- ============================================================
-- Master Explosive Arrow Charm (Tier 3) — WCID 777720005
-- ILT Ability Charm — Ability ID 21 (HasExplosiveArrowCharm)
-- While active, Bow, Crossbow, and Thrown weapon projectiles explode on impact,
-- firing a damage-type-matched ring spell at the target's location after a 1s delay.
-- ============================================================

DELETE FROM `weenie` WHERE `class_Id` = 777720005;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777720005, 'ilt_explosivearrowcharm_level3', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777720005,    11, 1)  /* IgnoreCollisions */
     , (777720005,    13, 1)  /* Ethereal */
     , (777720005,    14, 1)  /* GravityStatus */
     , (777720005,    63, 1)  /* UnlimitedUse */
     , (777720005,  9040, 1)  /* IsCharm */
     , (777720005, 50000, 1); /* IsAbilityCharm */

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777720005,     1, 2048) /* ItemType - Gem */
     , (777720005,     5,    5) /* EncumbranceVal */
     , (777720005,     8,    5) /* Mass */
     , (777720005,    16,    8) /* ItemUseable - Contained */
     , (777720005,    19,    1) /* UiEffects - Magical */
     , (777720005,    33,    1) /* Bonded */
     , (777720005,    83,    2) /* ActivationResponse - Use */
     , (777720005,    93, 1044) /* PhysicsState */
     , (777720005,   114,    1) /* Attuned */
     , (777720005, 50000,   21) /* CharmGrantsAbility - ID 21 = Explosive Arrow Charm */
     , (777720005, 50005,    3) /* CharmLevel - 3 */
     , (777720005, 50006,    3); /* CharmMaxLevel - 3 tiers total */
     -- , (777720005, 50060, 1779768000); /* ItemExpirationTimestamp - Monday May 25, 2026 11:00 PM EST */

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777720005,    1, 33554556)  /* Setup - Coffer/Chest */
     , (777720005,    3, 536870932) /* SoundTable */
     , (777720005,    8, 100672653)  /* Icon - 0x0600248D (AC Icon Viewer 248D) */
     , (777720005,   48, 100676435) /* IconUnderlay */
     , (777720005,   50, 100667552); /* IconOverlay - Tier 3 badge */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777720005,  1, 'Master Explosive Arrow Charm')
     , (777720005, 14, '
Double-click to activate. While active, Bow, Crossbow, and Thrown weapon projectiles explode on impact, firing a damage-type-matched ring spell at the target''s location after a 1s delay. The explosion deals 100% of the arrow''s damage (90% - 110% random spread).
');
