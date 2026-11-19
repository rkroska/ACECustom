-- Zone Control change log: who changed which stat, from what to what, in game or on the portal.
-- old_curve / new_curve hold the whole stat curve as JSON (NULL = not set), so a revert restores it exactly.
-- kind = 'stat' (one stat, old/new curve) or 'section' (a whole zone, tier Default or server-wide list, old/new JSON);
-- reason = why, as typed by the editor; batch_id = rows saved together; section = which part (kind 'section' only).
--
-- The server also creates this table, and adds any missing column, the first time it needs it
-- (ZoneControlChangeLog.EnsureSchema), so running this file is optional. Safe to run with the server up; re-running is harmless.
-- Run it against the SHARD database: the column checks use DATABASE(), so select the shard schema first (Workbench keeps
-- whichever schema was selected last).

CREATE TABLE IF NOT EXISTS `zone_control_change_log` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `changed_at` DATETIME NOT NULL,
  `actor` VARCHAR(64) NOT NULL,
  `source` VARCHAR(16) NOT NULL,
  `zone_name` VARCHAR(128) NULL,
  `variation` INT NULL,
  `wcid` INT UNSIGNED NULL,
  `rank_key` VARCHAR(16) NOT NULL,
  `stat` VARCHAR(128) NOT NULL,
  `old_curve` TEXT NULL,
  `new_curve` TEXT NULL,
  `reverts_id` BIGINT NULL,
  `kind` VARCHAR(16) NOT NULL DEFAULT 'stat',
  `reason` VARCHAR(255) NULL,
  `batch_id` VARCHAR(64) NULL,
  `section` VARCHAR(160) NULL,
  `old_json` MEDIUMTEXT NULL,
  `new_json` MEDIUMTEXT NULL,
  PRIMARY KEY (`id`),
  KEY `ix_zone_name` (`zone_name`, `id`),
  KEY `ix_variation` (`variation`, `id`),
  KEY `ix_batch` (`batch_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- A table made from an earlier version of this file lacks some of kind / reason / batch_id / section / old_json / new_json. MySQL has no ADD COLUMN IF NOT EXISTS,
-- so each ALTER below runs only when information_schema says its column is missing.

SET @has := (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'zone_control_change_log' AND column_name = 'kind');
SET @sql := IF(@has = 0, 'ALTER TABLE `zone_control_change_log` ADD COLUMN `kind` VARCHAR(16) NOT NULL DEFAULT ''stat'' AFTER `reverts_id`', 'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @has := (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'zone_control_change_log' AND column_name = 'reason');
SET @sql := IF(@has = 0, 'ALTER TABLE `zone_control_change_log` ADD COLUMN `reason` VARCHAR(255) NULL AFTER `kind`', 'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @has := (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'zone_control_change_log' AND column_name = 'batch_id');
SET @sql := IF(@has = 0, 'ALTER TABLE `zone_control_change_log` ADD COLUMN `batch_id` VARCHAR(64) NULL AFTER `reason`, ADD KEY `ix_batch` (`batch_id`)', 'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @has := (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'zone_control_change_log' AND column_name = 'section');
SET @sql := IF(@has = 0, 'ALTER TABLE `zone_control_change_log` ADD COLUMN `section` VARCHAR(160) NULL AFTER `batch_id`', 'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @has := (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'zone_control_change_log' AND column_name = 'old_json');
SET @sql := IF(@has = 0, 'ALTER TABLE `zone_control_change_log` ADD COLUMN `old_json` MEDIUMTEXT NULL AFTER `section`', 'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @has := (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'zone_control_change_log' AND column_name = 'new_json');
SET @sql := IF(@has = 0, 'ALTER TABLE `zone_control_change_log` ADD COLUMN `new_json` MEDIUMTEXT NULL AFTER `old_json`', 'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
