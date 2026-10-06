-- =====================================================================================
-- Dressing Room - attendant NPC weenie
--
-- Creates one NPC, WCID 78780500: Aldous, Dressing Room Attendant. Using him offers to lock
-- the armour and clothing the player is wearing in as their look (the pieces are destroyed
-- and a pyreal fee is charged). The behaviour is server code (ACE.Server.Entity.DressingRoom),
-- switched on by PropertyBool.DressingRoomAttendant (53000) below - there are no emotes.
--
-- The feature itself is OFF until an admin runs:  /modifybool dressing_room_enabled true
-- While it is off the attendant only says the dressing room is closed.
--
-- This file only CREATES the weenie. It does not place him: spawn one with
--   /create 78780500
-- for testing, and place him properly once a home is chosen.
--
-- Cloned from 42720 (Ealdred, a human male barber NPC that already renders correctly) so the
-- model, motion table, sounds and body parts are known good. His emotes are NOT copied, so
-- he does not open the barber window.
--
-- All text is 7-bit ASCII per CLAUDE.md.
-- Re-runnable: the DELETE below cascades to every weenie_properties_* table.
-- Target database: ace_world
-- =====================================================================================

DELETE FROM `weenie` WHERE `class_Id` = 78780500;

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780500, 'dressing-room-attendant', 10, NOW());

INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`)
  SELECT 78780500,`type`,`value` FROM `weenie_properties_int` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`)
  SELECT 78780500,`type`,`value` FROM `weenie_properties_bool` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`)
  SELECT 78780500,`type`,`value` FROM `weenie_properties_float` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`)
  SELECT 78780500,`type`,`value` FROM `weenie_properties_string` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_create_list` (`object_Id`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`)
  SELECT 78780500,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond` FROM `weenie_properties_create_list` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`)
  SELECT 78780500,`type`,`value` FROM `weenie_properties_d_i_d` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`)
  SELECT 78780500,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent` FROM `weenie_properties_attribute` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`)
  SELECT 78780500,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level` FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`)
  SELECT 78780500,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time` FROM `weenie_properties_skill` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`)
  SELECT 78780500,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b` FROM `weenie_properties_body_part` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_palette` (`object_Id`,`sub_Palette_Id`,`offset`,`length`)
  SELECT 78780500,`sub_Palette_Id`,`offset`,`length` FROM `weenie_properties_palette` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_texture_map` (`object_Id`,`index`,`old_Id`,`new_Id`)
  SELECT 78780500,`index`,`old_Id`,`new_Id` FROM `weenie_properties_texture_map` WHERE `object_Id` = 42720;
INSERT INTO `weenie_properties_anim_part` (`object_Id`,`index`,`animation_Id`)
  SELECT 78780500,`index`,`animation_Id` FROM `weenie_properties_anim_part` WHERE `object_Id` = 42720;

UPDATE `weenie_properties_string` SET `value` = 'Aldous' WHERE `object_Id` = 78780500 AND `type` = 1;
UPDATE `weenie_properties_string` SET `value` = 'Dressing Room Attendant' WHERE `object_Id` = 78780500 AND `type` = 5;

-- Creature.cs  IsNPC => !Attackable && TargetingTactic == None
DELETE FROM `weenie_properties_int`  WHERE `object_Id` = 78780500 AND `type` IN (67, 68, 16, 95, 133, 134);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780500 AND `type` IN (1, 19, 98, 53000);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780500, 16, 32),  -- ItemUseable = Remote (can be used from a distance, like any NPC)
  (78780500, 95, 8),   -- RadarBlipColor = NPC
  (78780500, 133, 4),  -- ShowableOnRadar
  (78780500, 134, 16); -- PlayerKillerStatus = RubberGlue
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780500,     1, True),   -- Stuck: stays where he is placed
  (78780500,    19, False),  -- Attackable: no
  (78780500,    98, True),   -- Invincible: belt and braces
  (78780500, 53000, True);   -- DressingRoomAttendant: using him runs DressingRoom.HandleUse
