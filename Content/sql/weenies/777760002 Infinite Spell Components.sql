/*
    Infinite Spell Components (Fixed)
    Range: 777761001 - 777761010
    FIX: Ensured kebab-case class_Names (no spaces) to prevent ACE server lookup crashes.
*/

-- Full cleanup of any previous attempts
DELETE FROM weenie WHERE class_Id BETWEEN 777760000 AND 777762000;
DELETE FROM weenie_properties_int WHERE object_Id BETWEEN 777760000 AND 777762000;
DELETE FROM weenie_properties_bool WHERE object_Id BETWEEN 777760000 AND 777762000;
DELETE FROM weenie_properties_string WHERE object_Id BETWEEN 777760000 AND 777762000;
DELETE FROM weenie_properties_d_i_d WHERE object_Id BETWEEN 777760000 AND 777762000;

-- 1. Infinite Lead Scarab (777761001) <- 691
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 777761001, 'ilt-infinite-lead-scarab', type, NOW() FROM weenie WHERE class_Id = 691;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 777761001, type, value FROM weenie_properties_string WHERE object_Id = 691;
UPDATE weenie_properties_string SET value = 'Infinite Lead Scarab' WHERE object_Id = 777761001 AND type = 1;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 777761001, type, value FROM weenie_properties_int WHERE object_Id = 691;
UPDATE weenie_properties_int SET value = 30000 WHERE object_Id = 777761001 AND type = 20;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 777761001, type, value FROM weenie_properties_bool WHERE object_Id = 691;
INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES (777761001, 63, 1) ON DUPLICATE KEY UPDATE value = 1;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 777761001, type, value FROM weenie_properties_d_i_d WHERE object_Id = 691;

-- 2. Infinite Iron Scarab (777761002) <- 689
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 777761002, 'ilt-infinite-iron-scarab', type, NOW() FROM weenie WHERE class_Id = 689;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 777761002, type, value FROM weenie_properties_string WHERE object_Id = 689;
UPDATE weenie_properties_string SET value = 'Infinite Iron Scarab' WHERE object_Id = 777761002 AND type = 1;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 777761002, type, value FROM weenie_properties_int WHERE object_Id = 689;
UPDATE weenie_properties_int SET value = 30000 WHERE object_Id = 777761002 AND type = 20;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 777761002, type, value FROM weenie_properties_bool WHERE object_Id = 689;
INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES (777761002, 63, 1) ON DUPLICATE KEY UPDATE value = 1;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 777761002, type, value FROM weenie_properties_d_i_d WHERE object_Id = 689;

-- 3. Infinite Copper Scarab (777761003) <- 686
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 777761003, 'ilt-infinite-copper-scarab', type, NOW() FROM weenie WHERE class_Id = 686;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 777761003, type, value FROM weenie_properties_string WHERE object_Id = 686;
UPDATE weenie_properties_string SET value = 'Infinite Copper Scarab' WHERE object_Id = 777761003 AND type = 1;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 777761003, type, value FROM weenie_properties_int WHERE object_Id = 686;
UPDATE weenie_properties_int SET value = 30000 WHERE object_Id = 777761003 AND type = 20;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 777761003, type, value FROM weenie_properties_bool WHERE object_Id = 686;
INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES (777761003, 63, 1) ON DUPLICATE KEY UPDATE value = 1;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 777761003, type, value FROM weenie_properties_d_i_d WHERE object_Id = 686;

-- 4. Infinite Silver Scarab (777761004) <- 688
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 777761004, 'ilt-infinite-silver-scarab', type, NOW() FROM weenie WHERE class_Id = 688;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 777761004, type, value FROM weenie_properties_string WHERE object_Id = 688;
UPDATE weenie_properties_string SET value = 'Infinite Silver Scarab' WHERE object_Id = 777761004 AND type = 1;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 777761004, type, value FROM weenie_properties_int WHERE object_Id = 688;
UPDATE weenie_properties_int SET value = 30000 WHERE object_Id = 777761004 AND type = 20;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 777761004, type, value FROM weenie_properties_bool WHERE object_Id = 688;
INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES (777761004, 63, 1) ON DUPLICATE KEY UPDATE value = 1;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 777761004, type, value FROM weenie_properties_d_i_d WHERE object_Id = 688;

-- 5. Infinite Gold Scarab (777761005) <- 687
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 777761005, 'ilt-infinite-gold-scarab', type, NOW() FROM weenie WHERE class_Id = 687;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 777761005, type, value FROM weenie_properties_string WHERE object_Id = 687;
UPDATE weenie_properties_string SET value = 'Infinite Gold Scarab' WHERE object_Id = 777761005 AND type = 1;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 777761005, type, value FROM weenie_properties_int WHERE object_Id = 687;
UPDATE weenie_properties_int SET value = 30000 WHERE object_Id = 777761005 AND type = 20;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 777761005, type, value FROM weenie_properties_bool WHERE object_Id = 687;
INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES (777761005, 63, 1) ON DUPLICATE KEY UPDATE value = 1;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 777761005, type, value FROM weenie_properties_d_i_d WHERE object_Id = 687;

