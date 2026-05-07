-- ============================================================
-- Asheron's Favor (Tier 1) — WCID 777700020
-- ILT Ability Charm — Ability ID 17 (HasAsheronsFavor)
-- Maintains Asheron's Lesser Benediction (+10% Health)
-- and Blackmoor's Favor (+50 Natural Armor) permanently.
-- ============================================================

DELETE FROM `weenie` WHERE `class_Id` = 777700020;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700020, 'ilt_asheronsfavorcharm', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700020,    11, 1)  /* IgnoreCollisions */
     , (777700020,    13, 1)  /* Ethereal */
     , (777700020,    14, 1)  /* GravityStatus */
     , (777700020,    63, 1)  /* UnlimitedUse */
     , (777700020, 50000, 1); /* IsAbilityCharm */

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700020,     1, 2048) /* ItemType - Gem */
     , (777700020,     5,    5) /* EncumbranceVal */
     , (777700020,     8,    5) /* Mass */
     , (777700020,    16,    8) /* ItemUseable - Contained */
     , (777700020,    19,    1) /* UiEffects - Magical */
     , (777700020,    33,    1) /* Bonded */
     , (777700020,    83,    2) /* ActivationResponse - Use */
     , (777700020,    93, 1044) /* PhysicsState */
     , (777700020,   114,    1) /* Attuned */
     , (777700020, 50000,   17) /* CharmGrantsAbility - ID 17 = Asheron's Favor */
     , (777700020, 50005,    1); /* CharmLevel - 1 */

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700020,    1, 33558517)  /* Setup - Ember */
     , (777700020,    3, 536870932) /* SoundTable */
     , (777700020,    8, 100691356) /* Icon */
     , (777700020,   48, 100676435) /* IconUnderlay */
     , (777700020,   50, 100667550); /* IconOverlay - Tier 1 */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700020,  1, 'Asheron''s Favor')
     , (777700020, 14, 'Once carried by emissaries of Asheron himself, this charm resonates with the combined blessings of two of Dereth''s greatest protectors. Double-click to activate. While held, your maximum Health is bolstered by Asheron''s Lesser Benediction and your skin is hardened by the enduring resolve of Blackmoor''s Favor. The blessings persist as long as this charm rests in your possession.');
