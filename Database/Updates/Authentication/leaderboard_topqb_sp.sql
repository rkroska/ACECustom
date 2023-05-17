USE `ace_auth`;
DROP procedure IF EXISTS `TopQuestBonus`;

USE `ace_auth`;
DROP procedure IF EXISTS `ace_auth`.`TopQuestBonus`;
;

DELIMITER $$
USE `ace_auth`$$
CREATE PROCEDURE `TopQuestBonus`()
BEGIN
	SELECT (count(*) + (SELECT count(*) from ace_auth.account_quest a2 where a.accountId = a2.accountId and a2.num_times_completed >=1))
    as 'Score', a.accountId as 'Account', 
		(SELECT name from ace_shard.character c where c.account_id = a.accountId order by total_logins desc limit 1) as 'Character', a.accountId AS 'LeaderboardID'
    from ace_auth.account_quest a
    group by a.accountId
    order by count(*) desc
    LIMIT 25; 
END$$

DELIMITER ;
;