-- ============================================================
-- Infinite Casting Stone (WCID 777700019)
-- ILT Ability Charm — Ability ID 16 (HasInfiniteCasting)
-- Double-click to activate. While active and in inventory,
-- spells are cast without consuming components.
-- WeenieType 38 (AugmentationDevice) — matches all ILT charms.
-- ============================================================

DELETE FROM weenie WHERE class_Id = 777700019;
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
(777700019, 14, 'Double-click to activate. While this stone is in your pack, spells are cast without consuming components.');

-- DataIds (visuals — matching Mana Barrier charm)
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) VALUES
(777700019,  1, 33558517),   -- Setup
(777700019,  3, 536870932),  -- SoundTable
(777700019,  6, 67111919),   -- PaletteBase
(777700019,  8, 100691813),  -- Icon (0x06006F65)
(777700019, 50, 100663297);  -- SoundTable/IconOverlay
