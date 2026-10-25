-- LEVELLED CHARMS, part 2 (owner 2026-10-21, Schneebly): Armor Breaker (dual wield) and Shield Arc (shield).
-- Same shape as 777700035-777700038: one item per charm, level 1 to 100, the level lives on the player
-- (PropertyInt64 51204-51205), ability ids 34-35 (CharmAbilityRegistry), player flags PropertyBool 51207-51208.
-- The panel text comes from the live /charm settings at the item's level; the Use text carries no numbers.
-- ICONS ARE PLACEHOLDERS (they reuse the Improved Cleave and Chain charm icons) - pick real ones before release.
-- Safe to run again: it replaces the two weenies.

DELETE FROM `weenie_properties_bool` WHERE `object_Id` IN (777700039, 777700040);
DELETE FROM `weenie_properties_int` WHERE `object_Id` IN (777700039, 777700040);
DELETE FROM `weenie_properties_d_i_d` WHERE `object_Id` IN (777700039, 777700040);
DELETE FROM `weenie_properties_string` WHERE `object_Id` IN (777700039, 777700040);
DELETE FROM `weenie` WHERE `class_Id` IN (777700039, 777700040);

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700039, 'ilt_armorbreakercharm', 38, NOW())   /* type 38 = Gem: its use handler does the on / off toggle */
     , (777700040, 'ilt_shieldarccharm',    38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700039,    11, 1) /* IgnoreCollisions */
     , (777700039,    13, 1) /* Ethereal */
     , (777700039,    14, 1) /* GravityStatus */
     , (777700039,    63, 1) /* UnlimitedUse - never consumed */
     , (777700039,  9040, 1) /* IsCharm - the panel header */
     , (777700039, 50000, 1) /* IsAbilityCharm - the toggle */
     , (777700040,    11, 1) /* IgnoreCollisions */
     , (777700040,    13, 1) /* Ethereal */
     , (777700040,    14, 1) /* GravityStatus */
     , (777700040,    63, 1) /* UnlimitedUse - never consumed */
     , (777700040,  9040, 1) /* IsCharm - the panel header */
     , (777700040, 50000, 1) /* IsAbilityCharm - the toggle */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700039,     1, 2048) /* ItemType = Gem */
     , (777700039,     5,    5) /* EncumbranceVal */
     , (777700039,     8,    5) /* Mass */
     , (777700039,    16,    8) /* ItemUseable = Contained */
     , (777700039,    19,    1) /* Value */
     , (777700039,    33,    1) /* Bonded - stays on death */
     , (777700039,    83,    2) /* ActivationResponse = Use */
     , (777700039,    93, 1044) /* PhysicsState */
     , (777700039,   114,    1) /* Attuned - no drop / trade / give */
     , (777700039, 50000,   34) /* CharmGrantsAbility = Armor Breaker */
     , (777700039, 50005,    1) /* CharmLevel */
     , (777700039, 50006,  100) /* CharmMaxLevel */
     , (777700040,     1, 2048) /* ItemType = Gem */
     , (777700040,     5,    5) /* EncumbranceVal */
     , (777700040,     8,    5) /* Mass */
     , (777700040,    16,    8) /* ItemUseable = Contained */
     , (777700040,    19,    1) /* Value */
     , (777700040,    33,    1) /* Bonded - stays on death */
     , (777700040,    83,    2) /* ActivationResponse = Use */
     , (777700040,    93, 1044) /* PhysicsState */
     , (777700040,   114,    1) /* Attuned - no drop / trade / give */
     , (777700040, 50000,   35) /* CharmGrantsAbility = Shield Arc */
     , (777700040, 50005,    1) /* CharmLevel */
     , (777700040, 50006,  100) /* CharmMaxLevel */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700039,  1,  33554556) /* Setup - the model every charm uses */
     , (777700039,  3, 536870932) /* SoundTable */
     , (777700039,  8, 100670801) /* Icon - PLACEHOLDER */
     , (777700039, 48, 100676435) /* as every charm has */
     , (777700040,  1,  33554556) /* Setup - the model every charm uses */
     , (777700040,  3, 536870932) /* SoundTable */
     , (777700040,  8, 100668340) /* Icon - PLACEHOLDER */
     , (777700040, 48, 100676435) /* as every charm has */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700039,  1, 'Armor Breaker Charm')
     , (777700039, 14, 'A charm for dual wielders. Its power grows with its level.')
     , (777700040,  1, 'Shield Arc Charm')
     , (777700040, 14, 'A charm for shield bearers. Its power grows with its level.');
