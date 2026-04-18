-- ============================================================================
-- MASTER ABILITY CHARM UPGRADE RECIPES (ACE SCHEMA - FINAL)
-- ============================================================================
-- Re-importable SQL for the recipe/cook_book structure.
-- Handles T1->T2, Test->T2, and T2->T3 upgrades.
-- ============================================================================

-- Clear existing recipes and cookbook entries to avoid duplicates
DELETE FROM `cook_book` WHERE `recipe_Id` BETWEEN 777701000 AND 777701020;
DELETE FROM `recipe` WHERE `id` BETWEEN 777701000 AND 777701020;

-- ============================================================================
-- 1. MANA BARRIER (Registry ID 1)
-- ============================================================================

-- L1 -> L2 (777701001)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`) 
VALUES (777701001, 0, 0, 0, 0, 777710004, 1, 'You use the Catalyst to strengthen the Mana Barrier Charm!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701001, 777700010, 777700001);

-- Test -> L2 (777701002)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`) 
VALUES (777701002, 0, 0, 0, 0, 777710004, 1, 'You use the Catalyst to stabilize and strengthen the Mana Barrier Test Charm!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701002, 777700010, 777700054);

-- L2 -> L3 (777701003)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`) 
VALUES (777701003, 0, 0, 0, 0, 777720004, 1, 'You use the Catalyst to reach the Master tier of Mana Barrier!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701003, 777700010, 777710004);

-- ============================================================================
-- 2. HEAVY SWING (Registry ID 2)
-- ============================================================================

-- L1 -> L2 (777701004)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`) 
VALUES (777701004, 0, 0, 0, 0, 777710005, 1, 'You use the Catalyst to strengthen the Heavy Swing Charm!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701004, 777700010, 777700005);

-- Test -> L2 (777701005)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`) 
VALUES (777701005, 0, 0, 0, 0, 777710005, 1, 'You use the Catalyst to stabilize and strengthen the Heavy Swing Test Charm!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701005, 777700010, 777700055);

-- L2 -> L3 (777701006)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`) 
VALUES (777701006, 0, 0, 0, 0, 777720005, 1, 'You use the Catalyst to reach the Master tier of Heavy Swing!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701006, 777700010, 777710005);

-- ============================================================================
-- 3. HEAVY DRAW (Registry ID 3)
-- ============================================================================

-- L1 -> L2 (777701007)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`) 
VALUES (777701007, 0, 0, 0, 0, 777710006, 1, 'You use the Catalyst to strengthen the Heavy Draw Charm!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701007, 777700010, 777700006);

-- Test -> L2 (777701008)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`) 
VALUES (777701008, 0, 0, 0, 0, 777710006, 1, 'You use the Catalyst to stabilize and strengthen the Heavy Draw Test Charm!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701008, 777700010, 777700056);

-- L2 -> L3 (777701009)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`) 
VALUES (777701009, 0, 0, 0, 0, 777720006, 1, 'You use the Catalyst to reach the Master tier of Heavy Draw!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701009, 777700010, 777710006);

-- ============================================================================
-- 4. FOCUSED CASTING (Registry ID 4)
-- ============================================================================

-- L1 -> L2 (777701010)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`) 
VALUES (777701010, 0, 0, 0, 0, 777710007, 1, 'You use the Catalyst to strengthen the Focused Casting Charm!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701010, 777700010, 777700007);

-- Test -> L2 (777701011)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`) 
VALUES (777701011, 0, 0, 0, 0, 777710007, 1, 'You use the Catalyst to stabilize and strengthen the Focused Casting Test Charm!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701011, 777700010, 777700057);

-- L2 -> L3 (777701012)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`) 
VALUES (777701012, 0, 0, 0, 0, 777720007, 1, 'You use the Catalyst to reach the Master tier of Focused Casting!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701012, 777700010, 777710007);

-- ============================================================================
-- 5. CHAINING (Registry ID 5)
-- ============================================================================

-- L1 -> L2 (777701013)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`) 
VALUES (777701013, 0, 0, 0, 0, 777710002, 1, 'You use the Catalyst to strengthen the Chaining Charm!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701013, 777700010, 777700002);

-- L2 -> L3 (777701014)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`) 
VALUES (777701014, 0, 0, 0, 0, 777720002, 1, 'You use the Catalyst to reach the Master tier of Chaining!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701014, 777700010, 777710002);

-- ============================================================================
-- 6. REPEATER (Registry ID 6)
-- ============================================================================

-- L1 -> L2 (777701015)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`) 
VALUES (777701015, 0, 0, 0, 0, 777710003, 1, 'You use the Catalyst to strengthen the Repeater Charm!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701015, 777700010, 777700003);

-- L2 -> L3 (777701016)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`) 
VALUES (777701016, 0, 0, 0, 0, 777720003, 1, 'You use the Catalyst to reach the Master tier of Repeater!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701016, 777700010, 777710003);

-- ============================================================================
-- 7. BLOOD LETTING (Registry ID 7)
-- ============================================================================

-- L1 -> L2 (777701017)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`) 
VALUES (777701017, 0, 0, 0, 0, 777710008, 1, 'You use the Catalyst to strengthen the Blood Letting Charm!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701017, 777700010, 777700008);

-- Test -> L2 (777701018)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`) 
VALUES (777701018, 0, 0, 0, 0, 777710008, 1, 'You use the Catalyst to stabilize and strengthen the Blood Letting Test Charm!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701018, 777700010, 777700058);

-- L2 -> L3 (777701019)
INSERT INTO `recipe` (`id`, `unknown_1`, `skill`, `difficulty`, `salvage_Type`, `success_W_C_I_D`, `success_Amount`, `success_Message`, `fail_W_C_I_D`, `fail_Amount`, `fail_Message`, `success_Destroy_Source_Chance`, `success_Destroy_Source_Amount`, `success_Destroy_Target_Chance`, `success_Destroy_Target_Amount`, `fail_Destroy_Source_Chance`, `fail_Destroy_Source_Amount`, `fail_Destroy_Target_Chance`, `fail_Destroy_Target_Amount`, `data_Id`) 
VALUES (777701019, 0, 0, 0, 0, 777720008, 1, 'You use the Catalyst to reach the Master tier of Blood Letting!', 0, 0, 'You fail to upgrade the charm.', 1.0, 1, 1.0, 1, 0.0, 0, 0.0, 0, 0);
INSERT INTO `cook_book` (`recipe_Id`, `source_W_C_I_D`, `target_W_C_I_D`) VALUES (777701019, 777700010, 777710008);
