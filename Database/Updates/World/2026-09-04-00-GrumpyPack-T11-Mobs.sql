-- T11 Tou Tou mob layer authored 08-09..09-04: minions/leaders/bosses, pack and pool generators, appearance helpers
-- 66 weenies: 730000501..739999999. Re-runnable: each weenie is DELETEd (cascade) then re-inserted. Generated 2026-09-04 from the test world.
-- Emote actions use LAST_INSERT_ID, no hardcoded emote ids.

DELETE FROM `weenie` WHERE `class_Id` = 730000501;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000501, 'tou_ph_land_leader_1', 10, '2026-08-10 23:19:51');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000501, 1, 11, 0, 0),
(730000501, 2, 11, 0, 0),
(730000501, 3, 11, 0, 0),
(730000501, 4, 11, 0, 0),
(730000501, 5, 11, 0, 0),
(730000501, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000501, 1, 110000, 0, 0, 110000),
(730000501, 3, 110000, 0, 0, 110000),
(730000501, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000501, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000501, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000501, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000501, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000501, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000501, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000501, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000501, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000501, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000501, 1, 1),
(730000501, 6, 1),
(730000501, 11, 0),
(730000501, 12, 1),
(730000501, 13, 0),
(730000501, 14, 1),
(730000501, 19, 1),
(730000501, 50, 1),
(730000501, 50049, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000501, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000501, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000501, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000501, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000501, 1, 33559123),
(730000501, 2, 150995324),
(730000501, 3, 536871099),
(730000501, 4, 805306433),
(730000501, 6, 67116365),
(730000501, 7, 268436890),
(730000501, 8, 100677367),
(730000501, 22, 872415411),
(730000501, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000501, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000501, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000501, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000501, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000501, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000501, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000501, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000501, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000501, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000501, 94),
(730000501, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000501, 1, 5.0),
(730000501, 2, 0.0),
(730000501, 3, 0.7),
(730000501, 4, 3.0),
(730000501, 5, 1.0),
(730000501, 12, 0.0),
(730000501, 13, 1.0),
(730000501, 14, 1.0),
(730000501, 15, 1.0),
(730000501, 16, 1.0),
(730000501, 17, 1.0),
(730000501, 18, 1.0),
(730000501, 19, 1.0),
(730000501, 31, 18.0),
(730000501, 34, 1.0),
(730000501, 36, 1.0),
(730000501, 39, 2.0),
(730000501, 64, 0.6),
(730000501, 65, 0.6),
(730000501, 66, 0.6),
(730000501, 67, 0.6),
(730000501, 68, 0.6),
(730000501, 69, 0.6),
(730000501, 70, 0.6),
(730000501, 71, 1.0),
(730000501, 72, 1.0),
(730000501, 73, 1.0),
(730000501, 74, 1.0),
(730000501, 75, 1.0),
(730000501, 80, 3.0),
(730000501, 104, 20.0),
(730000501, 122, 2.0),
(730000501, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000501, 1, 16),
(730000501, 2, 82),
(730000501, 3, 76),
(730000501, 6, -1),
(730000501, 7, -1),
(730000501, 16, 1),
(730000501, 25, 1100),
(730000501, 27, 0),
(730000501, 40, 2),
(730000501, 68, 9),
(730000501, 93, 1032),
(730000501, 101, 131),
(730000501, 133, 2),
(730000501, 140, 1),
(730000501, 146, 290000000),
(730000501, 307, 1200),
(730000501, 308, 400),
(730000501, 332, 20000),
(730000501, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000501, 6, 0, 3, 0, 11, 0, 0.0),
(730000501, 7, 0, 3, 0, 11, 0, 0.0),
(730000501, 14, 0, 3, 0, 11, 0, 0.0),
(730000501, 15, 0, 3, 0, 11, 0, 0.0),
(730000501, 20, 0, 3, 0, 11, 0, 0.0),
(730000501, 31, 0, 3, 0, 11, 0, 0.0),
(730000501, 33, 0, 3, 0, 11, 0, 0.0),
(730000501, 34, 0, 3, 0, 11, 0, 0.0),
(730000501, 44, 0, 3, 0, 11, 0, 0.0),
(730000501, 45, 0, 3, 0, 11, 0, 0.0),
(730000501, 46, 0, 3, 0, 11, 0, 0.0),
(730000501, 47, 0, 3, 0, 11, 0, 0.0),
(730000501, 48, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000501, 2038, 2.007999897003174),
(730000501, 4186, 2.0299999713897705),
(730000501, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000501, 1, 'Mushy Marv');
DELETE FROM `weenie` WHERE `class_Id` = 730000502;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000502, 'tou_ph_land_leader_2', 10, '2026-08-10 23:19:51');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000502, 1, 11, 0, 0),
(730000502, 2, 11, 0, 0),
(730000502, 3, 11, 0, 0),
(730000502, 4, 11, 0, 0),
(730000502, 5, 11, 0, 0),
(730000502, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000502, 1, 110000, 0, 0, 110000),
(730000502, 3, 110000, 0, 0, 110000),
(730000502, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000502, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000502, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000502, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000502, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000502, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000502, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000502, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000502, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000502, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000502, 1, 1),
(730000502, 6, 1),
(730000502, 11, 0),
(730000502, 12, 1),
(730000502, 13, 0),
(730000502, 14, 1),
(730000502, 19, 1),
(730000502, 50, 1),
(730000502, 50049, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000502, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000502, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000502, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000502, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000502, 1, 33558024),
(730000502, 2, 150994951),
(730000502, 3, 536870917),
(730000502, 4, 805306370),
(730000502, 6, 67114021),
(730000502, 7, 268436497),
(730000502, 8, 100667453),
(730000502, 22, 872415255),
(730000502, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000502, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000502, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000502, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000502, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000502, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000502, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000502, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000502, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000502, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000502, 94),
(730000502, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000502, 1, 5.0),
(730000502, 2, 0.0),
(730000502, 3, 0.7),
(730000502, 4, 3.0),
(730000502, 5, 1.0),
(730000502, 12, 0.5),
(730000502, 13, 1.0),
(730000502, 14, 1.0),
(730000502, 15, 1.0),
(730000502, 16, 1.0),
(730000502, 17, 1.0),
(730000502, 18, 1.0),
(730000502, 19, 1.0),
(730000502, 31, 18.0),
(730000502, 34, 1.0),
(730000502, 36, 1.0),
(730000502, 39, 2.0),
(730000502, 64, 0.6),
(730000502, 65, 0.6),
(730000502, 66, 0.6),
(730000502, 67, 0.6),
(730000502, 68, 0.6),
(730000502, 69, 0.6),
(730000502, 70, 0.6),
(730000502, 71, 1.0),
(730000502, 72, 1.0),
(730000502, 73, 1.0),
(730000502, 74, 1.0),
(730000502, 75, 1.0),
(730000502, 80, 3.0),
(730000502, 104, 20.0),
(730000502, 122, 2.0),
(730000502, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000502, 1, 16),
(730000502, 2, 2),
(730000502, 3, 44),
(730000502, 6, -1),
(730000502, 7, -1),
(730000502, 16, 1),
(730000502, 25, 1100),
(730000502, 27, 0),
(730000502, 40, 2),
(730000502, 68, 9),
(730000502, 93, 1032),
(730000502, 101, 131),
(730000502, 133, 2),
(730000502, 140, 1),
(730000502, 146, 290000000),
(730000502, 307, 1200),
(730000502, 308, 400),
(730000502, 332, 20000),
(730000502, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000502, 6, 0, 3, 0, 11, 0, 0.0),
(730000502, 7, 0, 3, 0, 11, 0, 0.0),
(730000502, 14, 0, 3, 0, 11, 0, 0.0),
(730000502, 15, 0, 3, 0, 11, 0, 0.0),
(730000502, 20, 0, 3, 0, 11, 0, 0.0),
(730000502, 31, 0, 3, 0, 11, 0, 0.0),
(730000502, 33, 0, 3, 0, 11, 0, 0.0),
(730000502, 34, 0, 3, 0, 11, 0, 0.0),
(730000502, 44, 0, 3, 0, 11, 0, 0.0),
(730000502, 45, 0, 3, 0, 11, 0, 0.0),
(730000502, 46, 0, 3, 0, 11, 0, 0.0),
(730000502, 47, 0, 3, 0, 11, 0, 0.0),
(730000502, 48, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000502, 2038, 2.007999897003174),
(730000502, 4186, 2.0299999713897705),
(730000502, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000502, 1, 'Big Knuckles');
DELETE FROM `weenie` WHERE `class_Id` = 730000503;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000503, 'tou_ph_land_leader_3', 10, '2026-08-10 23:19:51');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000503, 1, 11, 0, 0),
(730000503, 2, 11, 0, 0),
(730000503, 3, 11, 0, 0),
(730000503, 4, 11, 0, 0),
(730000503, 5, 11, 0, 0),
(730000503, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000503, 1, 110000, 0, 0, 110000),
(730000503, 3, 110000, 0, 0, 110000),
(730000503, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000503, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000503, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000503, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000503, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000503, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000503, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000503, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000503, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000503, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000503, 1, 1),
(730000503, 6, 1),
(730000503, 11, 0),
(730000503, 12, 1),
(730000503, 13, 0),
(730000503, 14, 1),
(730000503, 19, 1),
(730000503, 50, 1),
(730000503, 50049, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000503, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000503, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000503, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000503, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000503, 1, 33556773),
(730000503, 2, 150995100),
(730000503, 3, 536871011),
(730000503, 4, 805306409),
(730000503, 6, 67112944),
(730000503, 7, 268436040),
(730000503, 8, 100670959),
(730000503, 22, 872415366),
(730000503, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000503, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000503, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000503, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000503, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000503, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000503, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000503, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000503, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000503, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000503, 94),
(730000503, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000503, 1, 5.0),
(730000503, 2, 0.0),
(730000503, 3, 0.7),
(730000503, 4, 3.0),
(730000503, 5, 1.0),
(730000503, 12, 0.5),
(730000503, 13, 1.0),
(730000503, 14, 1.0),
(730000503, 15, 1.0),
(730000503, 16, 1.0),
(730000503, 17, 1.0),
(730000503, 18, 1.0),
(730000503, 19, 1.0),
(730000503, 31, 18.0),
(730000503, 34, 1.0),
(730000503, 36, 1.0),
(730000503, 39, 2.0),
(730000503, 64, 0.6),
(730000503, 65, 0.6),
(730000503, 66, 0.6),
(730000503, 67, 0.6),
(730000503, 68, 0.6),
(730000503, 69, 0.6),
(730000503, 70, 0.6),
(730000503, 71, 1.0),
(730000503, 72, 1.0),
(730000503, 73, 1.0),
(730000503, 74, 1.0),
(730000503, 75, 1.0),
(730000503, 80, 3.0),
(730000503, 104, 20.0),
(730000503, 122, 2.0),
(730000503, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000503, 1, 16),
(730000503, 2, 46),
(730000503, 3, 62),
(730000503, 6, -1),
(730000503, 7, -1),
(730000503, 16, 1),
(730000503, 25, 1100),
(730000503, 27, 0),
(730000503, 40, 2),
(730000503, 68, 9),
(730000503, 93, 1032),
(730000503, 101, 131),
(730000503, 133, 2),
(730000503, 140, 1),
(730000503, 146, 290000000),
(730000503, 307, 1200),
(730000503, 308, 400),
(730000503, 332, 20000),
(730000503, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000503, 6, 0, 3, 0, 11, 0, 0.0),
(730000503, 7, 0, 3, 0, 11, 0, 0.0),
(730000503, 14, 0, 3, 0, 11, 0, 0.0),
(730000503, 15, 0, 3, 0, 11, 0, 0.0),
(730000503, 20, 0, 3, 0, 11, 0, 0.0),
(730000503, 31, 0, 3, 0, 11, 0, 0.0),
(730000503, 33, 0, 3, 0, 11, 0, 0.0),
(730000503, 34, 0, 3, 0, 11, 0, 0.0),
(730000503, 44, 0, 3, 0, 11, 0, 0.0),
(730000503, 45, 0, 3, 0, 11, 0, 0.0),
(730000503, 46, 0, 3, 0, 11, 0, 0.0),
(730000503, 47, 0, 3, 0, 11, 0, 0.0),
(730000503, 48, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000503, 2038, 2.007999897003174),
(730000503, 4186, 2.0299999713897705),
(730000503, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000503, 1, 'Old Growler');
DELETE FROM `weenie` WHERE `class_Id` = 730000504;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000504, 'tou_ph_land_leader_4', 10, '2026-08-10 23:19:51');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000504, 1, 11, 0, 0),
(730000504, 2, 11, 0, 0),
(730000504, 3, 11, 0, 0),
(730000504, 4, 11, 0, 0),
(730000504, 5, 11, 0, 0),
(730000504, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000504, 1, 110000, 0, 0, 110000),
(730000504, 3, 110000, 0, 0, 110000),
(730000504, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000504, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000504, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000504, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000504, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000504, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000504, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000504, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000504, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000504, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000504, 1, 1),
(730000504, 6, 1),
(730000504, 11, 0),
(730000504, 12, 1),
(730000504, 13, 0),
(730000504, 14, 1),
(730000504, 19, 1),
(730000504, 50, 1),
(730000504, 50049, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000504, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000504, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000504, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000504, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000504, 1, 33556251),
(730000504, 2, 150995091),
(730000504, 3, 536870914),
(730000504, 4, 805306408),
(730000504, 6, 67108990),
(730000504, 7, 268435871),
(730000504, 8, 100670398),
(730000504, 22, 872415331),
(730000504, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000504, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000504, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000504, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000504, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000504, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000504, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000504, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000504, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000504, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000504, 94),
(730000504, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000504, 1, 5.0),
(730000504, 2, 0.0),
(730000504, 3, 0.7),
(730000504, 4, 3.0),
(730000504, 5, 1.0),
(730000504, 12, 0.5),
(730000504, 13, 1.0),
(730000504, 14, 1.0),
(730000504, 15, 1.0),
(730000504, 16, 1.0),
(730000504, 17, 1.0),
(730000504, 18, 1.0),
(730000504, 19, 1.0),
(730000504, 31, 18.0),
(730000504, 34, 1.0),
(730000504, 36, 1.0),
(730000504, 39, 2.0),
(730000504, 64, 0.6),
(730000504, 65, 0.6),
(730000504, 66, 0.6),
(730000504, 67, 0.6),
(730000504, 68, 0.6),
(730000504, 69, 0.6),
(730000504, 70, 0.6),
(730000504, 71, 1.0),
(730000504, 72, 1.0),
(730000504, 73, 1.0),
(730000504, 74, 1.0),
(730000504, 75, 1.0),
(730000504, 80, 3.0),
(730000504, 104, 20.0),
(730000504, 122, 2.0),
(730000504, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000504, 1, 16),
(730000504, 2, 22),
(730000504, 3, 39),
(730000504, 6, -1),
(730000504, 7, -1),
(730000504, 16, 1),
(730000504, 25, 1100),
(730000504, 27, 0),
(730000504, 40, 2),
(730000504, 68, 9),
(730000504, 93, 1032),
(730000504, 101, 131),
(730000504, 133, 2),
(730000504, 140, 1),
(730000504, 146, 290000000),
(730000504, 307, 1200),
(730000504, 308, 400),
(730000504, 332, 20000),
(730000504, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000504, 6, 0, 3, 0, 11, 0, 0.0),
(730000504, 7, 0, 3, 0, 11, 0, 0.0),
(730000504, 14, 0, 3, 0, 11, 0, 0.0),
(730000504, 15, 0, 3, 0, 11, 0, 0.0),
(730000504, 20, 0, 3, 0, 11, 0, 0.0),
(730000504, 31, 0, 3, 0, 11, 0, 0.0),
(730000504, 33, 0, 3, 0, 11, 0, 0.0),
(730000504, 34, 0, 3, 0, 11, 0, 0.0),
(730000504, 44, 0, 3, 0, 11, 0, 0.0),
(730000504, 45, 0, 3, 0, 11, 0, 0.0),
(730000504, 46, 0, 3, 0, 11, 0, 0.0),
(730000504, 47, 0, 3, 0, 11, 0, 0.0),
(730000504, 48, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000504, 2038, 2.007999897003174),
(730000504, 4186, 2.0299999713897705),
(730000504, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000504, 1, 'Murky Mel');
DELETE FROM `weenie` WHERE `class_Id` = 730000511;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000511, 'tou_ph_beach_leader_1', 10, '2026-08-10 23:19:50');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000511, 1, 11, 0, 0),
(730000511, 2, 11, 0, 0),
(730000511, 3, 11, 0, 0),
(730000511, 4, 11, 0, 0),
(730000511, 5, 11, 0, 0),
(730000511, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000511, 1, 110000, 0, 0, 110000),
(730000511, 3, 110000, 0, 0, 110000),
(730000511, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000511, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000511, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000511, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000511, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000511, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000511, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000511, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000511, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000511, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000511, 1, 1),
(730000511, 6, 1),
(730000511, 11, 0),
(730000511, 12, 1),
(730000511, 13, 0),
(730000511, 14, 1),
(730000511, 19, 1),
(730000511, 50, 1),
(730000511, 50049, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000511, 2, 90000029, 0, 0, -1.0, 0),
(730000511, 2, 90000030, 0, 0, -1.0, 0),
(730000511, 2, 90000031, 0, 0, -1.0, 0),
(730000511, 2, 90000052, 0, 0, -1.0, 0),
(730000511, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000511, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000511, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000511, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000511, 1, 33554433),
(730000511, 2, 150994967),
(730000511, 3, 536870934),
(730000511, 4, 805306368),
(730000511, 6, 67110722),
(730000511, 7, 268436626),
(730000511, 8, 100667942),
(730000511, 22, 872415272),
(730000511, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000511, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000511, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000511, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000511, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000511, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000511, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000511, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000511, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000511, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000511, 94),
(730000511, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000511, 1, 5.0),
(730000511, 2, 0.0),
(730000511, 3, 0.7),
(730000511, 4, 3.0),
(730000511, 5, 1.0),
(730000511, 12, 0.5),
(730000511, 13, 1.0),
(730000511, 14, 1.0),
(730000511, 15, 1.0),
(730000511, 16, 1.0),
(730000511, 17, 1.0),
(730000511, 18, 1.0),
(730000511, 19, 1.0),
(730000511, 31, 18.0),
(730000511, 34, 1.0),
(730000511, 36, 1.0),
(730000511, 39, 1.95),
(730000511, 64, 0.6),
(730000511, 65, 0.6),
(730000511, 66, 0.6),
(730000511, 67, 0.6),
(730000511, 68, 0.6),
(730000511, 69, 0.6),
(730000511, 70, 0.6),
(730000511, 71, 1.0),
(730000511, 72, 1.0),
(730000511, 73, 1.0),
(730000511, 74, 1.0),
(730000511, 75, 1.0),
(730000511, 80, 3.0),
(730000511, 104, 20.0),
(730000511, 122, 2.0),
(730000511, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000511, 1, 16),
(730000511, 2, 14),
(730000511, 3, 8),
(730000511, 6, -1),
(730000511, 7, -1),
(730000511, 16, 1),
(730000511, 25, 1100),
(730000511, 27, 0),
(730000511, 40, 2),
(730000511, 68, 9),
(730000511, 93, 1032),
(730000511, 101, 131),
(730000511, 133, 2),
(730000511, 140, 1),
(730000511, 146, 290000000),
(730000511, 307, 1200),
(730000511, 308, 400),
(730000511, 332, 20000),
(730000511, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000511, 6, 0, 3, 0, 11, 0, 0.0),
(730000511, 7, 0, 3, 0, 11, 0, 0.0),
(730000511, 14, 0, 3, 0, 11, 0, 0.0),
(730000511, 15, 0, 3, 0, 11, 0, 0.0),
(730000511, 20, 0, 3, 0, 11, 0, 0.0),
(730000511, 24, 0, 3, 0, 11, 0, 0.0),
(730000511, 44, 0, 3, 0, 11, 0, 0.0),
(730000511, 45, 0, 3, 0, 11, 0, 0.0),
(730000511, 46, 0, 3, 0, 11, 0, 0.0),
(730000511, 47, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000511, 2038, 2.007999897003174),
(730000511, 4186, 2.0299999713897705),
(730000511, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000511, 1, 'Scurvy Gregg'),
(730000511, 16, 'This damp, green-skinned wretch smells faintly of yeast, seawater, and old milk. He mutters endlessly about a \'downstairs mix-up\' and keeps offering to show you his paintings.');
DELETE FROM `weenie` WHERE `class_Id` = 730000512;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000512, 'tou_ph_beach_leader_2', 10, '2026-08-10 23:19:50');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000512, 1, 11, 0, 0),
(730000512, 2, 11, 0, 0),
(730000512, 3, 11, 0, 0),
(730000512, 4, 11, 0, 0),
(730000512, 5, 11, 0, 0),
(730000512, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000512, 1, 110000, 0, 0, 110000),
(730000512, 3, 110000, 0, 0, 110000),
(730000512, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000512, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000512, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000512, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000512, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000512, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000512, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000512, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000512, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000512, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000512, 1, 1),
(730000512, 6, 1),
(730000512, 11, 0),
(730000512, 12, 1),
(730000512, 13, 0),
(730000512, 14, 1),
(730000512, 19, 1),
(730000512, 50, 1),
(730000512, 50049, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000512, 2, 22021, 0, 0, -1.0, 0),
(730000512, 2, 28868, 0, 0, -1.0, 0),
(730000512, 2, 36356, 0, 0, -1.0, 0),
(730000512, 2, 90000053, 0, 0, -1.0, 0),
(730000512, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000512, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000512, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000512, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000512, 1, 33554839),
(730000512, 2, 150994945),
(730000512, 3, 536870914),
(730000512, 4, 805306368),
(730000512, 6, 67108990),
(730000512, 7, 268436018),
(730000512, 8, 100667942),
(730000512, 22, 872415272),
(730000512, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000512, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000512, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000512, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000512, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000512, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000512, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000512, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000512, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000512, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000512, 94),
(730000512, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000512, 1, 5.0),
(730000512, 2, 0.0),
(730000512, 3, 0.7),
(730000512, 4, 3.0),
(730000512, 5, 1.0),
(730000512, 12, 0.5),
(730000512, 13, 1.0),
(730000512, 14, 1.0),
(730000512, 15, 1.0),
(730000512, 16, 1.0),
(730000512, 17, 1.0),
(730000512, 18, 1.0),
(730000512, 19, 1.0),
(730000512, 31, 18.0),
(730000512, 34, 1.0),
(730000512, 36, 1.0),
(730000512, 39, 2.145),
(730000512, 64, 0.6),
(730000512, 65, 0.6),
(730000512, 66, 0.6),
(730000512, 67, 0.6),
(730000512, 68, 0.6),
(730000512, 69, 0.6),
(730000512, 70, 0.6),
(730000512, 71, 1.0),
(730000512, 72, 1.0),
(730000512, 73, 1.0),
(730000512, 74, 1.0),
(730000512, 75, 1.0),
(730000512, 80, 3.0),
(730000512, 104, 20.0),
(730000512, 122, 2.0),
(730000512, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000512, 1, 16),
(730000512, 2, 14),
(730000512, 3, 8),
(730000512, 6, -1),
(730000512, 7, -1),
(730000512, 16, 1),
(730000512, 25, 1100),
(730000512, 27, 0),
(730000512, 40, 2),
(730000512, 68, 9),
(730000512, 93, 1032),
(730000512, 101, 131),
(730000512, 133, 2),
(730000512, 140, 1),
(730000512, 146, 290000000),
(730000512, 307, 1200),
(730000512, 308, 400),
(730000512, 332, 20000),
(730000512, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000512, 6, 0, 3, 0, 11, 0, 0.0),
(730000512, 7, 0, 3, 0, 11, 0, 0.0),
(730000512, 14, 0, 3, 0, 11, 0, 0.0),
(730000512, 15, 0, 3, 0, 11, 0, 0.0),
(730000512, 20, 0, 3, 0, 11, 0, 0.0),
(730000512, 24, 0, 3, 0, 11, 0, 0.0),
(730000512, 44, 0, 3, 0, 11, 0, 0.0),
(730000512, 45, 0, 3, 0, 11, 0, 0.0),
(730000512, 46, 0, 3, 0, 11, 0, 0.0),
(730000512, 47, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000512, 2038, 2.007999897003174),
(730000512, 4186, 2.0299999713897705),
(730000512, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000512, 1, 'Unhinged Sanjo'),
(730000512, 16, 'The stress of keeping the steam boilers running amidst endless monster attacks has finally broken him. He mutters calculations to himself, waving his tools wildly at anyone who approaches.');
DELETE FROM `weenie` WHERE `class_Id` = 730000513;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000513, 'tou_ph_beach_leader_3', 10, '2026-08-10 23:19:50');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000513, 1, 11, 0, 0),
(730000513, 2, 11, 0, 0),
(730000513, 3, 11, 0, 0),
(730000513, 4, 11, 0, 0),
(730000513, 5, 11, 0, 0),
(730000513, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000513, 1, 110000, 0, 0, 110000),
(730000513, 3, 110000, 0, 0, 110000),
(730000513, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000513, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000513, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000513, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000513, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000513, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000513, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000513, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000513, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000513, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000513, 1, 1),
(730000513, 6, 1),
(730000513, 11, 0),
(730000513, 12, 1),
(730000513, 13, 0),
(730000513, 14, 1),
(730000513, 19, 1),
(730000513, 50, 1),
(730000513, 50049, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000513, 2, 90000042, 0, 0, -1.0, 0),
(730000513, 2, 90000065, 0, 0, -1.0, 0),
(730000513, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000513, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000513, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000513, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000513, 1, 33555464),
(730000513, 2, 150994981),
(730000513, 3, 536870942),
(730000513, 4, 805306368),
(730000513, 6, 67110722),
(730000513, 7, 268436626),
(730000513, 8, 100667942),
(730000513, 22, 872415272),
(730000513, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000513, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000513, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000513, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000513, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000513, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000513, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000513, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000513, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000513, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000513, 94),
(730000513, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000513, 1, 5.0),
(730000513, 2, 0.0),
(730000513, 3, 0.7),
(730000513, 4, 3.0),
(730000513, 5, 1.0),
(730000513, 12, 0.5),
(730000513, 13, 1.0),
(730000513, 14, 1.0),
(730000513, 15, 1.0),
(730000513, 16, 1.0),
(730000513, 17, 1.0),
(730000513, 18, 1.0),
(730000513, 19, 1.0),
(730000513, 31, 18.0),
(730000513, 34, 1.0),
(730000513, 36, 1.0),
(730000513, 39, 1.95),
(730000513, 64, 0.6),
(730000513, 65, 0.6),
(730000513, 66, 0.6),
(730000513, 67, 0.6),
(730000513, 68, 0.6),
(730000513, 69, 0.6),
(730000513, 70, 0.6),
(730000513, 71, 1.0),
(730000513, 72, 1.0),
(730000513, 73, 1.0),
(730000513, 74, 1.0),
(730000513, 75, 1.0),
(730000513, 80, 3.0),
(730000513, 104, 20.0),
(730000513, 122, 2.0),
(730000513, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000513, 1, 16),
(730000513, 2, 30),
(730000513, 3, 39),
(730000513, 6, -1),
(730000513, 7, -1),
(730000513, 16, 1),
(730000513, 25, 1100),
(730000513, 27, 0),
(730000513, 40, 2),
(730000513, 68, 9),
(730000513, 93, 1032),
(730000513, 101, 131),
(730000513, 133, 2),
(730000513, 140, 1),
(730000513, 146, 290000000),
(730000513, 307, 1200),
(730000513, 308, 400),
(730000513, 332, 20000),
(730000513, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000513, 6, 0, 3, 0, 11, 0, 0.0),
(730000513, 7, 0, 3, 0, 11, 0, 0.0),
(730000513, 14, 0, 3, 0, 11, 0, 0.0),
(730000513, 15, 0, 3, 0, 11, 0, 0.0),
(730000513, 20, 0, 3, 0, 11, 0, 0.0),
(730000513, 24, 0, 3, 0, 11, 0, 0.0),
(730000513, 44, 0, 3, 0, 11, 0, 0.0),
(730000513, 45, 0, 3, 0, 11, 0, 0.0),
(730000513, 46, 0, 3, 0, 11, 0, 0.0),
(730000513, 47, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000513, 2038, 2.007999897003174),
(730000513, 4186, 2.0299999713897705),
(730000513, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000513, 1, 'Damned Skippy');
DELETE FROM `weenie` WHERE `class_Id` = 730000514;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000514, 'tou_ph_beach_leader_4', 10, '2026-08-10 23:19:50');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000514, 1, 11, 0, 0),
(730000514, 2, 11, 0, 0),
(730000514, 3, 11, 0, 0),
(730000514, 4, 11, 0, 0),
(730000514, 5, 11, 0, 0),
(730000514, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000514, 1, 110000, 0, 0, 110000),
(730000514, 3, 110000, 0, 0, 110000),
(730000514, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000514, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000514, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000514, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000514, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000514, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000514, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000514, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000514, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000514, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000514, 1, 1),
(730000514, 6, 1),
(730000514, 11, 0),
(730000514, 12, 1),
(730000514, 13, 0),
(730000514, 14, 1),
(730000514, 19, 1),
(730000514, 50, 1),
(730000514, 50049, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000514, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000514, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000514, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000514, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000514, 1, 33556454),
(730000514, 2, 150995073),
(730000514, 3, 536871067),
(730000514, 4, 805306376),
(730000514, 6, 67112775),
(730000514, 8, 100667940),
(730000514, 22, 872415320),
(730000514, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000514, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000514, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000514, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000514, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000514, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000514, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000514, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000514, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000514, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000514, 94),
(730000514, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000514, 1, 5.0),
(730000514, 2, 0.0),
(730000514, 3, 0.7),
(730000514, 4, 3.0),
(730000514, 5, 1.0),
(730000514, 12, 0.5),
(730000514, 13, 1.0),
(730000514, 14, 1.0),
(730000514, 15, 1.0),
(730000514, 16, 1.0),
(730000514, 17, 1.0),
(730000514, 18, 1.0),
(730000514, 19, 1.0),
(730000514, 31, 18.0),
(730000514, 34, 1.0),
(730000514, 36, 1.0),
(730000514, 39, 2.0),
(730000514, 64, 0.6),
(730000514, 65, 0.6),
(730000514, 66, 0.6),
(730000514, 67, 0.6),
(730000514, 68, 0.6),
(730000514, 69, 0.6),
(730000514, 70, 0.6),
(730000514, 71, 1.0),
(730000514, 72, 1.0),
(730000514, 73, 1.0),
(730000514, 74, 1.0),
(730000514, 75, 1.0),
(730000514, 80, 3.0),
(730000514, 104, 20.0),
(730000514, 122, 2.0),
(730000514, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000514, 1, 16),
(730000514, 2, 13),
(730000514, 3, 61),
(730000514, 6, -1),
(730000514, 7, -1),
(730000514, 16, 1),
(730000514, 25, 1100),
(730000514, 27, 0),
(730000514, 40, 2),
(730000514, 68, 9),
(730000514, 93, 1032),
(730000514, 101, 131),
(730000514, 133, 2),
(730000514, 140, 1),
(730000514, 146, 290000000),
(730000514, 307, 1200),
(730000514, 308, 400),
(730000514, 332, 20000),
(730000514, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000514, 6, 0, 3, 0, 11, 0, 0.0),
(730000514, 7, 0, 3, 0, 11, 0, 0.0),
(730000514, 14, 0, 3, 0, 11, 0, 0.0),
(730000514, 15, 0, 3, 0, 11, 0, 0.0),
(730000514, 20, 0, 3, 0, 11, 0, 0.0),
(730000514, 24, 0, 3, 0, 11, 0, 0.0),
(730000514, 44, 0, 3, 0, 11, 0, 0.0),
(730000514, 45, 0, 3, 0, 11, 0, 0.0),
(730000514, 46, 0, 3, 0, 11, 0, 0.0),
(730000514, 47, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000514, 2038, 2.007999897003174),
(730000514, 4186, 2.0299999713897705),
(730000514, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000514, 1, 'Old Breakwater');
DELETE FROM `weenie` WHERE `class_Id` = 730000521;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000521, 'tou_ph_water_leader_1', 10, '2026-08-10 23:19:51');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000521, 1, 11, 0, 0),
(730000521, 2, 11, 0, 0),
(730000521, 3, 11, 0, 0),
(730000521, 4, 11, 0, 0),
(730000521, 5, 11, 0, 0),
(730000521, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000521, 1, 110000, 0, 0, 110000),
(730000521, 3, 110000, 0, 0, 110000),
(730000521, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000521, 0, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000521, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000521, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000521, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000521, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000521, 5, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000521, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000521, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000521, 8, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071),
(730000521, 22, 16, 11, 0.75, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000521, 1, 1),
(730000521, 6, 1),
(730000521, 11, 0),
(730000521, 12, 1),
(730000521, 13, 0),
(730000521, 14, 1),
(730000521, 19, 1),
(730000521, 50, 1),
(730000521, 50049, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000521, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000521, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000521, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000521, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000521, 1, 33556882),
(730000521, 2, 150995104),
(730000521, 3, 536871018),
(730000521, 4, 805306403),
(730000521, 6, 67112872),
(730000521, 7, 268436086),
(730000521, 8, 100671185),
(730000521, 22, 872415337),
(730000521, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000521, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000521, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000521, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000521, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000521, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000521, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000521, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000521, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000521, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000521, 94),
(730000521, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000521, 1, 5.0),
(730000521, 2, 0.0),
(730000521, 3, 0.7),
(730000521, 4, 3.0),
(730000521, 5, 1.0),
(730000521, 12, 0.5),
(730000521, 13, 1.0),
(730000521, 14, 1.0),
(730000521, 15, 1.0),
(730000521, 16, 1.0),
(730000521, 17, 1.0),
(730000521, 18, 1.0),
(730000521, 19, 1.0),
(730000521, 31, 18.0),
(730000521, 34, 1.0),
(730000521, 36, 1.0),
(730000521, 39, 2.0),
(730000521, 64, 0.6),
(730000521, 65, 0.6),
(730000521, 66, 0.6),
(730000521, 67, 0.6),
(730000521, 68, 0.6),
(730000521, 69, 0.6),
(730000521, 70, 0.6),
(730000521, 71, 1.0),
(730000521, 72, 1.0),
(730000521, 73, 1.0),
(730000521, 74, 1.0),
(730000521, 75, 1.0),
(730000521, 80, 3.0),
(730000521, 104, 20.0),
(730000521, 122, 2.0),
(730000521, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000521, 1, 16),
(730000521, 2, 34),
(730000521, 3, 77),
(730000521, 6, -1),
(730000521, 7, -1),
(730000521, 16, 1),
(730000521, 25, 1100),
(730000521, 27, 0),
(730000521, 40, 2),
(730000521, 68, 9),
(730000521, 93, 1032),
(730000521, 101, 131),
(730000521, 133, 2),
(730000521, 140, 1),
(730000521, 146, 290000000),
(730000521, 307, 1200),
(730000521, 308, 400),
(730000521, 332, 20000),
(730000521, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000521, 6, 0, 3, 0, 11, 0, 0.0),
(730000521, 7, 0, 3, 0, 11, 0, 0.0),
(730000521, 15, 0, 3, 0, 11, 0, 0.0),
(730000521, 20, 0, 3, 0, 11, 0, 0.0),
(730000521, 22, 0, 3, 0, 11, 0, 0.0),
(730000521, 24, 0, 3, 0, 11, 0, 0.0),
(730000521, 44, 0, 3, 0, 11, 0, 0.0),
(730000521, 45, 0, 3, 0, 11, 0, 0.0),
(730000521, 47, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000521, 2038, 2.007999897003174),
(730000521, 4186, 2.0299999713897705),
(730000521, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000521, 1, 'Finneas the Foul');
DELETE FROM `weenie` WHERE `class_Id` = 730000522;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000522, 'tou_ph_water_leader_2', 10, '2026-08-10 23:19:51');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000522, 1, 11, 0, 0),
(730000522, 2, 11, 0, 0),
(730000522, 3, 11, 0, 0),
(730000522, 4, 11, 0, 0),
(730000522, 5, 11, 0, 0),
(730000522, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000522, 1, 110000, 0, 0, 110000),
(730000522, 3, 110000, 0, 0, 110000),
(730000522, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000522, 0, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000522, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000522, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000522, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000522, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000522, 5, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000522, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000522, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000522, 8, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071),
(730000522, 22, 16, 11, 0.75, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000522, 1, 1),
(730000522, 6, 1),
(730000522, 11, 0),
(730000522, 12, 1),
(730000522, 13, 0),
(730000522, 14, 1),
(730000522, 19, 1),
(730000522, 50, 1),
(730000522, 50049, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000522, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000522, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000522, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000522, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000522, 1, 33554489),
(730000522, 2, 150994970),
(730000522, 3, 536870928),
(730000522, 4, 805306378),
(730000522, 6, 67109313),
(730000522, 7, 268436731),
(730000522, 8, 100667939),
(730000522, 22, 872415268),
(730000522, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000522, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000522, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000522, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000522, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000522, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000522, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000522, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000522, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000522, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000522, 94),
(730000522, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000522, 1, 5.0),
(730000522, 2, 0.0),
(730000522, 3, 0.7),
(730000522, 4, 3.0),
(730000522, 5, 1.0),
(730000522, 12, 0.5),
(730000522, 13, 1.0),
(730000522, 14, 1.0),
(730000522, 15, 1.0),
(730000522, 16, 1.0),
(730000522, 17, 1.0),
(730000522, 18, 1.0),
(730000522, 19, 1.0),
(730000522, 31, 18.0),
(730000522, 34, 1.0),
(730000522, 36, 1.0),
(730000522, 39, 2.0),
(730000522, 64, 0.6),
(730000522, 65, 0.6),
(730000522, 66, 0.6),
(730000522, 67, 0.6),
(730000522, 68, 0.6),
(730000522, 69, 0.6),
(730000522, 70, 0.6),
(730000522, 71, 1.0),
(730000522, 72, 1.0),
(730000522, 73, 1.0),
(730000522, 74, 1.0),
(730000522, 75, 1.0),
(730000522, 80, 3.0),
(730000522, 104, 20.0),
(730000522, 122, 2.0),
(730000522, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000522, 1, 16),
(730000522, 2, 16),
(730000522, 3, 39),
(730000522, 6, -1),
(730000522, 7, -1),
(730000522, 16, 1),
(730000522, 25, 1100),
(730000522, 27, 0),
(730000522, 40, 2),
(730000522, 68, 9),
(730000522, 93, 1032),
(730000522, 101, 131),
(730000522, 133, 2),
(730000522, 140, 1),
(730000522, 146, 290000000),
(730000522, 307, 1200),
(730000522, 308, 400),
(730000522, 332, 20000),
(730000522, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000522, 6, 0, 3, 0, 11, 0, 0.0),
(730000522, 7, 0, 3, 0, 11, 0, 0.0),
(730000522, 15, 0, 3, 0, 11, 0, 0.0),
(730000522, 20, 0, 3, 0, 11, 0, 0.0),
(730000522, 22, 0, 3, 0, 11, 0, 0.0),
(730000522, 24, 0, 3, 0, 11, 0, 0.0),
(730000522, 44, 0, 3, 0, 11, 0, 0.0),
(730000522, 45, 0, 3, 0, 11, 0, 0.0),
(730000522, 47, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000522, 2038, 2.007999897003174),
(730000522, 4186, 2.0299999713897705),
(730000522, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000522, 1, 'Chompy Charlene');
DELETE FROM `weenie` WHERE `class_Id` = 730000523;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000523, 'tou_ph_water_leader_3', 10, '2026-08-10 23:19:51');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000523, 1, 11, 0, 0),
(730000523, 2, 11, 0, 0),
(730000523, 3, 11, 0, 0),
(730000523, 4, 11, 0, 0),
(730000523, 5, 11, 0, 0),
(730000523, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000523, 1, 110000, 0, 0, 110000),
(730000523, 3, 110000, 0, 0, 110000),
(730000523, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000523, 0, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000523, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000523, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000523, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000523, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000523, 5, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000523, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000523, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000523, 8, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071),
(730000523, 22, 16, 11, 0.75, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000523, 1, 1),
(730000523, 6, 1),
(730000523, 11, 0),
(730000523, 12, 1),
(730000523, 13, 0),
(730000523, 14, 1),
(730000523, 19, 1),
(730000523, 50, 1),
(730000523, 50049, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000523, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000523, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000523, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000523, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000523, 1, 33559712),
(730000523, 2, 150995347),
(730000523, 3, 536871010),
(730000523, 4, 805306410),
(730000523, 6, 67116764),
(730000523, 7, 268437049),
(730000523, 8, 100670961),
(730000523, 22, 872415416),
(730000523, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000523, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000523, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000523, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000523, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000523, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000523, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000523, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000523, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000523, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000523, 94),
(730000523, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000523, 1, 5.0),
(730000523, 2, 0.0),
(730000523, 3, 0.7),
(730000523, 4, 3.0),
(730000523, 5, 1.0),
(730000523, 13, 1.0),
(730000523, 14, 1.0),
(730000523, 15, 1.0),
(730000523, 16, 1.0),
(730000523, 17, 1.0),
(730000523, 18, 1.0),
(730000523, 19, 1.0),
(730000523, 31, 18.0),
(730000523, 34, 1.0),
(730000523, 36, 1.0),
(730000523, 39, 2.0),
(730000523, 64, 0.6),
(730000523, 65, 0.6),
(730000523, 66, 0.6),
(730000523, 67, 0.6),
(730000523, 68, 0.6),
(730000523, 69, 0.6),
(730000523, 70, 0.6),
(730000523, 71, 1.0),
(730000523, 72, 1.0),
(730000523, 73, 1.0),
(730000523, 74, 1.0),
(730000523, 75, 1.0),
(730000523, 80, 3.0),
(730000523, 104, 20.0),
(730000523, 122, 2.0),
(730000523, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000523, 1, 16),
(730000523, 2, 88),
(730000523, 3, 82),
(730000523, 6, -1),
(730000523, 7, -1),
(730000523, 16, 1),
(730000523, 25, 1100),
(730000523, 27, 0),
(730000523, 40, 2),
(730000523, 68, 9),
(730000523, 93, 1032),
(730000523, 101, 131),
(730000523, 133, 2),
(730000523, 140, 1),
(730000523, 146, 290000000),
(730000523, 307, 1200),
(730000523, 308, 400),
(730000523, 332, 20000),
(730000523, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000523, 6, 0, 3, 0, 11, 0, 0.0),
(730000523, 7, 0, 3, 0, 11, 0, 0.0),
(730000523, 15, 0, 3, 0, 11, 0, 0.0),
(730000523, 20, 0, 3, 0, 11, 0, 0.0),
(730000523, 22, 0, 3, 0, 11, 0, 0.0),
(730000523, 24, 0, 3, 0, 11, 0, 0.0),
(730000523, 44, 0, 3, 0, 11, 0, 0.0),
(730000523, 45, 0, 3, 0, 11, 0, 0.0),
(730000523, 47, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000523, 2038, 2.007999897003174),
(730000523, 4186, 2.0299999713897705),
(730000523, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000523, 1, 'Squishy Stan');
DELETE FROM `weenie` WHERE `class_Id` = 730000524;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000524, 'tou_ph_water_leader_4', 10, '2026-08-10 23:19:51');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000524, 1, 11, 0, 0),
(730000524, 2, 11, 0, 0),
(730000524, 3, 11, 0, 0),
(730000524, 4, 11, 0, 0),
(730000524, 5, 11, 0, 0),
(730000524, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000524, 1, 110000, 0, 0, 110000),
(730000524, 3, 110000, 0, 0, 110000),
(730000524, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000524, 0, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000524, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000524, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000524, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000524, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000524, 5, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000524, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000524, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000524, 8, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071),
(730000524, 22, 16, 11, 0.75, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000524, 1, 1),
(730000524, 6, 1),
(730000524, 11, 0),
(730000524, 12, 1),
(730000524, 13, 0),
(730000524, 14, 1),
(730000524, 19, 1),
(730000524, 50, 1),
(730000524, 50049, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000524, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000524, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000524, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000524, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000524, 1, 33556698),
(730000524, 2, 150995098),
(730000524, 3, 536871009),
(730000524, 4, 805306411),
(730000524, 6, 67112927),
(730000524, 7, 268436038),
(730000524, 8, 100670960),
(730000524, 22, 872415364),
(730000524, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000524, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000524, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000524, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000524, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000524, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000524, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000524, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000524, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000524, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000524, 94),
(730000524, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000524, 1, 5.0),
(730000524, 2, 0.0),
(730000524, 3, 0.7),
(730000524, 4, 3.0),
(730000524, 5, 1.0),
(730000524, 12, 0.5),
(730000524, 13, 1.0),
(730000524, 14, 1.0),
(730000524, 15, 1.0),
(730000524, 16, 1.0),
(730000524, 17, 1.0),
(730000524, 18, 1.0),
(730000524, 19, 1.0),
(730000524, 31, 18.0),
(730000524, 34, 1.0),
(730000524, 36, 1.0),
(730000524, 39, 2.0),
(730000524, 64, 0.6),
(730000524, 65, 0.6),
(730000524, 66, 0.6),
(730000524, 67, 0.6),
(730000524, 68, 0.6),
(730000524, 69, 0.6),
(730000524, 70, 0.6),
(730000524, 71, 1.0),
(730000524, 72, 1.0),
(730000524, 73, 1.0),
(730000524, 74, 1.0),
(730000524, 75, 1.0),
(730000524, 80, 3.0),
(730000524, 104, 20.0),
(730000524, 122, 2.0),
(730000524, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000524, 1, 16),
(730000524, 2, 44),
(730000524, 3, 5),
(730000524, 6, -1),
(730000524, 7, -1),
(730000524, 16, 1),
(730000524, 25, 1100),
(730000524, 27, 0),
(730000524, 40, 2),
(730000524, 68, 9),
(730000524, 93, 1032),
(730000524, 101, 131),
(730000524, 133, 2),
(730000524, 140, 1),
(730000524, 146, 290000000),
(730000524, 307, 1200),
(730000524, 308, 400),
(730000524, 332, 20000),
(730000524, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000524, 6, 0, 3, 0, 11, 0, 0.0),
(730000524, 7, 0, 3, 0, 11, 0, 0.0),
(730000524, 15, 0, 3, 0, 11, 0, 0.0),
(730000524, 20, 0, 3, 0, 11, 0, 0.0),
(730000524, 22, 0, 3, 0, 11, 0, 0.0),
(730000524, 24, 0, 3, 0, 11, 0, 0.0),
(730000524, 44, 0, 3, 0, 11, 0, 0.0),
(730000524, 45, 0, 3, 0, 11, 0, 0.0),
(730000524, 47, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000524, 2038, 2.007999897003174),
(730000524, 4186, 2.0299999713897705),
(730000524, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000524, 1, 'Old Eightlegs');
DELETE FROM `weenie` WHERE `class_Id` = 730000531;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000531, 'tou_ph_obsidian_leader_1', 10, '2026-08-10 23:19:51');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000531, 1, 11, 0, 0),
(730000531, 2, 11, 0, 0),
(730000531, 3, 11, 0, 0),
(730000531, 4, 11, 0, 0),
(730000531, 5, 11, 0, 0),
(730000531, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000531, 1, 110000, 0, 0, 110000),
(730000531, 3, 110000, 0, 0, 110000),
(730000531, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000531, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000531, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000531, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000531, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000531, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000531, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000531, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000531, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000531, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000531, 1, 1),
(730000531, 6, 1),
(730000531, 11, 0),
(730000531, 12, 1),
(730000531, 13, 0),
(730000531, 14, 1),
(730000531, 19, 1),
(730000531, 50, 1),
(730000531, 50049, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000531, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000531, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000531, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000531, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000531, 1, 33556440),
(730000531, 2, 150995073),
(730000531, 3, 536870933),
(730000531, 4, 805306376),
(730000531, 8, 100667940),
(730000531, 22, 872415327),
(730000531, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000531, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000531, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000531, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000531, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000531, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000531, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000531, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000531, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000531, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000531, 94),
(730000531, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000531, 1, 5.0),
(730000531, 2, 0.0),
(730000531, 3, 0.7),
(730000531, 4, 3.0),
(730000531, 5, 1.0),
(730000531, 13, 1.0),
(730000531, 14, 1.0),
(730000531, 15, 1.0),
(730000531, 16, 1.0),
(730000531, 17, 1.0),
(730000531, 18, 1.0),
(730000531, 19, 1.0),
(730000531, 31, 18.0),
(730000531, 34, 1.0),
(730000531, 36, 1.0),
(730000531, 39, 2.0),
(730000531, 64, 0.6),
(730000531, 65, 0.6),
(730000531, 66, 0.6),
(730000531, 67, 0.6),
(730000531, 68, 0.6),
(730000531, 69, 0.6),
(730000531, 70, 0.6),
(730000531, 71, 1.0),
(730000531, 72, 1.0),
(730000531, 73, 1.0),
(730000531, 74, 1.0),
(730000531, 75, 1.0),
(730000531, 80, 3.0),
(730000531, 104, 20.0),
(730000531, 122, 2.0),
(730000531, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000531, 1, 16),
(730000531, 2, 13),
(730000531, 6, -1),
(730000531, 7, -1),
(730000531, 16, 1),
(730000531, 25, 1100),
(730000531, 27, 0),
(730000531, 40, 2),
(730000531, 68, 9),
(730000531, 93, 1032),
(730000531, 101, 131),
(730000531, 133, 2),
(730000531, 140, 1),
(730000531, 146, 290000000),
(730000531, 307, 1200),
(730000531, 308, 400),
(730000531, 332, 20000),
(730000531, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000531, 6, 0, 3, 0, 11, 0, 0.0),
(730000531, 7, 0, 3, 0, 11, 0, 0.0),
(730000531, 14, 0, 3, 0, 11, 0, 0.0),
(730000531, 15, 0, 3, 0, 11, 0, 0.0),
(730000531, 20, 0, 3, 0, 11, 0, 0.0),
(730000531, 31, 0, 3, 0, 11, 0, 0.0),
(730000531, 33, 0, 3, 0, 11, 0, 0.0),
(730000531, 34, 0, 3, 0, 11, 0, 0.0),
(730000531, 44, 0, 3, 0, 11, 0, 0.0),
(730000531, 45, 0, 3, 0, 11, 0, 0.0),
(730000531, 46, 0, 3, 0, 11, 0, 0.0),
(730000531, 47, 0, 3, 0, 11, 0, 0.0),
(730000531, 48, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000531, 2038, 2.007999897003174),
(730000531, 4186, 2.0299999713897705),
(730000531, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000531, 1, 'Glassjaw Gus');
DELETE FROM `weenie` WHERE `class_Id` = 730000532;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000532, 'tou_ph_obsidian_leader_2', 10, '2026-08-10 23:19:51');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000532, 1, 11, 0, 0),
(730000532, 2, 11, 0, 0),
(730000532, 3, 11, 0, 0),
(730000532, 4, 11, 0, 0),
(730000532, 5, 11, 0, 0),
(730000532, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000532, 1, 110000, 0, 0, 110000),
(730000532, 3, 110000, 0, 0, 110000),
(730000532, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000532, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000532, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000532, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000532, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000532, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000532, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000532, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000532, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000532, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000532, 1, 1),
(730000532, 6, 1),
(730000532, 11, 0),
(730000532, 12, 1),
(730000532, 13, 0),
(730000532, 14, 1),
(730000532, 19, 1),
(730000532, 50, 1),
(730000532, 50049, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000532, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000532, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000532, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000532, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000532, 1, 33556427),
(730000532, 2, 150995073),
(730000532, 3, 536870933),
(730000532, 4, 805306376),
(730000532, 8, 100667940),
(730000532, 22, 872415325),
(730000532, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000532, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000532, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000532, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000532, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000532, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000532, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000532, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000532, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000532, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000532, 94),
(730000532, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000532, 1, 5.0),
(730000532, 2, 0.0),
(730000532, 3, 0.7),
(730000532, 4, 3.0),
(730000532, 5, 1.0),
(730000532, 13, 1.0),
(730000532, 14, 1.0),
(730000532, 15, 1.0),
(730000532, 16, 1.0),
(730000532, 17, 1.0),
(730000532, 18, 1.0),
(730000532, 19, 1.0),
(730000532, 31, 18.0),
(730000532, 34, 1.0),
(730000532, 36, 1.0),
(730000532, 39, 2.0),
(730000532, 64, 0.6),
(730000532, 65, 0.6),
(730000532, 66, 0.6),
(730000532, 67, 0.6),
(730000532, 68, 0.6),
(730000532, 69, 0.6),
(730000532, 70, 0.6),
(730000532, 71, 1.0),
(730000532, 72, 1.0),
(730000532, 73, 1.0),
(730000532, 74, 1.0),
(730000532, 75, 1.0),
(730000532, 80, 3.0),
(730000532, 104, 20.0),
(730000532, 122, 2.0),
(730000532, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000532, 1, 16),
(730000532, 2, 13),
(730000532, 6, -1),
(730000532, 7, -1),
(730000532, 16, 1),
(730000532, 25, 1100),
(730000532, 27, 0),
(730000532, 40, 2),
(730000532, 68, 9),
(730000532, 93, 1032),
(730000532, 101, 131),
(730000532, 133, 2),
(730000532, 140, 1),
(730000532, 146, 290000000),
(730000532, 307, 1200),
(730000532, 308, 400),
(730000532, 332, 20000),
(730000532, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000532, 6, 0, 3, 0, 11, 0, 0.0),
(730000532, 7, 0, 3, 0, 11, 0, 0.0),
(730000532, 14, 0, 3, 0, 11, 0, 0.0),
(730000532, 15, 0, 3, 0, 11, 0, 0.0),
(730000532, 20, 0, 3, 0, 11, 0, 0.0),
(730000532, 31, 0, 3, 0, 11, 0, 0.0),
(730000532, 33, 0, 3, 0, 11, 0, 0.0),
(730000532, 34, 0, 3, 0, 11, 0, 0.0),
(730000532, 44, 0, 3, 0, 11, 0, 0.0),
(730000532, 45, 0, 3, 0, 11, 0, 0.0),
(730000532, 46, 0, 3, 0, 11, 0, 0.0),
(730000532, 47, 0, 3, 0, 11, 0, 0.0),
(730000532, 48, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000532, 2038, 2.007999897003174),
(730000532, 4186, 2.0299999713897705),
(730000532, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000532, 1, 'Slagheart Sal');
DELETE FROM `weenie` WHERE `class_Id` = 730000533;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000533, 'tou_ph_obsidian_leader_3', 10, '2026-08-10 23:19:51');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000533, 1, 11, 0, 0),
(730000533, 2, 11, 0, 0),
(730000533, 3, 11, 0, 0),
(730000533, 4, 11, 0, 0),
(730000533, 5, 11, 0, 0),
(730000533, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000533, 1, 110000, 0, 0, 110000),
(730000533, 3, 110000, 0, 0, 110000),
(730000533, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000533, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000533, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000533, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000533, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000533, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000533, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000533, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000533, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000533, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000533, 1, 1),
(730000533, 6, 1),
(730000533, 11, 0),
(730000533, 12, 1),
(730000533, 13, 0),
(730000533, 14, 1),
(730000533, 19, 1),
(730000533, 50, 1),
(730000533, 50049, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000533, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000533, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000533, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000533, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000533, 1, 33558118),
(730000533, 2, 150995065),
(730000533, 3, 536870982),
(730000533, 4, 805306402),
(730000533, 6, 67114050),
(730000533, 7, 268436515),
(730000533, 8, 100669115),
(730000533, 22, 872415336),
(730000533, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000533, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000533, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000533, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000533, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000533, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000533, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000533, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000533, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000533, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000533, 94),
(730000533, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000533, 1, 5.0),
(730000533, 2, 0.0),
(730000533, 3, 0.7),
(730000533, 4, 3.0),
(730000533, 5, 1.0),
(730000533, 13, 1.0),
(730000533, 14, 1.0),
(730000533, 15, 1.0),
(730000533, 16, 1.0),
(730000533, 17, 1.0),
(730000533, 18, 1.0),
(730000533, 19, 1.0),
(730000533, 31, 18.0),
(730000533, 34, 1.0),
(730000533, 36, 1.0),
(730000533, 39, 2.0),
(730000533, 64, 0.6),
(730000533, 65, 0.6),
(730000533, 66, 0.6),
(730000533, 67, 0.6),
(730000533, 68, 0.6),
(730000533, 69, 0.6),
(730000533, 70, 0.6),
(730000533, 71, 1.0),
(730000533, 72, 1.0),
(730000533, 73, 1.0),
(730000533, 74, 1.0),
(730000533, 75, 1.0),
(730000533, 80, 3.0),
(730000533, 104, 20.0),
(730000533, 122, 2.0),
(730000533, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000533, 1, 16),
(730000533, 2, 33),
(730000533, 3, 13),
(730000533, 6, -1),
(730000533, 7, -1),
(730000533, 16, 1),
(730000533, 25, 1100),
(730000533, 27, 0),
(730000533, 40, 2),
(730000533, 68, 9),
(730000533, 93, 1032),
(730000533, 101, 131),
(730000533, 133, 2),
(730000533, 140, 1),
(730000533, 146, 290000000),
(730000533, 307, 1200),
(730000533, 308, 400),
(730000533, 332, 20000),
(730000533, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000533, 6, 0, 3, 0, 11, 0, 0.0),
(730000533, 7, 0, 3, 0, 11, 0, 0.0),
(730000533, 14, 0, 3, 0, 11, 0, 0.0),
(730000533, 15, 0, 3, 0, 11, 0, 0.0),
(730000533, 20, 0, 3, 0, 11, 0, 0.0),
(730000533, 31, 0, 3, 0, 11, 0, 0.0),
(730000533, 33, 0, 3, 0, 11, 0, 0.0),
(730000533, 34, 0, 3, 0, 11, 0, 0.0),
(730000533, 44, 0, 3, 0, 11, 0, 0.0),
(730000533, 45, 0, 3, 0, 11, 0, 0.0),
(730000533, 46, 0, 3, 0, 11, 0, 0.0),
(730000533, 47, 0, 3, 0, 11, 0, 0.0),
(730000533, 48, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000533, 2038, 2.007999897003174),
(730000533, 4186, 2.0299999713897705),
(730000533, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000533, 1, 'Skitters McGee');
DELETE FROM `weenie` WHERE `class_Id` = 730000534;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000534, 'tou_ph_obsidian_leader_4', 10, '2026-08-10 23:19:51');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000534, 1, 11, 0, 0),
(730000534, 2, 11, 0, 0),
(730000534, 3, 11, 0, 0),
(730000534, 4, 11, 0, 0),
(730000534, 5, 11, 0, 0),
(730000534, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000534, 1, 110000, 0, 0, 110000),
(730000534, 3, 110000, 0, 0, 110000),
(730000534, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000534, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000534, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000534, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000534, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000534, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000534, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000534, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000534, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000534, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000534, 1, 1),
(730000534, 6, 1),
(730000534, 11, 0),
(730000534, 12, 1),
(730000534, 13, 0),
(730000534, 14, 1),
(730000534, 19, 1),
(730000534, 50, 1),
(730000534, 50049, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000534, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000534, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000534, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000534, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000534, 1, 33555879),
(730000534, 2, 150995072),
(730000534, 3, 536870986),
(730000534, 4, 805306399),
(730000534, 6, 67112444),
(730000534, 7, 268436624),
(730000534, 8, 100669720),
(730000534, 22, 872415333),
(730000534, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000534, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000534, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000534, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000534, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000534, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000534, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000534, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000534, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000534, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000534, 94),
(730000534, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000534, 1, 5.0),
(730000534, 2, 0.0),
(730000534, 3, 0.7),
(730000534, 4, 3.0),
(730000534, 5, 1.0),
(730000534, 12, 0.5),
(730000534, 13, 1.0),
(730000534, 14, 1.0),
(730000534, 15, 1.0),
(730000534, 16, 1.0),
(730000534, 17, 1.0),
(730000534, 18, 1.0),
(730000534, 19, 1.0),
(730000534, 31, 18.0),
(730000534, 34, 1.0),
(730000534, 36, 1.0),
(730000534, 39, 2.0),
(730000534, 64, 0.6),
(730000534, 65, 0.6),
(730000534, 66, 0.6),
(730000534, 67, 0.6),
(730000534, 68, 0.6),
(730000534, 69, 0.6),
(730000534, 70, 0.6),
(730000534, 71, 1.0),
(730000534, 72, 1.0),
(730000534, 73, 1.0),
(730000534, 74, 1.0),
(730000534, 75, 1.0),
(730000534, 80, 3.0),
(730000534, 104, 20.0),
(730000534, 122, 2.0),
(730000534, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000534, 1, 16),
(730000534, 2, 32),
(730000534, 3, 8),
(730000534, 6, -1),
(730000534, 7, -1),
(730000534, 16, 1),
(730000534, 25, 1100),
(730000534, 27, 0),
(730000534, 40, 2),
(730000534, 68, 9),
(730000534, 93, 1032),
(730000534, 101, 131),
(730000534, 133, 2),
(730000534, 140, 1),
(730000534, 146, 290000000),
(730000534, 307, 1200),
(730000534, 308, 400),
(730000534, 332, 20000),
(730000534, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000534, 6, 0, 3, 0, 11, 0, 0.0),
(730000534, 7, 0, 3, 0, 11, 0, 0.0),
(730000534, 14, 0, 3, 0, 11, 0, 0.0),
(730000534, 15, 0, 3, 0, 11, 0, 0.0),
(730000534, 20, 0, 3, 0, 11, 0, 0.0),
(730000534, 31, 0, 3, 0, 11, 0, 0.0),
(730000534, 33, 0, 3, 0, 11, 0, 0.0),
(730000534, 34, 0, 3, 0, 11, 0, 0.0),
(730000534, 44, 0, 3, 0, 11, 0, 0.0),
(730000534, 45, 0, 3, 0, 11, 0, 0.0),
(730000534, 46, 0, 3, 0, 11, 0, 0.0),
(730000534, 47, 0, 3, 0, 11, 0, 0.0),
(730000534, 48, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000534, 2038, 2.007999897003174),
(730000534, 4186, 2.0299999713897705),
(730000534, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000534, 1, 'Old Smolders');
DELETE FROM `weenie` WHERE `class_Id` = 730000551;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000551, 'tou_ph_land_minion_1', 10, '2026-08-10 23:19:50');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000551, 1, 11, 0, 0),
(730000551, 2, 11, 0, 0),
(730000551, 3, 11, 0, 0),
(730000551, 4, 11, 0, 0),
(730000551, 5, 11, 0, 0),
(730000551, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000551, 1, 110000, 0, 0, 110000),
(730000551, 3, 110000, 0, 0, 110000),
(730000551, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000551, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000551, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000551, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000551, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000551, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000551, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000551, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000551, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000551, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000551, 1, 1),
(730000551, 6, 1),
(730000551, 11, 0),
(730000551, 12, 1),
(730000551, 13, 0),
(730000551, 14, 1),
(730000551, 19, 1),
(730000551, 50, 1),
(730000551, 50050, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000551, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000551, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000551, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000551, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000551, 1, 33559123),
(730000551, 2, 150995324),
(730000551, 3, 536871099),
(730000551, 4, 805306433),
(730000551, 6, 67116365),
(730000551, 7, 268436890),
(730000551, 8, 100677367),
(730000551, 22, 872415411),
(730000551, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000551, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000551, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000551, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000551, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000551, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000551, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000551, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000551, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000551, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000551, 94),
(730000551, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000551, 1, 5.0),
(730000551, 2, 0.0),
(730000551, 3, 0.7),
(730000551, 4, 3.0),
(730000551, 5, 1.0),
(730000551, 12, 0.0),
(730000551, 13, 1.0),
(730000551, 14, 1.0),
(730000551, 15, 1.0),
(730000551, 16, 1.0),
(730000551, 17, 1.0),
(730000551, 18, 1.0),
(730000551, 19, 1.0),
(730000551, 31, 18.0),
(730000551, 34, 1.0),
(730000551, 36, 1.0),
(730000551, 39, 1.0),
(730000551, 64, 0.6),
(730000551, 65, 0.6),
(730000551, 66, 0.6),
(730000551, 67, 0.6),
(730000551, 68, 0.6),
(730000551, 69, 0.6),
(730000551, 70, 0.6),
(730000551, 71, 1.0),
(730000551, 72, 1.0),
(730000551, 73, 1.0),
(730000551, 74, 1.0),
(730000551, 75, 1.0),
(730000551, 80, 3.0),
(730000551, 104, 20.0),
(730000551, 122, 2.0),
(730000551, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000551, 1, 16),
(730000551, 2, 82),
(730000551, 3, 76),
(730000551, 6, -1),
(730000551, 7, -1),
(730000551, 16, 1),
(730000551, 25, 1100),
(730000551, 27, 0),
(730000551, 40, 2),
(730000551, 68, 9),
(730000551, 93, 1032),
(730000551, 101, 131),
(730000551, 133, 2),
(730000551, 140, 1),
(730000551, 146, 290000000),
(730000551, 307, 1200),
(730000551, 308, 400),
(730000551, 332, 20000),
(730000551, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000551, 6, 0, 3, 0, 11, 0, 0.0),
(730000551, 7, 0, 3, 0, 11, 0, 0.0),
(730000551, 14, 0, 3, 0, 11, 0, 0.0),
(730000551, 15, 0, 3, 0, 11, 0, 0.0),
(730000551, 20, 0, 3, 0, 11, 0, 0.0),
(730000551, 31, 0, 3, 0, 11, 0, 0.0),
(730000551, 33, 0, 3, 0, 11, 0, 0.0),
(730000551, 34, 0, 3, 0, 11, 0, 0.0),
(730000551, 44, 0, 3, 0, 11, 0, 0.0),
(730000551, 45, 0, 3, 0, 11, 0, 0.0),
(730000551, 46, 0, 3, 0, 11, 0, 0.0),
(730000551, 47, 0, 3, 0, 11, 0, 0.0),
(730000551, 48, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000551, 2038, 2.007999897003174),
(730000551, 4186, 2.0299999713897705),
(730000551, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000551, 1, 'Sporecap Thrungus');
DELETE FROM `weenie` WHERE `class_Id` = 730000552;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000552, 'tou_ph_land_minion_2', 10, '2026-08-10 23:19:50');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000552, 1, 11, 0, 0),
(730000552, 2, 11, 0, 0),
(730000552, 3, 11, 0, 0),
(730000552, 4, 11, 0, 0),
(730000552, 5, 11, 0, 0),
(730000552, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000552, 1, 110000, 0, 0, 110000),
(730000552, 3, 110000, 0, 0, 110000),
(730000552, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000552, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000552, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000552, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000552, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000552, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000552, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000552, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000552, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000552, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000552, 1, 1),
(730000552, 6, 1),
(730000552, 11, 0),
(730000552, 12, 1),
(730000552, 13, 0),
(730000552, 14, 1),
(730000552, 19, 1),
(730000552, 50, 1),
(730000552, 50050, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000552, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000552, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000552, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000552, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000552, 1, 33558024),
(730000552, 2, 150994951),
(730000552, 3, 536870917),
(730000552, 4, 805306370),
(730000552, 8, 100667453),
(730000552, 22, 872415255),
(730000552, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000552, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000552, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000552, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000552, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000552, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000552, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000552, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000552, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000552, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000552, 94),
(730000552, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000552, 1, 5.0),
(730000552, 2, 0.0),
(730000552, 3, 0.7),
(730000552, 4, 3.0),
(730000552, 5, 1.0),
(730000552, 13, 1.0),
(730000552, 14, 1.0),
(730000552, 15, 1.0),
(730000552, 16, 1.0),
(730000552, 17, 1.0),
(730000552, 18, 1.0),
(730000552, 19, 1.0),
(730000552, 31, 18.0),
(730000552, 34, 1.0),
(730000552, 36, 1.0),
(730000552, 39, 1.0),
(730000552, 64, 0.6),
(730000552, 65, 0.6),
(730000552, 66, 0.6),
(730000552, 67, 0.6),
(730000552, 68, 0.6),
(730000552, 69, 0.6),
(730000552, 70, 0.6),
(730000552, 71, 1.0),
(730000552, 72, 1.0),
(730000552, 73, 1.0),
(730000552, 74, 1.0),
(730000552, 75, 1.0),
(730000552, 80, 3.0),
(730000552, 104, 20.0),
(730000552, 122, 2.0),
(730000552, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000552, 1, 16),
(730000552, 2, 2),
(730000552, 6, -1),
(730000552, 7, -1),
(730000552, 16, 1),
(730000552, 25, 1100),
(730000552, 27, 0),
(730000552, 40, 2),
(730000552, 68, 9),
(730000552, 93, 1032),
(730000552, 101, 131),
(730000552, 133, 2),
(730000552, 140, 1),
(730000552, 146, 290000000),
(730000552, 307, 1200),
(730000552, 308, 400),
(730000552, 332, 20000),
(730000552, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000552, 6, 0, 3, 0, 11, 0, 0.0),
(730000552, 7, 0, 3, 0, 11, 0, 0.0),
(730000552, 14, 0, 3, 0, 11, 0, 0.0),
(730000552, 15, 0, 3, 0, 11, 0, 0.0),
(730000552, 20, 0, 3, 0, 11, 0, 0.0),
(730000552, 31, 0, 3, 0, 11, 0, 0.0),
(730000552, 33, 0, 3, 0, 11, 0, 0.0),
(730000552, 34, 0, 3, 0, 11, 0, 0.0),
(730000552, 44, 0, 3, 0, 11, 0, 0.0),
(730000552, 45, 0, 3, 0, 11, 0, 0.0),
(730000552, 46, 0, 3, 0, 11, 0, 0.0),
(730000552, 47, 0, 3, 0, 11, 0, 0.0),
(730000552, 48, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000552, 2038, 2.007999897003174),
(730000552, 4186, 2.0299999713897705),
(730000552, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000552, 1, 'Scrub Banderling');
DELETE FROM `weenie` WHERE `class_Id` = 730000553;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000553, 'tou_ph_land_minion_3', 10, '2026-08-10 23:19:50');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000553, 1, 11, 0, 0),
(730000553, 2, 11, 0, 0),
(730000553, 3, 11, 0, 0),
(730000553, 4, 11, 0, 0),
(730000553, 5, 11, 0, 0),
(730000553, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000553, 1, 110000, 0, 0, 110000),
(730000553, 3, 110000, 0, 0, 110000),
(730000553, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000553, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000553, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000553, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000553, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000553, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000553, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000553, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000553, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000553, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000553, 1, 1),
(730000553, 6, 1),
(730000553, 11, 0),
(730000553, 12, 1),
(730000553, 13, 0),
(730000553, 14, 1),
(730000553, 19, 1),
(730000553, 50, 1),
(730000553, 50050, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000553, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000553, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000553, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000553, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000553, 1, 33556773),
(730000553, 2, 150995100),
(730000553, 3, 536871011),
(730000553, 4, 805306409),
(730000553, 6, 67112944),
(730000553, 7, 268436040),
(730000553, 8, 100670959),
(730000553, 22, 872415366),
(730000553, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000553, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000553, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000553, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000553, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000553, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000553, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000553, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000553, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000553, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000553, 94),
(730000553, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000553, 1, 5.0),
(730000553, 2, 0.0),
(730000553, 3, 0.7),
(730000553, 4, 3.0),
(730000553, 5, 1.0),
(730000553, 12, 0.5),
(730000553, 13, 1.0),
(730000553, 14, 1.0),
(730000553, 15, 1.0),
(730000553, 16, 1.0),
(730000553, 17, 1.0),
(730000553, 18, 1.0),
(730000553, 19, 1.0),
(730000553, 31, 18.0),
(730000553, 34, 1.0),
(730000553, 36, 1.0),
(730000553, 39, 1.0),
(730000553, 64, 0.6),
(730000553, 65, 0.6),
(730000553, 66, 0.6),
(730000553, 67, 0.6),
(730000553, 68, 0.6),
(730000553, 69, 0.6),
(730000553, 70, 0.6),
(730000553, 71, 1.0),
(730000553, 72, 1.0),
(730000553, 73, 1.0),
(730000553, 74, 1.0),
(730000553, 75, 1.0),
(730000553, 80, 3.0),
(730000553, 104, 20.0),
(730000553, 122, 2.0),
(730000553, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000553, 1, 16),
(730000553, 2, 46),
(730000553, 3, 53),
(730000553, 6, -1),
(730000553, 7, -1),
(730000553, 16, 1),
(730000553, 25, 1100),
(730000553, 27, 0),
(730000553, 40, 2),
(730000553, 68, 9),
(730000553, 93, 1032),
(730000553, 101, 131),
(730000553, 133, 2),
(730000553, 140, 1),
(730000553, 146, 290000000),
(730000553, 307, 1200),
(730000553, 308, 400),
(730000553, 332, 20000),
(730000553, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000553, 6, 0, 3, 0, 11, 0, 0.0),
(730000553, 7, 0, 3, 0, 11, 0, 0.0),
(730000553, 14, 0, 3, 0, 11, 0, 0.0),
(730000553, 15, 0, 3, 0, 11, 0, 0.0),
(730000553, 20, 0, 3, 0, 11, 0, 0.0),
(730000553, 31, 0, 3, 0, 11, 0, 0.0),
(730000553, 33, 0, 3, 0, 11, 0, 0.0),
(730000553, 34, 0, 3, 0, 11, 0, 0.0),
(730000553, 44, 0, 3, 0, 11, 0, 0.0),
(730000553, 45, 0, 3, 0, 11, 0, 0.0),
(730000553, 46, 0, 3, 0, 11, 0, 0.0),
(730000553, 47, 0, 3, 0, 11, 0, 0.0),
(730000553, 48, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000553, 2038, 2.007999897003174),
(730000553, 4186, 2.0299999713897705),
(730000553, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000553, 1, 'Grove Ursuin');
DELETE FROM `weenie` WHERE `class_Id` = 730000554;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000554, 'tou_ph_land_minion_4', 10, '2026-08-10 23:19:50');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000554, 1, 11, 0, 0),
(730000554, 2, 11, 0, 0),
(730000554, 3, 11, 0, 0),
(730000554, 4, 11, 0, 0),
(730000554, 5, 11, 0, 0),
(730000554, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000554, 1, 110000, 0, 0, 110000),
(730000554, 3, 110000, 0, 0, 110000),
(730000554, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000554, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000554, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000554, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000554, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000554, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000554, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000554, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000554, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000554, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000554, 1, 1),
(730000554, 6, 1),
(730000554, 11, 0),
(730000554, 12, 1),
(730000554, 13, 0),
(730000554, 14, 1),
(730000554, 19, 1),
(730000554, 50, 1),
(730000554, 50050, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000554, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000554, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000554, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000554, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000554, 1, 33556251),
(730000554, 2, 150995091),
(730000554, 3, 536870914),
(730000554, 4, 805306408),
(730000554, 6, 67108990),
(730000554, 7, 268435871),
(730000554, 8, 100670398),
(730000554, 22, 872415331),
(730000554, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000554, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000554, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000554, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000554, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000554, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000554, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000554, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000554, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000554, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000554, 94),
(730000554, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000554, 1, 5.0),
(730000554, 2, 0.0),
(730000554, 3, 0.7),
(730000554, 4, 3.0),
(730000554, 5, 1.0),
(730000554, 12, 0.5),
(730000554, 13, 1.0),
(730000554, 14, 1.0),
(730000554, 15, 1.0),
(730000554, 16, 1.0),
(730000554, 17, 1.0),
(730000554, 18, 1.0),
(730000554, 19, 1.0),
(730000554, 31, 18.0),
(730000554, 34, 1.0),
(730000554, 36, 1.0),
(730000554, 39, 1.0),
(730000554, 64, 0.6),
(730000554, 65, 0.6),
(730000554, 66, 0.6),
(730000554, 67, 0.6),
(730000554, 68, 0.6),
(730000554, 69, 0.6),
(730000554, 70, 0.6),
(730000554, 71, 1.0),
(730000554, 72, 1.0),
(730000554, 73, 1.0),
(730000554, 74, 1.0),
(730000554, 75, 1.0),
(730000554, 80, 3.0),
(730000554, 104, 20.0),
(730000554, 122, 2.0),
(730000554, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000554, 1, 16),
(730000554, 2, 22),
(730000554, 3, 39),
(730000554, 6, -1),
(730000554, 7, -1),
(730000554, 16, 1),
(730000554, 25, 1100),
(730000554, 27, 0),
(730000554, 40, 2),
(730000554, 68, 9),
(730000554, 93, 1032),
(730000554, 101, 131),
(730000554, 133, 2),
(730000554, 140, 1),
(730000554, 146, 290000000),
(730000554, 307, 1200),
(730000554, 308, 400),
(730000554, 332, 20000),
(730000554, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000554, 6, 0, 3, 0, 11, 0, 0.0),
(730000554, 7, 0, 3, 0, 11, 0, 0.0),
(730000554, 14, 0, 3, 0, 11, 0, 0.0),
(730000554, 15, 0, 3, 0, 11, 0, 0.0),
(730000554, 20, 0, 3, 0, 11, 0, 0.0),
(730000554, 31, 0, 3, 0, 11, 0, 0.0),
(730000554, 33, 0, 3, 0, 11, 0, 0.0),
(730000554, 34, 0, 3, 0, 11, 0, 0.0),
(730000554, 44, 0, 3, 0, 11, 0, 0.0),
(730000554, 45, 0, 3, 0, 11, 0, 0.0),
(730000554, 46, 0, 3, 0, 11, 0, 0.0),
(730000554, 47, 0, 3, 0, 11, 0, 0.0),
(730000554, 48, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000554, 2038, 2.007999897003174),
(730000554, 4186, 2.0299999713897705),
(730000554, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000554, 1, 'Gloom Shadow');
DELETE FROM `weenie` WHERE `class_Id` = 730000561;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000561, 'tou_ph_beach_minion_1', 10, '2026-08-10 23:19:51');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000561, 1, 11, 0, 0),
(730000561, 2, 11, 0, 0),
(730000561, 3, 11, 0, 0),
(730000561, 4, 11, 0, 0),
(730000561, 5, 11, 0, 0),
(730000561, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000561, 1, 110000, 0, 0, 110000),
(730000561, 3, 110000, 0, 0, 110000),
(730000561, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000561, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000561, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000561, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000561, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000561, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000561, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000561, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000561, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000561, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000561, 1, 1),
(730000561, 6, 1),
(730000561, 11, 0),
(730000561, 12, 1),
(730000561, 13, 0),
(730000561, 14, 1),
(730000561, 19, 1),
(730000561, 50, 1),
(730000561, 50050, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000561, 2, 36353, 0, 0, -1.0, 0),
(730000561, 2, 90000031, 0, 0, -1.0, 0),
(730000561, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000561, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000561, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000561, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000561, 1, 33554433),
(730000561, 2, 150994967),
(730000561, 3, 536870934),
(730000561, 4, 805306368),
(730000561, 6, 67110722),
(730000561, 7, 268436626),
(730000561, 8, 100667942),
(730000561, 22, 872415272),
(730000561, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000561, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000561, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000561, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000561, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000561, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000561, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000561, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000561, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000561, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000561, 94),
(730000561, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000561, 1, 5.0),
(730000561, 2, 0.0),
(730000561, 3, 0.7),
(730000561, 4, 3.0),
(730000561, 5, 1.0),
(730000561, 12, 0.5),
(730000561, 13, 1.0),
(730000561, 14, 1.0),
(730000561, 15, 1.0),
(730000561, 16, 1.0),
(730000561, 17, 1.0),
(730000561, 18, 1.0),
(730000561, 19, 1.0),
(730000561, 31, 18.0),
(730000561, 34, 1.0),
(730000561, 36, 1.0),
(730000561, 39, 1.0),
(730000561, 64, 0.6),
(730000561, 65, 0.6),
(730000561, 66, 0.6),
(730000561, 67, 0.6),
(730000561, 68, 0.6),
(730000561, 69, 0.6),
(730000561, 70, 0.6),
(730000561, 71, 1.0),
(730000561, 72, 1.0),
(730000561, 73, 1.0),
(730000561, 74, 1.0),
(730000561, 75, 1.0),
(730000561, 80, 3.0),
(730000561, 104, 20.0),
(730000561, 122, 2.0),
(730000561, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000561, 1, 16),
(730000561, 2, 14),
(730000561, 3, 39),
(730000561, 6, -1),
(730000561, 7, -1),
(730000561, 16, 1),
(730000561, 25, 1100),
(730000561, 27, 0),
(730000561, 40, 2),
(730000561, 68, 9),
(730000561, 93, 1032),
(730000561, 101, 131),
(730000561, 133, 2),
(730000561, 140, 1),
(730000561, 146, 290000000),
(730000561, 307, 1200),
(730000561, 308, 400),
(730000561, 332, 20000),
(730000561, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000561, 6, 0, 3, 0, 11, 0, 0.0),
(730000561, 7, 0, 3, 0, 11, 0, 0.0),
(730000561, 14, 0, 3, 0, 11, 0, 0.0),
(730000561, 15, 0, 3, 0, 11, 0, 0.0),
(730000561, 20, 0, 3, 0, 11, 0, 0.0),
(730000561, 24, 0, 3, 0, 11, 0, 0.0),
(730000561, 44, 0, 3, 0, 11, 0, 0.0),
(730000561, 45, 0, 3, 0, 11, 0, 0.0),
(730000561, 46, 0, 3, 0, 11, 0, 0.0),
(730000561, 47, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000561, 2038, 2.007999897003174),
(730000561, 4186, 2.0299999713897705),
(730000561, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000561, 1, 'Cursed Plunderer');
DELETE FROM `weenie` WHERE `class_Id` = 730000562;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000562, 'tou_ph_beach_minion_2', 10, '2026-08-10 23:19:51');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000562, 1, 11, 0, 0),
(730000562, 2, 11, 0, 0),
(730000562, 3, 11, 0, 0),
(730000562, 4, 11, 0, 0),
(730000562, 5, 11, 0, 0),
(730000562, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000562, 1, 110000, 0, 0, 110000),
(730000562, 3, 110000, 0, 0, 110000),
(730000562, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000562, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000562, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000562, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000562, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000562, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000562, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000562, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000562, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000562, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000562, 1, 1),
(730000562, 6, 1),
(730000562, 11, 0),
(730000562, 12, 1),
(730000562, 13, 0),
(730000562, 14, 1),
(730000562, 19, 1),
(730000562, 50, 1),
(730000562, 50050, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000562, 2, 36353, 0, 0, -1.0, 0),
(730000562, 2, 90000031, 0, 0, -1.0, 0),
(730000562, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000562, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000562, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000562, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000562, 1, 33554433),
(730000562, 2, 150994967),
(730000562, 3, 536870934),
(730000562, 4, 805306368),
(730000562, 6, 67110722),
(730000562, 7, 268436626),
(730000562, 8, 100667942),
(730000562, 22, 872415272),
(730000562, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000562, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000562, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000562, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000562, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000562, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000562, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000562, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000562, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000562, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000562, 94),
(730000562, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000562, 1, 5.0),
(730000562, 2, 0.0),
(730000562, 3, 0.7),
(730000562, 4, 3.0),
(730000562, 5, 1.0),
(730000562, 12, 0.5),
(730000562, 13, 1.0),
(730000562, 14, 1.0),
(730000562, 15, 1.0),
(730000562, 16, 1.0),
(730000562, 17, 1.0),
(730000562, 18, 1.0),
(730000562, 19, 1.0),
(730000562, 31, 18.0),
(730000562, 34, 1.0),
(730000562, 36, 1.0),
(730000562, 39, 1.0),
(730000562, 64, 0.6),
(730000562, 65, 0.6),
(730000562, 66, 0.6),
(730000562, 67, 0.6),
(730000562, 68, 0.6),
(730000562, 69, 0.6),
(730000562, 70, 0.6),
(730000562, 71, 1.0),
(730000562, 72, 1.0),
(730000562, 73, 1.0),
(730000562, 74, 1.0),
(730000562, 75, 1.0),
(730000562, 80, 3.0),
(730000562, 104, 20.0),
(730000562, 122, 2.0),
(730000562, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000562, 1, 16),
(730000562, 2, 14),
(730000562, 3, 39),
(730000562, 6, -1),
(730000562, 7, -1),
(730000562, 16, 1),
(730000562, 25, 1100),
(730000562, 27, 0),
(730000562, 40, 2),
(730000562, 68, 9),
(730000562, 93, 1032),
(730000562, 101, 131),
(730000562, 133, 2),
(730000562, 140, 1),
(730000562, 146, 290000000),
(730000562, 307, 1200),
(730000562, 308, 400),
(730000562, 332, 20000),
(730000562, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000562, 6, 0, 3, 0, 11, 0, 0.0),
(730000562, 7, 0, 3, 0, 11, 0, 0.0),
(730000562, 14, 0, 3, 0, 11, 0, 0.0),
(730000562, 15, 0, 3, 0, 11, 0, 0.0),
(730000562, 20, 0, 3, 0, 11, 0, 0.0),
(730000562, 24, 0, 3, 0, 11, 0, 0.0),
(730000562, 44, 0, 3, 0, 11, 0, 0.0),
(730000562, 45, 0, 3, 0, 11, 0, 0.0),
(730000562, 46, 0, 3, 0, 11, 0, 0.0),
(730000562, 47, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000562, 2038, 2.007999897003174),
(730000562, 4186, 2.0299999713897705),
(730000562, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000562, 1, 'Dread Miner');
DELETE FROM `weenie` WHERE `class_Id` = 730000563;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000563, 'tou_ph_beach_minion_3', 10, '2026-08-10 23:19:51');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000563, 1, 11, 0, 0),
(730000563, 2, 11, 0, 0),
(730000563, 3, 11, 0, 0),
(730000563, 4, 11, 0, 0),
(730000563, 5, 11, 0, 0),
(730000563, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000563, 1, 110000, 0, 0, 110000),
(730000563, 3, 110000, 0, 0, 110000),
(730000563, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000563, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000563, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000563, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000563, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000563, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000563, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000563, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000563, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000563, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000563, 1, 1),
(730000563, 6, 1),
(730000563, 11, 0),
(730000563, 12, 1),
(730000563, 13, 0),
(730000563, 14, 1),
(730000563, 19, 1),
(730000563, 50, 1),
(730000563, 50050, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000563, 2, 36353, 0, 0, -1.0, 0),
(730000563, 2, 90000031, 0, 0, -1.0, 0),
(730000563, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000563, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000563, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000563, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000563, 1, 33554433),
(730000563, 2, 150994967),
(730000563, 3, 536870934),
(730000563, 4, 805306368),
(730000563, 6, 67110722),
(730000563, 7, 268436626),
(730000563, 8, 100667942),
(730000563, 22, 872415272),
(730000563, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000563, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000563, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000563, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000563, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000563, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000563, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000563, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000563, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000563, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000563, 94),
(730000563, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000563, 1, 5.0),
(730000563, 2, 0.0),
(730000563, 3, 0.7),
(730000563, 4, 3.0),
(730000563, 5, 1.0),
(730000563, 12, 0.5),
(730000563, 13, 1.0),
(730000563, 14, 1.0),
(730000563, 15, 1.0),
(730000563, 16, 1.0),
(730000563, 17, 1.0),
(730000563, 18, 1.0),
(730000563, 19, 1.0),
(730000563, 31, 18.0),
(730000563, 34, 1.0),
(730000563, 36, 1.0),
(730000563, 39, 1.0),
(730000563, 64, 0.6),
(730000563, 65, 0.6),
(730000563, 66, 0.6),
(730000563, 67, 0.6),
(730000563, 68, 0.6),
(730000563, 69, 0.6),
(730000563, 70, 0.6),
(730000563, 71, 1.0),
(730000563, 72, 1.0),
(730000563, 73, 1.0),
(730000563, 74, 1.0),
(730000563, 75, 1.0),
(730000563, 80, 3.0),
(730000563, 104, 20.0),
(730000563, 122, 2.0),
(730000563, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000563, 1, 16),
(730000563, 2, 30),
(730000563, 3, 39),
(730000563, 6, -1),
(730000563, 7, -1),
(730000563, 16, 1),
(730000563, 25, 1100),
(730000563, 27, 0),
(730000563, 40, 2),
(730000563, 68, 9),
(730000563, 93, 1032),
(730000563, 101, 131),
(730000563, 133, 2),
(730000563, 140, 1),
(730000563, 146, 290000000),
(730000563, 307, 1200),
(730000563, 308, 400),
(730000563, 332, 20000),
(730000563, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000563, 6, 0, 3, 0, 11, 0, 0.0),
(730000563, 7, 0, 3, 0, 11, 0, 0.0),
(730000563, 14, 0, 3, 0, 11, 0, 0.0),
(730000563, 15, 0, 3, 0, 11, 0, 0.0),
(730000563, 20, 0, 3, 0, 11, 0, 0.0),
(730000563, 24, 0, 3, 0, 11, 0, 0.0),
(730000563, 44, 0, 3, 0, 11, 0, 0.0),
(730000563, 45, 0, 3, 0, 11, 0, 0.0),
(730000563, 46, 0, 3, 0, 11, 0, 0.0),
(730000563, 47, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000563, 2038, 2.007999897003174),
(730000563, 4186, 2.0299999713897705),
(730000563, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000563, 1, 'Dread Sentry');
DELETE FROM `weenie` WHERE `class_Id` = 730000564;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000564, 'tou_ph_beach_minion_4', 10, '2026-08-10 23:19:51');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000564, 1, 11, 0, 0),
(730000564, 2, 11, 0, 0),
(730000564, 3, 11, 0, 0),
(730000564, 4, 11, 0, 0),
(730000564, 5, 11, 0, 0),
(730000564, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000564, 1, 110000, 0, 0, 110000),
(730000564, 3, 110000, 0, 0, 110000),
(730000564, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000564, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000564, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000564, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000564, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000564, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000564, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000564, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000564, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000564, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000564, 1, 1),
(730000564, 6, 1),
(730000564, 11, 0),
(730000564, 12, 1),
(730000564, 13, 0),
(730000564, 14, 1),
(730000564, 19, 1),
(730000564, 50, 1),
(730000564, 50050, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000564, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000564, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000564, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000564, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000564, 1, 33556454),
(730000564, 2, 150995073),
(730000564, 3, 536871067),
(730000564, 4, 805306376),
(730000564, 6, 67112775),
(730000564, 8, 100667940),
(730000564, 22, 872415320),
(730000564, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000564, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000564, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000564, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000564, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000564, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000564, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000564, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000564, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000564, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000564, 94),
(730000564, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000564, 1, 5.0),
(730000564, 2, 0.0),
(730000564, 3, 0.7),
(730000564, 4, 3.0),
(730000564, 5, 1.0),
(730000564, 12, 0.5),
(730000564, 13, 1.0),
(730000564, 14, 1.0),
(730000564, 15, 1.0),
(730000564, 16, 1.0),
(730000564, 17, 1.0),
(730000564, 18, 1.0),
(730000564, 19, 1.0),
(730000564, 31, 18.0),
(730000564, 34, 1.0),
(730000564, 36, 1.0),
(730000564, 39, 1.0),
(730000564, 64, 0.6),
(730000564, 65, 0.6),
(730000564, 66, 0.6),
(730000564, 67, 0.6),
(730000564, 68, 0.6),
(730000564, 69, 0.6),
(730000564, 70, 0.6),
(730000564, 71, 1.0),
(730000564, 72, 1.0),
(730000564, 73, 1.0),
(730000564, 74, 1.0),
(730000564, 75, 1.0),
(730000564, 80, 3.0),
(730000564, 104, 20.0),
(730000564, 122, 2.0),
(730000564, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000564, 1, 16),
(730000564, 2, 13),
(730000564, 3, 61),
(730000564, 6, -1),
(730000564, 7, -1),
(730000564, 16, 1),
(730000564, 25, 1100),
(730000564, 27, 0),
(730000564, 40, 2),
(730000564, 68, 9),
(730000564, 93, 1032),
(730000564, 101, 131),
(730000564, 133, 2),
(730000564, 140, 1),
(730000564, 146, 290000000),
(730000564, 307, 1200),
(730000564, 308, 400),
(730000564, 332, 20000),
(730000564, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000564, 6, 0, 3, 0, 11, 0, 0.0),
(730000564, 7, 0, 3, 0, 11, 0, 0.0),
(730000564, 14, 0, 3, 0, 11, 0, 0.0),
(730000564, 15, 0, 3, 0, 11, 0, 0.0),
(730000564, 20, 0, 3, 0, 11, 0, 0.0),
(730000564, 24, 0, 3, 0, 11, 0, 0.0),
(730000564, 44, 0, 3, 0, 11, 0, 0.0),
(730000564, 45, 0, 3, 0, 11, 0, 0.0),
(730000564, 46, 0, 3, 0, 11, 0, 0.0),
(730000564, 47, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000564, 2038, 2.007999897003174),
(730000564, 4186, 2.0299999713897705),
(730000564, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000564, 1, 'Tidepool Golem');
DELETE FROM `weenie` WHERE `class_Id` = 730000571;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000571, 'tou_ph_water_minion_1', 10, '2026-08-10 23:19:50');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000571, 1, 11, 0, 0),
(730000571, 2, 11, 0, 0),
(730000571, 3, 11, 0, 0),
(730000571, 4, 11, 0, 0),
(730000571, 5, 11, 0, 0),
(730000571, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000571, 1, 110000, 0, 0, 110000),
(730000571, 3, 110000, 0, 0, 110000),
(730000571, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000571, 0, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000571, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000571, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000571, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000571, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000571, 5, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000571, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000571, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000571, 8, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071),
(730000571, 22, 16, 11, 0.75, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000571, 1, 1),
(730000571, 6, 1),
(730000571, 11, 0),
(730000571, 12, 1),
(730000571, 13, 0),
(730000571, 14, 1),
(730000571, 19, 1),
(730000571, 50, 1),
(730000571, 50050, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000571, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000571, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000571, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000571, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000571, 1, 33556882),
(730000571, 2, 150995104),
(730000571, 3, 536871018),
(730000571, 4, 805306403),
(730000571, 6, 67112872),
(730000571, 7, 268436086),
(730000571, 8, 100671185),
(730000571, 22, 872415337),
(730000571, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000571, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000571, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000571, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000571, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000571, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000571, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000571, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000571, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000571, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000571, 94),
(730000571, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000571, 1, 5.0),
(730000571, 2, 0.0),
(730000571, 3, 0.7),
(730000571, 4, 3.0),
(730000571, 5, 1.0),
(730000571, 12, 0.5),
(730000571, 13, 1.0),
(730000571, 14, 1.0),
(730000571, 15, 1.0),
(730000571, 16, 1.0),
(730000571, 17, 1.0),
(730000571, 18, 1.0),
(730000571, 19, 1.0),
(730000571, 31, 18.0),
(730000571, 34, 1.0),
(730000571, 36, 1.0),
(730000571, 39, 1.0),
(730000571, 64, 0.6),
(730000571, 65, 0.6),
(730000571, 66, 0.6),
(730000571, 67, 0.6),
(730000571, 68, 0.6),
(730000571, 69, 0.6),
(730000571, 70, 0.6),
(730000571, 71, 1.0),
(730000571, 72, 1.0),
(730000571, 73, 1.0),
(730000571, 74, 1.0),
(730000571, 75, 1.0),
(730000571, 80, 3.0),
(730000571, 104, 20.0),
(730000571, 122, 2.0),
(730000571, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000571, 1, 16),
(730000571, 2, 34),
(730000571, 3, 26),
(730000571, 6, -1),
(730000571, 7, -1),
(730000571, 16, 1),
(730000571, 25, 1100),
(730000571, 27, 0),
(730000571, 40, 2),
(730000571, 68, 9),
(730000571, 93, 1032),
(730000571, 101, 131),
(730000571, 133, 2),
(730000571, 140, 1),
(730000571, 146, 290000000),
(730000571, 307, 1200),
(730000571, 308, 400),
(730000571, 332, 20000),
(730000571, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000571, 6, 0, 3, 0, 11, 0, 0.0),
(730000571, 7, 0, 3, 0, 11, 0, 0.0),
(730000571, 15, 0, 3, 0, 11, 0, 0.0),
(730000571, 20, 0, 3, 0, 11, 0, 0.0),
(730000571, 22, 0, 3, 0, 11, 0, 0.0),
(730000571, 24, 0, 3, 0, 11, 0, 0.0),
(730000571, 44, 0, 3, 0, 11, 0, 0.0),
(730000571, 45, 0, 3, 0, 11, 0, 0.0),
(730000571, 47, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000571, 2038, 2.007999897003174),
(730000571, 4186, 2.0299999713897705),
(730000571, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000571, 1, 'Shallows Moarsman');
DELETE FROM `weenie` WHERE `class_Id` = 730000572;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000572, 'tou_ph_water_minion_2', 10, '2026-08-10 23:19:50');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000572, 1, 11, 0, 0),
(730000572, 2, 11, 0, 0),
(730000572, 3, 11, 0, 0),
(730000572, 4, 11, 0, 0),
(730000572, 5, 11, 0, 0),
(730000572, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000572, 1, 110000, 0, 0, 110000),
(730000572, 3, 110000, 0, 0, 110000),
(730000572, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000572, 0, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000572, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000572, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000572, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000572, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000572, 5, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000572, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000572, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000572, 8, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071),
(730000572, 22, 16, 11, 0.75, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000572, 1, 1),
(730000572, 6, 1),
(730000572, 11, 0),
(730000572, 12, 1),
(730000572, 13, 0),
(730000572, 14, 1),
(730000572, 19, 1),
(730000572, 50, 1),
(730000572, 50050, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000572, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000572, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000572, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000572, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000572, 1, 33554489),
(730000572, 2, 150994970),
(730000572, 3, 536870928),
(730000572, 4, 805306378),
(730000572, 6, 67109313),
(730000572, 7, 268435556),
(730000572, 8, 100667939),
(730000572, 22, 872415268),
(730000572, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000572, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000572, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000572, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000572, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000572, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000572, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000572, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000572, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000572, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000572, 94),
(730000572, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000572, 1, 5.0),
(730000572, 2, 0.0),
(730000572, 3, 0.7),
(730000572, 4, 3.0),
(730000572, 5, 1.0),
(730000572, 12, 0.5),
(730000572, 13, 1.0),
(730000572, 14, 1.0),
(730000572, 15, 1.0),
(730000572, 16, 1.0),
(730000572, 17, 1.0),
(730000572, 18, 1.0),
(730000572, 19, 1.0),
(730000572, 31, 18.0),
(730000572, 34, 1.0),
(730000572, 36, 1.0),
(730000572, 39, 1.0),
(730000572, 64, 0.6),
(730000572, 65, 0.6),
(730000572, 66, 0.6),
(730000572, 67, 0.6),
(730000572, 68, 0.6),
(730000572, 69, 0.6),
(730000572, 70, 0.6),
(730000572, 71, 1.0),
(730000572, 72, 1.0),
(730000572, 73, 1.0),
(730000572, 74, 1.0),
(730000572, 75, 1.0),
(730000572, 80, 3.0),
(730000572, 104, 20.0),
(730000572, 122, 2.0),
(730000572, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000572, 1, 16),
(730000572, 2, 16),
(730000572, 3, 2),
(730000572, 6, -1),
(730000572, 7, -1),
(730000572, 16, 1),
(730000572, 25, 1100),
(730000572, 27, 0),
(730000572, 40, 2),
(730000572, 68, 9),
(730000572, 93, 1032),
(730000572, 101, 131),
(730000572, 133, 2),
(730000572, 140, 1),
(730000572, 146, 290000000),
(730000572, 307, 1200),
(730000572, 308, 400),
(730000572, 332, 20000),
(730000572, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000572, 6, 0, 3, 0, 11, 0, 0.0),
(730000572, 7, 0, 3, 0, 11, 0, 0.0),
(730000572, 15, 0, 3, 0, 11, 0, 0.0),
(730000572, 20, 0, 3, 0, 11, 0, 0.0),
(730000572, 22, 0, 3, 0, 11, 0, 0.0),
(730000572, 24, 0, 3, 0, 11, 0, 0.0),
(730000572, 44, 0, 3, 0, 11, 0, 0.0),
(730000572, 45, 0, 3, 0, 11, 0, 0.0),
(730000572, 47, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000572, 2038, 2.007999897003174),
(730000572, 4186, 2.0299999713897705),
(730000572, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000572, 1, 'Coastal Shark');
DELETE FROM `weenie` WHERE `class_Id` = 730000573;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000573, 'tou_ph_water_minion_3', 10, '2026-08-10 23:19:50');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000573, 1, 11, 0, 0),
(730000573, 2, 11, 0, 0),
(730000573, 3, 11, 0, 0),
(730000573, 4, 11, 0, 0),
(730000573, 5, 11, 0, 0),
(730000573, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000573, 1, 110000, 0, 0, 110000),
(730000573, 3, 110000, 0, 0, 110000),
(730000573, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000573, 0, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000573, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000573, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000573, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000573, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000573, 5, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000573, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000573, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000573, 8, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071),
(730000573, 22, 16, 11, 0.75, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000573, 1, 1),
(730000573, 6, 1),
(730000573, 11, 0),
(730000573, 12, 1),
(730000573, 13, 0),
(730000573, 14, 1),
(730000573, 19, 1),
(730000573, 50, 1),
(730000573, 50050, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000573, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000573, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000573, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000573, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000573, 1, 33559712),
(730000573, 2, 150995347),
(730000573, 3, 536871010),
(730000573, 4, 805306410),
(730000573, 6, 67116764),
(730000573, 7, 268437049),
(730000573, 8, 100670961),
(730000573, 22, 872415416),
(730000573, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000573, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000573, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000573, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000573, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000573, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000573, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000573, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000573, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000573, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000573, 94),
(730000573, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000573, 1, 5.0),
(730000573, 2, 0.0),
(730000573, 3, 0.7),
(730000573, 4, 3.0),
(730000573, 5, 1.0),
(730000573, 12, 0.5),
(730000573, 13, 1.0),
(730000573, 14, 1.0),
(730000573, 15, 1.0),
(730000573, 16, 1.0),
(730000573, 17, 1.0),
(730000573, 18, 1.0),
(730000573, 19, 1.0),
(730000573, 31, 18.0),
(730000573, 34, 1.0),
(730000573, 36, 1.0),
(730000573, 39, 1.0),
(730000573, 64, 0.6),
(730000573, 65, 0.6),
(730000573, 66, 0.6),
(730000573, 67, 0.6),
(730000573, 68, 0.6),
(730000573, 69, 0.6),
(730000573, 70, 0.6),
(730000573, 71, 1.0),
(730000573, 72, 1.0),
(730000573, 73, 1.0),
(730000573, 74, 1.0),
(730000573, 75, 1.0),
(730000573, 80, 3.0),
(730000573, 104, 20.0),
(730000573, 122, 2.0),
(730000573, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000573, 1, 16),
(730000573, 2, 88),
(730000573, 3, 2),
(730000573, 6, -1),
(730000573, 7, -1),
(730000573, 16, 1),
(730000573, 25, 1100),
(730000573, 27, 0),
(730000573, 40, 2),
(730000573, 68, 9),
(730000573, 93, 1032),
(730000573, 101, 131),
(730000573, 133, 2),
(730000573, 140, 1),
(730000573, 146, 290000000),
(730000573, 307, 1200),
(730000573, 308, 400),
(730000573, 332, 20000),
(730000573, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000573, 6, 0, 3, 0, 11, 0, 0.0),
(730000573, 7, 0, 3, 0, 11, 0, 0.0),
(730000573, 15, 0, 3, 0, 11, 0, 0.0),
(730000573, 20, 0, 3, 0, 11, 0, 0.0),
(730000573, 22, 0, 3, 0, 11, 0, 0.0),
(730000573, 24, 0, 3, 0, 11, 0, 0.0),
(730000573, 44, 0, 3, 0, 11, 0, 0.0),
(730000573, 45, 0, 3, 0, 11, 0, 0.0),
(730000573, 47, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000573, 2038, 2.007999897003174),
(730000573, 4186, 2.0299999713897705),
(730000573, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000573, 1, 'Soggy Sleech');
DELETE FROM `weenie` WHERE `class_Id` = 730000574;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000574, 'tou_ph_water_minion_4', 10, '2026-08-10 23:19:51');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000574, 1, 11, 0, 0),
(730000574, 2, 11, 0, 0),
(730000574, 3, 11, 0, 0),
(730000574, 4, 11, 0, 0),
(730000574, 5, 11, 0, 0),
(730000574, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000574, 1, 110000, 0, 0, 110000),
(730000574, 3, 110000, 0, 0, 110000),
(730000574, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000574, 0, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000574, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000574, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000574, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000574, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000574, 5, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000574, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000574, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000574, 8, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071),
(730000574, 22, 16, 11, 0.75, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000574, 1, 1),
(730000574, 6, 1),
(730000574, 11, 0),
(730000574, 12, 1),
(730000574, 13, 0),
(730000574, 14, 1),
(730000574, 19, 1),
(730000574, 50, 1),
(730000574, 50050, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000574, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000574, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000574, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000574, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000574, 1, 33556698),
(730000574, 2, 150995098),
(730000574, 3, 536871009),
(730000574, 4, 805306411),
(730000574, 6, 67112927),
(730000574, 7, 268436038),
(730000574, 8, 100670960),
(730000574, 22, 872415364),
(730000574, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000574, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000574, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000574, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000574, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000574, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000574, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000574, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000574, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000574, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000574, 94),
(730000574, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000574, 1, 5.0),
(730000574, 2, 0.0),
(730000574, 3, 0.7),
(730000574, 4, 3.0),
(730000574, 5, 1.0),
(730000574, 12, 0.5),
(730000574, 13, 1.0),
(730000574, 14, 1.0),
(730000574, 15, 1.0),
(730000574, 16, 1.0),
(730000574, 17, 1.0),
(730000574, 18, 1.0),
(730000574, 19, 1.0),
(730000574, 31, 18.0),
(730000574, 34, 1.0),
(730000574, 36, 1.0),
(730000574, 39, 1.0),
(730000574, 64, 0.6),
(730000574, 65, 0.6),
(730000574, 66, 0.6),
(730000574, 67, 0.6),
(730000574, 68, 0.6),
(730000574, 69, 0.6),
(730000574, 70, 0.6),
(730000574, 71, 1.0),
(730000574, 72, 1.0),
(730000574, 73, 1.0),
(730000574, 74, 1.0),
(730000574, 75, 1.0),
(730000574, 80, 3.0),
(730000574, 104, 20.0),
(730000574, 122, 2.0),
(730000574, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000574, 1, 16),
(730000574, 2, 44),
(730000574, 3, 5),
(730000574, 6, -1),
(730000574, 7, -1),
(730000574, 16, 1),
(730000574, 25, 1100),
(730000574, 27, 0),
(730000574, 40, 2),
(730000574, 68, 9),
(730000574, 93, 1032),
(730000574, 101, 131),
(730000574, 133, 2),
(730000574, 140, 1),
(730000574, 146, 290000000),
(730000574, 307, 1200),
(730000574, 308, 400),
(730000574, 332, 20000),
(730000574, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000574, 6, 0, 3, 0, 11, 0, 0.0),
(730000574, 7, 0, 3, 0, 11, 0, 0.0),
(730000574, 15, 0, 3, 0, 11, 0, 0.0),
(730000574, 20, 0, 3, 0, 11, 0, 0.0),
(730000574, 22, 0, 3, 0, 11, 0, 0.0),
(730000574, 24, 0, 3, 0, 11, 0, 0.0),
(730000574, 44, 0, 3, 0, 11, 0, 0.0),
(730000574, 45, 0, 3, 0, 11, 0, 0.0),
(730000574, 47, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000574, 2038, 2.007999897003174),
(730000574, 4186, 2.0299999713897705),
(730000574, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000574, 1, 'Sea Spider');
DELETE FROM `weenie` WHERE `class_Id` = 730000581;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000581, 'tou_ph_obsidian_minion_1', 10, '2026-08-10 23:19:50');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000581, 1, 11, 0, 0),
(730000581, 2, 11, 0, 0),
(730000581, 3, 11, 0, 0),
(730000581, 4, 11, 0, 0),
(730000581, 5, 11, 0, 0),
(730000581, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000581, 1, 110000, 0, 0, 110000),
(730000581, 3, 110000, 0, 0, 110000),
(730000581, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000581, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000581, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000581, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000581, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000581, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000581, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000581, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000581, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000581, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000581, 1, 1),
(730000581, 6, 1),
(730000581, 11, 0),
(730000581, 12, 1),
(730000581, 13, 0),
(730000581, 14, 1),
(730000581, 19, 1),
(730000581, 50, 1),
(730000581, 50050, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000581, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000581, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000581, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000581, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000581, 1, 33556440),
(730000581, 2, 150995073),
(730000581, 3, 536870933),
(730000581, 4, 805306376),
(730000581, 8, 100667940),
(730000581, 22, 872415327),
(730000581, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000581, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000581, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000581, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000581, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000581, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000581, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000581, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000581, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000581, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000581, 94),
(730000581, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000581, 1, 5.0),
(730000581, 2, 0.0),
(730000581, 3, 0.7),
(730000581, 4, 3.0),
(730000581, 5, 1.0),
(730000581, 13, 1.0),
(730000581, 14, 1.0),
(730000581, 15, 1.0),
(730000581, 16, 1.0),
(730000581, 17, 1.0),
(730000581, 18, 1.0),
(730000581, 19, 1.0),
(730000581, 31, 18.0),
(730000581, 34, 1.0),
(730000581, 36, 1.0),
(730000581, 39, 1.0),
(730000581, 64, 0.6),
(730000581, 65, 0.6),
(730000581, 66, 0.6),
(730000581, 67, 0.6),
(730000581, 68, 0.6),
(730000581, 69, 0.6),
(730000581, 70, 0.6),
(730000581, 71, 1.0),
(730000581, 72, 1.0),
(730000581, 73, 1.0),
(730000581, 74, 1.0),
(730000581, 75, 1.0),
(730000581, 80, 3.0),
(730000581, 104, 20.0),
(730000581, 122, 2.0),
(730000581, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000581, 1, 16),
(730000581, 2, 13),
(730000581, 6, -1),
(730000581, 7, -1),
(730000581, 16, 1),
(730000581, 25, 1100),
(730000581, 27, 0),
(730000581, 40, 2),
(730000581, 68, 9),
(730000581, 93, 1032),
(730000581, 101, 131),
(730000581, 133, 2),
(730000581, 140, 1),
(730000581, 146, 290000000),
(730000581, 307, 1200),
(730000581, 308, 400),
(730000581, 332, 20000),
(730000581, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000581, 6, 0, 3, 0, 11, 0, 0.0),
(730000581, 7, 0, 3, 0, 11, 0, 0.0),
(730000581, 14, 0, 3, 0, 11, 0, 0.0),
(730000581, 15, 0, 3, 0, 11, 0, 0.0),
(730000581, 20, 0, 3, 0, 11, 0, 0.0),
(730000581, 31, 0, 3, 0, 11, 0, 0.0),
(730000581, 33, 0, 3, 0, 11, 0, 0.0),
(730000581, 34, 0, 3, 0, 11, 0, 0.0),
(730000581, 44, 0, 3, 0, 11, 0, 0.0),
(730000581, 45, 0, 3, 0, 11, 0, 0.0),
(730000581, 46, 0, 3, 0, 11, 0, 0.0),
(730000581, 47, 0, 3, 0, 11, 0, 0.0),
(730000581, 48, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000581, 2038, 2.007999897003174),
(730000581, 4186, 2.0299999713897705),
(730000581, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000581, 1, 'Glassland Golem');
DELETE FROM `weenie` WHERE `class_Id` = 730000582;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000582, 'tou_ph_obsidian_minion_2', 10, '2026-08-10 23:19:50');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000582, 1, 11, 0, 0),
(730000582, 2, 11, 0, 0),
(730000582, 3, 11, 0, 0),
(730000582, 4, 11, 0, 0),
(730000582, 5, 11, 0, 0),
(730000582, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000582, 1, 110000, 0, 0, 110000),
(730000582, 3, 110000, 0, 0, 110000),
(730000582, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000582, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000582, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000582, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000582, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000582, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000582, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000582, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000582, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000582, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000582, 1, 1),
(730000582, 6, 1),
(730000582, 11, 0),
(730000582, 12, 1),
(730000582, 13, 0),
(730000582, 14, 1),
(730000582, 19, 1),
(730000582, 50, 1),
(730000582, 50050, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000582, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000582, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000582, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000582, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000582, 1, 33556427),
(730000582, 2, 150995073),
(730000582, 3, 536870933),
(730000582, 4, 805306376),
(730000582, 8, 100667940),
(730000582, 22, 872415325),
(730000582, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000582, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000582, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000582, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000582, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000582, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000582, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000582, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000582, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000582, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000582, 94),
(730000582, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000582, 1, 5.0),
(730000582, 2, 0.0),
(730000582, 3, 0.7),
(730000582, 4, 3.0),
(730000582, 5, 1.0),
(730000582, 13, 1.0),
(730000582, 14, 1.0),
(730000582, 15, 1.0),
(730000582, 16, 1.0),
(730000582, 17, 1.0),
(730000582, 18, 1.0),
(730000582, 19, 1.0),
(730000582, 31, 18.0),
(730000582, 34, 1.0),
(730000582, 36, 1.0),
(730000582, 39, 1.0),
(730000582, 64, 0.6),
(730000582, 65, 0.6),
(730000582, 66, 0.6),
(730000582, 67, 0.6),
(730000582, 68, 0.6),
(730000582, 69, 0.6),
(730000582, 70, 0.6),
(730000582, 71, 1.0),
(730000582, 72, 1.0),
(730000582, 73, 1.0),
(730000582, 74, 1.0),
(730000582, 75, 1.0),
(730000582, 80, 3.0),
(730000582, 104, 20.0),
(730000582, 122, 2.0),
(730000582, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000582, 1, 16),
(730000582, 2, 13),
(730000582, 6, -1),
(730000582, 7, -1),
(730000582, 16, 1),
(730000582, 25, 1100),
(730000582, 27, 0),
(730000582, 40, 2),
(730000582, 68, 9),
(730000582, 93, 1032),
(730000582, 101, 131),
(730000582, 133, 2),
(730000582, 140, 1),
(730000582, 146, 290000000),
(730000582, 307, 1200),
(730000582, 308, 400),
(730000582, 332, 20000),
(730000582, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000582, 6, 0, 3, 0, 11, 0, 0.0),
(730000582, 7, 0, 3, 0, 11, 0, 0.0),
(730000582, 14, 0, 3, 0, 11, 0, 0.0),
(730000582, 15, 0, 3, 0, 11, 0, 0.0),
(730000582, 20, 0, 3, 0, 11, 0, 0.0),
(730000582, 31, 0, 3, 0, 11, 0, 0.0),
(730000582, 33, 0, 3, 0, 11, 0, 0.0),
(730000582, 34, 0, 3, 0, 11, 0, 0.0),
(730000582, 44, 0, 3, 0, 11, 0, 0.0),
(730000582, 45, 0, 3, 0, 11, 0, 0.0),
(730000582, 46, 0, 3, 0, 11, 0, 0.0),
(730000582, 47, 0, 3, 0, 11, 0, 0.0),
(730000582, 48, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000582, 2038, 2.007999897003174),
(730000582, 4186, 2.0299999713897705),
(730000582, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000582, 1, 'Cinder Golem');
DELETE FROM `weenie` WHERE `class_Id` = 730000583;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000583, 'tou_ph_obsidian_minion_3', 10, '2026-08-10 23:19:50');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000583, 1, 11, 0, 0),
(730000583, 2, 11, 0, 0),
(730000583, 3, 11, 0, 0),
(730000583, 4, 11, 0, 0),
(730000583, 5, 11, 0, 0),
(730000583, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000583, 1, 110000, 0, 0, 110000),
(730000583, 3, 110000, 0, 0, 110000),
(730000583, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000583, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000583, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000583, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000583, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000583, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000583, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000583, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000583, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000583, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000583, 1, 1),
(730000583, 6, 1),
(730000583, 11, 0),
(730000583, 12, 1),
(730000583, 13, 0),
(730000583, 14, 1),
(730000583, 19, 1),
(730000583, 50, 1),
(730000583, 50050, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000583, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000583, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000583, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000583, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000583, 1, 33558118),
(730000583, 2, 150995065),
(730000583, 3, 536870982),
(730000583, 4, 805306402),
(730000583, 6, 67114050),
(730000583, 7, 268436515),
(730000583, 8, 100669115),
(730000583, 22, 872415336),
(730000583, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000583, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000583, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000583, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000583, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000583, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000583, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000583, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000583, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000583, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000583, 94),
(730000583, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000583, 1, 5.0),
(730000583, 2, 0.0),
(730000583, 3, 0.7),
(730000583, 4, 3.0),
(730000583, 5, 1.0),
(730000583, 13, 1.0),
(730000583, 14, 1.0),
(730000583, 15, 1.0),
(730000583, 16, 1.0),
(730000583, 17, 1.0),
(730000583, 18, 1.0),
(730000583, 19, 1.0),
(730000583, 31, 18.0),
(730000583, 34, 1.0),
(730000583, 36, 1.0),
(730000583, 39, 1.0),
(730000583, 64, 0.6),
(730000583, 65, 0.6),
(730000583, 66, 0.6),
(730000583, 67, 0.6),
(730000583, 68, 0.6),
(730000583, 69, 0.6),
(730000583, 70, 0.6),
(730000583, 71, 1.0),
(730000583, 72, 1.0),
(730000583, 73, 1.0),
(730000583, 74, 1.0),
(730000583, 75, 1.0),
(730000583, 80, 3.0),
(730000583, 104, 20.0),
(730000583, 122, 2.0),
(730000583, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000583, 1, 16),
(730000583, 2, 33),
(730000583, 3, 39),
(730000583, 6, -1),
(730000583, 7, -1),
(730000583, 16, 1),
(730000583, 25, 1100),
(730000583, 27, 0),
(730000583, 40, 2),
(730000583, 68, 9),
(730000583, 93, 1032),
(730000583, 101, 131),
(730000583, 133, 2),
(730000583, 140, 1),
(730000583, 146, 290000000),
(730000583, 307, 1200),
(730000583, 308, 400),
(730000583, 332, 20000),
(730000583, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000583, 6, 0, 3, 0, 11, 0, 0.0),
(730000583, 7, 0, 3, 0, 11, 0, 0.0),
(730000583, 14, 0, 3, 0, 11, 0, 0.0),
(730000583, 15, 0, 3, 0, 11, 0, 0.0),
(730000583, 20, 0, 3, 0, 11, 0, 0.0),
(730000583, 31, 0, 3, 0, 11, 0, 0.0),
(730000583, 33, 0, 3, 0, 11, 0, 0.0),
(730000583, 34, 0, 3, 0, 11, 0, 0.0),
(730000583, 44, 0, 3, 0, 11, 0, 0.0),
(730000583, 45, 0, 3, 0, 11, 0, 0.0),
(730000583, 46, 0, 3, 0, 11, 0, 0.0),
(730000583, 47, 0, 3, 0, 11, 0, 0.0),
(730000583, 48, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000583, 2038, 2.007999897003174),
(730000583, 4186, 2.0299999713897705),
(730000583, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000583, 1, 'Glasswing Chittick');
DELETE FROM `weenie` WHERE `class_Id` = 730000584;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000584, 'tou_ph_obsidian_minion_4', 10, '2026-08-10 23:19:50');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000584, 1, 11, 0, 0),
(730000584, 2, 11, 0, 0),
(730000584, 3, 11, 0, 0),
(730000584, 4, 11, 0, 0),
(730000584, 5, 11, 0, 0),
(730000584, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000584, 1, 110000, 0, 0, 110000),
(730000584, 3, 110000, 0, 0, 110000),
(730000584, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000584, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000584, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000584, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000584, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000584, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000584, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000584, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000584, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000584, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000584, 1, 1),
(730000584, 6, 1),
(730000584, 11, 0),
(730000584, 12, 1),
(730000584, 13, 0),
(730000584, 14, 1),
(730000584, 19, 1),
(730000584, 50, 1),
(730000584, 50050, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000584, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000584, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000584, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000584, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000584, 1, 33555879),
(730000584, 2, 150995072),
(730000584, 3, 536870986),
(730000584, 4, 805306399),
(730000584, 6, 67112444),
(730000584, 7, 268435808),
(730000584, 8, 100669720),
(730000584, 22, 872415333),
(730000584, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000584, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000584, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000584, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000584, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000584, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000584, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000584, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000584, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000584, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000584, 94),
(730000584, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000584, 1, 5.0),
(730000584, 2, 0.0),
(730000584, 3, 0.7),
(730000584, 4, 3.0),
(730000584, 5, 1.0),
(730000584, 12, 0.5),
(730000584, 13, 1.0),
(730000584, 14, 1.0),
(730000584, 15, 1.0),
(730000584, 16, 1.0),
(730000584, 17, 1.0),
(730000584, 18, 1.0),
(730000584, 19, 1.0),
(730000584, 31, 18.0),
(730000584, 34, 1.0),
(730000584, 36, 1.0),
(730000584, 39, 1.0),
(730000584, 64, 0.6),
(730000584, 65, 0.6),
(730000584, 66, 0.6),
(730000584, 67, 0.6),
(730000584, 68, 0.6),
(730000584, 69, 0.6),
(730000584, 70, 0.6),
(730000584, 71, 1.0),
(730000584, 72, 1.0),
(730000584, 73, 1.0),
(730000584, 74, 1.0),
(730000584, 75, 1.0),
(730000584, 80, 3.0),
(730000584, 104, 20.0),
(730000584, 122, 2.0),
(730000584, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000584, 1, 16),
(730000584, 2, 32),
(730000584, 3, 62),
(730000584, 6, -1),
(730000584, 7, -1),
(730000584, 16, 1),
(730000584, 25, 1100),
(730000584, 27, 0),
(730000584, 40, 2),
(730000584, 68, 9),
(730000584, 93, 1032),
(730000584, 101, 131),
(730000584, 133, 2),
(730000584, 140, 1),
(730000584, 146, 290000000),
(730000584, 307, 1200),
(730000584, 308, 400),
(730000584, 332, 20000),
(730000584, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000584, 6, 0, 3, 0, 11, 0, 0.0),
(730000584, 7, 0, 3, 0, 11, 0, 0.0),
(730000584, 14, 0, 3, 0, 11, 0, 0.0),
(730000584, 15, 0, 3, 0, 11, 0, 0.0),
(730000584, 20, 0, 3, 0, 11, 0, 0.0),
(730000584, 31, 0, 3, 0, 11, 0, 0.0),
(730000584, 33, 0, 3, 0, 11, 0, 0.0),
(730000584, 34, 0, 3, 0, 11, 0, 0.0),
(730000584, 44, 0, 3, 0, 11, 0, 0.0),
(730000584, 45, 0, 3, 0, 11, 0, 0.0),
(730000584, 46, 0, 3, 0, 11, 0, 0.0),
(730000584, 47, 0, 3, 0, 11, 0, 0.0),
(730000584, 48, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000584, 2038, 2.007999897003174),
(730000584, 4186, 2.0299999713897705),
(730000584, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000584, 1, 'Cinderhide Shreth');
DELETE FROM `weenie` WHERE `class_Id` = 730000601;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000601, 'tou_ph_pack_gen_land_1', 1, '2026-08-09 12:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000601, 1, 1),
(730000601, 11, 1),
(730000601, 132, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000601, 1, 33555051),
(730000601, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000601, 39, 2.0),
(730000601, 41, 300.0),
(730000601, 43, 5.0),
(730000601, 9034, 10.0);
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(730000601, -1.0, 730000501, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000601, -1.0, 730000551, 300.0, 3, 3, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000601, 81, 4),
(730000601, 82, 4),
(730000601, 93, 1044);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000601, 1, 'PH Land Pack Generator 1');
DELETE FROM `weenie` WHERE `class_Id` = 730000602;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000602, 'tou_ph_pack_gen_land_2', 1, '2026-08-09 12:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000602, 1, 1),
(730000602, 11, 1),
(730000602, 132, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000602, 1, 33555051),
(730000602, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000602, 39, 2.0),
(730000602, 41, 300.0),
(730000602, 43, 5.0),
(730000602, 9034, 10.0);
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(730000602, -1.0, 730000502, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000602, -1.0, 730000552, 300.0, 3, 3, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000602, 81, 4),
(730000602, 82, 4),
(730000602, 93, 1044);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000602, 1, 'PH Land Pack Generator 2');
DELETE FROM `weenie` WHERE `class_Id` = 730000603;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000603, 'tou_ph_pack_gen_land_3', 1, '2026-08-09 12:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000603, 1, 1),
(730000603, 11, 1),
(730000603, 132, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000603, 1, 33555051),
(730000603, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000603, 39, 2.0),
(730000603, 41, 300.0),
(730000603, 43, 5.0),
(730000603, 9034, 10.0);
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(730000603, -1.0, 730000503, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000603, -1.0, 730000553, 300.0, 3, 3, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000603, 81, 4),
(730000603, 82, 4),
(730000603, 93, 1044);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000603, 1, 'PH Land Pack Generator 3');
DELETE FROM `weenie` WHERE `class_Id` = 730000604;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000604, 'tou_ph_pack_gen_land_4', 1, '2026-08-09 12:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000604, 1, 1),
(730000604, 11, 1),
(730000604, 132, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000604, 1, 33555051),
(730000604, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000604, 39, 2.0),
(730000604, 41, 300.0),
(730000604, 43, 5.0),
(730000604, 9034, 10.0);
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(730000604, -1.0, 730000504, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000604, -1.0, 730000554, 300.0, 3, 3, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000604, 81, 4),
(730000604, 82, 4),
(730000604, 93, 1044);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000604, 1, 'PH Land Pack Generator 4');
DELETE FROM `weenie` WHERE `class_Id` = 730000605;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000605, 'tou_ph_pack_gen_beach_1', 1, '2026-08-09 12:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000605, 1, 1),
(730000605, 11, 1),
(730000605, 132, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000605, 1, 33555051),
(730000605, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000605, 39, 2.0),
(730000605, 41, 300.0),
(730000605, 43, 5.0),
(730000605, 9034, 10.0);
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(730000605, -1.0, 730000511, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000605, -1.0, 730000561, 300.0, 3, 3, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000605, 81, 4),
(730000605, 82, 4),
(730000605, 93, 1044);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000605, 1, 'PH Beach Pack Generator 1');
DELETE FROM `weenie` WHERE `class_Id` = 730000606;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000606, 'tou_ph_pack_gen_beach_2', 1, '2026-08-09 12:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000606, 1, 1),
(730000606, 11, 1),
(730000606, 132, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000606, 1, 33555051),
(730000606, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000606, 39, 2.0),
(730000606, 41, 300.0),
(730000606, 43, 5.0),
(730000606, 9034, 10.0);
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(730000606, -1.0, 730000512, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000606, -1.0, 730000562, 300.0, 3, 3, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000606, 81, 4),
(730000606, 82, 4),
(730000606, 93, 1044);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000606, 1, 'PH Beach Pack Generator 2');
DELETE FROM `weenie` WHERE `class_Id` = 730000607;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000607, 'tou_ph_pack_gen_beach_3', 1, '2026-08-09 12:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000607, 1, 1),
(730000607, 11, 1),
(730000607, 132, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000607, 1, 33555051),
(730000607, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000607, 39, 2.0),
(730000607, 41, 300.0),
(730000607, 43, 5.0),
(730000607, 9034, 10.0);
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(730000607, -1.0, 730000513, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000607, -1.0, 730000563, 300.0, 3, 3, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000607, 81, 4),
(730000607, 82, 4),
(730000607, 93, 1044);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000607, 1, 'PH Beach Pack Generator 3');
DELETE FROM `weenie` WHERE `class_Id` = 730000608;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000608, 'tou_ph_pack_gen_beach_4', 1, '2026-08-09 12:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000608, 1, 1),
(730000608, 11, 1),
(730000608, 132, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000608, 1, 33555051),
(730000608, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000608, 39, 2.0),
(730000608, 41, 300.0),
(730000608, 43, 5.0),
(730000608, 9034, 10.0);
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(730000608, -1.0, 730000514, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000608, -1.0, 730000564, 300.0, 3, 3, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000608, 81, 4),
(730000608, 82, 4),
(730000608, 93, 1044);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000608, 1, 'PH Beach Pack Generator 4');
DELETE FROM `weenie` WHERE `class_Id` = 730000609;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000609, 'tou_ph_pack_gen_water_1', 1, '2026-08-09 12:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000609, 1, 1),
(730000609, 11, 1),
(730000609, 132, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000609, 1, 33555051),
(730000609, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000609, 39, 2.0),
(730000609, 41, 300.0),
(730000609, 43, 5.0),
(730000609, 9034, 10.0);
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(730000609, -1.0, 730000521, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000609, -1.0, 730000571, 300.0, 3, 3, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000609, 81, 4),
(730000609, 82, 4),
(730000609, 93, 1044);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000609, 1, 'PH Water Pack Generator 1');
DELETE FROM `weenie` WHERE `class_Id` = 730000610;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000610, 'tou_ph_pack_gen_water_2', 1, '2026-08-09 12:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000610, 1, 1),
(730000610, 11, 1),
(730000610, 132, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000610, 1, 33555051),
(730000610, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000610, 39, 2.0),
(730000610, 41, 300.0),
(730000610, 43, 5.0),
(730000610, 9034, 10.0);
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(730000610, -1.0, 730000522, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000610, -1.0, 730000572, 300.0, 3, 3, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000610, 81, 4),
(730000610, 82, 4),
(730000610, 93, 1044);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000610, 1, 'PH Water Pack Generator 2');
DELETE FROM `weenie` WHERE `class_Id` = 730000611;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000611, 'tou_ph_pack_gen_water_3', 1, '2026-08-09 12:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000611, 1, 1),
(730000611, 11, 1),
(730000611, 132, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000611, 1, 33555051),
(730000611, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000611, 39, 2.0),
(730000611, 41, 300.0),
(730000611, 43, 5.0),
(730000611, 9034, 10.0);
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(730000611, -1.0, 730000523, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000611, -1.0, 730000573, 300.0, 3, 3, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000611, 81, 4),
(730000611, 82, 4),
(730000611, 93, 1044);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000611, 1, 'PH Water Pack Generator 3');
DELETE FROM `weenie` WHERE `class_Id` = 730000612;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000612, 'tou_ph_pack_gen_water_4', 1, '2026-08-09 12:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000612, 1, 1),
(730000612, 11, 1),
(730000612, 132, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000612, 1, 33555051),
(730000612, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000612, 39, 2.0),
(730000612, 41, 300.0),
(730000612, 43, 5.0),
(730000612, 9034, 10.0);
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(730000612, -1.0, 730000524, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000612, -1.0, 730000574, 300.0, 3, 3, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000612, 81, 4),
(730000612, 82, 4),
(730000612, 93, 1044);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000612, 1, 'PH Water Pack Generator 4');
DELETE FROM `weenie` WHERE `class_Id` = 730000613;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000613, 'tou_ph_pack_gen_obsidian_1', 1, '2026-08-09 12:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000613, 1, 1),
(730000613, 11, 1),
(730000613, 132, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000613, 1, 33555051),
(730000613, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000613, 39, 2.0),
(730000613, 41, 300.0),
(730000613, 43, 5.0),
(730000613, 9034, 10.0);
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(730000613, -1.0, 730000531, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000613, -1.0, 730000581, 300.0, 3, 3, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000613, 81, 4),
(730000613, 82, 4),
(730000613, 93, 1044);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000613, 1, 'PH Obsidian Pack Generator 1');
DELETE FROM `weenie` WHERE `class_Id` = 730000614;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000614, 'tou_ph_pack_gen_obsidian_2', 1, '2026-08-09 12:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000614, 1, 1),
(730000614, 11, 1),
(730000614, 132, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000614, 1, 33555051),
(730000614, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000614, 39, 2.0),
(730000614, 41, 300.0),
(730000614, 43, 5.0),
(730000614, 9034, 10.0);
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(730000614, -1.0, 730000532, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000614, -1.0, 730000582, 300.0, 3, 3, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000614, 81, 4),
(730000614, 82, 4),
(730000614, 93, 1044);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000614, 1, 'PH Obsidian Pack Generator 2');
DELETE FROM `weenie` WHERE `class_Id` = 730000615;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000615, 'tou_ph_pack_gen_obsidian_3', 1, '2026-08-09 12:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000615, 1, 1),
(730000615, 11, 1),
(730000615, 132, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000615, 1, 33555051),
(730000615, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000615, 39, 2.0),
(730000615, 41, 300.0),
(730000615, 43, 5.0),
(730000615, 9034, 10.0);
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(730000615, -1.0, 730000533, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000615, -1.0, 730000583, 300.0, 3, 3, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000615, 81, 4),
(730000615, 82, 4),
(730000615, 93, 1044);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000615, 1, 'PH Obsidian Pack Generator 3');
DELETE FROM `weenie` WHERE `class_Id` = 730000616;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000616, 'tou_ph_pack_gen_obsidian_4', 1, '2026-08-09 12:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000616, 1, 1),
(730000616, 11, 1),
(730000616, 132, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000616, 1, 33555051),
(730000616, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000616, 39, 2.0),
(730000616, 41, 300.0),
(730000616, 43, 5.0),
(730000616, 9034, 10.0);
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(730000616, -1.0, 730000534, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000616, -1.0, 730000584, 300.0, 3, 3, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000616, 81, 4),
(730000616, 82, 4),
(730000616, 93, 1044);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000616, 1, 'PH Obsidian Pack Generator 4');
DELETE FROM `weenie` WHERE `class_Id` = 730000691;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000691, 'tou_ph_pool_gen_land', 1, '2026-08-09 12:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000691, 1, 1),
(730000691, 11, 1),
(730000691, 132, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000691, 1, 33555051),
(730000691, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000691, 39, 2.0),
(730000691, 41, 300.0),
(730000691, 43, 5.0),
(730000691, 9034, 10.0);
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(730000691, 0.25, 730000601, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000691, 0.5, 730000602, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000691, 0.75, 730000603, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000691, 1.0, 730000604, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000691, 81, 1),
(730000691, 82, 1),
(730000691, 93, 1044);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000691, 1, 'PH Land Pool Generator');
DELETE FROM `weenie` WHERE `class_Id` = 730000692;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000692, 'tou_ph_pool_gen_beach', 1, '2026-08-09 12:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000692, 1, 1),
(730000692, 11, 1),
(730000692, 132, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000692, 1, 33555051),
(730000692, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000692, 39, 2.0),
(730000692, 41, 300.0),
(730000692, 43, 5.0),
(730000692, 9034, 10.0);
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(730000692, 0.25, 730000605, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000692, 0.5, 730000606, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000692, 0.75, 730000607, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000692, 1.0, 730000608, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000692, 81, 1),
(730000692, 82, 1),
(730000692, 93, 1044);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000692, 1, 'PH Beach Pool Generator');
DELETE FROM `weenie` WHERE `class_Id` = 730000693;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000693, 'tou_ph_pool_gen_water', 1, '2026-08-09 12:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000693, 1, 1),
(730000693, 11, 1),
(730000693, 132, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000693, 1, 33555051),
(730000693, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000693, 39, 2.0),
(730000693, 41, 300.0),
(730000693, 43, 5.0),
(730000693, 9034, 10.0);
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(730000693, 0.25, 730000609, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000693, 0.5, 730000610, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000693, 0.75, 730000611, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000693, 1.0, 730000612, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000693, 81, 1),
(730000693, 82, 1),
(730000693, 93, 1044);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000693, 1, 'PH Water Pool Generator');
DELETE FROM `weenie` WHERE `class_Id` = 730000694;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000694, 'tou_ph_pool_gen_obsidian', 1, '2026-08-09 12:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000694, 1, 1),
(730000694, 11, 1),
(730000694, 132, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000694, 1, 33555051),
(730000694, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000694, 39, 2.0),
(730000694, 41, 300.0),
(730000694, 43, 5.0),
(730000694, 9034, 10.0);
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(730000694, 0.25, 730000613, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000694, 0.5, 730000614, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000694, 0.75, 730000615, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0),
(730000694, 1.0, 730000616, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000694, 81, 1),
(730000694, 82, 1),
(730000694, 93, 1044);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000694, 1, 'PH Obsidian Pool Generator');
DELETE FROM `weenie` WHERE `class_Id` = 730000701;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000701, 'tou_ph_boss_gen_land', 1, '2026-08-08 18:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000701, 1, 1),
(730000701, 11, 1),
(730000701, 132, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000701, 1, 33555051),
(730000701, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000701, 39, 5.0),
(730000701, 41, 300.0),
(730000701, 43, 70.0),
(730000701, 9034, 10.0);
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(730000701, -1.0, 730000711, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000701, 81, 1),
(730000701, 82, 1),
(730000701, 93, 1044),
(730000701, 50108, 1);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000701, 1, 'PH Land Boss Generator');
DELETE FROM `weenie` WHERE `class_Id` = 730000702;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000702, 'tou_ph_boss_gen_beach', 1, '2026-08-08 18:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000702, 1, 1),
(730000702, 11, 1),
(730000702, 132, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000702, 1, 33555051),
(730000702, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000702, 39, 5.0),
(730000702, 41, 300.0),
(730000702, 43, 70.0),
(730000702, 9034, 10.0);
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(730000702, -1.0, 730000712, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000702, 81, 1),
(730000702, 82, 1),
(730000702, 93, 1044),
(730000702, 50108, 2);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000702, 1, 'PH Beach Boss Generator');
DELETE FROM `weenie` WHERE `class_Id` = 730000703;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000703, 'tou_ph_boss_gen_water', 1, '2026-08-08 18:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000703, 1, 1),
(730000703, 11, 1),
(730000703, 132, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000703, 1, 33555051),
(730000703, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000703, 39, 5.0),
(730000703, 41, 300.0),
(730000703, 43, 70.0),
(730000703, 9034, 10.0);
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(730000703, -1.0, 730000713, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000703, 81, 1),
(730000703, 82, 1),
(730000703, 93, 1044),
(730000703, 50108, 3);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000703, 1, 'PH Water Boss Generator');
DELETE FROM `weenie` WHERE `class_Id` = 730000704;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000704, 'tou_ph_boss_gen_obsidian', 1, '2026-08-08 18:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000704, 1, 1),
(730000704, 11, 1),
(730000704, 132, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000704, 1, 33555051),
(730000704, 8, 100667494);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000704, 39, 5.0),
(730000704, 41, 300.0),
(730000704, 43, 70.0),
(730000704, 9034, 10.0);
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(730000704, -1.0, 730000714, 300.0, 1, 1, 1, 2, -1, 0, 0.0, 0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000704, 81, 1),
(730000704, 82, 1),
(730000704, 93, 1044),
(730000704, 50108, 4);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000704, 1, 'PH Obsidian Boss Generator');
DELETE FROM `weenie` WHERE `class_Id` = 730000711;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000711, 'tou_ph_land_boss', 10, '2026-08-10 23:19:50');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000711, 1, 11, 0, 0),
(730000711, 2, 11, 0, 0),
(730000711, 3, 11, 0, 0),
(730000711, 4, 11, 0, 0),
(730000711, 5, 11, 0, 0),
(730000711, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000711, 1, 110000, 0, 0, 110000),
(730000711, 3, 110000, 0, 0, 110000),
(730000711, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000711, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000711, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000711, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000711, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000711, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000711, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000711, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000711, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000711, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000711, 1, 1),
(730000711, 6, 1),
(730000711, 11, 0),
(730000711, 12, 1),
(730000711, 13, 0),
(730000711, 14, 1),
(730000711, 19, 1),
(730000711, 50, 1),
(730000711, 50048, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000711, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000711, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000711, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000711, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000711, 1, 33559123),
(730000711, 2, 150995324),
(730000711, 3, 536871099),
(730000711, 4, 805306433),
(730000711, 6, 67116365),
(730000711, 7, 268436890),
(730000711, 8, 100677367),
(730000711, 22, 872415411),
(730000711, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000711, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000711, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000711, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000711, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000711, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000711, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000711, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000711, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000711, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000711, 94),
(730000711, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000711, 1, 5.0),
(730000711, 2, 0.0),
(730000711, 3, 0.7),
(730000711, 4, 3.0),
(730000711, 5, 1.0),
(730000711, 13, 1.0),
(730000711, 14, 1.0),
(730000711, 15, 1.0),
(730000711, 16, 1.0),
(730000711, 17, 1.0),
(730000711, 18, 1.0),
(730000711, 19, 1.0),
(730000711, 31, 18.0),
(730000711, 34, 1.0),
(730000711, 36, 1.0),
(730000711, 39, 3.0),
(730000711, 64, 0.6),
(730000711, 65, 0.6),
(730000711, 66, 0.6),
(730000711, 67, 0.6),
(730000711, 68, 0.6),
(730000711, 69, 0.6),
(730000711, 70, 0.6),
(730000711, 71, 1.0),
(730000711, 72, 1.0),
(730000711, 73, 1.0),
(730000711, 74, 1.0),
(730000711, 75, 1.0),
(730000711, 80, 3.0),
(730000711, 104, 20.0),
(730000711, 122, 2.0),
(730000711, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000711, 1, 16),
(730000711, 2, 82),
(730000711, 3, 82),
(730000711, 6, -1),
(730000711, 7, -1),
(730000711, 16, 1),
(730000711, 25, 1100),
(730000711, 27, 0),
(730000711, 40, 2),
(730000711, 68, 9),
(730000711, 93, 1032),
(730000711, 101, 131),
(730000711, 133, 2),
(730000711, 140, 1),
(730000711, 146, 290000000),
(730000711, 307, 1200),
(730000711, 308, 400),
(730000711, 332, 20000),
(730000711, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000711, 6, 0, 3, 0, 11, 0, 0.0),
(730000711, 7, 0, 3, 0, 11, 0, 0.0),
(730000711, 14, 0, 3, 0, 11, 0, 0.0),
(730000711, 15, 0, 3, 0, 11, 0, 0.0),
(730000711, 20, 0, 3, 0, 11, 0, 0.0),
(730000711, 31, 0, 3, 0, 11, 0, 0.0),
(730000711, 33, 0, 3, 0, 11, 0, 0.0),
(730000711, 34, 0, 3, 0, 11, 0, 0.0),
(730000711, 44, 0, 3, 0, 11, 0, 0.0),
(730000711, 45, 0, 3, 0, 11, 0, 0.0),
(730000711, 46, 0, 3, 0, 11, 0, 0.0),
(730000711, 47, 0, 3, 0, 11, 0, 0.0),
(730000711, 48, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000711, 2038, 2.007999897003174),
(730000711, 4186, 2.0299999713897705),
(730000711, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000711, 1, 'Grandcap the Overgrown');
DELETE FROM `weenie` WHERE `class_Id` = 730000712;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000712, 'tou_ph_beach_boss', 10, '2026-08-10 23:19:50');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000712, 1, 11, 0, 0),
(730000712, 2, 11, 0, 0),
(730000712, 3, 11, 0, 0),
(730000712, 4, 11, 0, 0),
(730000712, 5, 11, 0, 0),
(730000712, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000712, 1, 110000, 0, 0, 110000),
(730000712, 3, 110000, 0, 0, 110000),
(730000712, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000712, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000712, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000712, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000712, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000712, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000712, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000712, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000712, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000712, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000712, 1, 1),
(730000712, 6, 1),
(730000712, 11, 0),
(730000712, 12, 1),
(730000712, 13, 0),
(730000712, 14, 1),
(730000712, 19, 1),
(730000712, 50, 1),
(730000712, 50048, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000712, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000712, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000712, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000712, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000712, 1, 33559700),
(730000712, 2, 150995342),
(730000712, 3, 536871103),
(730000712, 4, 805306396),
(730000712, 6, 67116726),
(730000712, 7, 268437046),
(730000712, 8, 100667937),
(730000712, 22, 872415414),
(730000712, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000712, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000712, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000712, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000712, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000712, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000712, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000712, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000712, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000712, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000712, 94),
(730000712, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000712, 1, 5.0),
(730000712, 2, 0.0),
(730000712, 3, 0.7),
(730000712, 4, 3.0),
(730000712, 5, 1.0),
(730000712, 12, 0.5),
(730000712, 13, 1.0),
(730000712, 14, 1.0),
(730000712, 15, 1.0),
(730000712, 16, 1.0),
(730000712, 17, 1.0),
(730000712, 18, 1.0),
(730000712, 19, 1.0),
(730000712, 31, 18.0),
(730000712, 34, 1.0),
(730000712, 36, 1.0),
(730000712, 39, 3.0),
(730000712, 64, 0.6),
(730000712, 65, 0.6),
(730000712, 66, 0.6),
(730000712, 67, 0.6),
(730000712, 68, 0.6),
(730000712, 69, 0.6),
(730000712, 70, 0.6),
(730000712, 71, 1.0),
(730000712, 72, 1.0),
(730000712, 73, 1.0),
(730000712, 74, 1.0),
(730000712, 75, 1.0),
(730000712, 80, 3.0),
(730000712, 104, 20.0),
(730000712, 122, 2.0),
(730000712, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000712, 1, 16),
(730000712, 2, 84),
(730000712, 3, 85),
(730000712, 6, -1),
(730000712, 7, -1),
(730000712, 16, 1),
(730000712, 25, 1100),
(730000712, 27, 0),
(730000712, 40, 2),
(730000712, 68, 9),
(730000712, 93, 1032),
(730000712, 101, 131),
(730000712, 133, 2),
(730000712, 140, 1),
(730000712, 146, 290000000),
(730000712, 307, 1200),
(730000712, 308, 400),
(730000712, 332, 20000),
(730000712, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000712, 6, 0, 3, 0, 11, 0, 0.0),
(730000712, 7, 0, 3, 0, 11, 0, 0.0),
(730000712, 14, 0, 3, 0, 11, 0, 0.0),
(730000712, 15, 0, 3, 0, 11, 0, 0.0),
(730000712, 20, 0, 3, 0, 11, 0, 0.0),
(730000712, 24, 0, 3, 0, 11, 0, 0.0),
(730000712, 44, 0, 3, 0, 11, 0, 0.0),
(730000712, 45, 0, 3, 0, 11, 0, 0.0),
(730000712, 46, 0, 3, 0, 11, 0, 0.0),
(730000712, 47, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000712, 2038, 2.007999897003174),
(730000712, 4186, 2.0299999713897705),
(730000712, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000712, 1, 'The Drowned King');
DELETE FROM `weenie` WHERE `class_Id` = 730000713;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000713, 'tou_ph_water_boss', 10, '2026-08-10 23:19:50');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000713, 1, 11, 0, 0),
(730000713, 2, 11, 0, 0),
(730000713, 3, 11, 0, 0),
(730000713, 4, 11, 0, 0),
(730000713, 5, 11, 0, 0),
(730000713, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000713, 1, 110000, 0, 0, 110000),
(730000713, 3, 110000, 0, 0, 110000),
(730000713, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000713, 0, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000713, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000713, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000713, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000713, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000713, 5, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000713, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000713, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000713, 8, 32, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071),
(730000713, 22, 16, 11, 0.75, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000713, 1, 1),
(730000713, 6, 1),
(730000713, 11, 0),
(730000713, 12, 1),
(730000713, 13, 0),
(730000713, 14, 1),
(730000713, 19, 1),
(730000713, 50, 1),
(730000713, 50048, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000713, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000713, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000713, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000713, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000713, 1, 33556774),
(730000713, 2, 150995099),
(730000713, 3, 536871010),
(730000713, 4, 805306410),
(730000713, 6, 67112937),
(730000713, 7, 268436039),
(730000713, 8, 100670961),
(730000713, 22, 872415365),
(730000713, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000713, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000713, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000713, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000713, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000713, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000713, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000713, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000713, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000713, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000713, 94),
(730000713, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000713, 1, 5.0),
(730000713, 2, 0.0),
(730000713, 3, 0.7),
(730000713, 4, 3.0),
(730000713, 5, 1.0),
(730000713, 13, 1.0),
(730000713, 14, 1.0),
(730000713, 15, 1.0),
(730000713, 16, 1.0),
(730000713, 17, 1.0),
(730000713, 18, 1.0),
(730000713, 19, 1.0),
(730000713, 31, 18.0),
(730000713, 34, 1.0),
(730000713, 36, 1.0),
(730000713, 39, 3.0),
(730000713, 64, 0.6),
(730000713, 65, 0.6),
(730000713, 66, 0.6),
(730000713, 67, 0.6),
(730000713, 68, 0.6),
(730000713, 69, 0.6),
(730000713, 70, 0.6),
(730000713, 71, 1.0),
(730000713, 72, 1.0),
(730000713, 73, 1.0),
(730000713, 74, 1.0),
(730000713, 75, 1.0),
(730000713, 80, 3.0),
(730000713, 104, 20.0),
(730000713, 122, 2.0),
(730000713, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000713, 1, 16),
(730000713, 2, 45),
(730000713, 3, 14),
(730000713, 6, -1),
(730000713, 7, -1),
(730000713, 16, 1),
(730000713, 25, 1100),
(730000713, 27, 0),
(730000713, 40, 2),
(730000713, 68, 9),
(730000713, 93, 1032),
(730000713, 101, 131),
(730000713, 133, 2),
(730000713, 140, 1),
(730000713, 146, 290000000),
(730000713, 307, 1200),
(730000713, 308, 400),
(730000713, 332, 20000),
(730000713, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000713, 6, 0, 3, 0, 11, 0, 0.0),
(730000713, 7, 0, 3, 0, 11, 0, 0.0),
(730000713, 15, 0, 3, 0, 11, 0, 0.0),
(730000713, 20, 0, 3, 0, 11, 0, 0.0),
(730000713, 22, 0, 3, 0, 11, 0, 0.0),
(730000713, 24, 0, 3, 0, 11, 0, 0.0),
(730000713, 44, 0, 3, 0, 11, 0, 0.0),
(730000713, 45, 0, 3, 0, 11, 0, 0.0),
(730000713, 47, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000713, 2038, 2.007999897003174),
(730000713, 4186, 2.0299999713897705),
(730000713, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000713, 1, 'The Kraken');
DELETE FROM `weenie` WHERE `class_Id` = 730000714;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (730000714, 'tou_ph_obsidian_boss', 10, '2026-08-10 23:19:50');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(730000714, 1, 11, 0, 0),
(730000714, 2, 11, 0, 0),
(730000714, 3, 11, 0, 0),
(730000714, 4, 11, 0, 0),
(730000714, 5, 11, 0, 0),
(730000714, 6, 11, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(730000714, 1, 110000, 0, 0, 110000),
(730000714, 3, 110000, 0, 0, 110000),
(730000714, 5, 110000, 0, 0, 110000);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(730000714, 0, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(730000714, 1, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(730000714, 2, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(730000714, 3, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(730000714, 4, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(730000714, 5, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(730000714, 6, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(730000714, 7, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(730000714, 8, 4, 11, 0.75, 11, 11, 11, 11, 11, 11, 11, 11, 11, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(730000714, 1, 1),
(730000714, 6, 1),
(730000714, 11, 0),
(730000714, 12, 1),
(730000714, 13, 0),
(730000714, 14, 1),
(730000714, 19, 1),
(730000714, 50, 1),
(730000714, 50048, 1);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`) VALUES
(730000714, 9, 0, 0, 0, 0.9599999785423279, 0),
(730000714, 9, 0, 0, 0, 0.9700000286102295, 0),
(730000714, 9, 24477, 0, 0, 0.03999999910593033, 0),
(730000714, 9, 90000104, 0, 0, 0.5, 0);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(730000714, 1, 33556427),
(730000714, 2, 150995073),
(730000714, 3, 536870933),
(730000714, 4, 805306376),
(730000714, 8, 100667940),
(730000714, 22, 872415325),
(730000714, 35, 73001);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000714, 5, 0.02500000037252903, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000714, 5, 0.07000000029802322, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000714, 5, 0.0949999988079071, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000714, 5, 0.10000000149011612, NULL, 2147483708, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000714, 5, 0.05000000074505806, NULL, 2147483710, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000714, 5, 0.02500000037252903, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435540, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000714, 5, 0.07000000029802322, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435539, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000714, 5, 0.0949999988079071, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435538, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (730000714, 5, 0.10000000149011612, NULL, 2147483709, 1090519043, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 5, 0.0, 1.0, 268435537, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`) VALUES
(730000714, 94),
(730000714, 414);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(730000714, 1, 5.0),
(730000714, 2, 0.0),
(730000714, 3, 0.7),
(730000714, 4, 3.0),
(730000714, 5, 1.0),
(730000714, 13, 1.0),
(730000714, 14, 1.0),
(730000714, 15, 1.0),
(730000714, 16, 1.0),
(730000714, 17, 1.0),
(730000714, 18, 1.0),
(730000714, 19, 1.0),
(730000714, 31, 18.0),
(730000714, 34, 1.0),
(730000714, 36, 1.0),
(730000714, 39, 3.0),
(730000714, 64, 0.6),
(730000714, 65, 0.6),
(730000714, 66, 0.6),
(730000714, 67, 0.6),
(730000714, 68, 0.6),
(730000714, 69, 0.6),
(730000714, 70, 0.6),
(730000714, 71, 1.0),
(730000714, 72, 1.0),
(730000714, 73, 1.0),
(730000714, 74, 1.0),
(730000714, 75, 1.0),
(730000714, 80, 3.0),
(730000714, 104, 20.0),
(730000714, 122, 2.0),
(730000714, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(730000714, 1, 16),
(730000714, 2, 13),
(730000714, 6, -1),
(730000714, 7, -1),
(730000714, 16, 1),
(730000714, 25, 1100),
(730000714, 27, 0),
(730000714, 40, 2),
(730000714, 68, 9),
(730000714, 93, 1032),
(730000714, 101, 131),
(730000714, 133, 2),
(730000714, 140, 1),
(730000714, 146, 290000000),
(730000714, 307, 1200),
(730000714, 308, 400),
(730000714, 332, 20000),
(730000714, 350, 700);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(730000714, 6, 0, 3, 0, 11, 0, 0.0),
(730000714, 7, 0, 3, 0, 11, 0, 0.0),
(730000714, 14, 0, 3, 0, 11, 0, 0.0),
(730000714, 15, 0, 3, 0, 11, 0, 0.0),
(730000714, 20, 0, 3, 0, 11, 0, 0.0),
(730000714, 31, 0, 3, 0, 11, 0, 0.0),
(730000714, 33, 0, 3, 0, 11, 0, 0.0),
(730000714, 34, 0, 3, 0, 11, 0, 0.0),
(730000714, 44, 0, 3, 0, 11, 0, 0.0),
(730000714, 45, 0, 3, 0, 11, 0, 0.0),
(730000714, 46, 0, 3, 0, 11, 0, 0.0),
(730000714, 47, 0, 3, 0, 11, 0, 0.0),
(730000714, 48, 0, 3, 0, 11, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(730000714, 2038, 2.007999897003174),
(730000714, 4186, 2.0299999713897705),
(730000714, 4425, 2.075000047683716);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(730000714, 1, 'Vitrus the Molten');
DELETE FROM `weenie` WHERE `class_Id` = 739999994;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (739999994, 'beergoggles', 10, '2026-08-21 22:30:19');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(739999994, 1, 9350, 0, 0),
(739999994, 2, 3320, 0, 0),
(739999994, 3, 200, 0, 0),
(739999994, 4, 3240, 0, 0),
(739999994, 5, 3210, 0, 0),
(739999994, 6, 3205, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(739999994, 1, 3850290, 0, 0, 3850450),
(739999994, 3, 50100, 0, 0, 50420),
(739999994, 5, 3010, 0, 0, 30215);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(739999994, 0, 4, 0, 0.0, 1300, 320, 520, 240, 360, 400, 560, 400, 0, 1, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0, 0.33000001311302185, 0.0, 0.0),
(739999994, 1, 4, 0, 0.0, 1300, 320, 520, 240, 360, 400, 560, 400, 0, 2, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0, 0.4399999976158142, 0.17000000178813934, 0.0),
(739999994, 2, 4, 0, 0.0, 1300, 320, 520, 240, 260, 400, 560, 400, 0, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0),
(739999994, 3, 4, 0, 0.0, 1300, 320, 520, 240, 260, 400, 560, 400, 0, 1, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(739999994, 4, 4, 0, 0.0, 1300, 320, 520, 240, 260, 400, 560, 400, 0, 2, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(739999994, 5, 4, 0, 0.0, 1300, 320, 520, 240, 260, 400, 560, 400, 0, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.20000000298023224, 0.0),
(739999994, 6, 4, 0, 0.0, 1300, 320, 520, 240, 260, 400, 560, 400, 0, 3, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737),
(739999994, 7, 4, 0, 0.0, 1300, 320, 520, 240, 300, 400, 560, 400, 0, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(739999994, 8, 4, 0, 0.0, 1300, 320, 520, 240, 260, 400, 560, 400, 0, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(739999994, 1, 1),
(739999994, 11, 0),
(739999994, 12, 1),
(739999994, 13, 0),
(739999994, 29, 1),
(739999994, 50, 1),
(739999994, 52, 1),
(739999994, 103, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(739999994, 1, 33558437),
(739999994, 2, 150994967),
(739999994, 3, 536870934),
(739999994, 4, 805306368),
(739999994, 6, 67114480),
(739999994, 7, 268436672),
(739999994, 8, 100674805),
(739999994, 22, 872415272);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(739999994, 1, 5.0),
(739999994, 2, 0.0),
(739999994, 3, 5.0),
(739999994, 4, 6.0),
(739999994, 5, 2.0),
(739999994, 12, 0.5),
(739999994, 13, 1.0),
(739999994, 14, 1.3),
(739999994, 15, 1.3),
(739999994, 16, 0.68),
(739999994, 17, 0.36),
(739999994, 18, 0.2),
(739999994, 19, 1.0),
(739999994, 31, 250.0),
(739999994, 34, 0.0),
(739999994, 36, 1.0),
(739999994, 39, 2.0),
(739999994, 44, 180.0),
(739999994, 55, 190.0),
(739999994, 64, 0.21),
(739999994, 65, 0.21),
(739999994, 66, 0.21),
(739999994, 67, 0.3),
(739999994, 68, 0.31),
(739999994, 69, 0.421),
(739999994, 70, 0.11),
(739999994, 71, 1.0),
(739999994, 72, 1.0),
(739999994, 73, 1.0),
(739999994, 74, 1.0),
(739999994, 75, 1.0),
(739999994, 76, 0.95),
(739999994, 80, 0.0),
(739999994, 104, 1.0),
(739999994, 125, 1.0),
(739999994, 151, 0.9),
(739999994, 165, 0.0),
(739999994, 166, 0.491);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(739999994, 1, 16),
(739999994, 2, 101),
(739999994, 3, 85),
(739999994, 6, -1),
(739999994, 7, -1),
(739999994, 16, 1),
(739999994, 68, 64),
(739999994, 93, 4195336),
(739999994, 101, 512),
(739999994, 133, 1),
(739999994, 146, 40000000),
(739999994, 179, 4),
(739999994, 290, 1),
(739999994, 291, 300),
(739999994, 307, 1500),
(739999994, 308, 800),
(739999994, 313, 20),
(739999994, 314, 10),
(739999994, 331, 160),
(739999994, 332, 1000000),
(739999994, 350, 999),
(739999994, 351, 999),
(739999994, 386, 250);
INSERT INTO `weenie_properties_int64` (`object_Id`, `type`, `value`) VALUES
(739999994, 9010, 12500),
(739999994, 9011, 1000),
(739999994, 9022, 0);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(739999994, 6, 0, 3, 0, 7000, 0, 0.0),
(739999994, 7, 0, 3, 0, 3500, 0, 0.0),
(739999994, 15, 0, 3, 0, 5000, 0, 0.0),
(739999994, 20, 0, 2, 0, 3000, 0, 0.0),
(739999994, 22, 0, 2, 0, 1000, 0, 0.0),
(739999994, 24, 0, 2, 0, 50, 0, 0.0),
(739999994, 33, 0, 2, 0, 50000, 0, 0.0),
(739999994, 34, 0, 3, 0, 50000, 0, 0.0),
(739999994, 43, 0, 3, 0, 50000, 0, 0.0),
(739999994, 44, 0, 3, 0, 50000, 0, 0.0),
(739999994, 45, 0, 3, 0, 86325, 0, 0.0),
(739999994, 47, 0, 3, 0, 50000, 0, 0.0);
INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`) VALUES
(739999994, 4949, 2.990000009536743);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(739999994, 1, '');
DELETE FROM `weenie` WHERE `class_Id` = 739999995;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (739999995, 'draftedlook_739999995', 10, '2026-08-10 17:21:25');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(739999995, 1, 460, 0, 0),
(739999995, 2, 410, 0, 0),
(739999995, 3, 365, 0, 0),
(739999995, 4, 400, 0, 0),
(739999995, 5, 285, 0, 0),
(739999995, 6, 285, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(739999995, 1, 122049790, 0, 0, 122050000),
(739999995, 3, 3000, 0, 0, 3410),
(739999995, 5, 215, 0, 0, 500);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(739999995, 0, 2, 350, 0.75, 310, 155, 155, 155, 155, 155, 155, 155, 0, 1, 0.4955559968948364, 0.30000001192092896, 0.0, 0.4399999976158142, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0),
(739999995, 1, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 2, 0.3522219955921173, 0.17000000178813934, 0.0, 0.33000001311302185, 0.17000000178813934, 0.0, 0.33000001311302185, 0.17000000178813934, 0.0, 0.33000001311302185, 0.17000000178813934, 0.0),
(739999995, 2, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.0, 0.0, 0.0, 0.17000000178813934, 0.0),
(739999995, 3, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 1, 0.15222199261188507, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.17000000178813934, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(739999995, 4, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(739999995, 5, 4, 350, 0.75, 310, 155, 155, 155, 155, 155, 155, 155, 0, 2, 0.0, 0.10000000149011612, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.0, 0.0, 0.0, 0.20000000298023224, 0.0),
(739999995, 6, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 3, 0.0, 0.029999999329447746, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.4399999976158142, 0.12999999523162842, 0.18000000715255737),
(739999995, 7, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.4399999976158142, 0.20000000298023224, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(739999995, 8, 4, 350, 0.75, 310, 155, 155, 155, 155, 155, 155, 155, 0, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.029999999329447746, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071),
(739999995, 22, 16, 375, 0.5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(739999995, 1, 1),
(739999995, 12, 1),
(739999995, 14, 1),
(739999995, 19, 1),
(739999995, 50, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(739999995, 1, 33559990),
(739999995, 2, 150995348),
(739999995, 3, 536871107),
(739999995, 4, 805306435),
(739999995, 6, 67116771),
(739999995, 7, 268437061),
(739999995, 8, 100688542),
(739999995, 22, 872415417),
(739999995, 30, 86),
(739999995, 35, 1008);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (739999995, 3, 1.0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 22, 0.0, 1.0, NULL, 'GameHunterVeryHardTally2@#kt', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(739999995, 1, 5.0),
(739999995, 2, 0.0),
(739999995, 3, 0.9),
(739999995, 4, 0.5),
(739999995, 5, 2.0),
(739999995, 6, 0.1),
(739999995, 7, 0.25),
(739999995, 8, 0.3),
(739999995, 12, 0.5),
(739999995, 13, 0.79),
(739999995, 14, 0.9),
(739999995, 15, 1.0),
(739999995, 16, 0.84),
(739999995, 17, 0.84),
(739999995, 18, 0.84),
(739999995, 19, 0.84),
(739999995, 31, 30.0),
(739999995, 34, 1.5),
(739999995, 39, 1.2),
(739999995, 64, 0.75),
(739999995, 65, 1.0),
(739999995, 66, 1.0),
(739999995, 67, 0.75),
(739999995, 68, 0.75),
(739999995, 69, 0.42),
(739999995, 70, 0.25),
(739999995, 71, 0.25),
(739999995, 72, 0.25),
(739999995, 73, 0.25),
(739999995, 74, 0.25),
(739999995, 75, 0.25),
(739999995, 77, 1.0),
(739999995, 80, 4.0),
(739999995, 104, 10.0),
(739999995, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(739999995, 1, 16),
(739999995, 2, 89),
(739999995, 3, 39),
(739999995, 6, -1),
(739999995, 7, -1),
(739999995, 16, 1),
(739999995, 25, 185),
(739999995, 27, 0),
(739999995, 68, 3),
(739999995, 81, 2),
(739999995, 82, 2),
(739999995, 93, 1032),
(739999995, 103, 1),
(739999995, 133, 2),
(739999995, 146, 800000);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(739999995, 15, 0, 3, 0, 46585, 0, 0.0);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(739999995, 1, 'Drafted Look');
DELETE FROM `weenie` WHERE `class_Id` = 739999996;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (739999996, 'draftedlook_739999996', 10, '2026-08-10 17:21:25');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(739999996, 1, 460, 0, 0),
(739999996, 2, 410, 0, 0),
(739999996, 3, 365, 0, 0),
(739999996, 4, 400, 0, 0),
(739999996, 5, 285, 0, 0),
(739999996, 6, 285, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(739999996, 1, 122049790, 0, 0, 122050000),
(739999996, 3, 3000, 0, 0, 3410),
(739999996, 5, 215, 0, 0, 500);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(739999996, 0, 2, 350, 0.75, 310, 155, 155, 155, 155, 155, 155, 155, 0, 1, 0.4955559968948364, 0.30000001192092896, 0.0, 0.4399999976158142, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0),
(739999996, 1, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 2, 0.3522219955921173, 0.17000000178813934, 0.0, 0.33000001311302185, 0.17000000178813934, 0.0, 0.33000001311302185, 0.17000000178813934, 0.0, 0.33000001311302185, 0.17000000178813934, 0.0),
(739999996, 2, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.0, 0.0, 0.0, 0.17000000178813934, 0.0),
(739999996, 3, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 1, 0.15222199261188507, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.17000000178813934, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(739999996, 4, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(739999996, 5, 4, 350, 0.75, 310, 155, 155, 155, 155, 155, 155, 155, 0, 2, 0.0, 0.10000000149011612, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.0, 0.0, 0.0, 0.20000000298023224, 0.0),
(739999996, 6, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 3, 0.0, 0.029999999329447746, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.4399999976158142, 0.12999999523162842, 0.18000000715255737),
(739999996, 7, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.4399999976158142, 0.20000000298023224, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(739999996, 8, 4, 350, 0.75, 310, 155, 155, 155, 155, 155, 155, 155, 0, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.029999999329447746, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071),
(739999996, 22, 16, 375, 0.5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(739999996, 1, 1),
(739999996, 12, 1),
(739999996, 14, 1),
(739999996, 19, 1),
(739999996, 50, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(739999996, 1, 33559990),
(739999996, 2, 150995348),
(739999996, 3, 536871107),
(739999996, 4, 805306435),
(739999996, 6, 67116771),
(739999996, 7, 268437061),
(739999996, 8, 100688542),
(739999996, 22, 872415417),
(739999996, 30, 86),
(739999996, 35, 1008);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (739999996, 3, 1.0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 22, 0.0, 1.0, NULL, 'GameHunterVeryHardTally2@#kt', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(739999996, 1, 5.0),
(739999996, 2, 0.0),
(739999996, 3, 0.9),
(739999996, 4, 0.5),
(739999996, 5, 2.0),
(739999996, 6, 0.1),
(739999996, 7, 0.25),
(739999996, 8, 0.3),
(739999996, 12, 0.5),
(739999996, 13, 0.79),
(739999996, 14, 0.9),
(739999996, 15, 1.0),
(739999996, 16, 0.84),
(739999996, 17, 0.84),
(739999996, 18, 0.84),
(739999996, 19, 0.84),
(739999996, 31, 30.0),
(739999996, 34, 1.5),
(739999996, 39, 1.2),
(739999996, 64, 0.75),
(739999996, 65, 1.0),
(739999996, 66, 1.0),
(739999996, 67, 0.75),
(739999996, 68, 0.75),
(739999996, 69, 0.42),
(739999996, 70, 0.25),
(739999996, 71, 0.25),
(739999996, 72, 0.25),
(739999996, 73, 0.25),
(739999996, 74, 0.25),
(739999996, 75, 0.25),
(739999996, 77, 1.0),
(739999996, 80, 4.0),
(739999996, 104, 10.0),
(739999996, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(739999996, 1, 16),
(739999996, 2, 89),
(739999996, 3, 39),
(739999996, 6, -1),
(739999996, 7, -1),
(739999996, 16, 1),
(739999996, 25, 185),
(739999996, 27, 0),
(739999996, 68, 3),
(739999996, 81, 2),
(739999996, 82, 2),
(739999996, 93, 1032),
(739999996, 103, 1),
(739999996, 133, 2),
(739999996, 146, 800000);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(739999996, 15, 0, 3, 0, 46585, 0, 0.0);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(739999996, 1, 'Drafted Look');
DELETE FROM `weenie` WHERE `class_Id` = 739999997;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (739999997, 'draftedlook_739999997', 10, '2026-08-10 17:21:25');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(739999997, 1, 460, 0, 0),
(739999997, 2, 410, 0, 0),
(739999997, 3, 365, 0, 0),
(739999997, 4, 400, 0, 0),
(739999997, 5, 285, 0, 0),
(739999997, 6, 285, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(739999997, 1, 122049790, 0, 0, 122050000),
(739999997, 3, 3000, 0, 0, 3410),
(739999997, 5, 215, 0, 0, 500);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(739999997, 0, 2, 350, 0.75, 310, 155, 155, 155, 155, 155, 155, 155, 0, 1, 0.4955559968948364, 0.30000001192092896, 0.0, 0.4399999976158142, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0),
(739999997, 1, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 2, 0.3522219955921173, 0.17000000178813934, 0.0, 0.33000001311302185, 0.17000000178813934, 0.0, 0.33000001311302185, 0.17000000178813934, 0.0, 0.33000001311302185, 0.17000000178813934, 0.0),
(739999997, 2, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.0, 0.0, 0.0, 0.17000000178813934, 0.0),
(739999997, 3, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 1, 0.15222199261188507, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.17000000178813934, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(739999997, 4, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(739999997, 5, 4, 350, 0.75, 310, 155, 155, 155, 155, 155, 155, 155, 0, 2, 0.0, 0.10000000149011612, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.0, 0.0, 0.0, 0.20000000298023224, 0.0),
(739999997, 6, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 3, 0.0, 0.029999999329447746, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.4399999976158142, 0.12999999523162842, 0.18000000715255737),
(739999997, 7, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.4399999976158142, 0.20000000298023224, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(739999997, 8, 4, 350, 0.75, 310, 155, 155, 155, 155, 155, 155, 155, 0, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.029999999329447746, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071),
(739999997, 22, 16, 375, 0.5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(739999997, 1, 1),
(739999997, 12, 1),
(739999997, 14, 1),
(739999997, 19, 1),
(739999997, 50, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(739999997, 1, 33559990),
(739999997, 2, 150995348),
(739999997, 3, 536871107),
(739999997, 4, 805306435),
(739999997, 6, 67116771),
(739999997, 7, 268437061),
(739999997, 8, 100688542),
(739999997, 22, 872415417),
(739999997, 30, 86),
(739999997, 35, 1008);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (739999997, 3, 1.0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 22, 0.0, 1.0, NULL, 'GameHunterVeryHardTally2@#kt', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(739999997, 1, 5.0),
(739999997, 2, 0.0),
(739999997, 3, 0.9),
(739999997, 4, 0.5),
(739999997, 5, 2.0),
(739999997, 6, 0.1),
(739999997, 7, 0.25),
(739999997, 8, 0.3),
(739999997, 12, 0.5),
(739999997, 13, 0.79),
(739999997, 14, 0.9),
(739999997, 15, 1.0),
(739999997, 16, 0.84),
(739999997, 17, 0.84),
(739999997, 18, 0.84),
(739999997, 19, 0.84),
(739999997, 31, 30.0),
(739999997, 34, 1.5),
(739999997, 39, 1.2),
(739999997, 64, 0.75),
(739999997, 65, 1.0),
(739999997, 66, 1.0),
(739999997, 67, 0.75),
(739999997, 68, 0.75),
(739999997, 69, 0.42),
(739999997, 70, 0.25),
(739999997, 71, 0.25),
(739999997, 72, 0.25),
(739999997, 73, 0.25),
(739999997, 74, 0.25),
(739999997, 75, 0.25),
(739999997, 77, 1.0),
(739999997, 80, 4.0),
(739999997, 104, 10.0),
(739999997, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(739999997, 1, 16),
(739999997, 2, 89),
(739999997, 3, 39),
(739999997, 6, -1),
(739999997, 7, -1),
(739999997, 16, 1),
(739999997, 25, 185),
(739999997, 27, 0),
(739999997, 68, 3),
(739999997, 81, 2),
(739999997, 82, 2),
(739999997, 93, 1032),
(739999997, 103, 1),
(739999997, 133, 2),
(739999997, 146, 800000);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(739999997, 15, 0, 3, 0, 46585, 0, 0.0);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(739999997, 1, 'Drafted Look');
DELETE FROM `weenie` WHERE `class_Id` = 739999998;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (739999998, 'draftedlook_739999998', 10, '2026-08-10 17:21:25');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(739999998, 1, 460, 0, 0),
(739999998, 2, 410, 0, 0),
(739999998, 3, 365, 0, 0),
(739999998, 4, 400, 0, 0),
(739999998, 5, 285, 0, 0),
(739999998, 6, 285, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(739999998, 1, 122049790, 0, 0, 122050000),
(739999998, 3, 3000, 0, 0, 3410),
(739999998, 5, 215, 0, 0, 500);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(739999998, 0, 2, 350, 0.75, 310, 155, 155, 155, 155, 155, 155, 155, 0, 1, 0.4955559968948364, 0.30000001192092896, 0.0, 0.4399999976158142, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0),
(739999998, 1, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 2, 0.3522219955921173, 0.17000000178813934, 0.0, 0.33000001311302185, 0.17000000178813934, 0.0, 0.33000001311302185, 0.17000000178813934, 0.0, 0.33000001311302185, 0.17000000178813934, 0.0),
(739999998, 2, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.0, 0.0, 0.0, 0.17000000178813934, 0.0),
(739999998, 3, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 1, 0.15222199261188507, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.17000000178813934, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(739999998, 4, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(739999998, 5, 4, 350, 0.75, 310, 155, 155, 155, 155, 155, 155, 155, 0, 2, 0.0, 0.10000000149011612, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.0, 0.0, 0.0, 0.20000000298023224, 0.0),
(739999998, 6, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 3, 0.0, 0.029999999329447746, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.4399999976158142, 0.12999999523162842, 0.18000000715255737),
(739999998, 7, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.4399999976158142, 0.20000000298023224, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(739999998, 8, 4, 350, 0.75, 310, 155, 155, 155, 155, 155, 155, 155, 0, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.029999999329447746, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071),
(739999998, 22, 16, 375, 0.5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(739999998, 1, 1),
(739999998, 12, 1),
(739999998, 14, 1),
(739999998, 19, 1),
(739999998, 50, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(739999998, 1, 33559990),
(739999998, 2, 150995348),
(739999998, 3, 536871107),
(739999998, 4, 805306435),
(739999998, 6, 67116771),
(739999998, 7, 268437061),
(739999998, 8, 100688542),
(739999998, 22, 872415417),
(739999998, 30, 86),
(739999998, 35, 1008);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (739999998, 3, 1.0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 22, 0.0, 1.0, NULL, 'GameHunterVeryHardTally2@#kt', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(739999998, 1, 5.0),
(739999998, 2, 0.0),
(739999998, 3, 0.9),
(739999998, 4, 0.5),
(739999998, 5, 2.0),
(739999998, 6, 0.1),
(739999998, 7, 0.25),
(739999998, 8, 0.3),
(739999998, 12, 0.5),
(739999998, 13, 0.79),
(739999998, 14, 0.9),
(739999998, 15, 1.0),
(739999998, 16, 0.84),
(739999998, 17, 0.84),
(739999998, 18, 0.84),
(739999998, 19, 0.84),
(739999998, 31, 30.0),
(739999998, 34, 1.5),
(739999998, 39, 1.2),
(739999998, 64, 0.75),
(739999998, 65, 1.0),
(739999998, 66, 1.0),
(739999998, 67, 0.75),
(739999998, 68, 0.75),
(739999998, 69, 0.42),
(739999998, 70, 0.25),
(739999998, 71, 0.25),
(739999998, 72, 0.25),
(739999998, 73, 0.25),
(739999998, 74, 0.25),
(739999998, 75, 0.25),
(739999998, 77, 1.0),
(739999998, 80, 4.0),
(739999998, 104, 10.0),
(739999998, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(739999998, 1, 16),
(739999998, 2, 89),
(739999998, 3, 39),
(739999998, 6, -1),
(739999998, 7, -1),
(739999998, 16, 1),
(739999998, 25, 185),
(739999998, 27, 0),
(739999998, 68, 3),
(739999998, 81, 2),
(739999998, 82, 2),
(739999998, 93, 1032),
(739999998, 103, 1),
(739999998, 133, 2),
(739999998, 146, 800000);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(739999998, 15, 0, 3, 0, 46585, 0, 0.0);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(739999998, 1, 'Drafted Look');
DELETE FROM `weenie` WHERE `class_Id` = 739999999;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (739999999, 'draftedlook_739999999', 10, '2026-08-10 17:21:25');
INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES
(739999999, 1, 460, 0, 0),
(739999999, 2, 410, 0, 0),
(739999999, 3, 365, 0, 0),
(739999999, 4, 400, 0, 0),
(739999999, 5, 285, 0, 0),
(739999999, 6, 285, 0, 0);
INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES
(739999999, 1, 122049790, 0, 0, 122050000),
(739999999, 3, 3000, 0, 0, 3410),
(739999999, 5, 215, 0, 0, 500);
INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`) VALUES
(739999999, 0, 2, 350, 0.75, 310, 155, 155, 155, 155, 155, 155, 155, 0, 1, 0.4955559968948364, 0.30000001192092896, 0.0, 0.4399999976158142, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0),
(739999999, 1, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 2, 0.3522219955921173, 0.17000000178813934, 0.0, 0.33000001311302185, 0.17000000178813934, 0.0, 0.33000001311302185, 0.17000000178813934, 0.0, 0.33000001311302185, 0.17000000178813934, 0.0),
(739999999, 2, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 3, 0.0, 0.17000000178813934, 0.0, 0.0, 0.17000000178813934, 0.0, 0.0, 0.0, 0.0, 0.0, 0.17000000178813934, 0.0),
(739999999, 3, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 1, 0.15222199261188507, 0.029999999329447746, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0, 0.23000000417232513, 0.17000000178813934, 0.0, 0.23000000417232513, 0.029999999329447746, 0.0),
(739999999, 4, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 2, 0.0, 0.20000000298023224, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0, 0.0, 0.30000001192092896, 0.0),
(739999999, 5, 4, 350, 0.75, 310, 155, 155, 155, 155, 155, 155, 155, 0, 2, 0.0, 0.10000000149011612, 0.0, 0.0, 0.20000000298023224, 0.0, 0.0, 0.0, 0.0, 0.0, 0.20000000298023224, 0.0),
(739999999, 6, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 3, 0.0, 0.029999999329447746, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.0, 0.12999999523162842, 0.18000000715255737, 0.4399999976158142, 0.12999999523162842, 0.18000000715255737),
(739999999, 7, 4, 0, 0.0, 310, 155, 155, 155, 155, 155, 155, 155, 0, 3, 0.0, 0.0, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579, 0.4399999976158142, 0.20000000298023224, 0.6000000238418579, 0.0, 0.0, 0.6000000238418579),
(739999999, 8, 4, 350, 0.75, 310, 155, 155, 155, 155, 155, 155, 155, 0, 3, 0.0, 0.0, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071, 0.0, 0.029999999329447746, 0.2199999988079071, 0.0, 0.0, 0.2199999988079071),
(739999999, 22, 16, 375, 0.5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(739999999, 1, 1),
(739999999, 12, 1),
(739999999, 14, 1),
(739999999, 19, 1),
(739999999, 50, 1);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(739999999, 1, 33559990),
(739999999, 2, 150995348),
(739999999, 3, 536871107),
(739999999, 4, 805306435),
(739999999, 6, 67116771),
(739999999, 7, 268437061),
(739999999, 8, 100688542),
(739999999, 22, 872415417),
(739999999, 30, 86),
(739999999, 35, 1008);
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`) VALUES (739999999, 3, 1.0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`) VALUES
(@parent_id, 0, 22, 0.0, 1.0, NULL, 'GameHunterVeryHardTally2@#kt', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(739999999, 1, 5.0),
(739999999, 2, 0.0),
(739999999, 3, 0.9),
(739999999, 4, 0.5),
(739999999, 5, 2.0),
(739999999, 6, 0.1),
(739999999, 7, 0.25),
(739999999, 8, 0.3),
(739999999, 12, 0.5),
(739999999, 13, 0.79),
(739999999, 14, 0.9),
(739999999, 15, 1.0),
(739999999, 16, 0.84),
(739999999, 17, 0.84),
(739999999, 18, 0.84),
(739999999, 19, 0.84),
(739999999, 31, 30.0),
(739999999, 34, 1.5),
(739999999, 39, 1.2),
(739999999, 64, 0.75),
(739999999, 65, 1.0),
(739999999, 66, 1.0),
(739999999, 67, 0.75),
(739999999, 68, 0.75),
(739999999, 69, 0.42),
(739999999, 70, 0.25),
(739999999, 71, 0.25),
(739999999, 72, 0.25),
(739999999, 73, 0.25),
(739999999, 74, 0.25),
(739999999, 75, 0.25),
(739999999, 77, 1.0),
(739999999, 80, 4.0),
(739999999, 104, 10.0),
(739999999, 125, 1.0);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(739999999, 1, 16),
(739999999, 2, 89),
(739999999, 3, 39),
(739999999, 6, -1),
(739999999, 7, -1),
(739999999, 16, 1),
(739999999, 25, 185),
(739999999, 27, 0),
(739999999, 68, 3),
(739999999, 81, 2),
(739999999, 82, 2),
(739999999, 93, 1032),
(739999999, 103, 1),
(739999999, 133, 2),
(739999999, 146, 800000);
INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`) VALUES
(739999999, 15, 0, 3, 0, 46585, 0, 0.0);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(739999999, 1, 'Drafted Look');
