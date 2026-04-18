DELETE FROM `weenie` WHERE `class_Id` = 777700057;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700057, 'ilt_focusedcastingtestcharm', 38, '2026-04-18 00:00:00');

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700057,    11, 1) /* IgnoreCollisions */
     , (777700057,    13, 1) /* Ethereal */
     , (777700057,    14, 1) /* GravityStatus */
     , (777700057, 50000, 1) /* IsAbilityCharm — ILT system */
     , (777700057, 50002, 1) /* IsTestCharm — ILT system */
     , (777700057,    63, 1) /* UnlimitedUse */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700057,     1, 2048) /* ItemType - Misc */
     , (777700057,     5,     5) /* EncumbranceVal */
     , (777700057,     8,     5) /* Mass */
     , (777700057,    16,     8) /* ItemUseable - Contained */
     , (777700057,    83,     2) /* ActivationResponse - Use */
     , (777700057,    19,     1) /* UiEffects - Magical */
     , (777700057,    33,     1) /* Bonded - Bonded */
     , (777700057,    93,  1044) /* PhysicsState */
     , (777700057,   114,     1) /* Attuned - Attuned */
     , (777700057, 50000,     4) /* CharmGrantsAbility - ID 4 = Focused Casting */
     , (777700057, 50005,     1) /* CharmLevel - 1 */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700057,    1, 33558517)  /* Setup - Ember */
     , (777700057,    3, 536870932) /* SoundTable */
     , (777700057,    8, 100676392) /* Icon - Unified Charm Icon (13096) */
     , (777700057,   50, 100663297) /* IconOverlay - Outline 1 */;

INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`)
VALUES (777700057,    10, 3600) /* TimeToRot - 60 minutes */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700057,    1, 'Focused Casting Test Charm')
     , (777700057,   14, 'TEST - Expires after 60 minutes. Double-click to activate Focused Casting. Ability deactivates if this charm expires or leaves your inventory.');
