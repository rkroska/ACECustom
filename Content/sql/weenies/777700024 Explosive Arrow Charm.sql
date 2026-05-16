-- ============================================================
-- Explosive Arrow Charm (Tier 1) — WCID 777700024
-- ILT Ability Charm — Ability ID 21 (HasExplosiveArrowCharm)
-- While active, each arrow that hits an enemy triggers Ring of Exploding Magma
-- centered on the caster. Requires Exploding Magma (spell 1781) in your spellbook.
-- ============================================================

DELETE FROM `weenie` WHERE `class_Id` = 777700024;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700024, 'ilt_explosivearrowcharm', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700024,    11, 1)  /* IgnoreCollisions */
     , (777700024,    13, 1)  /* Ethereal */
     , (777700024,    14, 1)  /* GravityStatus */
     , (777700024,    63, 1)  /* UnlimitedUse */
     , (777700024,  9040, 1)  /* IsCharm */
     , (777700024, 50000, 1); /* IsAbilityCharm */

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700024,     1, 2048) /* ItemType - Gem */
     , (777700024,     5,    5) /* EncumbranceVal */
     , (777700024,     8,    5) /* Mass */
     , (777700024,    16,    8) /* ItemUseable - Contained */
     , (777700024,    19,    1) /* UiEffects - Magical */
     , (777700024,    33,    1) /* Bonded */
     , (777700024,    83,    2) /* ActivationResponse - Use */
     , (777700024,    93, 1044) /* PhysicsState */
     , (777700024,   114,    1) /* Attuned */
     , (777700024, 50000,   21) /* CharmGrantsAbility - ID 21 = Explosive Arrow Charm */
     , (777700024, 50005,    1) /* CharmLevel - 1 */
     , (777700024, 50006,    3); /* CharmMaxLevel - 3 tiers total */

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700024,    1, 33554556)  /* Setup - Coffer/Chest */
     , (777700024,    3, 536870932) /* SoundTable */
     , (777700024,    8, 100670573) /* Icon - 0x06001D6D (FlameRing/fire ring icon) */
     , (777700024,   48, 100676435) /* IconUnderlay */
     , (777700024,   50, 100667550); /* IconOverlay - Tier 1 badge */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700024,  1, 'Explosive Arrow Charm')
     , (777700024, 14, '
While held, each arrow that connects with an enemy erupts into a Ring of Exploding Magma, blasting nearby foes from your position. Requires Ring of Exploding Magma to be in your spellbook.
');
