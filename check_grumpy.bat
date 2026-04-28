@echo off
REM Fix Grumpy's character:
REM 1. Set SpellComponentsRequired = true (type 68)
REM 2. Clear stale HasInfiniteCasting flag (type 50028)
"C:\Program Files\MariaDB 12.2\bin\mysql.exe" -u acedockeruser -p2020acEmulator2017 ace_shard -e "UPDATE biota_properties_bool bp JOIN `character` c ON c.id = bp.object_Id SET bp.value = 1 WHERE c.name = 'Grumpy' AND bp.type = 68; DELETE bp FROM biota_properties_bool bp JOIN `character` c ON c.id = bp.object_Id WHERE c.name = 'Grumpy' AND bp.type = 50028;"
echo Done. Grumpy spell comp flags reset.
