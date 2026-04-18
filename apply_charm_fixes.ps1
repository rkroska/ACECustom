# ACE Charm Deployment Helper
# This script ensures your modified SQL files are in the active server folder.

$SourceDir = "c:\ACE\ACECustom\Content\sql\weenies"
$BuildDir  = "C:\ACE\ACEBuild\Content\sql\weenies"

Write-Host "Syncing Charm SQL files to Build folder..." -ForegroundColor Cyan

# Sync the specific 7777 files
robocopy $SourceDir $BuildDir 7777*.sql /XO

Write-Host "`n--- DEPLOYMENT INSTRUCTIONS ---" -ForegroundColor Green
Write-Host "Please run the following SQL files in your database tool (HeidiSQL, DBeaver, etc.) in order:" -ForegroundColor Yellow

$Files = Get-ChildItem -Path $BuildDir -Filter "7777*.sql" | Sort-Object Name
foreach ($f in $Files) {
    Write-Host "  [ ] $($f.Name)"
}

Write-Host "`nAfter running the SQL, type '@re-import' in your server console or restart the server." -ForegroundColor Cyan
