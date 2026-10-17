-- Jail Escape trial: everything the world database needs, in one file. Safe to re-run.
--
--   1. the cooldown quest, the Trial Master (787800400) and the Escape Portal (787800401) weenies
--   2. the Trial Master placed in the jail, 0x016C0154 [8.88 -49.11] (where it stands on the dev server)
--   3. the Escape Portal in each trial chamber, landblock 0x00C0 variations 2-10, where the Emissary of Asheron
--      stands in the retail copy: 0x00C0019D [80.55 -96.13], about 1860 m of corridor from the start 0x0242
--   4. clean-up: the old light-marker weenie (787800500) and its placements, and two Dispel All Traps (19853501)
--      that sat in variation 2 only, on the final approach to the exit
--
-- Nothing else is placed: trial chambers are empty variations, and the light effects are played by code.
-- Replaces Content/sql/weenies/custom_features_setup.sql for the trial; do not run that file's old marker
-- and copy steps after this one.
-- The Trial Master keeps its dev-server GUID 0x7016CE03 and the portals use 0x700C0800 + (variation - 2) * 0x40.

-- 0. GUID check, before anything changes. If any of those GUIDs belongs to some other object on this server,
--    this statement fails with "Subquery returns more than 1 row": stop there and pick free GUIDs for the
--    colliding rows (MySQL Workbench keeps going after an error, so run this one first and read its result).
--    It returns 'no GUID collisions' when the file is safe to run.
SELECT IF(COUNT(*) = 0, 'no GUID collisions', (SELECT 1 UNION ALL SELECT 2)) AS guid_check
FROM `landblock_instance`
WHERE `guid` IN (0x7016CE03, 0x700C0800, 0x700C0840, 0x700C0880, 0x700C08C0, 0x700C0900, 0x700C0940, 0x700C0980, 0x700C09C0, 0x700C0A00)
  AND NOT (`weenie_Class_Id` = 787800400 AND `guid` = 0x7016CE03)
  AND NOT (`weenie_Class_Id` = 787800401 AND `landblock` = 192 AND `variation_Id` BETWEEN 2 AND 10);


-- 1. Quest Cooldown Definition (Corrected Schema Columns Casing)
DELETE FROM `quest` WHERE `name` = 'jail_escape_cooldown';
INSERT INTO `quest` (`name`, `min_Delta`, `max_Solves`, `message`, `last_Modified`)
VALUES ('jail_escape_cooldown', 7200, -1, 'Jail Escape Trial Cooldown', NOW());

-- 2. Entry NPC (WCID: 787800400)
DELETE FROM `weenie` WHERE `class_Id` = 787800400;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) 
VALUES (787800400, 'jail_escape_entry_npc', 10, NOW()); -- Type 10 = Creature

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES 
(787800400, 1, 16),      -- ItemType = 16 (Creature)
(787800400, 2, 31),      -- CreatureType = 31 (Humanoid)
(787800400, 6, -1),      -- ItemsCapacity = -1
(787800400, 7, -1),      -- ContainersCapacity = -1
(787800400, 16, 32),     -- ItemUseable = 32 (Usable/Talkable)
(787800400, 25, 575),    -- Level = 575
(787800400, 93, 6292504),-- PhysicsState = 6292504 (Oliver Standard)
(787800400, 95, 8),      -- NpcSpeechInterval = 8
(787800400, 113, 1),     -- AiType = 1
(787800400, 133, 4),     -- ShowableOnRadar = 4
(787800400, 134, 16),    -- PlayerKillingStatus = 16
(787800400, 188, 1);     -- VisualDesc = 1

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES 
(787800400, 1, True),    -- Stuck = True (Static spawn)
(787800400, 8, True),    -- AllowGive = True
(787800400, 11, True),   -- IgnoreCollisions = True
(787800400, 12, True),   -- ReportCollisions = True
(787800400, 13, False),  -- Ethereal = False
(787800400, 14, True),   -- GravityStatus = True
(787800400, 19, False),  -- Attackable = False
(787800400, 41, True),   -- ReportCollisionsAsEnvironment = True
(787800400, 42, True);   -- AllowEdgeSlide = True

INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(787800400, 39, 1.33),   -- DefaultScale = 1.33
(787800400, 54, 3.0);    -- UseRadius = 3.0

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES 
(787800400, 1, 'Trial Master'),
(787800400, 5, 'Jail Escape Coordinator'); -- Title/ShortDesc

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES 
(787800400, 1, 33554433),  -- Setup DID = 33554433 (0x02000001)
(787800400, 2, 150994945), -- MotionTable = 150994945 (0x09000001)
(787800400, 3, 536870913), -- SoundTable = 536870913 (0x20000001)
(787800400, 6, 67108990),  -- PaletteBase = 67108990 (Texture palette)
(787800400, 8, 100667446); -- Icon = 100667446

INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`) VALUES 
(787800400, 1, 500, 0, 0), -- Strength
(787800400, 2, 500, 0, 0), -- Endurance
(787800400, 3, 400, 0, 0), -- Quickness
(787800400, 4, 550, 0, 0), -- Coordination
(787800400, 5, 600, 0, 0), -- Focus
(787800400, 6, 600, 0, 0); -- Self

INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`) VALUES 
(787800400, 1, 0, 0, 0, 750),    -- MaxHealth
(787800400, 3, 10, 0, 0, 1000),  -- MaxStamina
(787800400, 5, 0, 0, 0, 1250);   -- MaxMana

-- 3. Exit Portal (WCID: 787800401)
DELETE FROM `weenie` WHERE `class_Id` = 787800401;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) 
VALUES (787800401, 'jail_escape_exit_portal', 7, NOW()); -- Type 7 = Portal

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES 
(787800401, 1, 65536),    -- ItemType = 65536 (Portal)
(787800401, 16, 32),     -- ItemUseable = 32 (Usable/Talkable)
(787800401, 93, 3084),   -- PhysicsState = 3084 (Ethereal + ReportCollisions + LightingOn + ParticleEmitter)
(787800401, 111, 1),     -- AppraisalUsefulness = 1
(787800401, 133, 4),     -- ShowableOnRadar = 4
(787800401, 150, 3);     -- DefaultClickAction = 3 (Use/Activate)

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES 
(787800401, 1, True),    -- Stuck = True (Static spawn)
(787800401, 11, False),  -- IgnoreCollisions = False
(787800401, 12, True),   -- ReportCollisions = True
(787800401, 13, True),   -- Ethereal = True
(787800401, 15, True),   -- LightsStatus = True
(787800401, 63, True);   -- Invincible = True

INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(787800401, 54, -0.1);   -- UseRadius = -0.1 (Portal Standard)

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES 
(787800401, 1, 'Escape Portal'),
(787800401, 14, 'Double click this portal to complete the trial and escape jail!');

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES 
(787800401, 1, 0x02000004),  -- Setup DID (Purple Portal Visual Effect)
(787800401, 2, 150994947),   -- MotionTable (Standard Portal motion table)
(787800401, 8, 100667499);   -- Icon

-- 2. Trial Master in the jail
DELETE FROM `landblock_instance` WHERE `weenie_Class_Id` = 787800400;
INSERT INTO `landblock_instance` (`guid`, `weenie_Class_Id`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`,
    `angles_W`, `angles_X`, `angles_Y`, `angles_Z`, `is_Link_Child`, `last_Modified`, `variation_Id`) VALUES
(0x7016CE03, 787800400, 0x016C0154, 8.87982, -49.105, 0.00665, 0.954864, 0, 0, -0.297042, 0, NOW(), NULL);

-- 3. Escape Portal in each trial chamber. GUIDs: 0x700C0800 + (variation - 2) * 0x40.
DELETE FROM `landblock_instance`
WHERE `landblock` = 192 AND `weenie_Class_Id` = 787800401 AND `variation_Id` BETWEEN 2 AND 10;

INSERT INTO `landblock_instance` (`guid`, `weenie_Class_Id`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`,
    `angles_W`, `angles_X`, `angles_Y`, `angles_Z`, `is_Link_Child`, `last_Modified`, `variation_Id`) VALUES
(0x700C0800, 787800401, 0x00C0019D, 80.552, -96.133, 0.005, 1, 0, 0, 0, 0, NOW(), 2) /* v2 exit portal */,
(0x700C0840, 787800401, 0x00C0019D, 80.552, -96.133, 0.005, 1, 0, 0, 0, 0, NOW(), 3) /* v3 exit portal */,
(0x700C0880, 787800401, 0x00C0019D, 80.552, -96.133, 0.005, 1, 0, 0, 0, 0, NOW(), 4) /* v4 exit portal */,
(0x700C08C0, 787800401, 0x00C0019D, 80.552, -96.133, 0.005, 1, 0, 0, 0, 0, NOW(), 5) /* v5 exit portal */,
(0x700C0900, 787800401, 0x00C0019D, 80.552, -96.133, 0.005, 1, 0, 0, 0, 0, NOW(), 6) /* v6 exit portal */,
(0x700C0940, 787800401, 0x00C0019D, 80.552, -96.133, 0.005, 1, 0, 0, 0, 0, NOW(), 7) /* v7 exit portal */,
(0x700C0980, 787800401, 0x00C0019D, 80.552, -96.133, 0.005, 1, 0, 0, 0, 0, NOW(), 8) /* v8 exit portal */,
(0x700C09C0, 787800401, 0x00C0019D, 80.552, -96.133, 0.005, 1, 0, 0, 0, 0, NOW(), 9) /* v9 exit portal */,
(0x700C0A00, 787800401, 0x00C0019D, 80.552, -96.133, 0.005, 1, 0, 0, 0, 0, NOW(), 10) /* v10 exit portal */;

-- 4. Clean-up
DELETE FROM `landblock_instance` WHERE `weenie_Class_Id` = 787800500;
DELETE FROM `weenie` WHERE `class_Id` = 787800500;
DELETE FROM `landblock_instance` WHERE `landblock` = 192 AND `weenie_Class_Id` = 19853501 AND `variation_Id` = 2;
