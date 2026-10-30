param([string]$Root = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
$packageRoot = [IO.Path]::GetFullPath($Root)
$prefix = $packageRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$manifest = Get-Content -Raw -LiteralPath (Join-Path $packageRoot 'SHA256SUMS.json') | ConvertFrom-Json -NoEnumerate
if ($null -eq $manifest -or $manifest.GetType().FullName -ne 'System.Management.Automation.PSCustomObject') { throw 'Manifest must be a JSON object.' }
$entries = @($manifest.PSObject.Properties)
if ($entries.Count -eq 0) { throw 'Empty package manifest.' }
$required = @('server/ACE.Server.dll', 'server/ACE.Server.deps.json', 'server/ACE.Server.runtimeconfig.json',
    'server/Config/nether-fracture.json', 'nether-fracture.production.json', 'nether-fracture.disabled.json',
    'DEPLOYMENT.html', 'LICENSE')
foreach ($file in $required) {
    if ($file -notin $entries.Name) { throw "Required file missing from manifest: $file" }
    if (-not (Test-Path -LiteralPath (Join-Path $packageRoot $file) -PathType Leaf)) { throw "Missing required packaged file: $file" }
}
$count = 0
foreach ($entry in $entries) {
    $target = [IO.Path]::GetFullPath((Join-Path $packageRoot $entry.Name))
    if (-not $target.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid manifest path' }
    if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { throw "Missing packaged file: $($entry.Name)" }
    $actual = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    if ($actual -ne $entry.Value) { throw "Checksum mismatch: $($entry.Name)" }
    $count++
}
Write-Output "Package verification passed: $count files."
