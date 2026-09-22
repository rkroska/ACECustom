/* =====================================================================================
   Pet Breeding / Ruggan's Annex - PROD BUNDLE for ace_world
   Exported 2026-09-22 from the test server by deploy/export_annex_bundle.py.
   Re-run that script on test to refresh this file; never hand-edit it.

   CREATES 38 weenies, copied row for row from test:
     78780200-78780299  (37)
     98760388  (1)
   Left out as test-only: Baby Candidate B (78780234), Baby Candidate C (78780235), Scene Tester (78780241).
   PLACES them: see the PLACEMENTS section. Guids are fresh on the target, never copied from test.

   ONE TRANSACTION. Run it so an error stops before COMMIT (batch mode stops at the first error;
   never add --force):
       mysql ace_world < deploy/Annex-Prod-Bundle.sql
   In Workbench, select ace_world first and watch the output: Workbench keeps going after errors.
   Re-runnable: everything it owns is deleted first.

   AFTER IT (server running the new build):
     @modifylong pet_breeding_allowed_landblock 262      (0x0106)
     @modifylong pet_breeding_allowed_variant 2
     @clearcache, then @reload-landblock in the annex (or restart)
   Then read the V1-V4 results at the end.
   All text is 7-bit ASCII with LF line endings, per CLAUDE.md.
   ===================================================================================== */

USE `ace_world`;  -- whichever schema Workbench has selected, this runs against the world database

SET @__old_safe_updates = @@SQL_SAFE_UPDATES;
SET SQL_SAFE_UPDATES = 0;
START TRANSACTION;

/* ---- WEENIES ---------------------------------------------------------------------- */
-- Clean slate: the weenie_properties_* FKs cascade, so this clears every property row too.
DELETE FROM `weenie` WHERE `class_Id` IN (78780200,78780201,78780202,78780203,78780204,78780206,78780210,78780211,78780212,78780213,78780214,78780215,78780220,78780221,78780222,78780223,78780224,78780225,78780230,78780231,78780232,78780233,78780240,78780250,78780251,78780252,78780253,78780254,78780255,78780257,78780258,78780259,78780260,78780261,78780262,78780263,78780264,98760388);

-- 78780200  Fenwick, Kennel Intern
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780200, 'annex-fenwick', 10, NOW());
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) VALUES
  (78780200, 1, 70, 0, 0),
  (78780200, 2, 70, 0, 0),
  (78780200, 3, 60, 0, 0),
  (78780200, 4, 65, 0, 0),
  (78780200, 5, 50, 0, 0),
  (78780200, 6, 50, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) VALUES
  (78780200, 1, 75, 0, 0, 110),
  (78780200, 3, 110, 0, 0, 180),
  (78780200, 5, 55, 0, 0, 105);
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780200, 8, True),
  (78780200, 12, True),
  (78780200, 13, False),
  (78780200, 14, True),
  (78780200, 41, True),
  (78780200, 42, True),
  (78780200, 52, True),
  (78780200, 1, True),
  (78780200, 19, False),
  (78780200, 98, True);
INSERT INTO `weenie_properties_create_list` (`object_Id`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`) VALUES
  (78780200, 2, 10696, 1, 4, 0.5, False),
  (78780200, 2, 2587, 1, 2, 0.8182, False),
  (78780200, 2, 127, 1, 9, 0.9821, False),
  (78780200, 2, 132, 1, 4, 1.0, False),
  (78780200, 2, 10697, 1, 8, 0.5, False);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780200, 1, 33554433),
  (78780200, 2, 150994945),
  (78780200, 3, 536870913),
  (78780200, 6, 67108990),
  (78780200, 8, 100667446),
  (78780200, 9, 83890497),
  (78780200, 10, 83890562),
  (78780200, 11, 83890633),
  (78780200, 15, 67117023),
  (78780200, 16, 67110065),
  (78780200, 17, 67109559);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780200, 54, 3.0);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780200, 1, 16),
  (78780200, 2, 31),
  (78780200, 6, -1),
  (78780200, 7, -1),
  (78780200, 25, 200),
  (78780200, 93, 6292504),
  (78780200, 113, 1),
  (78780200, 188, 1),
  (78780200, 16, 32),
  (78780200, 95, 8),
  (78780200, 133, 4),
  (78780200, 134, 16),
  (78780200, 290, 1),
  (78780200, 291, 60);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780200, 1, 'Fenwick, Kennel Intern'),
  (78780200, 5, 'Barber');
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780200, 7, 1.0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 10, 0.0, 0.0, NULL, 'Whoa. Whoa. Okay. Yes. This is the place. Yes, the Professor did this.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 10, 4.0, 0.0, NULL, 'No, I don''t know why the Ursuin is pink. I have stopped needing to know why the Ursuin is pink.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 2, 10, 4.0, 0.0, NULL, 'Professor Ruggan is a genius. I want that on the record before I say anything else.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 3, 10, 4.0, 0.0, NULL, 'He said, and I quote, ''let''s see what happens.'' Something always happens. That is the problem with the sentence.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 4, 10, 4.0, 0.0, NULL, 'Rules are on the sign. The sign is on the floor. The floor is where the Professor put it.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 5, 10, 4.0, 0.0, NULL, 'So I''ll just tell you myself. Bring a pair. One male, one female. The essence panel says which one you have.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 6, 10, 4.0, 0.0, NULL, 'Summon them both in the room with the lights. Same room. Then the two of you dance, within five seconds of each other.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 7, 10, 4.0, 0.0, NULL, 'A stud gets ten breedings a day. A dam needs four hours between litters. Both numbers are on the panels.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 8, 10, 4.0, 0.0, NULL, 'The baby goes to the dam''s owner. Always. Settle up before you dance, not after.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 9, 10, 4.0, 0.0, NULL, 'About one in twenty comes out changed. New colour, permanent gain. Its spirit stands up and the two parents have to put it down. It cannot touch you.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 10, 10, 4.0, 0.0, NULL, 'Babies come out small and useless. Three hundred kills at their tier or better and they''re grown.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 11, 10, 4.0, 0.0, NULL, 'First one to summon it owns it forever. Trade it before you summon it, not after.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 12, 10, 4.0, 0.0, NULL, 'Shinies don''t breed. Don''t ask me for an exception. I am an intern.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 13, 10, 4.0, 0.0, NULL, 'That''s everything. Don''t feed them. Don''t pet them. And do not tell the left side what the right side said.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780200, 37, 0.5, NULL, NULL, NULL, 'annex_feud_b1', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 4.5, 0.0, NULL, 'Please. Both of you. There are customers.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780200, 37, 0.6, NULL, NULL, NULL, 'annex_feud_b5', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 4.5, 0.0, NULL, 'It''s a portrait. I framed it. I was told to frame it.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780200, 5, 0.0067, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 0.0, 0.0, NULL, 'Mrs. Ruggan sent another letter. I said he was in the field. He is under a desk.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780200, 5, 0.0133, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 0.0, 0.0, NULL, 'There is a sign. There was a sign.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780200, 5, 0.02, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 0.0, 0.0, NULL, 'Nine hundred essences catalogued. Nobody asks about those.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

-- 78780201  Ivo, Ruggan's Quartermaster
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780201, 'annex-ivo', 12, NOW());
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) VALUES
  (78780201, 1, 220, 0, 0),
  (78780201, 2, 270, 0, 0),
  (78780201, 3, 200, 0, 0),
  (78780201, 4, 200, 0, 0),
  (78780201, 5, 290, 0, 0),
  (78780201, 6, 290, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) VALUES
  (78780201, 1, 196, 0, 0, 331),
  (78780201, 3, 196, 0, 0, 466),
  (78780201, 5, 196, 0, 0, 486);
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`) VALUES
  (78780201, 0, 4, 0, 0.0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0),
  (78780201, 1, 4, 0, 0.0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0),
  (78780201, 2, 4, 0, 0.0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0),
  (78780201, 3, 4, 0, 0.0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0),
  (78780201, 4, 4, 0, 0.0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0),
  (78780201, 5, 4, 2, 0.75, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0),
  (78780201, 6, 4, 0, 0.0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18),
  (78780201, 7, 4, 0, 0.0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6),
  (78780201, 8, 4, 2, 0.75, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22);
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780201, 39, True),
  (78780201, 1, True),
  (78780201, 19, False),
  (78780201, 98, True);
INSERT INTO `weenie_properties_create_list` (`object_Id`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`) VALUES
  (78780201, 2, 25641, 0, 4, 0.0, False),
  (78780201, 2, 25645, 0, 4, 0.0, False),
  (78780201, 2, 25651, 0, 4, 0.0, False),
  (78780201, 2, 25661, 0, 4, 0.0, False),
  (78780201, 2, 130, 0, 88, 0.4, False),
  (78780201, 4, 78780258, -1, 0, 0.0, False),
  (78780201, 4, 78780259, -1, 0, 0.0, False),
  (78780201, 4, 78780250, -1, 0, 0.0, False),
  (78780201, 4, 78780251, -1, 0, 0.0, False),
  (78780201, 4, 78780252, -1, 0, 0.0, False),
  (78780201, 4, 78780253, -1, 0, 0.0, False),
  (78780201, 4, 78780254, -1, 0, 0.0, False),
  (78780201, 4, 78780255, -1, 0, 0.0, False),
  (78780201, 4, 78780257, -1, 0, 0.0, False),
  (78780201, 4, 78780262, -1, 0, 0.0, False),
  (78780201, 4, 78780263, -1, 0, 0.0, False);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780201, 1, 33554433),
  (78780201, 2, 150994945),
  (78780201, 3, 536870913),
  (78780201, 6, 67108990),
  (78780201, 8, 100667446),
  (78780201, 9, 83890483),
  (78780201, 10, 83890538),
  (78780201, 11, 83890617),
  (78780201, 15, 67117080),
  (78780201, 16, 67110062),
  (78780201, 17, 67109550),
  (78780201, 18, 16795650);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780201, 1, 5.0),
  (78780201, 2, 0.0),
  (78780201, 3, 0.16),
  (78780201, 4, 5.0),
  (78780201, 5, 1.0),
  (78780201, 11, 300.0),
  (78780201, 13, 0.9),
  (78780201, 14, 1.0),
  (78780201, 15, 1.1),
  (78780201, 16, 0.4),
  (78780201, 17, 0.4),
  (78780201, 18, 1.0),
  (78780201, 19, 0.6),
  (78780201, 37, 1.0),
  (78780201, 38, 1.0),
  (78780201, 54, 3.0),
  (78780201, 64, 1.0),
  (78780201, 65, 1.0),
  (78780201, 66, 1.0),
  (78780201, 67, 1.0),
  (78780201, 68, 1.0),
  (78780201, 69, 1.0),
  (78780201, 70, 1.0),
  (78780201, 71, 1.0),
  (78780201, 72, 1.0),
  (78780201, 73, 1.0),
  (78780201, 74, 1.0),
  (78780201, 75, 1.0),
  (78780201, 104, 10.0),
  (78780201, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780201, 1, 16),
  (78780201, 2, 31),
  (78780201, 6, -1),
  (78780201, 7, -1),
  (78780201, 25, 250),
  (78780201, 27, 0),
  (78780201, 93, 2098200),
  (78780201, 113, 1),
  (78780201, 126, 125),
  (78780201, 127, 125),
  (78780201, 188, 2),
  (78780201, 16, 32),
  (78780201, 95, 8),
  (78780201, 133, 4),
  (78780201, 134, 16),
  (78780201, 74, 0),
  (78780201, 75, 0),
  (78780201, 76, 1000000);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780201, 1, 'Ivo, Ruggan''s Quartermaster'),
  (78780201, 5, 'Stipend Vendor');
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780201, 7, 1.0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 10, 0.0, 0.0, NULL, 'Eleven things on the list. Most of them are smoke in a jar.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 10, 3.5, 0.0, NULL, 'The incense makes the next litter likelier to come out changed. Bigger jar, better odds. Both parents can wear it.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 2, 10, 3.5, 0.0, NULL, 'The draught grows a baby twice as fast. The catalyst makes a change come out loud. The offering makes the spirit easy to put down.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 3, 10, 3.5, 0.0, NULL, 'The serum re-rolls a colour on the spot. The stats stay where they were.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 4, 10, 3.5, 0.0, NULL, 'The tinctures make a pet more or less see-through, a tenth at a time. They do nothing for actual ghosts.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 5, 10, 3.5, 0.0, NULL, 'The tailoring kit takes the look off one pet and puts it on another. The first one does not survive that. The price reflects it.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 6, 10, 3.5, 0.0, NULL, 'The neutering kit is for people who have made a decision. I don''t ask which decision.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 7, 10, 3.5, 0.0, NULL, 'Buy off the list. I don''t haggle and I don''t explain the Professor.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

-- 78780202  DJ Skulk
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780202, 'annex-dj-skulk', 10, NOW());
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) VALUES
  (78780202, 1, 20, 0, 0),
  (78780202, 2, 30, 0, 0),
  (78780202, 3, 30, 0, 0),
  (78780202, 4, 25, 0, 0),
  (78780202, 5, 25, 0, 0),
  (78780202, 6, 15, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) VALUES
  (78780202, 1, 5, 0, 0, 20),
  (78780202, 3, 50, 0, 0, 80),
  (78780202, 5, 0, 0, 0, 15);
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`) VALUES
  (78780202, 0, 4, 0, 0.0, 3, 3, 3, 3, 2, 2, 3, 1, 0, 1, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0),
  (78780202, 1, 4, 0, 0.0, 7, 6, 7, 8, 4, 4, 7, 3, 0, 2, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0),
  (78780202, 2, 4, 0, 0.0, 7, 6, 7, 8, 4, 4, 7, 3, 0, 3, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0),
  (78780202, 3, 4, 0, 0.0, 5, 5, 5, 6, 3, 3, 5, 2, 0, 1, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0),
  (78780202, 4, 4, 0, 0.0, 7, 6, 7, 8, 4, 4, 7, 3, 0, 2, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0),
  (78780202, 5, 4, 2, 0.75, 5, 5, 5, 6, 3, 3, 5, 2, 0, 2, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0),
  (78780202, 6, 4, 0, 0.0, 5, 5, 5, 6, 3, 3, 5, 2, 0, 3, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18),
  (78780202, 7, 4, 0, 0.0, 5, 5, 5, 6, 3, 3, 5, 2, 0, 3, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6),
  (78780202, 8, 4, 3, 0.75, 5, 5, 5, 6, 3, 3, 5, 2, 0, 3, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22);
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780202, 12, True),
  (78780202, 13, False),
  (78780202, 41, True),
  (78780202, 1, True),
  (78780202, 19, False),
  (78780202, 98, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780202, 1, 33556445),
  (78780202, 2, 150994952),
  (78780202, 3, 536870919),
  (78780202, 4, 805306372),
  (78780202, 6, 67112812),
  (78780202, 7, 268435974),
  (78780202, 8, 100667445),
  (78780202, 22, 872415258),
  (78780202, 35, 453);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780202, 1, 2.0),
  (78780202, 2, 0.0),
  (78780202, 3, 0.067),
  (78780202, 4, 5.0),
  (78780202, 5, 1.0),
  (78780202, 12, 1.0),
  (78780202, 13, 0.9),
  (78780202, 14, 1.0),
  (78780202, 15, 1.1),
  (78780202, 16, 0.6),
  (78780202, 17, 0.6),
  (78780202, 18, 1.0),
  (78780202, 19, 0.36),
  (78780202, 31, 10.0),
  (78780202, 34, 1.0),
  (78780202, 36, 1.0),
  (78780202, 39, 0.95),
  (78780202, 64, 0.86),
  (78780202, 65, 0.75),
  (78780202, 66, 0.66),
  (78780202, 67, 1.42),
  (78780202, 68, 1.42),
  (78780202, 69, 0.75),
  (78780202, 70, 1.42),
  (78780202, 71, 1.0),
  (78780202, 72, 1.0),
  (78780202, 73, 1.0),
  (78780202, 74, 1.0),
  (78780202, 75, 1.0),
  (78780202, 104, 10.0),
  (78780202, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780202, 1, 16),
  (78780202, 2, 3),
  (78780202, 3, 48),
  (78780202, 6, -1),
  (78780202, 7, -1),
  (78780202, 25, 4),
  (78780202, 27, 0),
  (78780202, 40, 2),
  (78780202, 93, 2098200),
  (78780202, 101, 131),
  (78780202, 146, 45),
  (78780202, 16, 32),
  (78780202, 95, 8),
  (78780202, 133, 4),
  (78780202, 134, 16),
  (78780202, 290, 1),
  (78780202, 291, 60);
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`) VALUES
  (78780202, 1, 0, 3, 0, 5, 0, 432.91977126602785),
  (78780202, 4, 0, 3, 0, 5, 0, 432.91977126602785),
  (78780202, 5, 0, 3, 0, 5, 0, 432.91977126602785),
  (78780202, 6, 0, 3, 0, 5, 0, 432.91977126602785),
  (78780202, 7, 0, 3, 0, 15, 0, 432.91977126602785),
  (78780202, 9, 0, 3, 0, 5, 0, 432.91977126602785),
  (78780202, 10, 0, 3, 0, 5, 0, 432.91977126602785),
  (78780202, 11, 0, 3, 0, 5, 0, 432.91977126602785),
  (78780202, 13, 0, 3, 0, 5, 0, 432.91977126602785),
  (78780202, 15, 0, 3, 0, 5, 0, 432.91977126602785),
  (78780202, 24, 0, 2, 0, 40, 0, 432.91977126602785);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780202, 1, 'DJ Skulk');
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780202, 5, 0.8, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 5, 0.0, 0.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780202, 5, 1.0, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 5, 0.0, 0.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780202, 5, 0.8, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 5, 0.0, 0.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780202, 5, 1.0, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 5, 0.0, 0.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780202, 5, 0.8, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 5, 0.0, 0.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780202, 5, 1.0, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 5, 0.0, 0.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780202, 5, 0.0133, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 0.0, 0.0, NULL, 'You. Yes. Dance. That is the entire ritual. I did not design it.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780202, 5, 0.0267, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 0.0, 0.0, NULL, 'Everyone dances here. Even the Registrar. Once. He does not discuss it.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780202, 5, 0.04, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 0.0, 0.0, NULL, 'Both of you. Together. Now.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780202, 37, 1.0, NULL, NULL, NULL, 'pat_dj_4', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 4.5, 0.0, NULL, '...Wow.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

-- 78780203  Gary
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780203, 'annex-gary', 10, NOW());
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) VALUES
  (78780203, 1, 1800, 0, 0),
  (78780203, 2, 4000, 0, 0),
  (78780203, 3, 200, 0, 0),
  (78780203, 4, 50, 0, 0),
  (78780203, 5, 1000, 0, 0),
  (78780203, 6, 1000, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) VALUES
  (78780203, 1, 198000, 0, 0, 200000),
  (78780203, 3, 196000, 0, 0, 200000),
  (78780203, 5, 199000, 0, 0, 200000);
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`) VALUES
  (78780203, 0, 4, 0, 0.0, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 1, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0),
  (78780203, 1, 4, 0, 0.0, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 2, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0),
  (78780203, 2, 4, 0, 0.0, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 3, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0),
  (78780203, 3, 4, 0, 0.0, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 1, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0),
  (78780203, 4, 4, 0, 0.0, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 2, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0),
  (78780203, 5, 1, 50, 0.5, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 2, 0.0, 0.1, 0.0, 0.0, 0.1, 0.0, 0.0, 0.1, 0.0, 0.0, 0.1, 0.0),
  (78780203, 6, 4, 0, 0.0, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 3, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18),
  (78780203, 7, 4, 0, 0.0, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 3, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6),
  (78780203, 8, 4, 50, 0.5, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 3, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22),
  (78780203, 20, 1, 50, 0.5, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 2, 0.0, 0.1, 0.0, 0.0, 0.1, 0.0, 0.0, 0.1, 0.0, 0.0, 0.1, 0.0);
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780203, 6, True),
  (78780203, 11, False),
  (78780203, 12, True),
  (78780203, 13, False),
  (78780203, 50, True),
  (78780203, 1, True),
  (78780203, 19, False),
  (78780203, 98, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780203, 1, 33558882),
  (78780203, 2, 150995310),
  (78780203, 3, 536871095),
  (78780203, 4, 805306430),
  (78780203, 6, 67115354),
  (78780203, 7, 268436860),
  (78780203, 8, 100677029),
  (78780203, 22, 872415402),
  (78780203, 35, 32);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780203, 1, 5.0),
  (78780203, 2, 0.0),
  (78780203, 3, 1.0),
  (78780203, 4, 5.0),
  (78780203, 5, 2.0),
  (78780203, 12, 0.5),
  (78780203, 13, 1.0),
  (78780203, 14, 1.0),
  (78780203, 15, 1.2),
  (78780203, 16, 1.0),
  (78780203, 17, 0.9),
  (78780203, 18, 1.2),
  (78780203, 19, 1.0),
  (78780203, 31, 40.0),
  (78780203, 34, 1.1),
  (78780203, 36, 1.0),
  (78780203, 55, 100.0),
  (78780203, 64, 0.3),
  (78780203, 65, 0.5),
  (78780203, 66, 0.3),
  (78780203, 67, 0.35),
  (78780203, 68, 0.45),
  (78780203, 69, 0.05),
  (78780203, 70, 0.55),
  (78780203, 71, 1.0),
  (78780203, 72, 0.0),
  (78780203, 73, 1.0),
  (78780203, 74, 0.0),
  (78780203, 75, 1.0),
  (78780203, 80, 3.0),
  (78780203, 104, 10.0),
  (78780203, 122, 2.0),
  (78780203, 125, 0.1),
  (78780203, 127, 2.0),
  (78780203, 128, 1.0),
  (78780203, 151, 0.9),
  (78780203, 166, 0.3),
  (78780203, 39, 0.275);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780203, 1, 16),
  (78780203, 2, 75),
  (78780203, 3, 39),
  (78780203, 6, -1),
  (78780203, 7, -1),
  (78780203, 25, 999),
  (78780203, 27, 0),
  (78780203, 40, 2),
  (78780203, 93, 1032),
  (78780203, 146, 25000000),
  (78780203, 16, 32),
  (78780203, 95, 8),
  (78780203, 133, 4),
  (78780203, 134, 16),
  (78780203, 290, 1),
  (78780203, 291, 60);
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`) VALUES
  (78780203, 1, 0, 3, 0, 33, 0, 2088.5099122453316),
  (78780203, 2, 0, 3, 0, 350, 0, 2088.5099122453316),
  (78780203, 3, 0, 3, 0, 350, 0, 2088.5099122453316),
  (78780203, 4, 0, 3, 0, 0, 0, 2088.5099122453316),
  (78780203, 5, 0, 3, 0, 33, 0, 2088.5099122453316),
  (78780203, 6, 0, 3, 0, 17, 0, 2088.5099122453316),
  (78780203, 7, 0, 3, 0, 75, 0, 2088.5099122453316),
  (78780203, 9, 0, 3, 0, 33, 0, 2088.5099122453316),
  (78780203, 10, 0, 3, 0, 33, 0, 2088.5099122453316),
  (78780203, 11, 0, 3, 0, 33, 0, 2088.5099122453316),
  (78780203, 12, 0, 3, 0, 350, 0, 2088.5099122453316),
  (78780203, 13, 0, 3, 0, 33, 0, 2088.5099122453316),
  (78780203, 15, 0, 3, 0, 135, 0, 2088.5099122453316),
  (78780203, 20, 0, 3, 0, 0, 0, 2088.5099122453316),
  (78780203, 22, 0, 3, 0, 0, 0, 2088.5099122453316),
  (78780203, 24, 0, 3, 0, 0, 0, 2088.5099122453316),
  (78780203, 31, 0, 3, 0, 25, 0, 2088.5099122453316),
  (78780203, 32, 0, 3, 0, 25, 0, 2088.5099122453316),
  (78780203, 33, 0, 3, 0, 25, 0, 2088.5099122453316),
  (78780203, 34, 0, 3, 0, 25, 0, 2088.5099122453316);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780203, 1, 'Gary');
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780203, 37, 0.25, NULL, NULL, NULL, 'annex_feud_b1', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 5.0, 0.0, NULL, '...I''m just here for the music.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780203, 5, 0.02, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 0.0, 0.0, NULL, 'Both sides invite me to things. Neither side wants me there.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780203, 37, 1.0, NULL, NULL, NULL, 'pat_gary_3', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 4.0, 0.0, NULL, 'I''m just here for the music.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'pat_gary_4', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

-- 78780204  Mrs. Ruggan
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780204, 'annex-mrs-ruggan', 10, NOW());
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) VALUES
  (78780204, 1, 80, 0, 0),
  (78780204, 2, 90, 0, 0),
  (78780204, 3, 70, 0, 0),
  (78780204, 4, 70, 0, 0),
  (78780204, 5, 50, 0, 0),
  (78780204, 6, 60, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) VALUES
  (78780204, 1, 80, 0, 0, 125),
  (78780204, 3, 110, 0, 0, 200),
  (78780204, 5, 40, 0, 0, 100);
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`) VALUES
  (78780204, 0, 4, 0, 0.0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0),
  (78780204, 1, 4, 0, 0.0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0),
  (78780204, 2, 4, 0, 0.0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0),
  (78780204, 3, 4, 0, 0.0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0),
  (78780204, 4, 4, 0, 0.0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0),
  (78780204, 5, 4, 2, 0.75, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0),
  (78780204, 6, 4, 0, 0.0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18),
  (78780204, 7, 4, 0, 0.0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6),
  (78780204, 8, 4, 2, 0.75, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22);
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780204, 8, True),
  (78780204, 12, True),
  (78780204, 13, False),
  (78780204, 41, True),
  (78780204, 42, True),
  (78780204, 52, True),
  (78780204, 1, True),
  (78780204, 19, False),
  (78780204, 98, True);
