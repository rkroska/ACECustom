@echo off
set MYSQL="C:\Program Files\MariaDB 12.2\bin\mysql.exe"
set USER=acedockeruser
set PASS=2020acEmulator2017
set DBDIR=c:\ACE\ACECustom\Database

echo Loading AuthenticationBase...
%MYSQL% -u %USER% -p%PASS% ace_auth < "%DBDIR%\Base\AuthenticationBase.sql"

echo Loading ShardBase...
%MYSQL% -u %USER% -p%PASS% ace_shard < "%DBDIR%\Base\ShardBase.sql"

echo Loading WorldBase (this takes a few minutes)...
%MYSQL% -u %USER% -p%PASS% ace_world < "%DBDIR%\Base\WorldBase.sql"

echo Loading Updates (Shard)...
for %%f in ("%DBDIR%\Updates\Shard\*.sql") do (
    echo   Applying %%~nxf...
    %MYSQL% -u %USER% -p%PASS% ace_shard < "%%f"
)

echo Loading Updates (World)...
for %%f in ("%DBDIR%\Updates\World\*.sql") do (
    echo   Applying %%~nxf...
    %MYSQL% -u %USER% -p%PASS% ace_world < "%%f"
)

echo Loading Auth Updates...
for %%f in ("%DBDIR%\Updates\Authentication\*.sql") do (
    echo   Applying %%~nxf...
    %MYSQL% -u %USER% -p%PASS% ace_auth < "%%f"
)

echo All done!
