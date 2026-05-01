-- ============================================================
-- Infinite Casting Stone (WCID 777700019)
-- ILT Ability Charm — Ability ID 16 (HasInfiniteCasting)
-- Double-click to activate. While active and in inventory,
-- spells are cast without consuming components.
-- WeenieType 38 (AugmentationDevice) — matches all ILT charms.
-- ============================================================

DELETE FROM weenie WHERE class_Id = 777700019;
DELETE FROM weenie_properties_bool WHERE object_Id = 777700019;
DELETE FROM weenie_properties_int WHERE object_Id = 777700019;
DELETE FROM weenie_properties_float WHERE object_Id = 777700019;
DELETE FROM weenie_properties_string WHERE object_Id = 777700019;
DELETE FROM weenie_properties_d_i_d WHERE object_Id = 777700019;
DELETE FROM weenie_properties_i_i_d WHERE object_Id = 777700019;
DELETE FROM weenie_properties_attribute WHERE object_Id = 777700019;
DELETE FROM weenie_properties_attribute_2nd WHERE object_Id = 777700019;
DELETE FROM weenie_properties_skill WHERE object_Id = 777700019;
DELETE FROM weenie_properties_book WHERE object_Id = 777700019;

INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (777700019, 'ilt_infinitecastingstone', 38, NOW());

-- Bools
INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES
(777700019,    11, 1),  -- IgnoreCollisions
(777700019,    13, 1),  -- Ethereal
(777700019,    14, 1),  -- GravityStatus
(777700019, 50000, 1);  -- IsAbilityCharm

-- Ints
INSERT INTO weenie_properties_int (object_Id, type, value) VALUES
(777700019,     1, 65536),  -- ItemType: Misc
(777700019,     5,     5),  -- EncumbranceVal
(777700019,     8,     5),  -- Mass
(777700019,    16,     8),  -- ItemUseable: Contained
(777700019,    83,     2),  -- ActivationResponse: Use
(777700019,    18,     1),  -- UiEffects: Magical
(777700019,    33,     1),  -- Bonded
(777700019,    93,  1044),  -- PhysicsState
(777700019,   114,     1),  -- Attuned
(777700019, 50000,    16);  -- CharmGrantsAbility: ID 16 = Infinite Casting

-- Strings
INSERT INTO weenie_properties_string (object_Id, type, value) VALUES
(777700019,  1, 'Infinite Casting Stone'),
(777700019, 14, 'A smooth, obsidian-like stone that hums with the infinite echoes of a thousand cast spells. It seems to draw energy from the very air around it.'),
(777700019, 16, 'Double-click to activate. While this stone is in your possession, spells are cast without consuming components.');

-- DataIds (visuals — matching Mana Barrier charm)
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) VALUES
(777700019,  1, 33554975),   -- Setup (0x0200021F - Focus Stone)
(777700019,  3, 536870932),  -- SoundTable
(777700019,  6, 67111919),   -- PaletteBase
(777700019,  8, 100689503),  -- Icon (0x0600665F)
(777700019, 50, 100663297);  -- SoundTable/IconOverlay
