-- Staff kill rates history: T11+ kills per character per UTC hour (KillRateHistory, the portal's 7d / 30d Kill Rates windows).
-- One row per character per hour per server run: the server writes each hour when it ends (and the running hour, partial = 1,
-- at a clean shutdown). A restart in the middle of an hour leaves two rows for it (two run_id values); readers add them up.
-- kills_by_tier is a JSON object of the tiers with kills, e.g. {"14": 2, "15": 730}. own_kills = the own_tier entry of it.
-- Rows older than the server setting kill_rate_history_days (default 365, 0 = keep all) are deleted once a day.
--
-- The server also creates this table the first time it needs it (KillRateHistory.EnsureSchema), so running this file is
-- optional and re-running it is harmless. Run it against the SHARD database.

CREATE TABLE IF NOT EXISTS `kill_rate_hour` (
  `character_id` INT UNSIGNED NOT NULL,
  `hour_utc` DATETIME NOT NULL,
  `run_id` BIGINT NOT NULL,
  `character_name` VARCHAR(64) NOT NULL,
  `own_tier` TINYINT UNSIGNED NOT NULL DEFAULT 0,
  `own_kills` INT NOT NULL DEFAULT 0,
  `total_kills` INT NOT NULL DEFAULT 0,
  `kills_by_tier` JSON NOT NULL,
  `melee_kills` INT NOT NULL DEFAULT 0,
  `missile_kills` INT NOT NULL DEFAULT 0,
  `war_kills` INT NOT NULL DEFAULT 0,
  `void_kills` INT NOT NULL DEFAULT 0,
  `pet_kills` INT NOT NULL DEFAULT 0,
  `other_kills` INT NOT NULL DEFAULT 0,
  `first_kill` DATETIME NOT NULL,
  `last_kill` DATETIME NOT NULL,
  `partial` TINYINT(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`character_id`, `hour_utc`, `run_id`),
  KEY `ix_kill_rate_hour_hour` (`hour_utc`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Staff kill rates: T11+ kills per character per UTC hour, one row per server run (KillRateHistory)';
