-- ==========================================================
-- Truly Infinite Spell Components
-- ==========================================================

-- Helper procedure to clone and make infinite (Conceptual, but I'll write raw SQL)

-- 1. Infinite Mana Scarab (900002) - already partially defined, ensuring full clone
DELETE FROM weenie WHERE class_Id = 900002;
INSERT INTO weenie (class_Id, weenie_Type, last_Modified) SELECT 900002, weenie_Type, last_Modified FROM weenie WHERE class_Id = 3371;
DELETE FROM weenie_properties_string WHERE weenie_class_Id = 900002;
INSERT INTO weenie_properties_string (weenie_class_Id, type, value) SELECT 900002, type, value FROM weenie_properties_string WHERE weenie_class_Id = 3371;
UPDATE weenie_properties_string SET value = 'Infinite Mana Scarab' WHERE weenie_class_Id = 900002 AND type = 1; -- Name
DELETE FROM weenie_properties_int WHERE weenie_class_Id = 900002;
INSERT INTO weenie_properties_int (weenie_class_Id, type, value) SELECT 900002, type, value FROM weenie_properties_int WHERE weenie_class_Id = 3371;
DELETE FROM weenie_properties_bool WHERE weenie_class_Id = 900002;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) SELECT 900002, type, value FROM weenie_properties_bool WHERE weenie_class_Id = 3371;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) VALUES (900002, 63, 1) ON DUPLICATE KEY UPDATE value = 1; -- UnlimitedUse

-- 2. Infinite Diamond Scarab (900003)
DELETE FROM weenie WHERE class_Id = 900003;
INSERT INTO weenie (class_Id, weenie_Type, last_Modified) SELECT 900003, weenie_Type, last_Modified FROM weenie WHERE class_Id = 3367;
INSERT INTO weenie_properties_string (weenie_class_Id, type, value) SELECT 900003, type, value FROM weenie_properties_string WHERE weenie_class_Id = 3367;
UPDATE weenie_properties_string SET value = 'Infinite Diamond Scarab' WHERE weenie_class_Id = 900003 AND type = 1;
INSERT INTO weenie_properties_int (weenie_class_Id, type, value) SELECT 900003, type, value FROM weenie_properties_int WHERE weenie_class_Id = 3367;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) SELECT 900003, type, value FROM weenie_properties_bool WHERE weenie_class_Id = 3367;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) VALUES (900003, 63, 1) ON DUPLICATE KEY UPDATE value = 1;

-- 3. Infinite Emerald Scarab (900004)
DELETE FROM weenie WHERE class_Id = 900004;
INSERT INTO weenie (class_Id, weenie_Type, last_Modified) SELECT 900004, weenie_Type, last_Modified FROM weenie WHERE class_Id = 3368;
INSERT INTO weenie_properties_string (weenie_class_Id, type, value) SELECT 900004, type, value FROM weenie_properties_string WHERE weenie_class_Id = 3368;
UPDATE weenie_properties_string SET value = 'Infinite Emerald Scarab' WHERE weenie_class_Id = 900004 AND type = 1;
INSERT INTO weenie_properties_int (weenie_class_Id, type, value) SELECT 900004, type, value FROM weenie_properties_int WHERE weenie_class_Id = 3368;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) SELECT 900004, type, value FROM weenie_properties_bool WHERE weenie_class_Id = 3368;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) VALUES (900004, 63, 1) ON DUPLICATE KEY UPDATE value = 1;

-- 4. Infinite Jet Scarab (900005)
DELETE FROM weenie WHERE class_Id = 900005;
INSERT INTO weenie (class_Id, weenie_Type, last_Modified) SELECT 900005, weenie_Type, last_Modified FROM weenie WHERE class_Id = 3369;
INSERT INTO weenie_properties_string (weenie_class_Id, type, value) SELECT 900005, type, value FROM weenie_properties_string WHERE weenie_class_Id = 3369;
UPDATE weenie_properties_string SET value = 'Infinite Jet Scarab' WHERE weenie_class_Id = 900005 AND type = 1;
INSERT INTO weenie_properties_int (weenie_class_Id, type, value) SELECT 900005, type, value FROM weenie_properties_int WHERE weenie_class_Id = 3369;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) SELECT 900005, type, value FROM weenie_properties_bool WHERE weenie_class_Id = 3369;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) VALUES (900005, 63, 1) ON DUPLICATE KEY UPDATE value = 1;

