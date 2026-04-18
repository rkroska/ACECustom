DELETE FROM `weenie` WHERE `class_Id` = 777700058;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700058, 'ilt_bloodlettingcharm_test', 38, '2026-04-18 00:00:00');

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700058,    11, 1) /* IgnoreCollisions */
     , (777700058,    13, 1) /* Ethereal */
     , (777700058,    14, 1) /* GravityStatus */
     , (777700058, 50000, 1) /* IsAbilityCharm */
     , (777700058, 50002, 1) /* IsTestCharm */
     , (777700058,    63, 1) /* UnlimitedUse */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700058,     1, 2048) /* ItemType - Misc */
     , (777700058,     5,     5) /* EncumbranceVal */
     , (777700058,     8,     5) /* Mass */
     , (777700058,    16,     8) /* ItemUseable - InInventory */
     , (777700058,    83,     2) /* ActivationResponse - Use */
     , (777700058,    19,     1) /* UiEffects - Magical */
     , (777700058,    33,     1) /* Bonded - Bonded */
     , (777700058,    93,  1044) /* PhysicsState */
     , (777700058,   114,     1) /* Attuned - Attuned */
     , (777700058, 50000,     7) /* CharmGrantsAbility - ID 7 = Blood Letting */
     , (777700058, 50005,     1) /* CharmLevel - 1 */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700058,    1, 33558517)  /* Setup - Ember */
     , (777700058,    3, 536870932) /* SoundTable */
     , (777700058,    8, 100676392) /* Icon - Unified Charm Icon (13096) */
     , (777700058,   50, 100663297) /* IconOverlay - Outline 1 */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700058,    1, 'Blood Letting Test Charm')
     , (777700058,   14, 'Double-click to activate or deactivate Blood Letting. This is a testing version that expires. Consumes 10% Max HP per attack/spell for double damage, but reduces HP regen by 50%.');
