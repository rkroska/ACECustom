-- ==========================================================
-- Truly Infinite Spell Components (Finalized Range 77776XXXX)
-- ==========================================================

-- 1. Infinite Mana Scarab (777760002)
DELETE FROM weenie WHERE class_Id = 777760002;
INSERT INTO weenie (class_Id, weenie_Type, last_Modified) SELECT 777760002, weenie_Type, last_Modified FROM weenie WHERE class_Id = 3371;
DELETE FROM weenie_properties_string WHERE weenie_class_Id = 777760002;
INSERT INTO weenie_properties_string (weenie_class_Id, type, value) SELECT 777760002, type, value FROM weenie_properties_string WHERE weenie_class_Id = 3371;
UPDATE weenie_properties_string SET value = 'Infinite Mana Scarab' WHERE weenie_class_Id = 777760002 AND type = 1;
DELETE FROM weenie_properties_int WHERE weenie_class_Id = 777760002;
INSERT INTO weenie_properties_int (weenie_class_Id, type, value) SELECT 777760002, type, value FROM weenie_properties_int WHERE weenie_class_Id = 3371;
UPDATE weenie_properties_int SET value = 30000 WHERE weenie_class_Id = 777760002 AND type = 20; -- StackSize (visual)
DELETE FROM weenie_properties_bool WHERE weenie_class_Id = 777760002;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) SELECT 777760002, type, value FROM weenie_properties_bool WHERE weenie_class_Id = 3371;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) VALUES (777760002, 63, 1) ON DUPLICATE KEY UPDATE value = 1; -- UnlimitedUse
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) VALUES (777760002, 60001, 1) ON DUPLICATE KEY UPDATE value = 1; -- IsInfiniteItem

-- 2. Infinite Diamond Scarab (777760003)
DELETE FROM weenie WHERE class_Id = 777760003;
INSERT INTO weenie (class_Id, weenie_Type, last_Modified) SELECT 777760003, weenie_Type, last_Modified FROM weenie WHERE class_Id = 3367;
INSERT INTO weenie_properties_string (weenie_class_Id, type, value) SELECT 777760003, type, value FROM weenie_properties_string WHERE weenie_class_Id = 3367;
UPDATE weenie_properties_string SET value = 'Infinite Diamond Scarab' WHERE weenie_class_Id = 777760003 AND type = 1;
INSERT INTO weenie_properties_int (weenie_class_Id, type, value) SELECT 777760003, type, value FROM weenie_properties_int WHERE weenie_class_Id = 3367;
UPDATE weenie_properties_int SET value = 30000 WHERE weenie_class_Id = 777760003 AND type = 20;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) SELECT 777760003, type, value FROM weenie_properties_bool WHERE weenie_class_Id = 3367;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) VALUES (777760003, 63, 1) ON DUPLICATE KEY UPDATE value = 1;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) VALUES (777760003, 60001, 1) ON DUPLICATE KEY UPDATE value = 1;

-- 3. Infinite Emerald Scarab (777760004)
DELETE FROM weenie WHERE class_Id = 777760004;
INSERT INTO weenie (class_Id, weenie_Type, last_Modified) SELECT 777760004, weenie_Type, last_Modified FROM weenie WHERE class_Id = 3368;
INSERT INTO weenie_properties_string (weenie_class_Id, type, value) SELECT 777760004, type, value FROM weenie_properties_string WHERE weenie_class_Id = 3368;
UPDATE weenie_properties_string SET value = 'Infinite Emerald Scarab' WHERE weenie_class_Id = 777760004 AND type = 1;
INSERT INTO weenie_properties_int (weenie_class_Id, type, value) SELECT 777760004, type, value FROM weenie_properties_int WHERE weenie_class_Id = 3368;
UPDATE weenie_properties_int SET value = 30000 WHERE weenie_class_Id = 777760004 AND type = 20;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) SELECT 777760004, type, value FROM weenie_properties_bool WHERE weenie_class_Id = 3368;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) VALUES (777760004, 63, 1) ON DUPLICATE KEY UPDATE value = 1;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) VALUES (777760004, 60001, 1) ON DUPLICATE KEY UPDATE value = 1;

-- 4. Infinite Jet Scarab (777760005)
DELETE FROM weenie WHERE class_Id = 777760005;
INSERT INTO weenie (class_Id, weenie_Type, last_Modified) SELECT 777760005, weenie_Type, last_Modified FROM weenie WHERE class_Id = 3369;
INSERT INTO weenie_properties_string (weenie_class_Id, type, value) SELECT 777760005, type, value FROM weenie_properties_string WHERE weenie_class_Id = 3369;
UPDATE weenie_properties_string SET value = 'Infinite Jet Scarab' WHERE weenie_class_Id = 777760005 AND type = 1;
INSERT INTO weenie_properties_int (weenie_class_Id, type, value) SELECT 777760005, type, value FROM weenie_properties_int WHERE weenie_class_Id = 3369;
UPDATE weenie_properties_int SET value = 30000 WHERE weenie_class_Id = 777760005 AND type = 20;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) SELECT 777760005, type, value FROM weenie_properties_bool WHERE weenie_class_Id = 3369;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) VALUES (777760005, 63, 1) ON DUPLICATE KEY UPDATE value = 1;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) VALUES (777760005, 60001, 1) ON DUPLICATE KEY UPDATE value = 1;

