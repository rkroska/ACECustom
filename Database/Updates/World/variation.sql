ALTER TABLE `ace_world`.`weenie_properties_position` 
ADD COLUMN `variation_Id` INT NULL AFTER `angles_Z`;

ALTER TABLE `ace_world`.`landblock_instance` 
ADD COLUMN `variation_Id` INT NULL AFTER `last_Modified`;

ALTER TABLE `ace_shard`.`biota_properties_position` 
ADD COLUMN `variation_Id` INT NULL AFTER `angles_Z`;