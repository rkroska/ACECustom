DELETE FROM `weenie` WHERE `class_Id` = 777700010;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700010, 'ilt_charmcatalyst', 44, '2026-04-18 00:00:00');
/* WeenieType 44 = Stackable (same as Bandit Hilt) - required for "Use On" targeting cursor */

DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 777700010;
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700010,    11, 1) /* IgnoreCollisions */
     , (777700010,    13, 1) /* Ethereal */
     , (777700010,    14, 1) /* GravityStatus */;

DELETE FROM `weenie_properties_int` WHERE `object_Id` = 777700010;
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700010,     1,    128) /* ItemType - Misc (0x80) */
     , (777700010,     5,      5) /* EncumbranceVal */
     , (777700010,     8,      5) /* Value */
     , (777700010,    11,      1) /* MaxStackSize */
     , (777700010,    12,      1) /* StackSize */
     , (777700010,    13,      1) /* NumItemsInMaterial (stack unit) */
     , (777700010,    18,     10) /* UiEffects - Magical */
     , (777700010,    33,      1) /* Bonded - Bonded */
     , (777700010,    16, 524296) /* ItemUseable - SourceContainedTargetContained (0x80008) - triggers "Use On" targeting cursor */
     , (777700010,    93,   1044) /* PhysicsState */
     , (777700010,    94,   2048) /* TargetType - Gem (0x800) - allows targeting Ability Charms */
     , (777700010,   114,      1) /* Attuned - Attuned */;
/* NOTE: No ActivationResponse (83) - not needed for "Use On" crafting items like Bandit Hilt */

DELETE FROM `weenie_properties_d_i_d` WHERE `object_Id` = 777700010;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700010,     1, 33558517)  /* Setup */
     , (777700010,     8, 100676392) /* Icon */
     , (777700010,    22, 872415275) /* IconOverlay - matches Hilt pattern */;

DELETE FROM `weenie_properties_string` WHERE `object_Id` = 777700010;
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700010,    1, 'Charm Catalyst')
     , (777700010,   14, 'A rare crystalline powder used to upgrade Ability Charms to higher tiers via Synthesis. Use this on an Ability Charm to apply it.')
     , (777700010,   15, 'A charm catalyst.')
     , (777700010,   16, 'A well-crafted charm catalyst.');