INSERT INTO `weenie_properties_create_list` (`object_Id`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`) VALUES
  (78780204, 2, 8371, 1, 11, 0.3, False),
  (78780204, 2, 132, 1, 39, 0.9, False);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780204, 1, 33554510),
  (78780204, 2, 150994945),
  (78780204, 3, 536870914),
  (78780204, 4, 805306368),
  (78780204, 8, 100667446);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780204, 1, 5.0),
  (78780204, 2, 0.0),
  (78780204, 3, 0.16),
  (78780204, 4, 5.0),
  (78780204, 5, 1.0),
  (78780204, 13, 0.9),
  (78780204, 14, 1.0),
  (78780204, 15, 1.1),
  (78780204, 16, 0.4),
  (78780204, 17, 0.4),
  (78780204, 18, 1.0),
  (78780204, 19, 0.6),
  (78780204, 54, 3.0),
  (78780204, 64, 1.0),
  (78780204, 65, 1.0),
  (78780204, 66, 1.0),
  (78780204, 67, 1.0),
  (78780204, 68, 1.0),
  (78780204, 69, 1.0),
  (78780204, 70, 1.0),
  (78780204, 71, 1.0),
  (78780204, 72, 1.0),
  (78780204, 73, 1.0),
  (78780204, 74, 1.0),
  (78780204, 75, 1.0),
  (78780204, 104, 10.0),
  (78780204, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780204, 1, 16),
  (78780204, 2, 31),
  (78780204, 6, -1),
  (78780204, 7, -1),
  (78780204, 8, 120),
  (78780204, 25, 5),
  (78780204, 27, 0),
  (78780204, 93, 6292504),
  (78780204, 113, 2),
  (78780204, 146, 221),
  (78780204, 188, 3),
  (78780204, 16, 32),
  (78780204, 95, 8),
  (78780204, 133, 4),
  (78780204, 134, 16);
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`) VALUES
  (78780204, 6, 0, 2, 0, 1, 0, 0.0),
  (78780204, 7, 0, 2, 0, 1, 0, 0.0),
  (78780204, 45, 0, 2, 0, 1, 0, 0.0);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780204, 1, 'Mrs. Ruggan'),
  (78780204, 3, 'Female'),
  (78780204, 4, 'Sho'),
  (78780204, 5, 'Trophy Collector');
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780204, 7, 1.0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 10, 0.0, 0.0, NULL, 'Where is he.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 10, 3.0, 0.0, NULL, 'He told me this was a filing annex.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 2, 10, 3.0, 0.0, NULL, 'There is a drudge paternity dispute happening in my husband''s filing annex.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 3, 10, 3.0, 0.0, NULL, 'If you see the Professor, tell him I found the receipts for the incense. All of them.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780204, 5, 0.03, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 0.0, 0.0, NULL, 'Where IS he.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

-- 78780206  Fallen Sign
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780206, 'annex_fallen_sign', 1, NOW());
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780206, 1, True),
  (78780206, 12, True),
  (78780206, 13, True),
  (78780206, 22, False);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780206, 1, 33556890),
  (78780206, 8, 100668115);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780206, 54, 3.0);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780206, 1, 128),
  (78780206, 5, 9000),
  (78780206, 8, 1500),
  (78780206, 16, 1),
  (78780206, 19, 0),
  (78780206, 93, 1052);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780206, 1, 'Fallen Sign'),
  (78780206, 16, 'RUGGAN''S ANNEX - HOUSE RULES
1. Both pets in the same room. Both owners dance.
2. The baby goes to the dam''s owner. Settle up first.
3. Do not feed the Registry. Do not pet the Ward.
4. Shinies do not breed. Do not ask.
5. This is a filing annex.  - Prof. R.');

-- 78780210  Bexley, Keeper of the Registry
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780210, 'annex-bexley', 10, NOW());
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) VALUES
  (78780210, 1, 70, 0, 0),
  (78780210, 2, 70, 0, 0),
  (78780210, 3, 60, 0, 0),
  (78780210, 4, 65, 0, 0),
  (78780210, 5, 50, 0, 0),
  (78780210, 6, 50, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) VALUES
  (78780210, 1, 75, 0, 0, 110),
  (78780210, 3, 110, 0, 0, 180),
  (78780210, 5, 55, 0, 0, 105);
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780210, 8, True),
  (78780210, 12, True),
  (78780210, 13, False),
  (78780210, 14, True),
  (78780210, 41, True),
  (78780210, 42, True),
  (78780210, 52, True),
  (78780210, 1, True),
  (78780210, 19, False),
  (78780210, 98, True);
