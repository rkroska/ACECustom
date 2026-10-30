-- READ ONLY. Replace ace_shard / ace_world if production uses different names.
-- Export full text (not a GUI-truncated cell). No player/account data is selected.
SELECT `key`, `value`
FROM ace_shard.config_properties_string
WHERE `key` = 'zonecontrol_data';

SELECT 'boolean' AS setting_type, `key`, CAST(`value` AS CHAR) AS setting_value
FROM ace_shard.config_properties_boolean
WHERE LEFT(`key`, 3) = 'zc_' OR LEFT(`key`, 12) = 'zonecontrol_'
UNION ALL
SELECT 'double', `key`, CAST(`value` AS CHAR)
FROM ace_shard.config_properties_double
WHERE LEFT(`key`, 3) = 'zc_' OR LEFT(`key`, 12) = 'zonecontrol_'
UNION ALL
SELECT 'long', `key`, CAST(`value` AS CHAR)
FROM ace_shard.config_properties_long
WHERE LEFT(`key`, 3) = 'zc_' OR LEFT(`key`, 12) = 'zonecontrol_'
ORDER BY setting_type, `key`;

SELECT charm_name, setting_key, setting_value
FROM ace_world.charm_settings
WHERE charm_name = 'pandemic'
ORDER BY setting_key;
-- Missing persisted flags/charm rows may use code defaults; absence is not false/zero.
-- Nether Fracture's own per-zone overrides are file-backed, not SQL-backed.
