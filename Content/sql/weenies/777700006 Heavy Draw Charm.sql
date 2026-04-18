DELETE FROM `weenie` WHERE `class_Id` = 777700006;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700006, 'ilt_heavydrawcharm', 38, '2026-04-17 00:00:00');

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700006,    11, 1) /* IgnoreCollisions */
     , (777700006,    13, 1) /* Ethereal */
     , (777700006,    14, 1) /* GravityStatus */
     , (777700006, 50000, 1) /* IsAbilityCharm — ILT system */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700006,     1, 65536) /* ItemType - Misc */
     , (777700006,     5,     5) /* EncumbranceVal */
     , (777700006,     8,     5) /* Mass */
     , (777700006,    16,     8) /* ItemUseable - Contained */
     , (777700006,    18,     2) /* ActivationResponse - Use */
     , (777700006,    19,     1) /* UiEffects - Magical */
     , (777700006,    33,     1) /* Bonded - Bonded */
     , (777700006,    93,  1044) /* PhysicsState */
     , (777700006,   114,     1) /* Attuned - Attuned */
     , (777700006, 50000,     3) /* CharmGrantsAbility - ID 3 = Heavy Draw */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700006,    1, 33554763)  /* Setup - Small Red Gem (Will palette swap to blue if possible, or just leave as red for now) */
     , (777700006,    3, 536870932) /* SoundTable */
     , (777700006,    6, 16777217)  /* PaletteBase - Trying a blueish one (67111919) */
     , (777700006,    8, 100676395) /* Icon */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700006,    1, 'Heavy Draw Charm')
     , (777700006,   14, 'Double-click to activate or deactivate Heavy Draw. Missile attacks spend 20% of current stamina for 2x damage. Stamina regeneration is halved while active. Ability deactivates if this charm leaves your inventory.');
