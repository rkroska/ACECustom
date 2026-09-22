-- =====================================================================================
-- Ruggan's Annex - the room four paternity storyline
-- Target database: ace_world. Safe to re-run.
-- Generated from a scene table; see the comic-strip design agreed on 2026-09-21.
--
-- A drudge couple (Mubb and Gorta) argue about who fathered their small neon baby. It is a
-- mutation. Seven short scenes pull in Splotch, DJ Skulk, Gary, Denton and Bexley.
--
-- How it runs (all verified in EmoteManager.cs / Landblock.cs):
--   * ONE hidden director (78780240) starts every scene from its HeartBeat, then stays busy
--     for 120 s. EmoteManager.ExecuteEmoteSet ignores new emotes while IsBusy, so only one
--     scene can run at a time and nobody talks over anybody.
--   * Each spoken line is a ReceiveLocalSignal (37) set on its speaker. It waits, says the
--     line, then sends the next cue. The last line sends nothing, so every chain ends.
--   * An NPC never hears its own cue (Landblock.EmitSignal skips the emitter).
--   * Clicks (Use, 7) answer with private Tells.
--   * The Scene Tester (78780241) is a visible admin NPC: click it for a random scene, or
--     say "scene dj" (etc.) next to it for a specific one. Remove it after testing.
--
-- Placement rule: every speaker must be within 60 m of whoever sends it a cue, and the
-- director within 60 m of Mubb, Denton and Bexley (straight-line, walls do not matter).
-- Place them in the annex - landblock 0x0106, variation 2 - with @createinst.
--
-- Also in this file:
--   * DJ Skulk gains HearLocalSignals. The annex file strips it from him and never adds it
--     back, so until now he could not hear any cue.
--   * Bexley's and Splotch's old feud OPENERS (HeartBeat sets that send annex_feud_*) are
--     removed so they cannot start an argument over a director scene. Their replies stay
--     in place, unused, until the feud moves onto the director.
--
-- All text is 7-bit ASCII per CLAUDE.md.
-- Afterwards: @clearcache, then place or respawn the NPCs.
-- =====================================================================================

SET @__old_safe_updates = @@SQL_SAFE_UPDATES;
SET SQL_SAFE_UPDATES = 0;

-- =====================================================================================
-- PART 1  Clean slate for everything this file owns
-- =====================================================================================
DELETE FROM `weenie` WHERE `class_Id` IN (78780230, 78780231, 78780232, 78780233, 78780240, 78780241);
-- Retired: the blue baby colour candidates, no longer built.
DELETE FROM `weenie` WHERE `class_Id` IN (78780234, 78780235);

DELETE FROM `weenie_properties_emote`
WHERE `object_Id` IN (78780202, 78780203, 78780210, 78780220) AND `category` = 37 AND `quest` LIKE 'pat\_%';

-- =====================================================================================
-- PART 2  New NPCs, cloned from templates that already render correctly (same pattern as
--         the annex file). Monster templates are cloned without their create lists, so
--         they carry no weapons or loot.
-- =====================================================================================

-- ------------------------------------------------------------------------------------
-- 78780230  Mubb
--     template 1608 - Drudge Lurker (MidGrey)
-- ------------------------------------------------------------------------------------
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780230, 'annex-mubb', 10, NOW());

INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`)
  SELECT 78780230,`type`,`value` FROM `weenie_properties_int` WHERE `object_Id` = 1608;
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`)
  SELECT 78780230,`type`,`value` FROM `weenie_properties_bool` WHERE `object_Id` = 1608;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`)
  SELECT 78780230,`type`,`value` FROM `weenie_properties_float` WHERE `object_Id` = 1608;
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`)
  SELECT 78780230,`type`,`value` FROM `weenie_properties_string` WHERE `object_Id` = 1608;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`)
  SELECT 78780230,`type`,`value` FROM `weenie_properties_d_i_d` WHERE `object_Id` = 1608;
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`)
  SELECT 78780230,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM `weenie_properties_attribute` WHERE `object_Id` = 1608;
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`)
  SELECT 78780230,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 1608;
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`)
  SELECT 78780230,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM `weenie_properties_skill` WHERE `object_Id` = 1608;
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`)
  SELECT 78780230,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM `weenie_properties_body_part` WHERE `object_Id` = 1608;
INSERT INTO `weenie_properties_palette` (`object_Id`,`sub_Palette_Id`,`offset`,`length`)
  SELECT 78780230,`sub_Palette_Id`,`offset`,`length` FROM `weenie_properties_palette` WHERE `object_Id` = 1608;
INSERT INTO `weenie_properties_texture_map` (`object_Id`,`index`,`old_Id`,`new_Id`)
  SELECT 78780230,`index`,`old_Id`,`new_Id` FROM `weenie_properties_texture_map` WHERE `object_Id` = 1608;
INSERT INTO `weenie_properties_anim_part` (`object_Id`,`index`,`animation_Id`)
  SELECT 78780230,`index`,`animation_Id` FROM `weenie_properties_anim_part` WHERE `object_Id` = 1608;

UPDATE `weenie_properties_string` SET `value` = 'Mubb' WHERE `object_Id` = 78780230 AND `type` = 1;
-- Creature.cs:153  IsNPC => !Attackable && TargetingTactic == None
DELETE FROM `weenie_properties_int`  WHERE `object_Id` = 78780230 AND `type` IN (67, 68, 16, 95, 133, 134, 290, 291);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780230 AND `type` IN (1, 19, 98);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780230, 16, 32), -- ItemUseable = Remote (clickable)
  (78780230, 95, 8), -- RadarBlipColor = NPC
  (78780230, 133, 4), -- ShowableOnRadar
  (78780230, 134, 16), -- PlayerKillerStatus = RubberGlue
  (78780230, 290, 1), -- HearLocalSignals
  (78780230, 291, 60); -- HearLocalSignalsRadius, metres
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780230,  1, True),   -- Stuck: stays where it is placed
  (78780230, 19, False),  -- Attackable: no
  (78780230, 98, True);   -- Invincible: belt and braces

-- ------------------------------------------------------------------------------------
-- 78780231  Gorta
--     template 193 - Drudge Slinker (PastyYellow)
-- ------------------------------------------------------------------------------------
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780231, 'annex-gorta', 10, NOW());

INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`)
  SELECT 78780231,`type`,`value` FROM `weenie_properties_int` WHERE `object_Id` = 193;
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`)
  SELECT 78780231,`type`,`value` FROM `weenie_properties_bool` WHERE `object_Id` = 193;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`)
  SELECT 78780231,`type`,`value` FROM `weenie_properties_float` WHERE `object_Id` = 193;
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`)
  SELECT 78780231,`type`,`value` FROM `weenie_properties_string` WHERE `object_Id` = 193;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`)
  SELECT 78780231,`type`,`value` FROM `weenie_properties_d_i_d` WHERE `object_Id` = 193;
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`)
  SELECT 78780231,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM `weenie_properties_attribute` WHERE `object_Id` = 193;
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`)
  SELECT 78780231,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 193;
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`)
  SELECT 78780231,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM `weenie_properties_skill` WHERE `object_Id` = 193;
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`)
  SELECT 78780231,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM `weenie_properties_body_part` WHERE `object_Id` = 193;
INSERT INTO `weenie_properties_palette` (`object_Id`,`sub_Palette_Id`,`offset`,`length`)
  SELECT 78780231,`sub_Palette_Id`,`offset`,`length` FROM `weenie_properties_palette` WHERE `object_Id` = 193;
INSERT INTO `weenie_properties_texture_map` (`object_Id`,`index`,`old_Id`,`new_Id`)
  SELECT 78780231,`index`,`old_Id`,`new_Id` FROM `weenie_properties_texture_map` WHERE `object_Id` = 193;
INSERT INTO `weenie_properties_anim_part` (`object_Id`,`index`,`animation_Id`)
  SELECT 78780231,`index`,`animation_Id` FROM `weenie_properties_anim_part` WHERE `object_Id` = 193;

UPDATE `weenie_properties_string` SET `value` = 'Gorta' WHERE `object_Id` = 78780231 AND `type` = 1;
-- Creature.cs:153  IsNPC => !Attackable && TargetingTactic == None
DELETE FROM `weenie_properties_int`  WHERE `object_Id` = 78780231 AND `type` IN (67, 68, 16, 95, 133, 134, 290, 291);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780231 AND `type` IN (1, 19, 98);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780231, 16, 32), -- ItemUseable = Remote (clickable)
  (78780231, 95, 8), -- RadarBlipColor = NPC
  (78780231, 133, 4), -- ShowableOnRadar
  (78780231, 134, 16), -- PlayerKillerStatus = RubberGlue
  (78780231, 290, 1), -- HearLocalSignals
  (78780231, 291, 60); -- HearLocalSignalsRadius, metres
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780231,  1, True),   -- Stuck: stays where it is placed
  (78780231, 19, False),  -- Attackable: no
  (78780231, 98, True);   -- Invincible: belt and braces

