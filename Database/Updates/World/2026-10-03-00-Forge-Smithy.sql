-- ------------------------------------------------------------------------------------
-- Blacksmithing: the rest of the smithy - wcids 78780401-78780429 (docs/WCID_ALLOCATION_7878.md).
-- Moved here from 787803xx on 2026-10-05: 78780310-78780329 belong to the Salvage Bags.
--   78780401  Grindstone        station: hone stones only work near one   (PropertyBool 52001 ForgeGrindstone)
--   78780402  Dye Vat           station: dyes only work near one          (PropertyBool 52002 ForgeDyeVat)
--   78780403  smith's apprentice, a vendor selling every tool below
--   78780404  Sorrel the Dyer, a vendor selling only the ten dyes (for opening the dye vats on their own)
--   78780410-78780417  hone stones, one per stat (ForgeTool 1, ForgeToolArg = ForgeMath.ForgeLine 1-8)
--   78780418  Smith's Flux      (ForgeTool 3; one is used per hone attempt, +forge_flux_bonus)
--   78780419  Unbinding Oil     (ForgeTool 4)
--   78780420-78780429  dyes: any colour, then one per colour family (ForgeTool 2, ForgeToolArg = ForgeDyes.Family 0-9)
--
-- Tools are reusable instruments (never consumed, except flux): the pyreals are charged by the action itself.
-- All of them are IsSellable FALSE so no vendor buys them back. PropertyInt 52004 ForgeTool / 52005 ForgeToolArg.
-- Nothing is placed by this file. Bodies and art are copied from retail weenies named on each block.
-- Every string is plain ASCII: the client cannot draw anything else.
-- ------------------------------------------------------------------------------------

-- Old ids from before the move to 787804xx (only rows this file itself created are removed).
DELETE FROM `weenie` WHERE `class_Id` = 78780301 AND `class_Name` = 'forge-grindstone';
DELETE FROM `weenie` WHERE `class_Id` = 78780302 AND `class_Name` = 'forge-dye-vat';
DELETE FROM `weenie` WHERE `class_Id` = 78780310 AND `class_Name` = 'forge-hone-stone-damage';
DELETE FROM `weenie` WHERE `class_Id` = 78780311 AND `class_Name` = 'forge-hone-stone-balance';
DELETE FROM `weenie` WHERE `class_Id` = 78780312 AND `class_Name` = 'forge-hone-stone-swiftness';
DELETE FROM `weenie` WHERE `class_Id` = 78780313 AND `class_Name` = 'forge-hone-stone-attack';
DELETE FROM `weenie` WHERE `class_Id` = 78780314 AND `class_Name` = 'forge-hone-stone-melee-defense';
DELETE FROM `weenie` WHERE `class_Id` = 78780315 AND `class_Name` = 'forge-hone-stone-missile-defense';
DELETE FROM `weenie` WHERE `class_Id` = 78780316 AND `class_Name` = 'forge-hone-stone-magic-defense';
DELETE FROM `weenie` WHERE `class_Id` = 78780317 AND `class_Name` = 'forge-hone-stone-damage-modifier';
DELETE FROM `weenie` WHERE `class_Id` = 78780318 AND `class_Name` = 'forge-flux';
DELETE FROM `weenie` WHERE `class_Id` = 78780319 AND `class_Name` = 'forge-unbinding-oil';
DELETE FROM `weenie` WHERE `class_Id` = 78780320 AND `class_Name` = 'forge-dye-smith';
DELETE FROM `weenie` WHERE `class_Id` = 78780321 AND `class_Name` = 'forge-dye-crimson';
DELETE FROM `weenie` WHERE `class_Id` = 78780322 AND `class_Name` = 'forge-dye-amber';
DELETE FROM `weenie` WHERE `class_Id` = 78780323 AND `class_Name` = 'forge-dye-golden';
DELETE FROM `weenie` WHERE `class_Id` = 78780324 AND `class_Name` = 'forge-dye-verdant';
DELETE FROM `weenie` WHERE `class_Id` = 78780325 AND `class_Name` = 'forge-dye-teal';
DELETE FROM `weenie` WHERE `class_Id` = 78780326 AND `class_Name` = 'forge-dye-azure';
DELETE FROM `weenie` WHERE `class_Id` = 78780327 AND `class_Name` = 'forge-dye-violet';
DELETE FROM `weenie` WHERE `class_Id` = 78780328 AND `class_Name` = 'forge-dye-rose';
DELETE FROM `weenie` WHERE `class_Id` = 78780329 AND `class_Name` = 'forge-dye-ashen';
DELETE FROM `weenie` WHERE `class_Id` = 78780303 AND `class_Name` = 'npc-forge-apprentice';

-- 78780401 forge-grindstone: Grindstone (copied from retail 3110304)
DELETE FROM `weenie` WHERE `class_Id` = 78780401 AND `class_Name` = 'forge-grindstone';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780401, 'forge-grindstone', 1, '2026-10-03 12:00:00') /* Generic */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780401, 1, 1024)
     , (78780401, 5, 500)
     , (78780401, 8, 500)
     , (78780401, 16, 1)
     , (78780401, 19, 6000)
     , (78780401, 93, 24)
     , (78780401, 150, 103)
     , (78780401, 151, 24);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780401, 1, True)
     , (78780401, 22, True)
     , (78780401, 24, True)
     , (78780401, 52, True)
     , (78780401, 52001, True);

INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`)
VALUES (78780401, 39, 1);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780401, 1, 'Grindstone')
     , (78780401, 16, 'A smith''s grindstone. Stand here and use a hone stone on your weapon to hone it.');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780401, 1, 0x020000A7)
     , (78780401, 3, 0x20000014)
     , (78780401, 8, 0x06002FEB)
     , (78780401, 22, 0x3400002B);

-- 78780402 forge-dye-vat: Dye Vat (copied from retail 4383)
DELETE FROM `weenie` WHERE `class_Id` = 78780402 AND `class_Name` = 'forge-dye-vat';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780402, 'forge-dye-vat', 1, '2026-10-03 12:00:00') /* Generic */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780402, 1, 128)
     , (78780402, 5, 50)
     , (78780402, 8, 50)
     , (78780402, 9, 0)
     , (78780402, 16, 1)
     , (78780402, 19, 0)
     , (78780402, 93, 1040);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780402, 1, True)
     , (78780402, 13, False)
     , (78780402, 24, True)
     , (78780402, 52002, True);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780402, 1, 'Dye Vat')
     , (78780402, 16, 'A vat of smith''s dyes. Stand here and use a dye on your weapon to try a colour on it.');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780402, 1, 0x0200043C)
     , (78780402, 8, 0x06001066);

-- 78780410 forge-hone-stone-damage: Damage Hone Stone (copied from retail 6318)
DELETE FROM `weenie` WHERE `class_Id` = 78780410 AND `class_Name` = 'forge-hone-stone-damage';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780410, 'forge-hone-stone-damage', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780410, 1, 2048)
     , (78780410, 3, 82)
     , (78780410, 5, 5)
     , (78780410, 8, 5)
     , (78780410, 9, 0)
     , (78780410, 16, 524296)
     , (78780410, 19, 50000)
     , (78780410, 93, 1044)
     , (78780410, 94, 33025)
     , (78780410, 52004, 1)
     , (78780410, 52005, 1);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780410, 22, True)
     , (78780410, 23, True)
     , (78780410, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780410, 1, 'Damage Hone Stone')
     , (78780410, 14, 'Use this stone on a weapon while standing at a grindstone.')
     , (78780410, 16, 'A smith''s hone stone. Used on a weapon at a grindstone, it tries to hone its damage by one level. Each attempt costs pyreals; the stone is never used up and the weapon is never harmed.')
     , (78780410, 20, 'Damage Hone Stones');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780410, 1, 0x020007B7)
     , (78780410, 3, 0x20000014)
     , (78780410, 6, 0x04000BEF)
     , (78780410, 7, 0x100001FD)
     , (78780410, 8, 0x06001C1C)
     , (78780410, 22, 0x3400002B);

-- 78780411 forge-hone-stone-balance: Balance Hone Stone (copied from retail 6319)
DELETE FROM `weenie` WHERE `class_Id` = 78780411 AND `class_Name` = 'forge-hone-stone-balance';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780411, 'forge-hone-stone-balance', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780411, 1, 2048)
     , (78780411, 3, 8)
     , (78780411, 5, 5)
     , (78780411, 8, 5)
     , (78780411, 9, 0)
     , (78780411, 16, 524296)
     , (78780411, 19, 50000)
     , (78780411, 93, 1044)
     , (78780411, 94, 33025)
     , (78780411, 52004, 1)
     , (78780411, 52005, 2);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780411, 22, True)
     , (78780411, 23, True)
     , (78780411, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780411, 1, 'Balance Hone Stone')
     , (78780411, 14, 'Use this stone on a weapon while standing at a grindstone.')
     , (78780411, 16, 'A smith''s hone stone. Used on a weapon at a grindstone, it tries to hone its variance (a steadier hit) by one level. Each attempt costs pyreals; the stone is never used up and the weapon is never harmed.')
     , (78780411, 20, 'Balance Hone Stones');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780411, 1, 0x020007B7)
     , (78780411, 3, 0x20000014)
     , (78780411, 6, 0x04000BEF)
     , (78780411, 7, 0x100001FD)
     , (78780411, 8, 0x06001C1F)
     , (78780411, 22, 0x3400002B);

-- 78780412 forge-hone-stone-swiftness: Swiftness Hone Stone (copied from retail 6320)
DELETE FROM `weenie` WHERE `class_Id` = 78780412 AND `class_Name` = 'forge-hone-stone-swiftness';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780412, 'forge-hone-stone-swiftness', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780412, 1, 2048)
     , (78780412, 3, 14)
     , (78780412, 5, 5)
     , (78780412, 8, 5)
     , (78780412, 9, 0)
     , (78780412, 16, 524296)
     , (78780412, 19, 50000)
     , (78780412, 93, 1044)
     , (78780412, 94, 33025)
     , (78780412, 52004, 1)
     , (78780412, 52005, 3);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780412, 22, True)
     , (78780412, 23, True)
     , (78780412, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780412, 1, 'Swiftness Hone Stone')
     , (78780412, 14, 'Use this stone on a weapon while standing at a grindstone.')
     , (78780412, 16, 'A smith''s hone stone. Used on a weapon at a grindstone, it tries to hone its speed by one level. Each attempt costs pyreals; the stone is never used up and the weapon is never harmed.')
     , (78780412, 20, 'Swiftness Hone Stones');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780412, 1, 0x020007B7)
     , (78780412, 3, 0x20000014)
     , (78780412, 6, 0x04000BEF)
     , (78780412, 7, 0x100001FD)
     , (78780412, 8, 0x06001C20)
     , (78780412, 22, 0x3400002B);

-- 78780413 forge-hone-stone-attack: Attack Hone Stone (copied from retail 6321)
DELETE FROM `weenie` WHERE `class_Id` = 78780413 AND `class_Name` = 'forge-hone-stone-attack';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780413, 'forge-hone-stone-attack', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780413, 1, 2048)
     , (78780413, 3, 2)
     , (78780413, 5, 5)
     , (78780413, 8, 5)
     , (78780413, 9, 0)
     , (78780413, 16, 524296)
     , (78780413, 19, 50000)
     , (78780413, 93, 1044)
     , (78780413, 94, 33025)
     , (78780413, 52004, 1)
     , (78780413, 52005, 4);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780413, 22, True)
     , (78780413, 23, True)
     , (78780413, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780413, 1, 'Attack Hone Stone')
     , (78780413, 14, 'Use this stone on a weapon while standing at a grindstone.')
     , (78780413, 16, 'A smith''s hone stone. Used on a weapon at a grindstone, it tries to hone its attack modifier by one level. Each attempt costs pyreals; the stone is never used up and the weapon is never harmed.')
     , (78780413, 20, 'Attack Hone Stones');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780413, 1, 0x020007B7)
     , (78780413, 3, 0x20000014)
     , (78780413, 6, 0x04000BEF)
     , (78780413, 7, 0x100001FD)
     , (78780413, 8, 0x06001C19)
     , (78780413, 22, 0x3400002B);

-- 78780414 forge-hone-stone-melee-defense: Melee Defense Hone Stone (copied from retail 6318)
DELETE FROM `weenie` WHERE `class_Id` = 78780414 AND `class_Name` = 'forge-hone-stone-melee-defense';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780414, 'forge-hone-stone-melee-defense', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780414, 1, 2048)
     , (78780414, 3, 82)
     , (78780414, 5, 5)
     , (78780414, 8, 5)
     , (78780414, 9, 0)
     , (78780414, 16, 524296)
     , (78780414, 19, 50000)
     , (78780414, 93, 1044)
     , (78780414, 94, 33025)
     , (78780414, 52004, 1)
     , (78780414, 52005, 5);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780414, 22, True)
     , (78780414, 23, True)
     , (78780414, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780414, 1, 'Melee Defense Hone Stone')
     , (78780414, 14, 'Use this stone on a weapon while standing at a grindstone.')
     , (78780414, 16, 'A smith''s hone stone. Used on a weapon at a grindstone, it tries to hone its melee defense by one level. Each attempt costs pyreals; the stone is never used up and the weapon is never harmed.')
     , (78780414, 20, 'Melee Defense Hone Stones');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780414, 1, 0x020007B7)
     , (78780414, 3, 0x20000014)
     , (78780414, 6, 0x04000BEF)
     , (78780414, 7, 0x100001FD)
     , (78780414, 8, 0x06001C1C)
     , (78780414, 22, 0x3400002B);

-- 78780415 forge-hone-stone-missile-defense: Missile Defense Hone Stone (copied from retail 6319)
DELETE FROM `weenie` WHERE `class_Id` = 78780415 AND `class_Name` = 'forge-hone-stone-missile-defense';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780415, 'forge-hone-stone-missile-defense', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780415, 1, 2048)
     , (78780415, 3, 8)
     , (78780415, 5, 5)
     , (78780415, 8, 5)
     , (78780415, 9, 0)
     , (78780415, 16, 524296)
     , (78780415, 19, 50000)
     , (78780415, 93, 1044)
     , (78780415, 94, 33025)
     , (78780415, 52004, 1)
     , (78780415, 52005, 6);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780415, 22, True)
     , (78780415, 23, True)
     , (78780415, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780415, 1, 'Missile Defense Hone Stone')
     , (78780415, 14, 'Use this stone on a weapon while standing at a grindstone.')
     , (78780415, 16, 'A smith''s hone stone. Used on a weapon at a grindstone, it tries to hone its missile defense by one level. Each attempt costs pyreals; the stone is never used up and the weapon is never harmed.')
     , (78780415, 20, 'Missile Defense Hone Stones');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780415, 1, 0x020007B7)
     , (78780415, 3, 0x20000014)
     , (78780415, 6, 0x04000BEF)
     , (78780415, 7, 0x100001FD)
     , (78780415, 8, 0x06001C1F)
     , (78780415, 22, 0x3400002B);

-- 78780416 forge-hone-stone-magic-defense: Magic Defense Hone Stone (copied from retail 6320)
DELETE FROM `weenie` WHERE `class_Id` = 78780416 AND `class_Name` = 'forge-hone-stone-magic-defense';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780416, 'forge-hone-stone-magic-defense', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780416, 1, 2048)
     , (78780416, 3, 14)
     , (78780416, 5, 5)
     , (78780416, 8, 5)
     , (78780416, 9, 0)
     , (78780416, 16, 524296)
     , (78780416, 19, 50000)
     , (78780416, 93, 1044)
     , (78780416, 94, 33025)
     , (78780416, 52004, 1)
     , (78780416, 52005, 7);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780416, 22, True)
     , (78780416, 23, True)
     , (78780416, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780416, 1, 'Magic Defense Hone Stone')
     , (78780416, 14, 'Use this stone on a weapon while standing at a grindstone.')
     , (78780416, 16, 'A smith''s hone stone. Used on a weapon at a grindstone, it tries to hone its magic defense by one level. Each attempt costs pyreals; the stone is never used up and the weapon is never harmed.')
     , (78780416, 20, 'Magic Defense Hone Stones');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780416, 1, 0x020007B7)
     , (78780416, 3, 0x20000014)
     , (78780416, 6, 0x04000BEF)
     , (78780416, 7, 0x100001FD)
     , (78780416, 8, 0x06001C20)
     , (78780416, 22, 0x3400002B);

-- 78780417 forge-hone-stone-damage-modifier: Damage Modifier Hone Stone (copied from retail 6321)
DELETE FROM `weenie` WHERE `class_Id` = 78780417 AND `class_Name` = 'forge-hone-stone-damage-modifier';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780417, 'forge-hone-stone-damage-modifier', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780417, 1, 2048)
     , (78780417, 3, 2)
     , (78780417, 5, 5)
     , (78780417, 8, 5)
     , (78780417, 9, 0)
     , (78780417, 16, 524296)
     , (78780417, 19, 50000)
     , (78780417, 93, 1044)
     , (78780417, 94, 33025)
     , (78780417, 52004, 1)
     , (78780417, 52005, 8);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780417, 22, True)
     , (78780417, 23, True)
     , (78780417, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780417, 1, 'Damage Modifier Hone Stone')
     , (78780417, 14, 'Use this stone on a weapon while standing at a grindstone.')
     , (78780417, 16, 'A smith''s hone stone. Used on a weapon at a grindstone, it tries to hone its damage modifier (casters and missile weapons) by one level. Each attempt costs pyreals; the stone is never used up and the weapon is never harmed.')
     , (78780417, 20, 'Damage Modifier Hone Stones');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780417, 1, 0x020007B7)
     , (78780417, 3, 0x20000014)
     , (78780417, 6, 0x04000BEF)
     , (78780417, 7, 0x100001FD)
     , (78780417, 8, 0x06001C19)
     , (78780417, 22, 0x3400002B);

-- 78780418 forge-flux: Smith's Flux (copied from retail 7598)
DELETE FROM `weenie` WHERE `class_Id` = 78780418 AND `class_Name` = 'forge-flux';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780418, 'forge-flux', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780418, 1, 8388608)
     , (78780418, 3, 39)
     , (78780418, 5, 50)
     , (78780418, 8, 50)
     , (78780418, 9, 0)
     , (78780418, 11, 100)
     , (78780418, 12, 1)
     , (78780418, 15, 100000)
     , (78780418, 16, 524296)
     , (78780418, 19, 100000)
     , (78780418, 93, 1044)
     , (78780418, 94, 33025)
     , (78780418, 52004, 3)
     , (78780418, 52005, 0);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780418, 23, True)
     , (78780418, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780418, 1, 'Smith''s Flux')
     , (78780418, 14, 'Keep this in your pack: one is used each time you hone a weapon.')
     , (78780418, 16, 'A smith''s flux. Carried in your pack, one is used on each hone attempt and makes it likelier to take.')
     , (78780418, 20, 'Smith''s Flux');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780418, 1, 0x020005FD)
     , (78780418, 3, 0x20000014)
     , (78780418, 6, 0x04000BEF)
     , (78780418, 7, 0x10000166)
     , (78780418, 8, 0x06001D11)
     , (78780418, 22, 0x3400002B);

-- 78780419 forge-unbinding-oil: Unbinding Oil (copied from retail 19533)
DELETE FROM `weenie` WHERE `class_Id` = 78780419 AND `class_Name` = 'forge-unbinding-oil';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780419, 'forge-unbinding-oil', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780419, 1, 67108864)
     , (78780419, 3, 82)
     , (78780419, 5, 15)
     , (78780419, 8, 5)
     , (78780419, 9, 0)
     , (78780419, 16, 524296)
     , (78780419, 19, 250000)
     , (78780419, 93, 1044)
     , (78780419, 94, 33031)
     , (78780419, 150, 103)
     , (78780419, 151, 11)
     , (78780419, 52004, 4)
     , (78780419, 52005, 0);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780419, 22, True)
     , (78780419, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780419, 1, 'Unbinding Oil')
     , (78780419, 14, 'Use this oil on a forged or honed weapon, or a forged piece of armour, in your pack.')
     , (78780419, 16, 'A smith''s oil that loosens the binding on a forged or honed weapon or a forged piece of armour, for a price in pyreals. It can then change hands, and binds again to whoever wields it next. The oil is never used up.')
     , (78780419, 20, 'Unbinding Oils');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780419, 1, 0x020005FD)
     , (78780419, 3, 0x20000014)
     , (78780419, 6, 0x04000BEF)
     , (78780419, 7, 0x10000166)
     , (78780419, 8, 0x06002563)
     , (78780419, 22, 0x3400002B);

-- 78780420 forge-dye-smith: Smith's Dye (copied from retail 8643)
DELETE FROM `weenie` WHERE `class_Id` = 78780420 AND `class_Name` = 'forge-dye-smith';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780420, 'forge-dye-smith', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780420, 1, 67108864)
     , (78780420, 3, 90)
     , (78780420, 5, 10)
     , (78780420, 8, 5)
     , (78780420, 9, 0)
     , (78780420, 16, 524296)
     , (78780420, 19, 25000)
     , (78780420, 93, 1044)
     , (78780420, 94, 33047)
     , (78780420, 150, 103)
     , (78780420, 151, 9)
     , (78780420, 52004, 2)
     , (78780420, 52005, 0);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780420, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780420, 1, 'Smith''s Dye')
     , (78780420, 14, 'Use this dye on a weapon or a piece of armour or clothing while standing at a dye vat. Use it on yourself to dye everything you are wearing the same colour.')
     , (78780420, 16, 'A smith''s dye. Used at a dye vat on a weapon, armour or clothing, it tries any colour on it. Used on yourself, it tries the colour on everything you wear. Keeping the colour costs pyreals per piece; trying is free, and the dye is never used up.')
     , (78780420, 20, 'Smith''s Dyes');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780420, 1, 0x0200090F)
     , (78780420, 3, 0x20000014)
     , (78780420, 6, 0x04000BEF)
     , (78780420, 7, 0x10000242)
     , (78780420, 8, 0x06001DED)
     , (78780420, 22, 0x3400002B);

-- 78780421 forge-dye-crimson: Crimson Dye (copied from retail 8643)
DELETE FROM `weenie` WHERE `class_Id` = 78780421 AND `class_Name` = 'forge-dye-crimson';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780421, 'forge-dye-crimson', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780421, 1, 67108864)
     , (78780421, 3, 14)
     , (78780421, 5, 10)
     , (78780421, 8, 5)
     , (78780421, 9, 0)
     , (78780421, 16, 524296)
     , (78780421, 19, 100000)
     , (78780421, 93, 1044)
     , (78780421, 94, 33047)
     , (78780421, 150, 103)
     , (78780421, 151, 9)
     , (78780421, 52004, 2)
     , (78780421, 52005, 1);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780421, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780421, 1, 'Crimson Dye')
     , (78780421, 14, 'Use this dye on a weapon or a piece of armour or clothing while standing at a dye vat. Use it on yourself to dye everything you are wearing the same colour.')
     , (78780421, 16, 'A smith''s dye. Used at a dye vat on a weapon, armour or clothing, it tries a red on it. Used on yourself, it tries the colour on everything you wear. Keeping the colour costs pyreals per piece; trying is free, and the dye is never used up.')
     , (78780421, 20, 'Crimson Dyes');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780421, 1, 0x0200090F)
     , (78780421, 3, 0x20000014)
     , (78780421, 6, 0x04000BEF)
     , (78780421, 7, 0x10000242)
     , (78780421, 8, 0x06001DE6)
     , (78780421, 22, 0x3400002B);

-- 78780422 forge-dye-amber: Amber Dye (copied from retail 8643)
DELETE FROM `weenie` WHERE `class_Id` = 78780422 AND `class_Name` = 'forge-dye-amber';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780422, 'forge-dye-amber', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780422, 1, 67108864)
     , (78780422, 3, 4)
     , (78780422, 5, 10)
     , (78780422, 8, 5)
     , (78780422, 9, 0)
     , (78780422, 16, 524296)
     , (78780422, 19, 100000)
     , (78780422, 93, 1044)
     , (78780422, 94, 33047)
     , (78780422, 150, 103)
     , (78780422, 151, 9)
     , (78780422, 52004, 2)
     , (78780422, 52005, 2);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780422, 69, False)
     , (78780422, 84, True);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780422, 1, 'Amber Dye')
     , (78780422, 14, 'Use this dye on a weapon or a piece of armour or clothing while standing at a dye vat. Use it on yourself to dye everything you are wearing the same colour.')
     , (78780422, 16, 'A smith''s dye. Used at a dye vat on a weapon, armour or clothing, it tries an orange on it. Used on yourself, it tries the colour on everything you wear. Keeping the colour costs pyreals per piece; trying is free, and the dye is never used up.')
     , (78780422, 20, 'Amber Dyes');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780422, 1, 0x0200090F)
     , (78780422, 3, 0x20000014)
     , (78780422, 6, 0x04000BEF)
     , (78780422, 7, 0x10000242)
     , (78780422, 8, 0x060061C1)
     , (78780422, 22, 0x3400002B);

-- 78780423 forge-dye-golden: Golden Dye (copied from retail 8643)
DELETE FROM `weenie` WHERE `class_Id` = 78780423 AND `class_Name` = 'forge-dye-golden';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780423, 'forge-dye-golden', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780423, 1, 67108864)
     , (78780423, 3, 17)
     , (78780423, 5, 10)
     , (78780423, 8, 5)
     , (78780423, 9, 0)
     , (78780423, 16, 524296)
     , (78780423, 19, 100000)
     , (78780423, 93, 1044)
     , (78780423, 94, 33047)
     , (78780423, 150, 103)
     , (78780423, 151, 9)
     , (78780423, 52004, 2)
     , (78780423, 52005, 3);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780423, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780423, 1, 'Golden Dye')
     , (78780423, 14, 'Use this dye on a weapon or a piece of armour or clothing while standing at a dye vat. Use it on yourself to dye everything you are wearing the same colour.')
     , (78780423, 16, 'A smith''s dye. Used at a dye vat on a weapon, armour or clothing, it tries a yellow on it. Used on yourself, it tries the colour on everything you wear. Keeping the colour costs pyreals per piece; trying is free, and the dye is never used up.')
     , (78780423, 20, 'Golden Dyes');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780423, 1, 0x0200090F)
     , (78780423, 3, 0x20000014)
     , (78780423, 6, 0x04000BEF)
     , (78780423, 7, 0x10000242)
     , (78780423, 8, 0x06001DE7)
     , (78780423, 22, 0x3400002B);

-- 78780424 forge-dye-verdant: Verdant Dye (copied from retail 8643)
DELETE FROM `weenie` WHERE `class_Id` = 78780424 AND `class_Name` = 'forge-dye-verdant';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780424, 'forge-dye-verdant', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780424, 1, 67108864)
     , (78780424, 3, 8)
     , (78780424, 5, 10)
     , (78780424, 8, 5)
     , (78780424, 9, 0)
     , (78780424, 16, 524296)
     , (78780424, 19, 100000)
     , (78780424, 93, 1044)
     , (78780424, 94, 33047)
     , (78780424, 150, 103)
     , (78780424, 151, 9)
     , (78780424, 52004, 2)
     , (78780424, 52005, 4);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780424, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780424, 1, 'Verdant Dye')
     , (78780424, 14, 'Use this dye on a weapon or a piece of armour or clothing while standing at a dye vat. Use it on yourself to dye everything you are wearing the same colour.')
     , (78780424, 16, 'A smith''s dye. Used at a dye vat on a weapon, armour or clothing, it tries a green on it. Used on yourself, it tries the colour on everything you wear. Keeping the colour costs pyreals per piece; trying is free, and the dye is never used up.')
     , (78780424, 20, 'Verdant Dyes');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780424, 1, 0x0200090F)
     , (78780424, 3, 0x20000014)
     , (78780424, 6, 0x04000BEF)
     , (78780424, 7, 0x10000242)
     , (78780424, 8, 0x06001DE8)
     , (78780424, 22, 0x3400002B);

-- 78780425 forge-dye-teal: Teal Dye (copied from retail 8643)
DELETE FROM `weenie` WHERE `class_Id` = 78780425 AND `class_Name` = 'forge-dye-teal';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780425, 'forge-dye-teal', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780425, 1, 67108864)
     , (78780425, 3, 77)
     , (78780425, 5, 10)
     , (78780425, 8, 5)
     , (78780425, 9, 0)
     , (78780425, 16, 524296)
     , (78780425, 19, 100000)
     , (78780425, 93, 1044)
     , (78780425, 94, 33047)
     , (78780425, 150, 103)
     , (78780425, 151, 9)
     , (78780425, 52004, 2)
     , (78780425, 52005, 5);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780425, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780425, 1, 'Teal Dye')
     , (78780425, 14, 'Use this dye on a weapon or a piece of armour or clothing while standing at a dye vat. Use it on yourself to dye everything you are wearing the same colour.')
     , (78780425, 16, 'A smith''s dye. Used at a dye vat on a weapon, armour or clothing, it tries a teal on it. Used on yourself, it tries the colour on everything you wear. Keeping the colour costs pyreals per piece; trying is free, and the dye is never used up.')
     , (78780425, 20, 'Teal Dyes');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780425, 1, 0x0200090F)
     , (78780425, 3, 0x20000014)
     , (78780425, 6, 0x04000BEF)
     , (78780425, 7, 0x10000242)
     , (78780425, 8, 0x06001DEE)
     , (78780425, 22, 0x3400002B);

-- 78780426 forge-dye-azure: Azure Dye (copied from retail 8643)
DELETE FROM `weenie` WHERE `class_Id` = 78780426 AND `class_Name` = 'forge-dye-azure';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780426, 'forge-dye-azure', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780426, 1, 67108864)
     , (78780426, 3, 2)
     , (78780426, 5, 10)
     , (78780426, 8, 5)
     , (78780426, 9, 0)
     , (78780426, 16, 524296)
     , (78780426, 19, 100000)
     , (78780426, 93, 1044)
     , (78780426, 94, 33047)
     , (78780426, 150, 103)
     , (78780426, 151, 9)
     , (78780426, 52004, 2)
     , (78780426, 52005, 6);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780426, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780426, 1, 'Azure Dye')
     , (78780426, 14, 'Use this dye on a weapon or a piece of armour or clothing while standing at a dye vat. Use it on yourself to dye everything you are wearing the same colour.')
     , (78780426, 16, 'A smith''s dye. Used at a dye vat on a weapon, armour or clothing, it tries a blue on it. Used on yourself, it tries the colour on everything you wear. Keeping the colour costs pyreals per piece; trying is free, and the dye is never used up.')
     , (78780426, 20, 'Azure Dyes');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780426, 1, 0x0200090F)
     , (78780426, 3, 0x20000014)
     , (78780426, 6, 0x04000BEF)
     , (78780426, 7, 0x10000242)
     , (78780426, 8, 0x06001DE9)
     , (78780426, 22, 0x3400002B);

-- 78780427 forge-dye-violet: Violet Dye (copied from retail 8643)
DELETE FROM `weenie` WHERE `class_Id` = 78780427 AND `class_Name` = 'forge-dye-violet';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780427, 'forge-dye-violet', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780427, 1, 67108864)
     , (78780427, 3, 13)
     , (78780427, 5, 10)
     , (78780427, 8, 5)
     , (78780427, 9, 0)
     , (78780427, 16, 524296)
     , (78780427, 19, 100000)
     , (78780427, 93, 1044)
     , (78780427, 94, 33047)
     , (78780427, 150, 103)
     , (78780427, 151, 9)
     , (78780427, 52004, 2)
     , (78780427, 52005, 7);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780427, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780427, 1, 'Violet Dye')
     , (78780427, 14, 'Use this dye on a weapon or a piece of armour or clothing while standing at a dye vat. Use it on yourself to dye everything you are wearing the same colour.')
     , (78780427, 16, 'A smith''s dye. Used at a dye vat on a weapon, armour or clothing, it tries a violet on it. Used on yourself, it tries the colour on everything you wear. Keeping the colour costs pyreals per piece; trying is free, and the dye is never used up.')
     , (78780427, 20, 'Violet Dyes');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780427, 1, 0x0200090F)
     , (78780427, 3, 0x20000014)
     , (78780427, 6, 0x04000BEF)
     , (78780427, 7, 0x10000242)
     , (78780427, 8, 0x06001DEB)
     , (78780427, 22, 0x3400002B);

-- 78780428 forge-dye-rose: Rose Dye (copied from retail 8643)
DELETE FROM `weenie` WHERE `class_Id` = 78780428 AND `class_Name` = 'forge-dye-rose';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780428, 'forge-dye-rose', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780428, 1, 67108864)
     , (78780428, 3, 14)
     , (78780428, 5, 10)
     , (78780428, 8, 5)
     , (78780428, 9, 0)
     , (78780428, 16, 524296)
     , (78780428, 19, 100000)
     , (78780428, 93, 1044)
     , (78780428, 94, 33047)
     , (78780428, 150, 103)
     , (78780428, 151, 9)
     , (78780428, 52004, 2)
     , (78780428, 52005, 8);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780428, 69, False)
     , (78780428, 84, True);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780428, 1, 'Rose Dye')
     , (78780428, 14, 'Use this dye on a weapon or a piece of armour or clothing while standing at a dye vat. Use it on yourself to dye everything you are wearing the same colour.')
     , (78780428, 16, 'A smith''s dye. Used at a dye vat on a weapon, armour or clothing, it tries a pink on it. Used on yourself, it tries the colour on everything you wear. Keeping the colour costs pyreals per piece; trying is free, and the dye is never used up.')
     , (78780428, 20, 'Rose Dyes');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780428, 1, 0x0200090F)
     , (78780428, 3, 0x20000014)
     , (78780428, 6, 0x04000BEF)
     , (78780428, 7, 0x10000242)
     , (78780428, 8, 0x0600690E)
     , (78780428, 22, 0x3400002B);

-- 78780429 forge-dye-ashen: Ashen Dye (copied from retail 8643)
DELETE FROM `weenie` WHERE `class_Id` = 78780429 AND `class_Name` = 'forge-dye-ashen';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780429, 'forge-dye-ashen', 44, '2026-10-03 12:00:00') /* CraftTool */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780429, 1, 67108864)
     , (78780429, 3, 9)
     , (78780429, 5, 10)
     , (78780429, 8, 5)
     , (78780429, 9, 0)
     , (78780429, 16, 524296)
     , (78780429, 19, 100000)
     , (78780429, 93, 1044)
     , (78780429, 94, 33047)
     , (78780429, 150, 103)
     , (78780429, 151, 9)
     , (78780429, 52004, 2)
     , (78780429, 52005, 9);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780429, 69, False);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780429, 1, 'Ashen Dye')
     , (78780429, 14, 'Use this dye on a weapon or a piece of armour or clothing while standing at a dye vat. Use it on yourself to dye everything you are wearing the same colour.')
     , (78780429, 16, 'A smith''s dye. Used at a dye vat on a weapon, armour or clothing, it tries a grey on it. Used on yourself, it tries the colour on everything you wear. Keeping the colour costs pyreals per piece; trying is free, and the dye is never used up.')
     , (78780429, 20, 'Ashen Dyes');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780429, 1, 0x0200090F)
     , (78780429, 3, 0x20000014)
     , (78780429, 6, 0x04000BEF)
     , (78780429, 7, 0x10000242)
     , (78780429, 8, 0x06001DEC)
     , (78780429, 22, 0x3400002B);

-- 78780403 npc-forge-apprentice: Mei the Smith's Apprentice, vendor of every forge tool (copied from retail 835)
DELETE FROM `weenie` WHERE `class_Id` = 78780403 AND `class_Name` = 'npc-forge-apprentice';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780403, 'npc-forge-apprentice', 12, '2026-10-03 12:00:00') /* Vendor */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780403, 1, 16)
     , (78780403, 2, 31)
     , (78780403, 6, -1)
     , (78780403, 7, -1)
     , (78780403, 8, 120)
     , (78780403, 16, 32)
     , (78780403, 25, 6)
     , (78780403, 27, 0)
     , (78780403, 74, 0)
     , (78780403, 75, 0)
     , (78780403, 76, 1000000)
     , (78780403, 93, 2098200)
     , (78780403, 126, 1000)
     , (78780403, 127, 500)
     , (78780403, 133, 4)
     , (78780403, 134, 16)
     , (78780403, 146, 108);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780403, 1, True)
     , (78780403, 12, True)
     , (78780403, 13, False)
     , (78780403, 19, False)
     , (78780403, 39, True)
     , (78780403, 41, True);

INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`)
VALUES (78780403, 1, 5)
     , (78780403, 2, 0)
     , (78780403, 3, 0.16)
     , (78780403, 4, 5)
     , (78780403, 5, 1)
     , (78780403, 11, 300)
     , (78780403, 13, 0.9)
     , (78780403, 14, 1)
     , (78780403, 15, 1.1)
     , (78780403, 16, 0.4)
     , (78780403, 17, 0.4)
     , (78780403, 18, 1)
     , (78780403, 19, 0.6)
     , (78780403, 37, 0.9)
     , (78780403, 38, 1)
     , (78780403, 54, 3)
     , (78780403, 64, 1)
     , (78780403, 65, 1)
     , (78780403, 66, 1)
     , (78780403, 67, 1)
     , (78780403, 68, 1)
     , (78780403, 69, 1)
     , (78780403, 70, 1)
     , (78780403, 71, 1)
     , (78780403, 72, 1)
     , (78780403, 73, 1)
     , (78780403, 74, 1)
     , (78780403, 75, 1)
     , (78780403, 104, 10)
     , (78780403, 125, 1);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780403, 1, 'Mei the Smith''s Apprentice')
     , (78780403, 3, 'Female')
     , (78780403, 4, 'Sho')
     , (78780403, 5, 'Smith''s Apprentice');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780403, 1, 0x0200004E)
     , (78780403, 2, 0x09000001)
     , (78780403, 3, 0x20000002)
     , (78780403, 4, 0x30000000)
     , (78780403, 8, 0x06001036);

INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`)
VALUES (78780403, 1, 55, 0, 0)
     , (78780403, 2, 65, 0, 0)
     , (78780403, 3, 50, 0, 0)
     , (78780403, 4, 50, 0, 0)
     , (78780403, 5, 35, 0, 0)
     , (78780403, 6, 25, 0, 0);

INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`)
VALUES (78780403, 1, 95, 0, 0, 128)
     , (78780403, 3, 100, 0, 0, 165)
     , (78780403, 5, 30, 0, 0, 55);

INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`)
VALUES (78780403, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0.33, 0, 0, 0.33, 0, 0, 0.33, 0, 0, 0.33, 0, 0)
     , (78780403, 1, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0.44, 0.17, 0, 0.44, 0.17, 0, 0.44, 0.17, 0, 0.44, 0.17, 0)
     , (78780403, 2, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0.17, 0, 0, 0.17, 0, 0, 0.17, 0, 0, 0.17, 0)
     , (78780403, 3, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0.23, 0.03, 0, 0.23, 0.03, 0, 0.23, 0.03, 0, 0.23, 0.03, 0)
     , (78780403, 4, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0.3, 0, 0, 0.3, 0, 0, 0.3, 0, 0, 0.3, 0)
     , (78780403, 5, 4, 2, 0.75, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0.2, 0, 0, 0.2, 0, 0, 0.2, 0, 0, 0.2, 0)
     , (78780403, 6, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0.13, 0.18, 0, 0.13, 0.18, 0, 0.13, 0.18, 0, 0.13, 0.18)
     , (78780403, 7, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0.6, 0, 0, 0.6, 0, 0, 0.6, 0, 0, 0.6)
     , (78780403, 8, 4, 2, 0.75, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0.22, 0, 0, 0.22, 0, 0, 0.22, 0, 0, 0.22);

INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`)
VALUES (78780403, 2, 303, 0, 0, 0, False)
     , (78780403, 2, 2596, 0, 13, 0.5, False)
     , (78780403, 2, 2602, 0, 9, 1, False)
     , (78780403, 2, 132, 0, 5, 0, False)
     , (78780403, 2, 10696, 0, 15, 1, False)
     , (78780403, 4, 78780410, -1, 0, 0, False)
     , (78780403, 4, 78780411, -1, 0, 0, False)
     , (78780403, 4, 78780412, -1, 0, 0, False)
     , (78780403, 4, 78780413, -1, 0, 0, False)
     , (78780403, 4, 78780414, -1, 0, 0, False)
     , (78780403, 4, 78780415, -1, 0, 0, False)
     , (78780403, 4, 78780416, -1, 0, 0, False)
     , (78780403, 4, 78780417, -1, 0, 0, False)
     , (78780403, 4, 78780418, -1, 0, 0, False)
     , (78780403, 4, 78780419, -1, 0, 0, False)
     , (78780403, 4, 78780420, -1, 0, 0, False)
     , (78780403, 4, 78780421, -1, 0, 0, False)
     , (78780403, 4, 78780422, -1, 0, 0, False)
     , (78780403, 4, 78780423, -1, 0, 0, False)
     , (78780403, 4, 78780424, -1, 0, 0, False)
     , (78780403, 4, 78780425, -1, 0, 0, False)
     , (78780403, 4, 78780426, -1, 0, 0, False)
     , (78780403, 4, 78780427, -1, 0, 0, False)
     , (78780403, 4, 78780428, -1, 0, 0, False)
     , (78780403, 4, 78780429, -1, 0, 0, False);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780403, 2, 0.8, NULL, NULL, NULL, NULL, 1, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 10, 0, 1, NULL, 'Welcome! What''s your pleasure today?', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780403, 2, 0.8, NULL, NULL, NULL, NULL, 2, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 10, 0, 1, NULL, 'Thank you for your business. Please return soon.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780403, 2, 0.8, NULL, NULL, NULL, NULL, 3, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 10, 0, 1, NULL, 'You drive a hard bargain, my friend.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780403, 2, 0.8, NULL, NULL, NULL, NULL, 4, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 10, 0, 1, NULL, 'An excellent purchase.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780403, 2, 0.125, NULL, NULL, NULL, NULL, 5, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 5, 0, 1, 318767239, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780403, 2, 0.25, NULL, NULL, NULL, NULL, 5, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 5, 0, 1, 318767229, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780403, 2, 0.375, NULL, NULL, NULL, NULL, 5, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 5, 0, 1, 318767238, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780403, 2, 0.5, NULL, NULL, NULL, NULL, 5, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 5, 0, 1, 318767235, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