INSERT INTO `weenie_properties_create_list` (`object_Id`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`) VALUES
  (78780210, 2, 130, 1, 61, 0.1, False),
  (78780210, 2, 117, 1, 39, 0.9, False),
  (78780210, 2, 132, 1, 39, 0.9, False),
  (78780210, 2, 5588, 1, 39, 0.9, False);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780210, 1, 33554433),
  (78780210, 2, 150994945),
  (78780210, 3, 536870913),
  (78780210, 6, 67108990),
  (78780210, 8, 100667446),
  (78780210, 9, 83890497),
  (78780210, 10, 83890562),
  (78780210, 11, 83890633),
  (78780210, 15, 67117023),
  (78780210, 16, 67110065),
  (78780210, 17, 67109559);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780210, 54, 3.0);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780210, 1, 16),
  (78780210, 2, 31),
  (78780210, 6, -1),
  (78780210, 7, -1),
  (78780210, 25, 200),
  (78780210, 93, 6292504),
  (78780210, 113, 1),
  (78780210, 188, 1),
  (78780210, 16, 32),
  (78780210, 95, 8),
  (78780210, 133, 4),
  (78780210, 134, 16),
  (78780210, 290, 1),
  (78780210, 291, 60);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780210, 1, 'Bexley, Keeper of the Registry'),
  (78780210, 5, 'Barber');
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780210, 7, 1.0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 10, 0.0, 0.0, NULL, 'The Registry is open to inspection. Quietly.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 10, 3.5, 0.0, NULL, 'Every creature on this side is documented. Species, line, generation, colour of record.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 2, 10, 3.5, 0.0, NULL, 'Colour OF RECORD. Write that part down.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 3, 10, 3.5, 0.0, NULL, 'You will find no such documentation across the hall. You will find enthusiasm.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780210, 37, 1.0, NULL, NULL, NULL, 'annex_feud_a3', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 3.0, 0.0, NULL, 'We do not acknowledge the ward.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'annex_feud_b3', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780210, 37, 1.0, NULL, NULL, NULL, 'annex_feud_a5', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 3.5, 0.0, NULL, 'That is not a portrait. That is a case file.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'annex_feud_b5', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780210, 37, 1.0, NULL, NULL, NULL, 'pat_results_1', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 0.0, 0.0, NULL, 'Paternity results for room four. Mubb. You ARE the father.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'pat_results_2', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780210, 37, 1.0, NULL, NULL, NULL, 'pat_results_3', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 3.5, 0.0, NULL, 'The colour is a mutation. It happens in the best families. Not mine. But families.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'pat_results_4', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

-- 78780211  Registered Browerk, Champion Line
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780211, 'annex-registry-browerk', 10, NOW());
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) VALUES
  (78780211, 1, 1800, 0, 0),
  (78780211, 2, 4000, 0, 0),
  (78780211, 3, 200, 0, 0),
  (78780211, 4, 50, 0, 0),
  (78780211, 5, 1000, 0, 0),
  (78780211, 6, 1000, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) VALUES
  (78780211, 1, 198000, 0, 0, 200000),
  (78780211, 3, 196000, 0, 0, 200000),
  (78780211, 5, 199000, 0, 0, 200000);
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`) VALUES
  (78780211, 0, 4, 0, 0.0, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 1, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0),
  (78780211, 1, 4, 0, 0.0, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 2, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0),
  (78780211, 2, 4, 0, 0.0, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 3, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0),
  (78780211, 3, 4, 0, 0.0, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 1, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0),
  (78780211, 4, 4, 0, 0.0, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 2, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0),
  (78780211, 5, 1, 50, 0.5, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 2, 0.0, 0.1, 0.0, 0.0, 0.1, 0.0, 0.0, 0.1, 0.0, 0.0, 0.1, 0.0),
  (78780211, 6, 4, 0, 0.0, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 3, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18),
  (78780211, 7, 4, 0, 0.0, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 3, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6),
  (78780211, 8, 4, 50, 0.5, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 3, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22),
  (78780211, 20, 1, 50, 0.5, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 2, 0.0, 0.1, 0.0, 0.0, 0.1, 0.0, 0.0, 0.1, 0.0, 0.0, 0.1, 0.0);
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780211, 6, True),
  (78780211, 11, False),
  (78780211, 12, True),
  (78780211, 13, False),
  (78780211, 50, True),
  (78780211, 1, True),
  (78780211, 19, False),
  (78780211, 98, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780211, 1, 33558882),
  (78780211, 2, 150995310),
  (78780211, 3, 536871095),
  (78780211, 4, 805306430),
  (78780211, 6, 67115354),
  (78780211, 7, 268436860),
  (78780211, 8, 100677029),
  (78780211, 22, 872415402),
  (78780211, 35, 32);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780211, 1, 5.0),
  (78780211, 2, 0.0),
  (78780211, 3, 1.0),
  (78780211, 4, 5.0),
  (78780211, 5, 2.0),
  (78780211, 12, 0.5),
  (78780211, 13, 1.0),
  (78780211, 14, 1.0),
  (78780211, 15, 1.2),
  (78780211, 16, 1.0),
  (78780211, 17, 0.9),
  (78780211, 18, 1.2),
  (78780211, 19, 1.0),
  (78780211, 31, 40.0),
  (78780211, 34, 1.1),
  (78780211, 36, 1.0),
  (78780211, 39, 1.1),
  (78780211, 55, 100.0),
  (78780211, 64, 0.3),
  (78780211, 65, 0.5),
  (78780211, 66, 0.3),
  (78780211, 67, 0.35),
  (78780211, 68, 0.45),
  (78780211, 69, 0.05),
  (78780211, 70, 0.55),
  (78780211, 71, 1.0),
  (78780211, 72, 0.0),
  (78780211, 73, 1.0),
  (78780211, 74, 0.0),
  (78780211, 75, 1.0),
  (78780211, 80, 3.0),
  (78780211, 104, 10.0),
  (78780211, 122, 2.0),
  (78780211, 125, 0.1),
  (78780211, 127, 2.0),
  (78780211, 128, 1.0),
  (78780211, 151, 0.9),
  (78780211, 166, 0.3);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780211, 1, 16),
  (78780211, 2, 75),
  (78780211, 3, 39),
  (78780211, 6, -1),
  (78780211, 7, -1),
  (78780211, 25, 999),
  (78780211, 27, 0),
  (78780211, 40, 2),
  (78780211, 93, 1032),
  (78780211, 146, 25000000),
  (78780211, 16, 32),
  (78780211, 95, 8),
  (78780211, 133, 4),
  (78780211, 134, 16),
  (78780211, 290, 1),
  (78780211, 291, 60);
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`) VALUES
  (78780211, 1, 0, 3, 0, 33, 0, 2088.5099122453316),
  (78780211, 2, 0, 3, 0, 350, 0, 2088.5099122453316),
  (78780211, 3, 0, 3, 0, 350, 0, 2088.5099122453316),
  (78780211, 4, 0, 3, 0, 0, 0, 2088.5099122453316),
  (78780211, 5, 0, 3, 0, 33, 0, 2088.5099122453316),
  (78780211, 6, 0, 3, 0, 17, 0, 2088.5099122453316),
  (78780211, 7, 0, 3, 0, 75, 0, 2088.5099122453316),
  (78780211, 9, 0, 3, 0, 33, 0, 2088.5099122453316),
  (78780211, 10, 0, 3, 0, 33, 0, 2088.5099122453316),
  (78780211, 11, 0, 3, 0, 33, 0, 2088.5099122453316),
  (78780211, 12, 0, 3, 0, 350, 0, 2088.5099122453316),
  (78780211, 13, 0, 3, 0, 33, 0, 2088.5099122453316),
  (78780211, 15, 0, 3, 0, 135, 0, 2088.5099122453316),
  (78780211, 20, 0, 3, 0, 0, 0, 2088.5099122453316),
  (78780211, 22, 0, 3, 0, 0, 0, 2088.5099122453316),
  (78780211, 24, 0, 3, 0, 0, 0, 2088.5099122453316),
  (78780211, 31, 0, 3, 0, 25, 0, 2088.5099122453316),
  (78780211, 32, 0, 3, 0, 25, 0, 2088.5099122453316),
  (78780211, 33, 0, 3, 0, 25, 0, 2088.5099122453316),
  (78780211, 34, 0, 3, 0, 25, 0, 2088.5099122453316);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780211, 1, 'Registered Browerk, Champion Line');
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780211, 37, 0.3, NULL, NULL, NULL, 'annex_feud_b2', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 4.0, 0.0, NULL, 'Must he shout.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

-- 78780212  Certified Shreth, Third Generation
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780212, 'annex-registry-shreth', 10, NOW());
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) VALUES
  (78780212, 1, 30, 0, 0),
  (78780212, 2, 30, 0, 0),
  (78780212, 3, 20, 0, 0),
  (78780212, 4, 35, 0, 0),
  (78780212, 5, 15, 0, 0),
  (78780212, 6, 15, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) VALUES
  (78780212, 1, 0, 0, 0, 15),
  (78780212, 3, 70, 0, 0, 100),
  (78780212, 5, 0, 0, 0, 15);
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`) VALUES
  (78780212, 0, 4, 3, 0.75, 10, 5, 3, 8, 35, 4, 8, 3, 0, 1, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0),
  (78780212, 1, 1, 4, 0.0, 10, 5, 3, 8, 35, 4, 8, 3, 0, 2, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0),
  (78780212, 2, 4, 0, 0.0, 5, 2, 1, 4, 17, 2, 4, 2, 0, 3, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0),
  (78780212, 3, 4, 0, 0.0, 20, 9, 6, 16, 69, 7, 16, 7, 0, 1, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0),
  (78780212, 4, 4, 0, 0.0, 20, 9, 6, 16, 69, 7, 16, 7, 0, 2, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0),
  (78780212, 5, 4, 2, 0.75, 10, 5, 3, 8, 35, 4, 8, 3, 0, 2, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0),
  (78780212, 6, 4, 0, 0.0, 20, 9, 6, 16, 69, 7, 16, 7, 0, 3, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18),
  (78780212, 7, 4, 0, 0.0, 20, 9, 6, 16, 69, 7, 16, 7, 0, 3, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6),
  (78780212, 8, 4, 3, 0.75, 10, 5, 3, 8, 35, 4, 8, 3, 0, 3, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22);
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780212, 11, False),
  (78780212, 12, True),
  (78780212, 13, False),
  (78780212, 1, True),
  (78780212, 19, False),
  (78780212, 98, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780212, 1, 33555908),
  (78780212, 2, 150995072),
  (78780212, 3, 536870986),
  (78780212, 4, 805306399),
  (78780212, 6, 67112444),
  (78780212, 7, 268435840),
  (78780212, 8, 100669720),
  (78780212, 22, 872415333),
  (78780212, 35, 459);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780212, 1, 5.0),
  (78780212, 2, 0.0),
  (78780212, 3, 0.1),
  (78780212, 4, 4.0),
  (78780212, 5, 1.0),
  (78780212, 12, 0.5),
  (78780212, 13, 0.46),
  (78780212, 14, 0.28),
  (78780212, 15, 0.8),
  (78780212, 16, 3.45),
  (78780212, 17, 0.35),
  (78780212, 18, 0.8),
  (78780212, 19, 0.34),
  (78780212, 31, 8.0),
  (78780212, 34, 1.3),
  (78780212, 36, 1.0),
  (78780212, 39, 0.6),
  (78780212, 41, 3600.0),
  (78780212, 43, 2.0),
  (78780212, 64, 0.9),
  (78780212, 65, 0.9),
  (78780212, 66, 1.0),
  (78780212, 67, 0.8),
  (78780212, 68, 1.42),
  (78780212, 69, 1.0),
  (78780212, 70, 0.85),
  (78780212, 71, 1.0),
  (78780212, 72, 1.0),
  (78780212, 73, 1.0),
  (78780212, 74, 1.0),
  (78780212, 75, 1.0),
  (78780212, 104, 10.0),
  (78780212, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780212, 1, 16),
  (78780212, 2, 32),
  (78780212, 3, 40),
  (78780212, 6, -1),
  (78780212, 7, -1),
  (78780212, 25, 8),
  (78780212, 27, 0),
  (78780212, 40, 2),
  (78780212, 81, 3),
  (78780212, 82, 3),
  (78780212, 93, 1032),
  (78780212, 103, 1),
  (78780212, 142, 3),
  (78780212, 146, 1000),
  (78780212, 16, 32),
  (78780212, 95, 8),
  (78780212, 133, 4),
  (78780212, 134, 16),
  (78780212, 290, 1),
  (78780212, 291, 60);
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`) VALUES
  (78780212, 6, 0, 3, 0, 18, 0, 379.986215552444),
  (78780212, 7, 0, 3, 0, 34, 0, 379.986215552444),
  (78780212, 15, 0, 3, 0, 8, 0, 379.986215552444),
  (78780212, 20, 0, 2, 0, 0, 0, 379.986215552444),
  (78780212, 22, 0, 2, 0, 10, 0, 379.986215552444),
  (78780212, 45, 0, 3, 0, 5, 0, 379.986215552444);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780212, 1, 'Certified Shreth, Third Generation'),
  (78780212, 34, 'springbabies');
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780212, 37, 0.3, NULL, NULL, NULL, 'annex_feud_b2', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 4.0, 0.0, NULL, 'One does not respond. One simply files.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

-- 78780213  Pedigreed Ursuin (Papers Pending)
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780213, 'annex-registry-ursuin', 10, NOW());
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) VALUES
  (78780213, 1, 80, 0, 0),
  (78780213, 2, 90, 0, 0),
  (78780213, 3, 50, 0, 0),
  (78780213, 4, 90, 0, 0),
  (78780213, 5, 50, 0, 0),
  (78780213, 6, 20, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) VALUES
  (78780213, 1, 35, 0, 0, 80),
  (78780213, 3, 150, 0, 0, 240),
  (78780213, 5, 0, 0, 0, 20);
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`) VALUES
  (78780213, 0, 2, 15, 0.75, 45, 2, 36, 2, 2, 25, 2, 2, 0, 1, 0.4, 0.1, 0.0, 0.4, 0.1, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0),
  (78780213, 10, 1, 15, 0.75, 45, 2, 36, 2, 2, 25, 2, 2, 0, 3, 0.0, 0.2, 0.8, 0.0, 0.2, 0.8, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0),
  (78780213, 13, 1, 15, 0.75, 45, 2, 36, 2, 2, 25, 2, 2, 0, 3, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.1, 0.3, 0.7, 0.1, 0.3, 0.7),
  (78780213, 16, 4, 0, 0.0, 40, 2, 32, 2, 2, 22, 2, 2, 0, 2, 0.6, 0.7, 0.2, 0.6, 0.7, 0.2, 0.9, 0.7, 0.3, 0.9, 0.7, 0.3);
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780213, 11, False),
  (78780213, 12, True),
  (78780213, 13, False),
  (78780213, 1, True),
  (78780213, 19, False),
  (78780213, 98, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780213, 1, 33556773),
  (78780213, 2, 150995100),
  (78780213, 3, 536871011),
  (78780213, 4, 805306409),
  (78780213, 8, 100670959),
  (78780213, 22, 872415366),
  (78780213, 35, 459);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780213, 1, 5.0),
  (78780213, 2, 0.0),
  (78780213, 3, 0.1),
  (78780213, 4, 3.0),
  (78780213, 5, 1.0),
  (78780213, 13, 0.05),
  (78780213, 14, 0.8),
  (78780213, 15, 0.05),
  (78780213, 16, 0.05),
  (78780213, 17, 0.56),
  (78780213, 18, 0.05),
  (78780213, 19, 0.05),
  (78780213, 31, 24.0),
  (78780213, 34, 1.0),
  (78780213, 36, 1.0),
  (78780213, 41, 3600.0),
  (78780213, 43, 3.0),
  (78780213, 64, 0.58),
  (78780213, 65, 1.0),
  (78780213, 66, 0.58),
  (78780213, 67, 0.86),
  (78780213, 68, 0.58),
  (78780213, 69, 0.58),
  (78780213, 70, 0.58),
  (78780213, 71, 1.0),
  (78780213, 72, 1.0),
  (78780213, 73, 1.0),
  (78780213, 74, 1.0),
  (78780213, 75, 1.0),
  (78780213, 104, 10.0),
  (78780213, 125, 1.0),
  (78780213, 39, 0.5);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780213, 1, 16),
  (78780213, 2, 46),
  (78780213, 6, -1),
  (78780213, 7, -1),
  (78780213, 25, 8),
  (78780213, 27, 0),
  (78780213, 40, 2),
  (78780213, 81, 3),
  (78780213, 82, 3),
  (78780213, 93, 1032),
  (78780213, 101, 131),
  (78780213, 103, 1),
  (78780213, 140, 1),
  (78780213, 142, 3),
  (78780213, 146, 1000),
  (78780213, 16, 32),
  (78780213, 95, 8),
  (78780213, 133, 4),
  (78780213, 134, 16),
  (78780213, 290, 1),
  (78780213, 291, 60);
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`) VALUES
  (78780213, 6, 0, 3, 0, 46, 0, 562.780431222187),
  (78780213, 7, 0, 3, 0, 86, 0, 562.780431222187),
  (78780213, 15, 0, 3, 0, 42, 0, 562.780431222187),
  (78780213, 45, 0, 3, 0, 30, 0, 562.780431222187);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780213, 1, 'Pedigreed Ursuin (Papers Pending)'),
  (78780213, 34, 'springbabies');
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780213, 37, 0.3, NULL, NULL, NULL, 'annex_feud_b2', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 4.0, 0.0, NULL, 'My papers are pending. Pending papers are still papers.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

-- 78780214  Drudge Skulker of Record
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780214, 'annex-registry-drudge', 10, NOW());
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) VALUES
  (78780214, 1, 70, 0, 0),
  (78780214, 2, 60, 0, 0),
  (78780214, 3, 110, 0, 0),
  (78780214, 4, 90, 0, 0),
  (78780214, 5, 15, 0, 0),
  (78780214, 6, 15, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) VALUES
  (78780214, 1, 12, 0, 0, 42),
  (78780214, 3, 20, 0, 0, 80),
  (78780214, 5, 0, 0, 0, 15);
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`) VALUES
  (78780214, 0, 4, 0, 0.0, 3, 3, 3, 3, 2, 2, 3, 1, 0, 1, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0),
  (78780214, 1, 4, 0, 0.0, 7, 6, 7, 8, 4, 4, 7, 3, 0, 2, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0),
  (78780214, 2, 4, 0, 0.0, 7, 6, 7, 8, 4, 4, 7, 3, 0, 3, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0),
  (78780214, 3, 4, 0, 0.0, 5, 5, 5, 6, 3, 3, 5, 2, 0, 1, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0),
  (78780214, 4, 4, 0, 0.0, 7, 6, 7, 8, 4, 4, 7, 3, 0, 2, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0),
  (78780214, 5, 4, 2, 0.75, 5, 5, 5, 6, 3, 3, 5, 2, 0, 2, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0),
  (78780214, 6, 4, 0, 0.0, 5, 5, 5, 6, 3, 3, 5, 2, 0, 3, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18),
  (78780214, 7, 4, 0, 0.0, 5, 5, 5, 6, 3, 3, 5, 2, 0, 3, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6),
  (78780214, 8, 4, 3, 0.75, 5, 5, 5, 6, 3, 3, 5, 2, 0, 3, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22);
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780214, 11, False),
  (78780214, 12, True),
  (78780214, 13, False),
  (78780214, 14, True),
  (78780214, 1, True),
  (78780214, 19, False),
  (78780214, 98, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780214, 1, 33556445),
  (78780214, 2, 150994952),
  (78780214, 3, 536870919),
  (78780214, 4, 805306372),
  (78780214, 6, 67112812),
  (78780214, 7, 268435974),
  (78780214, 8, 100667445),
  (78780214, 22, 872415258),
  (78780214, 32, 80),
  (78780214, 35, 453);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780214, 1, 5.0),
  (78780214, 2, 0.0),
  (78780214, 3, 0.067),
  (78780214, 4, 5.0),
  (78780214, 5, 1.0),
  (78780214, 12, 1.0),
  (78780214, 13, 0.9),
  (78780214, 14, 1.0),
  (78780214, 15, 1.1),
  (78780214, 16, 0.6),
  (78780214, 17, 0.6),
  (78780214, 18, 1.0),
  (78780214, 19, 0.36),
  (78780214, 31, 10.0),
  (78780214, 34, 1.0),
  (78780214, 36, 1.0),
  (78780214, 39, 0.95),
  (78780214, 64, 0.86),
  (78780214, 65, 0.75),
  (78780214, 66, 0.66),
  (78780214, 67, 1.42),
  (78780214, 68, 1.42),
  (78780214, 69, 0.75),
  (78780214, 70, 1.42),
  (78780214, 71, 1.0),
  (78780214, 72, 1.0),
  (78780214, 73, 1.0),
  (78780214, 74, 1.0),
  (78780214, 75, 1.0),
  (78780214, 104, 10.0),
  (78780214, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780214, 1, 16),
  (78780214, 2, 3),
  (78780214, 3, 48),
  (78780214, 6, -1),
  (78780214, 7, -1),
  (78780214, 25, 8),
  (78780214, 27, 0),
  (78780214, 40, 2),
  (78780214, 93, 1032),
  (78780214, 101, 131),
  (78780214, 140, 1),
  (78780214, 146, 1000),
  (78780214, 16, 32),
  (78780214, 95, 8),
  (78780214, 133, 4),
  (78780214, 134, 16),
  (78780214, 290, 1),
  (78780214, 291, 60);
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`) VALUES
  (78780214, 6, 0, 3, 0, 5, 0, 0.0),
  (78780214, 7, 0, 3, 0, 15, 0, 0.0),
  (78780214, 15, 0, 3, 0, 5, 0, 0.0),
  (78780214, 24, 0, 3, 0, 40, 0, 0.0),
  (78780214, 44, 0, 3, 0, 5, 0, 0.0),
  (78780214, 45, 0, 3, 0, 5, 0, 0.0),
  (78780214, 46, 0, 3, 0, 5, 0, 0.0);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780214, 1, 'Drudge Skulker of Record');
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780214, 37, 0.3, NULL, NULL, NULL, 'annex_feud_b2', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 4.0, 0.0, NULL, 'I have a lineage chart. It is very long.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

-- 78780215  Certified Sawato Bandit, Reformed
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780215, 'annex-registry-sawato-bandit', 10, NOW());
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) VALUES
  (78780215, 1, 315, 0, 0),
  (78780215, 2, 245, 0, 0),
  (78780215, 3, 255, 0, 0),
  (78780215, 4, 295, 0, 0),
  (78780215, 5, 140, 0, 0),
  (78780215, 6, 150, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) VALUES
  (78780215, 1, 477, 0, 0, 600),
  (78780215, 3, 855, 0, 0, 1100),
  (78780215, 5, 120, 0, 0, 270);
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`) VALUES
  (78780215, 0, 4, 0, 0.0, 540, 486, 486, 540, 540, 432, 486, 432, 0, 1, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0),
  (78780215, 1, 4, 0, 0.0, 390, 351, 351, 390, 390, 312, 351, 312, 0, 2, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0),
  (78780215, 2, 4, 0, 0.0, 390, 351, 351, 390, 390, 312, 351, 312, 0, 3, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0),
  (78780215, 3, 4, 0, 0.0, 390, 351, 351, 390, 390, 312, 351, 312, 0, 1, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0),
  (78780215, 4, 4, 0, 0.0, 390, 351, 351, 390, 390, 312, 351, 312, 0, 2, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0),
  (78780215, 5, 4, 200, 0.75, 300, 270, 270, 300, 300, 240, 270, 240, 0, 2, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0),
  (78780215, 6, 4, 0, 0.0, 390, 351, 351, 390, 390, 312, 351, 312, 0, 3, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18),
  (78780215, 7, 4, 0, 0.0, 390, 351, 351, 390, 390, 312, 351, 312, 0, 3, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6),
  (78780215, 8, 4, 200, 0.75, 320, 288, 288, 320, 320, 256, 288, 256, 0, 3, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22);
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780215, 11, False),
  (78780215, 12, True),
  (78780215, 13, False),
  (78780215, 1, True),
  (78780215, 19, False),
  (78780215, 98, True);
