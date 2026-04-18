DELETE FROM `weenie` WHERE `class_Id` = 777700056;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700056, 'ilt_heavydrawtestcharm', 38, '2026-04-17 00:00:00');

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700056,    11, 1) /* IgnoreCollisions */
     , (777700056,    13, 1) /* Ethereal */
     , (777700056,    14, 1) /* GravityStatus */
     , (777700056, 50000, 1) /* IsAbilityCharm — ILT system */
     , (777700056, 50002, 1) /* IsTestCharm — triggers expiry message */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700056,     1, 65536) /* ItemType - Misc */
     , (777700056,     5,     5) /* EncumbranceVal */
     , (777700056,     8,     5) /* Mass */
     , (777700056,    16,     8) /* ItemUseable - Contained */
     , (777700056,    18,     2) /* ActivationResponse - Use */
     , (777700056,    19,     1) /* UiEffects - Magical */
     , (777700056,    33,     1) /* Bonded - Bonded */
     , (777700056,    93,  1044) /* PhysicsState */
     , (777700056,   114,     1) /* Attuned - Attuned */
     , (777700056, 50000,     3) /* CharmGrantsAbility - ID 3 = Heavy Draw */
     , (777700056,   267,  3600) /* Lifespan - 3600 seconds = 60 minutes */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700056,    1, 33554763)  /* Setup - Small Red Gem */
     , (777700056,    3, 536870932) /* SoundTable */
     , (777700056,    6, 16777217)  /* PaletteBase - blueish */
     , (777700056,    8, 100676395) /* Icon */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700056,    1, 'Heavy Draw Test Charm')
     , (777700056,   14, 'TEST — Expires after 60 minutes. Double-click to activate Heavy Draw. Ability deactivates if this charm expires or leaves your inventory.');
