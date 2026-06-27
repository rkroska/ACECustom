# Smoke-test a running ACE web portal (local or prod).
# Usage:
#   .\scripts\portal-smoke.ps1
#   .\scripts\portal-smoke.ps1 -BaseUrl "http://76.237.151.184:5002"
#   .\scripts\portal-smoke.ps1 -BaseUrl "http://localhost:5001" -Username admin -Password secret
#
# Exit code: 0 = all checks passed, 1 = one or more failed

param(
    [string]$BaseUrl = "http://localhost:5001",
    [string]$Username = "",
    [string]$Password = "",
    [switch]$SkipAuth
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd("/")
$script:failed = 0
$script:passed = 0
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Method = "GET",
        [string]$Path,
        [int[]]$ExpectStatus = @(200),
        [object]$Body = $null,
        [switch]$UseSession
    )

    $uri = "$BaseUrl$Path"
    try {
        $params = @{
            Uri             = $uri
            Method          = $Method
            UseBasicParsing = $true
            TimeoutSec      = 30
        }
        if ($UseSession) { $params.WebSession = $session }
        if ($Body) {
            $params.Body = ($Body | ConvertTo-Json -Compress)
            $params.ContentType = "application/json"
        }

        $resp = Invoke-WebRequest @params
        if ($ExpectStatus -notcontains $resp.StatusCode) {
            Write-Host ('[FAIL] {0} - expected {1}, got {2}' -f $Name, ($ExpectStatus -join '|'), $resp.StatusCode) -ForegroundColor Red
            $script:failed++
            return
        }
        Write-Host ('[OK]   {0} ({1})' -f $Name, $resp.StatusCode) -ForegroundColor Green
        $script:passed++
    }
    catch {
        $status = $null
        if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
        if ($status -and ($ExpectStatus -contains $status)) {
            Write-Host ('[OK]   {0} ({1})' -f $Name, $status) -ForegroundColor Green
            $script:passed++
            return
        }
        Write-Host ('[FAIL] {0} - {1}' -f $Name, $_.Exception.Message) -ForegroundColor Red
        $script:failed++
    }
}

Write-Host "Portal smoke: $BaseUrl" -ForegroundColor Cyan

Test-Endpoint -Name "Health" -Path "/api/health"
Test-Endpoint -Name "Patch notes meta (public)" -Path "/api/patch-notes/meta"
Test-Endpoint -Name "Patch notes list (public)" -Path "/api/patch-notes?page=1&pageSize=5"
Test-Endpoint -Name "Portal index (SPA)" -Path "/" -ExpectStatus @(200)
Test-Endpoint -Name "Patch notes SPA shell" -Path "/index.html"

# Anonymous requests must not reach authenticated admin APIs (401 Unauthorized or 403 Forbidden).
Test-Endpoint -Name "Patch notes admin (no auth)" -Path "/api/patch-notes/admin/all?pageSize=5" -ExpectStatus @(401, 403)
Test-Endpoint -Name "Portal access pages (no auth)" -Path "/api/portal-access/pages" -ExpectStatus @(401, 403)
Test-Endpoint -Name "Audit transfers (no auth)" -Path "/api/audit/transfers?page=1&pageSize=1&days=7" -ExpectStatus @(401, 403)
Test-Endpoint -Name "Quest builder ping (no auth)" -Path "/api/quest-builder/ping" -ExpectStatus @(401, 403)
Test-Endpoint -Name "Combat config (no auth)" -Path "/api/combat/config" -ExpectStatus @(401, 403)
Test-Endpoint -Name "Leaderboards catalog (no auth)" -Path "/api/leaderboards/boards" -ExpectStatus @(401, 403)

if (-not $SkipAuth -and $Username -and $Password) {
    Test-Endpoint -Name "Login" -Method POST -Path "/api/auth/login" -Body @{
        username = $Username
        password = $Password
    } -UseSession

    Test-Endpoint -Name "Auth me" -Path "/api/auth/me" -UseSession -ExpectStatus @(200, 401)
    Test-Endpoint -Name "Portal access pages (auth)" -Path "/api/portal-access/pages" -UseSession -ExpectStatus @(200, 403)
    Test-Endpoint -Name "Audit transfers (auth)" -Path "/api/audit/transfers?page=1&pageSize=1&days=7" -UseSession -ExpectStatus @(200, 403)
    Test-Endpoint -Name "Patch notes admin list (auth)" -Path "/api/patch-notes/admin/all?pageSize=5" -UseSession -ExpectStatus @(200, 403)
    Test-Endpoint -Name "Leaderboards catalog (auth)" -Path "/api/leaderboards/boards" -UseSession -ExpectStatus @(200, 403)
    Test-Endpoint -Name "Leaderboard level board (auth)" -Path "/api/leaderboards/level" -UseSession -ExpectStatus @(200, 403)
    Test-Endpoint -Name "Combat config (auth)" -Path "/api/combat/config" -UseSession -ExpectStatus @(200, 403)
    Test-Endpoint -Name "Quest builder ping (auth)" -Path "/api/quest-builder/ping" -UseSession -ExpectStatus @(200, 403)
    Test-Endpoint -Name "Quest builder capabilities (auth)" -Path "/api/quest-builder/capabilities" -UseSession -ExpectStatus @(200, 403)
}
else {
    Write-Host '[SKIP] Auth checks (pass -Username and -Password or use -SkipAuth)' -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Passed: $script:passed  Failed: $script:failed"
if ($script:failed -gt 0) { exit 1 }
exit 0
