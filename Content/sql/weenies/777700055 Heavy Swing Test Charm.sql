DELETE FROM `weenie` WHERE `class_Id` = 777700055;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700055, 'ilt_heavyswingtestcharm', 38, '2026-04-17 00:00:00');

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700055,    11, 1) /* IgnoreCollisions */
     , (777700055,    13, 1) /* Ethereal */
     , (777700055,    14, 1) /* GravityStatus */
     , (777700055, 50000, 1) /* IsAbilityCharm — ILT system */
     , (777700055, 50002, 1) /* IsTestCharm — triggers expiry message */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700055,     1, 65536) /* ItemType - Misc */
     , (777700055,     5,     5) /* EncumbranceVal */
     , (777700055,     8,     5) /* Mass */
     , (777700055,    16,     8) /* ItemUseable - Contained */
     , (777700055,    18,     2) /* ActivationResponse - Use */
     , (777700055,    19,     1) /* UiEffects - Magical */
     , (777700055,    33,     1) /* Bonded - Bonded */
     , (777700055,    93,  1044) /* PhysicsState */
     , (777700055,   114,     1) /* Attuned - Attuned */
     , (777700055, 50000,     2) /* CharmGrantsAbility - ID 2 = Heavy Swing */
     , (777700055,   267,  3600) /* Lifespan - 3600 seconds = 60 minutes */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700055,    1, 33554763)  /* Setup - Small Red Gem */
     , (777700055,    3, 536870932) /* SoundTable */
     , (777700055,    8, 100676394) /* Icon */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700055,    1, 'Heavy Swing Test Charm')
     , (777700055,   14, 'TEST — Expires after 60 minutes. Double-click to activate Heavy Swing. Ability deactivates if this charm expires or leaves your inventory.');
