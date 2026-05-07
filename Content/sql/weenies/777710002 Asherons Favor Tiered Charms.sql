-- ============================================================
-- Greater Asheron's Favor (Tier 2) — WCID 777710002
-- Greater Asheron's Favor (+15% Health, +75 Natural Armor)
-- ============================================================

DELETE FROM `weenie` WHERE `class_Id` = 777710002;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777710002, 'ilt_asheronsfavorcharm_level2', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777710002,    11, 1)
     , (777710002,    13, 1)
     , (777710002,    14, 1)
     , (777710002,    63, 1)
     , (777710002, 50000, 1);

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777710002,     1, 2048)
     , (777710002,     5,    5)
     , (777710002,     8,    5)
     , (777710002,    16,    8)
     , (777710002,    19,    1)
     , (777710002,    33,    1)
     , (777710002,    83,    2)
     , (777710002,    93, 1044)
     , (777710002,   114,    1)
     , (777710002, 50000,   17) /* CharmGrantsAbility - 17 */
     , (777710002, 50005,    2); /* CharmLevel - 2 */

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777710002,    1, 33558517)
     , (777710002,    3, 536870932)
     , (777710002,    8, 100691356)
     , (777710002,   48, 100676435)
     , (777710002,   50, 100667551); /* IconOverlay - Tier 2 */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777710002,  1, 'Greater Asheron''s Favor')
     , (777710002, 14, '[Tier 2] The twin blessings of Dereth''s greatest protectors burn brighter within this charm. Double-click to activate. While held, your maximum Health is increased by 15% and your Natural Armor is hardened by 100 points through the combined will of Asheron and Antius Blackmoor.');

-- ============================================================
-- Asheron's Blessing (Tier 3) — WCID 777720002
-- Asheron's Blessing (+20% Health, +100 Natural Armor)
-- ============================================================

DELETE FROM `weenie` WHERE `class_Id` = 777720002;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777720002, 'ilt_asheronsfavorcharm_level3', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777720002,    11, 1)
     , (777720002,    13, 1)
     , (777720002,    14, 1)
     , (777720002,    63, 1)
     , (777720002, 50000, 1);

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777720002,     1, 2048)
     , (777720002,     5,    5)
     , (777720002,     8,    5)
     , (777720002,    16,    8)
     , (777720002,    19,    1)
     , (777720002,    33,    1)
     , (777720002,    83,    2)
     , (777720002,    93, 1044)
     , (777720002,   114,    1)
     , (777720002, 50000,   17) /* CharmGrantsAbility - 17 */
     , (777720002, 50005,    3); /* CharmLevel - 3 */

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777720002,    1, 33558517)
     , (777720002,    3, 536870932)
     , (777720002,    8, 100691356)
     , (777720002,   48, 100676435)
     , (777720002,   50, 100667552); /* IconOverlay - Tier 3 */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777720002,  1, 'Asheron''s Blessing')
     , (777720002, 14, '[Tier 3] The fullest expression of Asheron''s grace upon a mortal soul. Double-click to activate. While held, your maximum Health swells by 20% and your flesh is fortified by 250 points of Natural Armor — the combined legacy of two of the mightiest figures in Dereth''s history, sustained without end.');
