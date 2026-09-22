-- =====================================================================================
-- Ruggan's Annex - resize creatures that tower over the room
-- Target database: ace_world. Safe to re-run.
--
-- DefaultScale (float 39) on the annex clones:
--   Gary, Splotch      template 29008 Browerk, 1.1   -> 0.275 (a quarter)
--   both Ursuins       template 7990 Field Ursuin, 1.0 -> 0.5 (a half)
--   Nine-Colour Shreth template 4110 Blood Shreth, 1.0 -> 0.75 (set in game, copied back here)
--
-- Afterwards: @clearcache, then @reload-landblock where they stand (or restart) so the
-- placed copies respawn at the new size.
-- =====================================================================================

DELETE FROM `weenie_properties_float`
WHERE `object_Id` IN (78780203, 78780220, 78780213, 78780222, 78780223) AND `type` = 39;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780203, 39, 0.275), -- Gary: a quarter of the Browerk's 1.1
  (78780220, 39, 0.275), -- Splotch: a quarter of the Browerk's 1.1
  (78780213, 39, 0.5),   -- Pedigreed Ursuin (Papers Pending): half of 1.0
  (78780222, 39, 0.5),   -- Ursuin, Unregistered: half of 1.0
  (78780223, 39, 0.75);  -- Nine-Colour Shreth: three quarters of 1.0
