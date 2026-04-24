@echo off
echo ============================================
echo  ACE Deploy — Build + Deploy to ACEBuild
echo ============================================

set SOURCE=C:\ACE\ACECustom\Source
set BUILD_OUT=C:\ACE\ACEBuild
set MYSQL="C:\Program Files\MariaDB 12.2\bin\mysql.exe"
set USER=acedockeruser
set PASS=2020acEmulator2017
set DBDIR=C:\ACE\ACECustom\Database

:: -----------------------------------------------
:: Step 1 — Build ACE.Server
:: -----------------------------------------------
echo.
echo [1/3] Building ACE.Server...
dotnet build "%SOURCE%\ACE.Server\ACE.Server.csproj" -c Release -v quiet
if %ERRORLEVEL% neq 0 (
    echo BUILD FAILED. Aborting deploy.
    pause
    exit /b 1
)
echo Build succeeded.

:: -----------------------------------------------
:: Step 2 — Copy binaries to ACEBuild
:: -----------------------------------------------
echo.
echo [2/3] Copying binaries to ACEBuild...
copy /Y "%SOURCE%\ACE.Server\bin\Release\net10.0\ACE.Server.dll" "%BUILD_OUT%\ACE.Server.dll"
copy /Y "%SOURCE%\ACE.Server\bin\Release\net10.0\ACE.Server.pdb" "%BUILD_OUT%\ACE.Server.pdb"
copy /Y "%SOURCE%\ACE.Database\bin\Release\net10.0\ACE.Database.dll" "%BUILD_OUT%\ACE.Database.dll"
copy /Y "%SOURCE%\ACE.Entity\bin\Release\net10.0\ACE.Entity.dll" "%BUILD_OUT%\ACE.Entity.dll"
echo Binaries deployed.

:: -----------------------------------------------
:: Step 3 — Sync SQL weenies to ACEBuild
:: -----------------------------------------------
echo.
echo [3/3] Syncing SQL weenies to ACEBuild...
if exist "C:\ACE\ACECustom\Content\sql\weenies\" (
    xcopy /E /Y /I /Q "C:\ACE\ACECustom\Content\sql\weenies\*" "%BUILD_OUT%\Content\sql\weenies\"
    echo SQL weenies synced.
) else (
    echo No weenies folder found, skipping.
)

:: -----------------------------------------------
echo.
echo ============================================
echo  Deploy complete!
echo ============================================
pause
