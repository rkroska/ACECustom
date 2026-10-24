-- CHARM UPGRADE GEMS, part 2 (owner 2026-10-21): raise the Armor Breaker / Shield Arc charm level.
-- Exactly as 777700071-777700082: Use emote PromptAddAugment (123) with message CharmLevel_<charm>_<levels>,
-- the charm must be in the pack, never past level 100, and each level also charges the charm's live gemcost setting.
-- Vendor prices (Value) are PLACEHOLDERS: 1 coin a level. The two charms also go on Enephior's stock (19860002).
-- Safe to run again: it replaces the six weenies and Enephior's rows for these items.

DELETE FROM `weenie_properties_emote` WHERE `object_Id` IN (777700083, 777700084, 777700085, 777700086, 777700087, 777700088);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` IN (777700083, 777700084, 777700085, 777700086, 777700087, 777700088);
DELETE FROM `weenie_properties_int` WHERE `object_Id` IN (777700083, 777700084, 777700085, 777700086, 777700087, 777700088);
DELETE FROM `weenie_properties_d_i_d` WHERE `object_Id` IN (777700083, 777700084, 777700085, 777700086, 777700087, 777700088);
DELETE FROM `weenie_properties_string` WHERE `object_Id` IN (777700083, 777700084, 777700085, 777700086, 777700087, 777700088);
DELETE FROM `weenie` WHERE `class_Id` IN (777700083, 777700084, 777700085, 777700086, 777700087, 777700088);

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700083, 'ilt_armorbreaker_gem_1', 38, NOW())
     , (777700084, 'ilt_armorbreaker_gem_5', 38, NOW())
     , (777700085, 'ilt_armorbreaker_gem_10', 38, NOW())
     , (777700086, 'ilt_shieldarc_gem_1', 38, NOW())
     , (777700087, 'ilt_shieldarc_gem_5', 38, NOW())
     , (777700088, 'ilt_shieldarc_gem_10', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)  /* 63 UnlimitedUse: only the handler uses it up */
VALUES (777700083, 63, 1)
     , (777700084, 63, 1)
     , (777700085, 63, 1)
     , (777700086, 63, 1)
     , (777700087, 63, 1)
     , (777700088, 63, 1);

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700083,   1, 2048)
     , (777700083,   5, 5)
     , (777700083,  11, 1)
     , (777700083,  12, 1)
     , (777700083,  16, 8)
     , (777700083,  33, 1)
     , (777700083,  91, 1)
     , (777700083,  92, 1)
     , (777700083,  93, 1044)
     , (777700083,  94, 16)
     , (777700083, 114, 1)
     , (777700083,  19, 1)
     , (777700084,   1, 2048)
     , (777700084,   5, 5)
     , (777700084,  11, 1)
     , (777700084,  12, 1)
     , (777700084,  16, 8)
     , (777700084,  33, 1)
     , (777700084,  91, 1)
     , (777700084,  92, 1)
     , (777700084,  93, 1044)
     , (777700084,  94, 16)
     , (777700084, 114, 1)
     , (777700084,  19, 5)
     , (777700085,   1, 2048)
     , (777700085,   5, 5)
     , (777700085,  11, 1)
     , (777700085,  12, 1)
     , (777700085,  16, 8)
     , (777700085,  33, 1)
     , (777700085,  91, 1)
     , (777700085,  92, 1)
     , (777700085,  93, 1044)
     , (777700085,  94, 16)
     , (777700085, 114, 1)
     , (777700085,  19, 10)
     , (777700086,   1, 2048)
     , (777700086,   5, 5)
     , (777700086,  11, 1)
     , (777700086,  12, 1)
     , (777700086,  16, 8)
     , (777700086,  33, 1)
     , (777700086,  91, 1)
     , (777700086,  92, 1)
     , (777700086,  93, 1044)
     , (777700086,  94, 16)
     , (777700086, 114, 1)
     , (777700086,  19, 1)
     , (777700087,   1, 2048)
     , (777700087,   5, 5)
     , (777700087,  11, 1)
     , (777700087,  12, 1)
     , (777700087,  16, 8)
     , (777700087,  33, 1)
     , (777700087,  91, 1)
     , (777700087,  92, 1)
     , (777700087,  93, 1044)
     , (777700087,  94, 16)
     , (777700087, 114, 1)
     , (777700087,  19, 5)
     , (777700088,   1, 2048)
     , (777700088,   5, 5)
     , (777700088,  11, 1)
     , (777700088,  12, 1)
     , (777700088,  16, 8)
     , (777700088,  33, 1)
     , (777700088,  91, 1)
     , (777700088,  92, 1)
     , (777700088,  93, 1044)
     , (777700088,  94, 16)
     , (777700088, 114, 1)
     , (777700088,  19, 10);

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)  /* setup, sound, palette, icon, physics effects = the growth gems' (icon PLACEHOLDER) */
VALUES (777700083,  1, 33556769)
     , (777700083,  3, 536870932)
     , (777700083,  6, 67111919)
     , (777700083,  8, 100668360)
     , (777700083, 22, 872415275)
     , (777700084,  1, 33556769)
     , (777700084,  3, 536870932)
     , (777700084,  6, 67111919)
     , (777700084,  8, 100668360)
     , (777700084, 22, 872415275)
     , (777700085,  1, 33556769)
     , (777700085,  3, 536870932)
     , (777700085,  6, 67111919)
     , (777700085,  8, 100668360)
     , (777700085, 22, 872415275)
     , (777700086,  1, 33556769)
     , (777700086,  3, 536870932)
     , (777700086,  6, 67111919)
     , (777700086,  8, 100668362)
     , (777700086, 22, 872415275)
     , (777700087,  1, 33556769)
     , (777700087,  3, 536870932)
     , (777700087,  6, 67111919)
     , (777700087,  8, 100668362)
     , (777700087, 22, 872415275)
     , (777700088,  1, 33556769)
     , (777700088,  3, 536870932)
     , (777700088,  6, 67111919)
     , (777700088,  8, 100668362)
     , (777700088, 22, 872415275);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700083,  1, 'Chipped Armor Breaker Upgrade Gem (+1)')
     , (777700083, 14, 'Double click to raise your Armor Breaker charm by 1 level, up to level 100. The charm must be in your pack. Each level also costs Prestige Coins (pack or bank), more as the charm climbs; you are asked before any are spent.')
     , (777700083, 15, 'A gem that deepens the power of an Armor Breaker charm.')
     , (777700084,  1, 'Flawed Armor Breaker Upgrade Gem (+5)')
     , (777700084, 14, 'Double click to raise your Armor Breaker charm by 5 levels, up to level 100. The charm must be in your pack. Each level also costs Prestige Coins (pack or bank), more as the charm climbs; you are asked before any are spent.')
     , (777700084, 15, 'A gem that deepens the power of an Armor Breaker charm.')
     , (777700085,  1, 'Flawless Armor Breaker Upgrade Gem (+10)')
     , (777700085, 14, 'Double click to raise your Armor Breaker charm by 10 levels, up to level 100. The charm must be in your pack. Each level also costs Prestige Coins (pack or bank), more as the charm climbs; you are asked before any are spent.')
     , (777700085, 15, 'A gem that deepens the power of an Armor Breaker charm.')
     , (777700086,  1, 'Chipped Shield Arc Upgrade Gem (+1)')
     , (777700086, 14, 'Double click to raise your Shield Arc charm by 1 level, up to level 100. The charm must be in your pack. Each level also costs Prestige Coins (pack or bank), more as the charm climbs; you are asked before any are spent.')
     , (777700086, 15, 'A gem that deepens the power of a Shield Arc charm.')
     , (777700087,  1, 'Flawed Shield Arc Upgrade Gem (+5)')
     , (777700087, 14, 'Double click to raise your Shield Arc charm by 5 levels, up to level 100. The charm must be in your pack. Each level also costs Prestige Coins (pack or bank), more as the charm climbs; you are asked before any are spent.')
     , (777700087, 15, 'A gem that deepens the power of a Shield Arc charm.')
     , (777700088,  1, 'Flawless Shield Arc Upgrade Gem (+10)')
     , (777700088, 14, 'Double click to raise your Shield Arc charm by 10 levels, up to level 100. The charm must be in your pack. Each level also costs Prestige Coins (pack or bank), more as the charm climbs; you are asked before any are spent.')
     , (777700088, 15, 'A gem that deepens the power of a Shield Arc charm.');

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (777700083, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `message`)
VALUES (@parent_id, 0, 123 /* PromptAddAugment */, 0, 1, 'CharmLevel_armorbreaker_1');

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (777700084, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `message`)
VALUES (@parent_id, 0, 123 /* PromptAddAugment */, 0, 1, 'CharmLevel_armorbreaker_5');

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (777700085, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `message`)
VALUES (@parent_id, 0, 123 /* PromptAddAugment */, 0, 1, 'CharmLevel_armorbreaker_10');

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (777700086, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `message`)
VALUES (@parent_id, 0, 123 /* PromptAddAugment */, 0, 1, 'CharmLevel_shieldarc_1');

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (777700087, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `message`)
VALUES (@parent_id, 0, 123 /* PromptAddAugment */, 0, 1, 'CharmLevel_shieldarc_5');

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (777700088, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `message`)
VALUES (@parent_id, 0, 123 /* PromptAddAugment */, 0, 1, 'CharmLevel_shieldarc_10');

-- Enephior's stock: the two charms (Value 1 = 1 coin, PLACEHOLDER) and the six gems.
DELETE FROM `weenie_properties_create_list` WHERE `object_Id` = 19860002 AND `weenie_Class_Id` IN (777700039, 777700040, 777700083, 777700084, 777700085, 777700086, 777700087, 777700088);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`)
VALUES (19860002, 4, 777700039, -1, 0, 0, 0)
     , (19860002, 4, 777700040, -1, 0, 0, 0)
     , (19860002, 4, 777700083, -1, 0, 0, 0)
     , (19860002, 4, 777700084, -1, 0, 0, 0)
     , (19860002, 4, 777700085, -1, 0, 0, 0)
     , (19860002, 4, 777700086, -1, 0, 0, 0)
     , (19860002, 4, 777700087, -1, 0, 0, 0)
     , (19860002, 4, 777700088, -1, 0, 0, 0);
