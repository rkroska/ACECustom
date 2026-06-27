/* Aerbax's Prodigal - correct platform-port health thresholds to retail values.

   Each phase is a WoundedTaunt emote (category 15) that ports Aerbax's Shadow
   to the next platform when health drops to/below the band's max_Health.

   Retail thresholds:  East 90%, North 77%, West 72%, South 60%, Center 33%
   North/West/Center already matched. This corrects East (was 60%) and South (61%).

   Pairs with the engine fix that fires every crossed WoundedTaunt band on lethal
   hits (EmoteManager.SelectWoundedTauntBands). */

-- East port  | Aerbax's Shadow WCID 36951, emote 199801 | 60% -> 90%
UPDATE weenie_properties_emote
SET    max_Health = 0.9
WHERE  id = 199801 AND object_Id = 36951;

-- South port | Aerbax's Shadow WCID 37381, emote 199850 | 61% -> 60% (exact retail)
UPDATE weenie_properties_emote
SET    max_Health = 0.6
WHERE  id = 199850 AND object_Id = 37381;