-- ------------------------------------------------------------------------------------
-- 78780232  Mubb Junior
--     template 7 - Drudge Skulker, shrunk, neon mutation colour
-- ------------------------------------------------------------------------------------
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780232, 'annex-baby', 10, NOW());

INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`)
  SELECT 78780232,`type`,`value` FROM `weenie_properties_int` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`)
  SELECT 78780232,`type`,`value` FROM `weenie_properties_bool` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`)
  SELECT 78780232,`type`,`value` FROM `weenie_properties_float` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`)
  SELECT 78780232,`type`,`value` FROM `weenie_properties_string` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`)
  SELECT 78780232,`type`,`value` FROM `weenie_properties_d_i_d` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`)
  SELECT 78780232,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM `weenie_properties_attribute` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`)
  SELECT 78780232,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`)
  SELECT 78780232,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM `weenie_properties_skill` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`)
  SELECT 78780232,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM `weenie_properties_body_part` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_palette` (`object_Id`,`sub_Palette_Id`,`offset`,`length`)
  SELECT 78780232,`sub_Palette_Id`,`offset`,`length` FROM `weenie_properties_palette` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_texture_map` (`object_Id`,`index`,`old_Id`,`new_Id`)
  SELECT 78780232,`index`,`old_Id`,`new_Id` FROM `weenie_properties_texture_map` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_anim_part` (`object_Id`,`index`,`animation_Id`)
  SELECT 78780232,`index`,`animation_Id` FROM `weenie_properties_anim_part` WHERE `object_Id` = 7;

UPDATE `weenie_properties_string` SET `value` = 'Mubb Junior' WHERE `object_Id` = 78780232 AND `type` = 1;
-- Creature.cs:153  IsNPC => !Attackable && TargetingTactic == None
DELETE FROM `weenie_properties_int`  WHERE `object_Id` = 78780232 AND `type` IN (67, 68, 16, 95, 133, 134, 290, 291, 3);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780232 AND `type` IN (1, 19, 98);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780232, 16, 32), -- ItemUseable = Remote (clickable)
  (78780232, 95, 8), -- RadarBlipColor = NPC
  (78780232, 133, 4), -- ShowableOnRadar
  (78780232, 134, 16), -- PlayerKillerStatus = RubberGlue
  (78780232, 290, 1), -- HearLocalSignals
  (78780232, 291, 60), -- HearLocalSignalsRadius, metres
  (78780232, 3, 67113089); -- PaletteTemplate = 0x04001081, the neon red-orange mutation rolled on a real bred Drudge Skulker pet
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780232,  1, True),   -- Stuck: stays where it is placed
  (78780232, 19, False),  -- Attackable: no
  (78780232, 98, True);   -- Invincible: belt and braces
DELETE FROM `weenie_properties_float` WHERE `object_Id` = 78780232 AND `type` IN (39);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780232, 39, 0.55); -- DefaultScale: about 58% of an adult drudge

-- ------------------------------------------------------------------------------------
-- 78780233  Denton
--     template 35462 - Jarvis Hammerstone (Aluvian male NPC)
-- ------------------------------------------------------------------------------------
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780233, 'annex-denton', 10, NOW());

INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`)
  SELECT 78780233,`type`,`value` FROM `weenie_properties_int` WHERE `object_Id` = 35462;
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`)
  SELECT 78780233,`type`,`value` FROM `weenie_properties_bool` WHERE `object_Id` = 35462;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`)
  SELECT 78780233,`type`,`value` FROM `weenie_properties_float` WHERE `object_Id` = 35462;
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`)
  SELECT 78780233,`type`,`value` FROM `weenie_properties_string` WHERE `object_Id` = 35462;
