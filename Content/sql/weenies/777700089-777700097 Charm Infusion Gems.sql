-- CHARM INFUSION GEMS (owner 2026-10-24): put Prestige Coins into a levelled charm (coin ledger, Player_CharmLevels.cs).
-- One gem type for every levelled charm, nine sizes. Enephior (19860002, Prestige Palace, AlternateCurrency = Prestige
-- Coin, SellPrice 1.0) sells each for exactly the coins it holds: Value = StackUnitValue = PropertyInt 51210
-- (CharmInfusionCoins). Use one ON a levelled charm in your pack: the server asks first, refuses a gem that would take
-- the charm past level 100, checks again on Yes, uses up one gem and adds its coins. The charm's level comes from the
-- coins put in and its live price curve (/charm <name> coinbase / coingrowth, /charm curve <name>).
-- Not sellable (IsSellable 0), Bonded + Attuned: they cannot be sold back, traded or dropped, so they never become a
-- second currency. Stacks of 100. One icon for every size (PLACEHOLDER - the size is in the name).
--
-- Replaces the +1 / +5 / +10 charm upgrade gems (777700071-777700088, removed below with their Enephior rows); the
-- six levelled charms stay on Enephior (they were listed by the removed files). Safe to run again.

-- the old level gems and their shop rows
DELETE FROM `weenie_properties_create_list` WHERE `object_Id` = 19860002 AND `weenie_Class_Id` IN (777700071, 777700072, 777700073, 777700074, 777700075, 777700076, 777700077, 777700078, 777700079, 777700080, 777700081, 777700082, 777700083, 777700084, 777700085, 777700086, 777700087, 777700088);
DELETE FROM `weenie_properties_emote` WHERE `object_Id` IN (777700071, 777700072, 777700073, 777700074, 777700075, 777700076, 777700077, 777700078, 777700079, 777700080, 777700081, 777700082, 777700083, 777700084, 777700085, 777700086, 777700087, 777700088);
DELETE FROM `weenie_properties_bool` WHERE `object_Id` IN (777700071, 777700072, 777700073, 777700074, 777700075, 777700076, 777700077, 777700078, 777700079, 777700080, 777700081, 777700082, 777700083, 777700084, 777700085, 777700086, 777700087, 777700088);
DELETE FROM `weenie_properties_int` WHERE `object_Id` IN (777700071, 777700072, 777700073, 777700074, 777700075, 777700076, 777700077, 777700078, 777700079, 777700080, 777700081, 777700082, 777700083, 777700084, 777700085, 777700086, 777700087, 777700088);
DELETE FROM `weenie_properties_d_i_d` WHERE `object_Id` IN (777700071, 777700072, 777700073, 777700074, 777700075, 777700076, 777700077, 777700078, 777700079, 777700080, 777700081, 777700082, 777700083, 777700084, 777700085, 777700086, 777700087, 777700088);
DELETE FROM `weenie_properties_string` WHERE `object_Id` IN (777700071, 777700072, 777700073, 777700074, 777700075, 777700076, 777700077, 777700078, 777700079, 777700080, 777700081, 777700082, 777700083, 777700084, 777700085, 777700086, 777700087, 777700088);
DELETE FROM `weenie_properties_float` WHERE `object_Id` IN (777700071, 777700072, 777700073, 777700074, 777700075, 777700076, 777700077, 777700078, 777700079, 777700080, 777700081, 777700082, 777700083, 777700084, 777700085, 777700086, 777700087, 777700088);
DELETE FROM `weenie_properties_int64` WHERE `object_Id` IN (777700071, 777700072, 777700073, 777700074, 777700075, 777700076, 777700077, 777700078, 777700079, 777700080, 777700081, 777700082, 777700083, 777700084, 777700085, 777700086, 777700087, 777700088);
DELETE FROM `weenie` WHERE `class_Id` IN (777700071, 777700072, 777700073, 777700074, 777700075, 777700076, 777700077, 777700078, 777700079, 777700080, 777700081, 777700082, 777700083, 777700084, 777700085, 777700086, 777700087, 777700088);

