/* =====================================================================================
   Ruggan's Annex - idle line weights
   Target database: ace_world. Safe to re-run.

   Every annex NPC with several idle (HeartBeat) lines gave them all the same probability.
   EmoteManager.GetEmoteSet keeps the sets whose probability is above a random roll and takes the
   LOWEST, so with equal values only the first line ever fired - and four of the six feud
   openers never started. These are stacked thresholds that keep each NPC's overall chatter rate
   and share it across its lines. Rows are matched by object and line text, because emote row
   ids differ between databases.

   Afterwards: @clearcache, then respawn the placed NPCs (or restart) so they reload.
   ===================================================================================== */

SET @__old_safe_updates = @@SQL_SAFE_UPDATES;
SET SQL_SAFE_UPDATES = 0;

UPDATE `weenie_properties_emote` e
JOIN `weenie_properties_emote_action` a ON a.`emote_Id` = e.`id` AND a.`order` = 0
SET e.`probability` = 0.0067
WHERE e.`object_Id` = 78780200 AND e.`category` = 5 AND a.`message` LIKE 'Mrs. Ruggan sent another letter%';

UPDATE `weenie_properties_emote` e
JOIN `weenie_properties_emote_action` a ON a.`emote_Id` = e.`id` AND a.`order` = 0
SET e.`probability` = 0.0133
WHERE e.`object_Id` = 78780200 AND e.`category` = 5 AND a.`message` LIKE 'There is a sign%';

UPDATE `weenie_properties_emote` e
JOIN `weenie_properties_emote_action` a ON a.`emote_Id` = e.`id` AND a.`order` = 0
SET e.`probability` = 0.02
WHERE e.`object_Id` = 78780200 AND e.`category` = 5 AND a.`message` LIKE 'Nine hundred essences%';

UPDATE `weenie_properties_emote` e
JOIN `weenie_properties_emote_action` a ON a.`emote_Id` = e.`id` AND a.`order` = 0
SET e.`probability` = 0.0133
WHERE e.`object_Id` = 78780202 AND e.`category` = 5 AND a.`message` LIKE 'You. Yes. Dance%';

UPDATE `weenie_properties_emote` e
JOIN `weenie_properties_emote_action` a ON a.`emote_Id` = e.`id` AND a.`order` = 0
SET e.`probability` = 0.0267
WHERE e.`object_Id` = 78780202 AND e.`category` = 5 AND a.`message` LIKE 'Everyone dances here%';

UPDATE `weenie_properties_emote` e
JOIN `weenie_properties_emote_action` a ON a.`emote_Id` = e.`id` AND a.`order` = 0
SET e.`probability` = 0.04
WHERE e.`object_Id` = 78780202 AND e.`category` = 5 AND a.`message` LIKE 'Both of you. Together%';

UPDATE `weenie_properties_emote` e
JOIN `weenie_properties_emote_action` a ON a.`emote_Id` = e.`id` AND a.`order` = 0
SET e.`probability` = 0.0075
WHERE e.`object_Id` = 78780210 AND e.`category` = 5 AND a.`message` LIKE 'A Browerk is brown%';

UPDATE `weenie_properties_emote` e
JOIN `weenie_properties_emote_action` a ON a.`emote_Id` = e.`id` AND a.`order` = 0
SET e.`probability` = 0.015
WHERE e.`object_Id` = 78780210 AND e.`category` = 5 AND a.`message` LIKE 'Every creature in this lounge%';

UPDATE `weenie_properties_emote` e
JOIN `weenie_properties_emote_action` a ON a.`emote_Id` = e.`id` AND a.`order` = 0
SET e.`probability` = 0.0225
WHERE e.`object_Id` = 78780210 AND e.`category` = 5 AND a.`message` LIKE 'Bloodline. Conformation%';

UPDATE `weenie_properties_emote` e
JOIN `weenie_properties_emote_action` a ON a.`emote_Id` = e.`id` AND a.`order` = 0
SET e.`probability` = 0.03
WHERE e.`object_Id` = 78780210 AND e.`category` = 5 AND a.`message` LIKE 'I have been asked to stop%';

UPDATE `weenie_properties_emote` e
JOIN `weenie_properties_emote_action` a ON a.`emote_Id` = e.`id` AND a.`order` = 0
SET e.`probability` = 0.015
WHERE e.`object_Id` = 78780220 AND e.`category` = 5 AND a.`message` LIKE 'They call it purebred%';

UPDATE `weenie_properties_emote` e
JOIN `weenie_properties_emote_action` a ON a.`emote_Id` = e.`id` AND a.`order` = 0
SET e.`probability` = 0.03
WHERE e.`object_Id` = 78780220 AND e.`category` = 5 AND a.`message` LIKE 'The Professor called me an accident%';

SET SQL_SAFE_UPDATES = @__old_safe_updates;

/* Check: every idle line should now show a distinct probability. */
SELECT e.`object_Id`, e.`probability`, LEFT(a.`message`, 60) AS line
FROM `weenie_properties_emote` e
JOIN `weenie_properties_emote_action` a ON a.`emote_Id` = e.`id` AND a.`order` = 0
WHERE e.`object_Id` BETWEEN 78780200 AND 78780249 AND e.`category` = 5 AND a.`message` IS NOT NULL
ORDER BY e.`object_Id`, e.`probability`;
