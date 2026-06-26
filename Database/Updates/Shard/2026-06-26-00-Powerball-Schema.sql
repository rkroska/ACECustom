-- Powerball Lottery System Schema
-- Tables are also created at runtime by PowerballManager.EnsureTablesExist().
-- This script is provided for manual deployment or reference.

CREATE TABLE IF NOT EXISTS `powerball_state` (
  `id`              int     NOT NULL DEFAULT 1,
  `current_draw_id` int     NOT NULL DEFAULT 1,
  `jackpot_pool`    bigint  NOT NULL DEFAULT 0,
  `next_draw_time`  datetime DEFAULT NULL,
  `last_draw_time`  datetime DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `powerball_state` (`id`, `current_draw_id`, `jackpot_pool`)
SELECT 1, 1, 0
WHERE NOT EXISTS (SELECT 1 FROM `powerball_state` LIMIT 1);

CREATE TABLE IF NOT EXISTS `powerball_tickets` (
  `id`             bigint       NOT NULL AUTO_INCREMENT,
  `draw_id`        int          NOT NULL,
  `character_id`   bigint       NOT NULL,
  `character_name` varchar(64)  NOT NULL,
  `n1`             tinyint      NOT NULL,
  `n2`             tinyint      NOT NULL,
  `n3`             tinyint      NOT NULL,
  `pb`             tinyint      NOT NULL,
  `is_test`        tinyint(1)   NOT NULL DEFAULT 0,
  `created_at`     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `IX_pb_tickets_draw_char` (`draw_id`, `character_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `powerball_history` (
  `id`           bigint    NOT NULL AUTO_INCREMENT,
  `draw_id`      int       NOT NULL,
  `drawn_n1`     tinyint   NOT NULL,
  `drawn_n2`     tinyint   NOT NULL,
  `drawn_n3`     tinyint   NOT NULL,
  `drawn_pb`     tinyint   NOT NULL,
  `jackpot_pool` bigint    NOT NULL,
  `ticket_count` int       NOT NULL DEFAULT 0,
  `rollover`     bigint    NOT NULL DEFAULT 0,
  `winner_count` int       NOT NULL DEFAULT 0,
  `jackpot_won`  tinyint(1) NOT NULL DEFAULT 0,
  `draw_time`    datetime  NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `UQ_pb_history_draw_id` (`draw_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
