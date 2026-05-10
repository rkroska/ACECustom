-- ============================================================
-- Agony Charm (Tier 1) — WCID 777700023
-- ILT Ability Charm — Ability ID 20 (HasAgonyCharm)
-- While active, Tectonic Rifts I/II fire Ring of Unspeakable Agony instead.
-- Rocky Shrapnel takes priority if both this and the Shrapnel Charm are active.
-- Requires Ring of Unspeakable Agony (2673) to be in the player's spellbook.
-- ============================================================

DELETE FROM `weenie` WHERE `class_Id` = 777700023;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700023, 'ilt_agonycharm', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700023,    11, 1)  /* IgnoreCollisions */
     , (777700023,    13, 1)  /* Ethereal */
     , (777700023,    14, 1)  /* GravityStatus */
     , (777700023,    63, 1)  /* UnlimitedUse */
     , (777700023,  9040, 1)  /* IsCharm */
     , (777700023, 50000, 1); /* IsAbilityCharm */

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700023,     1, 2048) /* ItemType - Gem */
     , (777700023,     5,    5) /* EncumbranceVal */
     , (777700023,     8,    5) /* Mass */
     , (777700023,    16,    8) /* ItemUseable - Contained */
     , (777700023,    19,    1) /* UiEffects - Magical */
     , (777700023,    33,    1) /* Bonded */
     , (777700023,    83,    2) /* ActivationResponse - Use */
     , (777700023,    93, 1044) /* PhysicsState */
     , (777700023,   114,    1) /* Attuned */
     , (777700023, 50000,   20) /* CharmGrantsAbility - ID 20 = Agony Charm */
     , (777700023, 50005,    1) /* CharmLevel - 1 */
     , (777700023, 50006,    1); /* CharmMaxLevel - 1 tier */

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700023,    1, 33554556)  /* Setup - Coffer/Chest */
     , (777700023,    3, 536870932) /* SoundTable */
     , (777700023,    8, 100670704) /* Icon - 0x06001CF0 (Ring of Unspeakable Agony spell icon) */
     , (777700023,   48, 100676435) /* IconUnderlay */
     , (777700023,   50, 100667550); /* IconOverlay - Tier 1 badge */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700023,  1, 'Agony Charm')
     , (777700023, 14, '
While held, casting Tectonic Rifts I or II will fire Ring of Unspeakable Agony instead. Requires Ring of Unspeakable Agony to be in your spellbook.
');
