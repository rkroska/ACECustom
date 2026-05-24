/* Set spawned combat pet Level (PropertyInt 25) to match summon tier.
 * Tier pets 787802001–787802072 clone capstone-200 pets (Level 200); this corrects display/identification only —
 * combat damage still comes from weenie_properties_body_part d_Val (scaled in 2026-05-12-00-Combat-Pet-Tiers-250-300.sql).
 */
START TRANSACTION;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
SELECT `class_Id`, 25,
  CASE
    WHEN `class_Id` BETWEEN 787802037 AND 787802072 THEN 300
    ELSE 250
  END
FROM `weenie`
WHERE `class_Id` BETWEEN 787802001 AND 787802072
ON DUPLICATE KEY UPDATE `value` = VALUES(`value`);

COMMIT;
