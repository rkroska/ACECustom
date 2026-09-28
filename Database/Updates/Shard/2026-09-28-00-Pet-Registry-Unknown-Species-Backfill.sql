-- Pet registry: backfill captures with no species (creature_type IS NULL).
--
-- Essences from creatures whose weenie has no CreatureType (walls, bone piles, stockpiles, ...) were
-- registered with a NULL creature_type. RegisterEssence skipped the species stamps for them, so no
-- CapturedEssence / ShinyEssence stamp was ever granted, and the Monster-Dex showed them on a second
-- "Unknown" page next to the real CreatureType.Unknown (40) one.
--
-- The server now files these under CreatureType.Unknown. This script repairs the existing rows:
--   1. grants CapturedEssenceUnknown to every account with a NULL row that lacks it
--   2. grants ShinyEssenceUnknown to every account with a NULL shiny row that lacks it
--   3. moves the NULL rows to creature_type 40
--
-- Touches ace_shard AND ace_auth. Edit the schema names below if yours differ.
-- Run with the server STOPPED: the account quest-bonus cache only reloads on login.
-- The stamps must be granted before the UPDATE, because they are found by the NULL rows.
--
-- The script ends with the transaction still open. Check the verification counts, then run
-- COMMIT; (or ROLLBACK;) yourself in the same session. Closing the session without COMMIT rolls back.

-- Preview: accounts affected and which stamps they are owed
SELECT p.account_id, acc.accountName,
  COUNT(*)                                     AS null_rows,
  SUM(p.is_shiny)                              AS shiny_rows,
  MAX(cu.accountId IS NULL)                    AS needs_captured_unknown,
  MAX(p.is_shiny = 1 AND su.accountId IS NULL) AS needs_shiny_unknown
FROM ace_shard.pet_registry p
LEFT JOIN ace_auth.account acc      ON acc.accountId = p.account_id
LEFT JOIN ace_auth.account_quest cu ON cu.accountId = p.account_id AND cu.quest = 'CapturedEssenceUnknown' AND cu.num_Times_Completed >= 1
LEFT JOIN ace_auth.account_quest su ON su.accountId = p.account_id AND su.quest = 'ShinyEssenceUnknown' AND su.num_Times_Completed >= 1
WHERE p.creature_type IS NULL
GROUP BY p.account_id, acc.accountName
ORDER BY needs_shiny_unknown DESC, needs_captured_unknown DESC, p.account_id;

SET SQL_SAFE_UPDATES = 0;
START TRANSACTION;

-- 1. Base Unknown stamp
INSERT INTO ace_auth.account_quest (accountId, quest, num_Times_Completed)
SELECT DISTINCT account_id, 'CapturedEssenceUnknown', 1
FROM ace_shard.pet_registry
WHERE creature_type IS NULL
ON DUPLICATE KEY UPDATE num_Times_Completed = GREATEST(COALESCE(num_Times_Completed, 0), 1);

-- 2. Shiny Unknown stamp
INSERT INTO ace_auth.account_quest (accountId, quest, num_Times_Completed)
SELECT DISTINCT account_id, 'ShinyEssenceUnknown', 1
FROM ace_shard.pet_registry
WHERE creature_type IS NULL AND is_shiny = 1
ON DUPLICATE KEY UPDATE num_Times_Completed = GREATEST(COALESCE(num_Times_Completed, 0), 1);

-- 3. File the NULL rows under CreatureType.Unknown (40)
UPDATE ace_shard.pet_registry SET creature_type = 40 WHERE creature_type IS NULL;

-- Verify before committing
SELECT COUNT(*) AS null_rows_left FROM ace_shard.pet_registry WHERE creature_type IS NULL;   -- expect 0

SELECT COUNT(*) AS registry_accounts_missing_captured_unknown
FROM (SELECT DISTINCT account_id FROM ace_shard.pet_registry WHERE creature_type = 40) p
LEFT JOIN ace_auth.account_quest q
  ON q.accountId = p.account_id AND q.quest = 'CapturedEssenceUnknown' AND q.num_Times_Completed >= 1
WHERE q.accountId IS NULL;                                                                     -- expect 0

SELECT COUNT(*) AS registry_accounts_missing_shiny_unknown
FROM (SELECT DISTINCT account_id FROM ace_shard.pet_registry WHERE creature_type = 40 AND is_shiny = 1) p
LEFT JOIN ace_auth.account_quest q
  ON q.accountId = p.account_id AND q.quest = 'ShinyEssenceUnknown' AND q.num_Times_Completed >= 1
WHERE q.accountId IS NULL;                                                                     -- expect 0

-- All three counts 0: COMMIT;
-- Otherwise:          ROLLBACK;
