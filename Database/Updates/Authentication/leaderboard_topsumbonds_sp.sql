-- REFERENCE ONLY: runtime uses LeaderboardInlineSql.TopSumBonds in ACE.Database (portal + /top sumbond).
-- Kept for manual DBA review / optional MySQL install; not required for the server to run.

USE `ace_auth`;
DROP procedure IF EXISTS `TopSumBonds`;

DELIMITER $$
USE `ace_auth`$$
CREATE PROCEDURE `TopSumBonds` ()
BEGIN
    SELECT SUM(COALESCE(bond.value, 1)) AS 'Score',
           c.account_Id AS 'Account',
           c.name AS 'Character',
           c.id AS 'LeaderboardID'
    FROM ace_shard.biota_properties_int64 att
    INNER JOIN ace_shard.character c ON c.id = CAST(att.value AS UNSIGNED) AND att.type = 9052
    INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0
        AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
    LEFT JOIN ace_shard.biota_properties_int bond ON bond.object_id = att.object_id AND bond.type = 9053
    LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
    WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
    GROUP BY c.id, c.account_Id, c.name
    HAVING SUM(COALESCE(bond.value, 1)) > 0
    ORDER BY Score DESC, c.name ASC
    LIMIT 25;
END$$

DELIMITER ;
