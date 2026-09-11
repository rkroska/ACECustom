-- Zone Control store: widen config_properties_string.value from TEXT (64 KB) to MEDIUMTEXT (16 MB).
-- 2026-09-02: the zonecontrol_data JSON (15 tier Defaults + rank rows + the elemental baselines) reached
-- 82 KB and the save failed with ERROR 1406 "Data too long for column 'value'". Applied by the startup
-- updater when this file sits in DatabaseSetupScripts/Updates/Shard (hand-maintained on live); idempotent (re-running MODIFY to the same type is a no-op). Already applied by
-- hand on the test shard the same night (C:\ACE\zonecontrol_store_column_mediumtext_2026-09-02.sql).
ALTER TABLE `config_properties_string` MODIFY `value` MEDIUMTEXT NOT NULL;
