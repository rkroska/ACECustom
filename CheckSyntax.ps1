$errors = $null
$tokens = $null
[System.Management.Automation.Language.Parser]::ParseFile('C:\ACE\ACECustom\ServerManager.ps1', [ref]$tokens, [ref]$errors) | Out-Null
if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Host "ERROR: $_" }
} else {
    Write-Host "Parse OK - no syntax errors"
}
