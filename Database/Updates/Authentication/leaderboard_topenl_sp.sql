USE `ace_auth`;
DROP procedure IF EXISTS `ace_auth`.`TopEnlightenment`;
;

DELIMITER $$
USE `ace_auth`$$
CREATE PROCEDURE `TopEnlightenment`()
BEGIN
    /* Enlightenment = PropertyInt 390 (biota_properties_int).
       Include plussed (+) character rows so prestige / variant characters count (they hold the property).
       Omit characters flagged ExcludeFromLeaderboards (PropertyBool 9011), same as TopLum / TopBank. */
    SELECT i.value AS 'Score',
           c.account_Id AS 'Account',
           c.name AS 'Character',
           c.id AS 'LeaderboardID'
    FROM ace_shard.character c
    INNER JOIN ace_shard.biota_properties_int i ON i.object_id = c.id AND i.type = 390
    LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
    WHERE c.is_Deleted = 0
      AND (b.value IS NULL OR b.value = 0)
    ORDER BY i.value DESC
    LIMIT 25;
END$$

DELIMITER ;
;
