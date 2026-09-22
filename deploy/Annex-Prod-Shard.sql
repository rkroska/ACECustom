/* =====================================================================================
   Pet Breeding / Ruggan's Annex - PROD SHARD SCRIPT for ace_shard
   Run with ace_shard selected (section 0 refuses to run anywhere else).

   WHAT IT DOES
     1. Creates pet_name_requests (the @pet-name queue the web portal reviews), if absent.
     2. Writes the breeding settings prod needs. Stored values override the code defaults
        (CLAUDE.md), so the location MUST be written here: the code default still points at
        the old motel (0x013A variation 3).
     3. Pins every TEST-ONLY switch to off, so no test value can leak into prod.
     4. Shows, at the end, the settings breeding depends on that this script does NOT change.

   Everything else (mutation rates, cooldowns, charges, maturity) is left at the code
   default on purpose - see deploy/PROD_DEPLOY_PLAN.md for the full table.

   RE-RUNNABLE, and nothing is dropped. Run it with the server stopped; a running server
   also picks the settings up by itself within 5 minutes.
   All text is 7-bit ASCII with LF line endings, per CLAUDE.md.
   ===================================================================================== */

SET @__old_safe_updates = @@SQL_SAFE_UPDATES;
SET SQL_SAFE_UPDATES = 0;

/* 0. SCHEMA GUARD. `biota` and `character` exist only in the shard. If they are absent this
      fails with an error whose table name says what went wrong. */
SET @__is_shard = (
    SELECT COUNT(*) FROM information_schema.tables
    WHERE table_schema = DATABASE() AND table_name IN ('biota', 'character'));
SET @__guard = IF(@__is_shard = 2, 'DO 0',
    'SELECT 1 FROM `RUN_THIS_AGAINST_ace_shard_NOT_ace_world`');
PREPARE __guard_stmt FROM @__guard;
EXECUTE __guard_stmt;
DEALLOCATE PREPARE __guard_stmt;

