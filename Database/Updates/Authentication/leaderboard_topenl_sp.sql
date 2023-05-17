
USE `ace_auth`;
DROP procedure IF EXISTS `ace_auth`.`TopEnlightenment`;
;

DELIMITER $$
USE `ace_auth`$$
CREATE PROCEDURE `TopEnlightenment`()
BEGIN
    select i.value as 'Score', c.account_Id as 'Account', c.Name as 'Character', c.id as 'LeaderboardID'
    from ace_shard.character c join ace_shard.biota_properties_int i on i.object_id = c.id and i.type = 390
    where is_Plussed = 0 and is_Deleted = 0
    order by i.value desc
    LIMIT 25;
END$$

DELIMITER ;
;
