DELETE FROM `weenie` WHERE `class_Id` = 777700008;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700008, 'ilt_bloodlettingcharm', 38, '2026-04-18 00:00:00');

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700008,    11, 1) /* IgnoreCollisions */
     , (777700008,    13, 1) /* Ethereal */
     , (777700008,    14, 1) /* GravityStatus */
     , (777700008, 50000, 1) /* IsAbilityCharm */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700008,     1, 2048) /* ItemType - Misc */
     , (777700008,     5,     5) /* EncumbranceVal */
     , (777700008,     8,     5) /* Mass */
     , (777700008,    16,     8) /* ItemUseable - InInventory */
     , (777700008,   114,     1) /* Attuned - Attuned */
     , (777700008, 50000,     7) /* CharmGrantsAbility - ID 7 = Blood Letting */
     , (777700008, 50005,     1) /* CharmLevel - 1 */
     , (777700008,    83,     2) /* ActivationResponse - Use */;

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700008,    63, 1) /* UnlimitedUse */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700008,    1, 33558517)  /* Setup - Ember */
     , (777700008,    3, 536870932) /* SoundTable */
     , (777700008,    8, 100676392) /* Icon - Unified Charm Icon (13096) */
     , (777700008,   50, 100663297) /* IconOverlay - Outline 1 */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700008,    1, 'Blood Letting Charm')
     , (777700008,   14, 'Double-click to activate or deactivate Blood Letting. Consumes 10% Max HP per attack/spell for double damage, but reduces HP regen by 50%. Ability deactivates if this charm leaves your inventory.');
