-- Update Infinite Casting Stone to be a Gem (toggleable charm)
-- WCID 777700019
-- WeenieType 35 (Gem)

-- Update WeenieType
UPDATE weenie SET type = 35 WHERE class_Id = 777700019;

-- Add charm properties
DELETE FROM weenie_properties_bool WHERE object_Id = 777700019 AND type = 50000; -- IsAbilityCharm
INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES (777700019, 50000, 1);

DELETE FROM weenie_properties_int WHERE object_Id = 777700019 AND type = 50000; -- CharmGrantsAbility
INSERT INTO weenie_properties_int (object_Id, type, value) VALUES (777700019, 50000, 16);

-- Set to level 1 for now
DELETE FROM weenie_properties_int WHERE object_Id = 777700019 AND type = 50005; -- CharmLevel
INSERT INTO weenie_properties_int (object_Id, type, value) VALUES (777700019, 50005, 1);