INSERT INTO `weenie_properties_create_list` (`object_Id`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`) VALUES
  (78780215, 2, 6046, 1, 2, 0.4789, False),
  (78780215, 2, 6047, 1, 2, 0.4789, False),
  (78780215, 2, 27226, 1, 39, 0.0, False),
  (78780215, 2, 9392, 1, 2, 0.2, False),
  (78780215, 2, 31704, 1, 0, 0.5, False);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780215, 1, 33554433),
  (78780215, 2, 150994945),
  (78780215, 3, 536870913),
  (78780215, 4, 805306368),
  (78780215, 7, 268437191),
  (78780215, 8, 100667446),
  (78780215, 22, 872415236),
  (78780215, 35, 455);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780215, 1, 5.0),
  (78780215, 2, 0.0),
  (78780215, 3, 2.0),
  (78780215, 4, 5.0),
  (78780215, 5, 1.0),
  (78780215, 13, 0.9),
  (78780215, 14, 0.9),
  (78780215, 15, 1.0),
  (78780215, 16, 1.0),
  (78780215, 17, 0.8),
  (78780215, 18, 0.9),
  (78780215, 19, 0.8),
  (78780215, 31, 18.0),
  (78780215, 55, 80.0),
  (78780215, 64, 0.6),
  (78780215, 65, 0.6),
  (78780215, 66, 0.6),
  (78780215, 67, 0.7),
  (78780215, 68, 0.6),
  (78780215, 69, 0.6),
  (78780215, 70, 0.7),
  (78780215, 80, 2.0),
  (78780215, 104, 10.0),
  (78780215, 122, 2.0),
  (78780215, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780215, 1, 16),
  (78780215, 2, 31),
  (78780215, 3, 9),
  (78780215, 6, -1),
  (78780215, 7, -1),
  (78780215, 25, 160),
  (78780215, 93, 1032),
  (78780215, 113, 1),
  (78780215, 146, 500000),
  (78780215, 188, 3),
  (78780215, 307, 5),
  (78780215, 16, 32),
  (78780215, 95, 8),
  (78780215, 133, 4),
  (78780215, 134, 16);
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`) VALUES
  (78780215, 6, 0, 2, 0, 350, 0, 0.0),
  (78780215, 7, 0, 2, 0, 380, 0, 0.0),
  (78780215, 15, 0, 2, 0, 360, 0, 0.0),
  (78780215, 44, 0, 2, 0, 400, 0, 0.0),
  (78780215, 45, 0, 2, 0, 400, 0, 0.0),
  (78780215, 46, 0, 2, 0, 400, 0, 0.0),
  (78780215, 47, 0, 2, 0, 350, 0, 0.0);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780215, 1, 'Certified Sawato Bandit, Reformed'),
  (78780215, 45, 'KillTaskSawatoBandit');

-- 78780220  Splotch
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780220, 'annex-ward-splotch', 10, NOW());
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) VALUES
  (78780220, 1, 1800, 0, 0),
  (78780220, 2, 4000, 0, 0),
  (78780220, 3, 200, 0, 0),
  (78780220, 4, 50, 0, 0),
  (78780220, 5, 1000, 0, 0),
  (78780220, 6, 1000, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) VALUES
  (78780220, 1, 198000, 0, 0, 200000),
  (78780220, 3, 196000, 0, 0, 200000),
  (78780220, 5, 199000, 0, 0, 200000);
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`) VALUES
  (78780220, 0, 4, 0, 0.0, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 1, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0),
  (78780220, 1, 4, 0, 0.0, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 2, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0),
  (78780220, 2, 4, 0, 0.0, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 3, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0),
  (78780220, 3, 4, 0, 0.0, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 1, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0),
  (78780220, 4, 4, 0, 0.0, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 2, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0),
  (78780220, 5, 1, 50, 0.5, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 2, 0.0, 0.1, 0.0, 0.0, 0.1, 0.0, 0.0, 0.1, 0.0, 0.0, 0.1, 0.0),
  (78780220, 6, 4, 0, 0.0, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 3, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18),
  (78780220, 7, 4, 0, 0.0, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 3, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6),
  (78780220, 8, 4, 50, 0.5, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 3, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22),
  (78780220, 20, 1, 50, 0.5, 650, 780, 650, 1040, 650, 715, 1040, 650, 0, 2, 0.0, 0.1, 0.0, 0.0, 0.1, 0.0, 0.0, 0.1, 0.0, 0.0, 0.1, 0.0);
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780220, 6, True),
  (78780220, 11, False),
  (78780220, 12, True),
  (78780220, 13, False),
  (78780220, 50, True),
  (78780220, 1, True),
  (78780220, 19, False),
  (78780220, 98, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780220, 1, 33558882),
  (78780220, 2, 150995310),
  (78780220, 3, 536871095),
  (78780220, 4, 805306430),
  (78780220, 6, 67115354),
  (78780220, 7, 268436860),
  (78780220, 8, 100677029),
  (78780220, 22, 872415402),
  (78780220, 35, 32);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780220, 1, 5.0),
  (78780220, 2, 0.0),
  (78780220, 3, 1.0),
  (78780220, 4, 5.0),
  (78780220, 5, 2.0),
  (78780220, 12, 0.5),
  (78780220, 13, 1.0),
  (78780220, 14, 1.0),
  (78780220, 15, 1.2),
  (78780220, 16, 1.0),
  (78780220, 17, 0.9),
  (78780220, 18, 1.2),
  (78780220, 19, 1.0),
  (78780220, 31, 40.0),
  (78780220, 34, 1.1),
  (78780220, 36, 1.0),
  (78780220, 55, 100.0),
  (78780220, 64, 0.3),
  (78780220, 65, 0.5),
  (78780220, 66, 0.3),
  (78780220, 67, 0.35),
  (78780220, 68, 0.45),
  (78780220, 69, 0.05),
  (78780220, 70, 0.55),
  (78780220, 71, 1.0),
  (78780220, 72, 0.0),
  (78780220, 73, 1.0),
  (78780220, 74, 0.0),
  (78780220, 75, 1.0),
  (78780220, 80, 3.0),
  (78780220, 104, 10.0),
  (78780220, 122, 2.0),
  (78780220, 125, 0.1),
  (78780220, 127, 2.0),
  (78780220, 128, 1.0),
  (78780220, 151, 0.9),
  (78780220, 166, 0.3),
  (78780220, 39, 0.275);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780220, 1, 16),
  (78780220, 2, 75),
  (78780220, 6, -1),
  (78780220, 7, -1),
  (78780220, 25, 999),
  (78780220, 27, 0),
  (78780220, 40, 2),
  (78780220, 93, 1032),
  (78780220, 146, 25000000),
  (78780220, 16, 32),
  (78780220, 95, 8),
  (78780220, 133, 4),
  (78780220, 134, 16),
  (78780220, 290, 1),
  (78780220, 291, 60),
  (78780220, 3, 67111092);
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`) VALUES
  (78780220, 1, 0, 3, 0, 33, 0, 2088.5099122453316),
  (78780220, 2, 0, 3, 0, 350, 0, 2088.5099122453316),
  (78780220, 3, 0, 3, 0, 350, 0, 2088.5099122453316),
  (78780220, 4, 0, 3, 0, 0, 0, 2088.5099122453316),
  (78780220, 5, 0, 3, 0, 33, 0, 2088.5099122453316),
  (78780220, 6, 0, 3, 0, 17, 0, 2088.5099122453316),
  (78780220, 7, 0, 3, 0, 75, 0, 2088.5099122453316),
  (78780220, 9, 0, 3, 0, 33, 0, 2088.5099122453316),
  (78780220, 10, 0, 3, 0, 33, 0, 2088.5099122453316),
  (78780220, 11, 0, 3, 0, 33, 0, 2088.5099122453316),
  (78780220, 12, 0, 3, 0, 350, 0, 2088.5099122453316),
  (78780220, 13, 0, 3, 0, 33, 0, 2088.5099122453316),
  (78780220, 15, 0, 3, 0, 135, 0, 2088.5099122453316),
  (78780220, 20, 0, 3, 0, 0, 0, 2088.5099122453316),
  (78780220, 22, 0, 3, 0, 0, 0, 2088.5099122453316),
  (78780220, 24, 0, 3, 0, 0, 0, 2088.5099122453316),
  (78780220, 31, 0, 3, 0, 25, 0, 2088.5099122453316),
  (78780220, 32, 0, 3, 0, 25, 0, 2088.5099122453316),
  (78780220, 33, 0, 3, 0, 25, 0, 2088.5099122453316),
  (78780220, 34, 0, 3, 0, 25, 0, 2088.5099122453316);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780220, 1, 'Splotch');
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780220, 7, 1.0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 10, 0.0, 0.0, NULL, 'First one. That''s me. Before me there was brown.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 10, 3.5, 0.0, NULL, 'The Professor wrote ''unexpected'' in his notes and then he wrote it again, underlined.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 2, 10, 3.5, 0.0, NULL, 'Breed something over there on that floor and if you''re lucky it comes out looking like one of us.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 3, 10, 3.5, 0.0, NULL, 'Then bring it here. We''ll know it.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780220, 37, 1.0, NULL, NULL, NULL, 'annex_feud_a1', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 3.0, 0.0, NULL, 'It means BORING. Say the quiet part, Bexley.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'annex_feud_b1', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780220, 37, 1.0, NULL, NULL, NULL, 'annex_feud_a2', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 3.0, 0.0, NULL, 'Papers? I have PALETTES.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'annex_feud_b2', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780220, 37, 1.0, NULL, NULL, NULL, 'annex_feud_a4', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 3.0, 0.0, NULL, 'We don''t have a bloodline. We have a RANGE.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'annex_feud_b2', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780220, 37, 1.0, NULL, NULL, NULL, 'annex_feud_a6', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 3.0, 0.0, NULL, 'Ask them what colour they''ll be next year. Go on. Ask them.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'annex_feud_b1', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780220, 37, 1.0, NULL, NULL, NULL, 'annex_feud_b3', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 3.0, 0.0, NULL, 'He acknowledged us. Write that down.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'annex_feud_b1', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780220, 37, 1.0, NULL, NULL, NULL, 'pat_splotch_2', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 3.5, 0.0, NULL, 'Not me. I''d remember.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'pat_splotch_3', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780220, 37, 1.0, NULL, NULL, NULL, 'pat_splotch_4', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 3.0, 0.0, NULL, 'I really don''t.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

-- 78780221  The Teal Incident
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780221, 'annex-ward-teal', 10, NOW());
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) VALUES
  (78780221, 1, 30, 0, 0),
  (78780221, 2, 30, 0, 0),
  (78780221, 3, 20, 0, 0),
  (78780221, 4, 35, 0, 0),
  (78780221, 5, 15, 0, 0),
  (78780221, 6, 15, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) VALUES
  (78780221, 1, 0, 0, 0, 15),
  (78780221, 3, 70, 0, 0, 100),
  (78780221, 5, 0, 0, 0, 15);
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`) VALUES
  (78780221, 0, 4, 3, 0.75, 10, 5, 3, 8, 35, 4, 8, 3, 0, 1, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0),
  (78780221, 1, 1, 4, 0.0, 10, 5, 3, 8, 35, 4, 8, 3, 0, 2, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0),
  (78780221, 2, 4, 0, 0.0, 5, 2, 1, 4, 17, 2, 4, 2, 0, 3, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0),
  (78780221, 3, 4, 0, 0.0, 20, 9, 6, 16, 69, 7, 16, 7, 0, 1, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0),
  (78780221, 4, 4, 0, 0.0, 20, 9, 6, 16, 69, 7, 16, 7, 0, 2, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0),
  (78780221, 5, 4, 2, 0.75, 10, 5, 3, 8, 35, 4, 8, 3, 0, 2, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0),
  (78780221, 6, 4, 0, 0.0, 20, 9, 6, 16, 69, 7, 16, 7, 0, 3, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18),
  (78780221, 7, 4, 0, 0.0, 20, 9, 6, 16, 69, 7, 16, 7, 0, 3, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6),
  (78780221, 8, 4, 3, 0.75, 10, 5, 3, 8, 35, 4, 8, 3, 0, 3, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22);
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780221, 11, False),
  (78780221, 12, True),
  (78780221, 13, False),
  (78780221, 1, True),
  (78780221, 19, False),
  (78780221, 98, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780221, 1, 33555908),
  (78780221, 2, 150995072),
  (78780221, 3, 536870986),
  (78780221, 4, 805306399),
  (78780221, 6, 67112444),
  (78780221, 7, 268435840),
  (78780221, 8, 100669720),
  (78780221, 22, 872415333),
  (78780221, 35, 459);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780221, 1, 5.0),
  (78780221, 2, 0.0),
  (78780221, 3, 0.1),
  (78780221, 4, 4.0),
  (78780221, 5, 1.0),
  (78780221, 12, 0.5),
  (78780221, 13, 0.46),
  (78780221, 14, 0.28),
  (78780221, 15, 0.8),
  (78780221, 16, 3.45),
  (78780221, 17, 0.35),
  (78780221, 18, 0.8),
  (78780221, 19, 0.34),
  (78780221, 31, 8.0),
  (78780221, 34, 1.3),
  (78780221, 36, 1.0),
  (78780221, 39, 0.6),
  (78780221, 41, 3600.0),
  (78780221, 43, 2.0),
  (78780221, 64, 0.9),
  (78780221, 65, 0.9),
  (78780221, 66, 1.0),
  (78780221, 67, 0.8),
  (78780221, 68, 1.42),
  (78780221, 69, 1.0),
  (78780221, 70, 0.85),
  (78780221, 71, 1.0),
  (78780221, 72, 1.0),
  (78780221, 73, 1.0),
  (78780221, 74, 1.0),
  (78780221, 75, 1.0),
  (78780221, 104, 10.0),
  (78780221, 125, 1.0),
  (78780221, 9060, 60.0);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780221, 1, 16),
  (78780221, 2, 32),
  (78780221, 6, -1),
  (78780221, 7, -1),
  (78780221, 25, 8),
  (78780221, 27, 0),
  (78780221, 40, 2),
  (78780221, 81, 3),
  (78780221, 82, 3),
  (78780221, 93, 1032),
  (78780221, 103, 1),
  (78780221, 142, 3),
  (78780221, 146, 1000),
  (78780221, 16, 32),
  (78780221, 95, 8),
  (78780221, 133, 4),
  (78780221, 134, 16),
  (78780221, 290, 1),
  (78780221, 291, 60),
  (78780221, 3, 67113301);
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`) VALUES
  (78780221, 6, 0, 3, 0, 18, 0, 379.986215552444),
  (78780221, 7, 0, 3, 0, 34, 0, 379.986215552444),
  (78780221, 15, 0, 3, 0, 8, 0, 379.986215552444),
  (78780221, 20, 0, 2, 0, 0, 0, 379.986215552444),
  (78780221, 22, 0, 2, 0, 10, 0, 379.986215552444),
  (78780221, 45, 0, 3, 0, 5, 0, 379.986215552444);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780221, 1, 'The Teal Incident'),
  (78780221, 34, 'springbabies');
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780221, 37, 0.3, NULL, NULL, NULL, 'annex_feud_b1', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 4.0, 0.0, NULL, 'HA.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

