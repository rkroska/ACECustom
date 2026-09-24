-- =====================================================================================
-- Pet system text: make it client-safe and correct.
-- Target database: ace_world. Safe to re-run.
--
-- 1. The AC client cannot draw em dashes (U+2014) or curly apostrophes (U+2019); it shows a junk
--    glyph mid-sentence. They are replaced with " - " and "'". The characters are written as
--    byte literals so this file stays 7-bit ASCII and survives any client character set.
--    Touches: Resonance Lens, Savage Echo, Echo Weaver, Arcanum Lens Collector, Tamantha the Tamer,
--    Baby Bonk Bat and the camp wands 19853000-19853007.
-- 2. The Resonance Lens said it only works on shinies (it retries ANY failed capture), and the
--    Essence Resonator said Hollow essences return less (they return the same) and did not mention
--    bred pets (it salvages those too).
-- 3. The mastery statues' third paid change needs 30 MMDs, but its "not enough" message said 10.
--
-- Afterwards: @clearcache (or restart) so the cached weenies reload.
-- =====================================================================================

SET @em_dash = CONVERT(0xE28094 USING utf8mb4);
SET @curly_apostrophe = CONVERT(0xE28099 USING utf8mb4);

-- 1a. Item and NPC strings (names, descriptions, use text).
UPDATE `weenie_properties_string`
SET `value` = REPLACE(REPLACE(REPLACE(`value`, CONCAT(' ', @em_dash, ' '), ' - '), @em_dash, ' - '), @curly_apostrophe, '''')
WHERE `object_Id` IN (78780010, 78780013, 98760152, 19853000, 19853001, 19853003, 19853004, 19853005, 19853007)
  AND (`value` LIKE CONCAT('%', @em_dash, '%') OR `value` LIKE CONCAT('%', @curly_apostrophe, '%'));

-- 1b. NPC dialogue.
UPDATE `weenie_properties_emote_action` a
JOIN `weenie_properties_emote` e ON e.`id` = a.`emote_Id`
SET a.`message` = REPLACE(REPLACE(REPLACE(a.`message`, CONCAT(' ', @em_dash, ' '), ' - '), @em_dash, ' - '), @curly_apostrophe, '''')
WHERE e.`object_Id` IN (78780020, 78780022, 98760107)
  AND (a.`message` LIKE CONCAT('%', @em_dash, '%') OR a.`message` LIKE CONCAT('%', @curly_apostrophe, '%'));

-- 2a. Resonance Lens (78780010): it retries any failed capture, not only shinies.
UPDATE `weenie_properties_string`
SET `value` = 'Use this lens to retry the last creature that escaped your capture. It must still be nearby, and weakened again.'
WHERE `object_Id` = 78780010 AND `type` = 14 /* Use */;

UPDATE `weenie_properties_string`
SET `value` = 'A crystalline lens attuned to echoes of missed opportunities. When a creature escapes your capture, this lens can call that chance back for one final attempt. The resonance fades quickly - use it before the moment is lost. Obtained from the Echo Weaver in exchange for a Shimmering Echo.'
WHERE `object_Id` = 78780010 AND `type` = 16 /* LongDesc */;

-- 2b. Essence Resonator (78780014): same yield for Hollow and Siphoned, and bred pets can be salvaged.
UPDATE `weenie_properties_string`
SET `value` = 'A resonant crystalline tool that extracts Savage Echo from spare essences. Use it on a Siphoned or Hollow essence in your pack, or on a bred pet essence you no longer want. Siphoned and Hollow essences return the same amount, and shiny ones return more. Other summoning essences cannot be salvaged.'
WHERE `object_Id` = 78780014 AND `type` = 14 /* Use */;

-- 3. Mastery statues (Naturalist 49514, Necromancer 49515, Primalist 49516): the third paid change
--    checks for 30 MMDs (OwnsItem-20630_5), so its failure message must say 30.
UPDATE `weenie_properties_emote_action` a
JOIN `weenie_properties_emote` e ON e.`id` = a.`emote_Id`
SET a.`message` = 'You need 30 MMDs available to have your mastery changed.'
WHERE e.`object_Id` IN (49514, 49515, 49516)
  AND e.`category` = 23 /* TestFailure */
  AND e.`quest` = 'OwnsItem-20630_5'
  AND a.`message` = 'You need 10 MMDs available to have your mastery changed.';
