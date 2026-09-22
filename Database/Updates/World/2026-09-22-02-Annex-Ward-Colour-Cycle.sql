-- =====================================================================================
-- Ruggan's Annex - the Ward pets cycle through mutation colours
-- Target database: ace_world. Safe to re-run.
-- Needs the server build that adds PropertyFloat.ShowcaseColourCycleSeconds (9060) and
-- Creature_ShowcaseColour.cs. On an older build the value is ignored.
--
-- Every ~60 s, while a player is nearby, each of these re-rolls a vibrant mutation colour
-- (the Chromatic Catalyst pool) and plays a small sparkle. Each starts at a random point
-- in its cycle, so they never change together. Live only: nothing is saved, and the colour
-- resets on respawn.
--
-- Deliberately NOT on the Registry pets (they stay brown - that is the joke) or on Mubb
-- Junior (the paternity scenes depend on him staying neon).
--
-- Afterwards: @clearcache, then @reload-landblock in the annex.
-- =====================================================================================

DELETE FROM `weenie_properties_float` WHERE `object_Id` IN (78780221, 78780222, 78780223, 78780224) AND `type` = 9060;
INSERT INTO `weenie_properties_float` (`object_Id`,`type`,`value`) VALUES
  (78780221, 9060, 60), -- The Teal Incident
  (78780222, 9060, 60), -- Ursuin, Unregistered
  (78780223, 9060, 60), -- Nine-Colour Shreth
  (78780224, 9060, 60); -- Subject Twelve
