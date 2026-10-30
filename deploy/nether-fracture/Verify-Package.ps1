param([string]$Root = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
$packageRoot = [IO.Path]::GetFullPath($Root)
$prefix = $packageRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$manifest = Get-Content -Raw -LiteralPath (Join-Path $packageRoot 'SHA256SUMS.json') | ConvertFrom-Json
$count = 0
foreach ($entry in $manifest.PSObject.Properties) {
    $target = [IO.Path]::GetFullPath((Join-Path $packageRoot $entry.Name))
    if (-not $target.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid manifest path' }
    if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { throw "Missing packaged file: $($entry.Name)" }
    $actual = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    if ($actual -ne $entry.Value) { throw "Checksum mismatch: $($entry.Name)" }
    $count++
}
Write-Output "Package verification passed: $count files."