DELETE FROM `weenie_properties_bool` WHERE `object_Id` IN (777700089, 777700090, 777700091, 777700092, 777700093, 777700094, 777700095, 777700096, 777700097);
DELETE FROM `weenie_properties_int` WHERE `object_Id` IN (777700089, 777700090, 777700091, 777700092, 777700093, 777700094, 777700095, 777700096, 777700097);
DELETE FROM `weenie_properties_d_i_d` WHERE `object_Id` IN (777700089, 777700090, 777700091, 777700092, 777700093, 777700094, 777700095, 777700096, 777700097);
DELETE FROM `weenie_properties_string` WHERE `object_Id` IN (777700089, 777700090, 777700091, 777700092, 777700093, 777700094, 777700095, 777700096, 777700097);
DELETE FROM `weenie_properties_float` WHERE `object_Id` IN (777700089, 777700090, 777700091, 777700092, 777700093, 777700094, 777700095, 777700096, 777700097);
DELETE FROM `weenie_properties_int64` WHERE `object_Id` IN (777700089, 777700090, 777700091, 777700092, 777700093, 777700094, 777700095, 777700096, 777700097);
DELETE FROM `weenie` WHERE `class_Id` IN (777700089, 777700090, 777700091, 777700092, 777700093, 777700094, 777700095, 777700096, 777700097);

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700089, 'ilt_charm_infusion_gem_1', 38, NOW())
     , (777700090, 'ilt_charm_infusion_gem_5', 38, NOW())
     , (777700091, 'ilt_charm_infusion_gem_10', 38, NOW())
     , (777700092, 'ilt_charm_infusion_gem_25', 38, NOW())
     , (777700093, 'ilt_charm_infusion_gem_50', 38, NOW())
     , (777700094, 'ilt_charm_infusion_gem_100', 38, NOW())
     , (777700095, 'ilt_charm_infusion_gem_500', 38, NOW())
     , (777700096, 'ilt_charm_infusion_gem_1000', 38, NOW())
     , (777700097, 'ilt_charm_infusion_gem_5000', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)  /* 69 IsSellable = false */
