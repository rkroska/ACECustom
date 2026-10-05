# Runs every automated blacksmithing test.
#
#   scripts\run-forge-tests.ps1            unit tests + the tests against real data
#   scripts\run-forge-tests.ps1 -UnitOnly  unit tests only (seconds, no database)
#
# The real-data tests (ForgeDatabaseTests) read the world and shard databases named in Config.js and the
# client DAT files. They never write: forged items are built in memory and thrown away. They take a few
# minutes. It is safe to run while the server is up, because the test build goes to the Debug folder,
# not the Release folder the server runs from.
param(
    [switch]$UnitOnly
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo 'Source\ACE.Server.Tests\ACE.Server.Tests.csproj'

if ($UnitOnly) {
    $env:ACE_FORGE_DB_TESTS = $null
    $filter = 'FullyQualifiedName~ForgeMathTests'
} else {
    $env:ACE_FORGE_DB_TESTS = '1'
    $filter = 'FullyQualifiedName~ForgeMathTests|FullyQualifiedName~ForgeDatabaseTests'
}

Write-Host "Running blacksmithing tests ($filter)..."
dotnet test $project -p:Platform=x64 --filter $filter --nologo --logger 'console;verbosity=normal' |
    Where-Object { $_ -notmatch 'warning (CS|MSTEST|NU|CA)\d+' -and "$_".Trim() -ne '' }   # everything except compiler-warning noise, so a failure keeps its message

if ($LASTEXITCODE -ne 0) {
    Write-Host 'Blacksmithing tests FAILED.' -ForegroundColor Red
    exit 1
}
Write-Host 'Blacksmithing tests passed.' -ForegroundColor Green
