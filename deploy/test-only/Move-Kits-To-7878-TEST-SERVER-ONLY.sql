/* =====================================================================================
   TEST SERVER ONLY - move the pet kits from 98760399-98760401 to 78780258-78780260
   Run AFTER Database/Updates/World/2026-09-09-01-Pet-Tailoring-and-Neutering-Kits.sql (which creates
   the new 78780258-78780260 weenies). Uses explicit schema names: ace_world and ace_shard.

   NEVER RUN ON PRODUCTION. On prod, 98760399-98760401 are other content (Tyrannical Drudge Gen,
   Realm of Woe, Doriathazaar). Every statement below is additionally guarded by the KIT'S NAME, so on a
   database where those ids are not the kits nothing matches and nothing changes.

   Log the owning characters out first: an online character's in-memory kit would be saved back with
   the old id. Safe to re-run.
   ===================================================================================== */

SET @__old_safe_updates = @@SQL_SAFE_UPDATES;
SET SQL_SAFE_UPDATES = 0;
START TRANSACTION;

-- Which old ids really are kits on THIS server (name-guarded map old -> new).
DROP TEMPORARY TABLE IF EXISTS `__kit_map`;
CREATE TEMPORARY TABLE `__kit_map` (`old_id` INT UNSIGNED PRIMARY KEY, `new_id` INT UNSIGNED NOT NULL);
INSERT INTO `__kit_map` (`old_id`, `new_id`)
SELECT s.`object_Id`,
       CASE s.`object_Id` WHEN 98760399 THEN 78780258 WHEN 98760400 THEN 78780259 WHEN 98760401 THEN 78780260 END
FROM `ace_world`.`weenie_properties_string` s
WHERE s.`type` = 1
  AND ((s.`object_Id` = 98760399 AND s.`value` = 'Pet Neutering Kit')
    OR (s.`object_Id` = 98760400 AND s.`value` = 'Pet Tailoring Kit')
    OR (s.`object_Id` = 98760401 AND s.`value` = 'Pet Tailoring Kit (Filled)'));

-- Items players already own.
UPDATE `ace_shard`.`biota` b JOIN `__kit_map` m ON b.`weenie_Class_Id` = m.`old_id`
SET b.`weenie_Class_Id` = m.`new_id`;

-- Ivo's shop (and any other create list that stocks them).
UPDATE `ace_world`.`weenie_properties_create_list` c JOIN `__kit_map` m ON c.`weenie_Class_Id` = m.`old_id`
SET c.`weenie_Class_Id` = m.`new_id`;

-- The old kit weenies themselves (FKs cascade to their property rows).
DELETE w FROM `ace_world`.`weenie` w JOIN `__kit_map` m ON w.`class_Id` = m.`old_id`;

COMMIT;
SET SQL_SAFE_UPDATES = @__old_safe_updates;

/* V1. Expect 78780258-78780260 named as kits, and no kit names left on 98760399-98760401. */
SELECT w.`class_Id`, s.`value` FROM `ace_world`.`weenie` w
JOIN `ace_world`.`weenie_properties_string` s ON s.`object_Id` = w.`class_Id` AND s.`type` = 1
WHERE w.`class_Id` IN (98760399, 98760400, 98760401, 78780258, 78780259, 78780260);

/* V2. Ivo's shop: expect 78780258 and 78780259, and no 9876039x/9876040x. */
SELECT `weenie_Class_Id` FROM `ace_world`.`weenie_properties_create_list`
WHERE `object_Id` = 78780201 AND `destination_Type` = 4 ORDER BY `weenie_Class_Id`;

DROP TEMPORARY TABLE IF EXISTS `__kit_map`;