-- 5. Infinite Pearl Scarab (777760006)
DELETE FROM weenie WHERE class_Id = 777760006;
INSERT INTO weenie (class_Id, weenie_Type, last_Modified) SELECT 777760006, weenie_Type, last_Modified FROM weenie WHERE class_Id = 3370;
INSERT INTO weenie_properties_string (weenie_class_Id, type, value) SELECT 777760006, type, value FROM weenie_properties_string WHERE weenie_class_Id = 3370;
UPDATE weenie_properties_string SET value = 'Infinite Pearl Scarab' WHERE weenie_class_Id = 777760006 AND type = 1;
INSERT INTO weenie_properties_int (weenie_class_Id, type, value) SELECT 777760006, type, value FROM weenie_properties_int WHERE weenie_class_Id = 3370;
UPDATE weenie_properties_int SET value = 30000 WHERE weenie_class_Id = 777760006 AND type = 20;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) SELECT 777760006, type, value FROM weenie_properties_bool WHERE weenie_class_Id = 3370;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) VALUES (777760006, 63, 1) ON DUPLICATE KEY UPDATE value = 1;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) VALUES (777760006, 60001, 1) ON DUPLICATE KEY UPDATE value = 1;

-- 6. Infinite Pyreal Scarab (777760007)
DELETE FROM weenie WHERE class_Id = 777760007;
INSERT INTO weenie (class_Id, weenie_Type, last_Modified) SELECT 777760007, weenie_Type, last_Modified FROM weenie WHERE class_Id = 3372;
INSERT INTO weenie_properties_string (weenie_class_Id, type, value) SELECT 777760007, type, value FROM weenie_properties_string WHERE weenie_class_Id = 3372;
UPDATE weenie_properties_string SET value = 'Infinite Pyreal Scarab' WHERE weenie_class_Id = 777760007 AND type = 1;
INSERT INTO weenie_properties_int (weenie_class_Id, type, value) SELECT 777760007, type, value FROM weenie_properties_int WHERE weenie_class_Id = 3372;
UPDATE weenie_properties_int SET value = 30000 WHERE weenie_class_Id = 777760007 AND type = 20;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) SELECT 777760007, type, value FROM weenie_properties_bool WHERE weenie_class_Id = 3372;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) VALUES (777760007, 63, 1) ON DUPLICATE KEY UPDATE value = 1;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) VALUES (777760007, 60001, 1) ON DUPLICATE KEY UPDATE value = 1;

-- 7. Infinite Red Coral Scarab (777760008)
DELETE FROM weenie WHERE class_Id = 777760008;
INSERT INTO weenie (class_Id, weenie_Type, last_Modified) SELECT 777760008, weenie_Type, last_Modified FROM weenie WHERE class_Id = 3373;
INSERT INTO weenie_properties_string (weenie_class_Id, type, value) SELECT 777760008, type, value FROM weenie_properties_string WHERE weenie_class_Id = 3373;
UPDATE weenie_properties_string SET value = 'Infinite Red Coral Scarab' WHERE weenie_class_Id = 777760008 AND type = 1;
INSERT INTO weenie_properties_int (weenie_class_Id, type, value) SELECT 777760008, type, value FROM weenie_properties_int WHERE weenie_class_Id = 3373;
UPDATE weenie_properties_int SET value = 30000 WHERE weenie_class_Id = 777760008 AND type = 20;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) SELECT 777760008, type, value FROM weenie_properties_bool WHERE weenie_class_Id = 3373;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) VALUES (777760008, 63, 1) ON DUPLICATE KEY UPDATE value = 1;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) VALUES (777760008, 60001, 1) ON DUPLICATE KEY UPDATE value = 1;

-- 8. Infinite Prismatic Taper (777760009)
DELETE FROM weenie WHERE class_Id = 777760009;
INSERT INTO weenie (class_Id, weenie_Type, last_Modified) SELECT 777760009, weenie_Type, last_Modified FROM weenie WHERE class_Id = 21822;
INSERT INTO weenie_properties_string (weenie_class_Id, type, value) SELECT 777760009, type, value FROM weenie_properties_string WHERE weenie_class_Id = 21822;
UPDATE weenie_properties_string SET value = 'Infinite Prismatic Taper' WHERE weenie_class_Id = 777760009 AND type = 1;
INSERT INTO weenie_properties_int (weenie_class_Id, type, value) SELECT 777760009, type, value FROM weenie_properties_int WHERE weenie_class_Id = 21822;
UPDATE weenie_properties_int SET value = 30000 WHERE weenie_class_Id = 777760009 AND type = 20;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) SELECT 777760009, type, value FROM weenie_properties_bool WHERE weenie_class_Id = 21822;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) VALUES (777760009, 63, 1) ON DUPLICATE KEY UPDATE value = 1;
INSERT INTO weenie_properties_bool (weenie_class_Id, type, value) VALUES (777760009, 60001, 1) ON DUPLICATE KEY UPDATE value = 1;
