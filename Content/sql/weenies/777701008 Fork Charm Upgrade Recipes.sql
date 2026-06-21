-- Fork Charm Upgrade Recipes
-- Uses Charm Catalyst (777700010) to upgrade Fork Charm between tiers.
-- Tier 1 (777700027) -> Tier 2 (777710007) -> Tier 3 (777720007)

START TRANSACTION;

DELETE FROM `cook_book` WHERE `recipe_Id` IN (777701008, 777701009);
DELETE FROM `recipe` WHERE `id` IN (777701008, 777701009);

-- T1 -> T2 (777701008)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`)
VALUES (777701008, 0, 0, 0, 0, 777710007, 1, 'You use the Catalyst to strengthen the Fork Charm. Greater Fork Charm takes hold!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701008, 777700010, 777700027);

-- T2 -> T3 (777701009)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`)
VALUES (777701009, 0, 0, 0, 0, 777720007, 1, 'You use the Catalyst to master the Fork Charm. Master Fork Charm is yours!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701009, 777700010, 777710007);

COMMIT;
