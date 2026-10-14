-- LEVELLED CHARMS (owner 2026-10-13): Split Arrow, Chain, Pandemic, Improved Cleave.
-- One item per charm, level 1 to 100 (CharmLevel on the item; no Greater / Master items, no upgrade recipes).
-- Ability ids 30-33 (CharmAbilityRegistry), player flags PropertyBool 51201-51204.
-- The item's panel text is built by the server from the live /charm settings at the item's level; the Use text below
-- is only the fixed line under it, so it carries no numbers.
-- How players get them: not decided here (owner: Prestige Coins, later). How a charm gains levels: not built
-- (/charm level <name> <n> is the test tool).
-- Safe to run again: it replaces the four weenies.

DELETE FROM `weenie_properties_bool`   WHERE `object_Id` IN (777700035, 777700036, 777700037, 777700038);
DELETE FROM `weenie_properties_int`    WHERE `object_Id` IN (777700035, 777700036, 777700037, 777700038);
DELETE FROM `weenie_properties_d_i_d`  WHERE `object_Id` IN (777700035, 777700036, 777700037, 777700038);
DELETE FROM `weenie_properties_string` WHERE `object_Id` IN (777700035, 777700036, 777700037, 777700038);
DELETE FROM `weenie` WHERE `class_Id` IN (777700035, 777700036, 777700037, 777700038);

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700035, 'ilt_splitarrowcharm',     38, NOW())   /* type 38 = Gem: its use handler does the on / off toggle */
     , (777700036, 'ilt_chaincharm',          38, NOW())
     , (777700037, 'ilt_pandemiccharm',       38, NOW())
     , (777700038, 'ilt_improvedcleavecharm', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700035,    11, 1) /* IgnoreCollisions */
     , (777700035,    13, 1) /* Ethereal */
     , (777700035,    14, 1) /* GravityStatus */
     , (777700035,    63, 1) /* UnlimitedUse - never consumed */
     , (777700035,  9040, 1) /* IsCharm - the panel header */
     , (777700035, 50000, 1) /* IsAbilityCharm - the toggle */
     , (777700036,    11, 1)
     , (777700036,    13, 1)
     , (777700036,    14, 1)
     , (777700036,    63, 1)
     , (777700036,  9040, 1)
     , (777700036, 50000, 1)
     , (777700037,    11, 1)
     , (777700037,    13, 1)
     , (777700037,    14, 1)
     , (777700037,    63, 1)
     , (777700037,  9040, 1)
     , (777700037, 50000, 1)
     , (777700038,    11, 1)
     , (777700038,    13, 1)
     , (777700038,    14, 1)
     , (777700038,    63, 1)
     , (777700038,  9040, 1)
     , (777700038, 50000, 1);

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700035,     1, 2048) /* ItemType = Gem */
     , (777700035,     5,    5) /* EncumbranceVal */
     , (777700035,     8,    5) /* Mass */
     , (777700035,    16,    8) /* ItemUseable = Contained */
     , (777700035,    19,    1) /* Value */
     , (777700035,    33,    1) /* Bonded - stays on death */
     , (777700035,    83,    2) /* ActivationResponse = Use */
     , (777700035,    93, 1044) /* PhysicsState */
     , (777700035,   114,    1) /* Attuned - no drop / trade / give */
     , (777700035, 50000,   30) /* CharmGrantsAbility = Split Arrow */
     , (777700035, 50005,    1) /* CharmLevel */
     , (777700035, 50006,  100) /* CharmMaxLevel */
     , (777700036,     1, 2048)
     , (777700036,     5,    5)
     , (777700036,     8,    5)
     , (777700036,    16,    8)
     , (777700036,    19,    1)
     , (777700036,    33,    1)
     , (777700036,    83,    2)
     , (777700036,    93, 1044)
     , (777700036,   114,    1)
     , (777700036, 50000,   31) /* CharmGrantsAbility = Chain */
     , (777700036, 50005,    1)
     , (777700036, 50006,  100)
     , (777700037,     1, 2048)
     , (777700037,     5,    5)
     , (777700037,     8,    5)
     , (777700037,    16,    8)
     , (777700037,    19,    1)
     , (777700037,    33,    1)
     , (777700037,    83,    2)
     , (777700037,    93, 1044)
     , (777700037,   114,    1)
     , (777700037, 50000,   32) /* CharmGrantsAbility = Pandemic */
     , (777700037, 50005,    1)
     , (777700037, 50006,  100)
     , (777700038,     1, 2048)
     , (777700038,     5,    5)
     , (777700038,     8,    5)
     , (777700038,    16,    8)
     , (777700038,    19,    1)
     , (777700038,    33,    1)
     , (777700038,    83,    2)
     , (777700038,    93, 1044)
     , (777700038,   114,    1)
     , (777700038, 50000,   33) /* CharmGrantsAbility = Improved Cleave */
     , (777700038, 50005,    1)
     , (777700038, 50006,  100);

-- Icons: picked 2026-10-13 from C:\AI\ZoneControl\CharmIcons (sheets picks_*.png hold the runners-up). No tier badge
-- overlay (type 50): on the older charms it is a fixed "1 / 2 / 3" and these level to 100.
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700035,  1,  33554556) /* Setup - the model every charm uses */
     , (777700035,  3, 536870932) /* SoundTable */
     , (777700035,  8, 100691471) /* Icon - a forked arrow */
     , (777700035, 48, 100676435) /* as every charm has */
     , (777700036,  1,  33554556)
     , (777700036,  3, 536870932)
     , (777700036,  8, 100668340) /* Icon - three stacked bolts */
     , (777700036, 48, 100676435)
     , (777700037,  1,  33554556)
     , (777700037,  3, 536870932)
     , (777700037,  8, 100691561) /* Icon - three nether bolts with skulls */
     , (777700037, 48, 100676435)
     , (777700038,  1,  33554556)
     , (777700038,  3, 536870932)
     , (777700038,  8, 100670801) /* Icon - a blade swinging inside a ring */
     , (777700038, 48, 100676435);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700035,  1, 'Split Arrow Charm')
     , (777700035, 14, 'A charm for archers. Its power grows with its level.')
     , (777700036,  1, 'Chain Charm')
     , (777700036, 14, 'A charm for war mages. Its power grows with its level.')
     , (777700037,  1, 'Pandemic Charm')
     , (777700037, 14, 'A charm for void mages. Its power grows with its level.')
     , (777700038,  1, 'Improved Cleave Charm')
     , (777700038, 14, 'A charm for melee fighters. Its power grows with its level.');
