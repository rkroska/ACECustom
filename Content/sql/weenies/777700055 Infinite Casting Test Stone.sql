-- ============================================================
-- Infinite Casting Test Stone (WCID 777700055)
-- ILT Ability Charm — Ability ID 16 (HasInfiniteCasting)
-- TEST VERSION: 60 Minute Duration
-- ============================================================

DELETE FROM weenie WHERE class_Id = 777700055;
DELETE FROM weenie_properties_bool WHERE object_Id = 777700055;
DELETE FROM weenie_properties_int WHERE object_Id = 777700055;
DELETE FROM weenie_properties_float WHERE object_Id = 777700055;
DELETE FROM weenie_properties_string WHERE object_Id = 777700055;
DELETE FROM weenie_properties_d_i_d WHERE object_Id = 777700055;
DELETE FROM weenie_properties_i_i_d WHERE object_Id = 777700055;
DELETE FROM weenie_properties_attribute WHERE object_Id = 777700055;
DELETE FROM weenie_properties_attribute_2nd WHERE object_Id = 777700055;
DELETE FROM weenie_properties_skill WHERE object_Id = 777700055;
DELETE FROM weenie_properties_book WHERE object_Id = 777700055;

INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (777700055, 'ilt_infinitecastingteststone', 38, NOW());

-- Bools
INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES
(777700055,    11, 1),  -- IgnoreCollisions
(777700055,    13, 1),  -- Ethereal
(777700055,    14, 1),  -- GravityStatus
(777700055, 50000, 1),  -- IsAbilityCharm
(777700055, 50002, 1);  -- IsTestCharm (triggers expiry message)

-- Ints
INSERT INTO weenie_properties_int (object_Id, type, value) VALUES
(777700055,     1, 65536),  -- ItemType: Misc
(777700055,     5,     5),  -- EncumbranceVal
(777700055,     8,     5),  -- Mass
(777700055,    16,     8),  -- ItemUseable: Contained
(777700055,    83,     2),  -- ActivationResponse: Use
(777700055,    18,     1),  -- UiEffects: Magical
(777700055,    33,     1),  -- Bonded
(777700055,    93,  1044),  -- PhysicsState
(777700055,   114,     1),  -- Attuned
(777700055,   267,  3600),  -- Lifespan: 60 minutes
(777700055, 50000,    16);  -- CharmGrantsAbility: ID 16 = Infinite Casting

-- Strings
INSERT INTO weenie_properties_string (object_Id, type, value) VALUES
(777700055,  1, 'Infinite Casting Test Stone'),
(777700055, 14, 'A smooth, obsidian-like stone that hums with the infinite echoes of a thousand cast spells. It seems to draw energy from the very air around it.'),
(777700055, 16, 'TEST — Expires after 60 minutes. Double-click to activate. While this stone is in your possession, spells are cast without consuming components.');

-- DataIds
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) VALUES
(777700055,  1, 33554975),   -- Setup (0x0200021F - Focus Stone)
(777700055,  3, 536870932),  -- SoundTable
(777700055,  6, 67111919),   -- PaletteBase
(777700055,  8, 100689503),  -- Icon (0x0600665F)
(777700055, 50, 100663297);  -- IconOverlay
