-- =====================================================================================
-- Ruggan's Annex - point the motel portal (98760388) at the annex's new home
-- Target database: ace_world. Safe to re-run.
--
-- The annex moved from landblock 0x013A variation 3 to 0x0106 variation 2. The portal still
-- sent players to the old place (cell 0x013A02AE, variation 3). New destination: "The Drop",
-- cell 0x01060186 beside Fenwick, a spot stood on in game, facing east toward Fenwick (turned 180 degrees on 2026-09-21;
-- the first version faced the player away from him).
-- 2026-07-26-00-Seedy-Motel-Portal.sql carries the same destination for fresh installs.
--
-- Afterwards: @clearcache, then @reload-landblock where the portal stands (next to Prof. Ruggan).
-- =====================================================================================

DELETE FROM `weenie_properties_position` WHERE `object_Id` = 98760388 AND `position_Type` = 2;
INSERT INTO `weenie_properties_position` (`object_Id`, `position_Type`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`, `variation_Id`)
VALUES (98760388, 2, 0x01060186, 35.1771, -19.7579, 0.005, 0.698934, 0, 0, -0.715186, 2); -- Type 2 = Destination
