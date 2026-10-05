-- Blacksmithing: everything the world database needs, in one script for MySQL Workbench.
-- It is the two update files joined, unchanged:
--   Database/Updates/World/2026-10-02-00-Forge-Smith-NPC.sql
--   Database/Updates/World/2026-10-03-00-Forge-Smithy.sql
-- Safe to run more than once. USE picks the schema so Workbench's last-selected schema cannot misdirect it.
-- If any statement fails: run ROLLBACK; in this same tab before anything else.
USE ace_world;
START TRANSACTION;

-- ------------------------------------------------------------------------------------
-- Aldric the Forgemaster (NPC) - wcid 78780300
-- Blacksmithing forge smith, first id of the blacksmithing block 78780300+ (docs/WCID_ALLOCATION_7878.md).
-- Body, clothes and stats copied from retail 712 holtburgblacksmith (Sedor Wystan the Blacksmith), with the
-- vendor parts removed: the type is Creature (10), not Vendor, and the shop list, prices and vendor emotes are gone.
--
-- PropertyBool 50058 ForgeSmith makes him a forge smith: a weapon handed to him is never taken. The first one
-- is noted as the player's main weapon, the second is offered as a forge (ForgeService.HandleGive). He does
-- nothing while ServerConfig forge_enabled is FALSE except say the forge is cold.
--
-- Not placed by this file: put him where the smithy should be (an ILT-Anvil, wcid 3110304, makes a good prop).
-- Every string here is plain ASCII: the client cannot draw anything else.
-- ------------------------------------------------------------------------------------
-- Narrow on purpose: removes only this weenie (id AND class_Name). If the id has been taken by
-- something else, that weenie is left alone and the INSERT below fails on the primary key instead
-- of silently replacing it. Child rows follow the parent (ON DELETE CASCADE).
DELETE FROM `weenie` WHERE `class_Id` = 78780300 AND `class_Name` = 'npc-forge-smith';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780300, 'npc-forge-smith', 10, '2026-10-02 12:00:00') /* Creature */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780300, 1, 16)
     , (78780300, 2, 31)
     , (78780300, 6, -1)
     , (78780300, 7, -1)
     , (78780300, 8, 120)
     , (78780300, 16, 32)
     , (78780300, 25, 7)
     , (78780300, 27, 0)
     , (78780300, 93, 2098200)
     , (78780300, 133, 4)
     , (78780300, 134, 16)
     , (78780300, 146, 133);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780300, 1, True)
     , (78780300, 12, True)
     , (78780300, 13, False)
     , (78780300, 19, False)
     , (78780300, 41, True)
     , (78780300, 50058, True);

INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`)
VALUES (78780300, 1, 5)
     , (78780300, 2, 0)
     , (78780300, 3, 0.16)
     , (78780300, 4, 5)
     , (78780300, 5, 1)
     , (78780300, 11, 300)
     , (78780300, 13, 0.9)
     , (78780300, 14, 1)
     , (78780300, 15, 1.1)
     , (78780300, 16, 0.4)
     , (78780300, 17, 0.4)
     , (78780300, 18, 1)
     , (78780300, 19, 0.6)
     , (78780300, 54, 6)
     , (78780300, 64, 1)
     , (78780300, 65, 1)
     , (78780300, 66, 1)
     , (78780300, 67, 1)
     , (78780300, 68, 1)
     , (78780300, 69, 1)
     , (78780300, 70, 1)
     , (78780300, 71, 1)
     , (78780300, 72, 1)
     , (78780300, 73, 1)
     , (78780300, 74, 1)
     , (78780300, 75, 1)
     , (78780300, 104, 10)
     , (78780300, 125, 1);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780300, 1, 'Aldric the Forgemaster')
     , (78780300, 3, 'Male')
     , (78780300, 4, 'Aluvian')
     , (78780300, 5, 'Forgemaster');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780300, 1, 0x02000001)
     , (78780300, 2, 0x09000001)
     , (78780300, 3, 0x20000001)
     , (78780300, 4, 0x30000000)
     , (78780300, 8, 0x06001036);

INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`)
VALUES (78780300, 1, 80, 0, 0)
     , (78780300, 2, 70, 0, 0)
     , (78780300, 3, 50, 0, 0)
     , (78780300, 4, 70, 0, 0)
     , (78780300, 5, 30, 0, 0)
     , (78780300, 6, 30, 0, 0);

INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`)
VALUES (78780300, 1, 60, 0, 0, 95)
     , (78780300, 3, 75, 0, 0, 145)
     , (78780300, 5, 40, 0, 0, 70);

INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`)
VALUES (78780300, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0.33, 0, 0, 0.33, 0, 0, 0.33, 0, 0, 0.33, 0, 0)
     , (78780300, 1, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0.44, 0.17, 0, 0.44, 0.17, 0, 0.44, 0.17, 0, 0.44, 0.17, 0)
     , (78780300, 2, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0.17, 0, 0, 0.17, 0, 0, 0.17, 0, 0, 0.17, 0)
     , (78780300, 3, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0.23, 0.03, 0, 0.23, 0.03, 0, 0.23, 0.03, 0, 0.23, 0.03, 0)
     , (78780300, 4, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0.3, 0, 0, 0.3, 0, 0, 0.3, 0, 0, 0.3, 0)
     , (78780300, 5, 4, 2, 0.75, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0.2, 0, 0, 0.2, 0, 0, 0.2, 0, 0, 0.2, 0)
     , (78780300, 6, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0.13, 0.18, 0, 0.13, 0.18, 0, 0.13, 0.18, 0, 0.13, 0.18)
     , (78780300, 7, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0.6, 0, 0, 0.6, 0, 0, 0.6, 0, 0, 0.6)
     , (78780300, 8, 4, 2, 0.75, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0.22, 0, 0, 0.22, 0, 0, 0.22, 0, 0, 0.22);

INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`)
VALUES (78780300, 2, 303, 0, 0, 0, False)
     , (78780300, 2, 124, 0, 8, 0.67, False)
     , (78780300, 2, 117, 0, 8, 0.67, False)
     , (78780300, 2, 132, 0, 7, 0.33, False)
     , (78780300, 2, 10696, 0, 4, 0.5, False);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780300, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 10 /* Tell */, 0, 1, NULL, 'Hand me the weapon or the piece of armour you mean to keep. It holds its shape, its tinkering and its colour.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
     , (@parent_id, 1, 10 /* Tell */, 3, 1, NULL, 'Then hand me a second of its kind: sword to sword, helm to helm. The new piece draws every quality from the pair, and the second is melted down for good.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
     , (@parent_id, 2, 10 /* Tell */, 3, 1, NULL, 'What leaves my forge is bound to you. I will name my price before I strike.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

-- ------------------------------------------------------------------------------------
-- Blacksmithing: the rest of the smithy - wcids 78780301-78780329 (docs/WCID_ALLOCATION_7878.md).
--   78780301  Grindstone        station: hone stones only work near one   (PropertyBool 50059 ForgeGrindstone)
--   78780302  Dye Vat           station: dyes only work near one          (PropertyBool 50060 ForgeDyeVat)
--   78780303  smith's apprentice, a vendor selling every tool below
--   78780310-78780317  hone stones, one per stat (ForgeTool 1, ForgeToolArg = ForgeMath.ForgeLine 1-8)
--   78780318  Smith's Flux      (ForgeTool 3; one is used per hone attempt, +forge_flux_bonus)
--   78780319  Unbinding Oil     (ForgeTool 4)
--   78780320-78780329  dyes: any colour, then one per colour family (ForgeTool 2, ForgeToolArg = ForgeDyes.Family 0-9)
--
-- Tools are reusable instruments (never consumed, except flux): the pyreals are charged by the action itself.
-- All of them are IsSellable FALSE so no vendor buys them back. PropertyInt 50116 ForgeTool / 50117 ForgeToolArg.
-- Nothing is placed by this file. Bodies and art are copied from retail weenies named on each block.
-- Every string is plain ASCII: the client cannot draw anything else.
-- ------------------------------------------------------------------------------------

-- 78780301 forge-grindstone: Grindstone (copied from retail 3110304)
DELETE FROM `weenie` WHERE `class_Id` = 78780301 AND `class_Name` = 'forge-grindstone';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780301, 'forge-grindstone', 1, '2026-10-03 12:00:00') /* Generic */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780301, 1, 1024)
     , (78780301, 5, 500)
     , (78780301, 8, 500)
     , (78780301, 16, 1)
     , (78780301, 19, 6000)
     , (78780301, 93, 24)
     , (78780301, 150, 103)
     , (78780301, 151, 24);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780301, 1, True)
     , (78780301, 22, True)
     , (78780301, 24, True)
     , (78780301, 52, True)
     , (78780301, 50059, True);

INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`)
VALUES (78780301, 39, 1);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780301, 1, 'Grindstone')
     , (78780301, 16, 'A smith''s grindstone. Stand here and use a hone stone on your weapon to hone it.');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780301, 1, 0x020000A7)
     , (78780301, 3, 0x20000014)
     , (78780301, 8, 0x06002FEB)
     , (78780301, 22, 0x3400002B);

-- 78780302 forge-dye-vat: Dye Vat (copied from retail 4383)
DELETE FROM `weenie` WHERE `class_Id` = 78780302 AND `class_Name` = 'forge-dye-vat';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780302, 'forge-dye-vat', 1, '2026-10-03 12:00:00') /* Generic */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780302, 1, 128)
     , (78780302, 5, 50)
     , (78780302, 8, 50)
     , (78780302, 9, 0)
     , (78780302, 16, 1)
     , (78780302, 19, 0)
     , (78780302, 93, 1040);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780302, 1, True)
     , (78780302, 13, False)
     , (78780302, 24, True)
     , (78780302, 50060, True);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780302, 1, 'Dye Vat')
     , (78780302, 16, 'A vat of smith''s dyes. Stand here and use a dye on your weapon to try a colour on it.');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780302, 1, 0x0200043C)
     , (78780302, 8, 0x06001066);

-- 78780310 forge-hone-stone-damage: Damage Hone Stone (copied from retail 6318)
DELETE FROM `weenie` WHERE `class_Id` = 78780310 AND `class_Name` = 'forge-hone-stone-damage';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780310, 'forge-hone-stone-damage', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780310, 1, 2048)
     , (78780310, 3, 82)
     , (78780310, 5, 5)
     , (78780310, 8, 5)
     , (78780310, 9, 0)
     , (78780310, 16, 524296)
     , (78780310, 19, 50000)
     , (78780310, 93, 1044)
     , (78780310, 94, 33025)
     , (78780310, 50116, 1)
     , (78780310, 50117, 1);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780310, 22, True)
     , (78780310, 23, True)
     , (78780310, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780310, 1, 'Damage Hone Stone')
     , (78780310, 14, 'Use this stone on a weapon while standing at a grindstone.')
     , (78780310, 16, 'A smith''s hone stone. Used on a weapon at a grindstone, it tries to hone its damage by one level. Each attempt costs pyreals; the stone is never used up and the weapon is never harmed.')
     , (78780310, 20, 'Damage Hone Stones');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780310, 1, 0x020007B7)
     , (78780310, 3, 0x20000014)
     , (78780310, 6, 0x04000BEF)
     , (78780310, 7, 0x100001FD)
     , (78780310, 8, 0x06001C1C)
     , (78780310, 22, 0x3400002B);

