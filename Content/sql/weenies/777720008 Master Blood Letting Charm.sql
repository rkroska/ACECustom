DELETE FROM `weenie` WHERE `class_Id` = 777720008;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777720008, 'ilt_bloodlettingcharm_level3', 38, '2026-04-18 00:00:00');

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777720008,    11, 1) /* IgnoreCollisions */
     , (777720008,    13, 1) /* Ethereal */
     , (777720008,    14, 1) /* GravityStatus */
     , (777720008, 50000, 1) /* IsAbilityCharm */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777720008,     1, 2048) /* ItemType - Misc */
     , (777720008,     5,     5) /* EncumbranceVal */
     , (777720008,     8,     5) /* Mass */
     , (777720008,    16,     8) /* ItemUseable - InInventory */
     , (777720008,   114,     1) /* Attuned - Attuned */
     , (777720008, 50000,     7) /* CharmGrantsAbility - ID 7 = Blood Letting */
     , (777720008, 50005,     3) /* CharmLevel - 3 */
     , (777720008,    83,     2) /* ActivationResponse - Use */;

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777720008,    63, 1) /* UnlimitedUse */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777720008,    1, 33558517)  /* Setup - Ember */
     , (777720008,    3, 536870932) /* SoundTable */
     , (777720008,    8, 100676392) /* Icon - Unified Charm Icon (13096) */
     , (777720008,   50, 100663297) /* IconOverlay - Outline 1 */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777720008,    1, 'Master Blood Letting Charm')
     , (777720008,   14, 'Double-click to activate or deactivate Blood Letting. [Tier 3] Consumes 6% Max HP per attack/spell for 3.0x damage, but reduces HP regen by 50%.');
