USE `ace_auth`;
DROP procedure IF EXISTS `TopAugments`;
DELIMITER $$ USE `ace_auth` $$ CREATE PROCEDURE `TopAugments` () BEGIN
-- Sums luminance aug int64 purchase counts (9007-9011, 9016-9018, 9022-9026) plus int rating-style augs (333-345, 365).
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
        COALESCE((select value from ace_shard.biota_properties_int64 where object_id = c.id and type = 9026), 0) +
        COALESCE((select value from ace_shard.biota_properties_int where object_id = c.id and type = 333), 0) +
        COALESCE((select value from ace_shard.biota_properties_int where object_id = c.id and type = 334), 0) +
        COALESCE((select value from ace_shard.biota_properties_int where object_id = c.id and type = 335), 0) +
        COALESCE((select value from ace_shard.biota_properties_int where object_id = c.id and type = 336), 0) +
        COALESCE((select value from ace_shard.biota_properties_int where object_id = c.id and type = 337), 0) +
        COALESCE((select value from ace_shard.biota_properties_int where object_id = c.id and type = 338), 0) +
        COALESCE((select value from ace_shard.biota_properties_int where object_id = c.id and type = 339), 0) +
        COALESCE((select value from ace_shard.biota_properties_int where object_id = c.id and type = 340), 0) +
        COALESCE((select value from ace_shard.biota_properties_int where object_id = c.id and type = 341), 0) +
        COALESCE((select value from ace_shard.biota_properties_int where object_id = c.id and type = 342), 0) +
        COALESCE((select value from ace_shard.biota_properties_int where object_id = c.id and type = 343), 0) +
        COALESCE((select value from ace_shard.biota_properties_int where object_id = c.id and type = 344), 0) +
        COALESCE((select value from ace_shard.biota_properties_int where object_id = c.id and type = 345), 0) +
        COALESCE((select value from ace_shard.biota_properties_int where object_id = c.id and type = 365), 0)
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
END $$
DELIMITER ;
