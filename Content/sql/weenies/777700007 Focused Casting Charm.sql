DELETE FROM `weenie` WHERE `class_Id` = 777700007;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700007, 'ilt_focusedcastingcharm', 38, '2026-04-18 00:00:00');

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700007,    11, 1) /* IgnoreCollisions */
     , (777700007,    13, 1) /* Ethereal */
     , (777700007,    14, 1) /* GravityStatus */
     , (777700007, 50000, 1) /* IsAbilityCharm — ILT system */
     , (777700007,    63, 1) /* UnlimitedUse */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700007,     1, 2048) /* ItemType - Misc */
     , (777700007,     5,     5) /* EncumbranceVal */
     , (777700007,     8,     5) /* Mass */
     , (777700007,    16,     8) /* ItemUseable - Contained */
     , (777700007,    83,     2) /* ActivationResponse - Use */
     , (777700007,    19,     1) /* UiEffects - Magical */
     , (777700007,    33,     1) /* Bonded - Bonded */
     , (777700007,    93,  1044) /* PhysicsState */
     , (777700007,   114,     1) /* Attuned - Attuned */
     , (777700007, 50000,     4) /* CharmGrantsAbility - ID 4 = Focused Casting */
     , (777700007, 50005,     1) /* CharmLevel - 1 */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700007,    1, 33558517)  /* Setup - Ember */
     , (777700007,    3, 536870932) /* SoundTable */
     , (777700007,    8, 100676392) /* Icon - Unified Charm Icon (13096) */
     , (777700007,   50, 100663297) /* IconOverlay - Outline 1 */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700007,    1, 'Focused Casting Charm')
     , (777700007,   14, 'Double-click to activate or deactivate Focused Casting. Magic attacks spend 10% of maximum mana for 2x damage. Mana regeneration is halved while active. Ability deactivates if this charm leaves your inventory.');
