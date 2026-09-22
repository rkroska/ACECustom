-- =====================================================================================
-- Ruggan's Annex - exit portal back to Prof. Ruggan (plan Q4)
-- Target database: ace_world. Safe to re-run.
--
-- The annex (landblock 0x0106 variation 2) had no way out; players had to recall. This adds
-- 78780261, a copy of the motel portal (98760388) pointing the other way:
--   placed at   cell 0x01060179 (29.79, -30.03) in the annex, variation 2
--   lands at    cell 0xDB3B0019 (80.28, 18.18, 30.70) beside Prof. Ruggan, a few steps from
--               the motel portal
-- Both positions were stood on in game on 2026-09-21.
--
-- Afterwards: @clearcache, then @reload-landblock inside the annex.
-- =====================================================================================

DELETE FROM `weenie` WHERE `class_Id` = 78780261;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780261, 'annex_exit_portal', 7, '2026-09-21 00:00:00');

DELETE FROM `weenie_properties_int` WHERE `object_Id` = 78780261;
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780261,   1,  65536) /* ItemType - Portal */
     , (78780261,  16,     32) /* ItemUseable - Remote */
     , (78780261,  93,   3084) /* PhysicsState */
     , (78780261, 111,      1) /* PortalBitmask - Unrestricted */
     , (78780261, 133,      4) /* ShowableOnRadar */
     , (78780261, 150,      3) /* same as the motel portal */;

DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780261;
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780261,  1, True ) /* Stuck */
     , (78780261, 11, False) /* IgnoreCollisions */
     , (78780261, 12, True ) /* ReportCollisions */
     , (78780261, 13, True ) /* Ethereal */
     , (78780261, 15, True ) /* LightsStatus */
     , (78780261, 63, True ) /* same as the motel portal */;

DELETE FROM `weenie_properties_float` WHERE `object_Id` = 78780261;
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`)
VALUES (78780261, 54, -0.1) /* UseRadius */;

DELETE FROM `weenie_properties_string` WHERE `object_Id` = 78780261;
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780261,  1, 'Portal to Prof. Ruggan') /* Name */
     , (78780261, 14, 'Double click this portal to return to Prof. Ruggan.') /* Use */;

DELETE FROM `weenie_properties_d_i_d` WHERE `object_Id` = 78780261;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780261, 1,  33554436) /* Setup - portal */
     , (78780261, 2, 150994947) /* MotionTable */
     , (78780261, 8, 100667499) /* Icon */;

DELETE FROM `weenie_properties_position` WHERE `object_Id` = 78780261;
INSERT INTO `weenie_properties_position` (`object_Id`, `position_Type`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`, `variation_Id`)
VALUES (78780261, 2, 0xDB3B0019, 80.283607, 18.184467, 30.695301, -0.967966, 0, 0, -0.251082, NULL); -- Type 2 = Destination

-- The placement, with the next free static guid in landblock 0x0106.
DELETE FROM `landblock_instance` WHERE `weenie_Class_Id` = 78780261 AND `landblock` = 0x0106 AND `variation_Id` = 2;
SET @g = (SELECT COALESCE(MAX(`guid`), 0x70105FFF) FROM `landblock_instance` WHERE `guid` BETWEEN 0x70106000 AND 0x70106FFF);
INSERT INTO `landblock_instance` (`guid`, `weenie_Class_Id`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`, `is_Link_Child`, `last_Modified`, `variation_Id`)
VALUES (@g + 1, 78780261, 0x01060179, 29.785805, -30.034668, 0.005, -0.999403, 0, 0, -0.034540, False, NOW(), 2);
