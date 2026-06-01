USE `ace_auth`;
DROP procedure IF EXISTS `TopAttributes`;
DELIMITER $$ USE `ace_auth` $$ CREATE PROCEDURE `TopAttributes` () BEGIN
-- Sums primary attribute ranks purchased (types 1-6), using level_From_C_P.
-- CP spent is very large and confusing for a leaderboard.
-- Excludes characters with PropertyBool ExcludeFromLeaderboards (9011) on biota_properties_bool.
select (
        COALESCE((select level_From_C_P from ace_shard.biota_properties_attribute where object_Id = c.id and type = 1), 0) +
        COALESCE((select level_From_C_P from ace_shard.biota_properties_attribute where object_Id = c.id and type = 2), 0) +
        COALESCE((select level_From_C_P from ace_shard.biota_properties_attribute where object_Id = c.id and type = 3), 0) +
        COALESCE((select level_From_C_P from ace_shard.biota_properties_attribute where object_Id = c.id and type = 4), 0) +
        COALESCE((select level_From_C_P from ace_shard.biota_properties_attribute where object_Id = c.id and type = 5), 0) +
        COALESCE((select level_From_C_P from ace_shard.biota_properties_attribute where object_Id = c.id and type = 6), 0)
    ) as 'Score',
    c.account_Id as 'Account',
    c.name as 'Character',
    c.id as 'LeaderboardID'
from ace_shard.character c
    left join ace_shard.biota_properties_bool b on b.object_id = c.id
    and b.type = 9011
where c.is_Deleted = 0
    and (
        b.value is null
        or b.value = 0
    )
having Score > 0
order by Score desc
LIMIT 25;
END $$ DELIMITER;
