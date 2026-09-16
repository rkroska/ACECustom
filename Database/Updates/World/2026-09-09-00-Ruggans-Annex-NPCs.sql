-- =====================================================================================
-- Ruggan's Annex - NPC weenies
-- Design: docs/PET_BREEDING_ANNEX_DESIGN.md
--
-- Creates 15 NPCs in the reserved WCID block 78780200-78780249: a greeter, a vendor,
-- a drudge on the dance floor, two faction leads, eight faction creatures and two
-- walk-ons. They talk to each other using LocalSignal emote chains (see part 4).
--
-- This file only CREATES the weenies. It does not place them - see part 5 for that.
--
-- Everything here is derived from the live databases rather than guessed:
--   * templates are NPCs and creatures that already exist and render correctly,
--     several of them already standing in this same landblock
--   * property ids come from Source/ACE.Entity/Enum/Properties/*.cs
--   * the Ward's mutation palettes are palette ids this server's own breeding code
--     has already rolled (ace_shard.biota_properties_int type 9035), so they are
--     known to survive PetMutationService.IsUsableCreaturePalette
--
-- All text is 7-bit ASCII per CLAUDE.md.
-- Re-runnable: the DELETE below cascades to every weenie_properties_* table.
-- Target database: ace_world
-- =====================================================================================

-- =====================================================================================
-- PART 1  Clean slate (FKs are ON DELETE CASCADE, so this clears every property table)
--         Only the 15 WCIDs this file defines are removed. The rest of the reserved
--         78780200-78780249 block belongs to other annex patches (props, later NPCs) and
--         must survive a re-run of this one.
-- =====================================================================================

DELETE FROM `weenie` WHERE `class_Id` IN (
  78780200, 78780201, 78780202, 78780203, 78780204,
  78780210, 78780211, 78780212, 78780213, 78780214,
  78780220, 78780221, 78780222, 78780223, 78780224
);

-- =====================================================================================
-- PART 2  Create each NPC by cloning a known-good template, then override what differs.
--         Cloning rather than hand-writing DIDs is what keeps the models, motion
--         tables, sounds and body parts correct.
-- =====================================================================================

-- ------------------------------------------------------------------------------------
-- 78780200  Fenwick, Kennel Intern
--     template 42720 - Ealdred (human male NPC, already standing in the motel)
-- ------------------------------------------------------------------------------------
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780200, 'annex-fenwick', 10, NOW());

INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`)
  SELECT 78780200,`type`,`value` FROM `weenie_properties_int` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`)
  SELECT 78780200,`type`,`value` FROM `weenie_properties_bool` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`)
  SELECT 78780200,`type`,`value` FROM `weenie_properties_float` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`)
  SELECT 78780200,`type`,`value` FROM `weenie_properties_string` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_create_list` (`object_Id`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`)
  SELECT 78780200,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond` FROM `weenie_properties_create_list` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`)
  SELECT 78780200,`type`,`value` FROM `weenie_properties_d_i_d` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`)
  SELECT 78780200,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM `weenie_properties_attribute` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`)
  SELECT 78780200,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`)
  SELECT 78780200,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM `weenie_properties_skill` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`)
  SELECT 78780200,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM `weenie_properties_body_part` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_palette` (`object_Id`,`sub_Palette_Id`,`offset`,`length`)
  SELECT 78780200,`sub_Palette_Id`,`offset`,`length` FROM `weenie_properties_palette` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_texture_map` (`object_Id`,`index`,`old_Id`,`new_Id`)
  SELECT 78780200,`index`,`old_Id`,`new_Id` FROM `weenie_properties_texture_map` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_anim_part` (`object_Id`,`index`,`animation_Id`)
  SELECT 78780200,`index`,`animation_Id` FROM `weenie_properties_anim_part` WHERE `object_Id` = 42720;

UPDATE `weenie_properties_string` SET `value` = 'Fenwick, Kennel Intern' WHERE `object_Id` = 78780200 AND `type` = 1;
-- Creature.cs:153  IsNPC => !Attackable && TargetingTactic == None
DELETE FROM `weenie_properties_int`  WHERE `object_Id` = 78780200 AND `type` IN (67, 68, 16, 95, 133, 134, 290, 291);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780200 AND `type` IN (1, 19, 98);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780200, 16, 32), -- ItemUseable = Remote (clickable for dialogue)
  (78780200, 95, 8), -- RadarBlipColor = NPC
  (78780200, 133, 4), -- ShowableOnRadar
  (78780200, 134, 16), -- PlayerKillerStatus = RubberGlue
  (78780200, 290, 1), -- HearLocalSignals - takes part in the feud
  (78780200, 291, 60); -- HearLocalSignalsRadius, metres
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780200,  1, True),   -- Stuck: stays where it is placed
  (78780200, 19, False),  -- Attackable: no
  (78780200, 98, True);   -- Invincible: belt and braces
-- Outfit addition on top of the template's clothes.
INSERT INTO `weenie_properties_create_list` (`object_Id`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`) VALUES
  (78780200, 2, 10697, NULL, 8, 0.5, False);

-- ------------------------------------------------------------------------------------
-- 78780201  Ivo, Ruggan's Quartermaster
--     template 46425 - Marid (human male vendor, already standing in the motel)
-- ------------------------------------------------------------------------------------
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780201, 'annex-ivo', 12, NOW());

INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`)
  SELECT 78780201,`type`,`value` FROM `weenie_properties_int` WHERE `object_Id` = 46425;
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`)
  SELECT 78780201,`type`,`value` FROM `weenie_properties_bool` WHERE `object_Id` = 46425;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`)
  SELECT 78780201,`type`,`value` FROM `weenie_properties_float` WHERE `object_Id` = 46425;
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`)
  SELECT 78780201,`type`,`value` FROM `weenie_properties_string` WHERE `object_Id` = 46425;
INSERT INTO `weenie_properties_create_list` (`object_Id`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`)
  SELECT 78780201,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond` FROM `weenie_properties_create_list` WHERE `object_Id` = 46425;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`)
  SELECT 78780201,`type`,`value` FROM `weenie_properties_d_i_d` WHERE `object_Id` = 46425;
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`)
  SELECT 78780201,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM `weenie_properties_attribute` WHERE `object_Id` = 46425;
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`)
  SELECT 78780201,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 46425;
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`)
  SELECT 78780201,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM `weenie_properties_skill` WHERE `object_Id` = 46425;
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`)
  SELECT 78780201,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM `weenie_properties_body_part` WHERE `object_Id` = 46425;
INSERT INTO `weenie_properties_palette` (`object_Id`,`sub_Palette_Id`,`offset`,`length`)
  SELECT 78780201,`sub_Palette_Id`,`offset`,`length` FROM `weenie_properties_palette` WHERE `object_Id` = 46425;
INSERT INTO `weenie_properties_texture_map` (`object_Id`,`index`,`old_Id`,`new_Id`)
  SELECT 78780201,`index`,`old_Id`,`new_Id` FROM `weenie_properties_texture_map` WHERE `object_Id` = 46425;
INSERT INTO `weenie_properties_anim_part` (`object_Id`,`index`,`animation_Id`)
  SELECT 78780201,`index`,`animation_Id` FROM `weenie_properties_anim_part` WHERE `object_Id` = 46425;

UPDATE `weenie_properties_string` SET `value` = 'Ivo, Ruggan''s Quartermaster' WHERE `object_Id` = 78780201 AND `type` = 1;
-- Creature.cs:153  IsNPC => !Attackable && TargetingTactic == None
DELETE FROM `weenie_properties_int`  WHERE `object_Id` = 78780201 AND `type` IN (67, 68, 16, 95, 133, 134, 290, 291);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780201 AND `type` IN (1, 19, 98);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780201, 16, 32), -- ItemUseable = Remote (clickable for dialogue)
  (78780201, 95, 8), -- RadarBlipColor = NPC
  (78780201, 133, 4), -- ShowableOnRadar
  (78780201, 134, 16); -- PlayerKillerStatus = RubberGlue
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780201,  1, True),   -- Stuck: stays where it is placed
  (78780201, 19, False),  -- Attackable: no
  (78780201, 98, True);   -- Invincible: belt and braces
-- Vendor stock (destination_Type 4). The primed kit 98760401 is deliberately not
-- sold: it only exists as the result of using 98760400 on a pet.
DELETE FROM `weenie_properties_create_list` WHERE `object_Id` = 78780201 AND `destination_Type` = 4;
INSERT INTO `weenie_properties_create_list` (`object_Id`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`) VALUES
  (78780201, 4, 98760399, NULL, 0, 0, False),   -- Neutering Kit
  (78780201, 4, 98760400, NULL, 0, 0, False);   -- Pet Tailoring Kit
DELETE FROM `weenie_properties_int` WHERE `object_Id` = 78780201 AND `type` IN (74,75,76);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780201, 74, 0),         -- MerchandiseItemTypes: buys nothing back
  (78780201, 75, 0),         -- MerchandiseMinValue
  (78780201, 76, 1000000);   -- MerchandiseMaxValue

-- ------------------------------------------------------------------------------------
-- 78780202  DJ Skulk
--     template 5595 - drudgeskulkerdancer (retail dancing drudge)
-- ------------------------------------------------------------------------------------
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780202, 'annex-dj-skulk', 10, NOW());

INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`)
  SELECT 78780202,`type`,`value` FROM `weenie_properties_int` WHERE `object_Id` = 5595;
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`)
  SELECT 78780202,`type`,`value` FROM `weenie_properties_bool` WHERE `object_Id` = 5595;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`)
  SELECT 78780202,`type`,`value` FROM `weenie_properties_float` WHERE `object_Id` = 5595;
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`)
  SELECT 78780202,`type`,`value` FROM `weenie_properties_string` WHERE `object_Id` = 5595;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`)
  SELECT 78780202,`type`,`value` FROM `weenie_properties_d_i_d` WHERE `object_Id` = 5595;
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`)
  SELECT 78780202,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM `weenie_properties_attribute` WHERE `object_Id` = 5595;
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`)
  SELECT 78780202,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 5595;
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`)
  SELECT 78780202,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM `weenie_properties_skill` WHERE `object_Id` = 5595;
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`)
  SELECT 78780202,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM `weenie_properties_body_part` WHERE `object_Id` = 5595;
INSERT INTO `weenie_properties_palette` (`object_Id`,`sub_Palette_Id`,`offset`,`length`)
  SELECT 78780202,`sub_Palette_Id`,`offset`,`length` FROM `weenie_properties_palette` WHERE `object_Id` = 5595;
INSERT INTO `weenie_properties_texture_map` (`object_Id`,`index`,`old_Id`,`new_Id`)
  SELECT 78780202,`index`,`old_Id`,`new_Id` FROM `weenie_properties_texture_map` WHERE `object_Id` = 5595;
INSERT INTO `weenie_properties_anim_part` (`object_Id`,`index`,`animation_Id`)
  SELECT 78780202,`index`,`animation_Id` FROM `weenie_properties_anim_part` WHERE `object_Id` = 5595;

-- The retail dancing drudge's loop, written out rather than cloned so it stays
-- readable. These are stance-gated HeartBeat motion emotes: EmoteManager.cs:3438
-- filters HeartBeat sets by the object's current stance and motion, so they only
-- fire in the matching pose. DJ Skulk's spoken lines are added in part 4 with a
-- NULL style, which matches any stance, so he both dances and talks.
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`style`,`substyle`)
VALUES (78780202, 5, 0.8, 2147483708, 1090519043);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`)
VALUES (@e, 0, 5, 0, 0, 268435537);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`style`,`substyle`)
VALUES (78780202, 5, 1, 2147483708, 1090519043);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`)
VALUES (@e, 0, 5, 0, 0, 268435538);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`style`,`substyle`)
VALUES (78780202, 5, 0.8, 2147483710, 1090519043);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`)
VALUES (@e, 0, 5, 0, 0, 268435537);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`style`,`substyle`)
VALUES (78780202, 5, 1, 2147483710, 1090519043);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`)
VALUES (@e, 0, 5, 0, 0, 268435538);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`style`,`substyle`)
VALUES (78780202, 5, 0.8, 2147483709, 1090519043);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`)
VALUES (@e, 0, 5, 0, 0, 268435537);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`style`,`substyle`)
VALUES (78780202, 5, 1, 2147483709, 1090519043);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`)
VALUES (@e, 0, 5, 0, 0, 268435538);


UPDATE `weenie_properties_string` SET `value` = 'DJ Skulk' WHERE `object_Id` = 78780202 AND `type` = 1;
-- Creature.cs:153  IsNPC => !Attackable && TargetingTactic == None
DELETE FROM `weenie_properties_int`  WHERE `object_Id` = 78780202 AND `type` IN (67, 68, 16, 95, 133, 134, 290, 291);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780202 AND `type` IN (1, 19, 98);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780202, 16, 32), -- ItemUseable = Remote (clickable for dialogue)
  (78780202, 95, 8), -- RadarBlipColor = NPC
  (78780202, 133, 4), -- ShowableOnRadar
  (78780202, 134, 16); -- PlayerKillerStatus = RubberGlue
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780202,  1, True),   -- Stuck: stays where it is placed
  (78780202, 19, False),  -- Attackable: no
  (78780202, 98, True);   -- Invincible: belt and braces

-- ------------------------------------------------------------------------------------
-- 78780203  Gary
--     template 29008 - Browerk
-- ------------------------------------------------------------------------------------
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780203, 'annex-gary', 10, NOW());

INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`)
  SELECT 78780203,`type`,`value` FROM `weenie_properties_int` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`)
  SELECT 78780203,`type`,`value` FROM `weenie_properties_bool` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`)
  SELECT 78780203,`type`,`value` FROM `weenie_properties_float` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`)
  SELECT 78780203,`type`,`value` FROM `weenie_properties_string` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`)
  SELECT 78780203,`type`,`value` FROM `weenie_properties_d_i_d` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`)
  SELECT 78780203,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM `weenie_properties_attribute` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`)
  SELECT 78780203,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`)
  SELECT 78780203,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM `weenie_properties_skill` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`)
  SELECT 78780203,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM `weenie_properties_body_part` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_palette` (`object_Id`,`sub_Palette_Id`,`offset`,`length`)
  SELECT 78780203,`sub_Palette_Id`,`offset`,`length` FROM `weenie_properties_palette` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_texture_map` (`object_Id`,`index`,`old_Id`,`new_Id`)
  SELECT 78780203,`index`,`old_Id`,`new_Id` FROM `weenie_properties_texture_map` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_anim_part` (`object_Id`,`index`,`animation_Id`)
  SELECT 78780203,`index`,`animation_Id` FROM `weenie_properties_anim_part` WHERE `object_Id` = 29008;

UPDATE `weenie_properties_string` SET `value` = 'Gary' WHERE `object_Id` = 78780203 AND `type` = 1;
-- Creature.cs:153  IsNPC => !Attackable && TargetingTactic == None
DELETE FROM `weenie_properties_int`  WHERE `object_Id` = 78780203 AND `type` IN (67, 68, 16, 95, 133, 134, 290, 291);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780203 AND `type` IN (1, 19, 98);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780203, 16, 32), -- ItemUseable = Remote (clickable for dialogue)
  (78780203, 95, 8), -- RadarBlipColor = NPC
  (78780203, 133, 4), -- ShowableOnRadar
  (78780203, 134, 16), -- PlayerKillerStatus = RubberGlue
  (78780203, 290, 1), -- HearLocalSignals - takes part in the feud
  (78780203, 291, 60); -- HearLocalSignalsRadius, metres
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780203,  1, True),   -- Stuck: stays where it is placed
  (78780203, 19, False),  -- Attackable: no
  (78780203, 98, True);   -- Invincible: belt and braces

-- ------------------------------------------------------------------------------------
-- 78780204  Mrs. Ruggan
--     template 3920 - collectorsho (human female NPC)
-- ------------------------------------------------------------------------------------
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780204, 'annex-mrs-ruggan', 10, NOW());

INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`)
  SELECT 78780204,`type`,`value` FROM `weenie_properties_int` WHERE `object_Id` = 3920;
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`)
  SELECT 78780204,`type`,`value` FROM `weenie_properties_bool` WHERE `object_Id` = 3920;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`)
  SELECT 78780204,`type`,`value` FROM `weenie_properties_float` WHERE `object_Id` = 3920;
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`)
  SELECT 78780204,`type`,`value` FROM `weenie_properties_string` WHERE `object_Id` = 3920;
INSERT INTO `weenie_properties_create_list` (`object_Id`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`)
  SELECT 78780204,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond` FROM `weenie_properties_create_list` WHERE `object_Id` = 3920;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`)
  SELECT 78780204,`type`,`value` FROM `weenie_properties_d_i_d` WHERE `object_Id` = 3920;
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`)
  SELECT 78780204,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM `weenie_properties_attribute` WHERE `object_Id` = 3920;
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`)
  SELECT 78780204,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 3920;
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`)
  SELECT 78780204,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM `weenie_properties_skill` WHERE `object_Id` = 3920;
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`)
  SELECT 78780204,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM `weenie_properties_body_part` WHERE `object_Id` = 3920;
INSERT INTO `weenie_properties_palette` (`object_Id`,`sub_Palette_Id`,`offset`,`length`)
  SELECT 78780204,`sub_Palette_Id`,`offset`,`length` FROM `weenie_properties_palette` WHERE `object_Id` = 3920;
INSERT INTO `weenie_properties_texture_map` (`object_Id`,`index`,`old_Id`,`new_Id`)
  SELECT 78780204,`index`,`old_Id`,`new_Id` FROM `weenie_properties_texture_map` WHERE `object_Id` = 3920;
INSERT INTO `weenie_properties_anim_part` (`object_Id`,`index`,`animation_Id`)
  SELECT 78780204,`index`,`animation_Id` FROM `weenie_properties_anim_part` WHERE `object_Id` = 3920;

UPDATE `weenie_properties_string` SET `value` = 'Mrs. Ruggan' WHERE `object_Id` = 78780204 AND `type` = 1;
-- Creature.cs:153  IsNPC => !Attackable && TargetingTactic == None
DELETE FROM `weenie_properties_int`  WHERE `object_Id` = 78780204 AND `type` IN (67, 68, 16, 95, 133, 134, 290, 291);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780204 AND `type` IN (1, 19, 98);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780204, 16, 32), -- ItemUseable = Remote (clickable for dialogue)
  (78780204, 95, 8), -- RadarBlipColor = NPC
  (78780204, 133, 4), -- ShowableOnRadar
  (78780204, 134, 16); -- PlayerKillerStatus = RubberGlue
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780204,  1, True),   -- Stuck: stays where it is placed
  (78780204, 19, False),  -- Attackable: no
  (78780204, 98, True);   -- Invincible: belt and braces
-- Outfit (create_list destination_Type 2 = worn).
DELETE FROM `weenie_properties_create_list` WHERE `object_Id` = 78780204 AND `destination_Type` = 2;
INSERT INTO `weenie_properties_create_list` (`object_Id`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`) VALUES
  (78780204, 2, 8371, NULL, 11, 0.3, False),
  (78780204, 2, 132, NULL, 39, 0.9, False);
-- Template 3920 is already female (Gender 2, setup 0x0200004E); nothing to fix.

-- ------------------------------------------------------------------------------------
-- 78780210  Bexley, Keeper of the Registry
--     template 42720 - Ealdred (human male NPC, already standing in the motel)
-- ------------------------------------------------------------------------------------
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780210, 'annex-bexley', 10, NOW());

INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`)
  SELECT 78780210,`type`,`value` FROM `weenie_properties_int` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`)
  SELECT 78780210,`type`,`value` FROM `weenie_properties_bool` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`)
  SELECT 78780210,`type`,`value` FROM `weenie_properties_float` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`)
  SELECT 78780210,`type`,`value` FROM `weenie_properties_string` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_create_list` (`object_Id`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`)
  SELECT 78780210,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond` FROM `weenie_properties_create_list` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`)
  SELECT 78780210,`type`,`value` FROM `weenie_properties_d_i_d` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`)
  SELECT 78780210,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM `weenie_properties_attribute` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`)
  SELECT 78780210,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`)
  SELECT 78780210,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM `weenie_properties_skill` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`)
  SELECT 78780210,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM `weenie_properties_body_part` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_palette` (`object_Id`,`sub_Palette_Id`,`offset`,`length`)
  SELECT 78780210,`sub_Palette_Id`,`offset`,`length` FROM `weenie_properties_palette` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_texture_map` (`object_Id`,`index`,`old_Id`,`new_Id`)
  SELECT 78780210,`index`,`old_Id`,`new_Id` FROM `weenie_properties_texture_map` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_anim_part` (`object_Id`,`index`,`animation_Id`)
  SELECT 78780210,`index`,`animation_Id` FROM `weenie_properties_anim_part` WHERE `object_Id` = 42720;

UPDATE `weenie_properties_string` SET `value` = 'Bexley, Keeper of the Registry' WHERE `object_Id` = 78780210 AND `type` = 1;
-- Creature.cs:153  IsNPC => !Attackable && TargetingTactic == None
DELETE FROM `weenie_properties_int`  WHERE `object_Id` = 78780210 AND `type` IN (67, 68, 16, 95, 133, 134, 290, 291);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780210 AND `type` IN (1, 19, 98);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780210, 16, 32), -- ItemUseable = Remote (clickable for dialogue)
  (78780210, 95, 8), -- RadarBlipColor = NPC
  (78780210, 133, 4), -- ShowableOnRadar
  (78780210, 134, 16), -- PlayerKillerStatus = RubberGlue
  (78780210, 290, 1), -- HearLocalSignals - takes part in the feud
  (78780210, 291, 60); -- HearLocalSignalsRadius, metres
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780210,  1, True),   -- Stuck: stays where it is placed
  (78780210, 19, False),  -- Attackable: no
  (78780210, 98, True);   -- Invincible: belt and braces
-- Outfit (create_list destination_Type 2 = worn).
DELETE FROM `weenie_properties_create_list` WHERE `object_Id` = 78780210 AND `destination_Type` = 2;
INSERT INTO `weenie_properties_create_list` (`object_Id`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`) VALUES
  (78780210, 2, 130, NULL, 61, 0.1, False),
  (78780210, 2, 117, NULL, 39, 0.9, False),
  (78780210, 2, 132, NULL, 39, 0.9, False),
  (78780210, 2, 5588, NULL, 39, 0.9, False);

-- ------------------------------------------------------------------------------------
-- 78780211  Registered Browerk, Champion Line
--     template 29008 - Browerk
-- ------------------------------------------------------------------------------------
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780211, 'annex-registry-browerk', 10, NOW());

INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`)
  SELECT 78780211,`type`,`value` FROM `weenie_properties_int` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`)
  SELECT 78780211,`type`,`value` FROM `weenie_properties_bool` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`)
  SELECT 78780211,`type`,`value` FROM `weenie_properties_float` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`)
  SELECT 78780211,`type`,`value` FROM `weenie_properties_string` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`)
  SELECT 78780211,`type`,`value` FROM `weenie_properties_d_i_d` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`)
  SELECT 78780211,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM `weenie_properties_attribute` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`)
  SELECT 78780211,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`)
  SELECT 78780211,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM `weenie_properties_skill` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`)
  SELECT 78780211,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM `weenie_properties_body_part` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_palette` (`object_Id`,`sub_Palette_Id`,`offset`,`length`)
  SELECT 78780211,`sub_Palette_Id`,`offset`,`length` FROM `weenie_properties_palette` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_texture_map` (`object_Id`,`index`,`old_Id`,`new_Id`)
  SELECT 78780211,`index`,`old_Id`,`new_Id` FROM `weenie_properties_texture_map` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_anim_part` (`object_Id`,`index`,`animation_Id`)
  SELECT 78780211,`index`,`animation_Id` FROM `weenie_properties_anim_part` WHERE `object_Id` = 29008;

UPDATE `weenie_properties_string` SET `value` = 'Registered Browerk, Champion Line' WHERE `object_Id` = 78780211 AND `type` = 1;
-- Creature.cs:153  IsNPC => !Attackable && TargetingTactic == None
DELETE FROM `weenie_properties_int`  WHERE `object_Id` = 78780211 AND `type` IN (67, 68, 16, 95, 133, 134, 290, 291);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780211 AND `type` IN (1, 19, 98);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780211, 16, 32), -- ItemUseable = Remote (clickable for dialogue)
  (78780211, 95, 8), -- RadarBlipColor = NPC
  (78780211, 133, 4), -- ShowableOnRadar
  (78780211, 134, 16), -- PlayerKillerStatus = RubberGlue
  (78780211, 290, 1), -- HearLocalSignals - takes part in the feud
  (78780211, 291, 60); -- HearLocalSignalsRadius, metres
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780211,  1, True),   -- Stuck: stays where it is placed
  (78780211, 19, False),  -- Attackable: no
  (78780211, 98, True);   -- Invincible: belt and braces

-- ------------------------------------------------------------------------------------
-- 78780212  Certified Shreth, Third Generation
--     template 4108 - Gnawer Shreth
-- ------------------------------------------------------------------------------------
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780212, 'annex-registry-shreth', 10, NOW());

INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`)
  SELECT 78780212,`type`,`value` FROM `weenie_properties_int` WHERE `object_Id` = 4108;
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`)
  SELECT 78780212,`type`,`value` FROM `weenie_properties_bool` WHERE `object_Id` = 4108;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`)
  SELECT 78780212,`type`,`value` FROM `weenie_properties_float` WHERE `object_Id` = 4108;
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`)
  SELECT 78780212,`type`,`value` FROM `weenie_properties_string` WHERE `object_Id` = 4108;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`)
  SELECT 78780212,`type`,`value` FROM `weenie_properties_d_i_d` WHERE `object_Id` = 4108;
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`)
  SELECT 78780212,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM `weenie_properties_attribute` WHERE `object_Id` = 4108;
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`)
  SELECT 78780212,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 4108;
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`)
  SELECT 78780212,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM `weenie_properties_skill` WHERE `object_Id` = 4108;
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`)
  SELECT 78780212,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM `weenie_properties_body_part` WHERE `object_Id` = 4108;
INSERT INTO `weenie_properties_palette` (`object_Id`,`sub_Palette_Id`,`offset`,`length`)
  SELECT 78780212,`sub_Palette_Id`,`offset`,`length` FROM `weenie_properties_palette` WHERE `object_Id` = 4108;
INSERT INTO `weenie_properties_texture_map` (`object_Id`,`index`,`old_Id`,`new_Id`)
  SELECT 78780212,`index`,`old_Id`,`new_Id` FROM `weenie_properties_texture_map` WHERE `object_Id` = 4108;
INSERT INTO `weenie_properties_anim_part` (`object_Id`,`index`,`animation_Id`)
  SELECT 78780212,`index`,`animation_Id` FROM `weenie_properties_anim_part` WHERE `object_Id` = 4108;

UPDATE `weenie_properties_string` SET `value` = 'Certified Shreth, Third Generation' WHERE `object_Id` = 78780212 AND `type` = 1;
-- Creature.cs:153  IsNPC => !Attackable && TargetingTactic == None
DELETE FROM `weenie_properties_int`  WHERE `object_Id` = 78780212 AND `type` IN (67, 68, 16, 95, 133, 134, 290, 291);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780212 AND `type` IN (1, 19, 98);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780212, 16, 32), -- ItemUseable = Remote (clickable for dialogue)
  (78780212, 95, 8), -- RadarBlipColor = NPC
  (78780212, 133, 4), -- ShowableOnRadar
  (78780212, 134, 16), -- PlayerKillerStatus = RubberGlue
  (78780212, 290, 1), -- HearLocalSignals - takes part in the feud
  (78780212, 291, 60); -- HearLocalSignalsRadius, metres
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780212,  1, True),   -- Stuck: stays where it is placed
  (78780212, 19, False),  -- Attackable: no
  (78780212, 98, True);   -- Invincible: belt and braces

-- ------------------------------------------------------------------------------------
-- 78780213  Pedigreed Ursuin (Papers Pending)
--     template 7990 - Field Ursuin
-- ------------------------------------------------------------------------------------
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780213, 'annex-registry-ursuin', 10, NOW());

INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`)
  SELECT 78780213,`type`,`value` FROM `weenie_properties_int` WHERE `object_Id` = 7990;
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`)
  SELECT 78780213,`type`,`value` FROM `weenie_properties_bool` WHERE `object_Id` = 7990;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`)
  SELECT 78780213,`type`,`value` FROM `weenie_properties_float` WHERE `object_Id` = 7990;
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`)
  SELECT 78780213,`type`,`value` FROM `weenie_properties_string` WHERE `object_Id` = 7990;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`)
  SELECT 78780213,`type`,`value` FROM `weenie_properties_d_i_d` WHERE `object_Id` = 7990;
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`)
  SELECT 78780213,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM `weenie_properties_attribute` WHERE `object_Id` = 7990;
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`)
  SELECT 78780213,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 7990;
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`)
  SELECT 78780213,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM `weenie_properties_skill` WHERE `object_Id` = 7990;
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`)
  SELECT 78780213,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM `weenie_properties_body_part` WHERE `object_Id` = 7990;
INSERT INTO `weenie_properties_palette` (`object_Id`,`sub_Palette_Id`,`offset`,`length`)
  SELECT 78780213,`sub_Palette_Id`,`offset`,`length` FROM `weenie_properties_palette` WHERE `object_Id` = 7990;
INSERT INTO `weenie_properties_texture_map` (`object_Id`,`index`,`old_Id`,`new_Id`)
  SELECT 78780213,`index`,`old_Id`,`new_Id` FROM `weenie_properties_texture_map` WHERE `object_Id` = 7990;
INSERT INTO `weenie_properties_anim_part` (`object_Id`,`index`,`animation_Id`)
  SELECT 78780213,`index`,`animation_Id` FROM `weenie_properties_anim_part` WHERE `object_Id` = 7990;

UPDATE `weenie_properties_string` SET `value` = 'Pedigreed Ursuin (Papers Pending)' WHERE `object_Id` = 78780213 AND `type` = 1;
-- Creature.cs:153  IsNPC => !Attackable && TargetingTactic == None
DELETE FROM `weenie_properties_int`  WHERE `object_Id` = 78780213 AND `type` IN (67, 68, 16, 95, 133, 134, 290, 291);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780213 AND `type` IN (1, 19, 98);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780213, 16, 32), -- ItemUseable = Remote (clickable for dialogue)
  (78780213, 95, 8), -- RadarBlipColor = NPC
  (78780213, 133, 4), -- ShowableOnRadar
  (78780213, 134, 16), -- PlayerKillerStatus = RubberGlue
  (78780213, 290, 1), -- HearLocalSignals - takes part in the feud
  (78780213, 291, 60); -- HearLocalSignalsRadius, metres
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780213,  1, True),   -- Stuck: stays where it is placed
  (78780213, 19, False),  -- Attackable: no
  (78780213, 98, True);   -- Invincible: belt and braces

-- ------------------------------------------------------------------------------------
-- 78780214  Drudge Skulker of Record
--     template 7 - Drudge Skulker
-- ------------------------------------------------------------------------------------
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780214, 'annex-registry-drudge', 10, NOW());

INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`)
  SELECT 78780214,`type`,`value` FROM `weenie_properties_int` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`)
  SELECT 78780214,`type`,`value` FROM `weenie_properties_bool` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`)
  SELECT 78780214,`type`,`value` FROM `weenie_properties_float` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`)
  SELECT 78780214,`type`,`value` FROM `weenie_properties_string` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`)
  SELECT 78780214,`type`,`value` FROM `weenie_properties_d_i_d` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`)
  SELECT 78780214,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM `weenie_properties_attribute` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`)
  SELECT 78780214,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`)
  SELECT 78780214,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM `weenie_properties_skill` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`)
  SELECT 78780214,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM `weenie_properties_body_part` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_palette` (`object_Id`,`sub_Palette_Id`,`offset`,`length`)
  SELECT 78780214,`sub_Palette_Id`,`offset`,`length` FROM `weenie_properties_palette` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_texture_map` (`object_Id`,`index`,`old_Id`,`new_Id`)
  SELECT 78780214,`index`,`old_Id`,`new_Id` FROM `weenie_properties_texture_map` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_anim_part` (`object_Id`,`index`,`animation_Id`)
  SELECT 78780214,`index`,`animation_Id` FROM `weenie_properties_anim_part` WHERE `object_Id` = 7;

UPDATE `weenie_properties_string` SET `value` = 'Drudge Skulker of Record' WHERE `object_Id` = 78780214 AND `type` = 1;
-- Creature.cs:153  IsNPC => !Attackable && TargetingTactic == None
DELETE FROM `weenie_properties_int`  WHERE `object_Id` = 78780214 AND `type` IN (67, 68, 16, 95, 133, 134, 290, 291);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780214 AND `type` IN (1, 19, 98);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780214, 16, 32), -- ItemUseable = Remote (clickable for dialogue)
  (78780214, 95, 8), -- RadarBlipColor = NPC
  (78780214, 133, 4), -- ShowableOnRadar
  (78780214, 134, 16), -- PlayerKillerStatus = RubberGlue
  (78780214, 290, 1), -- HearLocalSignals - takes part in the feud
  (78780214, 291, 60); -- HearLocalSignalsRadius, metres
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780214,  1, True),   -- Stuck: stays where it is placed
  (78780214, 19, False),  -- Attackable: no
  (78780214, 98, True);   -- Invincible: belt and braces

-- ------------------------------------------------------------------------------------
-- 78780220  Splotch
--     template 29008 - Browerk
-- ------------------------------------------------------------------------------------
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780220, 'annex-ward-splotch', 10, NOW());

INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`)
  SELECT 78780220,`type`,`value` FROM `weenie_properties_int` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`)
  SELECT 78780220,`type`,`value` FROM `weenie_properties_bool` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`)
  SELECT 78780220,`type`,`value` FROM `weenie_properties_float` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`)
  SELECT 78780220,`type`,`value` FROM `weenie_properties_string` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`)
  SELECT 78780220,`type`,`value` FROM `weenie_properties_d_i_d` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`)
  SELECT 78780220,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM `weenie_properties_attribute` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`)
  SELECT 78780220,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`)
  SELECT 78780220,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM `weenie_properties_skill` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`)
  SELECT 78780220,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM `weenie_properties_body_part` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_palette` (`object_Id`,`sub_Palette_Id`,`offset`,`length`)
  SELECT 78780220,`sub_Palette_Id`,`offset`,`length` FROM `weenie_properties_palette` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_texture_map` (`object_Id`,`index`,`old_Id`,`new_Id`)
  SELECT 78780220,`index`,`old_Id`,`new_Id` FROM `weenie_properties_texture_map` WHERE `object_Id` = 29008;
INSERT INTO `weenie_properties_anim_part` (`object_Id`,`index`,`animation_Id`)
  SELECT 78780220,`index`,`animation_Id` FROM `weenie_properties_anim_part` WHERE `object_Id` = 29008;

UPDATE `weenie_properties_string` SET `value` = 'Splotch' WHERE `object_Id` = 78780220 AND `type` = 1;
-- Creature.cs:153  IsNPC => !Attackable && TargetingTactic == None
DELETE FROM `weenie_properties_int`  WHERE `object_Id` = 78780220 AND `type` IN (67, 68, 16, 95, 133, 134, 290, 291);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780220 AND `type` IN (1, 19, 98);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780220, 16, 32), -- ItemUseable = Remote (clickable for dialogue)
  (78780220, 95, 8), -- RadarBlipColor = NPC
  (78780220, 133, 4), -- ShowableOnRadar
  (78780220, 134, 16), -- PlayerKillerStatus = RubberGlue
  (78780220, 290, 1), -- HearLocalSignals - takes part in the feud
  (78780220, 291, 60); -- HearLocalSignalsRadius, metres
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780220,  1, True),   -- Stuck: stays where it is placed
  (78780220, 19, False),  -- Attackable: no
  (78780220, 98, True);   -- Invincible: belt and braces
-- Mutation colour. Creature_Networking.cs:259 treats a PaletteTemplate whose high
-- byte is 0x04 as a full DAT palette DID and overlays it, which is exactly what a
-- bred mutation looks like. 0x040008B4 was rolled by this server's own breeding code.
DELETE FROM `weenie_properties_int` WHERE `object_Id` = 78780220 AND `type` = 3;
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES (78780220, 3, 67111092);  -- 0x040008B4

-- ------------------------------------------------------------------------------------
-- 78780221  The Teal Incident
--     template 4108 - Gnawer Shreth
-- ------------------------------------------------------------------------------------
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780221, 'annex-ward-teal', 10, NOW());

INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`)
  SELECT 78780221,`type`,`value` FROM `weenie_properties_int` WHERE `object_Id` = 4108;
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`)
  SELECT 78780221,`type`,`value` FROM `weenie_properties_bool` WHERE `object_Id` = 4108;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`)
  SELECT 78780221,`type`,`value` FROM `weenie_properties_float` WHERE `object_Id` = 4108;
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`)
  SELECT 78780221,`type`,`value` FROM `weenie_properties_string` WHERE `object_Id` = 4108;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`)
  SELECT 78780221,`type`,`value` FROM `weenie_properties_d_i_d` WHERE `object_Id` = 4108;
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`)
  SELECT 78780221,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM `weenie_properties_attribute` WHERE `object_Id` = 4108;
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`)
  SELECT 78780221,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 4108;
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`)
  SELECT 78780221,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM `weenie_properties_skill` WHERE `object_Id` = 4108;
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`)
  SELECT 78780221,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM `weenie_properties_body_part` WHERE `object_Id` = 4108;
INSERT INTO `weenie_properties_palette` (`object_Id`,`sub_Palette_Id`,`offset`,`length`)
  SELECT 78780221,`sub_Palette_Id`,`offset`,`length` FROM `weenie_properties_palette` WHERE `object_Id` = 4108;
INSERT INTO `weenie_properties_texture_map` (`object_Id`,`index`,`old_Id`,`new_Id`)
  SELECT 78780221,`index`,`old_Id`,`new_Id` FROM `weenie_properties_texture_map` WHERE `object_Id` = 4108;
INSERT INTO `weenie_properties_anim_part` (`object_Id`,`index`,`animation_Id`)
  SELECT 78780221,`index`,`animation_Id` FROM `weenie_properties_anim_part` WHERE `object_Id` = 4108;

UPDATE `weenie_properties_string` SET `value` = 'The Teal Incident' WHERE `object_Id` = 78780221 AND `type` = 1;
-- Creature.cs:153  IsNPC => !Attackable && TargetingTactic == None
DELETE FROM `weenie_properties_int`  WHERE `object_Id` = 78780221 AND `type` IN (67, 68, 16, 95, 133, 134, 290, 291);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780221 AND `type` IN (1, 19, 98);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780221, 16, 32), -- ItemUseable = Remote (clickable for dialogue)
  (78780221, 95, 8), -- RadarBlipColor = NPC
  (78780221, 133, 4), -- ShowableOnRadar
  (78780221, 134, 16), -- PlayerKillerStatus = RubberGlue
  (78780221, 290, 1), -- HearLocalSignals - takes part in the feud
  (78780221, 291, 60); -- HearLocalSignalsRadius, metres
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780221,  1, True),   -- Stuck: stays where it is placed
  (78780221, 19, False),  -- Attackable: no
  (78780221, 98, True);   -- Invincible: belt and braces
-- Mutation colour. Creature_Networking.cs:259 treats a PaletteTemplate whose high
-- byte is 0x04 as a full DAT palette DID and overlays it, which is exactly what a
-- bred mutation looks like. 0x04001155 was rolled by this server's own breeding code.
DELETE FROM `weenie_properties_int` WHERE `object_Id` = 78780221 AND `type` = 3;
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES (78780221, 3, 67113301);  -- 0x04001155

-- ------------------------------------------------------------------------------------
-- 78780222  Ursuin, Unregistered
--     template 7990 - Field Ursuin
-- ------------------------------------------------------------------------------------
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780222, 'annex-ward-ursuin', 10, NOW());

INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`)
  SELECT 78780222,`type`,`value` FROM `weenie_properties_int` WHERE `object_Id` = 7990;
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`)
  SELECT 78780222,`type`,`value` FROM `weenie_properties_bool` WHERE `object_Id` = 7990;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`)
  SELECT 78780222,`type`,`value` FROM `weenie_properties_float` WHERE `object_Id` = 7990;
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`)
  SELECT 78780222,`type`,`value` FROM `weenie_properties_string` WHERE `object_Id` = 7990;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`)
  SELECT 78780222,`type`,`value` FROM `weenie_properties_d_i_d` WHERE `object_Id` = 7990;
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`)
  SELECT 78780222,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM `weenie_properties_attribute` WHERE `object_Id` = 7990;
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`)
  SELECT 78780222,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 7990;
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`)
  SELECT 78780222,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM `weenie_properties_skill` WHERE `object_Id` = 7990;
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`)
  SELECT 78780222,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM `weenie_properties_body_part` WHERE `object_Id` = 7990;
INSERT INTO `weenie_properties_palette` (`object_Id`,`sub_Palette_Id`,`offset`,`length`)
  SELECT 78780222,`sub_Palette_Id`,`offset`,`length` FROM `weenie_properties_palette` WHERE `object_Id` = 7990;
INSERT INTO `weenie_properties_texture_map` (`object_Id`,`index`,`old_Id`,`new_Id`)
  SELECT 78780222,`index`,`old_Id`,`new_Id` FROM `weenie_properties_texture_map` WHERE `object_Id` = 7990;
INSERT INTO `weenie_properties_anim_part` (`object_Id`,`index`,`animation_Id`)
  SELECT 78780222,`index`,`animation_Id` FROM `weenie_properties_anim_part` WHERE `object_Id` = 7990;

UPDATE `weenie_properties_string` SET `value` = 'Ursuin, Unregistered' WHERE `object_Id` = 78780222 AND `type` = 1;
-- Creature.cs:153  IsNPC => !Attackable && TargetingTactic == None
DELETE FROM `weenie_properties_int`  WHERE `object_Id` = 78780222 AND `type` IN (67, 68, 16, 95, 133, 134, 290, 291);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780222 AND `type` IN (1, 19, 98);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780222, 16, 32), -- ItemUseable = Remote (clickable for dialogue)
  (78780222, 95, 8), -- RadarBlipColor = NPC
  (78780222, 133, 4), -- ShowableOnRadar
  (78780222, 134, 16), -- PlayerKillerStatus = RubberGlue
  (78780222, 290, 1), -- HearLocalSignals - takes part in the feud
  (78780222, 291, 60); -- HearLocalSignalsRadius, metres
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780222,  1, True),   -- Stuck: stays where it is placed
  (78780222, 19, False),  -- Attackable: no
  (78780222, 98, True);   -- Invincible: belt and braces
-- Mutation colour. Creature_Networking.cs:259 treats a PaletteTemplate whose high
-- byte is 0x04 as a full DAT palette DID and overlays it, which is exactly what a
-- bred mutation looks like. 0x04001465 was rolled by this server's own breeding code.
DELETE FROM `weenie_properties_int` WHERE `object_Id` = 78780222 AND `type` = 3;
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES (78780222, 3, 67114085);  -- 0x04001465

-- ------------------------------------------------------------------------------------
-- 78780223  Nine-Colour Shreth
--     template 4110 - Blood Shreth
-- ------------------------------------------------------------------------------------
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780223, 'annex-ward-shreth', 10, NOW());

INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`)
  SELECT 78780223,`type`,`value` FROM `weenie_properties_int` WHERE `object_Id` = 4110;
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`)
  SELECT 78780223,`type`,`value` FROM `weenie_properties_bool` WHERE `object_Id` = 4110;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`)
  SELECT 78780223,`type`,`value` FROM `weenie_properties_float` WHERE `object_Id` = 4110;
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`)
  SELECT 78780223,`type`,`value` FROM `weenie_properties_string` WHERE `object_Id` = 4110;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`)
  SELECT 78780223,`type`,`value` FROM `weenie_properties_d_i_d` WHERE `object_Id` = 4110;
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`)
  SELECT 78780223,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM `weenie_properties_attribute` WHERE `object_Id` = 4110;
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`)
  SELECT 78780223,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 4110;
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`)
  SELECT 78780223,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM `weenie_properties_skill` WHERE `object_Id` = 4110;
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`)
  SELECT 78780223,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM `weenie_properties_body_part` WHERE `object_Id` = 4110;
INSERT INTO `weenie_properties_palette` (`object_Id`,`sub_Palette_Id`,`offset`,`length`)
  SELECT 78780223,`sub_Palette_Id`,`offset`,`length` FROM `weenie_properties_palette` WHERE `object_Id` = 4110;
INSERT INTO `weenie_properties_texture_map` (`object_Id`,`index`,`old_Id`,`new_Id`)
  SELECT 78780223,`index`,`old_Id`,`new_Id` FROM `weenie_properties_texture_map` WHERE `object_Id` = 4110;
INSERT INTO `weenie_properties_anim_part` (`object_Id`,`index`,`animation_Id`)
  SELECT 78780223,`index`,`animation_Id` FROM `weenie_properties_anim_part` WHERE `object_Id` = 4110;

UPDATE `weenie_properties_string` SET `value` = 'Nine-Colour Shreth' WHERE `object_Id` = 78780223 AND `type` = 1;
-- Creature.cs:153  IsNPC => !Attackable && TargetingTactic == None
DELETE FROM `weenie_properties_int`  WHERE `object_Id` = 78780223 AND `type` IN (67, 68, 16, 95, 133, 134, 290, 291);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780223 AND `type` IN (1, 19, 98);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780223, 16, 32), -- ItemUseable = Remote (clickable for dialogue)
  (78780223, 95, 8), -- RadarBlipColor = NPC
  (78780223, 133, 4), -- ShowableOnRadar
  (78780223, 134, 16), -- PlayerKillerStatus = RubberGlue
  (78780223, 290, 1), -- HearLocalSignals - takes part in the feud
  (78780223, 291, 60); -- HearLocalSignalsRadius, metres
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780223,  1, True),   -- Stuck: stays where it is placed
  (78780223, 19, False),  -- Attackable: no
  (78780223, 98, True);   -- Invincible: belt and braces
-- Mutation colour. Creature_Networking.cs:259 treats a PaletteTemplate whose high
-- byte is 0x04 as a full DAT palette DID and overlays it, which is exactly what a
-- bred mutation looks like. 0x04001972 was rolled by this server's own breeding code.
DELETE FROM `weenie_properties_int` WHERE `object_Id` = 78780223 AND `type` = 3;
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES (78780223, 3, 67115378);  -- 0x04001972

-- ------------------------------------------------------------------------------------
-- 78780224  Subject Twelve
--     template 7 - Drudge Skulker
-- ------------------------------------------------------------------------------------
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780224, 'annex-ward-subject12', 10, NOW());

INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`)
  SELECT 78780224,`type`,`value` FROM `weenie_properties_int` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`)
  SELECT 78780224,`type`,`value` FROM `weenie_properties_bool` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`)
  SELECT 78780224,`type`,`value` FROM `weenie_properties_float` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`)
  SELECT 78780224,`type`,`value` FROM `weenie_properties_string` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`)
  SELECT 78780224,`type`,`value` FROM `weenie_properties_d_i_d` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`)
  SELECT 78780224,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM `weenie_properties_attribute` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`)
  SELECT 78780224,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`)
  SELECT 78780224,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM `weenie_properties_skill` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`)
  SELECT 78780224,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM `weenie_properties_body_part` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_palette` (`object_Id`,`sub_Palette_Id`,`offset`,`length`)
  SELECT 78780224,`sub_Palette_Id`,`offset`,`length` FROM `weenie_properties_palette` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_texture_map` (`object_Id`,`index`,`old_Id`,`new_Id`)
  SELECT 78780224,`index`,`old_Id`,`new_Id` FROM `weenie_properties_texture_map` WHERE `object_Id` = 7;
INSERT INTO `weenie_properties_anim_part` (`object_Id`,`index`,`animation_Id`)
  SELECT 78780224,`index`,`animation_Id` FROM `weenie_properties_anim_part` WHERE `object_Id` = 7;

UPDATE `weenie_properties_string` SET `value` = 'Subject Twelve' WHERE `object_Id` = 78780224 AND `type` = 1;
-- Creature.cs:153  IsNPC => !Attackable && TargetingTactic == None
DELETE FROM `weenie_properties_int`  WHERE `object_Id` = 78780224 AND `type` IN (67, 68, 16, 95, 133, 134, 290, 291);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780224 AND `type` IN (1, 19, 98);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780224, 16, 32), -- ItemUseable = Remote (clickable for dialogue)
  (78780224, 95, 8), -- RadarBlipColor = NPC
  (78780224, 133, 4), -- ShowableOnRadar
  (78780224, 134, 16), -- PlayerKillerStatus = RubberGlue
  (78780224, 290, 1), -- HearLocalSignals - takes part in the feud
  (78780224, 291, 60); -- HearLocalSignalsRadius, metres
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780224,  1, True),   -- Stuck: stays where it is placed
  (78780224, 19, False),  -- Attackable: no
  (78780224, 98, True);   -- Invincible: belt and braces
-- Mutation colour. Creature_Networking.cs:259 treats a PaletteTemplate whose high
-- byte is 0x04 as a full DAT palette DID and overlays it, which is exactly what a
-- bred mutation looks like. 0x04001D09 was rolled by this server's own breeding code.
DELETE FROM `weenie_properties_int` WHERE `object_Id` = 78780224 AND `type` = 3;
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES (78780224, 3, 67116297);  -- 0x04001D09

-- =====================================================================================
-- PART 3  Click dialogue (EmoteCategory.Use = 7, EmoteType.Tell = 10, private to the
--         player who clicked). Delays are seconds before that line is spoken.
-- =====================================================================================

-- Fenwick: the whole tutorial, in the order the Content Guide lists it.
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780200, 7, 1, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Whoa. Whoa. Okay. Yes. This is the place. Yes, the Professor did this.'),
  (@e, 1, 10, 4, 0, 'No, I don''t know why the Ursuin is pink. I have stopped needing to know why the Ursuin is pink.'),
  (@e, 2, 10, 4, 0, 'Professor Ruggan is a genius. I want that on the record before I say anything else.'),
  (@e, 3, 10, 4, 0, 'He said, and I quote, ''let''s see what happens.'' Something always happens. That is the problem with the sentence.'),
  (@e, 4, 10, 4, 0, 'Rules are on the sign. The sign is on the floor. The floor is where the Professor put it.'),
  (@e, 5, 10, 4, 0, 'So I''ll just tell you myself. Bring a pair. One male, one female. The essence panel says which one you have.'),
  (@e, 6, 10, 4, 0, 'Summon them both in the room with the lights. Same room. Then the two of you dance, within five seconds of each other.'),
  (@e, 7, 10, 4, 0, 'A stud gets ten breedings a day. A dam needs four hours between litters. Both numbers are on the panels.'),
  (@e, 8, 10, 4, 0, 'The baby goes to the dam''s owner. Always. Settle up before you dance, not after.'),
  (@e, 9, 10, 4, 0, 'About one in twenty comes out changed. New colour, permanent gain. Its spirit stands up and the two parents have to put it down. It cannot touch you.'),
  (@e, 10, 10, 4, 0, 'Babies come out small and useless. Three hundred kills at their tier or better and they''re grown.'),
  (@e, 11, 10, 4, 0, 'First one to summon it owns it forever. Trade it before you summon it, not after.'),
  (@e, 12, 10, 4, 0, 'Shinies don''t breed. Don''t ask me for an exception. I am an intern.'),
  (@e, 13, 10, 4, 0, 'That''s everything. Don''t feed them. Don''t pet them. And do not tell the left side what the right side said.');

-- Ivo: what is on the list and what it costs you.
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780201, 7, 1, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Three things on the list. Two of them are the same thing at different stages.'),
  (@e, 1, 10, 3.5, 0, 'The tailoring kit takes the look off one pet and puts it on another. The first one does not survive that. The price reflects it.'),
  (@e, 2, 10, 3.5, 0, 'The last one is for people who have made a decision. I don''t ask which decision.'),
  (@e, 3, 10, 3.5, 0, 'Buy off the list. I don''t haggle and I don''t explain the Professor.');

-- Mrs. Ruggan: four questions, none of them answerable.
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780204, 7, 1, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'Where is he.'),
  (@e, 1, 10, 3, 0, 'He told me this was a filing annex.'),
  (@e, 2, 10, 3, 0, 'There is a pink Ursuin in my husband''s filing annex.'),
  (@e, 3, 10, 3, 0, 'When you see him, tell him the lens order is still not delivered. He will know. He will pretend not to.');

-- Bexley: prim, and entirely about the other wing.
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780210, 7, 1, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'The Registry is open to inspection. Quietly.'),
  (@e, 1, 10, 3.5, 0, 'Every creature on this side is documented. Species, line, generation, colour of record.'),
  (@e, 2, 10, 3.5, 0, 'Colour OF RECORD. Write that part down.'),
  (@e, 3, 10, 3.5, 0, 'You will find no such documentation across the hall. You will find enthusiasm.');

-- Splotch: recruitment pitch.
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780220, 7, 1, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 10, 0, 0, 'First one. That''s me. Before me there was brown.'),
  (@e, 1, 10, 3.5, 0, 'The Professor wrote ''unexpected'' in his notes and then he wrote it again, underlined.'),
  (@e, 2, 10, 3.5, 0, 'Breed something over there on that floor and if you''re lucky it comes out looking like one of us.'),
  (@e, 3, 10, 3.5, 0, 'Then bring it here. We''ll know it.');

-- =====================================================================================
-- PART 4  The feud engine.
--
--   EmoteType.LocalSignal (88) calls Landblock.EmitSignal, which fires
--   EmoteCategory.ReceiveLocalSignal (37) on every object inside
--   HearLocalSignalsRadius whose `quest` matches the signal name.
--   (Landblock.cs:1487, EmoteManager.cs:4248)
--
--   So an opener on Bexley's heartbeat becomes an answer from Splotch three seconds
--   later, then a groan from Fenwick, then a chorus from whichever wing won. Six
--   openers at probability 0.03 on a 5s heartbeat is roughly one exchange every
--   three minutes per speaker.
-- =====================================================================================

-- ---- Openers (HeartBeat: say a line, then signal across the hall) ----

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780210, 5, 0.03, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 0, 0, 'A Browerk is brown. That is what the word means. That is the entire word.'),
  (@e, 1, 88, 0, 0, 'annex_feud_a1');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780210, 5, 0.03, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 0, 0, 'Every creature in this lounge has papers. Every creature in that ward has... a situation.'),
  (@e, 1, 88, 0, 0, 'annex_feud_a2');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780210, 5, 0.03, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 0, 0, 'Bloodline. Conformation. Restraint. Three concepts, none of them represented over there.'),
  (@e, 1, 88, 0, 0, 'annex_feud_a4');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780210, 5, 0.03, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 0, 0, 'I have been asked to stop using the phrase ''genetic vandalism.'' I have not agreed to stop.'),
  (@e, 1, 88, 0, 0, 'annex_feud_a6');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780220, 5, 0.03, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 0, 0, 'They call it purebred. I call it beige with a certificate.'),
  (@e, 1, 88, 0, 0, 'annex_feud_a3');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780220, 5, 0.03, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 0, 0, 'The Professor called me an accident. Then he hung my portrait in his office.'),
  (@e, 1, 88, 0, 0, 'annex_feud_a5');

-- ---- Answers, groans and choruses (ReceiveLocalSignal) ----

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780220, 37, 1, 'annex_feud_a1');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3, 0, 'It means BORING. Say the quiet part, Bexley.'),
  (@e, 1, 88, 0, 0, 'annex_feud_b1');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780220, 37, 1, 'annex_feud_a2');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3, 0, 'Papers? I have PALETTES.'),
  (@e, 1, 88, 0, 0, 'annex_feud_b2');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780220, 37, 1, 'annex_feud_a4');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3, 0, 'We don''t have a bloodline. We have a RANGE.'),
  (@e, 1, 88, 0, 0, 'annex_feud_b2');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780220, 37, 1, 'annex_feud_a6');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3, 0, 'Ask them what colour they''ll be next year. Go on. Ask them.'),
  (@e, 1, 88, 0, 0, 'annex_feud_b1');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780210, 37, 1, 'annex_feud_a3');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3, 0, 'We do not acknowledge the ward.'),
  (@e, 1, 88, 0, 0, 'annex_feud_b3');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780220, 37, 1, 'annex_feud_b3');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3, 0, 'He acknowledged us. Write that down.'),
  (@e, 1, 88, 0, 0, 'annex_feud_b1');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780210, 37, 1, 'annex_feud_a5');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3.5, 0, 'That is not a portrait. That is a case file.'),
  (@e, 1, 88, 0, 0, 'annex_feud_b5');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780200, 37, 0.5, 'annex_feud_b1');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 4.5, 0, 'Please. Both of you. There are customers.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780200, 37, 0.6, 'annex_feud_b5');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 4.5, 0, 'It''s a portrait. I framed it. I was told to frame it.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780203, 37, 0.25, 'annex_feud_b1');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 5, 0, '...I''m just here for the music.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780221, 37, 0.3, 'annex_feud_b1');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 4, 0, 'HA.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780222, 37, 0.3, 'annex_feud_b1');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 4, 0, 'Say it louder, Splotch.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780223, 37, 0.3, 'annex_feud_b1');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 4, 0, 'That''s the one. That''s the good one.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780224, 37, 0.3, 'annex_feud_b1');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 4, 0, 'Nobody over there has a nickname. Nobody.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780211, 37, 0.3, 'annex_feud_b2');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 4, 0, 'Must he shout.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780212, 37, 0.3, 'annex_feud_b2');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 4, 0, 'One does not respond. One simply files.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780213, 37, 0.3, 'annex_feud_b2');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 4, 0, 'My papers are pending. Pending papers are still papers.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780214, 37, 0.3, 'annex_feud_b2');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 4, 0, 'I have a lineage chart. It is very long.');

-- ---- Idle lines that belong to nobody's argument ----

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780200, 5, 0.02, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 0, 0, 'Mrs. Ruggan sent another letter. I said he was in the field. He is under a desk.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780200, 5, 0.02, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 0, 0, 'There is a sign. There was a sign.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780200, 5, 0.02, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 0, 0, 'Nine hundred essences catalogued. Nobody asks about those.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780202, 5, 0.04, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 0, 0, 'You. Yes. Dance. That is the entire ritual. I did not design it.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780202, 5, 0.04, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 0, 0, 'Everyone dances here. Even the Registrar. Once. He does not discuss it.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780202, 5, 0.04, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 0, 0, 'Both of you. Together. Now.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780203, 5, 0.02, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 0, 0, 'Both sides invite me to things. Neither side wants me there.');

INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780204, 5, 0.03, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 0, 0, 'Where IS he.');

-- =====================================================================================
-- PART 5  Placing them
--
-- These are NOT spawned by this file, on purpose. The Seedy Motel is landblock 0x013A
-- (314 decimal), used through variation 3 - that is where the portal in
-- 2026-07-26-00-Seedy-Motel-Portal.sql drops players (cell 0x013A02AE, variation_Id 3),
-- and breeding now defaults to landblock 0x013A variant 3 (pet_breeding_allowed_landblock /
-- pet_breeding_allowed_variant). 0x016C is the Marketplace, not the motel. The motel is a
-- large indoor block and this script has no way to know which of its cells are walkable
-- rooms, so any coordinates written here would be a guess. Place them in-game instead,
-- which writes the landblock_instance rows for you:
--
--   1. @showprops                  confirm pet_breeding_allowed_landblock is 314 (0x013A)
--                                  and pet_breeding_allowed_variant is 3. If a stored shard
--                                  value still says 364 (0x016C), fix it with @modifylong;
--                                  stored values override the code default.
--   2. Stand in the intended ritual room and run @breed-debug. Note the Cell=0x........
--      value and walk the whole room checking it does not change. If it changes, the
--      room is more than one landcell and two players on opposite sides of it will fail
--      to breed with no error. Pick a different room.
--   3. Walk to each mark and run @createinst <wcid>. Use @nudge and @rotate to adjust;
--      both write straight back to landblock_instance.
--   4. @export-sql <landblock> when you are happy, and commit the result.
--
--   The Drop            @createinst 78780200   Fenwick, Kennel Intern
--   Quartermaster nook  @createinst 78780201   Ivo, Ruggan's Quartermaster
--   Ritual Floor        @createinst 78780202   DJ Skulk
--   Ritual Floor        @createinst 78780203   Gary
--   The Drop (rare)     @createinst 78780204   Mrs. Ruggan
--   Registry wing       @createinst 78780210   Bexley, Keeper of the Registry
--   Registry wing       @createinst 78780211   Registered Browerk, Champion Line
--   Registry wing       @createinst 78780212   Certified Shreth, Third Generation
--   Registry wing       @createinst 78780213   Pedigreed Ursuin (Papers Pending)
--   Registry wing       @createinst 78780214   Drudge Skulker of Record
--   Ward wing           @createinst 78780220   Splotch
--   Ward wing           @createinst 78780221   The Teal Incident
--   Ward wing           @createinst 78780222   Ursuin, Unregistered
--   Ward wing           @createinst 78780223   Nine-Colour Shreth
--   Ward wing           @createinst 78780224   Subject Twelve
--
-- Two placement rules that come from the mechanics, not from taste:
--   * Keep the two wings OUT of the ritual room's landcell. Anything summoned in that
--     cell is a breeding candidate.
--   * Keep Bexley and Splotch within 60m of each other and of Fenwick, or the feud
--     chains stop resolving. 60m is the HearLocalSignalsRadius set in part 2; raise it
--     on all of them together if the wings end up further apart than that.
--
-- If you would rather script the placement, the shape is below. guid must be unique and
-- inside this landblock's static range, 0x7013A000-0x7013AFFF. No SQL in this repository
-- places anything in that range today, but the live shard may (check
-- SELECT MAX(guid) FROM landblock_instance WHERE guid BETWEEN 0x7013A000 AND 0x7013AFFF
-- before picking), so start at 0x7013A300 and go up. Coordinates are placeholders; the cell
-- is the portal's arrival cell. variation_Id must be 3 so the NPCs exist in the same
-- instanced copy of the motel the portal delivers players to. Do not write the `landblock`
-- column - it is a generated column derived from obj_Cell_Id.
--
--   INSERT INTO `landblock_instance`
--     (`guid`,`weenie_Class_Id`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,
--      `angles_W`,`angles_X`,`angles_Y`,`angles_Z`,`is_Link_Child`,`last_Modified`,`variation_Id`)
--   VALUES
--     (0x7013A300, 78780200, 0x013A02AE, 9.5, -49.8, 0.0, 1, 0, 0, 0, False, NOW(), 3);
-- =====================================================================================

-- Ruggan's Annex: 15 NPCs created.