-- 78780222  Ursuin, Unregistered
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780222, 'annex-ward-ursuin', 10, NOW());
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) VALUES
  (78780222, 1, 80, 0, 0),
  (78780222, 2, 90, 0, 0),
  (78780222, 3, 50, 0, 0),
  (78780222, 4, 90, 0, 0),
  (78780222, 5, 50, 0, 0),
  (78780222, 6, 20, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) VALUES
  (78780222, 1, 35, 0, 0, 80),
  (78780222, 3, 150, 0, 0, 240),
  (78780222, 5, 0, 0, 0, 20);
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`) VALUES
  (78780222, 0, 2, 15, 0.75, 45, 2, 36, 2, 2, 25, 2, 2, 0, 1, 0.4, 0.1, 0.0, 0.4, 0.1, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0),
  (78780222, 10, 1, 15, 0.75, 45, 2, 36, 2, 2, 25, 2, 2, 0, 3, 0.0, 0.2, 0.8, 0.0, 0.2, 0.8, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0),
  (78780222, 13, 1, 15, 0.75, 45, 2, 36, 2, 2, 25, 2, 2, 0, 3, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.1, 0.3, 0.7, 0.1, 0.3, 0.7),
  (78780222, 16, 4, 0, 0.0, 40, 2, 32, 2, 2, 22, 2, 2, 0, 2, 0.6, 0.7, 0.2, 0.6, 0.7, 0.2, 0.9, 0.7, 0.3, 0.9, 0.7, 0.3);
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780222, 11, False),
  (78780222, 12, True),
  (78780222, 13, False),
  (78780222, 1, True),
  (78780222, 19, False),
  (78780222, 98, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780222, 1, 33556773),
  (78780222, 2, 150995100),
  (78780222, 3, 536871011),
  (78780222, 4, 805306409),
  (78780222, 8, 100670959),
  (78780222, 22, 872415366),
  (78780222, 35, 459);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780222, 1, 5.0),
  (78780222, 2, 0.0),
  (78780222, 3, 0.1),
  (78780222, 4, 3.0),
  (78780222, 5, 1.0),
  (78780222, 13, 0.05),
  (78780222, 14, 0.8),
  (78780222, 15, 0.05),
  (78780222, 16, 0.05),
  (78780222, 17, 0.56),
  (78780222, 18, 0.05),
  (78780222, 19, 0.05),
  (78780222, 31, 24.0),
  (78780222, 34, 1.0),
  (78780222, 36, 1.0),
  (78780222, 41, 3600.0),
  (78780222, 43, 3.0),
  (78780222, 64, 0.58),
  (78780222, 65, 1.0),
  (78780222, 66, 0.58),
  (78780222, 67, 0.86),
  (78780222, 68, 0.58),
  (78780222, 69, 0.58),
  (78780222, 70, 0.58),
  (78780222, 71, 1.0),
  (78780222, 72, 1.0),
  (78780222, 73, 1.0),
  (78780222, 74, 1.0),
  (78780222, 75, 1.0),
  (78780222, 104, 10.0),
  (78780222, 125, 1.0),
  (78780222, 39, 0.5),
  (78780222, 9060, 60.0);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780222, 1, 16),
  (78780222, 2, 46),
  (78780222, 6, -1),
  (78780222, 7, -1),
  (78780222, 25, 8),
  (78780222, 27, 0),
  (78780222, 40, 2),
  (78780222, 81, 3),
  (78780222, 82, 3),
  (78780222, 93, 1032),
  (78780222, 101, 131),
  (78780222, 103, 1),
  (78780222, 140, 1),
  (78780222, 142, 3),
  (78780222, 146, 1000),
  (78780222, 16, 32),
  (78780222, 95, 8),
  (78780222, 133, 4),
  (78780222, 134, 16),
  (78780222, 290, 1),
  (78780222, 291, 60),
  (78780222, 3, 67114085);
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`) VALUES
  (78780222, 6, 0, 3, 0, 46, 0, 562.780431222187),
  (78780222, 7, 0, 3, 0, 86, 0, 562.780431222187),
  (78780222, 15, 0, 3, 0, 42, 0, 562.780431222187),
  (78780222, 45, 0, 3, 0, 30, 0, 562.780431222187);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780222, 1, 'Ursuin, Unregistered'),
  (78780222, 34, 'springbabies');
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780222, 37, 0.3, NULL, NULL, NULL, 'annex_feud_b1', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 4.0, 0.0, NULL, 'Say it louder, Splotch.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

-- 78780223  Nine-Colour Shreth
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780223, 'annex-ward-shreth', 10, NOW());
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) VALUES
  (78780223, 1, 65, 0, 0),
  (78780223, 2, 55, 0, 0),
  (78780223, 3, 80, 0, 0),
  (78780223, 4, 70, 0, 0),
  (78780223, 5, 40, 0, 0),
  (78780223, 6, 40, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) VALUES
  (78780223, 1, 10, 0, 0, 38),
  (78780223, 3, 150, 0, 0, 205),
  (78780223, 5, 0, 0, 0, 40);
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`) VALUES
  (78780223, 0, 4, 5, 0.75, 40, 2, 14, 32, 24, 9, 32, 11, 0, 1, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0),
  (78780223, 1, 1, 7, 0.0, 45, 2, 16, 36, 27, 10, 36, 13, 0, 2, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0),
  (78780223, 2, 4, 0, 0.0, 40, 2, 14, 32, 24, 9, 32, 11, 0, 3, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0),
  (78780223, 3, 4, 0, 0.0, 45, 2, 16, 36, 27, 10, 36, 13, 0, 1, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0),
  (78780223, 4, 4, 0, 0.0, 45, 2, 16, 36, 27, 10, 36, 13, 0, 2, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0),
  (78780223, 5, 4, 10, 0.75, 45, 2, 16, 36, 27, 10, 36, 13, 0, 2, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0),
  (78780223, 6, 4, 0, 0.0, 35, 2, 13, 28, 21, 8, 28, 10, 0, 3, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18),
  (78780223, 7, 4, 0, 0.0, 35, 2, 13, 28, 21, 8, 28, 10, 0, 3, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6),
  (78780223, 8, 4, 10, 0.75, 35, 2, 13, 28, 21, 8, 28, 10, 0, 3, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22);
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780223, 11, False),
  (78780223, 12, True),
  (78780223, 13, False),
  (78780223, 1, True),
  (78780223, 19, False),
  (78780223, 98, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780223, 1, 33555879),
  (78780223, 2, 150995072),
  (78780223, 3, 536870986),
  (78780223, 4, 805306399),
  (78780223, 6, 67112444),
  (78780223, 7, 268435808),
  (78780223, 8, 100669720),
  (78780223, 22, 872415333),
  (78780223, 35, 459);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780223, 1, 5.0),
  (78780223, 2, 0.0),
  (78780223, 3, 0.2),
  (78780223, 4, 4.0),
  (78780223, 5, 1.0),
  (78780223, 12, 0.5),
  (78780223, 13, 0.05),
  (78780223, 14, 0.36),
  (78780223, 15, 0.8),
  (78780223, 16, 0.6),
  (78780223, 17, 0.22),
  (78780223, 18, 0.8),
  (78780223, 19, 0.28),
  (78780223, 31, 8.0),
  (78780223, 34, 1.2),
  (78780223, 36, 1.0),
  (78780223, 41, 3600.0),
  (78780223, 43, 2.0),
  (78780223, 64, 0.58),
  (78780223, 65, 0.75),
  (78780223, 66, 1.0),
  (78780223, 67, 0.5),
  (78780223, 68, 1.0),
  (78780223, 69, 1.0),
  (78780223, 70, 0.6),
  (78780223, 71, 1.0),
  (78780223, 72, 1.0),
  (78780223, 73, 1.0),
  (78780223, 74, 1.0),
  (78780223, 75, 1.0),
  (78780223, 104, 10.0),
  (78780223, 125, 1.0),
  (78780223, 39, 0.75),
  (78780223, 9060, 60.0);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780223, 1, 16),
  (78780223, 2, 32),
  (78780223, 6, -1),
  (78780223, 7, -1),
  (78780223, 25, 8),
  (78780223, 27, 0),
  (78780223, 40, 2),
  (78780223, 81, 3),
  (78780223, 82, 3),
  (78780223, 93, 1032),
  (78780223, 103, 1),
  (78780223, 142, 3),
  (78780223, 146, 1000),
  (78780223, 16, 32),
  (78780223, 95, 8),
  (78780223, 133, 4),
  (78780223, 134, 16),
  (78780223, 290, 1),
  (78780223, 291, 60),
  (78780223, 3, 67115378);
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`) VALUES
  (78780223, 6, 0, 3, 0, 35, 0, 380.183351392565),
  (78780223, 7, 0, 3, 0, 68, 0, 380.183351392565),
  (78780223, 15, 0, 3, 0, 22, 0, 380.183351392565),
  (78780223, 20, 0, 3, 0, 0, 0, 380.183351392565),
  (78780223, 22, 0, 3, 0, 10, 0, 380.183351392565),
  (78780223, 45, 0, 3, 0, 20, 0, 380.183351392565);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780223, 1, 'Nine-Colour Shreth'),
  (78780223, 34, 'springbabies'),
  (78780223, 45, 'KilltaskBloodShreth');
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780223, 37, 0.3, NULL, NULL, NULL, 'annex_feud_b1', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 4.0, 0.0, NULL, 'That''s the one. That''s the good one.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

-- 78780224  Subject Twelve
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780224, 'annex-ward-subject12', 10, NOW());
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) VALUES
  (78780224, 1, 70, 0, 0),
  (78780224, 2, 60, 0, 0),
  (78780224, 3, 110, 0, 0),
  (78780224, 4, 90, 0, 0),
  (78780224, 5, 15, 0, 0),
  (78780224, 6, 15, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) VALUES
  (78780224, 1, 12, 0, 0, 42),
  (78780224, 3, 20, 0, 0, 80),
  (78780224, 5, 0, 0, 0, 15);
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`) VALUES
  (78780224, 0, 4, 0, 0.0, 3, 3, 3, 3, 2, 2, 3, 1, 0, 1, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0),
  (78780224, 1, 4, 0, 0.0, 7, 6, 7, 8, 4, 4, 7, 3, 0, 2, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0),
  (78780224, 2, 4, 0, 0.0, 7, 6, 7, 8, 4, 4, 7, 3, 0, 3, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0),
  (78780224, 3, 4, 0, 0.0, 5, 5, 5, 6, 3, 3, 5, 2, 0, 1, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0),
  (78780224, 4, 4, 0, 0.0, 7, 6, 7, 8, 4, 4, 7, 3, 0, 2, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0),
  (78780224, 5, 4, 2, 0.75, 5, 5, 5, 6, 3, 3, 5, 2, 0, 2, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0),
  (78780224, 6, 4, 0, 0.0, 5, 5, 5, 6, 3, 3, 5, 2, 0, 3, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18),
  (78780224, 7, 4, 0, 0.0, 5, 5, 5, 6, 3, 3, 5, 2, 0, 3, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6),
  (78780224, 8, 4, 3, 0.75, 5, 5, 5, 6, 3, 3, 5, 2, 0, 3, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22);
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780224, 11, False),
  (78780224, 12, True),
  (78780224, 13, False),
  (78780224, 14, True),
  (78780224, 1, True),
  (78780224, 19, False),
  (78780224, 98, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780224, 1, 33556445),
  (78780224, 2, 150994952),
  (78780224, 3, 536870919),
  (78780224, 4, 805306372),
  (78780224, 6, 67112812),
  (78780224, 7, 268435974),
  (78780224, 8, 100667445),
  (78780224, 22, 872415258),
  (78780224, 32, 80),
  (78780224, 35, 453);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780224, 1, 5.0),
  (78780224, 2, 0.0),
  (78780224, 3, 0.067),
  (78780224, 4, 5.0),
  (78780224, 5, 1.0),
  (78780224, 12, 1.0),
  (78780224, 13, 0.9),
  (78780224, 14, 1.0),
  (78780224, 15, 1.1),
  (78780224, 16, 0.6),
  (78780224, 17, 0.6),
  (78780224, 18, 1.0),
  (78780224, 19, 0.36),
  (78780224, 31, 10.0),
  (78780224, 34, 1.0),
  (78780224, 36, 1.0),
  (78780224, 39, 0.95),
  (78780224, 64, 0.86),
  (78780224, 65, 0.75),
  (78780224, 66, 0.66),
  (78780224, 67, 1.42),
  (78780224, 68, 1.42),
  (78780224, 69, 0.75),
  (78780224, 70, 1.42),
  (78780224, 71, 1.0),
  (78780224, 72, 1.0),
  (78780224, 73, 1.0),
  (78780224, 74, 1.0),
  (78780224, 75, 1.0),
  (78780224, 104, 10.0),
  (78780224, 125, 1.0),
  (78780224, 9060, 60.0);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780224, 1, 16),
  (78780224, 2, 3),
  (78780224, 6, -1),
  (78780224, 7, -1),
  (78780224, 25, 8),
  (78780224, 27, 0),
  (78780224, 40, 2),
  (78780224, 93, 1032),
  (78780224, 101, 131),
  (78780224, 140, 1),
  (78780224, 146, 1000),
  (78780224, 16, 32),
  (78780224, 95, 8),
  (78780224, 133, 4),
  (78780224, 134, 16),
  (78780224, 290, 1),
  (78780224, 291, 60),
  (78780224, 3, 67116297);
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`) VALUES
  (78780224, 6, 0, 3, 0, 5, 0, 0.0),
  (78780224, 7, 0, 3, 0, 15, 0, 0.0),
  (78780224, 15, 0, 3, 0, 5, 0, 0.0),
  (78780224, 24, 0, 3, 0, 40, 0, 0.0),
  (78780224, 44, 0, 3, 0, 5, 0, 0.0),
  (78780224, 45, 0, 3, 0, 5, 0, 0.0),
  (78780224, 46, 0, 3, 0, 5, 0, 0.0);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780224, 1, 'Subject Twelve');
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780224, 37, 0.3, NULL, NULL, NULL, 'annex_feud_b1', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 4.0, 0.0, NULL, 'Nobody over there has a nickname. Nobody.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

