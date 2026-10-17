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
-- The trial's placements and clean-up now live in
-- Database/Updates/World/2026-10-16-00-Jailbreak-Course.sql (also repeats sections 1-3 above).
-- The old light markers (787800500) and the step that copied placements from the base layer to
-- variations 2-10 were removed: the light effects are played by code, and the copy step deleted the
-- trial's exit portals whenever no base-layer portal existed.
-- ===================================================================
