-- CHARM UPGRADE GEMS (owner 2026-10-17): raise a levelled charm's level (Split Arrow, Chain, Pandemic, Improved Cleave).
-- The level lives on the PLAYER (PropertyInt64 51200-51203); the charm item only shows it. Bought from Enephior
-- (19860002, Prestige Palace) for Prestige Coins. Using one: the charm must be in the pack, it never goes past level 100,
-- and it also charges the charm's live /charm setting gemcost in Prestige Coins (pack first, then bank) for EACH level it
-- adds, read at the level being left - so the cost rises with the level. The gem is used up.
-- Use emote = PromptAddAugment (123), message CharmLevel_<charm>_<levels> (Player_CharmLevels.cs).
-- Vendor prices (Value) are PLACEHOLDERS: 1 coin a level. Charms 777700035-038 also go on Enephior's stock.
-- Safe to run again: it replaces the twelve weenies and Enephior's rows for these items.

DELETE FROM `weenie_properties_emote` WHERE `object_Id` IN (777700071, 777700072, 777700073, 777700074, 777700075, 777700076, 777700077, 777700078, 777700079, 777700080, 777700081, 777700082);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` IN (777700071, 777700072, 777700073, 777700074, 777700075, 777700076, 777700077, 777700078, 777700079, 777700080, 777700081, 777700082);
DELETE FROM `weenie_properties_int` WHERE `object_Id` IN (777700071, 777700072, 777700073, 777700074, 777700075, 777700076, 777700077, 777700078, 777700079, 777700080, 777700081, 777700082);
DELETE FROM `weenie_properties_d_i_d` WHERE `object_Id` IN (777700071, 777700072, 777700073, 777700074, 777700075, 777700076, 777700077, 777700078, 777700079, 777700080, 777700081, 777700082);
DELETE FROM `weenie_properties_string` WHERE `object_Id` IN (777700071, 777700072, 777700073, 777700074, 777700075, 777700076, 777700077, 777700078, 777700079, 777700080, 777700081, 777700082);
DELETE FROM `weenie` WHERE `class_Id` IN (777700071, 777700072, 777700073, 777700074, 777700075, 777700076, 777700077, 777700078, 777700079, 777700080, 777700081, 777700082);

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700071, 'ilt_splitarrow_gem_1', 38, NOW())
     , (777700072, 'ilt_splitarrow_gem_5', 38, NOW())
     , (777700073, 'ilt_splitarrow_gem_10', 38, NOW())
     , (777700074, 'ilt_chain_gem_1', 38, NOW())
     , (777700075, 'ilt_chain_gem_5', 38, NOW())
     , (777700076, 'ilt_chain_gem_10', 38, NOW())
     , (777700077, 'ilt_pandemic_gem_1', 38, NOW())
     , (777700078, 'ilt_pandemic_gem_5', 38, NOW())
     , (777700079, 'ilt_pandemic_gem_10', 38, NOW())
     , (777700080, 'ilt_improvedcleave_gem_1', 38, NOW())
     , (777700081, 'ilt_improvedcleave_gem_5', 38, NOW())
     , (777700082, 'ilt_improvedcleave_gem_10', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)  /* 63 UnlimitedUse: only the handler uses it up */
VALUES (777700071, 63, 1)
     , (777700072, 63, 1)
     , (777700073, 63, 1)
     , (777700074, 63, 1)
     , (777700075, 63, 1)
     , (777700076, 63, 1)
     , (777700077, 63, 1)
     , (777700078, 63, 1)
     , (777700079, 63, 1)
     , (777700080, 63, 1)
     , (777700081, 63, 1)
     , (777700082, 63, 1);

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700071,   1, 2048) /* ItemType Gem */
     , (777700071,   5, 5) /* Encumbrance */
     , (777700071,  11, 1) /* MaxStackSize */
     , (777700071,  12, 1) /* StackSize */
     , (777700071,  16, 8) /* ItemUseable */
     , (777700071,  33, 1) /* Bonded */
     , (777700071,  91, 1) /* MaxStructure */
     , (777700071,  92, 1) /* Structure */
     , (777700071,  93, 1044) /* PhysicsState */
     , (777700071,  94, 16) /* TargetType */
     , (777700071, 114, 1) /* Attuned */
     , (777700071,  19, 1) /* Value = price in Prestige Coins (PLACEHOLDER, 1 a level) */
     , (777700072,   1, 2048)
     , (777700072,   5, 5)
     , (777700072,  11, 1)
     , (777700072,  12, 1)
     , (777700072,  16, 8)
     , (777700072,  33, 1)
     , (777700072,  91, 1)
     , (777700072,  92, 1)
     , (777700072,  93, 1044)
     , (777700072,  94, 16)
     , (777700072, 114, 1)
     , (777700072,  19, 5)
     , (777700073,   1, 2048)
     , (777700073,   5, 5)
     , (777700073,  11, 1)
     , (777700073,  12, 1)
     , (777700073,  16, 8)
     , (777700073,  33, 1)
     , (777700073,  91, 1)
     , (777700073,  92, 1)
     , (777700073,  93, 1044)
     , (777700073,  94, 16)
     , (777700073, 114, 1)
     , (777700073,  19, 10)
     , (777700074,   1, 2048)
     , (777700074,   5, 5)
     , (777700074,  11, 1)
     , (777700074,  12, 1)
     , (777700074,  16, 8)
     , (777700074,  33, 1)
     , (777700074,  91, 1)
     , (777700074,  92, 1)
     , (777700074,  93, 1044)
     , (777700074,  94, 16)
     , (777700074, 114, 1)
     , (777700074,  19, 1)
     , (777700075,   1, 2048)
     , (777700075,   5, 5)
     , (777700075,  11, 1)
     , (777700075,  12, 1)
     , (777700075,  16, 8)
     , (777700075,  33, 1)
     , (777700075,  91, 1)
     , (777700075,  92, 1)
     , (777700075,  93, 1044)
     , (777700075,  94, 16)
     , (777700075, 114, 1)
     , (777700075,  19, 5)
     , (777700076,   1, 2048)
     , (777700076,   5, 5)
     , (777700076,  11, 1)
     , (777700076,  12, 1)
     , (777700076,  16, 8)
     , (777700076,  33, 1)
     , (777700076,  91, 1)
     , (777700076,  92, 1)
     , (777700076,  93, 1044)
     , (777700076,  94, 16)
     , (777700076, 114, 1)
     , (777700076,  19, 10)
     , (777700077,   1, 2048)
     , (777700077,   5, 5)
     , (777700077,  11, 1)
     , (777700077,  12, 1)
     , (777700077,  16, 8)
     , (777700077,  33, 1)
     , (777700077,  91, 1)
     , (777700077,  92, 1)
     , (777700077,  93, 1044)
     , (777700077,  94, 16)
     , (777700077, 114, 1)
     , (777700077,  19, 1)
     , (777700078,   1, 2048)
     , (777700078,   5, 5)
     , (777700078,  11, 1)
     , (777700078,  12, 1)
     , (777700078,  16, 8)
     , (777700078,  33, 1)
     , (777700078,  91, 1)
     , (777700078,  92, 1)
     , (777700078,  93, 1044)
     , (777700078,  94, 16)
     , (777700078, 114, 1)
     , (777700078,  19, 5)
     , (777700079,   1, 2048)
     , (777700079,   5, 5)
     , (777700079,  11, 1)
     , (777700079,  12, 1)
     , (777700079,  16, 8)
     , (777700079,  33, 1)
     , (777700079,  91, 1)
     , (777700079,  92, 1)
     , (777700079,  93, 1044)
     , (777700079,  94, 16)
     , (777700079, 114, 1)
     , (777700079,  19, 10)
     , (777700080,   1, 2048)
     , (777700080,   5, 5)
     , (777700080,  11, 1)
     , (777700080,  12, 1)
     , (777700080,  16, 8)
     , (777700080,  33, 1)
     , (777700080,  91, 1)
     , (777700080,  92, 1)
     , (777700080,  93, 1044)
     , (777700080,  94, 16)
     , (777700080, 114, 1)
     , (777700080,  19, 1)
     , (777700081,   1, 2048)
     , (777700081,   5, 5)
     , (777700081,  11, 1)
     , (777700081,  12, 1)
     , (777700081,  16, 8)
     , (777700081,  33, 1)
     , (777700081,  91, 1)
     , (777700081,  92, 1)
     , (777700081,  93, 1044)
     , (777700081,  94, 16)
     , (777700081, 114, 1)
     , (777700081,  19, 5)
     , (777700082,   1, 2048)
     , (777700082,   5, 5)
     , (777700082,  11, 1)
     , (777700082,  12, 1)
     , (777700082,  16, 8)
     , (777700082,  33, 1)
     , (777700082,  91, 1)
     , (777700082,  92, 1)
     , (777700082,  93, 1044)
     , (777700082,  94, 16)
     , (777700082, 114, 1)
     , (777700082,  19, 10);

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)  /* setup, sound, palette, icon, physics effects = the growth gems' */
VALUES (777700071,  1, 33556769)
     , (777700071,  3, 536870932)
     , (777700071,  6, 67111919)
     , (777700071,  8, 100668362)
     , (777700071, 22, 872415275)
     , (777700072,  1, 33556769)
     , (777700072,  3, 536870932)
     , (777700072,  6, 67111919)
     , (777700072,  8, 100668362)
     , (777700072, 22, 872415275)
     , (777700073,  1, 33556769)
     , (777700073,  3, 536870932)
     , (777700073,  6, 67111919)
     , (777700073,  8, 100668362)
     , (777700073, 22, 872415275)
     , (777700074,  1, 33556769)
     , (777700074,  3, 536870932)
     , (777700074,  6, 67111919)
     , (777700074,  8, 100667482)
     , (777700074, 22, 872415275)
     , (777700075,  1, 33556769)
     , (777700075,  3, 536870932)
     , (777700075,  6, 67111919)
     , (777700075,  8, 100667482)
     , (777700075, 22, 872415275)
     , (777700076,  1, 33556769)
     , (777700076,  3, 536870932)
     , (777700076,  6, 67111919)
     , (777700076,  8, 100667482)
     , (777700076, 22, 872415275)
     , (777700077,  1, 33556769)
     , (777700077,  3, 536870932)
     , (777700077,  6, 67111919)
     , (777700077,  8, 100668359)
     , (777700077, 22, 872415275)
     , (777700078,  1, 33556769)
     , (777700078,  3, 536870932)
     , (777700078,  6, 67111919)
     , (777700078,  8, 100668359)
     , (777700078, 22, 872415275)
     , (777700079,  1, 33556769)
     , (777700079,  3, 536870932)
     , (777700079,  6, 67111919)
     , (777700079,  8, 100668359)
     , (777700079, 22, 872415275)
     , (777700080,  1, 33556769)
     , (777700080,  3, 536870932)
     , (777700080,  6, 67111919)
     , (777700080,  8, 100668360)
     , (777700080, 22, 872415275)
     , (777700081,  1, 33556769)
     , (777700081,  3, 536870932)
     , (777700081,  6, 67111919)
     , (777700081,  8, 100668360)
     , (777700081, 22, 872415275)
     , (777700082,  1, 33556769)
     , (777700082,  3, 536870932)
     , (777700082,  6, 67111919)
     , (777700082,  8, 100668360)
     , (777700082, 22, 872415275);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700071,  1, 'Chipped Split Arrow Upgrade Gem (+1)')
     , (777700071, 14, 'Double click to raise your Split Arrow charm by 1 level, up to level 100. The charm must be in your pack. Each level also costs Prestige Coins (pack or bank), more as the charm climbs; you are asked before any are spent.')
     , (777700071, 15, 'A gem that deepens the power of a Split Arrow charm.')
     , (777700072,  1, 'Flawed Split Arrow Upgrade Gem (+5)')
     , (777700072, 14, 'Double click to raise your Split Arrow charm by 5 levels, up to level 100. The charm must be in your pack. Each level also costs Prestige Coins (pack or bank), more as the charm climbs; you are asked before any are spent.')
     , (777700072, 15, 'A gem that deepens the power of a Split Arrow charm.')
     , (777700073,  1, 'Flawless Split Arrow Upgrade Gem (+10)')
     , (777700073, 14, 'Double click to raise your Split Arrow charm by 10 levels, up to level 100. The charm must be in your pack. Each level also costs Prestige Coins (pack or bank), more as the charm climbs; you are asked before any are spent.')
     , (777700073, 15, 'A gem that deepens the power of a Split Arrow charm.')
     , (777700074,  1, 'Chipped Chain Upgrade Gem (+1)')
     , (777700074, 14, 'Double click to raise your Chain charm by 1 level, up to level 100. The charm must be in your pack. Each level also costs Prestige Coins (pack or bank), more as the charm climbs; you are asked before any are spent.')
     , (777700074, 15, 'A gem that deepens the power of a Chain charm.')
     , (777700075,  1, 'Flawed Chain Upgrade Gem (+5)')
     , (777700075, 14, 'Double click to raise your Chain charm by 5 levels, up to level 100. The charm must be in your pack. Each level also costs Prestige Coins (pack or bank), more as the charm climbs; you are asked before any are spent.')
     , (777700075, 15, 'A gem that deepens the power of a Chain charm.')
     , (777700076,  1, 'Flawless Chain Upgrade Gem (+10)')
     , (777700076, 14, 'Double click to raise your Chain charm by 10 levels, up to level 100. The charm must be in your pack. Each level also costs Prestige Coins (pack or bank), more as the charm climbs; you are asked before any are spent.')
     , (777700076, 15, 'A gem that deepens the power of a Chain charm.')
     , (777700077,  1, 'Chipped Pandemic Upgrade Gem (+1)')
     , (777700077, 14, 'Double click to raise your Pandemic charm by 1 level, up to level 100. The charm must be in your pack. Each level also costs Prestige Coins (pack or bank), more as the charm climbs; you are asked before any are spent.')
     , (777700077, 15, 'A gem that deepens the power of a Pandemic charm.')
     , (777700078,  1, 'Flawed Pandemic Upgrade Gem (+5)')
     , (777700078, 14, 'Double click to raise your Pandemic charm by 5 levels, up to level 100. The charm must be in your pack. Each level also costs Prestige Coins (pack or bank), more as the charm climbs; you are asked before any are spent.')
     , (777700078, 15, 'A gem that deepens the power of a Pandemic charm.')
     , (777700079,  1, 'Flawless Pandemic Upgrade Gem (+10)')
     , (777700079, 14, 'Double click to raise your Pandemic charm by 10 levels, up to level 100. The charm must be in your pack. Each level also costs Prestige Coins (pack or bank), more as the charm climbs; you are asked before any are spent.')
     , (777700079, 15, 'A gem that deepens the power of a Pandemic charm.')
     , (777700080,  1, 'Chipped Improved Cleave Upgrade Gem (+1)')
     , (777700080, 14, 'Double click to raise your Improved Cleave charm by 1 level, up to level 100. The charm must be in your pack. Each level also costs Prestige Coins (pack or bank), more as the charm climbs; you are asked before any are spent.')
     , (777700080, 15, 'A gem that deepens the power of a Improved Cleave charm.')
     , (777700081,  1, 'Flawed Improved Cleave Upgrade Gem (+5)')
     , (777700081, 14, 'Double click to raise your Improved Cleave charm by 5 levels, up to level 100. The charm must be in your pack. Each level also costs Prestige Coins (pack or bank), more as the charm climbs; you are asked before any are spent.')
     , (777700081, 15, 'A gem that deepens the power of a Improved Cleave charm.')
     , (777700082,  1, 'Flawless Improved Cleave Upgrade Gem (+10)')
     , (777700082, 14, 'Double click to raise your Improved Cleave charm by 10 levels, up to level 100. The charm must be in your pack. Each level also costs Prestige Coins (pack or bank), more as the charm climbs; you are asked before any are spent.')
     , (777700082, 15, 'A gem that deepens the power of a Improved Cleave charm.');

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (777700071, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `message`)
VALUES (@parent_id, 0, 123 /* PromptAddAugment */, 0, 1, 'CharmLevel_splitarrow_1');

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (777700072, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `message`)
VALUES (@parent_id, 0, 123 /* PromptAddAugment */, 0, 1, 'CharmLevel_splitarrow_5');

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (777700073, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `message`)
VALUES (@parent_id, 0, 123 /* PromptAddAugment */, 0, 1, 'CharmLevel_splitarrow_10');

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (777700074, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `message`)
VALUES (@parent_id, 0, 123 /* PromptAddAugment */, 0, 1, 'CharmLevel_chain_1');

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (777700075, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `message`)
VALUES (@parent_id, 0, 123 /* PromptAddAugment */, 0, 1, 'CharmLevel_chain_5');

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (777700076, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `message`)
VALUES (@parent_id, 0, 123 /* PromptAddAugment */, 0, 1, 'CharmLevel_chain_10');

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (777700077, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `message`)
VALUES (@parent_id, 0, 123 /* PromptAddAugment */, 0, 1, 'CharmLevel_pandemic_1');

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (777700078, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `message`)
VALUES (@parent_id, 0, 123 /* PromptAddAugment */, 0, 1, 'CharmLevel_pandemic_5');

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (777700079, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `message`)
VALUES (@parent_id, 0, 123 /* PromptAddAugment */, 0, 1, 'CharmLevel_pandemic_10');

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (777700080, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `message`)
VALUES (@parent_id, 0, 123 /* PromptAddAugment */, 0, 1, 'CharmLevel_improvedcleave_1');

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (777700081, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `message`)
VALUES (@parent_id, 0, 123 /* PromptAddAugment */, 0, 1, 'CharmLevel_improvedcleave_5');

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (777700082, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `message`)
VALUES (@parent_id, 0, 123 /* PromptAddAugment */, 0, 1, 'CharmLevel_improvedcleave_10');

-- Enephior's stock: the four charms (Value 1 = 1 coin, PLACEHOLDER) and the twelve gems.
DELETE FROM `weenie_properties_create_list` WHERE `object_Id` = 19860002 AND `weenie_Class_Id` IN (777700035, 777700036, 777700037, 777700038, 777700071, 777700072, 777700073, 777700074, 777700075, 777700076, 777700077, 777700078, 777700079, 777700080, 777700081, 777700082);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`)
VALUES (19860002, 4, 777700035, -1, 0, 0, 0)
     , (19860002, 4, 777700036, -1, 0, 0, 0)
     , (19860002, 4, 777700037, -1, 0, 0, 0)
     , (19860002, 4, 777700038, -1, 0, 0, 0)
     , (19860002, 4, 777700071, -1, 0, 0, 0)
     , (19860002, 4, 777700072, -1, 0, 0, 0)
     , (19860002, 4, 777700073, -1, 0, 0, 0)
     , (19860002, 4, 777700074, -1, 0, 0, 0)
     , (19860002, 4, 777700075, -1, 0, 0, 0)
     , (19860002, 4, 777700076, -1, 0, 0, 0)
     , (19860002, 4, 777700077, -1, 0, 0, 0)
     , (19860002, 4, 777700078, -1, 0, 0, 0)
     , (19860002, 4, 777700079, -1, 0, 0, 0)
     , (19860002, 4, 777700080, -1, 0, 0, 0)
     , (19860002, 4, 777700081, -1, 0, 0, 0)
     , (19860002, 4, 777700082, -1, 0, 0, 0);
