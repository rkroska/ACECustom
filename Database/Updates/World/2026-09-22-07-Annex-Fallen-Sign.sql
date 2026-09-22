-- =====================================================================================
-- Ruggan's Annex - the fallen sign (78780206)
-- Target database: ace_world. Safe to re-run.
--
-- Fenwick says "Rules are on the sign. The sign is on the floor." This is that sign: a copy of
-- the retail Old Rotted Sign (8564, model 0x0200099A), ethereal so players walk over it, lying on
-- its back in the Drop at Fenwick's feet. Appraising it shows the house rules.
-- The placement is tipped 90 degrees about X (angles W = X = 0.7071068); it was looked at in game
-- on 2026-09-22. @create always spawns it upright, so only the placement shows it lying down.
--
-- Afterwards: @clearcache, then @reload-landblock inside the annex.
-- =====================================================================================

DELETE FROM `weenie` WHERE `class_Id` = 78780206;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780206, 'annex_fallen_sign', 1, '2026-09-22 00:00:00');

DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780206;
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780206,  1, True ) /* Stuck */
     , (78780206, 12, True ) /* ReportCollisions */
     , (78780206, 13, True ) /* Ethereal - players walk over it */
     , (78780206, 22, False) /* Inscribable */;

DELETE FROM `weenie_properties_int` WHERE `object_Id` = 78780206;
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780206,  1,  128) /* ItemType - Misc */
     , (78780206,  5, 9000) /* EncumbranceVal */
     , (78780206,  8, 1500) /* Mass */
     , (78780206, 16,    1) /* ItemUseable - No */
     , (78780206, 19,    0) /* Value */
     , (78780206, 93, 1052) /* PhysicsState - the retail sign's 1048 plus Ethereal */;

DELETE FROM `weenie_properties_float` WHERE `object_Id` = 78780206;
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`)
VALUES (78780206, 54, 3.0) /* UseRadius */;

DELETE FROM `weenie_properties_d_i_d` WHERE `object_Id` = 78780206;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780206, 1, 0x0200099A) /* Setup - Old Rotted Sign */
     , (78780206, 8, 0x060012D3) /* Icon */;

DELETE FROM `weenie_properties_string` WHERE `object_Id` = 78780206;
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780206,  1, 'Fallen Sign') /* Name */
     , (78780206, 16, 'RUGGAN''S ANNEX - HOUSE RULES\n1. Both pets in the same room. Both owners dance.\n2. The baby goes to the dam''s owner. Settle up first.\n3. Do not feed the Registry. Do not pet the Ward.\n4. Shinies do not breed. Do not ask.\n5. This is a filing annex.  - Prof. R.') /* LongDesc - shown on appraisal */;

-- The placement, with the next free static guid in landblock 0x0106.
DELETE FROM `landblock_instance` WHERE `weenie_Class_Id` = 78780206 AND `landblock` = 0x0106 AND `variation_Id` = 2;
SET @g = (SELECT COALESCE(MAX(`guid`), 0x70105FFF) FROM `landblock_instance` WHERE `guid` BETWEEN 0x70106000 AND 0x70106FFF);
-- Range full (1880125439 = 0x70106FFF): the two-row subquery raises error 1242 and stops the script.
SET @g = IF(@g + 1 > 1880125439, (SELECT 1 UNION SELECT 2), @g);
INSERT INTO `landblock_instance` (`guid`, `weenie_Class_Id`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`, `is_Link_Child`, `last_Modified`, `variation_Id`)
VALUES (@g + 1, 78780206, 0x01060186, 39.0, -23.0, 0.05, 0.7071068, 0.7071068, 0, 0, False, NOW(), 2);
