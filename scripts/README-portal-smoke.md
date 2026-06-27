# Portal automated checks

## Unit tests (no server running)

From repo root:

```powershell
dotnet test Source\ACE.Server.Tests\ACE.Server.Tests.csproj --filter "FullyQualifiedName~PatchNotesManagerTests|FullyQualifiedName~PatchNotesSecurityTests|FullyQualifiedName~PortalAccessManagerTests|FullyQualifiedName~SkillFormulaTests"
```

Covers hash URLs, MOTD template tokens, slugify, portal page registry defaults, patch-notes published-only defaults (including `PatchNotesController.List`), combat calculator formula parity, and portal access defaults.

## Live smoke script (server must be up)

```powershell
# Public endpoints only
.\scripts\portal-smoke.ps1 -BaseUrl "http://localhost:5001"

# With admin login (cookie session)
.\scripts\portal-smoke.ps1 -BaseUrl "http://YOUR_HOST:5002" -Username "youraccount" -Password "yourpassword"

# Prod-like URL
.\scripts\portal-smoke.ps1 -BaseUrl "http://76.237.151.184:5002" -SkipAuth
```

Exit code `0` = pass, `1` = fail (usable in CI or post-deploy).

## Suggested deploy gate

1. `dotnet test` (unit tests above)
2. Deploy + restart ACE
3. `.\scripts\portal-smoke.ps1 -BaseUrl <prod-url> [-Username ...]`
