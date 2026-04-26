DELETE FROM `weenie` WHERE `class_Id` = 777700001;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700001, 'ilt_manabarriercharm', 38, '2026-04-18 00:00:00');

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700001,    11, 1) /* IgnoreCollisions */
     , (777700001,    13, 1) /* Ethereal */
     , (777700001,    14, 1) /* GravityStatus */
     , (777700001, 50000, 1) /* IsAbilityCharm — ILT system */
     , (777700001,   63, 1) /* UnlimitedUse */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700001,     1, 2048) /* ItemType - Gem */
     , (777700001,     5,     5) /* EncumbranceVal */
     , (777700001,     8,     5) /* Mass */
     , (777700001,    16,     8) /* ItemUseable - Contained */
     , (777700001,    19,     1) /* UiEffects - Magical */
     , (777700001,    33,     1) /* Bonded - Bonded */
     , (777700001,    83,     2) /* ActivationResponse - Use */
     , (777700001,    93,  1044) /* PhysicsState */
     , (777700001,   114,     1) /* Attuned - Attuned */
     , (777700001, 50000,     1) /* CharmGrantsAbility - ID 1 = Mana Barrier */
     , (777700001, 50005,     1) /* CharmLevel - 1 */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700001,    1, 33558517)  /* Setup - Ember */
     , (777700001,    3, 536870932) /* SoundTable */
     , (777700001,    8, 100691356) /* Icon */
     , (777700001,   48, 100676435) /* IconUnderlay */
     , (777700001,   50, 100667550) /* IconOverlay - Tier 1 */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700001,    1, 'Mana Barrier Charm')
     , (777700001,   14, 'Double-click to activate or deactivate Mana Barrier. Reduces damage taken by consuming mana. Ability deactivates if this charm leaves your inventory.');
