-- Greater Mana Barrier (Level 2)
DELETE FROM `weenie` WHERE `class_Id` = 777710004;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777710004, 'ilt_manabarriercharm_level2', 38, '2026-04-18 00:00:00');

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777710004,    11, 1) /* IgnoreCollisions */
     , (777710004,    13, 1) /* Ethereal */
     , (777710004,    14, 1) /* GravityStatus */
     , (777710004,   63, 1) /* UnlimitedUse */
     , (777710004, 50000, 1) /* IsAbilityCharm */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777710004,     1, 2048) /* ItemType - Misc */
     , (777710004,     5,     5) /* EncumbranceVal */
     , (777710004,     8,     5) /* Mass */
     , (777710004,    16,     8) /* ItemUseable - Contained */
     , (777710004,    19,     1) /* UiEffects - Magical */
     , (777710004,    33,     1) /* Bonded - Bonded */
     , (777710004,    83,     2) /* ActivationResponse - Use */
     , (777710004,    93,  1044) /* PhysicsState */
     , (777710004,   114,     1) /* Attuned - Attuned */
     , (777710004, 50000,     1) /* CharmGrantsAbility - ID 1 = Mana Barrier */
     , (777710004, 50005,     2) /* CharmLevel - 2 */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777710004,    1, 33558517)  /* Setup - Ember */
     , (777710004,    3, 536870932) /* SoundTable */
     , (777710004,    8, 100676392) /* Icon - Unified Charm Icon (13096) */
     , (777710004,   50, 100663297) /* IconOverlay - Outline 1 */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777710004,    1, 'Greater Mana Barrier Charm')
     , (777710004,   14, 'Double-click to activate or deactivate Mana Barrier. [Tier 2] Absorbs more damage per mana point consumed.');

-- Master Mana Barrier (Level 3)
DELETE FROM `weenie` WHERE `class_Id` = 777720004;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777720004, 'ilt_manabarriercharm_level3', 38, '2026-04-18 00:00:00');

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777720004,    11, 1) /* IgnoreCollisions */
     , (777720004,    13, 1) /* Ethereal */
     , (777720004,    14, 1) /* GravityStatus */
     , (777720004,   63, 1) /* UnlimitedUse */
     , (777720004, 50000, 1) /* IsAbilityCharm */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777720004,     1, 2048) /* ItemType - Misc */
     , (777720004,     5,     5) /* EncumbranceVal */
     , (777720004,     8,     5) /* Mass */
     , (777720004,    16,     8) /* ItemUseable - Contained */
     , (777720004,    19,     1) /* UiEffects - Magical */
     , (777720004,    33,     1) /* Bonded - Bonded */
     , (777720004,    83,     2) /* ActivationResponse - Use */
     , (777720004,    93,  1044) /* PhysicsState */
     , (777720004,   114,     1) /* Attuned - Attuned */
     , (777720004, 50000,     1) /* CharmGrantsAbility - ID 1 = Mana Barrier */
     , (777720004, 50005,     3) /* CharmLevel - 3 */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777720004,    1, 33558517)  /* Setup - Ember */
     , (777720004,    3, 536870932) /* SoundTable */
     , (777720004,    8, 100676392) /* Icon - Unified Charm Icon (13096) */
     , (777720004,   50, 100663297) /* IconOverlay - Outline 1 */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777720004,    1, 'Master Mana Barrier Charm')
     , (777720004,   14, 'Double-click to activate or deactivate Mana Barrier. [Tier 3] Absorbs significantly more damage per mana point consumed.');
