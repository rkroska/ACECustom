-- Explosive Arrow Charm Upgrade Recipes
-- Uses Charm Catalyst (777700010) to upgrade Explosive Arrow Charm between tiers.
-- Tier 1 (777700025) -> Tier 2 (777710005) -> Tier 3 (777720005)

START TRANSACTION;

DELETE FROM `cook_book` WHERE `recipe_Id` IN (777701010, 777701011);
DELETE FROM `recipe` WHERE `id` IN (777701010, 777701011);

-- T1 -> T2 (777701010)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`)
VALUES (777701010, 0, 0, 0, 0, 777710005, 1, 'You use the Catalyst to strengthen the Explosive Arrow Charm. Greater Explosive Arrow Charm takes hold!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701010, 777700010, 777700025);

-- T2 -> T3 (777701011)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`)
VALUES (777701011, 0, 0, 0, 0, 777720005, 1, 'You use the Catalyst to master the Explosive Arrow Charm. Master Explosive Arrow Charm is yours!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701011, 777700010, 777710005);

COMMIT;
