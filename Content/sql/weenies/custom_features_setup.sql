-- Custom Jail Escape Time Trial Setup

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

-- ===================================================================
-- CUSTOM JAIL ESCAPE TIME TRIAL OBSTACLES & SPAWNERS
-- ===================================================================

-- 7. Jailbreak Colored Portal Gen (WCID: 787800500)
DELETE FROM `weenie` WHERE `class_Id` = 787800500;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (787800500, 'jailbreak_colored_portal_gen', 1, NOW());
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES (787800500, 1, True); -- Stuck
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES (787800500, 11, True); -- IgnoreCollisions
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES (787800500, 13, True); -- Ethereal
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES (787800500, 18, True); -- Visibility (hidden from normal players)
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES (787800500, 19, False); -- Attackable
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES (787800500, 1, 'Jailbreak Colored Portal Gen');
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES (787800500, 1, 0x0200026B); -- Invisible generator setup

-- Clean up any removed obstacle nodes
DELETE FROM `weenie` WHERE `class_Id` BETWEEN 787800411 AND 787800421;
DELETE FROM `weenie` WHERE `class_Id` BETWEEN 787800501 AND 787800504;

-- 12. Duplicate spawners and the exit portal from the base layer to variations 2 through 10.
-- Clean up any existing records for variations 2 through 10 on landblock 0x00C0 (192) to prevent duplicate key errors
DELETE FROM `landblock_instance` 
WHERE `landblock` = 192 
  AND `variation_Id` BETWEEN 2 AND 10;

-- Duplicate spawners and the exit portal from the base layer to variations 2 through 10.
-- We cross join with the variation IDs 2..10 and offset GUIDs by (var_id - 1) * 1000.
INSERT INTO `landblock_instance` (
    `guid`, `weenie_Class_Id`, `obj_Cell_Id`, 
    `origin_X`, `origin_Y`, `origin_Z`, 
    `angles_W`, `angles_X`, `angles_Y`, `angles_Z`, 
    `is_Link_Child`, `last_Modified`, `variation_Id`
)
SELECT 
    l.`guid` + (v.var_id - 1) * 1000, 
    l.`weenie_Class_Id`, 
    l.`obj_Cell_Id`, 
    l.`origin_X`, 
    l.`origin_Y`, 
    l.`origin_Z`, 
    l.`angles_W`, 
    l.`angles_X`, 
    l.`angles_Y`, 
    l.`angles_Z`, 
    l.`is_Link_Child`, 
    NOW(), 
    v.var_id
FROM `landblock_instance` l
CROSS JOIN (
    SELECT 2 AS var_id UNION ALL
    SELECT 3 UNION ALL
    SELECT 4 UNION ALL
    SELECT 5 UNION ALL
    SELECT 6 UNION ALL
    SELECT 7 UNION ALL
    SELECT 8 UNION ALL
    SELECT 9 UNION ALL
    SELECT 10
) v
WHERE l.`landblock` = 192 
  AND (l.`weenie_Class_Id` = 787800500 OR l.`weenie_Class_Id` = 787800401)
  AND (l.`variation_Id` IS NULL OR l.`variation_Id` = 1);
