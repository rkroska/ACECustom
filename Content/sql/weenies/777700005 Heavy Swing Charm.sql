DELETE FROM `weenie` WHERE `class_Id` = 777700005;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700005, 'ilt_heavyswingcharm', 38, '2026-04-17 00:00:00');

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700005,    11, 1) /* IgnoreCollisions */
     , (777700005,    13, 1) /* Ethereal */
     , (777700005,    14, 1) /* GravityStatus */
     , (777700005, 50000, 1) /* IsAbilityCharm — ILT system */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700005,     1, 65536) /* ItemType - Misc */
     , (777700005,     5,     5) /* EncumbranceVal */
     , (777700005,     8,     5) /* Mass */
     , (777700005,    16,     8) /* ItemUseable - Contained */
     , (777700005,    18,     2) /* ActivationResponse - Use */
     , (777700005,    19,     1) /* UiEffects - Magical */
     , (777700005,    33,     1) /* Bonded - Bonded */
     , (777700005,    93,  1044) /* PhysicsState */
     , (777700005,   114,     1) /* Attuned - Attuned */
     , (777700005, 50000,     2) /* CharmGrantsAbility - ID 2 = Heavy Swing */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700005,    1, 33554763)  /* Setup - Small Red Gem */
     , (777700005,    3, 536870932) /* SoundTable */
     , (777700005,    8, 100676394) /* Icon */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700005,    1, 'Heavy Swing Charm')
     , (777700005,   14, 'Double-click to activate or deactivate Heavy Swing. Melee attacks spend 20% of current stamina for 2x damage. Stamina regeneration is halved while active. Ability deactivates if this charm leaves your inventory.');
