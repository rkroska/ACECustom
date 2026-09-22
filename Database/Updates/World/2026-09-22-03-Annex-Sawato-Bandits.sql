-- =====================================================================================
-- Ruggan's Annex - the Sawato Bandits: one for the Registry, one for the Ward
-- Target database: ace_world. Safe to re-run.
--
-- 78780215  Certified Sawato Bandit, Reformed  (Registry)  - the normal armoured look
-- 78780225  The Sawato Situation               (Ward)      - the same look baked in, then mutated
--                                                            (PART 2, built from @et export 78790009)
--
-- The mutant cannot simply be the same NPC with a mutation colour: equipped armour keeps its
-- own palettes, so the colour would only reach the skin. Its look is instead baked into anim
-- part and texture rows (what @et captures from a live NPC), and the mutation colour then
-- overlays the whole body, armour included - the same way a bred armoured pet looks.
--
-- Afterwards: @clearcache, then place with @createinst in the annex (0x0106, variation 2).
-- All text is 7-bit ASCII per CLAUDE.md.
-- =====================================================================================

-- =====================================================================================
-- PART 1  78780215  Certified Sawato Bandit, Reformed
--     template 33831 - Sawato Bandit (Sho male, level 160). Cloned without its quarrels,
--     random-weapon roll or loot: it wears its four armour pieces and carries its Tachi.
-- =====================================================================================
DELETE FROM `weenie` WHERE `class_Id` = 78780215;

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780215, 'annex-registry-sawato-bandit', 10, NOW());

INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`)
  SELECT 78780215,`type`,`value` FROM `weenie_properties_int` WHERE `object_Id` = 33831;
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`)
  SELECT 78780215,`type`,`value` FROM `weenie_properties_bool` WHERE `object_Id` = 33831;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`)
  SELECT 78780215,`type`,`value` FROM `weenie_properties_float` WHERE `object_Id` = 33831;
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`)
  SELECT 78780215,`type`,`value` FROM `weenie_properties_string` WHERE `object_Id` = 33831;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`)
  SELECT 78780215,`type`,`value` FROM `weenie_properties_d_i_d` WHERE `object_Id` = 33831;
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`)
  SELECT 78780215,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM `weenie_properties_attribute` WHERE `object_Id` = 33831;
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`)
  SELECT 78780215,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 33831;
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`)
  SELECT 78780215,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM `weenie_properties_skill` WHERE `object_Id` = 33831;
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`)
  SELECT 78780215,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM `weenie_properties_body_part` WHERE `object_Id` = 33831;
INSERT INTO `weenie_properties_palette` (`object_Id`,`sub_Palette_Id`,`offset`,`length`)
  SELECT 78780215,`sub_Palette_Id`,`offset`,`length` FROM `weenie_properties_palette` WHERE `object_Id` = 33831;
INSERT INTO `weenie_properties_texture_map` (`object_Id`,`index`,`old_Id`,`new_Id`)
  SELECT 78780215,`index`,`old_Id`,`new_Id` FROM `weenie_properties_texture_map` WHERE `object_Id` = 33831;
INSERT INTO `weenie_properties_anim_part` (`object_Id`,`index`,`animation_Id`)
  SELECT 78780215,`index`,`animation_Id` FROM `weenie_properties_anim_part` WHERE `object_Id` = 33831;

UPDATE `weenie_properties_string` SET `value` = 'Certified Sawato Bandit, Reformed' WHERE `object_Id` = 78780215 AND `type` = 1;
-- Creature.cs:153  IsNPC => !Attackable && TargetingTactic == None
DELETE FROM `weenie_properties_int`  WHERE `object_Id` = 78780215 AND `type` IN (67, 68, 16, 95, 133, 134, 290, 291);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780215 AND `type` IN (1, 19, 98);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780215, 16, 32), -- ItemUseable = Remote (clickable)
  (78780215, 95, 8), -- RadarBlipColor = NPC
  (78780215, 133, 4), -- ShowableOnRadar
  (78780215, 134, 16); -- PlayerKillerStatus = RubberGlue
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780215,  1, True),   -- Stuck: stays where it is placed
  (78780215, 19, False),  -- Attackable: no
  (78780215, 98, True);   -- Invincible: belt and braces
-- Worn gear only (destination_Type 2 = wield), with the template's own colours and shades.
INSERT INTO `weenie_properties_create_list` (`object_Id`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`) VALUES
  (78780215, 2, 6046,  1, 2,  0.4789, False),  -- Amuli Coat
  (78780215, 2, 6047,  1, 2,  0.4789, False),  -- Amuli Leggings
  (78780215, 2, 27226, 1, 39, 0,      False),  -- Nariyid Boots
  (78780215, 2, 9392,  1, 2,  0.2,    False),  -- Helm of the Crag
  (78780215, 2, 31704, 1, 0,  0.5,    False);  -- Tachi

