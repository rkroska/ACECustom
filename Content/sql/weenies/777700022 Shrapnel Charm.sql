-- ============================================================
-- Shrapnel Charm (Tier 1) — WCID 777700022
-- ILT Ability Charm — Ability ID 19 (HasShrapnelCharm)
-- While active, Tectonic Rifts I (1789) and II (6196) are
-- redirected to cast Rocky Shrapnel (6152) instead.
-- Requires Rocky Shrapnel to be in the player's spellbook.
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
     , (777700022,  9041,   19) /* AbilityId = 19 (ShrapnelCharm) */
     , (777700022, 50005,    1) /* CharmLevel - Tier 1 of 1 */
     , (777700022, 50006,    1) /* CharmMaxLevel */
     , (777700022,   188,    1) /* HookType */
     , (777700022,   187, 41943040); /* ValidHookTypes */

INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`)
VALUES (777700022, 54, 1.0); /* DefaultScale */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700022,  1, 'Shrapnel Charm')
     , (777700022, 16, 'A charm that redirects the earth''s fury. While active, casting Tectonic Rifts I or II will instead unleash Rocky Shrapnel — provided you have learned that spell.');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700022,  1, 0x02000001)  /* Setup */
     , (777700022,  8, 0x06002B33)  /* Icon - gem icon */
     , (777700022, 22, 0x20000001); /* PhysicsEffectTable */