VALUES (777700089, 69, 0)
     , (777700090, 69, 0)
     , (777700091, 69, 0)
     , (777700092, 69, 0)
     , (777700093, 69, 0)
     , (777700094, 69, 0)
     , (777700095, 69, 0)
     , (777700096, 69, 0)
     , (777700097, 69, 0);

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700089,     1, 2048) /* ItemType - Gem */
     , (777700089,     5,    5) /* EncumbranceVal */
     , (777700089,    11,  100) /* MaxStackSize */
     , (777700089,    12,    1) /* StackSize */
     , (777700089,    13,    5) /* StackUnitEncumbrance */
     , (777700089,    15,    1) /* StackUnitValue - coins it holds */
     , (777700089,    16, 524296) /* ItemUseable - SourceContainedTargetContained: Use On cursor */
     , (777700089,    19,    1) /* Value - Enephior's price in Prestige Coins */
     , (777700089,    33,    1) /* Bonded */
     , (777700089,    93, 1044) /* PhysicsState */
     , (777700089,    94, 2048) /* TargetType - Gem (the charms are gems) */
     , (777700089,   114,    1) /* Attuned */
     , (777700089, 51210,    1) /* CharmInfusionCoins - coins one gem puts into a charm */
     , (777700090,     1, 2048)
     , (777700090,     5,    5)
     , (777700090,    11,  100)
     , (777700090,    12,    1)
     , (777700090,    13,    5)
     , (777700090,    15,    5)
     , (777700090,    16, 524296)
     , (777700090,    19,    5)
     , (777700090,    33,    1)
     , (777700090,    93, 1044)
     , (777700090,    94, 2048)
     , (777700090,   114,    1)
     , (777700090, 51210,    5)
     , (777700091,     1, 2048)
     , (777700091,     5,    5)
     , (777700091,    11,  100)
     , (777700091,    12,    1)
     , (777700091,    13,    5)
     , (777700091,    15,   10)
     , (777700091,    16, 524296)
     , (777700091,    19,   10)
     , (777700091,    33,    1)
     , (777700091,    93, 1044)
     , (777700091,    94, 2048)
     , (777700091,   114,    1)
     , (777700091, 51210,   10)
     , (777700092,     1, 2048)
     , (777700092,     5,    5)
     , (777700092,    11,  100)
     , (777700092,    12,    1)
     , (777700092,    13,    5)
     , (777700092,    15,   25)
     , (777700092,    16, 524296)
     , (777700092,    19,   25)
     , (777700092,    33,    1)
     , (777700092,    93, 1044)
     , (777700092,    94, 2048)
     , (777700092,   114,    1)
     , (777700092, 51210,   25)
     , (777700093,     1, 2048)
     , (777700093,     5,    5)
     , (777700093,    11,  100)
     , (777700093,    12,    1)
     , (777700093,    13,    5)
     , (777700093,    15,   50)
     , (777700093,    16, 524296)
     , (777700093,    19,   50)
     , (777700093,    33,    1)
     , (777700093,    93, 1044)
     , (777700093,    94, 2048)
     , (777700093,   114,    1)
     , (777700093, 51210,   50)
     , (777700094,     1, 2048)
     , (777700094,     5,    5)
     , (777700094,    11,  100)
     , (777700094,    12,    1)
     , (777700094,    13,    5)
     , (777700094,    15,  100)
     , (777700094,    16, 524296)
     , (777700094,    19,  100)
     , (777700094,    33,    1)
     , (777700094,    93, 1044)
     , (777700094,    94, 2048)
     , (777700094,   114,    1)
     , (777700094, 51210,  100)
     , (777700095,     1, 2048)
     , (777700095,     5,    5)
     , (777700095,    11,  100)
     , (777700095,    12,    1)
     , (777700095,    13,    5)
     , (777700095,    15,  500)
     , (777700095,    16, 524296)
     , (777700095,    19,  500)
     , (777700095,    33,    1)
     , (777700095,    93, 1044)
     , (777700095,    94, 2048)
     , (777700095,   114,    1)
     , (777700095, 51210,  500)
     , (777700096,     1, 2048)
     , (777700096,     5,    5)
     , (777700096,    11,  100)
     , (777700096,    12,    1)
     , (777700096,    13,    5)
     , (777700096,    15, 1000)
     , (777700096,    16, 524296)
     , (777700096,    19, 1000)
     , (777700096,    33,    1)
     , (777700096,    93, 1044)
     , (777700096,    94, 2048)
     , (777700096,   114,    1)
     , (777700096, 51210, 1000)
     , (777700097,     1, 2048)
     , (777700097,     5,    5)
     , (777700097,    11,  100)
     , (777700097,    12,    1)
     , (777700097,    13,    5)
     , (777700097,    15, 5000)
     , (777700097,    16, 524296)
     , (777700097,    19, 5000)
     , (777700097,    33,    1)
     , (777700097,    93, 1044)
     , (777700097,    94, 2048)
     , (777700097,   114,    1)
     , (777700097, 51210, 5000);

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)  /* setup, sound, palette, icon (PLACEHOLDER), physics effects = the old upgrade gems' */
VALUES (777700089,  1, 33556769)
     , (777700089,  3, 536870932)
     , (777700089,  6, 67111919)
     , (777700089,  8, 100668360)
     , (777700089, 22, 872415275)
     , (777700090,  1, 33556769)
     , (777700090,  3, 536870932)
     , (777700090,  6, 67111919)
     , (777700090,  8, 100668360)
     , (777700090, 22, 872415275)
     , (777700091,  1, 33556769)
     , (777700091,  3, 536870932)
     , (777700091,  6, 67111919)
     , (777700091,  8, 100668360)
     , (777700091, 22, 872415275)
     , (777700092,  1, 33556769)
     , (777700092,  3, 536870932)
     , (777700092,  6, 67111919)
     , (777700092,  8, 100668360)
     , (777700092, 22, 872415275)
     , (777700093,  1, 33556769)
     , (777700093,  3, 536870932)
     , (777700093,  6, 67111919)
     , (777700093,  8, 100668360)
     , (777700093, 22, 872415275)
     , (777700094,  1, 33556769)
     , (777700094,  3, 536870932)
     , (777700094,  6, 67111919)
     , (777700094,  8, 100668360)
     , (777700094, 22, 872415275)
     , (777700095,  1, 33556769)
     , (777700095,  3, 536870932)
     , (777700095,  6, 67111919)
     , (777700095,  8, 100668360)
     , (777700095, 22, 872415275)
     , (777700096,  1, 33556769)
     , (777700096,  3, 536870932)
     , (777700096,  6, 67111919)
     , (777700096,  8, 100668360)
     , (777700096, 22, 872415275)
     , (777700097,  1, 33556769)
     , (777700097,  3, 536870932)
     , (777700097,  6, 67111919)
     , (777700097,  8, 100668360)
     , (777700097, 22, 872415275);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700089,  1, 'Charm Infusion Gem (1)')
     , (777700089, 14, 'Use this gem on a levelled charm in your pack (Split Arrow, Chain, Pandemic, Improved Cleave, Armor Breaker or Shield Arc) to put 1 Prestige Coin into it. You are asked first. A charm cannot take a gem that would carry it past level 100.')
     , (777700089, 15, 'A gem that holds 1 Prestige Coin for a levelled charm.')
     , (777700089, 20, 'Charm Infusion Gems (1)')
     , (777700090,  1, 'Charm Infusion Gem (5)')
     , (777700090, 14, 'Use this gem on a levelled charm in your pack (Split Arrow, Chain, Pandemic, Improved Cleave, Armor Breaker or Shield Arc) to put 5 Prestige Coins into it. You are asked first. A charm cannot take a gem that would carry it past level 100.')
     , (777700090, 15, 'A gem that holds 5 Prestige Coins for a levelled charm.')
     , (777700090, 20, 'Charm Infusion Gems (5)')
     , (777700091,  1, 'Charm Infusion Gem (10)')
     , (777700091, 14, 'Use this gem on a levelled charm in your pack (Split Arrow, Chain, Pandemic, Improved Cleave, Armor Breaker or Shield Arc) to put 10 Prestige Coins into it. You are asked first. A charm cannot take a gem that would carry it past level 100.')
     , (777700091, 15, 'A gem that holds 10 Prestige Coins for a levelled charm.')
     , (777700091, 20, 'Charm Infusion Gems (10)')
     , (777700092,  1, 'Charm Infusion Gem (25)')
     , (777700092, 14, 'Use this gem on a levelled charm in your pack (Split Arrow, Chain, Pandemic, Improved Cleave, Armor Breaker or Shield Arc) to put 25 Prestige Coins into it. You are asked first. A charm cannot take a gem that would carry it past level 100.')
     , (777700092, 15, 'A gem that holds 25 Prestige Coins for a levelled charm.')
     , (777700092, 20, 'Charm Infusion Gems (25)')
     , (777700093,  1, 'Charm Infusion Gem (50)')
     , (777700093, 14, 'Use this gem on a levelled charm in your pack (Split Arrow, Chain, Pandemic, Improved Cleave, Armor Breaker or Shield Arc) to put 50 Prestige Coins into it. You are asked first. A charm cannot take a gem that would carry it past level 100.')
     , (777700093, 15, 'A gem that holds 50 Prestige Coins for a levelled charm.')
     , (777700093, 20, 'Charm Infusion Gems (50)')
     , (777700094,  1, 'Charm Infusion Gem (100)')
     , (777700094, 14, 'Use this gem on a levelled charm in your pack (Split Arrow, Chain, Pandemic, Improved Cleave, Armor Breaker or Shield Arc) to put 100 Prestige Coins into it. You are asked first. A charm cannot take a gem that would carry it past level 100.')
     , (777700094, 15, 'A gem that holds 100 Prestige Coins for a levelled charm.')
     , (777700094, 20, 'Charm Infusion Gems (100)')
     , (777700095,  1, 'Charm Infusion Gem (500)')
     , (777700095, 14, 'Use this gem on a levelled charm in your pack (Split Arrow, Chain, Pandemic, Improved Cleave, Armor Breaker or Shield Arc) to put 500 Prestige Coins into it. You are asked first. A charm cannot take a gem that would carry it past level 100.')
     , (777700095, 15, 'A gem that holds 500 Prestige Coins for a levelled charm.')
     , (777700095, 20, 'Charm Infusion Gems (500)')
     , (777700096,  1, 'Charm Infusion Gem (1,000)')
     , (777700096, 14, 'Use this gem on a levelled charm in your pack (Split Arrow, Chain, Pandemic, Improved Cleave, Armor Breaker or Shield Arc) to put 1,000 Prestige Coins into it. You are asked first. A charm cannot take a gem that would carry it past level 100.')
     , (777700096, 15, 'A gem that holds 1,000 Prestige Coins for a levelled charm.')
     , (777700096, 20, 'Charm Infusion Gems (1,000)')
     , (777700097,  1, 'Charm Infusion Gem (5,000)')
     , (777700097, 14, 'Use this gem on a levelled charm in your pack (Split Arrow, Chain, Pandemic, Improved Cleave, Armor Breaker or Shield Arc) to put 5,000 Prestige Coins into it. You are asked first. A charm cannot take a gem that would carry it past level 100.')
     , (777700097, 15, 'A gem that holds 5,000 Prestige Coins for a levelled charm.')
     , (777700097, 20, 'Charm Infusion Gems (5,000)');

