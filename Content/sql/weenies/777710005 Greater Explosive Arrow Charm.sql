-- ============================================================
-- Greater Explosive Arrow Charm (Tier 2) — WCID 777710005
-- ILT Ability Charm — Ability ID 21 (HasExplosiveArrowCharm)
-- While active, each arrow that hits an enemy triggers Ring of Exploding Magma
-- centered on the caster. Requires Exploding Magma (spell 1781) in your spellbook.
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

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777710005,    1, 33554556)  /* Setup - Coffer/Chest */
     , (777710005,    3, 536870932) /* SoundTable */
     , (777710005,    8, 100670573) /* Icon - 0x06001D6D (FlameRing/fire ring icon) */
     , (777710005,   48, 100676435) /* IconUnderlay */
     , (777710005,   50, 100667551); /* IconOverlay - Tier 2 badge */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777710005,  1, 'Greater Explosive Arrow Charm')
     , (777710005, 14, '
While held, each arrow that connects with an enemy erupts into a Ring of Exploding Magma, blasting nearby foes from your position. Requires Ring of Exploding Magma to be in your spellbook.
');
