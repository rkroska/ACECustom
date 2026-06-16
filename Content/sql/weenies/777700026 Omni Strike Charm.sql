-- ============================================================
-- Omni Strike Charm — WCID 777700026
-- ILT Ability Charm — Ability ID 23 (HasPrismaticStrike)
-- While active, melee attacks scan target resistances, overriding the damage
-- type to their weakest element, and dynamically matching any weapon rending.
-- ============================================================

DELETE FROM `weenie` WHERE `class_Id` = 777700026;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700026, 'ilt_omnistrikecharm', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700026,    11, 1)  /* IgnoreCollisions */
     , (777700026,    13, 1)  /* Ethereal */
     , (777700026,    14, 1)  /* GravityStatus */
     , (777700026,    63, 1)  /* UnlimitedUse */
     , (777700026,  9040, 1)  /* IsCharm */
     , (777700026, 50000, 1); /* IsAbilityCharm */

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700026,     1, 2048) /* ItemType - Gem */
     , (777700026,     5,    5) /* EncumbranceVal */
     , (777700026,     8,    5) /* Mass */
     , (777700026,    16,    8) /* ItemUseable - Contained */
     , (777700026,    19,    1) /* UiEffects - Magical */
     , (777700026,    33,    1) /* Bonded */
     , (777700026,    83,    2) /* ActivationResponse - Use */
     , (777700026,    93, 1044) /* PhysicsState */
     , (777700026,   114,    1) /* Attuned */
     , (777700026, 50000,   23) /* CharmGrantsAbility - ID 23 = Omni Strike */
     , (777700026, 50005,    1) /* CharmLevel - 1 */
     , (777700026, 50006,    1); /* CharmMaxLevel - 1 */

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700026,    1, 33554556)  /* Setup - Coffer/Chest */
     , (777700026,    3, 536870932) /* SoundTable */
     , (777700026,    8, 100692234)  /* Icon - 0x0600710A */
     , (777700026,   48, 100676435) /* IconUnderlay */
     , (777700026,   50, 100667550); /* IconOverlay - Tier 1 badge */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700026,  1, 'Omni Strike Charm')
     , (777700026, 14, 'Double-click to activate. While active, your melee attacks dynamically align with the currents of magic, striking with the exact element or physical force your target is most vulnerable to. Weapon rending effects are dynamically attuned to match this weakness.');
