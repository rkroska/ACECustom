DELETE FROM `weenie` WHERE `class_Id` = 777710008;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777710008, 'ilt_bloodlettingcharm_level2', 38, '2026-04-18 00:00:00');

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777710008,    11, 1) /* IgnoreCollisions */
     , (777710008,    13, 1) /* Ethereal */
     , (777710008,    14, 1) /* GravityStatus */
     , (777710008, 50000, 1) /* IsAbilityCharm */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777710008,     1, 2048) /* ItemType - Misc */
     , (777710008,     5,     5) /* EncumbranceVal */
     , (777710008,     8,     5) /* Mass */
     , (777710008,    16,     8) /* ItemUseable - InInventory */
     , (777710008,   114,     1) /* Attuned - Attuned */
     , (777710008, 50000,     7) /* CharmGrantsAbility - ID 7 = Blood Letting */
     , (777710008, 50005,     2) /* CharmLevel - 2 */
     , (777710008,    83,     2) /* ActivationResponse - Use */;

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777710008,    63, 1) /* UnlimitedUse */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777710008,    1, 33558517)  /* Setup - Ember */
     , (777710008,    3, 536870932) /* SoundTable */
     , (777710008,    8, 100676392) /* Icon - Unified Charm Icon (13096) */
     , (777710008,   50, 100663297) /* IconOverlay - Outline 1 */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777710008,    1, 'Greater Blood Letting Charm')
     , (777710008,   14, 'Double-click to activate or deactivate Blood Letting. [Tier 2] Consumes 8% Max HP per attack/spell for 2.5x damage, but reduces HP regen by 50%.');
