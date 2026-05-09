-- ============================================================
-- Greater Artisan's Charm (Tier 2) — WCID 777710003
-- ILT Ability Charm — Ability ID 18 (HasArtisanCharm)
-- Increases imbue success chance by +8% while held.
-- Max imbue chance: 38% (base) + 5% (aug) + 8% (charm) = 51%
-- ============================================================

DELETE FROM `weenie` WHERE `class_Id` = 777710003;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777710003, 'ilt_artisanscharm_level2', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777710003,    11, 1)  /* IgnoreCollisions */
     , (777710003,    13, 1)  /* Ethereal */
     , (777710003,    14, 1)  /* GravityStatus */
     , (777710003,    63, 1)  /* UnlimitedUse */
     , (777710003,  9040, 1)  /* IsCharm */
     , (777710003, 50000, 1); /* IsAbilityCharm */

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777710003,     1, 2048) /* ItemType - Gem */
     , (777710003,     5,    5) /* EncumbranceVal */
     , (777710003,     8,    5) /* Mass */
     , (777710003,    16,    8) /* ItemUseable - Contained */
     , (777710003,    19,    1) /* UiEffects - Magical */
     , (777710003,    33,    1) /* Bonded */
     , (777710003,    83,    2) /* ActivationResponse - Use */
     , (777710003,    93, 1044) /* PhysicsState */
     , (777710003,   114,    1) /* Attuned */
     , (777710003, 50000,   18) /* CharmGrantsAbility - ID 18 = Artisan's Charm */
     , (777710003, 50005,    2) /* CharmLevel - 2 */
     , (777710003, 50006,    3); /* CharmMaxLevel - 3 tiers total */

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777710003,    1, 33554556)  /* Setup - Coffer/Chest */
     , (777710003,    3, 536870932) /* SoundTable */
     , (777710003,    8, 100669779) /* Icon - 0x06001953 */
     , (777710003,   48, 100676435) /* IconUnderlay */
     , (777710003,   50, 100667551); /* IconOverlay - Tier 2 badge */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777710003,  1, 'Greater Artisan''s Charm')
     , (777710003, 14, '
While held, your imbue success chance is increased by 8%.
');

-- ============================================================
-- Master Artisan's Charm (Tier 3) — WCID 777720003
-- ILT Ability Charm — Ability ID 18 (HasArtisanCharm)
-- Increases imbue success chance by +12% while held.
-- Max imbue chance: 38% (base) + 5% (aug) + 12% (charm) = 55%
-- ============================================================

DELETE FROM `weenie` WHERE `class_Id` = 777720003;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777720003, 'ilt_artisanscharm_level3', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777720003,    11, 1)  /* IgnoreCollisions */
     , (777720003,    13, 1)  /* Ethereal */
     , (777720003,    14, 1)  /* GravityStatus */
     , (777720003,    63, 1)  /* UnlimitedUse */
     , (777720003,  9040, 1)  /* IsCharm */
     , (777720003, 50000, 1); /* IsAbilityCharm */

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777720003,     1, 2048) /* ItemType - Gem */
     , (777720003,     5,    5) /* EncumbranceVal */
     , (777720003,     8,    5) /* Mass */
     , (777720003,    16,    8) /* ItemUseable - Contained */
     , (777720003,    19,    1) /* UiEffects - Magical */
     , (777720003,    33,    1) /* Bonded */
     , (777720003,    83,    2) /* ActivationResponse - Use */
     , (777720003,    93, 1044) /* PhysicsState */
     , (777720003,   114,    1) /* Attuned */
     , (777720003, 50000,   18) /* CharmGrantsAbility - ID 18 = Artisan's Charm */
     , (777720003, 50005,    3) /* CharmLevel - 3 */
     , (777720003, 50006,    3); /* CharmMaxLevel - 3 tiers total */

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777720003,    1, 33554556)  /* Setup - Coffer/Chest */
     , (777720003,    3, 536870932) /* SoundTable */
     , (777720003,    8, 100669779) /* Icon - 0x06001953 */
     , (777720003,   48, 100676435) /* IconUnderlay */
     , (777720003,   50, 100667552); /* IconOverlay - Tier 3 badge */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777720003,  1, 'Master Artisan''s Charm')
     , (777720003, 14, '
While held, your imbue success chance is increased by 12%.
');
