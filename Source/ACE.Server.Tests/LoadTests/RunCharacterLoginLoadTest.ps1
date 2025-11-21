# Character Login Load Test Runner
# Tests the character login and loading performance of ACE server

param(
    [int]$Clients = 25,
    [int]$DurationMinutes = 3,
    [string]$ServerHost = "127.0.0.1",
    [int]$ServerPort = 9000,
    [switch]$Verbose
)

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "  ACE Character Login Load Test" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "  Server: $ServerHost`:$ServerPort" -ForegroundColor White
Write-Host "  Clients: $Clients" -ForegroundColor White
Write-Host "  Duration: $DurationMinutes minutes" -ForegroundColor White
Write-Host "  Verbose: $Verbose" -ForegroundColor White
Write-Host ""

$testDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $testDir))

Write-Host "Building test project..." -ForegroundColor Yellow
Push-Location $testDir
Push-Location ..

$buildResult = dotnet build ACE.Server.Tests.csproj --configuration Release 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    Write-Host $buildResult
    Pop-Location
    Pop-Location
    exit 1
}

Write-Host "Build successful!" -ForegroundColor Green
Write-Host ""

# Create a temporary test runner C# file
$testRunnerCode = @"
using System;
using System.Threading.Tasks;
using ACE.Server.Tests.LoadTests;

namespace CharacterLoginLoadTestRunner
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Character Login Load Test...");
            Console.WriteLine();

            var config = new LoadTestConfiguration
            {
                ServerHost = "$ServerHost",
                ServerPort = $ServerPort,
                ConcurrentClients = $Clients,
                TestDuration = TimeSpan.FromMinutes($DurationMinutes),
                Scenario = LoadTestScenario.IdleClients, // Focus on connection and login
                ConnectionBatchSize = 5,
                ConnectionBatchDelay = 1000,
                VerboseLogging = $($Verbose.ToString().ToLower()),
                TestName = "Character Login Load Test"
            };

            Console.WriteLine("Test Configuration:");
            Console.WriteLine("  Server: " + config.ServerHost + ":" + config.ServerPort);
            Console.WriteLine("  Clients: " + config.ConcurrentClients);
            Console.WriteLine("  Duration: " + config.TestDuration.TotalMinutes + " minutes");
            Console.WriteLine("  Scenario: Character Login (Idle after login)");
            Console.WriteLine();

            var orchestrator = new LoadTestOrchestrator(config);
            var results = await orchestrator.RunLoadTestAsync();

            // Display detailed results
            Console.WriteLine();
            Console.WriteLine("===============================================");
            Console.WriteLine("           LOAD TEST RESULTS");
            Console.WriteLine("===============================================");
            Console.WriteLine();
            Console.WriteLine("CONNECTION METRICS:");
            Console.WriteLine("  Total Clients:      " + results.TotalClients);
            Console.WriteLine("  Successful:         " + results.SuccessfulConnections + " (" + (results.ConnectionSuccessRate * 100).ToString("F2") + "%)");
            Console.WriteLine("  Failed:             " + results.FailedConnections);
            Console.WriteLine("  Avg Connect Time:   " + results.AverageConnectionTime.TotalMilliseconds.ToString("F2") + "ms");
            Console.WriteLine();
            Console.WriteLine("ACTION METRICS:");
            Console.WriteLine("  Total Actions:      " + results.TotalActions);
            Console.WriteLine("  Failed Actions:     " + results.FailedActions);
            Console.WriteLine("  Actions/Second:     " + results.ActionsPerSecond.ToString("F2"));
            Console.WriteLine("  Avg Action Time:    " + results.AverageActionTime.TotalMilliseconds.ToString("F2") + "ms");
            Console.WriteLine("  Min Action Time:    " + results.MinActionTime.TotalMilliseconds.ToString("F2") + "ms");
            Console.WriteLine("  Max Action Time:    " + results.MaxActionTime.TotalMilliseconds.ToString("F2") + "ms");
            Console.WriteLine();
            Console.WriteLine("NETWORK METRICS:");
            Console.WriteLine("  Packets Sent:       " + results.TotalPacketsSent);
            Console.WriteLine("  Packets Received:   " + results.TotalPacketsReceived);
            Console.WriteLine("  Packets/Second:     " + results.PacketsPerSecond.ToString("F2"));
            Console.WriteLine();
            Console.WriteLine("ERROR METRICS:");
            Console.WriteLine("  Total Errors:       " + results.TotalErrors);
            Console.WriteLine("  Success Rate:       " + (results.SuccessRate * 100).ToString("F2") + "%");
            Console.WriteLine();
            Console.WriteLine("TEST DURATION:");
            Console.WriteLine("  Start Time:         " + results.StartTime.ToString("yyyy-MM-dd HH:mm:ss"));
            Console.WriteLine("  End Time:           " + results.EndTime.ToString("yyyy-MM-dd HH:mm:ss"));
            Console.WriteLine("  Duration:           " + results.Duration.ToString(@"hh\:mm\:ss"));
            Console.WriteLine();
            Console.WriteLine("===============================================");

            // Save results to CSV
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var csvFilename = string.Format("CharacterLogin_LoadTest_{0}.csv", timestamp);
            System.IO.File.WriteAllText(csvFilename, results.ToCsv());
            Console.WriteLine();
            Console.WriteLine("Results saved to: " + csvFilename);

            // Performance assessment
            Console.WriteLine();
            Console.WriteLine("PERFORMANCE ASSESSMENT:");
            if (results.ConnectionSuccessRate >= 0.95)
                Console.WriteLine("  ✓ Connection Success Rate: EXCELLENT (>95%)");
            else if (results.ConnectionSuccessRate >= 0.90)
                Console.WriteLine("  ⚠ Connection Success Rate: GOOD (>90%)");
            else
                Console.WriteLine("  ✗ Connection Success Rate: POOR (<90%)");

            if (results.AverageConnectionTime.TotalMilliseconds <= 500)
                Console.WriteLine("  ✓ Avg Connection Time: EXCELLENT (<500ms)");
            else if (results.AverageConnectionTime.TotalMilliseconds <= 1000)
                Console.WriteLine("  ⚠ Avg Connection Time: ACCEPTABLE (<1s)");
            else
                Console.WriteLine("  ✗ Avg Connection Time: SLOW (>1s)");

            if (results.TotalErrors == 0)
                Console.WriteLine("  ✓ Error Count: PERFECT (0 errors)");
            else if (results.TotalErrors <= results.TotalClients * 0.05)
                Console.WriteLine("  ⚠ Error Count: ACCEPTABLE (<5% of clients)");
            else
                Console.WriteLine("  ✗ Error Count: HIGH (>5% of clients)");

            Console.WriteLine();
            Console.WriteLine("Test completed successfully!");
        }
    }
}
"@

$tempCsFile = Join-Path $testDir "TempCharacterLoginRunner.cs"
$testRunnerCode | Out-File -FilePath $tempCsFile -Encoding UTF8

Write-Host "Running load test..." -ForegroundColor Yellow
Write-Host ""

# Run using dotnet script or compile and run
$tempExe = Join-Path $testDir "bin\Release\net8.0\TempRunner.exe"

# Use dotnet run with the test project
$env:TEST_SCENARIO = "CharacterLogin"
dotnet run --project ACE.Server.Tests.csproj --configuration Release --no-build -- --filter "FullyQualifiedName~LoadTest" 2>&1

# Clean up
if (Test-Path $tempCsFile) {
    Remove-Item $tempCsFile -Force
}

Pop-Location
Pop-Location

Write-Host ""
Write-Host "Load test execution complete!" -ForegroundColor Green
Write-Host ""
