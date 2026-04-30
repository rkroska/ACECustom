DELETE FROM `weenie` WHERE `class_Id` = 777700054;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700054, 'ilt_manabarriertestcharm', 38, '2026-04-15 00:00:00');

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700054,    11, 1) /* IgnoreCollisions */
     , (777700054,    13, 1) /* Ethereal */
     , (777700054,    14, 1) /* GravityStatus */
     , (777700054, 50000, 1) /* IsAbilityCharm — ILT system */
     , (777700054, 50002, 1) /* IsTestCharm — triggers expiry message */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700054,     1, 65536) /* ItemType - Misc */
     , (777700054,     5,     5) /* EncumbranceVal */
     , (777700054,     8,     5) /* Mass */
     , (777700054,    16,     8) /* ItemUseable - Contained */
     , (777700054,    83,     2) /* ActivationResponse - Use */
     , (777700054,    18,     1) /* UiEffects - Magical */
     , (777700054,    33,     1) /* Bonded - Bonded */
     , (777700054,    93,  1044) /* PhysicsState */
     , (777700054,   114,     1) /* Attuned - Attuned */
     , (777700054, 50000,     1) /* CharmGrantsAbility - ID 1 = Mana Barrier */
     , (777700054,   267,  3600) /* Lifespan - 3600 seconds = 60 minutes */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700054,    1, 33558517)  /* Setup - Ember */
     , (777700054,    3, 536870932) /* SoundTable */
     , (777700054,    6, 67111919)  /* PaletteBase */
     , (777700054,    8, 100691356) /* Icon */
     , (777700054,   48, 100676435) /* IconUnderlay */
     , (777700054,   50, 100690996) /* IconOverlay - "1" badge */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700054,    1, 'Mana Barrier Test Charm')
     , (777700054,   14, 'TEST — Expires after 60 minutes. Double-click to activate Mana Barrier. Ability deactivates when this charm expires or leaves your inventory.');
