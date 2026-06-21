DELETE FROM `weenie` WHERE `class_Id` = 777700101;
DELETE FROM `weenie_properties_int` WHERE `object_Id` = 777700101;
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 777700101;
DELETE FROM `weenie_properties_string` WHERE `object_Id` = 777700101;
DELETE FROM `weenie_properties_float` WHERE `object_Id` = 777700101;
DELETE FROM `weenie_properties_d_i_d` WHERE `object_Id` = 777700101;
DELETE FROM `weenie_properties_generator` WHERE `object_Id` = 777700101;

-- 1. Core Registration (Type 1 = Generic / Visible Static Object)
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700101, 'altar_of_charms', 1, NOW());

-- 2. Integer Properties (ItemType = 0x0, Spawner limits set to 3, PhysicsState = 1044, ExpirationTimestamp set to 1780282800)
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700101,   1,        0) /* ItemType - None */
     , (777700101,  81,        3) /* MaxGeneratedObjects - 3 active charms max */
     , (777700101,  82,        3) /* InitGeneratedObjects - Spawns 3 charms initially */
     , (777700101,  93,     1044); /* PhysicsState - Ethereal/IgnoreCollisions */

-- 3. Float Properties (RegenerationInterval set to 2 seconds)
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`)
VALUES (777700101,  41,      2.0) /* RegenerationInterval - ticks every 2 seconds */;

-- 4. Visuals (Setup 33556439 = Glowing Crystal Lord)
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700101,   1,  33556439) /* Setup - Glowing Crystal Lord */
     , (777700101,   3, 0x20000001) /* SoundTable */
     , (777700101,   8, 0x06001AF9) /* Radar Icon - Magenta Dot */;

-- 5. Behavior Bools (Ignore Collisions, show radar blip, Stuck for permanent landblock spawn)
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700101,  83,      False) /* IsPlayerInteractable - False */
     , (777700101,  11,       True) /* IgnoreCollisions */
     , (777700101,  51,       True) /* ShowRadarBlip */
     , (777700101,   1,       True) /* Stuck */;

-- 6. String Properties (Name and Description)
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700101,   1, 'Sir Spawns-A-Charm') /* Name */
     , (777700101,  16, 'A majestic, levitating crystal golem humming with ancient magic, materializing rare combat ability charms around its core.') /* LongDesc */;

-- 7. Generator Logic (Probability set to -1 for all, making them deterministic simultaneously)
-- Columns: object_Id, probability, weenie_Class_Id, delay, init_Create, max_Create, when_Create, where_Create, stack_Size, palette_Id, shade, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z
INSERT INTO `weenie_properties_generator` 
(`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES 
  -- A. Mana Barrier Charm (Tier 1) (Spawns 1.5m to the North, hovering 0.5m off the ground)
  (777700101, -1.0, 777700001, 2.0, 1, 1, 2, 4, -1, 0, 0.0, 0,  0.0,  1.5, 0.5, 1.0, 0.0, 0.0, 0.0)
  
  -- B. Charm of Auto Rebuffing (Spawns 1.5m to the West, hovering 0.5m off the ground)
  , (777700101, -1.0, 777700300, 2.0, 1, 1, 2, 4, -1, 0, 0.0, 0, -1.5,  0.0, 0.5, 1.0, 0.0, 0.0, 0.0)
  
  -- C. Fork Charm (Tier 1) (Spawns 1.5m to the East, hovering 0.5m off the ground)
  , (777700101, -1.0, 777700027, 2.0, 1, 1, 2, 4, -1, 0, 0.0, 0,  1.5,  0.0, 0.5, 1.0, 0.0, 0.0, 0.0);
