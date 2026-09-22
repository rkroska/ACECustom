-- ===========================================================================
-- GrantRandomQuestStamp: fold TakeItems into the grant  -  2026-09-20
--
-- [WARNING] DEPLOY THE SERVER BUILD FIRST, THEN THIS SCRIPT. [WARNING]
-- This script REQUIRES a build that understands the "TAKE:" prefix. Applied to
-- an older build it is destructive: the old parser does not recognise TAKE:,
-- so the whole message - prefix and all - becomes the stamp list, the separate
-- TakeItems row is already gone, and the NPC hands out stamps WITHOUT taking
-- the turn-in. It also writes the junk prefix text into
-- character_properties_quest_registry and account_quest as a quest name, which
-- neither this script nor its rollback removes.
-- The reverse order LOSES NOTHING but is not invisible: on this build an
-- un-migrated 136 action reserves an inventory slot for its REFUND item and no
-- longer gets a phantom freed-slot credit from its TakeItems, so a player with a
-- completely full pack can be told "you need 1 more free inventory slot(s)" on a
-- turn-in that would previously have gone through. Nothing is destroyed, and
-- applying this script clears it. AutoApplyDatabaseUpdates is off on these
-- shards, so this is a manual step and the order is the operator's.
--
-- RUNNING IT: apply as one unit. Do NOT use mysql --force - that would commit a
-- successful statement 1 after a failed statement 2, which is the charge-twice
-- state. Afterwards run /clearcache in game, or the running server keeps serving
-- the cached pre-migration weenies and it will look as though nothing happened.
--
-- WHY. These NPCs take the turn-in at emote action order 2 and grant the stamp
-- at order 4, with a 0.5 s pre-delay between. The chain is queued on the NPC's
-- own EmoteManager, so if the NPC is destroyed in that window - a timed event
-- NPC despawning, a generator shutting down - the item is consumed and nothing
-- is granted. Silently: WorldObject.EnqueueAction discards the continuation for
-- creatures, with no log and no exception.
--
-- WHAT. Emote type 136 understands "TAKE:<wcid>[:<amount>]|" and consumes the
-- turn-in itself, in the same synchronous step as the grant. Nothing can run
-- between them. With nothing left to grant it simply never takes the item, so
-- the refund path is unreachable for a migrated NPC.
--
-- NOT CONVERTED: a TakeItems with stack_Size = -1 ("take all"). The TAKE:
-- grammar cannot express it and the parser rejects a negative amount, so those
-- rows are left alone rather than silently downgraded to "take 1" - the NPC
-- keeps the old shape and the old window. 438 such rows exist world-wide;
-- none belong to the weenies below. A zero or any other negative stack_Size is
-- left alone the same way: only NULL or 1 and up is folded.
--
-- NOTE on the REFUND: section. It is left in the message so the rollback stays
-- lossless, but it is INERT while TAKE: is present: with nothing to grant the
-- handler returns before the refund block. Do not read it as live behaviour.
-- For 98760330 the refund wcid (867530128 QB Stipend) is not even the item it
-- takes (300004 Enlightened Coin); that exchange is inert too.
--
-- Weenies converted - every one in the world DB with this shape:
--   696900158  QBs R Us            takes 987100    Event Coin
--   98760330   QB Dynamo (tester)  takes 300004    Enlightened Coin
--   98760369   QB Dynamo (placed)  takes 867530128 QB Stipend
-- ===========================================================================

START TRANSACTION;

-- --- 1. carry the TakeItems wcid (and stack size, when set) into the message
-- Both statements below use the SAME predicate: category 13 and a TakeItems whose
-- stack_Size is NULL or at least 1 (never "take all", zero or a stray negative).
-- If they disagree, a set can gain the prefix and keep its TakeItems, and the item
-- is taken twice.
UPDATE weenie_properties_emote_action a
JOIN (
    SELECT emote_Id, MIN(weenie_Class_Id) AS weenie_Class_Id, MIN(stack_Size) AS stack_Size, COUNT(*) AS take_rows
      FROM weenie_properties_emote_action
     WHERE type = 74
       AND weenie_Class_Id IS NOT NULL
       AND (stack_Size IS NULL OR stack_Size >= 1)
     GROUP BY emote_Id
    HAVING COUNT(*) = 1          -- two takes in one set would fold only one and delete both
) t ON t.emote_Id = a.emote_Id
SET a.message = CONCAT('TAKE:', t.weenie_Class_Id,
                       CASE WHEN t.stack_Size IS NULL OR t.stack_Size <= 1 THEN '' ELSE CONCAT(':', t.stack_Size) END,
                       '|', a.message)
