-- ============================================================
-- Artisan's Charm (Tier 1) — WCID 777700021
-- ILT Ability Charm — Ability ID 18 (HasArtisanCharm)
-- Increases imbue success chance by +4% while held.
-- Max imbue chance: 38% (base) + 5% (aug) + 4% (charm) = 47%
-- ============================================================

DELETE FROM `weenie` WHERE `class_Id` = 777700021;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700021, 'ilt_artisanscharm', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700021,    11, 1)  /* IgnoreCollisions */
     , (777700021,    13, 1)  /* Ethereal */
     , (777700021,    14, 1)  /* GravityStatus */
     , (777700021,    63, 1)  /* UnlimitedUse */
     , (777700021,  9040, 1)  /* IsCharm */
     , (777700021, 50000, 1); /* IsAbilityCharm */

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700021,     1, 2048) /* ItemType - Gem */
     , (777700021,     5,    5) /* EncumbranceVal */
     , (777700021,     8,    5) /* Mass */
     , (777700021,    16,    8) /* ItemUseable - Contained */
     , (777700021,    19,    1) /* UiEffects - Magical */
     , (777700021,    33,    1) /* Bonded */
     , (777700021,    83,    2) /* ActivationResponse - Use */
     , (777700021,    93, 1044) /* PhysicsState */
     , (777700021,   114,    1) /* Attuned */
     , (777700021, 50000,   18) /* CharmGrantsAbility - ID 18 = Artisan's Charm */
     , (777700021, 50005,    1) /* CharmLevel - 1 */
     , (777700021, 50006,    3); /* CharmMaxLevel - 3 tiers total */

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700021,    1, 33554556)  /* Setup - Coffer/Chest */
     , (777700021,    3, 536870932) /* SoundTable */
     , (777700021,    8, 100669779) /* Icon - 0x06001953 */
     , (777700021,   48, 100676435) /* IconUnderlay */
     , (777700021,   50, 100667550); /* IconOverlay - Tier 1 badge */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700021,  1, 'Artisan''s Charm')
     , (777700021, 14, '
While held, your imbue success chance is increased by 4%.
');
