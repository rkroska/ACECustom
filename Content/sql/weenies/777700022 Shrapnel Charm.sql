-- ============================================================
-- Shrapnel Charm (Tier 1) — WCID 777700022
-- ILT Ability Charm — Ability ID 19 (HasShrapnelCharm)
-- While active, Tectonic Rifts I/II fire Rocky Shrapnel instead.
-- Requires Rocky Shrapnel (6152) to be in the player's spellbook.
-- ============================================================

DELETE FROM `weenie` WHERE `class_Id` = 777700022;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700022, 'ilt_shrapnelcharm', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700022,    11, 1)  /* IgnoreCollisions */
     , (777700022,    13, 1)  /* Ethereal */
     , (777700022,    14, 1)  /* GravityStatus */
     , (777700022,    63, 1)  /* UnlimitedUse */
     , (777700022,  9040, 1)  /* IsCharm */
     , (777700022, 50000, 1); /* IsAbilityCharm */

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700022,     1, 2048) /* ItemType - Gem */
     , (777700022,     5,    5) /* EncumbranceVal */
     , (777700022,     8,    5) /* Mass */
     , (777700022,    16,    8) /* ItemUseable - Contained */
     , (777700022,    19,    1) /* UiEffects - Magical */
     , (777700022,    33,    1) /* Bonded */
     , (777700022,    83,    2) /* ActivationResponse - Use */
     , (777700022,    93, 1044) /* PhysicsState */
     , (777700022,   114,    1) /* Attuned */
     , (777700022, 50000,   19) /* CharmGrantsAbility - ID 19 = Shrapnel Charm */
     , (777700022, 50005,    1) /* CharmLevel - 1 */
     , (777700022, 50006,    1); /* CharmMaxLevel - 1 tier */

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700022,    1, 33554556)  /* Setup - Coffer/Chest */
     , (777700022,    3, 536870932) /* SoundTable */
     , (777700022,    8, 100670704) /* Icon - 0x06001CF0 (Rocky Shrapnel spell icon) */
     , (777700022,   48, 100676435) /* IconUnderlay */
     , (777700022,   50, 100667550); /* IconOverlay - Tier 1 badge */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700022,  1, 'Shrapnel Charm')
     , (777700022, 14, '
While held, casting Tectonic Rifts I or II will fire Rocky Shrapnel instead. Requires Rocky Shrapnel to be in your spellbook.
');
