-- Carry-over: 8 charm-upgrade recipes + 8 cook_book rows.
-- (The 5 hilt-attach recipes 719220046-050 were removed 2026-08-08: dead duplicates of
-- ILT's live hilt system; original preserved in Migration pruned-archive\fixtures-2026-08-08.)
DELETE FROM `recipe`
WHERE `id` IN (777701004, 777701005, 777701008, 777701009, 777701010, 777701011, 777701012, 777701013);

INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Source_Message`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `success_Destroy_Target_Message`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Source_Message`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `fail_Destroy_Target_Message`, `data_Id`, `last_Modified`)
VALUES ('777701004','0','0','0','0','777710002','1','You use the Catalyst to deepen Asheron\'s blessing. Greater Asheron\'s Favor takes hold!','0','0','You fail to upgrade the charm.','1','1',NULL,'1','1',NULL,'0','0',NULL,'0','0',NULL,'0','2026-06-19 19:04:23')
     , ('777701005','0','0','0','0','777720002','1','You use the Catalyst to reach the pinnacle of Asheron\'s grace. Asheron\'s Blessing is yours!','0','0','You fail to upgrade the charm.','1','1',NULL,'1','1',NULL,'0','0',NULL,'0','0',NULL,'0','2026-06-19 19:04:23')
     , ('777701008','0','0','0','0','777710007','1','You use the Catalyst to strengthen the Fork Charm. Greater Fork Charm takes hold!','0','0','You fail to upgrade the charm.','1','1',NULL,'1','1',NULL,'0','0',NULL,'0','0',NULL,'0','2026-06-19 19:04:23')
     , ('777701009','0','0','0','0','777720007','1','You use the Catalyst to master the Fork Charm. Master Fork Charm is yours!','0','0','You fail to upgrade the charm.','1','1',NULL,'1','1',NULL,'0','0',NULL,'0','0',NULL,'0','2026-06-19 19:04:23')
     , ('777701010','0','0','0','0','777710005','1','You use the Catalyst to strengthen the Explosive Arrow Charm. Greater Explosive Arrow Charm takes hold!','0','0','You fail to upgrade the charm.','1','1',NULL,'1','1',NULL,'0','0',NULL,'0','0',NULL,'0','2026-06-19 19:04:23')
     , ('777701011','0','0','0','0','777720005','1','You use the Catalyst to master the Explosive Arrow Charm. Master Explosive Arrow Charm is yours!','0','0','You fail to upgrade the charm.','1','1',NULL,'1','1',NULL,'0','0',NULL,'0','0',NULL,'0','2026-06-19 19:04:23')
     , ('777701012','0','0','0','0','777710008','1','You use the Catalyst to strengthen the Far Shot Charm. Greater Far Shot Charm takes hold!','0','0','You fail to upgrade the charm.','1','1',NULL,'1','1',NULL,'0','0',NULL,'0','0',NULL,'0','2026-06-15 21:36:53')
     , ('777701013','0','0','0','0','777720008','1','You use the Catalyst to master the Far Shot Charm. Master Far Shot Charm is yours!','0','0','You fail to upgrade the charm.','1','1',NULL,'1','1',NULL,'0','0',NULL,'0','0',NULL,'0','2026-06-15 21:36:53');

DELETE FROM `cook_book`
WHERE `recipe_Id` IN (777701004, 777701005, 777701008, 777701009, 777701010, 777701011, 777701012, 777701013);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`, `last_Modified`)
VALUES ('777701004','777700010','777700020','2026-06-19 19:04:23')
     , ('777701005','777700010','777710002','2026-06-19 19:04:23')
     , ('777701008','777700010','777700027','2026-06-19 19:04:23')
     , ('777701009','777700010','777710007','2026-06-19 19:04:23')
     , ('777701010','777700010','777700025','2026-06-19 19:04:23')
     , ('777701011','777700010','777710005','2026-06-19 19:04:23')
     , ('777701012','777700010','777700028','2026-06-15 21:36:53')
     , ('777701013','777700010','777710008','2026-06-15 21:36:53');
