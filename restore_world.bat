@echo off
"C:\Program Files\MariaDB 12.2\bin\mysql.exe" -u acedockeruser -p2020acEmulator2017 --binary-mode ace_world < C:\ACE\ACEBuild_backup2\ace_world_backup.sql
echo World DB restore done.
