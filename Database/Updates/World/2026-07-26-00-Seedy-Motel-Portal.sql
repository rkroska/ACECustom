-- Custom Seedy Motel Portal Setup
-- WCID: 98760388
-- Landblock: 0x013A02AE, Variant: 3

-- 1. Define the Portal Weenie
DELETE FROM `weenie` WHERE `class_Id` = 98760388;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (98760388, 'seedy_motel_portal', 7, NOW()); -- Type 7 = Portal

-- 2. Portal Properties
DELETE FROM `weenie_properties_int` WHERE `object_Id` = 98760388;
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(98760388, 1, 65536),    -- ItemType = 65536 (Portal)
(98760388, 16, 32),     -- ItemUseable = 32 (Usable)
(98760388, 93, 3084),   -- PhysicsState = 3084 (Ethereal + ReportCollisions + ParticleEmitter)
(98760388, 111, 1),     -- AppraisalUsefulness = 1
(98760388, 133, 4),     -- ShowableOnRadar = 4
(98760388, 150, 3);     -- DefaultClickAction = 3 (Use/Activate)

DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 98760388;
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(98760388, 1, True),    -- Stuck = True
(98760388, 11, False),  -- IgnoreCollisions = False
(98760388, 12, True),   -- ReportCollisions = True
(98760388, 13, True),   -- Ethereal = True
(98760388, 15, True),   -- LightsStatus = True
(98760388, 63, True);   -- Invincible = True

DELETE FROM `weenie_properties_float` WHERE `object_Id` = 98760388;
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(98760388, 54, -0.1);   -- UseRadius = -0.1

DELETE FROM `weenie_properties_string` WHERE `object_Id` = 98760388;
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(98760388, 1, 'Portal to Seedy Motel'),
(98760388, 14, 'Double click this portal to travel to the Seedy Motel.');

DELETE FROM `weenie_properties_d_i_d` WHERE `object_Id` = 98760388;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(98760388, 1, 0x02000004),  -- Setup DID (Purple Portal)
(98760388, 2, 150994947),   -- MotionTable
(98760388, 8, 100667499);   -- Icon

-- 3. Portal Destination (Landblock 0x013A02AE, Variant 3)
DELETE FROM `weenie_properties_position` WHERE `object_Id` = 98760388;
INSERT INTO `weenie_properties_position` (`object_Id`, `type`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`, `variation_Id`)
VALUES (98760388, 2, 20578990, 0, 0, 0, 1, 0, 0, 0, 3); -- Type 2 = Destination, Cell = 0x013A02AE (20578990)

-- 4. Spawn Portal next to Professor Ruggan in Lin
--
-- Idempotent: every run first removes any placement of this portal, then places exactly one
-- copy again (or zero copies if no NPC named 'Prof. Ruggan' / 'Professor Ruggan' is placed;
-- INSERT ... SELECT with no matching row simply inserts nothing and does not error).
--
-- guid follows the static-instance convention 0x7LLLLnnn (LLLL = landblock of the cell the
-- portal lands in). It is derived from Ruggan's cell, so it stays inside the target
-- landblock's own static range whichever landblock he stands in: the next free guid above the
-- highest one already used in that range, starting at 0x7LLLL001 when the range is empty
-- (COALESCE guards the empty case, so no NULL ever reaches the PRIMARY KEY).
--
-- Do NOT list the `landblock` column: it is GENERATED from obj_Cell_Id.
DELETE FROM `landblock_instance` WHERE `weenie_Class_Id` = 98760388;

INSERT INTO `landblock_instance` (
    `guid`, `weenie_Class_Id`, `obj_Cell_Id`,
    `origin_X`, `origin_Y`, `origin_Z`,
    `angles_W`, `angles_X`, `angles_Y`, `angles_Z`,
    `is_Link_Child`, `last_Modified`, `variation_Id`
)
SELECT
    COALESCE(
        (SELECT MAX(x.`guid`) FROM `landblock_instance` x
          WHERE x.`guid` BETWEEN (0x70000000 | ((l.`obj_Cell_Id` >> 16) << 12))
                             AND (0x70000000 | ((l.`obj_Cell_Id` >> 16) << 12) | 0xFFF)),
        0x70000000 | ((l.`obj_Cell_Id` >> 16) << 12)
    ) + 1 AS guid_calc,
    98760388, -- Seedy Motel Portal WCID
    l.`obj_Cell_Id`,
    l.`origin_X` + 2.0, -- Spawn 2 meters away from Ruggan
    l.`origin_Y`,
    l.`origin_Z`,
    l.`angles_W`,
    l.`angles_X`,
    l.`angles_Y`,
    l.`angles_Z`,
    0,
    NOW(),
    l.`variation_Id`
FROM `landblock_instance` l
JOIN `weenie_properties_string` s ON l.`weenie_Class_Id` = s.`object_Id`
WHERE s.`type` = 1 AND (s.`value` = 'Prof. Ruggan' OR s.`value` = 'Professor Ruggan')
ORDER BY l.`guid`
LIMIT 1;

-- 5. Pet Neutering / Tailoring kits (98760399-98760401) are defined ONLY in
--    2026-09-09-01-Pet-Tailoring-and-Neutering-Kits.sql; the older copies that lived here were removed.
