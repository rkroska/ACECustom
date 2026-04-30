-- Greater Mana Barrier (Level 2)
DELETE FROM `weenie` WHERE `class_Id` = 777710001;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777710001, 'ilt_manabarriercharm_level2', 38, '2026-04-25 00:00:00');

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777710001,    11, 1) /* IgnoreCollisions */
     , (777710001,    13, 1) /* Ethereal */
     , (777710001,    14, 1) /* GravityStatus */
     , (777710001,   63, 1) /* UnlimitedUse */
     , (777710001, 50000, 1) /* IsAbilityCharm */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777710001,     1, 2048) /* ItemType - Gem */
     , (777710001,     5,     5) /* EncumbranceVal */
     , (777710001,     8,     5) /* Mass */
     , (777710001,    16,     8) /* ItemUseable - Contained */
     , (777710001,    18,     1) /* UiEffects - Magical */
     , (777710001,    33,     1) /* Bonded - Bonded */
     , (777710001,    83,     2) /* ActivationResponse - Use */
     , (777710001,    93,  1044) /* PhysicsState */
     , (777710001,   114,     1) /* Attuned - Attuned */
     , (777710001, 50000,     1) /* CharmGrantsAbility - ID 1 = Mana Barrier */
     , (777710001, 50005,     2) /* CharmLevel - 2 */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777710001,    1, 33558517)  /* Setup - Ember */
     , (777710001,    3, 536870932) /* SoundTable */
     , (777710001,    8, 100691356) /* Icon */
     , (777710001,   48, 100676435) /* IconUnderlay */
     , (777710001,   50, 100667551) /* IconOverlay - Tier 2 */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777710001,    1, 'Greater Mana Barrier Charm')
     , (777710001,   14, 'Double-click to activate or deactivate Mana Barrier. [Tier 2] Absorbs more damage per mana point consumed.');

-- Master Mana Barrier (Level 3)
DELETE FROM `weenie` WHERE `class_Id` = 777720001;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777720001, 'ilt_manabarriercharm_level3', 38, '2026-04-25 00:00:00');

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777720001,    11, 1) /* IgnoreCollisions */
     , (777720001,    13, 1) /* Ethereal */
     , (777720001,    14, 1) /* GravityStatus */
     , (777720001,   63, 1) /* UnlimitedUse */
     , (777720001, 50000, 1) /* IsAbilityCharm */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777720001,     1, 2048) /* ItemType - Gem */
     , (777720001,     5,     5) /* EncumbranceVal */
     , (777720001,     8,     5) /* Mass */
     , (777720001,    16,     8) /* ItemUseable - Contained */
     , (777720001,    18,     1) /* UiEffects - Magical */
     , (777720001,    33,     1) /* Bonded - Bonded */
     , (777720001,    83,     2) /* ActivationResponse - Use */
     , (777720001,    93,  1044) /* PhysicsState */
     , (777720001,   114,     1) /* Attuned - Attuned */
     , (777720001, 50000,     1) /* CharmGrantsAbility - ID 1 = Mana Barrier */
     , (777720001, 50005,     3) /* CharmLevel - 3 */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777720001,    1, 33558517)  /* Setup - Ember */
     , (777720001,    3, 536870932) /* SoundTable */
     , (777720001,    8, 100691356) /* Icon */
     , (777720001,   48, 100676435) /* IconUnderlay */
     , (777720001,   50, 100667552) /* IconOverlay - Tier 3 */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777720001,    1, 'Master Mana Barrier Charm')
     , (777720001,   14, 'Double-click to activate or deactivate Mana Barrier. [Tier 3] Absorbs significantly more damage per mana point consumed.');