INSERT INTO `weenie_properties_create_list` (`object_Id`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`)
  SELECT 78780233,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond` FROM `weenie_properties_create_list` WHERE `object_Id` = 35462;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`)
  SELECT 78780233,`type`,`value` FROM `weenie_properties_d_i_d` WHERE `object_Id` = 35462;
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`)
  SELECT 78780233,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM `weenie_properties_attribute` WHERE `object_Id` = 35462;
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`)
  SELECT 78780233,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 35462;
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`)
  SELECT 78780233,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM `weenie_properties_skill` WHERE `object_Id` = 35462;
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`)
  SELECT 78780233,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM `weenie_properties_body_part` WHERE `object_Id` = 35462;
INSERT INTO `weenie_properties_palette` (`object_Id`,`sub_Palette_Id`,`offset`,`length`)
  SELECT 78780233,`sub_Palette_Id`,`offset`,`length` FROM `weenie_properties_palette` WHERE `object_Id` = 35462;
INSERT INTO `weenie_properties_texture_map` (`object_Id`,`index`,`old_Id`,`new_Id`)
  SELECT 78780233,`index`,`old_Id`,`new_Id` FROM `weenie_properties_texture_map` WHERE `object_Id` = 35462;
INSERT INTO `weenie_properties_anim_part` (`object_Id`,`index`,`animation_Id`)
  SELECT 78780233,`index`,`animation_Id` FROM `weenie_properties_anim_part` WHERE `object_Id` = 35462;

UPDATE `weenie_properties_string` SET `value` = 'Denton' WHERE `object_Id` = 78780233 AND `type` = 1;
-- Creature.cs:153  IsNPC => !Attackable && TargetingTactic == None
DELETE FROM `weenie_properties_int`  WHERE `object_Id` = 78780233 AND `type` IN (67, 68, 16, 95, 133, 134, 290, 291);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780233 AND `type` IN (1, 19, 98);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780233, 16, 32), -- ItemUseable = Remote (clickable)
  (78780233, 95, 8), -- RadarBlipColor = NPC
  (78780233, 133, 4), -- ShowableOnRadar
  (78780233, 134, 16), -- PlayerKillerStatus = RubberGlue
  (78780233, 290, 1), -- HearLocalSignals
  (78780233, 291, 60); -- HearLocalSignalsRadius, metres
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780233,  1, True),   -- Stuck: stays where it is placed
  (78780233, 19, False),  -- Attackable: no
  (78780233, 98, True);   -- Invincible: belt and braces

-- ------------------------------------------------------------------------------------
-- 78780240  Annex Scene Director
--     template 7 - Drudge Skulker, shrunk to a speck and hidden
-- ------------------------------------------------------------------------------------
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780240, 'annex-scene-director', 10, NOW());

INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`)
  SELECT 78780240,`type`,`value` FROM `weenie_properties_int` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`)
  SELECT 78780240,`type`,`value` FROM `weenie_properties_bool` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`)
  SELECT 78780240,`type`,`value` FROM `weenie_properties_float` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`)
  SELECT 78780240,`type`,`value` FROM `weenie_properties_string` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`)
  SELECT 78780240,`type`,`value` FROM `weenie_properties_d_i_d` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`)
  SELECT 78780240,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM `weenie_properties_attribute` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`)
  SELECT 78780240,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`)
  SELECT 78780240,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM `weenie_properties_skill` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`)
  SELECT 78780240,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM `weenie_properties_body_part` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_palette` (`object_Id`,`sub_Palette_Id`,`offset`,`length`)
  SELECT 78780240,`sub_Palette_Id`,`offset`,`length` FROM `weenie_properties_palette` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_texture_map` (`object_Id`,`index`,`old_Id`,`new_Id`)
  SELECT 78780240,`index`,`old_Id`,`new_Id` FROM `weenie_properties_texture_map` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_anim_part` (`object_Id`,`index`,`animation_Id`)
  SELECT 78780240,`index`,`animation_Id` FROM `weenie_properties_anim_part` WHERE `object_Id` = 7;

UPDATE `weenie_properties_string` SET `value` = 'Annex Scene Director' WHERE `object_Id` = 78780240 AND `type` = 1;
-- Creature.cs:153  IsNPC => !Attackable && TargetingTactic == None
DELETE FROM `weenie_properties_int`  WHERE `object_Id` = 78780240 AND `type` IN (67, 68, 16, 95, 133, 134, 290, 291);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780240 AND `type` IN (1, 19, 98);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780240, 16, 1), -- ItemUseable = No
  (78780240, 133, 1), -- RadarBehavior = ShowNever
  (78780240, 134, 16); -- PlayerKillerStatus = RubberGlue
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780240,  1, True),   -- Stuck: stays where it is placed
  (78780240, 19, False),  -- Attackable: no
  (78780240, 98, True);   -- Invincible: belt and braces
DELETE FROM `weenie_properties_float` WHERE `object_Id` = 78780240 AND `type` IN (39);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780240, 39, 0.05); -- DefaultScale: a speck; also place it out of sight

-- ------------------------------------------------------------------------------------
-- 78780241  Scene Tester
--     template 7 - Drudge Skulker, admin testing only
-- ------------------------------------------------------------------------------------
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780241, 'annex-scene-tester', 10, NOW());

INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`)
  SELECT 78780241,`type`,`value` FROM `weenie_properties_int` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`)
  SELECT 78780241,`type`,`value` FROM `weenie_properties_bool` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`)
  SELECT 78780241,`type`,`value` FROM `weenie_properties_float` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`)
  SELECT 78780241,`type`,`value` FROM `weenie_properties_string` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`)
  SELECT 78780241,`type`,`value` FROM `weenie_properties_d_i_d` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`)
  SELECT 78780241,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM `weenie_properties_attribute` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`)
  SELECT 78780241,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`)
  SELECT 78780241,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM `weenie_properties_skill` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`)
  SELECT 78780241,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM `weenie_properties_body_part` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_palette` (`object_Id`,`sub_Palette_Id`,`offset`,`length`)
  SELECT 78780241,`sub_Palette_Id`,`offset`,`length` FROM `weenie_properties_palette` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_texture_map` (`object_Id`,`index`,`old_Id`,`new_Id`)
  SELECT 78780241,`index`,`old_Id`,`new_Id` FROM `weenie_properties_texture_map` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_anim_part` (`object_Id`,`index`,`animation_Id`)
  SELECT 78780241,`index`,`animation_Id` FROM `weenie_properties_anim_part` WHERE `object_Id` = 7;

UPDATE `weenie_properties_string` SET `value` = 'Scene Tester' WHERE `object_Id` = 78780241 AND `type` = 1;
-- Creature.cs:153  IsNPC => !Attackable && TargetingTactic == None
DELETE FROM `weenie_properties_int`  WHERE `object_Id` = 78780241 AND `type` IN (67, 68, 16, 95, 133, 134, 290, 291);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780241 AND `type` IN (1, 19, 98);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780241, 16, 32), -- ItemUseable = Remote (clickable)
  (78780241, 95, 8), -- RadarBlipColor = NPC
  (78780241, 133, 4), -- ShowableOnRadar
  (78780241, 134, 16); -- PlayerKillerStatus = RubberGlue
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780241,  1, True),   -- Stuck: stays where it is placed
  (78780241, 19, False),  -- Attackable: no
  (78780241, 98, True);   -- Invincible: belt and braces

-- =====================================================================================
-- PART 3  DJ Skulk can hear cues. His dance loop is untouched.
-- =====================================================================================
DELETE FROM `weenie_properties_int` WHERE `object_Id` = 78780202 AND `type` IN (290, 291);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780202, 290, 1),  -- HearLocalSignals
  (78780202, 291, 60); -- HearLocalSignalsRadius, metres

-- Bexley's and Splotch's old feud openers. Only HeartBeat sets that send an annex_feud_ cue.
DELETE e FROM `weenie_properties_emote` e
WHERE e.`object_Id` IN (78780210, 78780220) AND e.`category` = 5
  AND EXISTS (SELECT 1 FROM `weenie_properties_emote_action` a
              WHERE a.`emote_Id` = e.`id` AND a.`type` = 88 AND a.`message` LIKE 'annex\_feud\_%');

-- =====================================================================================
-- PART 4  The scenes. One ReceiveLocalSignal (37) set per spoken line, on its speaker.
--         Delay = seconds of pause before the line is said.
-- =====================================================================================

-- ---- He's NEON, Gorta (pat_neon) ----
-- Mubb, line 1
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780230, 37, 1, 'pat_neon_1');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 0, 0, 'He''s NEON, Gorta.'),
  (@e, 1, 88, 0, 0, 'pat_neon_2');

-- Gorta, line 2
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780231, 37, 1, 'pat_neon_2');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3.5, 0, 'Babies change colour.'),
  (@e, 1, 88, 0, 0, 'pat_neon_3');

-- the baby, line 3
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780232, 37, 1, 'pat_neon_3');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3, 0, 'Hi.');

-- ---- He's getting BRIGHTER (pat_brighter) ----
-- Mubb, line 1
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780230, 37, 1, 'pat_brighter_1');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 0, 0, 'Babies change colour, she says. Into NEON?'),
  (@e, 1, 88, 0, 0, 'pat_brighter_2');

-- Gorta, line 2
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780231, 37, 1, 'pat_brighter_2');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3.5, 0, 'He''ll grow out of it.'),
  (@e, 1, 88, 0, 0, 'pat_brighter_3');

-- Mubb, line 3
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780230, 37, 1, 'pat_brighter_3');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3.5, 0, 'He''s getting BRIGHTER.');

-- ---- Suspect one: Splotch (pat_splotch) ----
-- Mubb, line 1
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780230, 37, 1, 'pat_splotch_1');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 0, 0, 'Bright. Smug. Loud. Who does THAT remind you of?'),
  (@e, 1, 88, 0, 0, 'pat_splotch_2');

-- Splotch, line 2
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780220, 37, 1, 'pat_splotch_2');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3.5, 0, 'Not me. I''d remember.'),
  (@e, 1, 88, 0, 0, 'pat_splotch_3');

-- Gorta, line 3
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780231, 37, 1, 'pat_splotch_3');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3, 0, 'He WISHES.'),
  (@e, 1, 88, 0, 0, 'pat_splotch_4');

-- Splotch, line 4
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780220, 37, 1, 'pat_splotch_4');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3, 0, 'I really don''t.');

-- ---- Suspect two: the DJ (pat_dj) ----
-- Mubb, line 1
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780230, 37, 1, 'pat_dj_1');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 0, 0, 'You were at HIS set every night that week!'),
  (@e, 1, 88, 0, 0, 'pat_dj_2');

-- Gorta, line 2
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780231, 37, 1, 'pat_dj_2');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3.5, 0, 'I like the music!'),
  (@e, 1, 88, 0, 0, 'pat_dj_3');

-- Mubb, line 3
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780230, 37, 1, 'pat_dj_3');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3, 0, 'NOBODY likes the music.'),
  (@e, 1, 88, 0, 0, 'pat_dj_4'),
  (@e, 2, 88, 2.5, 0, 'pat_dj_4');

-- DJ Skulk, line 4
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780202, 37, 1, 'pat_dj_4');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 4.5, 0, '...Wow.');

-- ---- Suspect three: Gary (pat_gary) ----
-- Mubb, line 1
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780230, 37, 1, 'pat_gary_1');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 0, 0, 'Was it Gary?'),
  (@e, 1, 88, 0, 0, 'pat_gary_2');

-- Gorta, line 2
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780231, 37, 1, 'pat_gary_2');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3, 0, 'GARY??'),
  (@e, 1, 88, 0, 0, 'pat_gary_3');

-- Gary, line 3
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780203, 37, 1, 'pat_gary_3');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 4, 0, 'I''m just here for the music.'),
  (@e, 1, 88, 0, 0, 'pat_gary_4');

-- Mubb, line 4
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780230, 37, 1, 'pat_gary_4');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3, 0, 'THAT''S WHAT SHE SAID ABOUT THE DJ.');

-- ---- Denton, from a safe distance (pat_denton) ----
-- Denton, line 1
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780233, 37, 1, 'pat_denton_1');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 0, 0, 'Nobody touch the baby. That''s how it SPREADS.'),
  (@e, 1, 88, 0, 0, 'pat_denton_2');

-- Mubb, line 2
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780230, 37, 1, 'pat_denton_2');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3.5, 0, 'SEE? Even Denton thinks something happened!'),
  (@e, 1, 88, 0, 0, 'pat_denton_3');

-- Gorta, line 3
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780231, 37, 1, 'pat_denton_3');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3, 0, 'Denton thinks his sleeve is purple.'),
  (@e, 1, 88, 0, 0, 'pat_denton_4');

-- Denton, line 4
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780233, 37, 1, 'pat_denton_4');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3.5, 0, '...Is it?');

-- ---- The Registry results (rare) (pat_results) ----
-- Bexley, line 1
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780210, 37, 1, 'pat_results_1');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 0, 0, 'Paternity results for room four. Mubb. You ARE the father.'),
  (@e, 1, 88, 0, 0, 'pat_results_2');

-- Gorta, line 2
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780231, 37, 1, 'pat_results_2');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3, 0, 'I TOLD YOU!'),
  (@e, 1, 88, 0, 0, 'pat_results_3');

-- Bexley, line 3
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780210, 37, 1, 'pat_results_3');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3.5, 0, 'The colour is a mutation. It happens in the best families. Not mine. But families.'),
  (@e, 1, 88, 0, 0, 'pat_results_4');

-- Mubb, line 4
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780230, 37, 1, 'pat_results_4');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 5, 0, '...So who''s the mutation FROM?'),
  (@e, 1, 88, 0, 0, 'pat_results_5');

-- Gorta, line 5
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780231, 37, 1, 'pat_results_5');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 2.5, 0, 'Don''t.'),
  (@e, 1, 88, 0, 0, 'pat_results_6');

-- Mubb, line 6
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780230, 37, 1, 'pat_results_6');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 2.5, 0, 'WHO''S THE MUTATION FROM, GORTA.'),
  (@e, 1, 88, 0, 0, 'pat_results_7');

-- the baby, line 7
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780232, 37, 1, 'pat_results_7');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3.5, 0, 'I like my colour.');

-- =====================================================================================
-- PART 5  The director. HeartBeat (5) every ~5 s; probabilities are STACKED thresholds
--         (GetEmoteSet keeps sets above a random roll and takes the lowest), so each scene's
--         chance is the gap below its threshold. Total 0.050 per heartbeat = one start per
--         ~100 s on average, after a 120 s rest. The last step sends a cue nobody hears; its
--         delay is what keeps the director busy.
-- =====================================================================================

-- The Registry results (rare): chance 0.3% per heartbeat
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780240, 5, 0.003, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 88, 0, 0, 'pat_results_1'),
  (@e, 1, 88, 120, 0, 'pat_rest');

-- Denton, from a safe distance: chance 0.7% per heartbeat
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780240, 5, 0.01, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 88, 0, 0, 'pat_denton_1'),
  (@e, 1, 88, 120, 0, 'pat_rest');

-- He's NEON, Gorta: chance 0.8% per heartbeat
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780240, 5, 0.018, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 88, 0, 0, 'pat_neon_1'),
  (@e, 1, 88, 120, 0, 'pat_rest');

-- He's getting BRIGHTER: chance 0.8% per heartbeat
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780240, 5, 0.026, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 88, 0, 0, 'pat_brighter_1'),
  (@e, 1, 88, 120, 0, 'pat_rest');

-- Suspect one: Splotch: chance 0.8% per heartbeat
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780240, 5, 0.034, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 88, 0, 0, 'pat_splotch_1'),
  (@e, 1, 88, 120, 0, 'pat_rest');

-- Suspect two: the DJ: chance 0.8% per heartbeat
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780240, 5, 0.042, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 88, 0, 0, 'pat_dj_1'),
  (@e, 1, 88, 120, 0, 'pat_rest');

-- Suspect three: Gary: chance 0.8% per heartbeat
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780240, 5, 0.05, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 88, 0, 0, 'pat_gary_1'),
  (@e, 1, 88, 120, 0, 'pat_rest');

-- =====================================================================================
-- PART 6  Scene Tester (admin only). Click = a random scene. Say the exact phrase next to it
--         for a specific scene. It tells you what it started, then stays busy 35 s so scenes
--         cannot overlap; clicks and phrases during that time are ignored.
-- =====================================================================================

-- click -> He's NEON, Gorta
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780241, 7, 0.1429, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Starting: He''s NEON, Gorta. Ready again in 35 seconds.'),
  (@e, 1, 88, 0, 0, 'pat_neon_1'),
  (@e, 2, 88, 35, 0, 'pat_rest');

-- click -> He's getting BRIGHTER
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780241, 7, 0.2857, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Starting: He''s getting BRIGHTER. Ready again in 35 seconds.'),
  (@e, 1, 88, 0, 0, 'pat_brighter_1'),
  (@e, 2, 88, 35, 0, 'pat_rest');

-- click -> Suspect one: Splotch
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780241, 7, 0.4286, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Starting: Suspect one: Splotch. Ready again in 35 seconds.'),
  (@e, 1, 88, 0, 0, 'pat_splotch_1'),
  (@e, 2, 88, 35, 0, 'pat_rest');

-- click -> Suspect two: the DJ
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780241, 7, 0.5714, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Starting: Suspect two: the DJ. Ready again in 35 seconds.'),
  (@e, 1, 88, 0, 0, 'pat_dj_1'),
  (@e, 2, 88, 35, 0, 'pat_rest');

-- click -> Suspect three: Gary
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780241, 7, 0.7143, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Starting: Suspect three: Gary. Ready again in 35 seconds.'),
  (@e, 1, 88, 0, 0, 'pat_gary_1'),
  (@e, 2, 88, 35, 0, 'pat_rest');

-- click -> Denton, from a safe distance
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780241, 7, 0.8571, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Starting: Denton, from a safe distance. Ready again in 35 seconds.'),
  (@e, 1, 88, 0, 0, 'pat_denton_1'),
  (@e, 2, 88, 35, 0, 'pat_rest');

-- click -> The Registry results (rare)
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780241, 7, 1, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Starting: The Registry results (rare). Ready again in 35 seconds.'),
  (@e, 1, 88, 0, 0, 'pat_results_1'),
  (@e, 2, 88, 35, 0, 'pat_rest');

-- say "scene neon"
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780241, 24, 1, 'scene neon');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Starting: He''s NEON, Gorta. Ready again in 35 seconds.'),
  (@e, 1, 88, 0, 0, 'pat_neon_1'),
  (@e, 2, 88, 35, 0, 'pat_rest');

-- say "scene brighter"
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780241, 24, 1, 'scene brighter');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Starting: He''s getting BRIGHTER. Ready again in 35 seconds.'),
  (@e, 1, 88, 0, 0, 'pat_brighter_1'),
  (@e, 2, 88, 35, 0, 'pat_rest');

-- say "scene splotch"
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780241, 24, 1, 'scene splotch');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Starting: Suspect one: Splotch. Ready again in 35 seconds.'),
  (@e, 1, 88, 0, 0, 'pat_splotch_1'),
  (@e, 2, 88, 35, 0, 'pat_rest');

-- say "scene dj"
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780241, 24, 1, 'scene dj');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Starting: Suspect two: the DJ. Ready again in 35 seconds.'),
  (@e, 1, 88, 0, 0, 'pat_dj_1'),
  (@e, 2, 88, 35, 0, 'pat_rest');

-- say "scene gary"
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780241, 24, 1, 'scene gary');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Starting: Suspect three: Gary. Ready again in 35 seconds.'),
  (@e, 1, 88, 0, 0, 'pat_gary_1'),
  (@e, 2, 88, 35, 0, 'pat_rest');

-- say "scene denton"
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780241, 24, 1, 'scene denton');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Starting: Denton, from a safe distance. Ready again in 35 seconds.'),
  (@e, 1, 88, 0, 0, 'pat_denton_1'),
  (@e, 2, 88, 35, 0, 'pat_rest');

-- say "scene results"
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780241, 24, 1, 'scene results');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Starting: The Registry results (rare). Ready again in 35 seconds.'),
  (@e, 1, 88, 0, 0, 'pat_results_1'),
  (@e, 2, 88, 35, 0, 'pat_rest');

-- =====================================================================================
-- PART 7  Click lines: one private Tell per click, picked evenly (stacked thresholds).
-- =====================================================================================

-- Mubb click 1
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780230, 7, 0.2, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Look at me. Now look at the baby. Now look at the DJ.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780230, 7, 0.4, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'I''ve narrowed it down to everyone.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780230, 7, 0.6, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Nobody in my family glows. We checked. We checked twice.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780230, 7, 0.8, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'She says it''s a phase. Phases don''t have a colour.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780230, 7, 1, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'If you know anything, you tell me first. Not her. Me.');

-- Gorta click 1
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780231, 7, 0.2, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'He''s the father. Tell him he''s the father.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780231, 7, 0.4, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Twelve years, and he thinks I''d go near GARY.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780231, 7, 0.6, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Babies change colour. Everybody knows babies change colour.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780231, 7, 0.8, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'If the Registry man comes by, we are NOT a case file.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780231, 7, 1, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Don''t look at the baby like that. That''s how Mubb started.');

-- the baby click 1
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780232, 7, 0.25, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'I glow in the dark. Dad says it''s a phase.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780232, 7, 0.5, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Why does Dad keep staring at Gary?');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780232, 7, 0.75, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Mum says I''m a mystery.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780232, 7, 1, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Everyone yells when I''m around. I think it means they like me.');

-- Denton click 1
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780233, 7, 0.25, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Don''t stand too close to room four. It''s in the air.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780233, 7, 0.5, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'I washed my hands six times today. Do they look purple? Don''t look at them.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780233, 7, 0.75, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Mutations are contagious. The Registry won''t confirm it, which is basically confirming it.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780233, 7, 1, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Stay on the brown side of the hallway. It''s safer.');

SET SQL_SAFE_UPDATES = @__old_safe_updates;

-- Ruggan's Annex: paternity storyline loaded.
