-- Artisan's Charm Upgrade Recipes
-- Uses Charm Catalyst (777700010) to upgrade Artisan's Charm between tiers.
-- Tier 1 (777700021) -> Tier 2 (777710003) -> Tier 3 (777720003)

START TRANSACTION;

DELETE FROM `cook_book` WHERE `recipe_Id` IN (777701006, 777701007);
DELETE FROM `recipe` WHERE `id` IN (777701006, 777701007);

-- T1 -> T2 (777701006)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`)
VALUES (777701006, 0, 0, 0, 0, 777710003, 1, 'You use the Catalyst to sharpen the artisan''s gift. Greater Artisan''s Charm takes hold!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701006, 777700010, 777700021);

-- T2 -> T3 (777701007)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`)
VALUES (777701007, 0, 0, 0, 0, 777720003, 1, 'You use the Catalyst to master the artisan''s craft. Master Artisan''s Charm is yours!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701007, 777700010, 777710003);

COMMIT;
