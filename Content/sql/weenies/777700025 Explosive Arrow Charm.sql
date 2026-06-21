-- ============================================================
-- Explosive Arrow Charm (Tier 1) — WCID 777700025
-- ILT Ability Charm — Ability ID 21 (HasExplosiveArrowCharm)
-- While active, Bow, Crossbow, and Thrown weapon projectiles explode on impact,
-- firing a damage-type-matched ring spell at the target's location after a 1s delay.
-- ============================================================

DELETE FROM `weenie` WHERE `class_Id` = 777700025;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700025, 'ilt_explosivearrowcharm', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700025,    11, 1)  /* IgnoreCollisions */
     , (777700025,    13, 1)  /* Ethereal */
     , (777700025,    14, 1)  /* GravityStatus */
     , (777700025,    63, 1)  /* UnlimitedUse */
     , (777700025,  9040, 1)  /* IsCharm */
     , (777700025, 50000, 1); /* IsAbilityCharm */

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700025,     1, 2048) /* ItemType - Gem */
     , (777700025,     5,    5) /* EncumbranceVal */
     , (777700025,     8,    5) /* Mass */
     , (777700025,    16,    8) /* ItemUseable - Contained */
     , (777700025,    19,    1) /* UiEffects - Magical */
     , (777700025,    33,    1) /* Bonded */
     , (777700025,    83,    2) /* ActivationResponse - Use */
     , (777700025,    93, 1044) /* PhysicsState */
     , (777700025,   114,    1) /* Attuned */
     , (777700025, 50000,   21) /* CharmGrantsAbility - ID 21 = Explosive Arrow Charm */
     , (777700025, 50005,    1) /* CharmLevel - 1 */
     , (777700025, 50006,    3); /* CharmMaxLevel - 3 tiers total */
     -- , (777700025, 50060, 1779768000); /* ItemExpirationTimestamp - Monday May 25, 2026 11:00 PM EST */

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700025,    1, 33554556)  /* Setup - Coffer/Chest */
     , (777700025,    3, 536870932) /* SoundTable */
     , (777700025,    8, 100672653)  /* Icon - 0x0600248D (AC Icon Viewer 248D) */
     , (777700025,   48, 100676435) /* IconUnderlay */
     , (777700025,   50, 100667550); /* IconOverlay - Tier 1 badge */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700025,  1, 'Explosive Arrow Charm')
     , (777700025, 14, '
Double-click to activate. While active, Bow, Crossbow, and Thrown weapon projectiles explode on impact, firing a damage-type-matched ring spell at the target''s location after a 1s delay. The explosion deals 50% of the arrow''s damage (40% - 60% random spread).
');
