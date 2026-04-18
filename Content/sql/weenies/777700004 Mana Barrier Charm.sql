DELETE FROM `weenie` WHERE `class_Id` = 777700004;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700004, 'ilt_manabarriercharm', 38, '2026-04-15 00:00:00');

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700004,    11, 1) /* IgnoreCollisions */
     , (777700004,    13, 1) /* Ethereal */
     , (777700004,    14, 1) /* GravityStatus */
     , (777700004, 50000, 1) /* IsAbilityCharm — ILT system */
     , (777700004,   63, 1) /* UnlimitedUse */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700004,     1, 2048) /* ItemType - Misc */
     , (777700004,     5,     5) /* EncumbranceVal */
     , (777700004,     8,     5) /* Mass */
     , (777700004,    16,     8) /* ItemUseable - Contained */
     , (777700004,    19,     1) /* UiEffects - Magical */
     , (777700004,    33,     1) /* Bonded - Bonded */
     , (777700004,    83,     2) /* ActivationResponse - Use */
     , (777700004,    93,  1044) /* PhysicsState */
     , (777700004,   114,     1) /* Attuned - Attuned */
     , (777700004, 50000,     1) /* CharmGrantsAbility - ID 1 = Mana Barrier */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700004,    1, 33558517)  /* Setup - Ember */
     , (777700004,    3, 536870932) /* SoundTable */
     , (777700004,    8, 100676392) /* Icon - Unified Charm Icon (13096) */
     , (777700004,   50, 100663297) /* IconOverlay - Outline 1 */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700004,    1, 'Mana Barrier Charm')
     , (777700004,   14, 'Double-click to activate or deactivate Mana Barrier. Reduces damage taken by consuming mana. Ability deactivates if this charm leaves your inventory.');
