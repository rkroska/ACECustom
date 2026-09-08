-- Carry-over: 1 treasure_death row(s) + 15 prestige_allowed_landblocks rows.
DELETE FROM `treasure_death` WHERE `treasure_Type` IN (73001);
INSERT INTO `treasure_death` (`treasure_Type`, `tier`, `loot_Quality_Mod`, `unknown_Chances`, `item_Chance`, `item_Min_Amount`, `item_Max_Amount`, `item_Treasure_Type_Selection_Chances`, `magic_Item_Chance`, `magic_Item_Min_Amount`, `magic_Item_Max_Amount`, `magic_Item_Treasure_Type_Selection_Chances`, `mundane_Item_Chance`, `mundane_Item_Min_Amount`, `mundane_Item_Max_Amount`, `mundane_Item_Type_Selection_Chances`, `last_Modified`)
VALUES ('73001','11','0.3','19','0','0','0','9','0','0','0','27','0','0','0','0','2026-07-18 01:13:23');

DELETE FROM `prestige_allowed_landblocks` WHERE `landblock` IN (60138, 60138, 60138, 60138, 60138, 60138, 60138, 60138, 60138, 60138, 60138, 60138, 60138, 60138, 60138);
INSERT INTO `prestige_allowed_landblocks` (`tier`, `landblock`, `is_active`, `updated_at`)
VALUES ('2','60138','0','2026-07-16 17:23:57')
     , ('15','60138','0','2026-07-16 17:23:57')
     , ('14','60138','0','2026-07-16 17:23:57')
     , ('13','60138','0','2026-07-16 17:23:57')
     , ('12','60138','0','2026-07-16 17:23:57')
     , ('11','60138','0','2026-07-16 17:23:57')
     , ('10','60138','0','2026-07-16 17:23:57')
     , ('9','60138','0','2026-07-16 17:23:57')
     , ('8','60138','0','2026-07-16 17:23:57')
     , ('7','60138','0','2026-07-16 17:23:57')
     , ('6','60138','0','2026-07-16 17:23:57')
     , ('5','60138','0','2026-07-16 17:23:57')
     , ('4','60138','0','2026-07-16 17:23:57')
     , ('3','60138','0','2026-07-16 17:23:57')
     , ('16','60138','0','2026-07-16 17:23:57');