-- 78780311 forge-hone-stone-balance: Balance Hone Stone (copied from retail 6319)
DELETE FROM `weenie` WHERE `class_Id` = 78780311 AND `class_Name` = 'forge-hone-stone-balance';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780311, 'forge-hone-stone-balance', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780311, 1, 2048)
     , (78780311, 3, 8)
     , (78780311, 5, 5)
     , (78780311, 8, 5)
     , (78780311, 9, 0)
     , (78780311, 16, 524296)
     , (78780311, 19, 50000)
     , (78780311, 93, 1044)
     , (78780311, 94, 33025)
     , (78780311, 50116, 1)
     , (78780311, 50117, 2);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780311, 22, True)
     , (78780311, 23, True)
     , (78780311, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780311, 1, 'Balance Hone Stone')
     , (78780311, 14, 'Use this stone on a weapon while standing at a grindstone.')
     , (78780311, 16, 'A smith''s hone stone. Used on a weapon at a grindstone, it tries to hone its variance (a steadier hit) by one level. Each attempt costs pyreals; the stone is never used up and the weapon is never harmed.')
     , (78780311, 20, 'Balance Hone Stones');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780311, 1, 0x020007B7)
     , (78780311, 3, 0x20000014)
     , (78780311, 6, 0x04000BEF)
     , (78780311, 7, 0x100001FD)
     , (78780311, 8, 0x06001C1F)
     , (78780311, 22, 0x3400002B);

-- 78780312 forge-hone-stone-swiftness: Swiftness Hone Stone (copied from retail 6320)
DELETE FROM `weenie` WHERE `class_Id` = 78780312 AND `class_Name` = 'forge-hone-stone-swiftness';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780312, 'forge-hone-stone-swiftness', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780312, 1, 2048)
     , (78780312, 3, 14)
     , (78780312, 5, 5)
     , (78780312, 8, 5)
     , (78780312, 9, 0)
     , (78780312, 16, 524296)
     , (78780312, 19, 50000)
     , (78780312, 93, 1044)
     , (78780312, 94, 33025)
     , (78780312, 50116, 1)
     , (78780312, 50117, 3);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780312, 22, True)
     , (78780312, 23, True)
     , (78780312, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780312, 1, 'Swiftness Hone Stone')
     , (78780312, 14, 'Use this stone on a weapon while standing at a grindstone.')
     , (78780312, 16, 'A smith''s hone stone. Used on a weapon at a grindstone, it tries to hone its speed by one level. Each attempt costs pyreals; the stone is never used up and the weapon is never harmed.')
     , (78780312, 20, 'Swiftness Hone Stones');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780312, 1, 0x020007B7)
     , (78780312, 3, 0x20000014)
     , (78780312, 6, 0x04000BEF)
     , (78780312, 7, 0x100001FD)
     , (78780312, 8, 0x06001C20)
     , (78780312, 22, 0x3400002B);

-- 78780313 forge-hone-stone-attack: Attack Hone Stone (copied from retail 6321)
DELETE FROM `weenie` WHERE `class_Id` = 78780313 AND `class_Name` = 'forge-hone-stone-attack';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780313, 'forge-hone-stone-attack', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780313, 1, 2048)
     , (78780313, 3, 2)
     , (78780313, 5, 5)
     , (78780313, 8, 5)
     , (78780313, 9, 0)
     , (78780313, 16, 524296)
     , (78780313, 19, 50000)
     , (78780313, 93, 1044)
     , (78780313, 94, 33025)
     , (78780313, 50116, 1)
     , (78780313, 50117, 4);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780313, 22, True)
     , (78780313, 23, True)
     , (78780313, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780313, 1, 'Attack Hone Stone')
     , (78780313, 14, 'Use this stone on a weapon while standing at a grindstone.')
     , (78780313, 16, 'A smith''s hone stone. Used on a weapon at a grindstone, it tries to hone its attack modifier by one level. Each attempt costs pyreals; the stone is never used up and the weapon is never harmed.')
     , (78780313, 20, 'Attack Hone Stones');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780313, 1, 0x020007B7)
     , (78780313, 3, 0x20000014)
     , (78780313, 6, 0x04000BEF)
     , (78780313, 7, 0x100001FD)
     , (78780313, 8, 0x06001C19)
     , (78780313, 22, 0x3400002B);

-- 78780314 forge-hone-stone-melee-defense: Melee Defense Hone Stone (copied from retail 6318)
DELETE FROM `weenie` WHERE `class_Id` = 78780314 AND `class_Name` = 'forge-hone-stone-melee-defense';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780314, 'forge-hone-stone-melee-defense', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780314, 1, 2048)
     , (78780314, 3, 82)
     , (78780314, 5, 5)
     , (78780314, 8, 5)
     , (78780314, 9, 0)
     , (78780314, 16, 524296)
     , (78780314, 19, 50000)
     , (78780314, 93, 1044)
     , (78780314, 94, 33025)
     , (78780314, 50116, 1)
     , (78780314, 50117, 5);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780314, 22, True)
     , (78780314, 23, True)
     , (78780314, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780314, 1, 'Melee Defense Hone Stone')
     , (78780314, 14, 'Use this stone on a weapon while standing at a grindstone.')
     , (78780314, 16, 'A smith''s hone stone. Used on a weapon at a grindstone, it tries to hone its melee defense by one level. Each attempt costs pyreals; the stone is never used up and the weapon is never harmed.')
     , (78780314, 20, 'Melee Defense Hone Stones');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780314, 1, 0x020007B7)
     , (78780314, 3, 0x20000014)
     , (78780314, 6, 0x04000BEF)
     , (78780314, 7, 0x100001FD)
     , (78780314, 8, 0x06001C1C)
     , (78780314, 22, 0x3400002B);

