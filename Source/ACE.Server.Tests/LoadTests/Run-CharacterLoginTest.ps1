#!/usr/bin/env pwsh
# Simple runner for Character Login Load Test

param(
    [int]$Clients = 25,
    [int]$Minutes = 3,
    [string]$Server = "127.0.0.1",
    [int]$Port = 9000,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Starting Character Login Load Test" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Build the C# code that will run the test
$code = @"
using System;
using System.Threading.Tasks;
using ACE.Server.Tests.LoadTests;

class Program
{
    static async Task Main(string[] args)
    {
        var results = await CharacterLoginLoadTest.RunAsync(
            serverHost: "$Server",
            serverPort: $Port,
            clientCount: $Clients,
            durationMinutes: $Minutes,
            verbose: $($Verbose.ToString().ToLower())
        );

        Environment.Exit(results.ConnectionSuccessRate >= 0.80 ? 0 : 1);
    }
}
"@

# Save to temp file
$tempDir = [System.IO.Path]::GetTempPath()
$tempFile = Join-Path $tempDir "CharacterLoginTest_$(Get-Random).cs"
$code | Out-File -FilePath $tempFile -Encoding UTF8

try {
    # Navigate to the test project directory
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    Push-Location $scriptDir
    Push-Location ..

    Write-Host "Building test project..." -ForegroundColor Yellow
    $buildOutput = dotnet build ACE.Server.Tests.csproj -c Release --nologo -v quiet 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed:" -ForegroundColor Red
        Write-Host $buildOutput
        exit 1
    }

    Write-Host "Build complete!" -ForegroundColor Green
    Write-Host ""

    # Get the built assembly location
    $dllPath = "bin\Release\net8.0\ACE.Server.Tests.dll"
    
    if (-not (Test-Path $dllPath)) {
        # Try net6.0 or net7.0
        $dllPath = Get-ChildItem "bin\Release" -Filter "ACE.Server.Tests.dll" -Recurse | Select-Object -First 1 | ForEach-Object { $_.FullName }
    }

    if (-not $dllPath -or -not (Test-Path $dllPath)) {
        Write-Host "Could not find built test DLL" -ForegroundColor Red
        exit 1
    }

    Write-Host "Running load test with:" -ForegroundColor Cyan
    Write-Host "  Server: $Server`:$Port" -ForegroundColor White
    Write-Host "  Clients: $Clients" -ForegroundColor White  
    Write-Host "  Duration: $Minutes minutes" -ForegroundColor White
    Write-Host ""
    Write-Host "Please wait..." -ForegroundColor Yellow
    Write-Host ""

    # Use dotnet-script or compile and run inline
    $projectDir = Get-Location
    $csprojContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$projectDir\ACE.Server.Tests.csproj" />
  </ItemGroup>
</Project>
"@

    $runnerDir = Join-Path $tempDir "LoadTestRunner_$(Get-Random)"
    New-Item -ItemType Directory -Path $runnerDir -Force | Out-Null
    
    $csprojPath = Join-Path $runnerDir "Runner.csproj"
    $csPath = Join-Path $runnerDir "Program.cs"
    
    $csprojContent | Out-File -FilePath $csprojPath -Encoding UTF8
    $code | Out-File -FilePath $csPath -Encoding UTF8

    Push-Location $runnerDir
    
    Write-Host "Executing test..." -ForegroundColor Yellow
    dotnet run --configuration Release
    $exitCode = $LASTEXITCODE

    Pop-Location
    Remove-Item -Path $runnerDir -Recurse -Force -ErrorAction SilentlyContinue

} finally {
    Pop-Location
    Pop-Location
    if (Test-Path $tempFile) {
        Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
    }
}

Write-Host ""
if ($exitCode -eq 0) {
    Write-Host "Test completed successfully!" -ForegroundColor Green
} else {
    Write-Host "Test completed with warnings or errors." -ForegroundColor Yellow
}
Write-Host ""

exit $exitCode