-- 78780225  The Sawato Situation
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780225, 'annex-ward-sawato-mutant', 10, NOW());
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
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`) VALUES
  (78780225, 0, 4, 0, 0.0, 540, 486, 486, 540, 540, 432, 486, 432, 0, 1, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0),
  (78780225, 1, 4, 0, 0.0, 390, 351, 351, 390, 390, 312, 351, 312, 0, 2, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0),
  (78780225, 2, 4, 0, 0.0, 390, 351, 351, 390, 390, 312, 351, 312, 0, 3, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0),
  (78780225, 3, 4, 0, 0.0, 390, 351, 351, 390, 390, 312, 351, 312, 0, 1, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0),
  (78780225, 4, 4, 0, 0.0, 390, 351, 351, 390, 390, 312, 351, 312, 0, 2, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0),
  (78780225, 5, 4, 200, 0.75, 300, 270, 270, 300, 300, 240, 270, 240, 0, 2, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0),
  (78780225, 6, 4, 0, 0.0, 390, 351, 351, 390, 390, 312, 351, 312, 0, 3, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18),
  (78780225, 7, 4, 0, 0.0, 390, 351, 351, 390, 390, 312, 351, 312, 0, 3, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6),
  (78780225, 8, 4, 200, 0.75, 320, 288, 288, 320, 320, 256, 288, 256, 0, 3, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22);
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780225, 1, True),
  (78780225, 11, False),
  (78780225, 12, True),
  (78780225, 13, True),
  (78780225, 14, True),
  (78780225, 19, False),
  (78780225, 98, True);
INSERT INTO `weenie_properties_create_list` (`object_Id`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`) VALUES
  (78780225, 2, 31704, 1, 20, 0.5, False);
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
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780225, 1, 5.0),
  (78780225, 3, 2.0),
  (78780225, 4, 5.0),
  (78780225, 5, 1.0),
  (78780225, 13, 0.9),
  (78780225, 14, 0.9),
  (78780225, 15, 1.0),
  (78780225, 16, 1.0),
  (78780225, 17, 0.8),
  (78780225, 18, 0.9),
  (78780225, 19, 0.8),
  (78780225, 31, 18.0),
  (78780225, 54, 0.5),
  (78780225, 55, 80.0),
  (78780225, 64, 0.6),
  (78780225, 65, 0.6),
  (78780225, 66, 0.6),
  (78780225, 67, 0.7),
  (78780225, 68, 0.6),
  (78780225, 69, 0.6),
  (78780225, 70, 0.7),
  (78780225, 80, 2.0),
  (78780225, 104, 10.0),
  (78780225, 122, 2.0),
  (78780225, 125, 1.0),
  (78780225, 9060, 60.0);
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
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`) VALUES
  (78780225, 6, 0, 2, 0, 350, 0, 0.0),
  (78780225, 7, 0, 2, 0, 380, 0, 0.0),
  (78780225, 15, 0, 2, 0, 360, 0, 0.0),
  (78780225, 20, 0, 1, 0, 0, 0, 0.0),
  (78780225, 24, 0, 1, 0, 0, 0, 0.0),
  (78780225, 44, 0, 2, 0, 400, 0, 0.0),
  (78780225, 45, 0, 2, 0, 400, 0, 0.0),
  (78780225, 46, 0, 2, 0, 400, 0, 0.0),
  (78780225, 47, 0, 2, 0, 350, 0, 0.0);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780225, 1, 'The Sawato Situation');
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

-- 78780230  Mubb
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780230, 'annex-mubb', 10, NOW());
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) VALUES
  (78780230, 1, 110, 0, 0),
  (78780230, 2, 100, 0, 0),
  (78780230, 3, 170, 0, 0),
  (78780230, 4, 120, 0, 0),
  (78780230, 5, 60, 0, 0),
  (78780230, 6, 60, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) VALUES
  (78780230, 1, 67, 0, 0, 117),
  (78780230, 3, 90, 0, 0, 190),
  (78780230, 5, 125, 0, 0, 185);
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`) VALUES
  (78780230, 0, 4, 0, 0.0, 120, 101, 77, 108, 101, 108, 101, 31, 0, 1, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0),
  (78780230, 1, 4, 0, 0.0, 110, 92, 70, 99, 92, 99, 92, 29, 0, 2, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0),
  (78780230, 2, 4, 0, 0.0, 110, 92, 70, 99, 92, 99, 92, 29, 0, 3, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0),
  (78780230, 3, 4, 0, 0.0, 115, 97, 74, 104, 97, 104, 97, 30, 0, 1, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0),
  (78780230, 4, 4, 0, 0.0, 115, 97, 74, 104, 97, 104, 97, 30, 0, 2, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0),
  (78780230, 5, 4, 25, 0.75, 110, 92, 70, 99, 92, 99, 92, 29, 0, 2, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0),
  (78780230, 6, 4, 0, 0.0, 110, 92, 70, 99, 92, 99, 92, 29, 0, 3, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18),
  (78780230, 7, 4, 0, 0.0, 110, 92, 70, 99, 92, 99, 92, 29, 0, 3, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6),
  (78780230, 8, 4, 25, 0.75, 110, 92, 70, 99, 92, 99, 92, 29, 0, 3, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22);
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780230, 6, True),
  (78780230, 11, False),
  (78780230, 12, True),
  (78780230, 13, False),
  (78780230, 14, True),
  (78780230, 50, True),
  (78780230, 1, True),
  (78780230, 19, False),
  (78780230, 98, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780230, 1, 33556445),
  (78780230, 2, 150994952),
  (78780230, 3, 536870919),
  (78780230, 4, 805306372),
  (78780230, 6, 67112812),
  (78780230, 7, 268435976),
  (78780230, 8, 100667445),
  (78780230, 22, 872415258),
  (78780230, 32, 71),
  (78780230, 35, 450);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780230, 1, 5.0),
  (78780230, 2, 0.0),
  (78780230, 3, 0.5),
  (78780230, 4, 3.0),
  (78780230, 5, 1.0),
  (78780230, 12, 0.5),
  (78780230, 13, 0.84),
  (78780230, 14, 0.64),
  (78780230, 15, 0.9),
  (78780230, 16, 0.84),
  (78780230, 17, 0.9),
  (78780230, 18, 0.84),
  (78780230, 19, 0.26),
  (78780230, 31, 24.0),
  (78780230, 34, 1.2),
  (78780230, 36, 1.0),
  (78780230, 39, 0.95),
  (78780230, 64, 0.9),
  (78780230, 65, 0.61),
  (78780230, 66, 1.0),
  (78780230, 67, 1.0),
  (78780230, 68, 0.9),
  (78780230, 69, 0.9),
  (78780230, 70, 0.23),
  (78780230, 71, 1.0),
  (78780230, 72, 1.0),
  (78780230, 73, 1.0),
  (78780230, 74, 1.0),
  (78780230, 75, 1.0),
  (78780230, 80, 3.0),
  (78780230, 104, 10.0),
  (78780230, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780230, 1, 16),
  (78780230, 2, 3),
  (78780230, 3, 51),
  (78780230, 6, -1),
  (78780230, 7, -1),
  (78780230, 25, 40),
  (78780230, 27, 0),
  (78780230, 40, 2),
  (78780230, 93, 1032),
  (78780230, 101, 131),
  (78780230, 140, 1),
  (78780230, 146, 7000),
  (78780230, 16, 32),
  (78780230, 95, 8),
  (78780230, 133, 4),
  (78780230, 134, 16),
  (78780230, 290, 1),
  (78780230, 291, 60);
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`) VALUES
  (78780230, 6, 0, 3, 0, 70, 0, 0.0),
  (78780230, 7, 0, 3, 0, 200, 0, 0.0),
  (78780230, 14, 0, 2, 0, 110, 0, 0.0),
  (78780230, 15, 0, 3, 0, 96, 0, 0.0),
  (78780230, 20, 0, 2, 0, 70, 0, 0.0),
  (78780230, 24, 0, 2, 0, 80, 0, 0.0),
  (78780230, 31, 0, 3, 0, 85, 0, 0.0),
  (78780230, 33, 0, 3, 0, 85, 0, 0.0),
  (78780230, 34, 0, 3, 0, 85, 0, 0.0),
  (78780230, 44, 0, 3, 0, 115, 0, 0.0),
  (78780230, 45, 0, 3, 0, 115, 0, 0.0),
  (78780230, 46, 0, 3, 0, 115, 0, 0.0),
  (78780230, 47, 0, 3, 0, 0, 0, 0.0);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780230, 1, 'Mubb'),
  (78780230, 45, 'KillTaskDrudgeLurkers_0507');
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780230, 37, 1.0, NULL, NULL, NULL, 'pat_neon_1', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 0.0, 0.0, NULL, 'He''s NEON, Gorta.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'pat_neon_2', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780230, 37, 1.0, NULL, NULL, NULL, 'pat_brighter_1', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 0.0, 0.0, NULL, 'Babies change colour, she says. Into NEON?', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'pat_brighter_2', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780230, 37, 1.0, NULL, NULL, NULL, 'pat_brighter_3', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 3.5, 0.0, NULL, 'He''s getting BRIGHTER.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780230, 37, 1.0, NULL, NULL, NULL, 'pat_splotch_1', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 0.0, 0.0, NULL, 'Bright. Smug. Loud. Who does THAT remind you of?', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'pat_splotch_2', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780230, 37, 1.0, NULL, NULL, NULL, 'pat_dj_1', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 0.0, 0.0, NULL, 'You were at HIS set every night that week!', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'pat_dj_2', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780230, 37, 1.0, NULL, NULL, NULL, 'pat_dj_3', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 3.0, 0.0, NULL, 'NOBODY likes the music.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'pat_dj_4', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 2, 88, 2.5, 0.0, NULL, 'pat_dj_4', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780230, 37, 1.0, NULL, NULL, NULL, 'pat_gary_1', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 0.0, 0.0, NULL, 'Was it Gary?', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'pat_gary_2', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780230, 37, 1.0, NULL, NULL, NULL, 'pat_gary_4', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 3.0, 0.0, NULL, 'THAT''S WHAT SHE SAID ABOUT THE DJ.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780230, 37, 1.0, NULL, NULL, NULL, 'pat_denton_2', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 3.5, 0.0, NULL, 'SEE? Even Denton thinks something happened!', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'pat_denton_3', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780230, 37, 1.0, NULL, NULL, NULL, 'pat_results_4', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 5.0, 0.0, NULL, '...So who''s the mutation FROM?', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'pat_results_5', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780230, 37, 1.0, NULL, NULL, NULL, 'pat_results_6', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 2.5, 0.0, NULL, 'WHO''S THE MUTATION FROM, GORTA.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'pat_results_7', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780230, 7, 0.2, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 10, 0.0, 0.0, NULL, 'Look at me. Now look at the baby. Now look at the DJ.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780230, 7, 0.4, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 10, 0.0, 0.0, NULL, 'I''ve narrowed it down to everyone.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780230, 7, 0.6, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 10, 0.0, 0.0, NULL, 'Nobody in my family glows. We checked. We checked twice.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780230, 7, 0.8, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 10, 0.0, 0.0, NULL, 'She says it''s a phase. Phases don''t have a colour.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780230, 7, 1.0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 10, 0.0, 0.0, NULL, 'If you know anything, you tell me first. Not her. Me.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

-- 78780231  Gorta
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780231, 'annex-gorta', 10, NOW());
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) VALUES
  (78780231, 1, 60, 0, 0),
  (78780231, 2, 60, 0, 0),
  (78780231, 3, 120, 0, 0),
  (78780231, 4, 90, 0, 0),
  (78780231, 5, 15, 0, 0),
  (78780231, 6, 15, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) VALUES
  (78780231, 1, 10, 0, 0, 40),
  (78780231, 3, 20, 0, 0, 80),
  (78780231, 5, 0, 0, 0, 15);
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`) VALUES
  (78780231, 0, 4, 0, 0.0, 3, 3, 3, 3, 2, 2, 3, 2, 0, 1, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0),
  (78780231, 1, 4, 0, 0.0, 7, 6, 7, 8, 4, 4, 7, 4, 0, 2, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0),
  (78780231, 2, 4, 0, 0.0, 7, 6, 7, 8, 4, 4, 7, 4, 0, 3, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0),
  (78780231, 3, 4, 0, 0.0, 5, 5, 5, 6, 3, 3, 5, 3, 0, 1, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0),
  (78780231, 4, 4, 0, 0.0, 7, 6, 7, 8, 4, 4, 7, 4, 0, 2, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0),
  (78780231, 5, 4, 2, 0.75, 5, 5, 5, 6, 3, 3, 5, 3, 0, 2, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0),
  (78780231, 6, 4, 0, 0.0, 5, 5, 5, 6, 3, 3, 5, 3, 0, 3, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18),
  (78780231, 7, 4, 0, 0.0, 5, 5, 5, 6, 3, 3, 5, 3, 0, 3, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6),
  (78780231, 8, 4, 3, 0.75, 5, 5, 5, 6, 3, 3, 5, 3, 0, 3, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22);
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780231, 11, False),
  (78780231, 12, True),
  (78780231, 13, False),
  (78780231, 14, True),
  (78780231, 1, True),
  (78780231, 19, False),
  (78780231, 98, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780231, 1, 33556445),
  (78780231, 2, 150994952),
  (78780231, 3, 536870919),
  (78780231, 4, 805306372),
  (78780231, 6, 67112812),
  (78780231, 7, 268435970),
  (78780231, 8, 100667445),
  (78780231, 22, 872415258),
  (78780231, 32, 82),
  (78780231, 35, 453);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780231, 1, 5.0),
  (78780231, 2, 0.0),
  (78780231, 3, 0.067),
  (78780231, 4, 5.0),
  (78780231, 5, 1.0),
  (78780231, 12, 0.5),
  (78780231, 13, 0.9),
  (78780231, 14, 1.0),
  (78780231, 15, 1.1),
  (78780231, 16, 0.6),
  (78780231, 17, 0.6),
  (78780231, 18, 1.0),
  (78780231, 19, 0.6),
  (78780231, 31, 10.0),
  (78780231, 34, 1.0),
  (78780231, 36, 1.0),
  (78780231, 39, 0.95),
  (78780231, 64, 0.86),
  (78780231, 65, 0.75),
  (78780231, 66, 0.66),
  (78780231, 67, 1.42),
  (78780231, 68, 1.42),
  (78780231, 69, 0.75),
  (78780231, 70, 1.42),
  (78780231, 71, 1.0),
  (78780231, 72, 1.0),
  (78780231, 73, 1.0),
  (78780231, 74, 1.0),
  (78780231, 75, 1.0),
  (78780231, 104, 10.0),
  (78780231, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780231, 1, 16),
  (78780231, 2, 3),
  (78780231, 3, 47),
  (78780231, 6, -1),
  (78780231, 7, -1),
  (78780231, 25, 8),
  (78780231, 27, 0),
  (78780231, 40, 2),
  (78780231, 93, 1032),
  (78780231, 101, 131),
  (78780231, 140, 1),
  (78780231, 146, 1000),
  (78780231, 16, 32),
  (78780231, 95, 8),
  (78780231, 133, 4),
  (78780231, 134, 16),
  (78780231, 290, 1),
  (78780231, 291, 60);
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`) VALUES
  (78780231, 6, 0, 3, 0, 10, 0, 0.0),
  (78780231, 7, 0, 3, 0, 20, 0, 0.0),
  (78780231, 15, 0, 3, 0, 9, 0, 0.0),
  (78780231, 24, 0, 3, 0, 40, 0, 0.0),
  (78780231, 44, 0, 3, 0, 10, 0, 0.0),
  (78780231, 45, 0, 3, 0, 10, 0, 0.0),
  (78780231, 46, 0, 3, 0, 10, 0, 0.0);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780231, 1, 'Gorta');
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780231, 37, 1.0, NULL, NULL, NULL, 'pat_neon_2', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 3.5, 0.0, NULL, 'Babies change colour.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'pat_neon_3', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780231, 37, 1.0, NULL, NULL, NULL, 'pat_brighter_2', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 3.5, 0.0, NULL, 'He''ll grow out of it.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'pat_brighter_3', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780231, 37, 1.0, NULL, NULL, NULL, 'pat_splotch_3', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 3.0, 0.0, NULL, 'He WISHES.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'pat_splotch_4', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780231, 37, 1.0, NULL, NULL, NULL, 'pat_dj_2', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 3.5, 0.0, NULL, 'I like the music!', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'pat_dj_3', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780231, 37, 1.0, NULL, NULL, NULL, 'pat_gary_2', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 3.0, 0.0, NULL, 'GARY??', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'pat_gary_3', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780231, 37, 1.0, NULL, NULL, NULL, 'pat_denton_3', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 3.0, 0.0, NULL, 'Denton thinks his sleeve is purple.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'pat_denton_4', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780231, 37, 1.0, NULL, NULL, NULL, 'pat_results_2', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 3.0, 0.0, NULL, 'I TOLD YOU!', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'pat_results_3', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780231, 37, 1.0, NULL, NULL, NULL, 'pat_results_5', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 2.5, 0.0, NULL, 'Don''t.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'pat_results_6', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780231, 7, 0.2, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 10, 0.0, 0.0, NULL, 'He''s the father. Tell him he''s the father.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780231, 7, 0.4, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 10, 0.0, 0.0, NULL, 'Twelve years, and he thinks I''d go near GARY.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780231, 7, 0.6, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 10, 0.0, 0.0, NULL, 'Babies change colour. Everybody knows babies change colour.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780231, 7, 0.8, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 10, 0.0, 0.0, NULL, 'If the Registry man comes by, we are NOT a case file.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780231, 7, 1.0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 10, 0.0, 0.0, NULL, 'Don''t look at the baby like that. That''s how Mubb started.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

-- 78780232  Mubb Junior
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780232, 'annex-baby', 10, NOW());
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) VALUES
  (78780232, 1, 70, 0, 0),
  (78780232, 2, 60, 0, 0),
  (78780232, 3, 110, 0, 0),
  (78780232, 4, 90, 0, 0),
  (78780232, 5, 15, 0, 0),
  (78780232, 6, 15, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) VALUES
  (78780232, 1, 12, 0, 0, 42),
  (78780232, 3, 20, 0, 0, 80),
  (78780232, 5, 0, 0, 0, 15);
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`) VALUES
  (78780232, 0, 4, 0, 0.0, 3, 3, 3, 3, 2, 2, 3, 1, 0, 1, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0),
  (78780232, 1, 4, 0, 0.0, 7, 6, 7, 8, 4, 4, 7, 3, 0, 2, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0),
  (78780232, 2, 4, 0, 0.0, 7, 6, 7, 8, 4, 4, 7, 3, 0, 3, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0),
  (78780232, 3, 4, 0, 0.0, 5, 5, 5, 6, 3, 3, 5, 2, 0, 1, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0),
  (78780232, 4, 4, 0, 0.0, 7, 6, 7, 8, 4, 4, 7, 3, 0, 2, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0),
  (78780232, 5, 4, 2, 0.75, 5, 5, 5, 6, 3, 3, 5, 2, 0, 2, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0),
  (78780232, 6, 4, 0, 0.0, 5, 5, 5, 6, 3, 3, 5, 2, 0, 3, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18),
  (78780232, 7, 4, 0, 0.0, 5, 5, 5, 6, 3, 3, 5, 2, 0, 3, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6),
  (78780232, 8, 4, 3, 0.75, 5, 5, 5, 6, 3, 3, 5, 2, 0, 3, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22);
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780232, 11, False),
  (78780232, 12, True),
  (78780232, 13, False),
  (78780232, 14, True),
  (78780232, 1, True),
  (78780232, 19, False),
  (78780232, 98, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780232, 1, 33556445),
  (78780232, 2, 150994952),
  (78780232, 3, 536870919),
  (78780232, 4, 805306372),
  (78780232, 6, 67112812),
  (78780232, 7, 268435974),
  (78780232, 8, 100667445),
  (78780232, 22, 872415258),
  (78780232, 32, 80),
  (78780232, 35, 453);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780232, 1, 5.0),
  (78780232, 2, 0.0),
  (78780232, 3, 0.067),
  (78780232, 4, 5.0),
  (78780232, 5, 1.0),
  (78780232, 12, 1.0),
  (78780232, 13, 0.9),
  (78780232, 14, 1.0),
  (78780232, 15, 1.1),
  (78780232, 16, 0.6),
  (78780232, 17, 0.6),
  (78780232, 18, 1.0),
  (78780232, 19, 0.36),
  (78780232, 31, 10.0),
  (78780232, 34, 1.0),
  (78780232, 36, 1.0),
  (78780232, 64, 0.86),
  (78780232, 65, 0.75),
  (78780232, 66, 0.66),
  (78780232, 67, 1.42),
  (78780232, 68, 1.42),
  (78780232, 69, 0.75),
  (78780232, 70, 1.42),
  (78780232, 71, 1.0),
  (78780232, 72, 1.0),
  (78780232, 73, 1.0),
  (78780232, 74, 1.0),
  (78780232, 75, 1.0),
  (78780232, 104, 10.0),
  (78780232, 125, 1.0),
  (78780232, 39, 0.55);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780232, 1, 16),
  (78780232, 2, 3),
  (78780232, 6, -1),
  (78780232, 7, -1),
  (78780232, 25, 8),
  (78780232, 27, 0),
  (78780232, 40, 2),
  (78780232, 93, 1032),
  (78780232, 101, 131),
  (78780232, 140, 1),
  (78780232, 146, 1000),
  (78780232, 16, 32),
  (78780232, 95, 8),
  (78780232, 133, 4),
  (78780232, 134, 16),
  (78780232, 290, 1),
  (78780232, 291, 60),
  (78780232, 3, 67113089);
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`) VALUES
  (78780232, 6, 0, 3, 0, 5, 0, 0.0),
  (78780232, 7, 0, 3, 0, 15, 0, 0.0),
  (78780232, 15, 0, 3, 0, 5, 0, 0.0),
  (78780232, 24, 0, 3, 0, 40, 0, 0.0),
  (78780232, 44, 0, 3, 0, 5, 0, 0.0),
  (78780232, 45, 0, 3, 0, 5, 0, 0.0),
  (78780232, 46, 0, 3, 0, 5, 0, 0.0);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780232, 1, 'Mubb Junior');
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780232, 37, 1.0, NULL, NULL, NULL, 'pat_neon_3', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 3.0, 0.0, NULL, 'Hi.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780232, 37, 1.0, NULL, NULL, NULL, 'pat_results_7', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 3.5, 0.0, NULL, 'I like my colour.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780232, 7, 0.25, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 10, 0.0, 0.0, NULL, 'I glow in the dark. Dad says it''s a phase.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780232, 7, 0.5, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 10, 0.0, 0.0, NULL, 'Why does Dad keep staring at Gary?', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780232, 7, 0.75, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 10, 0.0, 0.0, NULL, 'Mum says I''m a mystery.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780232, 7, 1.0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 10, 0.0, 0.0, NULL, 'Everyone yells when I''m around. I think it means they like me.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

-- 78780233  Denton
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780233, 'annex-denton', 10, NOW());
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) VALUES
  (78780233, 1, 280, 0, 0),
  (78780233, 2, 240, 0, 0),
  (78780233, 3, 150, 0, 0),
  (78780233, 4, 230, 0, 0),
  (78780233, 5, 350, 0, 0),
  (78780233, 6, 285, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) VALUES
  (78780233, 1, 100, 0, 0, 220),
  (78780233, 3, 151, 0, 0, 391),
  (78780233, 5, 10, 0, 0, 295);
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780233, 8, True),
  (78780233, 1, True),
  (78780233, 19, False),
  (78780233, 98, True);
INSERT INTO `weenie_properties_create_list` (`object_Id`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`) VALUES
  (78780233, 2, 10697, 1, 5, 0.3333, False),
  (78780233, 2, 130, 1, 5, 0.3333, False),
  (78780233, 2, 10696, 1, 5, 0.3333, False),
  (78780233, 2, 120, 1, 93, 0.9818, False),
  (78780233, 2, 132, 1, 5, 0.6667, False);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780233, 1, 33554433),
  (78780233, 2, 150994945),
  (78780233, 3, 536870913),
  (78780233, 6, 67108990),
  (78780233, 8, 100667446);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780233, 54, 3.0);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780233, 1, 16),
  (78780233, 2, 31),
  (78780233, 6, -1),
  (78780233, 7, -1),
  (78780233, 25, 84),
  (78780233, 93, 6292504),
  (78780233, 113, 1),
  (78780233, 188, 1),
  (78780233, 16, 32),
  (78780233, 95, 8),
  (78780233, 133, 4),
  (78780233, 134, 16),
  (78780233, 290, 1),
  (78780233, 291, 60);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780233, 1, 'Denton'),
  (78780233, 5, 'Merchant');
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780233, 37, 1.0, NULL, NULL, NULL, 'pat_denton_1', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 0.0, 0.0, NULL, 'Nobody touch the baby. That''s how it SPREADS.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 0.0, 0.0, NULL, 'pat_denton_2', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780233, 37, 1.0, NULL, NULL, NULL, 'pat_denton_4', NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 8, 3.5, 0.0, NULL, '...Is it?', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780233, 7, 0.25, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 10, 0.0, 0.0, NULL, 'Don''t stand too close to room four. It''s in the air.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780233, 7, 0.5, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 10, 0.0, 0.0, NULL, 'I washed my hands six times today. Do they look purple? Don''t look at them.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780233, 7, 0.75, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 10, 0.0, 0.0, NULL, 'Mutations are contagious. The Registry won''t confirm it, which is basically confirming it.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780233, 7, 1.0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 10, 0.0, 0.0, NULL, 'Stay on the brown side of the hallway. It''s safer.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