-- Enephior's stock: the six levelled charms (Value 1 = 1 coin, PLACEHOLDER) and the nine gems.
DELETE FROM `weenie_properties_create_list` WHERE `object_Id` = 19860002 AND `weenie_Class_Id` IN (777700035, 777700036, 777700037, 777700038, 777700039, 777700040, 777700089, 777700090, 777700091, 777700092, 777700093, 777700094, 777700095, 777700096, 777700097);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`)
VALUES (19860002, 4, 777700035, -1, 0, 0, 0)
     , (19860002, 4, 777700036, -1, 0, 0, 0)
     , (19860002, 4, 777700037, -1, 0, 0, 0)
     , (19860002, 4, 777700038, -1, 0, 0, 0)
     , (19860002, 4, 777700039, -1, 0, 0, 0)
     , (19860002, 4, 777700040, -1, 0, 0, 0)
     , (19860002, 4, 777700089, -1, 0, 0, 0)
     , (19860002, 4, 777700090, -1, 0, 0, 0)
     , (19860002, 4, 777700091, -1, 0, 0, 0)
     , (19860002, 4, 777700092, -1, 0, 0, 0)
     , (19860002, 4, 777700093, -1, 0, 0, 0)
     , (19860002, 4, 777700094, -1, 0, 0, 0)
     , (19860002, 4, 777700095, -1, 0, 0, 0)
     , (19860002, 4, 777700096, -1, 0, 0, 0)
     , (19860002, 4, 777700097, -1, 0, 0, 0);
