USE `ace_auth`;
DROP procedure IF EXISTS `TopTitles`;

DELIMITER $$
USE `ace_auth`$$
CREATE PROCEDURE `TopTitles` ()
BEGIN
    SELECT i.value AS 'Score',
           c.account_Id AS 'Account',
           c.name AS 'Character',
           c.id AS 'LeaderboardID'
    from ace_shard.character c
    inner join ace_auth.account a on a.accountId = c.account_Id and a.accessLevel = 0 and (a.ban_Expire_Time is null or a.ban_Expire_Time <= utc_timestamp())
    INNER JOIN ace_shard.biota_properties_int i ON i.object_id = c.id AND i.type = 262
    LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
    LEFT JOIN ace_shard.biota_properties_bool m ON m.object_id = c.id AND m.type = 131
    WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0) AND (m.value IS NULL OR m.value = 0)
    ORDER BY i.value DESC
    LIMIT 25;
END$$

DELIMITER ;