-- =====================================================================================
-- PART 2  78780225  The Sawato Situation  (Ward)
--     Built from @et export 78790009 of 78780215 (the armoured look baked into anim part
--     and texture rows), written out as literal rows so it does not need the staging export.
--     Changed from the export: renamed; kill-task tag dropped; ALL palette rows dropped, so the
--     mutation template overlays skin and armour alike (what ApplyMutationPalette does for a bred
--     pet); PaletteTemplate set to a mutation palette; joins the Ward's 60 s colour cycle.
-- =====================================================================================
DELETE FROM `weenie` WHERE `class_Id` = 78780225;

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780225, 'annex-ward-sawato-mutant', 10, NOW());

-- PaletteTemplate (3) = 0x04001F52, a full mutation palette; the colour cycle replaces it.
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780225, 1, 16),
  (78780225, 2, 31),
  (78780225, 3, 67116882),
  (78780225, 6, -1),
  (78780225, 7, -1),
  (78780225, 16, 32),
  (78780225, 25, 160),
  (78780225, 65, 101),
  (78780225, 93, 1032),
  (78780225, 95, 8),
  (78780225, 113, 1),
  (78780225, 133, 4),
  (78780225, 134, 16),
  (78780225, 146, 500000),
  (78780225, 188, 3),
  (78780225, 307, 5);

INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780225, 1, True),
  (78780225, 11, False),
  (78780225, 12, True),
  (78780225, 13, True),
  (78780225, 14, True),
  (78780225, 19, False),
  (78780225, 98, True);

-- 9060 ShowcaseColourCycleSeconds = 60: joins the Ward pets' colour cycle.
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780225, 1, 5),
  (78780225, 3, 2),
  (78780225, 4, 5),
  (78780225, 5, 1),
  (78780225, 13, 0.9),
  (78780225, 14, 0.9),
  (78780225, 15, 1),
  (78780225, 16, 1),
  (78780225, 17, 0.8),
  (78780225, 18, 0.9),
  (78780225, 19, 0.8),
  (78780225, 31, 18),
  (78780225, 54, 0.5),
  (78780225, 55, 80),
  (78780225, 64, 0.6),
  (78780225, 65, 0.6),
  (78780225, 66, 0.6),
  (78780225, 67, 0.7),
  (78780225, 68, 0.6),
  (78780225, 69, 0.6),
  (78780225, 70, 0.7),
  (78780225, 80, 2),
  (78780225, 104, 10),
  (78780225, 122, 2),
  (78780225, 125, 1),
  (78780225, 9060, 60);

INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780225, 1, 'The Sawato Situation');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780225, 1, 33554433),
  (78780225, 2, 150994945),
  (78780225, 3, 536870913),
  (78780225, 4, 805306368),
  (78780225, 6, 67108990),
  (78780225, 7, 268437191),
  (78780225, 8, 100667446),
  (78780225, 9, 83890463),
  (78780225, 10, 83890561),
  (78780225, 11, 83890577),
  (78780225, 12, 83886668),
  (78780225, 13, 83886837),
  (78780225, 14, 83886684),
  (78780225, 15, 67117072),
  (78780225, 16, 67109565),
  (78780225, 17, 67110054),
  (78780225, 18, 16795638),
  (78780225, 22, 872415236),
  (78780225, 35, 455);

INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) VALUES
  (78780225, 1, 315, 0, 0),
  (78780225, 2, 245, 0, 0),
  (78780225, 3, 255, 0, 0),
  (78780225, 4, 295, 0, 0),
  (78780225, 5, 140, 0, 0),
  (78780225, 6, 150, 0, 0);

INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) VALUES
  (78780225, 1, 477, 0, 0, 0),
  (78780225, 3, 855, 0, 0, 0),
  (78780225, 5, 120, 0, 0, 0);

INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`) VALUES
  (78780225, 6, 0, 2, 0, 350, 0, 0),
  (78780225, 7, 0, 2, 0, 380, 0, 0),
  (78780225, 15, 0, 2, 0, 360, 0, 0),
  (78780225, 20, 0, 1, 0, 0, 0, 0),
  (78780225, 24, 0, 1, 0, 0, 0, 0),
  (78780225, 44, 0, 2, 0, 400, 0, 0),
  (78780225, 45, 0, 2, 0, 400, 0, 0),
  (78780225, 46, 0, 2, 0, 400, 0, 0),
  (78780225, 47, 0, 2, 0, 350, 0, 0);

INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`) VALUES
  (78780225, 0, 4, 0, 0, 540, 486, 486, 540, 540, 432, 486, 432, 0, 1, 0.33, 0, 0, 0.33, 0, 0, 0.33, 0, 0, 0.33, 0, 0),
  (78780225, 1, 4, 0, 0, 390, 351, 351, 390, 390, 312, 351, 312, 0, 2, 0.44, 0.17, 0, 0.44, 0.17, 0, 0.44, 0.17, 0, 0.44, 0.17, 0),
  (78780225, 2, 4, 0, 0, 390, 351, 351, 390, 390, 312, 351, 312, 0, 3, 0, 0.17, 0, 0, 0.17, 0, 0, 0.17, 0, 0, 0.17, 0),
  (78780225, 3, 4, 0, 0, 390, 351, 351, 390, 390, 312, 351, 312, 0, 1, 0.23, 0.03, 0, 0.23, 0.03, 0, 0.23, 0.03, 0, 0.23, 0.03, 0),
  (78780225, 4, 4, 0, 0, 390, 351, 351, 390, 390, 312, 351, 312, 0, 2, 0, 0.3, 0, 0, 0.3, 0, 0, 0.3, 0, 0, 0.3, 0),
  (78780225, 5, 4, 200, 0.75, 300, 270, 270, 300, 300, 240, 270, 240, 0, 2, 0, 0.2, 0, 0, 0.2, 0, 0, 0.2, 0, 0, 0.2, 0),
  (78780225, 6, 4, 0, 0, 390, 351, 351, 390, 390, 312, 351, 312, 0, 3, 0, 0.13, 0.18, 0, 0.13, 0.18, 0, 0.13, 0.18, 0, 0.13, 0.18),
  (78780225, 7, 4, 0, 0, 390, 351, 351, 390, 390, 312, 351, 312, 0, 3, 0, 0, 0.6, 0, 0, 0.6, 0, 0, 0.6, 0, 0, 0.6),
  (78780225, 8, 4, 200, 0.75, 320, 288, 288, 320, 320, 256, 288, 256, 0, 3, 0, 0, 0.22, 0, 0, 0.22, 0, 0, 0.22, 0, 0, 0.22);

-- The baked look: body and armour parts as rendered on 78780215.
INSERT INTO `weenie_properties_anim_part` (`object_Id`,`index`,`animation_Id`) VALUES
  (78780225, 0, 16783894),
  (78780225, 1, 16783912),
  (78780225, 2, 16783918),
  (78780225, 3, 16790020),
  (78780225, 4, 16790017),
  (78780225, 5, 16783916),
  (78780225, 6, 16783920),
  (78780225, 7, 16790018),
  (78780225, 8, 16790019),
  (78780225, 9, 16781837),
  (78780225, 10, 16783863),
  (78780225, 11, 16783853),
  (78780225, 12, 16777304),
  (78780225, 13, 16783871),
  (78780225, 14, 16783855),
  (78780225, 15, 16777307),
  (78780225, 16, 16785647),
  (78780225, 17, 16777708),
  (78780225, 18, 16777708),
  (78780225, 19, 16777708),
  (78780225, 20, 16777708),
  (78780225, 21, 16777708),
  (78780225, 22, 16777708),
  (78780225, 23, 16777708),
  (78780225, 24, 16777708),
  (78780225, 25, 16777708),
  (78780225, 26, 16777708),
  (78780225, 27, 16777708),
  (78780225, 28, 16777708),
  (78780225, 29, 16777708),
  (78780225, 30, 16777708),
  (78780225, 31, 16777708),
  (78780225, 32, 16777708),
  (78780225, 33, 16777708);

INSERT INTO `weenie_properties_texture_map` (`object_Id`,`index`,`old_Id`,`new_Id`) VALUES
  (78780225, 0, 83892345, 83892370),
  (78780225, 0, 83892344, 83892370),
  (78780225, 1, 83892352, 83892374),
  (78780225, 2, 83892351, 83892373),
  (78780225, 5, 83892352, 83892374),
  (78780225, 6, 83892351, 83892373),
  (78780225, 9, 83887061, 83892375),
  (78780225, 9, 83887060, 83892376),
  (78780225, 10, 83892347, 83892372),
  (78780225, 11, 83892346, 83892371),
  (78780225, 13, 83892347, 83892372),
  (78780225, 14, 83892346, 83892371),
  (78780225, 16, 83886668, 83890463),
  (78780225, 16, 83886837, 83890561),
  (78780225, 16, 83886684, 83890577);

-- Still carries its Tachi. A weapon is not armour or clothing, so it does not stop the baked look from rendering.
INSERT INTO `weenie_properties_create_list` (`object_Id`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`) VALUES
  (78780225, 2, 31704, 1, 20, 0.5, False);