-- 78780240  Annex Scene Director
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780240, 'annex-scene-director', 10, NOW());
INSERT INTO `weenie_properties_attribute` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`) VALUES
  (78780240, 1, 70, 0, 0),
  (78780240, 2, 60, 0, 0),
  (78780240, 3, 110, 0, 0),
  (78780240, 4, 90, 0, 0),
  (78780240, 5, 15, 0, 0),
  (78780240, 6, 15, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`,`type`,`init_Level`,`level_From_C_P`,`c_P_Spent`,`current_Level`) VALUES
  (78780240, 1, 12, 0, 0, 42),
  (78780240, 3, 20, 0, 0, 80),
  (78780240, 5, 0, 0, 0, 15);
INSERT INTO `weenie_properties_body_part` (`object_Id`,`key`,`d_Type`,`d_Val`,`d_Var`,`base_Armor`,`armor_Vs_Slash`,`armor_Vs_Pierce`,`armor_Vs_Bludgeon`,`armor_Vs_Cold`,`armor_Vs_Fire`,`armor_Vs_Acid`,`armor_Vs_Electric`,`armor_Vs_Nether`,`b_h`,`h_l_f`,`m_l_f`,`l_l_f`,`h_r_f`,`m_r_f`,`l_r_f`,`h_l_b`,`m_l_b`,`l_l_b`,`h_r_b`,`m_r_b`,`l_r_b`) VALUES
  (78780240, 0, 4, 0, 0.0, 3, 3, 3, 3, 2, 2, 3, 1, 0, 1, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0, 0.33, 0.0, 0.0),
  (78780240, 1, 4, 0, 0.0, 7, 6, 7, 8, 4, 4, 7, 3, 0, 2, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0, 0.44, 0.17, 0.0),
  (78780240, 2, 4, 0, 0.0, 7, 6, 7, 8, 4, 4, 7, 3, 0, 3, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0, 0.0, 0.17, 0.0),
  (78780240, 3, 4, 0, 0.0, 5, 5, 5, 6, 3, 3, 5, 2, 0, 1, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0, 0.23, 0.03, 0.0),
  (78780240, 4, 4, 0, 0.0, 7, 6, 7, 8, 4, 4, 7, 3, 0, 2, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0, 0.0, 0.3, 0.0),
  (78780240, 5, 4, 2, 0.75, 5, 5, 5, 6, 3, 3, 5, 2, 0, 2, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0),
  (78780240, 6, 4, 0, 0.0, 5, 5, 5, 6, 3, 3, 5, 2, 0, 3, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18, 0.0, 0.13, 0.18),
  (78780240, 7, 4, 0, 0.0, 5, 5, 5, 6, 3, 3, 5, 2, 0, 3, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6, 0.0, 0.0, 0.6),
  (78780240, 8, 4, 3, 0.75, 5, 5, 5, 6, 3, 3, 5, 2, 0, 3, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22, 0.0, 0.0, 0.22);
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780240, 11, False),
  (78780240, 12, True),
  (78780240, 13, False),
  (78780240, 14, True),
  (78780240, 1, True),
  (78780240, 19, False),
  (78780240, 98, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780240, 1, 33556445),
  (78780240, 2, 150994952),
  (78780240, 3, 536870919),
  (78780240, 4, 805306372),
  (78780240, 6, 67112812),
  (78780240, 7, 268435974),
  (78780240, 8, 100667445),
  (78780240, 22, 872415258),
  (78780240, 32, 80),
  (78780240, 35, 453);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780240, 1, 5.0),
  (78780240, 2, 0.0),
  (78780240, 3, 0.067),
  (78780240, 4, 5.0),
  (78780240, 5, 1.0),
  (78780240, 12, 1.0),
  (78780240, 13, 0.9),
  (78780240, 14, 1.0),
  (78780240, 15, 1.1),
  (78780240, 16, 0.6),
  (78780240, 17, 0.6),
  (78780240, 18, 1.0),
  (78780240, 19, 0.36),
  (78780240, 31, 10.0),
  (78780240, 34, 1.0),
  (78780240, 36, 1.0),
  (78780240, 64, 0.86),
  (78780240, 65, 0.75),
  (78780240, 66, 0.66),
  (78780240, 67, 1.42),
  (78780240, 68, 1.42),
  (78780240, 69, 0.75),
  (78780240, 70, 1.42),
  (78780240, 71, 1.0),
  (78780240, 72, 1.0),
  (78780240, 73, 1.0),
  (78780240, 74, 1.0),
  (78780240, 75, 1.0),
  (78780240, 104, 10.0),
  (78780240, 125, 1.0),
  (78780240, 39, 0.05);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780240, 1, 16),
  (78780240, 2, 3),
  (78780240, 3, 48),
  (78780240, 6, -1),
  (78780240, 7, -1),
  (78780240, 25, 8),
  (78780240, 27, 0),
  (78780240, 40, 2),
  (78780240, 93, 1032),
  (78780240, 101, 131),
  (78780240, 140, 1),
  (78780240, 146, 1000),
  (78780240, 16, 1),
  (78780240, 133, 1),
  (78780240, 134, 16);
INSERT INTO `weenie_properties_skill` (`object_Id`,`type`,`level_From_P_P`,`s_a_c`,`p_p`,`init_Level`,`resistance_At_Last_Check`,`last_Used_Time`) VALUES
  (78780240, 6, 0, 3, 0, 5, 0, 0.0),
  (78780240, 7, 0, 3, 0, 15, 0, 0.0),
  (78780240, 15, 0, 3, 0, 5, 0, 0.0),
  (78780240, 24, 0, 3, 0, 40, 0, 0.0),
  (78780240, 44, 0, 3, 0, 5, 0, 0.0),
  (78780240, 45, 0, 3, 0, 5, 0, 0.0),
  (78780240, 46, 0, 3, 0, 5, 0, 0.0);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780240, 1, 'Annex Scene Director');
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780240, 5, 0.003, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 88, 0.0, 0.0, NULL, 'pat_results_1', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 120.0, 0.0, NULL, 'pat_rest', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780240, 5, 0.01, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 88, 0.0, 0.0, NULL, 'pat_denton_1', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 120.0, 0.0, NULL, 'pat_rest', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780240, 5, 0.018, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 88, 0.0, 0.0, NULL, 'pat_neon_1', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 120.0, 0.0, NULL, 'pat_rest', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780240, 5, 0.026, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 88, 0.0, 0.0, NULL, 'pat_brighter_1', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 120.0, 0.0, NULL, 'pat_rest', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780240, 5, 0.034, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 88, 0.0, 0.0, NULL, 'pat_splotch_1', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 120.0, 0.0, NULL, 'pat_rest', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780240, 5, 0.042, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 88, 0.0, 0.0, NULL, 'pat_dj_1', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 120.0, 0.0, NULL, 'pat_rest', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`weenie_Class_Id`,`style`,`substyle`,`quest`,`vendor_Type`,`min_Health`,`max_Health`,`damage_type`) VALUES (78780240, 5, 0.05, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`motion`,`message`,`test_String`,`min`,`max`,`min_64`,`max_64`,`min_Dbl`,`max_Dbl`,`stat`,`display`,`amount`,`amount_64`,`hero_X_P_64`,`percent`,`spell_Id`,`wealth_Rating`,`treasure_Class`,`treasure_Type`,`p_Script`,`sound`,`destination_Type`,`weenie_Class_Id`,`stack_Size`,`palette`,`shade`,`try_To_Bond`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (@e, 0, 88, 0.0, 0.0, NULL, 'pat_gary_1', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (@e, 1, 88, 120.0, 0.0, NULL, 'pat_rest', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

-- 78780250  Lesser Courtship Incense
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780250, 'ace78780250-lessercourtshipincense', 44, NOW());
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780250, 11, True),
  (78780250, 13, True),
  (78780250, 14, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780250, 1, 33558818),
  (78780250, 8, 100670744),
  (78780250, 52, 100689404);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780250, 1, 128),
  (78780250, 5, 5),
  (78780250, 8, 5),
  (78780250, 11, 100),
  (78780250, 12, 1),
  (78780250, 13, 1),
  (78780250, 14, 1),
  (78780250, 16, 524296),
  (78780250, 18, 10),
  (78780250, 19, 1250000),
  (78780250, 93, 1044),
  (78780250, 94, 128);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780250, 1, 'Lesser Courtship Incense'),
  (78780250, 14, 'Use on a combat pet essence before breeding to prime it. Grants up to +2.5% mutation chance on the next breeding attempt; less on a heavily mutated line.'),
  (78780250, 15, 'A fragrant ceremonial incense stick.'),
  (78780250, 16, 'A fragrant ceremonial incense stick.');

-- 78780251  Refined Courtship Incense
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780251, 'ace78780251-refinedcourtshipincense', 44, NOW());
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780251, 11, True),
  (78780251, 13, True),
  (78780251, 14, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780251, 1, 33558818),
  (78780251, 22, 872415275),
  (78780251, 8, 100670743),
  (78780251, 52, 100689404);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780251, 1, 128),
  (78780251, 5, 5),
  (78780251, 8, 5),
  (78780251, 11, 100),
  (78780251, 12, 1),
  (78780251, 13, 1),
  (78780251, 14, 1),
  (78780251, 16, 524296),
  (78780251, 18, 10),
  (78780251, 19, 2500000),
  (78780251, 93, 1044),
  (78780251, 94, 128);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780251, 1, 'Refined Courtship Incense'),
  (78780251, 14, 'Use on a combat pet essence before breeding to prime it. Grants up to +5.0% mutation chance on the next breeding attempt; less on a heavily mutated line.'),
  (78780251, 15, 'A potent ceremonial incense stick.'),
  (78780251, 16, 'A potent ceremonial incense stick.');

-- 78780252  Exquisite Courtship Incense
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780252, 'ace78780252-exquisitecourtshipincense', 44, NOW());
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780252, 11, True),
  (78780252, 13, True),
  (78780252, 14, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780252, 1, 33558818),
  (78780252, 8, 100670742),
  (78780252, 52, 100689404);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780252, 1, 128),
  (78780252, 5, 5),
  (78780252, 8, 5),
  (78780252, 11, 100),
  (78780252, 12, 1),
  (78780252, 13, 1),
  (78780252, 14, 1),
  (78780252, 16, 524296),
  (78780252, 18, 10),
  (78780252, 19, 5000000),
  (78780252, 93, 1044),
  (78780252, 94, 128);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780252, 1, 'Exquisite Courtship Incense'),
  (78780252, 14, 'Use on a combat pet essence before breeding to prime it. Grants up to +10.0% mutation chance on the next breeding attempt; less on a heavily mutated line.'),
  (78780252, 15, 'An exquisite masterwork incense stick.'),
  (78780252, 16, 'An exquisite masterwork incense stick.');

-- 78780253  Nurturing Draught
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780253, 'ace78780253-nurturingdraught', 44, NOW());
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780253, 11, True),
  (78780253, 13, True),
  (78780253, 14, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780253, 1, 33554446),
  (78780253, 8, 100670839),
  (78780253, 52, 100676546);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780253, 1, 128),
  (78780253, 5, 10),
  (78780253, 8, 10),
  (78780253, 11, 100),
  (78780253, 12, 1),
  (78780253, 13, 1),
  (78780253, 14, 1),
  (78780253, 16, 524296),
  (78780253, 18, 10),
  (78780253, 19, 2500000),
  (78780253, 93, 1044),
  (78780253, 94, 128);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780253, 1, 'Nurturing Draught'),
  (78780253, 14, 'Feed to a juvenile combat pet essence. Doubles the maturity kill credit it earns until it reaches adulthood.'),
  (78780253, 15, 'A glowing elixir that stimulates pet growth.'),
  (78780253, 16, 'A glowing elixir that stimulates pet growth.');

-- 78780254  Chromatic Catalyst
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780254, 'ace78780254-chromaticcatalyst', 44, NOW());
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780254, 11, True),
  (78780254, 13, True),
  (78780254, 14, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780254, 1, 33558818),
  (78780254, 8, 100686621),
  (78780254, 52, 100676443);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780254, 1, 128),
  (78780254, 5, 10),
  (78780254, 8, 10),
  (78780254, 11, 100),
  (78780254, 12, 1),
  (78780254, 13, 1),
  (78780254, 14, 1),
  (78780254, 16, 524296),
  (78780254, 18, 10),
  (78780254, 19, 2500000),
  (78780254, 93, 1044),
  (78780254, 94, 128);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780254, 1, 'Chromatic Catalyst'),
  (78780254, 14, 'Use on a combat pet essence before breeding. If a palette mutation occurs, it is guaranteed to select from rare, vibrant, high-saturation colors.'),
  (78780254, 15, 'An alchemical prism that refracts pure chroma.'),
  (78780254, 16, 'An alchemical prism that refracts pure chroma.');

-- 78780255  Offering of Subjugation
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780255, 'ace78780255-offeringofsubjugation', 44, NOW());
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780255, 11, True),
  (78780255, 13, True),
  (78780255, 14, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780255, 1, 33558818),
  (78780255, 8, 100675792),
  (78780255, 52, 100683040);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780255, 1, 128),
  (78780255, 5, 10),
  (78780255, 8, 10),
  (78780255, 11, 100),
  (78780255, 12, 1),
  (78780255, 13, 1),
  (78780255, 14, 1),
  (78780255, 16, 524296),
  (78780255, 18, 10),
  (78780255, 19, 2500000),
  (78780255, 93, 1044),
  (78780255, 94, 128);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780255, 1, 'Offering of Subjugation'),
  (78780255, 14, 'Use on a combat pet essence before breeding. If the litter mutates and its spirit rises, the spirit is badly weakened: it hits for half, takes 2.5x damage, and each blow can take a quarter of its health. Consumed from both parents when a spirit appears.'),
  (78780255, 15, 'Use on a combat pet essence before breeding. If the litter mutates and its spirit rises, the spirit is badly weakened: it hits for half, takes 2.5x damage, and each blow can take a quarter of its health. Consumed from both parents when a spirit appears.'),
  (78780255, 16, 'Use on a combat pet essence before breeding. If the litter mutates and its spirit rises, the spirit is badly weakened: it hits for half, takes 2.5x damage, and each blow can take a quarter of its health. Consumed from both parents when a spirit appears.');

