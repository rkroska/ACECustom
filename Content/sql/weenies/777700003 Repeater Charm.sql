DELETE FROM `weenie` WHERE `class_Id` = 777700003;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) 
VALUES (777700003, 'repeatercharm', 38, '2026-04-14 00:00:00');

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) 
VALUES (777700003,   11, 1) /* IgnoreCollisions */
     , (777700003,   13, 1) /* Ethereal */
     , (777700003,   14, 1) /* GravityStatus */
     , (777700003, 9040, 1) /* IsCharm */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) 
VALUES (777700003,    1,     65536) /* ItemType - Misc */
     , (777700003,    5,          5) /* EncumbranceVal */
     , (777700003,    8,          5) /* Mass */
     , (777700003,   16,          8) /* ItemUseable - Contained */
     , (777700003,   18,          2) /* ActivationResponse - Use */
     , (777700003,   19,          1) /* UiEffects - Magical */
     , (777700003,   33,          1) /* Bonded - Bonded */
     , (777700003,   93,       1044) /* PhysicsState */
     , (777700003,  114,          1) /* Attuned - Attuned */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) 
VALUES (777700003,    1, 33558517) /* Setup - Ember */
     , (777700003,    3, 536870932) /* SoundTable */
     , (777700003,    6, 17) /* PaletteBase - Blue/Bright */
     , (777700003,    8, 100676392) /* Icon - Ember */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) 
VALUES (777700003,    1, 'Repeater Charm')
     , (777700003,   14, 'Double-click to toggle Repeating. When active, your single-projectile offensive spells will fire additional bolts from your position.');
