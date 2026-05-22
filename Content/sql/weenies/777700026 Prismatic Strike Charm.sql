-- ============================================================
-- Prismatic Strike Charm — WCID 777700026
-- ILT Ability Charm — Ability ID 23 (HasPrismaticStrike)
-- While active, melee attacks scan target resistances, overriding the damage
-- type to their weakest element, and dynamically matching any weapon rending.
-- ============================================================

DELETE FROM `weenie` WHERE `class_Id` = 777700026;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700026, 'ilt_prismaticstrikecharm', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700026,    11, 1)  /* IgnoreCollisions */
     , (777700026,    13, 1)  /* Ethereal */
     , (777700026,    14, 1)  /* GravityStatus */
     , (777700026,    63, 1)  /* UnlimitedUse */
     , (777700026,  9040, 1)  /* IsCharm */
     , (777700026, 50000, 1)  /* IsAbilityCharm */
     , (777700026, 50002, 1); /* IsTestCharm */

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
     , (777700026, 50000,   23) /* CharmGrantsAbility - ID 23 = Prismatic Strike */
     , (777700026, 50005,    1) /* CharmLevel - 1 */
     , (777700026, 50006,    1) /* CharmMaxLevel - 1 */
     , (777700026, 50060, 1779768000); /* ItemExpirationTimestamp - Monday May 25, 2026 11:00 PM EST */

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700026,    1, 33554556)  /* Setup - Coffer/Chest */
     , (777700026,    3, 536870932) /* SoundTable */
     , (777700026,    8, 100673030)  /* Icon - 0x06002606 (AC Icon Viewer 2606) */
     , (777700026,   48, 100676435) /* IconUnderlay */
     , (777700026,   50, 100667552); /* IconOverlay - Master tier gold badge */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700026,  1, 'Prismatic Strike Charm')
     , (777700026, 14, '
Double-click to activate. While active, your melee attacks scan the target creature''s resistances, overriding the damage type to their weakest element or physical type (Slash, Pierce, Bludgeon, Fire, Cold, Acid, Electric, or Nether), and dynamically matching any weapon rending effect to the matched type.
');
