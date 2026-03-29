-- Enlightenment tier bands: target enlightenment T = current enlightenment + 1.
-- Partition [1, ∞): no gaps/overlaps; only the last row uses max_target_enl NULL.
-- Luminance: ceil(T * lum_base_per_target * multiplier), where multiplier = 1 + lum_step_increment * steps
--   when lum_step_* are set, with over = T - lum_step_anchor, steps = max(0, (over - 1) / lum_step_every).
-- Item stacks required when item_wcid set: max(0, T - item_count_target_minus).
-- Edit row 6 (325+) for new cost/item; add rows for future breakpoints (350, 700, …).

CREATE TABLE IF NOT EXISTS `config_enlightenment_tier` (
  `id` int NOT NULL AUTO_INCREMENT,
  `sort_order` int NOT NULL DEFAULT 0,
  `min_target_enl` int NOT NULL,
  `max_target_enl` int DEFAULT NULL COMMENT 'NULL = open-ended (final row only)',
  `lum_base_per_target` bigint NOT NULL DEFAULT 0,
  `lum_step_anchor` int DEFAULT NULL,
  `lum_step_every` int DEFAULT NULL,
  `lum_step_increment` decimal(10,4) DEFAULT NULL COMMENT 'per step added to 1.0 base multiplier',
  `item_wcid` int DEFAULT NULL,
  `item_count_target_minus` int DEFAULT NULL COMMENT 'required stacks = max(0, T - value)',
  `item_label` varchar(80) DEFAULT NULL,
  `quest_stamp` varchar(100) DEFAULT NULL,
  `quest_failure_message` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `IX_config_enlightenment_tier_min` (`min_target_enl`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `config_enlightenment_tier`
  (`sort_order`, `min_target_enl`, `max_target_enl`, `lum_base_per_target`, `lum_step_anchor`, `lum_step_every`, `lum_step_increment`, `item_wcid`, `item_count_target_minus`, `item_label`, `quest_stamp`, `quest_failure_message`)
SELECT * FROM (
  SELECT 1 AS `sort_order`, 1 AS `min_target_enl`, 5 AS `max_target_enl`, CAST(0 AS SIGNED) AS `lum_base_per_target`, NULL AS `lum_step_anchor`, NULL AS `lum_step_every`, NULL AS `lum_step_increment`, NULL AS `item_wcid`, NULL AS `item_count_target_minus`, NULL AS `item_label`, NULL AS `quest_stamp`, NULL AS `quest_failure_message`
  UNION ALL SELECT 2, 6, 50, 0, NULL, NULL, NULL, 300000, 5, 'Enlightenment Tokens', NULL, NULL
  UNION ALL SELECT 3, 51, 150, 100000000, NULL, NULL, NULL, 300000, 5, 'Enlightenment Tokens', NULL, NULL
  UNION ALL SELECT 4, 151, 300, 1000000000, NULL, NULL, NULL, 90000217, 5, 'Enlightenment Medallions', 'ParagonEnlCompleted', 'You must have completed 50th Paragon to enlighten beyond level 150.'
  UNION ALL SELECT 5, 301, 324, 2000000000, 300, 50, 0.5000, 300101189, 5, 'Enlightenment Sigils', 'ParagonArmorCompleted', 'You must have completed 50th Armor Paragon to enlighten beyond level 300.'
  UNION ALL SELECT 6, 325, NULL, 2000000000, 300, 50, 0.5000, 300101189, 5, 'Enlightenment Sigils', 'ParagonArmorCompleted', 'You must have completed 50th Armor Paragon to enlighten beyond level 300.'
) AS `seed`
WHERE NOT EXISTS (SELECT 1 FROM `config_enlightenment_tier` LIMIT 1);