-- 78780315 forge-hone-stone-missile-defense: Missile Defense Hone Stone (copied from retail 6319)
DELETE FROM `weenie` WHERE `class_Id` = 78780315 AND `class_Name` = 'forge-hone-stone-missile-defense';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780315, 'forge-hone-stone-missile-defense', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780315, 1, 2048)
     , (78780315, 3, 8)
     , (78780315, 5, 5)
     , (78780315, 8, 5)
     , (78780315, 9, 0)
     , (78780315, 16, 524296)
     , (78780315, 19, 50000)
     , (78780315, 93, 1044)
     , (78780315, 94, 33025)
     , (78780315, 50116, 1)
     , (78780315, 50117, 6);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780315, 22, True)
     , (78780315, 23, True)
     , (78780315, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780315, 1, 'Missile Defense Hone Stone')
     , (78780315, 14, 'Use this stone on a weapon while standing at a grindstone.')
     , (78780315, 16, 'A smith''s hone stone. Used on a weapon at a grindstone, it tries to hone its missile defense by one level. Each attempt costs pyreals; the stone is never used up and the weapon is never harmed.')
     , (78780315, 20, 'Missile Defense Hone Stones');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780315, 1, 0x020007B7)
     , (78780315, 3, 0x20000014)
     , (78780315, 6, 0x04000BEF)
     , (78780315, 7, 0x100001FD)
     , (78780315, 8, 0x06001C1F)
     , (78780315, 22, 0x3400002B);

-- 78780316 forge-hone-stone-magic-defense: Magic Defense Hone Stone (copied from retail 6320)
DELETE FROM `weenie` WHERE `class_Id` = 78780316 AND `class_Name` = 'forge-hone-stone-magic-defense';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780316, 'forge-hone-stone-magic-defense', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780316, 1, 2048)
     , (78780316, 3, 14)
     , (78780316, 5, 5)
     , (78780316, 8, 5)
     , (78780316, 9, 0)
     , (78780316, 16, 524296)
     , (78780316, 19, 50000)
     , (78780316, 93, 1044)
     , (78780316, 94, 33025)
     , (78780316, 50116, 1)
     , (78780316, 50117, 7);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780316, 22, True)
     , (78780316, 23, True)
     , (78780316, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780316, 1, 'Magic Defense Hone Stone')
     , (78780316, 14, 'Use this stone on a weapon while standing at a grindstone.')
     , (78780316, 16, 'A smith''s hone stone. Used on a weapon at a grindstone, it tries to hone its magic defense by one level. Each attempt costs pyreals; the stone is never used up and the weapon is never harmed.')
     , (78780316, 20, 'Magic Defense Hone Stones');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780316, 1, 0x020007B7)
     , (78780316, 3, 0x20000014)
     , (78780316, 6, 0x04000BEF)
     , (78780316, 7, 0x100001FD)
     , (78780316, 8, 0x06001C20)
     , (78780316, 22, 0x3400002B);

-- 78780317 forge-hone-stone-damage-modifier: Damage Modifier Hone Stone (copied from retail 6321)
DELETE FROM `weenie` WHERE `class_Id` = 78780317 AND `class_Name` = 'forge-hone-stone-damage-modifier';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780317, 'forge-hone-stone-damage-modifier', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780317, 1, 2048)
     , (78780317, 3, 2)
     , (78780317, 5, 5)
     , (78780317, 8, 5)
     , (78780317, 9, 0)
     , (78780317, 16, 524296)
     , (78780317, 19, 50000)
     , (78780317, 93, 1044)
     , (78780317, 94, 33025)
     , (78780317, 50116, 1)
     , (78780317, 50117, 8);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780317, 22, True)
     , (78780317, 23, True)
     , (78780317, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780317, 1, 'Damage Modifier Hone Stone')
     , (78780317, 14, 'Use this stone on a weapon while standing at a grindstone.')
     , (78780317, 16, 'A smith''s hone stone. Used on a weapon at a grindstone, it tries to hone its damage modifier (casters and missile weapons) by one level. Each attempt costs pyreals; the stone is never used up and the weapon is never harmed.')
     , (78780317, 20, 'Damage Modifier Hone Stones');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780317, 1, 0x020007B7)
     , (78780317, 3, 0x20000014)
     , (78780317, 6, 0x04000BEF)
     , (78780317, 7, 0x100001FD)
     , (78780317, 8, 0x06001C19)
     , (78780317, 22, 0x3400002B);

-- 78780318 forge-flux: Smith's Flux (copied from retail 7598)
DELETE FROM `weenie` WHERE `class_Id` = 78780318 AND `class_Name` = 'forge-flux';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780318, 'forge-flux', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780318, 1, 8388608)
     , (78780318, 3, 39)
     , (78780318, 5, 50)
     , (78780318, 8, 50)
     , (78780318, 9, 0)
     , (78780318, 11, 100)
     , (78780318, 12, 1)
     , (78780318, 15, 100000)
     , (78780318, 16, 524296)
     , (78780318, 19, 100000)
     , (78780318, 93, 1044)
     , (78780318, 94, 33025)
     , (78780318, 50116, 3)
     , (78780318, 50117, 0);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780318, 23, True)
     , (78780318, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780318, 1, 'Smith''s Flux')
     , (78780318, 14, 'Keep this in your pack: one is used each time you hone a weapon.')
     , (78780318, 16, 'A smith''s flux. Carried in your pack, one is used on each hone attempt and makes it likelier to take.')
     , (78780318, 20, 'Smith''s Flux');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780318, 1, 0x020005FD)
     , (78780318, 3, 0x20000014)
     , (78780318, 6, 0x04000BEF)
     , (78780318, 7, 0x10000166)
     , (78780318, 8, 0x06001D11)
     , (78780318, 22, 0x3400002B);