WHERE a.type = 136
  AND a.message NOT LIKE 'TAKE:%'
  AND a.emote_Id IN (
        SELECT id FROM weenie_properties_emote
         WHERE object_Id IN (696900158, 98760330, 98760369)
           AND category = 13
  );

-- --- 2. drop the TakeItems rows that were actually folded in
-- Keyed to the message we just wrote, so an unconverted set keeps its TakeItems.
DELETE a FROM weenie_properties_emote_action a
  JOIN weenie_properties_emote e ON e.id = a.emote_Id
  JOIN weenie_properties_emote_action g
    ON g.emote_Id = a.emote_Id
   AND g.type = 136
   -- match the wcid exactly at its boundary: a bare '%' would also match a sibling take
   -- whose wcid is a decimal prefix of this one (folded 987100, sibling 98710) and delete
   -- it without folding it, silently losing that take.
   AND (g.message LIKE CONCAT('TAKE:', a.weenie_Class_Id, '|%')
        OR g.message LIKE CONCAT('TAKE:', a.weenie_Class_Id, ':%'))
 WHERE a.type = 74
   AND (a.stack_Size IS NULL OR a.stack_Size >= 1)
   AND e.category = 13
   AND e.object_Id IN (696900158, 98760330, 98760369);

COMMIT;

-- --- verify: every converted 136 starts with TAKE:, and no type 74 remains
-- If takeitems_left is 1 the two statements disagreed: the prefix is written and the
-- TakeItems survives, so the turn-in charges TWICE. Do not leave the shard in that state.
-- SELECT e.object_Id, LEFT(a.message, 40) AS msg,
--        (SELECT COUNT(*) FROM weenie_properties_emote_action t
--          WHERE t.emote_Id = a.emote_Id AND t.type = 74) AS takeitems_left
--   FROM weenie_properties_emote_action a
--   JOIN weenie_properties_emote e ON e.id = a.emote_Id
--  WHERE a.type = 136;

-- ===========================================================================
-- ROLLBACK. Restores the TakeItems action and strips the prefix.
-- Valid ONLY against a build WITHOUT TAKE support - on a TAKE build the NPC
-- would then take the item twice.
-- [WARNING] RUN BOTH STATEMENTS OR NEITHER, IN A TRANSACTION. The second statement on
-- its own strips the prefix without restoring the TakeItems row, which leaves the
-- NPC handing out stamps FOR FREE, forever, on any build.
-- order 2 is free because statement 2 above vacated it (orders run 0,1,3,4,5);
-- the NOT EXISTS guard makes a second run a no-op rather than a double take.
-- ===========================================================================
-- START TRANSACTION;
-- INSERT INTO weenie_properties_emote_action (emote_Id, `order`, type, delay, extent, weenie_Class_Id, stack_Size)
--   SELECT a.emote_Id, 2, 74, 0, 1,
--          CAST(SUBSTRING_INDEX(SUBSTRING_INDEX(SUBSTRING(a.message, 6), '|', 1), ':', 1) AS UNSIGNED),
--          CASE WHEN LOCATE(':', SUBSTRING_INDEX(SUBSTRING(a.message, 6), '|', 1)) = 0 THEN NULL
--               ELSE CAST(SUBSTRING_INDEX(SUBSTRING_INDEX(SUBSTRING(a.message, 6), '|', 1), ':', -1) AS SIGNED) END
--     FROM weenie_properties_emote_action a
--     JOIN weenie_properties_emote e ON e.id = a.emote_Id
--    WHERE a.type = 136 AND a.message LIKE 'TAKE:%'
--      AND e.object_Id IN (696900158, 98760330, 98760369)
--      AND NOT EXISTS (SELECT 1 FROM weenie_properties_emote_action x
--                       WHERE x.emote_Id = a.emote_Id AND x.type = 74);
-- UPDATE weenie_properties_emote_action a
--    JOIN weenie_properties_emote e ON e.id = a.emote_Id
--     SET a.message = SUBSTRING(a.message, LOCATE('|', a.message) + 1)
--  WHERE a.type = 136 AND a.message LIKE 'TAKE:%'
--    AND e.object_Id IN (696900158, 98760330, 98760369);
-- COMMIT;
