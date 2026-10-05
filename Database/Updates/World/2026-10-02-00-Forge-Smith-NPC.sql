-- ------------------------------------------------------------------------------------
-- Aldric the Forgemaster (NPC) - wcid 78780400
-- Blacksmithing forge smith, first id of the blacksmithing block 78780400+ (docs/WCID_ALLOCATION_7878.md).
-- Body, clothes and stats copied from retail 712 holtburgblacksmith (Sedor Wystan the Blacksmith), with the
-- vendor parts removed: the type is Creature (10), not Vendor, and the shop list, prices and vendor emotes are gone.
--
-- PropertyBool 52000 ForgeSmith makes him a forge smith: a weapon handed to him is never taken. The first one
-- is noted as the player's main weapon, the second is offered as a forge (ForgeService.HandleGive). He does
-- nothing while ServerConfig forge_enabled is FALSE except say the forge is cold.
--
-- Not placed by this file: put him where the smithy should be (an ILT-Anvil, wcid 3110304, makes a good prop).
-- Every string here is plain ASCII: the client cannot draw anything else.
-- ------------------------------------------------------------------------------------
-- Narrow on purpose: removes only this weenie (id AND class_Name). If the id has been taken by
-- something else, that weenie is left alone and the INSERT below fails on the primary key instead
-- of silently replacing it. Child rows follow the parent (ON DELETE CASCADE).
DELETE FROM `weenie` WHERE `class_Id` = 78780400 AND `class_Name` = 'npc-forge-smith';
-- his id before the move from 787803xx (2026-10-05); same narrow rule
DELETE FROM `weenie` WHERE `class_Id` = 78780300 AND `class_Name` = 'npc-forge-smith';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780400, 'npc-forge-smith', 10, '2026-10-02 12:00:00') /* Creature */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780400, 1, 16)
     , (78780400, 2, 31)
     , (78780400, 6, -1)
     , (78780400, 7, -1)
     , (78780400, 8, 120)
     , (78780400, 16, 32)
     , (78780400, 25, 7)
     , (78780400, 27, 0)
     , (78780400, 93, 2098200)
     , (78780400, 133, 4)
     , (78780400, 134, 16)
     , (78780400, 146, 133);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780400, 1, True)
     , (78780400, 12, True)
     , (78780400, 13, False)
     , (78780400, 19, False)
     , (78780400, 41, True)
     , (78780400, 52000, True);

INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`)
VALUES (78780400, 1, 5)
     , (78780400, 2, 0)
     , (78780400, 3, 0.16)
     , (78780400, 4, 5)
     , (78780400, 5, 1)
     , (78780400, 11, 300)
     , (78780400, 13, 0.9)
     , (78780400, 14, 1)
     , (78780400, 15, 1.1)
     , (78780400, 16, 0.4)
     , (78780400, 17, 0.4)
     , (78780400, 18, 1)
     , (78780400, 19, 0.6)
     , (78780400, 54, 6)
     , (78780400, 64, 1)
     , (78780400, 65, 1)
     , (78780400, 66, 1)
     , (78780400, 67, 1)
     , (78780400, 68, 1)
     , (78780400, 69, 1)
     , (78780400, 70, 1)
     , (78780400, 71, 1)
     , (78780400, 72, 1)
     , (78780400, 73, 1)
     , (78780400, 74, 1)
     , (78780400, 75, 1)
     , (78780400, 104, 10)
     , (78780400, 125, 1);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780400, 1, 'Aldric the Forgemaster')
     , (78780400, 3, 'Male')
     , (78780400, 4, 'Aluvian')
     , (78780400, 5, 'Forgemaster');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780400, 1, 0x02000001)
     , (78780400, 2, 0x09000001)
     , (78780400, 3, 0x20000001)
     , (78780400, 4, 0x30000000)
     , (78780400, 8, 0x06001036);

INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`)
VALUES (78780400, 1, 80, 0, 0)
     , (78780400, 2, 70, 0, 0)
     , (78780400, 3, 50, 0, 0)
     , (78780400, 4, 70, 0, 0)
     , (78780400, 5, 30, 0, 0)
     , (78780400, 6, 30, 0, 0);

INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`)
VALUES (78780400, 1, 60, 0, 0, 95)
     , (78780400, 3, 75, 0, 0, 145)
     , (78780400, 5, 40, 0, 0, 70);

INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`)
VALUES (78780400, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0.33, 0, 0, 0.33, 0, 0, 0.33, 0, 0, 0.33, 0, 0)
     , (78780400, 1, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0.44, 0.17, 0, 0.44, 0.17, 0, 0.44, 0.17, 0, 0.44, 0.17, 0)
     , (78780400, 2, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0.17, 0, 0, 0.17, 0, 0, 0.17, 0, 0, 0.17, 0)
     , (78780400, 3, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0.23, 0.03, 0, 0.23, 0.03, 0, 0.23, 0.03, 0, 0.23, 0.03, 0)
     , (78780400, 4, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0.3, 0, 0, 0.3, 0, 0, 0.3, 0, 0, 0.3, 0)
     , (78780400, 5, 4, 2, 0.75, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0.2, 0, 0, 0.2, 0, 0, 0.2, 0, 0, 0.2, 0)
     , (78780400, 6, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0.13, 0.18, 0, 0.13, 0.18, 0, 0.13, 0.18, 0, 0.13, 0.18)
     , (78780400, 7, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0.6, 0, 0, 0.6, 0, 0, 0.6, 0, 0, 0.6)
     , (78780400, 8, 4, 2, 0.75, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0.22, 0, 0, 0.22, 0, 0, 0.22, 0, 0, 0.22);

INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`)
VALUES (78780400, 2, 303, 0, 0, 0, False)
     , (78780400, 2, 124, 0, 8, 0.67, False)
     , (78780400, 2, 117, 0, 8, 0.67, False)
     , (78780400, 2, 132, 0, 7, 0.33, False)
     , (78780400, 2, 10696, 0, 4, 0.5, False);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780400, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 10 /* Tell */, 0, 1, NULL, 'Hand me the weapon or the piece of armour you mean to keep. It holds its shape, its tinkering and its colour.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
     , (@parent_id, 1, 10 /* Tell */, 3, 1, NULL, 'Then hand me a second of its kind: sword to sword, helm to helm. The new piece draws every quality from the pair, and the second is melted down for good.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
     , (@parent_id, 2, 10 /* Tell */, 3, 1, NULL, 'What leaves my forge is bound to you. I will name my price before I strike.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