-- 78780319 forge-unbinding-oil: Unbinding Oil (copied from retail 19533)
DELETE FROM `weenie` WHERE `class_Id` = 78780319 AND `class_Name` = 'forge-unbinding-oil';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780319, 'forge-unbinding-oil', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780319, 1, 67108864)
     , (78780319, 3, 82)
     , (78780319, 5, 15)
     , (78780319, 8, 5)
     , (78780319, 9, 0)
     , (78780319, 16, 524296)
     , (78780319, 19, 250000)
     , (78780319, 93, 1044)
     , (78780319, 94, 33031)
     , (78780319, 150, 103)
     , (78780319, 151, 11)
     , (78780319, 50116, 4)
     , (78780319, 50117, 0);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780319, 22, True)
     , (78780319, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780319, 1, 'Unbinding Oil')
     , (78780319, 14, 'Use this oil on a forged or honed weapon, or a forged piece of armour, in your pack.')
     , (78780319, 16, 'A smith''s oil that loosens the binding on a forged or honed weapon or a forged piece of armour, for a price in pyreals. It can then change hands, and binds again to whoever wields it next. The oil is never used up.')
     , (78780319, 20, 'Unbinding Oils');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780319, 1, 0x020005FD)
     , (78780319, 3, 0x20000014)
     , (78780319, 6, 0x04000BEF)
     , (78780319, 7, 0x10000166)
     , (78780319, 8, 0x06002563)
     , (78780319, 22, 0x3400002B);

-- 78780320 forge-dye-smith: Smith's Dye (copied from retail 8643)
DELETE FROM `weenie` WHERE `class_Id` = 78780320 AND `class_Name` = 'forge-dye-smith';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780320, 'forge-dye-smith', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780320, 1, 67108864)
     , (78780320, 3, 90)
     , (78780320, 5, 10)
     , (78780320, 8, 5)
     , (78780320, 9, 0)
     , (78780320, 16, 524296)
     , (78780320, 19, 25000)
     , (78780320, 93, 1044)
     , (78780320, 94, 33047)
     , (78780320, 150, 103)
     , (78780320, 151, 9)
     , (78780320, 50116, 2)
     , (78780320, 50117, 0);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780320, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780320, 1, 'Smith''s Dye')
     , (78780320, 14, 'Use this dye on a weapon or a piece of armour or clothing while standing at a dye vat. Use it on yourself to dye everything you are wearing the same colour.')
     , (78780320, 16, 'A smith''s dye. Used at a dye vat on a weapon, armour or clothing, it tries any colour on it. Used on yourself, it tries the colour on everything you wear. Keeping the colour costs pyreals per piece; trying is free, and the dye is never used up.')
     , (78780320, 20, 'Smith''s Dyes');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780320, 1, 0x0200090F)
     , (78780320, 3, 0x20000014)
     , (78780320, 6, 0x04000BEF)
     , (78780320, 7, 0x10000242)
     , (78780320, 8, 0x0600690C)
     , (78780320, 22, 0x3400002B);

-- 78780321 forge-dye-crimson: Crimson Dye (copied from retail 8643)
DELETE FROM `weenie` WHERE `class_Id` = 78780321 AND `class_Name` = 'forge-dye-crimson';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780321, 'forge-dye-crimson', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780321, 1, 67108864)
     , (78780321, 3, 90)
     , (78780321, 5, 10)
     , (78780321, 8, 5)
     , (78780321, 9, 0)
     , (78780321, 16, 524296)
     , (78780321, 19, 100000)
     , (78780321, 93, 1044)
     , (78780321, 94, 33047)
     , (78780321, 150, 103)
     , (78780321, 151, 9)
     , (78780321, 50116, 2)
     , (78780321, 50117, 1);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780321, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780321, 1, 'Crimson Dye')
     , (78780321, 14, 'Use this dye on a weapon or a piece of armour or clothing while standing at a dye vat. Use it on yourself to dye everything you are wearing the same colour.')
     , (78780321, 16, 'A smith''s dye. Used at a dye vat on a weapon, armour or clothing, it tries a red on it. Used on yourself, it tries the colour on everything you wear. Keeping the colour costs pyreals per piece; trying is free, and the dye is never used up.')
     , (78780321, 20, 'Crimson Dyes');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780321, 1, 0x0200090F)
     , (78780321, 3, 0x20000014)
     , (78780321, 6, 0x04000BEF)
     , (78780321, 7, 0x10000242)
     , (78780321, 8, 0x060061BA)
     , (78780321, 22, 0x3400002B);

-- 78780322 forge-dye-amber: Amber Dye (copied from retail 8643)
DELETE FROM `weenie` WHERE `class_Id` = 78780322 AND `class_Name` = 'forge-dye-amber';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780322, 'forge-dye-amber', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780322, 1, 67108864)
     , (78780322, 3, 90)
     , (78780322, 5, 10)
     , (78780322, 8, 5)
     , (78780322, 9, 0)
     , (78780322, 16, 524296)
     , (78780322, 19, 100000)
     , (78780322, 93, 1044)
     , (78780322, 94, 33047)
     , (78780322, 150, 103)
     , (78780322, 151, 9)
     , (78780322, 50116, 2)
     , (78780322, 50117, 2);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780322, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780322, 1, 'Amber Dye')
     , (78780322, 14, 'Use this dye on a weapon or a piece of armour or clothing while standing at a dye vat. Use it on yourself to dye everything you are wearing the same colour.')
     , (78780322, 16, 'A smith''s dye. Used at a dye vat on a weapon, armour or clothing, it tries an orange on it. Used on yourself, it tries the colour on everything you wear. Keeping the colour costs pyreals per piece; trying is free, and the dye is never used up.')
     , (78780322, 20, 'Amber Dyes');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780322, 1, 0x0200090F)
     , (78780322, 3, 0x20000014)
     , (78780322, 6, 0x04000BEF)
     , (78780322, 7, 0x10000242)
     , (78780322, 8, 0x060061C1)
     , (78780322, 22, 0x3400002B);

