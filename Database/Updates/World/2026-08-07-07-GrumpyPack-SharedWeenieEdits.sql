-- Carry-over: 1 shared custom wcid where the test version is authoritative - the Tou Tou
-- prestige recall gem 777700029 (live gems exist in players' packs). Full replace, canonical
-- format.
-- (The 7 benched camp gens this script used to replace were removed 2026-08-08 with the rest
-- of the Tou Tou mob layer - see 2026-08-08-01-GrumpyPack-BenchedGens-ILT-Delete.sql and
-- Migration pruned-archive\toutou-mobs-2026-08-08.)

DELETE FROM `weenie`
WHERE `class_Id` = 777700029;

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES ('777700029','777700029TouTouPrestigeRecallGem','38','2026-06-04 23:25:00');

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES ('777700029','15',1)
     , ('777700029','63',1);

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES ('777700029','1','33557625')
     , ('777700029','3','536870932')
     , ('777700029','6','67111919')
     , ('777700029','7','268435723')
     , ('777700029','8','100668362')
     , ('777700029','22','872415275');

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES ('777700029','1','2048')
     , ('777700029','3','8')
     , ('777700029','5','10')
     , ('777700029','8','10')
     , ('777700029','9','0')
     , ('777700029','11','1')
     , ('777700029','12','1')
     , ('777700029','13','10')
     , ('777700029','14','10')
     , ('777700029','15','50')
     , ('777700029','16','8')
     , ('777700029','18','1')
     , ('777700029','19','50')
     , ('777700029','33','1')
     , ('777700029','93','3092')
     , ('777700029','94','16')
     , ('777700029','106','210')
     , ('777700029','107','50')
     , ('777700029','108','50')
     , ('777700029','114','1')
     , ('777700029','150','103')
     , ('777700029','151','2');

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES ('777700029','1','Tou Tou Prestige Portal Gem')
     , ('777700029','16','Use this gem to be teleported to the Tou Tou Prestige Area.');

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`, `damage_type`)
VALUES ('777700029','7','1',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL);
SET @parent = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent, '0','13','0','1',NULL,'Analyzing spatial coordinates... Target set to Tou Tou Prestige Zone. Adjusting planar frequency to Prestige Variant 11. Commencing teleportation.',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL)
     , (@parent, '1','99','0','1',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'11',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'4116250676','152.59','80.8','20.005','0.92388','0','0','-0.382683');