-- 5. Infinite Pearl Scarab (900006)
DELETE FROM weenie WHERE class_Id = 900006;
INSERT INTO weenie (class_Id, weenie_Type, last_Modified) SELECT 900006, weenie_Type, last_Modified FROM weenie WHERE class_Id = 3370;
INSERT INTO weenie_properties_string (weenie_class_Id, type, value) SELECT 900006, type, value FROM weenie_properties_string WHERE weenie_class_Id = 3370;
UPDATE weenie_properties_string SET value = 'Infinite Pearl Scarab' WHERE weenie_class_Id = 900006 AND type = 1;
INSERT INTO weenie_properties_int (weenie_class_Id, type, value) SELECT 900006, type, value FROM weenie_properties_int WHERE weenie_class_Id = 3370;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) SELECT 900006, type, value FROM weenie_properties_bool WHERE weenie_class_Id = 3370;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) VALUES (900006, 63, 1) ON DUPLICATE KEY UPDATE value = 1;

-- 6. Infinite Pyreal Scarab (900007)
DELETE FROM weenie WHERE class_Id = 900007;
INSERT INTO weenie (class_Id, weenie_Type, last_Modified) SELECT 900007, weenie_Type, last_Modified FROM weenie WHERE class_Id = 3372;
INSERT INTO weenie_properties_string (weenie_class_Id, type, value) SELECT 900007, type, value FROM weenie_properties_string WHERE weenie_class_Id = 3372;
UPDATE weenie_properties_string SET value = 'Infinite Pyreal Scarab' WHERE weenie_class_Id = 900007 AND type = 1;
INSERT INTO weenie_properties_int (weenie_class_Id, type, value) SELECT 900007, type, value FROM weenie_properties_int WHERE weenie_class_Id = 3372;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) SELECT 900007, type, value FROM weenie_properties_bool WHERE weenie_class_Id = 3372;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) VALUES (900007, 63, 1) ON DUPLICATE KEY UPDATE value = 1;

-- 7. Infinite Red Coral Scarab (900008)
DELETE FROM weenie WHERE class_Id = 900008;
INSERT INTO weenie (class_Id, weenie_Type, last_Modified) SELECT 900008, weenie_Type, last_Modified FROM weenie WHERE class_Id = 3373;
INSERT INTO weenie_properties_string (weenie_class_Id, type, value) SELECT 900008, type, value FROM weenie_properties_string WHERE weenie_class_Id = 3373;
UPDATE weenie_properties_string SET value = 'Infinite Red Coral Scarab' WHERE weenie_class_Id = 900008 AND type = 1;
INSERT INTO weenie_properties_int (weenie_class_Id, type, value) SELECT 900008, type, value FROM weenie_properties_int WHERE weenie_class_Id = 3373;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) SELECT 900008, type, value FROM weenie_properties_bool WHERE weenie_class_Id = 3373;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) VALUES (900008, 63, 1) ON DUPLICATE KEY UPDATE value = 1;

-- 8. Infinite Prismatic Taper (900009)
DELETE FROM weenie WHERE class_Id = 900009;
INSERT INTO weenie (class_Id, weenie_Type, last_Modified) SELECT 900009, weenie_Type, last_Modified FROM weenie WHERE class_Id = 21822;
INSERT INTO weenie_properties_string (weenie_class_Id, type, value) SELECT 900009, type, value FROM weenie_properties_string WHERE weenie_class_Id = 21822;
UPDATE weenie_properties_string SET value = 'Infinite Prismatic Taper' WHERE weenie_class_Id = 900009 AND type = 1;
INSERT INTO weenie_properties_int (weenie_class_Id, type, value) SELECT 900009, type, value FROM weenie_properties_int WHERE weenie_class_Id = 21822;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) SELECT 900009, type, value FROM weenie_properties_bool WHERE weenie_class_Id = 21822;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) VALUES (900009, 63, 1) ON DUPLICATE KEY UPDATE value = 1;
