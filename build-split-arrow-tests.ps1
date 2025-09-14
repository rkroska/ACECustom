Write-Host "Building ACE Server with Split Arrow Tests..." -ForegroundColor Green
Write-Host ""

Write-Host "Step 1: Building the solution..." -ForegroundColor Yellow
dotnet build Source/ACE.Server/ACE.Server.csproj --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Step 2: Running Split Arrow Tests..." -ForegroundColor Yellow
dotnet test Source/ACE.Server.Tests/ACE.Server.Tests.csproj --filter "FullyQualifiedName~SplitArrowTests" --verbosity normal --no-build
if ($LASTEXITCODE -ne 0) {
    Write-Host "Split Arrow tests failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "✅ Build and Split Arrow Tests completed successfully!" -ForegroundColor Green
Write-Host ""
