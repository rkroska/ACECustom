@echo off
"C:\Program Files\MariaDB 12.2\bin\mysql.exe" -u acedockeruser -p2020acEmulator2017 ace_world < "C:\ACE\ACECustom\Content\sql\weenies\777700019 Infinite Casting Stone.sql"
echo Infinite Casting Stone loaded to ace_world.
xcopy /Y /Q "C:\ACE\ACECustom\Content\sql\weenies\*" "C:\ACE\ACEBuild\Content\sql\weenies\"
echo SQL synced to ACEBuild.