-- 78780323 forge-dye-golden: Golden Dye (copied from retail 8643)
DELETE FROM `weenie` WHERE `class_Id` = 78780323 AND `class_Name` = 'forge-dye-golden';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780323, 'forge-dye-golden', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780323, 1, 67108864)
     , (78780323, 3, 90)
     , (78780323, 5, 10)
     , (78780323, 8, 5)
     , (78780323, 9, 0)
     , (78780323, 16, 524296)
     , (78780323, 19, 100000)
     , (78780323, 93, 1044)
     , (78780323, 94, 33047)
     , (78780323, 150, 103)
     , (78780323, 151, 9)
     , (78780323, 50116, 2)
     , (78780323, 50117, 3);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780323, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780323, 1, 'Golden Dye')
     , (78780323, 14, 'Use this dye on a weapon or a piece of armour or clothing while standing at a dye vat. Use it on yourself to dye everything you are wearing the same colour.')
     , (78780323, 16, 'A smith''s dye. Used at a dye vat on a weapon, armour or clothing, it tries a yellow on it. Used on yourself, it tries the colour on everything you wear. Keeping the colour costs pyreals per piece; trying is free, and the dye is never used up.')
     , (78780323, 20, 'Golden Dyes');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780323, 1, 0x0200090F)
     , (78780323, 3, 0x20000014)
     , (78780323, 6, 0x04000BEF)
     , (78780323, 7, 0x10000242)
     , (78780323, 8, 0x060061B8)
     , (78780323, 22, 0x3400002B);

-- 78780324 forge-dye-verdant: Verdant Dye (copied from retail 8643)
DELETE FROM `weenie` WHERE `class_Id` = 78780324 AND `class_Name` = 'forge-dye-verdant';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780324, 'forge-dye-verdant', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780324, 1, 67108864)
     , (78780324, 3, 90)
     , (78780324, 5, 10)
     , (78780324, 8, 5)
     , (78780324, 9, 0)
     , (78780324, 16, 524296)
     , (78780324, 19, 100000)
     , (78780324, 93, 1044)
     , (78780324, 94, 33047)
     , (78780324, 150, 103)
     , (78780324, 151, 9)
     , (78780324, 50116, 2)
     , (78780324, 50117, 4);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780324, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780324, 1, 'Verdant Dye')
     , (78780324, 14, 'Use this dye on a weapon or a piece of armour or clothing while standing at a dye vat. Use it on yourself to dye everything you are wearing the same colour.')
     , (78780324, 16, 'A smith''s dye. Used at a dye vat on a weapon, armour or clothing, it tries a green on it. Used on yourself, it tries the colour on everything you wear. Keeping the colour costs pyreals per piece; trying is free, and the dye is never used up.')
     , (78780324, 20, 'Verdant Dyes');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780324, 1, 0x0200090F)
     , (78780324, 3, 0x20000014)
     , (78780324, 6, 0x04000BEF)
     , (78780324, 7, 0x10000242)
     , (78780324, 8, 0x060061B9)
     , (78780324, 22, 0x3400002B);

-- 78780325 forge-dye-teal: Teal Dye (copied from retail 8643)
DELETE FROM `weenie` WHERE `class_Id` = 78780325 AND `class_Name` = 'forge-dye-teal';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780325, 'forge-dye-teal', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780325, 1, 67108864)
     , (78780325, 3, 90)
     , (78780325, 5, 10)
     , (78780325, 8, 5)
     , (78780325, 9, 0)
     , (78780325, 16, 524296)
     , (78780325, 19, 100000)
     , (78780325, 93, 1044)
     , (78780325, 94, 33047)
     , (78780325, 150, 103)
     , (78780325, 151, 9)
     , (78780325, 50116, 2)
     , (78780325, 50117, 5);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780325, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780325, 1, 'Teal Dye')
     , (78780325, 14, 'Use this dye on a weapon or a piece of armour or clothing while standing at a dye vat. Use it on yourself to dye everything you are wearing the same colour.')
     , (78780325, 16, 'A smith''s dye. Used at a dye vat on a weapon, armour or clothing, it tries a teal on it. Used on yourself, it tries the colour on everything you wear. Keeping the colour costs pyreals per piece; trying is free, and the dye is never used up.')
     , (78780325, 20, 'Teal Dyes');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780325, 1, 0x0200090F)
     , (78780325, 3, 0x20000014)
     , (78780325, 6, 0x04000BEF)
     , (78780325, 7, 0x10000242)
     , (78780325, 8, 0x060061C0)
     , (78780325, 22, 0x3400002B);

-- 78780326 forge-dye-azure: Azure Dye (copied from retail 8643)
DELETE FROM `weenie` WHERE `class_Id` = 78780326 AND `class_Name` = 'forge-dye-azure';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780326, 'forge-dye-azure', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780326, 1, 67108864)
     , (78780326, 3, 90)
     , (78780326, 5, 10)
     , (78780326, 8, 5)
     , (78780326, 9, 0)
     , (78780326, 16, 524296)
     , (78780326, 19, 100000)
     , (78780326, 93, 1044)
     , (78780326, 94, 33047)
     , (78780326, 150, 103)
     , (78780326, 151, 9)
     , (78780326, 50116, 2)
     , (78780326, 50117, 6);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780326, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780326, 1, 'Azure Dye')
     , (78780326, 14, 'Use this dye on a weapon or a piece of armour or clothing while standing at a dye vat. Use it on yourself to dye everything you are wearing the same colour.')
     , (78780326, 16, 'A smith''s dye. Used at a dye vat on a weapon, armour or clothing, it tries a blue on it. Used on yourself, it tries the colour on everything you wear. Keeping the colour costs pyreals per piece; trying is free, and the dye is never used up.')
     , (78780326, 20, 'Azure Dyes');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780326, 1, 0x0200090F)
     , (78780326, 3, 0x20000014)
     , (78780326, 6, 0x04000BEF)
     , (78780326, 7, 0x10000242)
     , (78780326, 8, 0x060061BD)
     , (78780326, 22, 0x3400002B);

