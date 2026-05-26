-- ============================================================
-- Charm of Auto Rebuffing (WCID 777700300)
-- ILT Ability Charm — Ability ID 24 (HasAutoRebuffCharm)
-- Double-click to activate. While active and in inventory,
-- automatically rebuffs you with Level 8s 60 mins before they expire.
-- WeenieType 38 (AugmentationDevice) — matches all ILT charms.
-- ============================================================

DELETE FROM `weenie` WHERE `class_Id` = 777700300;
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 777700300;
DELETE FROM `weenie_properties_int` WHERE `object_Id` = 777700300;
DELETE FROM `weenie_properties_float` WHERE `object_Id` = 777700300;
DELETE FROM `weenie_properties_string` WHERE `object_Id` = 777700300;
DELETE FROM `weenie_properties_d_i_d` WHERE `object_Id` = 777700300;

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700300, 'ilt_autorebuffcharm', 38, NOW());

-- Bools
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(777700300,    11, 1),  -- IgnoreCollisions
(777700300,    13, 1),  -- Ethereal
(777700300,    14, 1),  -- GravityStatus
(777700300,    63, 1),  -- UnlimitedUse
(777700300,  9040, 1),  -- IsCharm — enables tier-aware appraise header
(777700300, 50000, 1);  -- IsAbilityCharm

-- Ints
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(777700300,     1,  2048),  -- ItemType: Gem
(777700300,     5,     5),  -- EncumbranceVal
(777700300,     8,     5),  -- Mass
(777700300,    16,     8),  -- ItemUseable: Contained
(777700300,    83,     2),  -- ActivationResponse: Use
(777700300,    19,     1),  -- UiEffects: Magical
(777700300,    33,     1),  -- Bonded
(777700300,    93,  1044),  -- PhysicsState
(777700300,   114,     1),  -- Attuned
(777700300, 50000,    24),  -- CharmGrantsAbility: ID 24 = Auto Rebuff
(777700300, 50005,     1),  -- CharmLevel: 1
(777700300, 50006,     1);  -- CharmMaxLevel: 1

-- Strings
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(777700300,  1, 'Charm of Auto Rebuffing'),
(777700300, 14, '
On Use: Buff yourself and all equipment instantly.

Active: Automatically rebuffs your equipment and all standard Level 8 self-buffs, Impen VIII, and Banes 60 minutes prior to expiring or if expired.

Dispel Lockout: Dispels trigger a 3-minute lockout where auto-rebuffing is paused. The charm can still be enabled during a lockout; buffs will apply automatically when the lockout expires.
');

-- DataIds (visuals — matching custom charms coffer/chest Setup)
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(777700300,  1, 33554556),   -- Setup (Coffer/Chest)
(777700300,  3, 536870932),  -- SoundTable
(777700300,  6, 67111919),   -- PaletteBase
(777700300,  8, 100672516),  -- Icon (AC Icon Viewer 2404)
(777700300, 48, 100676435),  -- IconUnderlay
(777700300, 50, 100667550);  -- IconOverlay (Tier 1 badge)
