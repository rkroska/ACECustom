-- Asheron's Favor Charm Upgrade Recipes
-- Uses Charm Catalyst (777700010) to upgrade Asheron's Favor charms between tiers.
-- Tier 1 (777700020) -> Tier 2 (777710002) -> Tier 3 (777720002)

START TRANSACTION;

DELETE FROM `cook_book` WHERE `recipe_Id` IN (777701004, 777701005);
DELETE FROM `recipe` WHERE `id` IN (777701004, 777701005);

-- T1 -> T2 (777701004)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`)
VALUES (777701004, 0, 0, 0, 0, 777710002, 1, 'You use the Catalyst to deepen Asheron''s blessing. Greater Asheron''s Favor takes hold!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701004, 777700010, 777700020);

-- T2 -> T3 (777701005)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`)
VALUES (777701005, 0, 0, 0, 0, 777720002, 1, 'You use the Catalyst to reach the pinnacle of Asheron''s grace. Asheron''s Blessing is yours!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701005, 777700010, 777710002);

COMMIT;
