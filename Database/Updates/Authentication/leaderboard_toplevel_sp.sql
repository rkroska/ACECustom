USE `ace_auth`;
DROP procedure IF EXISTS `TopLevel`;

DELIMITER $$
USE `ace_auth`$$
CREATE PROCEDURE `TopLevel` ()
BEGIN
    /* PropertyInt.Level = 25. Include plussed rows; respect ExcludeFromLeaderboards (9011). */
    SELECT i.value AS 'Score',
           c.account_Id AS 'Account',
           c.name AS 'Character',
           c.id AS 'LeaderboardID'
    FROM ace_shard.character c
    INNER JOIN ace_shard.biota_properties_int i ON i.object_id = c.id AND i.type = 25
    LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
    WHERE c.is_Deleted = 0
      AND (b.value IS NULL OR b.value = 0)
    ORDER BY i.value DESC
    LIMIT 25;
END$$

DELIMITER ;