-- 6. Infinite Pyreal Scarab (777761006) <- 690
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 777761006, 'ilt-infinite-pyreal-scarab', type, NOW() FROM weenie WHERE class_Id = 690;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 777761006, type, value FROM weenie_properties_string WHERE object_Id = 690;
UPDATE weenie_properties_string SET value = 'Infinite Pyreal Scarab' WHERE object_Id = 777761006 AND type = 1;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 777761006, type, value FROM weenie_properties_int WHERE object_Id = 690;
UPDATE weenie_properties_int SET value = 30000 WHERE object_Id = 777761006 AND type = 20;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 777761006, type, value FROM weenie_properties_bool WHERE object_Id = 690;
INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES (777761006, 63, 1) ON DUPLICATE KEY UPDATE value = 1;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 777761006, type, value FROM weenie_properties_d_i_d WHERE object_Id = 690;

-- 7. Infinite Platinum Scarab (777761007) <- 8897
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 777761007, 'ilt-infinite-platinum-scarab', type, NOW() FROM weenie WHERE class_Id = 8897;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 777761007, type, value FROM weenie_properties_string WHERE object_Id = 8897;
UPDATE weenie_properties_string SET value = 'Infinite Platinum Scarab' WHERE object_Id = 777761007 AND type = 1;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 777761007, type, value FROM weenie_properties_int WHERE object_Id = 8897;
UPDATE weenie_properties_int SET value = 30000 WHERE object_Id = 777761007 AND type = 20;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 777761007, type, value FROM weenie_properties_bool WHERE object_Id = 8897;
INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES (777761007, 63, 1) ON DUPLICATE KEY UPDATE value = 1;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 777761007, type, value FROM weenie_properties_d_i_d WHERE object_Id = 8897;

-- 8. Infinite Diamond Scarab (777761008) <- 7299
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 777761008, 'ilt-infinite-diamond-scarab', type, NOW() FROM weenie WHERE class_Id = 7299;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 777761008, type, value FROM weenie_properties_string WHERE object_Id = 7299;
UPDATE weenie_properties_string SET value = 'Infinite Diamond Scarab' WHERE object_Id = 777761008 AND type = 1;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 777761008, type, value FROM weenie_properties_int WHERE object_Id = 7299;
UPDATE weenie_properties_int SET value = 30000 WHERE object_Id = 777761008 AND type = 20;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 777761008, type, value FROM weenie_properties_bool WHERE object_Id = 7299;
INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES (777761008, 63, 1) ON DUPLICATE KEY UPDATE value = 1;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 777761008, type, value FROM weenie_properties_d_i_d WHERE object_Id = 7299;

-- 9. Infinite Mana Scarab (777761009) <- 37155
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 777761009, 'ilt-infinite-mana-scarab', type, NOW() FROM weenie WHERE class_Id = 37155;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 777761009, type, value FROM weenie_properties_string WHERE object_Id = 37155;
UPDATE weenie_properties_string SET value = 'Infinite Mana Scarab' WHERE object_Id = 777761009 AND type = 1;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 777761009, type, value FROM weenie_properties_int WHERE object_Id = 37155;
UPDATE weenie_properties_int SET value = 30000 WHERE object_Id = 777761009 AND type = 20;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 777761009, type, value FROM weenie_properties_bool WHERE object_Id = 37155;
INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES (777761009, 63, 1) ON DUPLICATE KEY UPDATE value = 1;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 777761009, type, value FROM weenie_properties_d_i_d WHERE object_Id = 37155;

-- 10. Infinite Prismatic Taper (777761010) <- 20631
INSERT INTO weenie (class_Id, class_Name, type, last_Modified) SELECT 777761010, 'ilt-infinite-prismatic-taper', type, NOW() FROM weenie WHERE class_Id = 20631;
INSERT INTO weenie_properties_string (object_Id, type, value) SELECT 777761010, type, value FROM weenie_properties_string WHERE object_Id = 20631;
UPDATE weenie_properties_string SET value = 'Infinite Prismatic Taper' WHERE object_Id = 777761010 AND type = 1;
INSERT INTO weenie_properties_int (object_Id, type, value) SELECT 777761010, type, value FROM weenie_properties_int WHERE object_Id = 20631;
UPDATE weenie_properties_int SET value = 30000 WHERE object_Id = 777761010 AND type = 20;
INSERT INTO weenie_properties_bool (object_Id, type, value) SELECT 777761010, type, value FROM weenie_properties_bool WHERE object_Id = 20631;
INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES (777761010, 63, 1) ON DUPLICATE KEY UPDATE value = 1;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value) SELECT 777761010, type, value FROM weenie_properties_d_i_d WHERE object_Id = 20631;
