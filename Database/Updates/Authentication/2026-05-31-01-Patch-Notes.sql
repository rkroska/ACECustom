USE `ace_auth`;

CREATE TABLE IF NOT EXISTS `patch_notes` (
  `id` INT NOT NULL AUTO_INCREMENT,
  `slug` VARCHAR(128) NOT NULL,
  `title` VARCHAR(255) NOT NULL,
  `summary` VARCHAR(1000) NULL,
  `body` MEDIUMTEXT NOT NULL,
  `status` VARCHAR(16) NOT NULL DEFAULT 'draft',
  `published_at` DATETIME(6) NULL,
  `published_by_account_id` INT UNSIGNED NULL,
  `post_to_discord` TINYINT(1) NOT NULL DEFAULT 1,
  `discord_message_id` BIGINT NULL,
  `created_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`),
  UNIQUE KEY `UX_patch_notes_slug` (`slug`),
  KEY `IX_patch_notes_status_published_at` (`status`, `published_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