-- 78780327 forge-dye-violet: Violet Dye (copied from retail 8643)
DELETE FROM `weenie` WHERE `class_Id` = 78780327 AND `class_Name` = 'forge-dye-violet';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780327, 'forge-dye-violet', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780327, 1, 67108864)
     , (78780327, 3, 90)
     , (78780327, 5, 10)
     , (78780327, 8, 5)
     , (78780327, 9, 0)
     , (78780327, 16, 524296)
     , (78780327, 19, 100000)
     , (78780327, 93, 1044)
     , (78780327, 94, 33047)
     , (78780327, 150, 103)
     , (78780327, 151, 9)
     , (78780327, 50116, 2)
     , (78780327, 50117, 7);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780327, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780327, 1, 'Violet Dye')
     , (78780327, 14, 'Use this dye on a weapon or a piece of armour or clothing while standing at a dye vat. Use it on yourself to dye everything you are wearing the same colour.')
     , (78780327, 16, 'A smith''s dye. Used at a dye vat on a weapon, armour or clothing, it tries a violet on it. Used on yourself, it tries the colour on everything you wear. Keeping the colour costs pyreals per piece; trying is free, and the dye is never used up.')
     , (78780327, 20, 'Violet Dyes');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780327, 1, 0x0200090F)
     , (78780327, 3, 0x20000014)
     , (78780327, 6, 0x04000BEF)
     , (78780327, 7, 0x10000242)
     , (78780327, 8, 0x060061BC)
     , (78780327, 22, 0x3400002B);

-- 78780328 forge-dye-rose: Rose Dye (copied from retail 8643)
DELETE FROM `weenie` WHERE `class_Id` = 78780328 AND `class_Name` = 'forge-dye-rose';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780328, 'forge-dye-rose', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780328, 1, 67108864)
     , (78780328, 3, 90)
     , (78780328, 5, 10)
     , (78780328, 8, 5)
     , (78780328, 9, 0)
     , (78780328, 16, 524296)
     , (78780328, 19, 100000)
     , (78780328, 93, 1044)
     , (78780328, 94, 33047)
     , (78780328, 150, 103)
     , (78780328, 151, 9)
     , (78780328, 50116, 2)
     , (78780328, 50117, 8);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780328, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780328, 1, 'Rose Dye')
     , (78780328, 14, 'Use this dye on a weapon or a piece of armour or clothing while standing at a dye vat. Use it on yourself to dye everything you are wearing the same colour.')
     , (78780328, 16, 'A smith''s dye. Used at a dye vat on a weapon, armour or clothing, it tries a pink on it. Used on yourself, it tries the colour on everything you wear. Keeping the colour costs pyreals per piece; trying is free, and the dye is never used up.')
     , (78780328, 20, 'Rose Dyes');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780328, 1, 0x0200090F)
     , (78780328, 3, 0x20000014)
     , (78780328, 6, 0x04000BEF)
     , (78780328, 7, 0x10000242)
     , (78780328, 8, 0x0600690E)
     , (78780328, 22, 0x3400002B);

-- 78780329 forge-dye-ashen: Ashen Dye (copied from retail 8643)
DELETE FROM `weenie` WHERE `class_Id` = 78780329 AND `class_Name` = 'forge-dye-ashen';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780329, 'forge-dye-ashen', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780329, 1, 67108864)
     , (78780329, 3, 90)
     , (78780329, 5, 10)
     , (78780329, 8, 5)
     , (78780329, 9, 0)
     , (78780329, 16, 524296)
     , (78780329, 19, 100000)
     , (78780329, 93, 1044)
     , (78780329, 94, 33047)
     , (78780329, 150, 103)
     , (78780329, 151, 9)
     , (78780329, 50116, 2)
     , (78780329, 50117, 9);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780329, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780329, 1, 'Ashen Dye')
     , (78780329, 14, 'Use this dye on a weapon or a piece of armour or clothing while standing at a dye vat. Use it on yourself to dye everything you are wearing the same colour.')
     , (78780329, 16, 'A smith''s dye. Used at a dye vat on a weapon, armour or clothing, it tries a grey on it. Used on yourself, it tries the colour on everything you wear. Keeping the colour costs pyreals per piece; trying is free, and the dye is never used up.')
     , (78780329, 20, 'Ashen Dyes');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780329, 1, 0x0200090F)
     , (78780329, 3, 0x20000014)
     , (78780329, 6, 0x04000BEF)
     , (78780329, 7, 0x10000242)
     , (78780329, 8, 0x060061BE)
     , (78780329, 22, 0x3400002B);

-- 78780303 npc-forge-apprentice: Mei the Smith's Apprentice, vendor of forge tools (copied from retail 835)
DELETE FROM `weenie` WHERE `class_Id` = 78780303 AND `class_Name` = 'npc-forge-apprentice';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780303, 'npc-forge-apprentice', 12, '2026-10-03 12:00:00') /* Vendor */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780303, 1, 16)
     , (78780303, 2, 31)
     , (78780303, 6, -1)
     , (78780303, 7, -1)
     , (78780303, 8, 120)
     , (78780303, 16, 32)
     , (78780303, 25, 6)
     , (78780303, 27, 0)
     , (78780303, 74, 0)
     , (78780303, 75, 0)
     , (78780303, 76, 1000000)
     , (78780303, 93, 2098200)
     , (78780303, 126, 1000)
     , (78780303, 127, 500)
     , (78780303, 133, 4)
     , (78780303, 134, 16)
     , (78780303, 146, 108);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780303, 1, True)
     , (78780303, 12, True)
     , (78780303, 13, False)
     , (78780303, 19, False)
     , (78780303, 39, True)
     , (78780303, 41, True);

INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`)
VALUES (78780303, 1, 5)
     , (78780303, 2, 0)
     , (78780303, 3, 0.16)
     , (78780303, 4, 5)
     , (78780303, 5, 1)
     , (78780303, 11, 300)
     , (78780303, 13, 0.9)
     , (78780303, 14, 1)
     , (78780303, 15, 1.1)
     , (78780303, 16, 0.4)
     , (78780303, 17, 0.4)
     , (78780303, 18, 1)
     , (78780303, 19, 0.6)
     , (78780303, 37, 0.9)
     , (78780303, 38, 1)
     , (78780303, 54, 3)
     , (78780303, 64, 1)
     , (78780303, 65, 1)
     , (78780303, 66, 1)
     , (78780303, 67, 1)
     , (78780303, 68, 1)
     , (78780303, 69, 1)
     , (78780303, 70, 1)
     , (78780303, 71, 1)
     , (78780303, 72, 1)
     , (78780303, 73, 1)
     , (78780303, 74, 1)
     , (78780303, 75, 1)
     , (78780303, 104, 10)
     , (78780303, 125, 1);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780303, 1, 'Mei the Smith''s Apprentice')
     , (78780303, 3, 'Female')
     , (78780303, 4, 'Sho')
     , (78780303, 5, 'Smith''s Apprentice');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780303, 1, 0x0200004E)
     , (78780303, 2, 0x09000001)
     , (78780303, 3, 0x20000002)
     , (78780303, 4, 0x30000000)
     , (78780303, 8, 0x06001036);

INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`)
VALUES (78780303, 1, 55, 0, 0)
     , (78780303, 2, 65, 0, 0)
     , (78780303, 3, 50, 0, 0)
     , (78780303, 4, 50, 0, 0)
     , (78780303, 5, 35, 0, 0)
     , (78780303, 6, 25, 0, 0);

INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`)
VALUES (78780303, 1, 95, 0, 0, 128)
     , (78780303, 3, 100, 0, 0, 165)
     , (78780303, 5, 30, 0, 0, 55);

INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`)
VALUES (78780303, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0.33, 0, 0, 0.33, 0, 0, 0.33, 0, 0, 0.33, 0, 0)
     , (78780303, 1, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0.44, 0.17, 0, 0.44, 0.17, 0, 0.44, 0.17, 0, 0.44, 0.17, 0)
     , (78780303, 2, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0.17, 0, 0, 0.17, 0, 0, 0.17, 0, 0, 0.17, 0)
     , (78780303, 3, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0.23, 0.03, 0, 0.23, 0.03, 0, 0.23, 0.03, 0, 0.23, 0.03, 0)
     , (78780303, 4, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0.3, 0, 0, 0.3, 0, 0, 0.3, 0, 0, 0.3, 0)
     , (78780303, 5, 4, 2, 0.75, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0.2, 0, 0, 0.2, 0, 0, 0.2, 0, 0, 0.2, 0)
     , (78780303, 6, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0.13, 0.18, 0, 0.13, 0.18, 0, 0.13, 0.18, 0, 0.13, 0.18)
     , (78780303, 7, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0.6, 0, 0, 0.6, 0, 0, 0.6, 0, 0, 0.6)
     , (78780303, 8, 4, 2, 0.75, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0.22, 0, 0, 0.22, 0, 0, 0.22, 0, 0, 0.22);

INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`)
VALUES (78780303, 2, 303, 0, 0, 0, False)
     , (78780303, 2, 2596, 0, 13, 0.5, False)
     , (78780303, 2, 2602, 0, 9, 1, False)
     , (78780303, 2, 132, 0, 5, 0, False)
     , (78780303, 2, 10696, 0, 15, 1, False)
     , (78780303, 4, 78780310, -1, 0, 0, False)
     , (78780303, 4, 78780311, -1, 0, 0, False)
     , (78780303, 4, 78780312, -1, 0, 0, False)
     , (78780303, 4, 78780313, -1, 0, 0, False)
     , (78780303, 4, 78780314, -1, 0, 0, False)
     , (78780303, 4, 78780315, -1, 0, 0, False)
     , (78780303, 4, 78780316, -1, 0, 0, False)
     , (78780303, 4, 78780317, -1, 0, 0, False)
     , (78780303, 4, 78780318, -1, 0, 0, False)
     , (78780303, 4, 78780319, -1, 0, 0, False)
     , (78780303, 4, 78780320, -1, 0, 0, False)
     , (78780303, 4, 78780321, -1, 0, 0, False)
     , (78780303, 4, 78780322, -1, 0, 0, False)
     , (78780303, 4, 78780323, -1, 0, 0, False)
     , (78780303, 4, 78780324, -1, 0, 0, False)
     , (78780303, 4, 78780325, -1, 0, 0, False)
     , (78780303, 4, 78780326, -1, 0, 0, False)
     , (78780303, 4, 78780327, -1, 0, 0, False)
     , (78780303, 4, 78780328, -1, 0, 0, False)
     , (78780303, 4, 78780329, -1, 0, 0, False);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780303, 2, 0.8, NULL, NULL, NULL, NULL, 1, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 10, 0, 1, NULL, 'Welcome! What''s your pleasure today?', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780303, 2, 0.8, NULL, NULL, NULL, NULL, 2, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 10, 0, 1, NULL, 'Thank you for your business. Please return soon.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780303, 2, 0.8, NULL, NULL, NULL, NULL, 3, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 10, 0, 1, NULL, 'You drive a hard bargain, my friend.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780303, 2, 0.8, NULL, NULL, NULL, NULL, 4, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 10, 0, 1, NULL, 'An excellent purchase.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780303, 2, 0.125, NULL, NULL, NULL, NULL, 5, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 5, 0, 1, 318767239, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780303, 2, 0.25, NULL, NULL, NULL, NULL, 5, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 5, 0, 1, 318767229, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780303, 2, 0.375, NULL, NULL, NULL, NULL, 5, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 5, 0, 1, 318767238, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780303, 2, 0.5, NULL, NULL, NULL, NULL, 5, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 5, 0, 1, 318767235, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

COMMIT;

-- Check: must list 24 rows, 78780300 to 78780329.
SELECT w.class_Id, w.class_Name, s.value AS name
FROM weenie w LEFT JOIN weenie_properties_string s ON s.object_Id = w.class_Id AND s.type = 1
WHERE w.class_Id BETWEEN 78780300 AND 78780349 ORDER BY w.class_Id;
