ALTER TABLE `ace_world`.`weenie_properties_attribute` 
CHANGE COLUMN `c_P_Spent` `c_P_Spent` BIGINT UNSIGNED NOT NULL COMMENT 'XP spent on this attribute' ;

ALTER TABLE `ace_world`.`weenie_properties_attribute_2nd` 
CHANGE COLUMN `c_P_Spent` `c_P_Spent` BIGINT UNSIGNED NOT NULL COMMENT 'XP spent on this attribute' ;
