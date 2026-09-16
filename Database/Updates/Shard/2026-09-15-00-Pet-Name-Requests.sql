-- Pet naming / pedigree portal approval flow: players request a pet name change via
-- @pet-name, staff approve or deny it from the web portal.

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
