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
INSERT INTO `landblock_instance` (
    `guid`, `weenie_Class_Id`, `obj_Cell_Id`, 
    `origin_X`, `origin_Y`, `origin_Z`, 
    `angles_W`, `angles_X`, `angles_Y`, `angles_Z`, 
    `is_Link_Child`, `last_Modified`, `variation_Id`
)
SELECT 
    (SELECT MAX(guid) + 1 FROM `landblock_instance` WHERE guid < 1000000) AS guid_calc,
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
LIMIT 1;

-- 5. Define the Neutering Kit Item (WCID: 98760399)
DELETE FROM `weenie` WHERE `class_Id` = 98760399;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) 
VALUES (98760399, 'neutering_kit', 8, NOW()); -- Type 8 = Misc/Item

DELETE FROM `weenie_properties_int` WHERE `object_Id` = 98760399;
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES 
(98760399, 1, 8192),      -- ItemType = 8192 (Misc/Tool)
(98760399, 5, 100),       -- EncumbranceVal = 100
(98760399, 16, 16),      -- ItemUseable = 16 (Usable on another item)
(98760399, 19, 500),      -- Value = 500 pyreals
(98760399, 150, 4);      -- DefaultClickAction = 4 (Use on target)

DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 98760399;
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES 
(98760399, 14, True);    -- IsSellable = True

DELETE FROM `weenie_properties_string` WHERE `object_Id` = 98760399;
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES 
(98760399, 1, 'Neutering Kit'),
(98760399, 14, 'Use this kit on a combat pet device in your inventory to permanently spay/neuter it, locking it out of any future breeding.');

DELETE FROM `weenie_properties_d_i_d` WHERE `object_Id` = 98760399;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES 
(98760399, 1, 0x0200021A),  -- Setup DID (scissors/pliers tool model)
(98760399, 8, 100669279);   -- Icon (scissors/cut icon)

-- 6. Define the Pet Tailoring Kit Item (WCID: 98760400)
DELETE FROM `weenie` WHERE `class_Id` = 98760400;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) 
VALUES (98760400, 'pet_tailoring_kit', 8, NOW());

DELETE FROM `weenie_properties_int` WHERE `object_Id` = 98760400;
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES 
(98760400, 1, 8192),      -- ItemType = 8192 (Misc/Tool)
(98760400, 5, 100),       -- EncumbranceVal = 100
(98760400, 16, 16),      -- ItemUseable = 16 (Usable on another item)
(98760400, 19, 25000),    -- Value = 25,000 pyreals
(98760400, 150, 4);      -- DefaultClickAction = 4 (Use on target)

DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 98760400;
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES 
(98760400, 14, True);    -- IsSellable = True

DELETE FROM `weenie_properties_string` WHERE `object_Id` = 98760400;
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES 
(98760400, 1, 'Pet Tailoring Kit'),
(98760400, 14, 'Use this kit on a source combat pet device in your inventory to extract its appearance. The source pet will be consumed, and this kit will hold the appearance.');

DELETE FROM `weenie_properties_d_i_d` WHERE `object_Id` = 98760400;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES 
(98760400, 1, 0x020006CF),  -- Setup DID (sewing kit / dye kit mesh)
(98760400, 8, 100671391);   -- Icon (sewing/dye kit icon)


-- 7. Define the Intermediate Pet Tailoring Kit (WCID: 98760401)
DELETE FROM `weenie` WHERE `class_Id` = 98760401;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) 
VALUES (98760401, 'intermediate_pet_tailoring_kit', 8, NOW());

DELETE FROM `weenie_properties_int` WHERE `object_Id` = 98760401;
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES 
(98760401, 1, 8192),      -- ItemType = 8192 (Misc/Tool)
(98760401, 5, 100),       -- EncumbranceVal = 100
(98760401, 16, 16),      -- ItemUseable = 16 (Usable on another item)
(98760401, 19, 0),        -- Value = 0 (tailored)
(98760401, 150, 4);      -- DefaultClickAction = 4 (Use on target)

DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 98760401;
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES 
(98760401, 14, False);   -- IsSellable = False

DELETE FROM `weenie_properties_string` WHERE `object_Id` = 98760401;
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES 
(98760401, 1, 'Pet Tailoring Kit (Primed)'),
(98760401, 14, 'Use this primed kit on a destination combat pet device in your inventory to apply the stored appearance to it. This kit will be consumed.');

DELETE FROM `weenie_properties_d_i_d` WHERE `object_Id` = 98760401;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES 
(98760401, 1, 0x020006CF),  -- Setup DID
(98760401, 8, 100671392);   -- Icon (glowing/primed sewing kit icon)