/* 1. Pet name request queue (from Database/Updates/Shard/2026-09-15-00-Pet-Name-Requests.sql). */
CREATE TABLE IF NOT EXISTS `pet_name_requests` (
  `id`               bigint       NOT NULL AUTO_INCREMENT,
  `character_id`     bigint       NOT NULL,
  `character_name`   varchar(64)  NOT NULL,
  `pet_guid`         int unsigned NOT NULL,
  `old_name`         varchar(64)  NOT NULL,
  `requested_name`   varchar(64)  NOT NULL,
  `status`           tinyint      NOT NULL DEFAULT 0,
  `review_note`      varchar(255) NULL,
  `created_at`       datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `reviewed_at`      datetime     NULL,
  `reviewed_by`      varchar(64)  NULL,
  PRIMARY KEY (`id`),
  KEY `IX_pet_name_req_status` (`status`, `created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- An older copy of the table may predate the index; CREATE INDEX has no IF NOT EXISTS.
SET @idx_exists = (
    SELECT COUNT(*) FROM information_schema.statistics
     WHERE table_schema = DATABASE() AND table_name = 'pet_name_requests'
       AND index_name = 'IX_pet_name_req_status');
SET @sql = IF(@idx_exists = 0,
    'CREATE INDEX `IX_pet_name_req_status` ON `pet_name_requests` (`status`, `created_at`)', 'DO 0');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

/* 2. Where breeding happens: the annex, landblock 0x0106 variation 2. REQUIRED. */
INSERT INTO `config_properties_long` (`key`, `value`, `description`) VALUES
  ('pet_breeding_allowed_landblock', 262, 'Where breeding is allowed. 262 = 0x0106, the Ruggan''s Annex landblock.'),
  ('pet_breeding_allowed_variant',   2,   'Landblock variation where breeding is allowed. 2 = the annex.')
ON DUPLICATE KEY UPDATE `value` = VALUES(`value`), `description` = VALUES(`description`);

/* 3. Feature switches. The guardian line is a DECISION - see the plan, question Q2. */
INSERT INTO `config_properties_boolean` (`key`, `value`, `description`) VALUES
  ('pet_breeding_enabled',           1, 'If TRUE, enables pet breeding in the annex.'),
  ('pet_breeding_guardian_enabled',  1, 'If TRUE, a mutation breed spawns a mating guardian the parents must kill. Fenwick''s tutorial describes it.'),
  -- TEST-ONLY switches: pinned off.
  ('pet_breeding_force_mutation',    0, 'TEST ONLY. If TRUE, every breed mutates.'),
  ('pet_breeding_bypass_male_charges', 0, 'TEST ONLY. If TRUE, stud charges are not checked or spent.'),
  ('pet_breeding_bypass_female_cooldown', 0, 'TEST ONLY. If TRUE, the dam recovery cooldown is skipped.'),
  ('pet_breeding_verbose_logging',   0, 'If TRUE, logs every dance trigger. Spammable by players.'),
  ('pet_trace',                      0, 'If TRUE, writes the full [PetTrace] session trace. Debug only.'),
  ('pet_visual_packet_debug',        0, 'If TRUE, logs every creature ObjDesc. Very noisy, debug only.')
ON DUPLICATE KEY UPDATE `value` = VALUES(`value`), `description` = VALUES(`description`);

/* 4. Mutation decay (plan Q3): a gentle slope. Each stat mutation a pet already carries costs about
      two more litters for the next one; the 2% floor is reached at about 15 mutations. */
INSERT INTO `config_properties_double` (`key`, `value`, `description`) VALUES
  ('pet_breeding_mutation_decay_rate', 0.1, 'Decay factor per inherited mutation: chance = base / (1 + decay x mutations), floored at pet_breeding_mutation_min_floor. 0.1 = gentle diminishing returns.')
ON DUPLICATE KEY UPDATE `value` = VALUES(`value`), `description` = VALUES(`description`);

SET SQL_SAFE_UPDATES = @__old_safe_updates;

/* =====================================================================================
   VERIFICATION - nothing below changes anything.
   ===================================================================================== */

/* V1. Expect table_Present = 1, index_Present = 1. */
SELECT
    (SELECT COUNT(*) FROM information_schema.tables
      WHERE table_schema = DATABASE() AND table_name = 'pet_name_requests') AS table_Present,
    (SELECT COUNT(DISTINCT index_name) FROM information_schema.statistics
      WHERE table_schema = DATABASE() AND table_name = 'pet_name_requests'
        AND index_name = 'IX_pet_name_req_status') AS index_Present;

/* V2. Expect landblock 262 and variant 2. */
SELECT `key`, `value` FROM `config_properties_long`
WHERE `key` IN ('pet_breeding_allowed_landblock', 'pet_breeding_allowed_variant') ORDER BY `key`;

/* V3. EVERY stored pet breeding / maturity / sex-icon setting. Anything here other than the
       lines this script wrote overrides a code default - read each one. */
SELECT 'boolean' AS kind, `key`, CAST(`value` AS UNSIGNED) AS value FROM `config_properties_boolean`
 WHERE `key` LIKE 'pet\_breeding\_%' OR `key` LIKE 'pet\_maturity\_%' OR `key` LIKE 'pet\_sex\_%' OR `key` IN ('pet_trace','pet_visual_packet_debug')
UNION ALL SELECT 'long',   `key`, `value` FROM `config_properties_long`
 WHERE `key` LIKE 'pet\_breeding\_%' OR `key` LIKE 'pet\_maturity\_%' OR `key` LIKE 'pet\_sex\_%'
UNION ALL SELECT 'double', `key`, `value` FROM `config_properties_double`
 WHERE `key` LIKE 'pet\_breeding\_%' OR `key` LIKE 'pet\_maturity\_%'
UNION ALL SELECT 'string', `key`, `value` FROM `config_properties_string`
 WHERE `key` LIKE 'pet\_maturity\_%'
ORDER BY `key`;

/* V4. Settings breeding DEPENDS ON that this script deliberately does not touch.
       pet_bond_enabled must be 1: breeding needs bond 100 and bond only grows while it is on.
       No row = the code default (pet_bond_enabled FALSE, pet_potency_enabled FALSE). */
SELECT 'pet_bond_enabled' AS `key`,
       (SELECT CAST(`value` AS UNSIGNED) FROM `config_properties_boolean` WHERE `key` = 'pet_bond_enabled') AS stored_value,
       'must be 1 or nobody can ever breed (min bond 100)' AS why
UNION ALL
SELECT 'pet_potency_enabled',
       (SELECT CAST(`value` AS UNSIGNED) FROM `config_properties_boolean` WHERE `key` = 'pet_potency_enabled'),
       'potency mutations (3% of breeds) only matter while this is 1';
