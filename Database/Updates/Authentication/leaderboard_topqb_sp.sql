USE `ace_auth`;
DROP procedure IF EXISTS `ace_auth`.`TopQuestBonus`;

DELIMITER $$
USE `ace_auth`$$
CREATE PROCEDURE `TopQuestBonus`()
BEGIN
    /* Account-level QB score. Omit account if ANY non-deleted character has ExcludeFromLeaderboards (bool 9011). */
    SELECT (COUNT(*) + (SELECT COUNT(*) FROM ace_auth.account_quest a2 WHERE a.accountId = a2.accountId AND a2.num_Times_Completed >= 1)) AS 'Score',
           a.accountId AS 'Account',
           (SELECT c.name
            FROM ace_shard.character c
            WHERE c.account_Id = a.accountId
              AND c.is_Deleted = 0
            ORDER BY c.total_Logins DESC
            LIMIT 1) AS 'Character',
           a.accountId AS 'LeaderboardID'
    FROM ace_auth.account_quest a
    GROUP BY a.accountId
    HAVING `Character` IS NOT NULL
       AND NOT EXISTS (
           SELECT 1
           FROM ace_shard.character cex
                    INNER JOIN ace_shard.biota_properties_bool bx
                               ON bx.object_id = cex.id AND bx.type = 9011 AND bx.value <> 0
           WHERE cex.account_Id = a.accountId
             AND cex.is_Deleted = 0
       )
    ORDER BY COUNT(*) DESC
    LIMIT 25;
END$$

DELIMITER ;