-- 78780404 npc-forge-dyer: Sorrel the Dyer, vendor of the ten dyes only (copied from retail 835)
DELETE FROM `weenie` WHERE `class_Id` = 78780404 AND `class_Name` = 'npc-forge-dyer';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780404, 'npc-forge-dyer', 12, '2026-10-03 12:00:00') /* Vendor */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780404, 1, 16)
     , (78780404, 2, 31)
     , (78780404, 6, -1)
     , (78780404, 7, -1)
     , (78780404, 8, 120)
     , (78780404, 16, 32)
     , (78780404, 25, 6)
     , (78780404, 27, 0)
     , (78780404, 74, 0)
     , (78780404, 75, 0)
     , (78780404, 76, 1000000)
     , (78780404, 93, 2098200)
     , (78780404, 126, 1000)
     , (78780404, 127, 500)
     , (78780404, 133, 4)
     , (78780404, 134, 16)
     , (78780404, 146, 108);

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780404, 1, True)
     , (78780404, 12, True)
     , (78780404, 13, False)
     , (78780404, 19, False)
     , (78780404, 39, True)
     , (78780404, 41, True);

INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`)
VALUES (78780404, 1, 5)
     , (78780404, 2, 0)
     , (78780404, 3, 0.16)
     , (78780404, 4, 5)
     , (78780404, 5, 1)
     , (78780404, 11, 300)
     , (78780404, 13, 0.9)
     , (78780404, 14, 1)
     , (78780404, 15, 1.1)
     , (78780404, 16, 0.4)
     , (78780404, 17, 0.4)
     , (78780404, 18, 1)
     , (78780404, 19, 0.6)
     , (78780404, 37, 0.9)
     , (78780404, 38, 1)
     , (78780404, 54, 3)
     , (78780404, 64, 1)
     , (78780404, 65, 1)
     , (78780404, 66, 1)
     , (78780404, 67, 1)
     , (78780404, 68, 1)
     , (78780404, 69, 1)
     , (78780404, 70, 1)
     , (78780404, 71, 1)
     , (78780404, 72, 1)
     , (78780404, 73, 1)
     , (78780404, 74, 1)
     , (78780404, 75, 1)
     , (78780404, 104, 10)
     , (78780404, 125, 1);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780404, 1, 'Sorrel the Dyer')
     , (78780404, 3, 'Female')
     , (78780404, 4, 'Sho')
     , (78780404, 5, 'Dyer');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780404, 1, 0x0200004E)
     , (78780404, 2, 0x09000001)
     , (78780404, 3, 0x20000002)
     , (78780404, 4, 0x30000000)
     , (78780404, 8, 0x06001036);

INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`)
VALUES (78780404, 1, 55, 0, 0)
     , (78780404, 2, 65, 0, 0)
     , (78780404, 3, 50, 0, 0)
     , (78780404, 4, 50, 0, 0)
     , (78780404, 5, 35, 0, 0)
     , (78780404, 6, 25, 0, 0);

INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`)
VALUES (78780404, 1, 95, 0, 0, 128)
     , (78780404, 3, 100, 0, 0, 165)
     , (78780404, 5, 30, 0, 0, 55);

INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`)
VALUES (78780404, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0.33, 0, 0, 0.33, 0, 0, 0.33, 0, 0, 0.33, 0, 0)
     , (78780404, 1, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0.44, 0.17, 0, 0.44, 0.17, 0, 0.44, 0.17, 0, 0.44, 0.17, 0)
     , (78780404, 2, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0.17, 0, 0, 0.17, 0, 0, 0.17, 0, 0, 0.17, 0)
     , (78780404, 3, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0.23, 0.03, 0, 0.23, 0.03, 0, 0.23, 0.03, 0, 0.23, 0.03, 0)
     , (78780404, 4, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0.3, 0, 0, 0.3, 0, 0, 0.3, 0, 0, 0.3, 0)
     , (78780404, 5, 4, 2, 0.75, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0.2, 0, 0, 0.2, 0, 0, 0.2, 0, 0, 0.2, 0)
     , (78780404, 6, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0.13, 0.18, 0, 0.13, 0.18, 0, 0.13, 0.18, 0, 0.13, 0.18)
     , (78780404, 7, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0.6, 0, 0, 0.6, 0, 0, 0.6, 0, 0, 0.6)
     , (78780404, 8, 4, 2, 0.75, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0.22, 0, 0, 0.22, 0, 0, 0.22, 0, 0, 0.22);

INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`)
VALUES (78780404, 2, 303, 0, 0, 0, False)
     , (78780404, 2, 2596, 0, 13, 0.5, False)
     , (78780404, 2, 2602, 0, 9, 1, False)
     , (78780404, 2, 132, 0, 5, 0, False)
     , (78780404, 2, 10696, 0, 15, 1, False)
     , (78780404, 4, 78780420, -1, 0, 0, False)
     , (78780404, 4, 78780421, -1, 0, 0, False)
     , (78780404, 4, 78780422, -1, 0, 0, False)
     , (78780404, 4, 78780423, -1, 0, 0, False)
     , (78780404, 4, 78780424, -1, 0, 0, False)
     , (78780404, 4, 78780425, -1, 0, 0, False)
     , (78780404, 4, 78780426, -1, 0, 0, False)
     , (78780404, 4, 78780427, -1, 0, 0, False)
     , (78780404, 4, 78780428, -1, 0, 0, False)
     , (78780404, 4, 78780429, -1, 0, 0, False);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780404, 2, 0.8, NULL, NULL, NULL, NULL, 1, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 10, 0, 1, NULL, 'Welcome! What''s your pleasure today?', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780404, 2, 0.8, NULL, NULL, NULL, NULL, 2, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 10, 0, 1, NULL, 'Thank you for your business. Please return soon.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780404, 2, 0.8, NULL, NULL, NULL, NULL, 3, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 10, 0, 1, NULL, 'You drive a hard bargain, my friend.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780404, 2, 0.8, NULL, NULL, NULL, NULL, 4, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 10, 0, 1, NULL, 'An excellent purchase.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780404, 2, 0.125, NULL, NULL, NULL, NULL, 5, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 5, 0, 1, 318767239, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780404, 2, 0.25, NULL, NULL, NULL, NULL, 5, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 5, 0, 1, 318767229, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780404, 2, 0.375, NULL, NULL, NULL, NULL, 5, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 5, 0, 1, 318767238, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (78780404, 2, 0.5, NULL, NULL, NULL, NULL, 5, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 5, 0, 1, 318767235, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
