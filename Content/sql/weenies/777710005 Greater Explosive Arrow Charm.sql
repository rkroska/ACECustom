-- ============================================================
-- Greater Explosive Arrow Charm (Tier 2) — WCID 777710005
-- ILT Ability Charm — Ability ID 21 (HasExplosiveArrowCharm)
-- While active, Bow, Crossbow, and Thrown weapon projectiles explode on impact,
-- firing a damage-type-matched ring spell at the target's location after a 1s delay.
-- ============================================================

DELETE FROM `weenie` WHERE `class_Id` = 777710005;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777710005, 'ilt_explosivearrowcharm_level2', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777710005,    11, 1)  /* IgnoreCollisions */
     , (777710005,    13, 1)  /* Ethereal */
     , (777710005,    14, 1)  /* GravityStatus */
     , (777710005,    63, 1)  /* UnlimitedUse */
     , (777710005,  9040, 1)  /* IsCharm */
     , (777710005, 50000, 1); /* IsAbilityCharm */

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777710005,     1, 2048) /* ItemType - Gem */
     , (777710005,     5,    5) /* EncumbranceVal */
     , (777710005,     8,    5) /* Mass */
     , (777710005,    16,    8) /* ItemUseable - Contained */
     , (777710005,    19,    1) /* UiEffects - Magical */
     , (777710005,    33,    1) /* Bonded */
     , (777710005,    83,    2) /* ActivationResponse - Use */
     , (777710005,    93, 1044) /* PhysicsState */
     , (777710005,   114,    1) /* Attuned */
     , (777710005, 50000,   21) /* CharmGrantsAbility - ID 21 = Explosive Arrow Charm */
     , (777710005, 50005,    2) /* CharmLevel - 2 */
     , (777710005, 50006,    3); /* CharmMaxLevel - 3 tiers total */
     -- , (777710005, 50060, 1779768000); /* ItemExpirationTimestamp - Monday May 25, 2026 11:00 PM EST */

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777710005,    1, 33554556)  /* Setup - Coffer/Chest */
     , (777710005,    3, 536870932) /* SoundTable */
     , (777710005,    8, 100672653)  /* Icon - 0x0600248D (AC Icon Viewer 248D) */
     , (777710005,   48, 100676435) /* IconUnderlay */
     , (777710005,   50, 100667551); /* IconOverlay - Tier 2 badge */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777710005,  1, 'Greater Explosive Arrow Charm')
     , (777710005, 14, '
Double-click to activate. While active, Bow, Crossbow, and Thrown weapon projectiles explode on impact, firing a damage-type-matched ring spell at the target''s location after a 1s delay. The explosion deals 75% of the arrow''s damage (65% - 85% random spread).
');
