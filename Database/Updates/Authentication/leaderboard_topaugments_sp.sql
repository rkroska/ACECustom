USE `ace_auth`;
DROP procedure IF EXISTS `TopAugments`;
DELIMITER $$ USE `ace_auth` $$ CREATE PROCEDURE `TopAugments` () BEGIN
-- Sums luminance skill-style aug int64 purchase counts (9007-9011, 9016-9018, 9022-9026).
-- Note: biota_properties_bool type 9011 is ExcludeFromLeaderboards; int64 9011 is LumAugWarCount.
select (
        COALESCE((select value from ace_shard.biota_properties_int64 where object_id = c.id and type = 9007), 0) +
        COALESCE((select value from ace_shard.biota_properties_int64 where object_id = c.id and type = 9008), 0) +
        COALESCE((select value from ace_shard.biota_properties_int64 where object_id = c.id and type = 9009), 0) +
        COALESCE((select value from ace_shard.biota_properties_int64 where object_id = c.id and type = 9010), 0) +
        COALESCE((select value from ace_shard.biota_properties_int64 where object_id = c.id and type = 9011), 0) +
        COALESCE((select value from ace_shard.biota_properties_int64 where object_id = c.id and type = 9016), 0) +
        COALESCE((select value from ace_shard.biota_properties_int64 where object_id = c.id and type = 9017), 0) +
        COALESCE((select value from ace_shard.biota_properties_int64 where object_id = c.id and type = 9018), 0) +
        COALESCE((select value from ace_shard.biota_properties_int64 where object_id = c.id and type = 9022), 0) +
        COALESCE((select value from ace_shard.biota_properties_int64 where object_id = c.id and type = 9023), 0) +
        COALESCE((select value from ace_shard.biota_properties_int64 where object_id = c.id and type = 9024), 0) +
        COALESCE((select value from ace_shard.biota_properties_int64 where object_id = c.id and type = 9025), 0) +
        COALESCE((select value from ace_shard.biota_properties_int64 where object_id = c.id and type = 9026), 0)
    ) as 'Score',
    c.account_Id as 'Account',
    c.name as 'Character',
    c.id as 'LeaderboardID'
from ace_shard.character c
    left join ace_shard.biota_properties_bool b on b.object_id = c.id
    and b.type = 9011
    left join ace_shard.biota_properties_bool m on m.object_id = c.id
    and m.type = 131
where c.is_Deleted = 0
    and (
        b.value is null
        or b.value = 0
    )
    and (
        m.value is null
        or m.value = 0
    )
having Score > 0
order by Score desc
LIMIT 25;
END $$
DELIMITER ;