-- 78780257  Mutagenic Serum
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780257, 'ace78780257-mutagenicserum', 44, NOW());
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780257, 11, True),
  (78780257, 13, True),
  (78780257, 14, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780257, 1, 33554446),
  (78780257, 8, 100672518),
  (78780257, 52, 100689403);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780257, 1, 128),
  (78780257, 5, 10),
  (78780257, 8, 10),
  (78780257, 11, 50),
  (78780257, 12, 1),
  (78780257, 13, 1),
  (78780257, 14, 1),
  (78780257, 16, 524296),
  (78780257, 18, 10),
  (78780257, 19, 25000000),
  (78780257, 93, 1044),
  (78780257, 94, 128);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780257, 1, 'Mutagenic Serum'),
  (78780257, 14, 'Use on any combat pet essence to re-roll its colour from the full mutation palette pool. Changes appearance only: stats, mutations and potency are untouched. Re-summon the pet to see its new colour.'),
  (78780257, 15, 'A cloudy serum that shifts a creature''s colouring without touching its nature.'),
  (78780257, 16, 'A cloudy serum that shifts a creature''s colouring without touching its nature.');

-- 78780258  Pet Neutering Kit
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780258, 'ace78780258-petneuteringkit', 44, NOW());
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780258, 11, True),
  (78780258, 13, True),
  (78780258, 14, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780258, 1, 33558818),
  (78780258, 6, 67115262),
  (78780258, 7, 268436836),
  (78780258, 22, 872415275),
  (78780258, 8, 100671428),
  (78780258, 52, 100676546);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780258, 1, 128),
  (78780258, 5, 25),
  (78780258, 8, 25),
  (78780258, 11, 100),
  (78780258, 12, 1),
  (78780258, 13, 1),
  (78780258, 14, 1),
  (78780258, 16, 524296),
  (78780258, 18, 10),
  (78780258, 19, 2500000),
  (78780258, 93, 1044),
  (78780258, 94, 128);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780258, 1, 'Pet Neutering Kit'),
  (78780258, 14, 'Use on a combat pet essence to permanently spay or neuter it. The essence can never be used for breeding again. This cannot be undone.'),
  (78780258, 15, 'A pet neutering kit.'),
  (78780258, 16, 'A pet neutering kit.');

-- 78780259  Pet Tailoring Kit
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780259, 'ace78780259-pettailoringkit', 44, NOW());
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780259, 11, True),
  (78780259, 13, True),
  (78780259, 14, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780259, 1, 33558818),
  (78780259, 6, 67115262),
  (78780259, 7, 268436836),
  (78780259, 22, 872415275),
  (78780259, 8, 100690891),
  (78780259, 52, 100689403);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780259, 1, 128),
  (78780259, 5, 25),
  (78780259, 8, 25),
  (78780259, 11, 10),
  (78780259, 12, 1),
  (78780259, 13, 1),
  (78780259, 14, 1),
  (78780259, 16, 524296),
  (78780259, 18, 10),
  (78780259, 19, 125000000),
  (78780259, 93, 1044),
  (78780259, 94, 128);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780259, 1, 'Pet Tailoring Kit'),
  (78780259, 14, 'Use on a combat pet essence to extract its entire appearance into this kit. The source essence is consumed. The filled kit can then be applied to another combat pet essence, which keeps its own stats, potency, bond and lineage and takes on only the look. Dismiss the pet first.'),
  (78780259, 15, 'A pet tailoring kit.'),
  (78780259, 16, 'A pet tailoring kit.');

-- 78780260  Pet Tailoring Kit (Filled)
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780260, 'ace78780260-pettailoringkitfilled', 44, NOW());
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780260, 11, True),
  (78780260, 13, True),
  (78780260, 14, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780260, 1, 33558818),
  (78780260, 6, 67115262),
  (78780260, 7, 268436836),
  (78780260, 22, 872415275),
  (78780260, 8, 100693217),
  (78780260, 52, 100689403);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780260, 1, 128),
  (78780260, 5, 25),
  (78780260, 8, 25),
  (78780260, 11, 1),
  (78780260, 12, 1),
  (78780260, 13, 1),
  (78780260, 14, 1),
  (78780260, 16, 524296),
  (78780260, 18, 10),
  (78780260, 19, 125000000),
  (78780260, 93, 1044),
  (78780260, 94, 128);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780260, 1, 'Pet Tailoring Kit (Filled)'),
  (78780260, 14, 'Holds the complete appearance of a combat pet: model, colours, size, name and equipment. Use on a combat pet essence to tailor that look onto it. Only the appearance changes. Dismiss the pet first. Consumed on use.'),
  (78780260, 15, 'A filled pet tailoring kit.'),
  (78780260, 16, 'A filled pet tailoring kit.');

-- 78780261  Portal to Prof. Ruggan
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780261, 'annex_exit_portal', 7, NOW());
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780261, 1, True),
  (78780261, 11, False),
  (78780261, 12, True),
  (78780261, 13, True),
  (78780261, 15, True),
  (78780261, 63, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780261, 1, 33554867),
  (78780261, 2, 150994947),
  (78780261, 8, 100667499);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780261, 54, -0.1);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780261, 1, 65536),
  (78780261, 16, 32),
  (78780261, 93, 3084),
  (78780261, 111, 1),
  (78780261, 133, 4),
  (78780261, 150, 3);
INSERT INTO `weenie_properties_position` (`object_Id`,`position_Type`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`,`variation_Id`) VALUES
  (78780261, 2, 3678076953, 80.2836, 18.1845, 30.6953, -0.967966, 0.0, 0.0, -0.251082, NULL);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780261, 1, 'Portal to Prof. Ruggan'),
  (78780261, 14, 'Double click this portal to return to Prof. Ruggan.');

-- 78780262  Solidifying Tincture
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780262, 'ace78780262-solidifyingtincture', 44, NOW());
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780262, 11, True),
  (78780262, 13, True),
  (78780262, 14, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780262, 1, 33554446),
  (78780262, 8, 100670839),
  (78780262, 52, 100689404);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780262, 1, 128),
  (78780262, 5, 10),
  (78780262, 8, 10),
  (78780262, 11, 100),
  (78780262, 12, 1),
  (78780262, 13, 1),
  (78780262, 14, 1),
  (78780262, 16, 524296),
  (78780262, 18, 10),
  (78780262, 19, 12500000),
  (78780262, 93, 1044),
  (78780262, 94, 128);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780262, 1, 'Solidifying Tincture'),
  (78780262, 14, 'Use on a combat pet essence to make its pet 10% less see-through, down to fully solid. Maidens and K''nath start at 50%. Summon the pet again to see the change. Does nothing for a look that is itself translucent.'),
  (78780262, 15, 'A thick, cloudy tincture that makes spectral things opaque.'),
  (78780262, 16, 'A thick, cloudy tincture that makes spectral things opaque.');

-- 78780263  Fading Tincture
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780263, 'ace78780263-fadingtincture', 44, NOW());
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780263, 11, True),
  (78780263, 13, True),
  (78780263, 14, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780263, 1, 33554446),
  (78780263, 8, 100670839),
  (78780263, 52, 100689403);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780263, 1, 128),
  (78780263, 5, 10),
  (78780263, 8, 10),
  (78780263, 11, 100),
  (78780263, 12, 1),
  (78780263, 13, 1),
  (78780263, 14, 1),
  (78780263, 16, 524296),
  (78780263, 18, 10),
  (78780263, 19, 12500000),
  (78780263, 93, 1044),
  (78780263, 94, 128);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780263, 1, 'Fading Tincture'),
  (78780263, 14, 'Use on a combat pet essence to make its pet 10% more see-through, up to 50%. Summon the pet again to see the change.'),
  (78780263, 15, 'A thin, pale tincture that lets the light through whatever drinks it.'),
  (78780263, 16, 'A thin, pale tincture that lets the light through whatever drinks it.');

-- 78780264  Annex Prismatic Generator
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (78780264, 'annex_prismatic_generator', 1, NOW());
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (78780264, 1, True),
  (78780264, 11, True),
  (78780264, 18, True),
  (78780264, 132, True),
  (78780264, 50057, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (78780264, 1, 33555051),
  (78780264, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780264, 41, 20.0),
  (78780264, 43, 5.0),
  (78780264, 9061, 0.5);
INSERT INTO `weenie_properties_generator` (`object_Id`,`probability`,`weenie_Class_Id`,`delay`,`init_Create`,`max_Create`,`when_Create`,`where_Create`,`stack_Size`,`palette_Id`,`shade`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`) VALUES
  (78780264, -1.0, 260031, 1.0, 10, 10, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (78780264, 81, 10),
  (78780264, 82, 10),
  (78780264, 93, 1044);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (78780264, 1, 'Annex Prismatic Generator');

-- 98760388  Portal to Seedy Motel
INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (98760388, 'seedy_motel_portal', 7, NOW());
INSERT INTO `weenie_properties_bool` (`object_Id`,`type`,`value`) VALUES
  (98760388, 1, True),
  (98760388, 11, False),
  (98760388, 12, True),
  (98760388, 13, True),
  (98760388, 15, True),
  (98760388, 63, True);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`,`type`,`value`) VALUES
  (98760388, 1, 33554867),
  (98760388, 2, 150994947),
  (98760388, 8, 100667499);
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (98760388, 54, -0.1);
INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES
  (98760388, 1, 65536),
  (98760388, 16, 32),
  (98760388, 93, 3084),
  (98760388, 111, 1),
  (98760388, 133, 4),
  (98760388, 150, 3);
INSERT INTO `weenie_properties_position` (`object_Id`,`position_Type`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,`angles_W`,`angles_X`,`angles_Y`,`angles_Z`,`variation_Id`) VALUES
  (98760388, 2, 17170822, 35.98, -20.044, 0.005, 0.710829, 0.0, 0.0, -0.703365, 2);
INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES
  (98760388, 1, 'Portal to Seedy Motel'),
  (98760388, 14, 'Double click this portal to travel to the Seedy Motel.');

/* ---- PLACEMENTS ------------------------------------------------------------------- */

-- The annex: 24 placement(s).
DELETE FROM `landblock_instance` WHERE `landblock` = 0x0106 AND `variation_Id` = 2 AND `weenie_Class_Id` BETWEEN 78780200 AND 78780299;
SET @g = (SELECT COALESCE(MAX(`guid`), 0x70105FFF) FROM `landblock_instance` WHERE `guid` BETWEEN 0x70106000 AND 0x70106FFF);
SET @g = IF(@g + 24 > 1880125439, (SELECT 1 UNION SELECT 2), @g); -- 1880125439 = 0x70106FFF, the top of this landblock's range
INSERT INTO `landblock_instance` (`guid`,`weenie_Class_Id`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,
  `angles_W`,`angles_X`,`angles_Y`,`angles_Z`,`is_Link_Child`,`last_Modified`,`variation_Id`) VALUES
  (@g + 1, 78780264, 0x01060163, 79.9978, -58.5984, -5.945, 0.999963, 0.0, 0.0, -0.00857348, False, NOW(), 2), -- Annex Prismatic Generator
  (@g + 2, 78780222, 0x0106019D, 55.1495, -23.873, 0.00200051, 0.999258, 0.0, 0.0, -0.0385069, False, NOW(), 2), -- Ursuin, Unregistered
  (@g + 3, 78780223, 0x0106018F, 45.0584, -24.4, -0.0149994, 0.99995, 0.0, 0.0, 0.00997638, False, NOW(), 2), -- Nine-Colour Shreth
  (@g + 4, 78780224, 0x010601A6, 73.9587, -23.4926, 0.0033254, 0.888357, 0.0, 0.0, 0.459154, False, NOW(), 2), -- Subject Twelve
  (@g + 5, 78780212, 0x010601A6, 65.8673, -16.3726, -0.00299972, -0.00243423, 0.0, 0.0, -0.999997, False, NOW(), 2), -- Certified Shreth, Third Generation
  (@g + 6, 78780213, 0x0106018F, 54.9419, -16.127, 0.00200051, -0.00429613, 0.0, 0.0, -0.999991, False, NOW(), 2), -- Pedigreed Ursuin (Papers Pending)
  (@g + 7, 78780214, 0x010601A6, 73.7327, -16.1424, 0.00332546, 0.46581, 0.0, 0.0, 0.884885, False, NOW(), 2), -- Drudge Skulker of Record
  (@g + 8, 78780233, 0x0106018F, 49.6757, -19.6893, 0.00500047, -0.694313, 0.0, 0.0, -0.719673, False, NOW(), 2), -- Denton
  (@g + 9, 78780200, 0x01060186, 41.1518, -21.1851, 0.00500041, -0.746286, 0.0, 0.0, -0.665626, False, NOW(), 2), -- Fenwick, Kennel Intern
  (@g + 10, 78780204, 0x01060186, 41.3706, -18.8561, 0.00500041, -0.609131, 0.0, 0.0, -0.79307, False, NOW(), 2), -- Mrs. Ruggan
  (@g + 11, 78780220, 0x01060186, 36.2615, -23.7276, 0.0113304, -0.929735, 0.0, 0.0, 0.368228, False, NOW(), 2), -- Splotch
  (@g + 12, 78780202, 0x010601A6, 74.0187, -18.4773, 0.0033254, 0.65115, 0.0, 0.0, 0.758949, False, NOW(), 2), -- DJ Skulk
  (@g + 13, 78780203, 0x01060186, 36.0829, -16.145, 0.0113304, -0.372708, 0.0, 0.0, 0.927949, False, NOW(), 2), -- Gary
  (@g + 14, 78780210, 0x01060187, 38.2976, -32.6147, 0.00500005, 0.996455, 0.0, 0.0, -0.0841315, False, NOW(), 2), -- Bexley, Keeper of the Registry
  (@g + 15, 78780230, 0x01060182, 38.6779, -6.44777, 0.00332493, 0.252831, 0.0, 0.0, -0.967511, False, NOW(), 2), -- Mubb
  (@g + 16, 78780231, 0x01060182, 41.2217, -6.2459, 0.00332493, -0.193173, 0.0, 0.0, -0.981165, False, NOW(), 2), -- Gorta
  (@g + 17, 78780232, 0x01060182, 40.0049, -6.92743, 0.00192499, -0.0068951, 0.0, 0.0, -0.999976, False, NOW(), 2), -- Mubb Junior
  (@g + 18, 78780223, 0x010601A6, 65.5085, -24.2238, -0.0112495, 0.999373, 0.0, 0.0, 0.0354174, False, NOW(), 2), -- Nine-Colour Shreth
  (@g + 19, 78780201, 0x01060187, 43.0561, -32.6733, 0.00500005, 0.976197, 0.0, 0.0, 0.216888, False, NOW(), 2), -- Ivo, Ruggan's Quartermaster
  (@g + 20, 78780240, 0x01060178, 33.6161, -20.5616, 0.000174951, -0.727399, 0.0, 0.0, -0.686215, False, NOW(), 2), -- Annex Scene Director
  (@g + 21, 78780225, 0x0106018F, 45.0388, -24.5199, 0.00500041, -0.999689, 0.0, 0.0, -0.0249216, False, NOW(), 2), -- The Sawato Situation
  (@g + 22, 78780215, 0x0106018F, 45.1932, -15.4979, 0.00500041, -0.0127857, 0.0, 0.0, 0.999918, False, NOW(), 2), -- Certified Sawato Bandit, Reformed
  (@g + 23, 78780261, 0x01060179, 29.7858, -30.0347, 0.005, -0.999403, 0.0, 0.0, -0.03454, False, NOW(), 2), -- Portal to Prof. Ruggan
  (@g + 24, 78780206, 0x01060186, 39.0, -23.0, 0.05, 0.707107, 0.707107, 0.0, 0.0, False, NOW(), 2); -- Fallen Sign

COMMIT;
SET SQL_SAFE_UPDATES = @__old_safe_updates;

/* V1. Expect 38. */
SELECT COUNT(*) AS bundle_weenies FROM `weenie` WHERE `class_Id` IN (78780200,78780201,78780202,78780203,78780204,78780206,78780210,78780211,78780212,78780213,78780214,78780215,78780220,78780221,78780222,78780223,78780224,78780225,78780230,78780231,78780232,78780233,78780240,78780250,78780251,78780252,78780253,78780254,78780255,78780257,78780258,78780259,78780260,78780261,78780262,78780263,78780264,98760388);

/* V2. Expect 24. */
SELECT COUNT(*) AS bundle_placements FROM `landblock_instance` WHERE `landblock` = 0x0106 AND `variation_Id` = 2
  AND `weenie_Class_Id` BETWEEN 78780200 AND 78780299;

/* V2b. The motel portal must still be placed (this bundle does not move it). Expect at least one row. */
SELECT HEX(`guid`) AS guid, HEX(`obj_Cell_Id`) AS cell, `origin_X`, `origin_Y`, `origin_Z` FROM `landblock_instance`
WHERE `weenie_Class_Id` = 98760388;

/* V3. Placements of a weenie that does not exist. Expect ZERO rows. */
SELECT li.`weenie_Class_Id`, HEX(li.`landblock`) AS landblock, li.`variation_Id` FROM `landblock_instance` li
LEFT JOIN `weenie` w ON w.`class_Id` = li.`weenie_Class_Id`
WHERE w.`class_Id` IS NULL AND (li.`weenie_Class_Id` BETWEEN 78780200 AND 78780299 OR li.`weenie_Class_Id` = 98760388);

/* V4. Annex NPCs placed anywhere OTHER than the annex (old test spots). Expect ZERO rows;
       remove any it lists with @removeinst <wcid> standing in that landblock. */
SELECT `weenie_Class_Id`, HEX(`landblock`) AS landblock, `variation_Id` FROM `landblock_instance`
WHERE `weenie_Class_Id` BETWEEN 78780200 AND 78780249 AND NOT (`landblock` = 0x0106 AND `variation_Id` = 2);
