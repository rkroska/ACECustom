/* =====================================================================================
   Pet Breeding - CONSOLIDATED SHARD PATCH
   Branch: feature/pet-breeding-motel
   Target database: ace_shard
   Generated from Database/Updates/Shard/2026-09-15-00-Pet-Name-Requests.sql.

   WHAT THIS CREATES
     pet_name_requests - one row per pending / reviewed pet rename request.
       Written by  @pet-name              (PlayerCommands.HandlePetName)
       Read/updated by the web portal     (PetNamingController, page /pet-names)

   This is the ONLY shard schema change on the branch. Everything else the branch
   stores lives in existing biota property tables (see docs/PET_BREEDING_DEVELOPER_GUIDE.md
   section 9) and needs no migration: the new property ids are just new rows in
   biota_properties_int / _float / _bool / _string / _d_i_d.

   RE-RUNNABLE
     Yes. CREATE TABLE IF NOT EXISTS, and the index is only added when it is absent,
     so an existing table with data is left alone.

   DESTRUCTIVE?
     No. Nothing here drops or alters an existing table, and no existing row is touched.

   All text is 7-bit ASCII with LF line endings, per CLAUDE.md.
   ===================================================================================== */

SET @__old_safe_updates = @@SQL_SAFE_UPDATES;
SET SQL_SAFE_UPDATES = 0;

/* -------------------------------------------------------------------------------------
   1. Pet name request queue
   ------------------------------------------------------------------------------------- */
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

/* -------------------------------------------------------------------------------------
   2. Add the status index if an older copy of the table predates it.
      CREATE INDEX has no IF NOT EXISTS in MySQL 5.7 / MariaDB, so it is guarded here.
      ------------------------------------------------------------------------------- */
SET @idx_exists = (
    SELECT COUNT(*) FROM information_schema.statistics
     WHERE table_schema = DATABASE()
       AND table_name   = 'pet_name_requests'
       AND index_name   = 'IX_pet_name_req_status');

SET @sql = IF(@idx_exists = 0,
    'CREATE INDEX `IX_pet_name_req_status` ON `pet_name_requests` (`status`, `created_at`)',
    'DO 0');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET SQL_SAFE_UPDATES = @__old_safe_updates;

/* =====================================================================================
   VERIFICATION - nothing below changes any data.
   ===================================================================================== */

/* V1. The table's columns. Expect 11 rows. */
SELECT ordinal_position, column_name, column_type, is_nullable, column_default
FROM information_schema.columns
WHERE table_schema = DATABASE() AND table_name = 'pet_name_requests'
ORDER BY ordinal_position;

/* V2. Indexes. Expect PRIMARY on (id) and IX_pet_name_req_status on (status, created_at). */
SELECT index_name, seq_in_index, column_name, non_unique
FROM information_schema.statistics
WHERE table_schema = DATABASE() AND table_name = 'pet_name_requests'
ORDER BY index_name, seq_in_index;

/* V3. Row counts by status (0 = pending, 1 = approved, 2 = denied).
       On a first install this is empty, which is correct. */
SELECT
    CASE `status` WHEN 0 THEN 'pending' WHEN 1 THEN 'approved' WHEN 2 THEN 'denied'
         ELSE CONCAT('unknown(', `status`, ')') END AS status_Name,
    COUNT(*) AS rows_Found
FROM `pet_name_requests`
GROUP BY `status`
ORDER BY `status`;

/* V4. Blunt check. Expect table_Present = 1 and index_Present = 1. */
SELECT
    (SELECT COUNT(*) FROM information_schema.tables
      WHERE table_schema = DATABASE() AND table_name = 'pet_name_requests')   AS table_Present,
    (SELECT COUNT(DISTINCT index_name) FROM information_schema.statistics
      WHERE table_schema = DATABASE() AND table_name = 'pet_name_requests'
        AND index_name = 'IX_pet_name_req_status')                            AS index_Present;
