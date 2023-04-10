ALTER TABLE `ace_shard`.`biota_properties_attribute` 
CHANGE COLUMN `c_P_Spent` `c_P_Spent` DOUBLE UNSIGNED NOT NULL DEFAULT '0' COMMENT 'XP spent on this attribute' ;

ALTER TABLE `ace_shard`.`biota_properties_attribute_2nd` 
CHANGE COLUMN `c_P_Spent` `c_P_Spent` DOUBLE UNSIGNED NOT NULL DEFAULT '0' COMMENT 'XP spent on this attribute' ;
