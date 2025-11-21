using ACE.Server.Tests.LoadTests;

Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("   ACE Character Login Load Test Runner");
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine();

// Parse command line arguments
string serverHost = args.Length > 0 ? args[0] : "127.0.0.1";
int serverPort = args.Length > 1 ? int.Parse(args[1]) : 9000;
int clientCount = args.Length > 2 ? int.Parse(args[2]) : 25;
int durationMinutes = args.Length > 3 ? int.Parse(args[3]) : 3;

var config = new LoadTestConfiguration
{
    ServerHost = serverHost,
    ServerPort = serverPort,
    ConcurrentClients = clientCount,
    TestDuration = TimeSpan.FromMinutes(durationMinutes),
    Scenario = LoadTestScenario.IdleClients, // Focus on login/connection
    ConnectionBatchSize = 5,
    ConnectionBatchDelay = 800,
    VerboseLogging = false,
    TestName = "Character Login Performance Test"
};

Console.WriteLine("Configuration:");
Console.WriteLine($"  Server:          {config.ServerHost}:{config.ServerPort}");
Console.WriteLine($"  Clients:         {config.ConcurrentClients}");
Console.WriteLine($"  Duration:        {config.TestDuration.TotalMinutes} minutes");
Console.WriteLine($"  Test Focus:      Character connection and login");
Console.WriteLine();

Console.WriteLine("Starting load test...");
Console.WriteLine("This will:");
Console.WriteLine("  1. Connect multiple clients to the server");
Console.WriteLine("  2. Authenticate each client");
Console.WriteLine("  3. Enter world with characters");
Console.WriteLine("  4. Monitor connection stability");
Console.WriteLine();

var orchestrator = new LoadTestOrchestrator(config);
var results = await orchestrator.RunLoadTestAsync();

// Display results
Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("                   TEST RESULTS");
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine();

Console.WriteLine("CONNECTION METRICS:");
Console.WriteLine($"  Total Clients:         {results.TotalClients}");
Console.WriteLine($"  Successful:            {results.SuccessfulConnections} ({results.ConnectionSuccessRate:P2})");
Console.WriteLine($"  Failed:                {results.FailedConnections}");
Console.WriteLine($"  Avg Connection Time:   {results.AverageConnectionTime.TotalMilliseconds:F2}ms");
Console.WriteLine();

Console.WriteLine("ACTION METRICS:");
Console.WriteLine($"  Total Actions:         {results.TotalActions}");
Console.WriteLine($"  Actions/Second:        {results.ActionsPerSecond:F2}");
Console.WriteLine($"  Avg Action Time:       {results.AverageActionTime.TotalMilliseconds:F2}ms");
Console.WriteLine();

Console.WriteLine("NETWORK METRICS:");
Console.WriteLine($"  Packets Sent:          {results.TotalPacketsSent}");
Console.WriteLine($"  Packets Received:      {results.TotalPacketsReceived}");
Console.WriteLine($"  Packets/Second:        {results.PacketsPerSecond:F2}");
Console.WriteLine();

Console.WriteLine("ERROR METRICS:");
Console.WriteLine($"  Total Errors:          {results.TotalErrors}");
Console.WriteLine($"  Success Rate:          {results.SuccessRate:P2}");
Console.WriteLine();

Console.WriteLine("TEST DURATION:");
Console.WriteLine($"  Start:                 {results.StartTime:HH:mm:ss}");
Console.WriteLine($"  End:                   {results.EndTime:HH:mm:ss}");
Console.WriteLine($"  Duration:              {results.Duration:hh\\:mm\\:ss}");
Console.WriteLine();

// Save results
var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
var csvFilename = $"CharacterLogin_LoadTest_{timestamp}.csv";
var jsonFilename = $"CharacterLogin_LoadTest_{timestamp}.json";
File.WriteAllText(csvFilename, results.ToCsv());
File.WriteAllText(jsonFilename, results.ToJson());

Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("                PERFORMANCE ASSESSMENT");
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine();

// Assessment
if (results.ConnectionSuccessRate >= 0.95)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("  ✓ Connection Success Rate: EXCELLENT (>95%)");
}
else if (results.ConnectionSuccessRate >= 0.90)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("  ⚠ Connection Success Rate: GOOD (>90%)");
}
else
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("  ✗ Connection Success Rate: NEEDS WORK (<90%)");
}
Console.ResetColor();

if (results.AverageConnectionTime.TotalMilliseconds <= 500)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("  ✓ Avg Connection Time: EXCELLENT (<500ms)");
}
else if (results.AverageConnectionTime.TotalMilliseconds <= 1000)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("  ⚠ Avg Connection Time: ACCEPTABLE (<1s)");
}
else
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("  ✗ Avg Connection Time: SLOW (>1s)");
}
Console.ResetColor();

double errorRate = (double)results.TotalErrors / results.TotalClients;
if (errorRate <= 0.01)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("  ✓ Error Rate: EXCELLENT (<1%)");
}
else if (errorRate <= 0.05)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("  ⚠ Error Rate: ACCEPTABLE (<5%)");
}
else
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("  ✗ Error Rate: HIGH (>5%)");
}
Console.ResetColor();

Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine();
Console.WriteLine($"Results saved to:");
Console.WriteLine($"  CSV:  {Path.GetFullPath(csvFilename)}");
Console.WriteLine($"  JSON: {Path.GetFullPath(jsonFilename)}");
Console.WriteLine();

// Overall verdict
var score = 0;
if (results.ConnectionSuccessRate >= 0.95) score += 40;
else if (results.ConnectionSuccessRate >= 0.90) score += 30;
else if (results.ConnectionSuccessRate >= 0.80) score += 20;

if (results.AverageConnectionTime.TotalMilliseconds <= 500) score += 30;
else if (results.AverageConnectionTime.TotalMilliseconds <= 1000) score += 20;
else if (results.AverageConnectionTime.TotalMilliseconds <= 2000) score += 10;

if (errorRate <= 0.01) score += 30;
else if (errorRate <= 0.05) score += 20;
else if (errorRate <= 0.10) score += 10;

if (score >= 90)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("✓ OVERALL: EXCELLENT - Server performing very well!");
}
else if (score >= 75)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("⚠ OVERALL: GOOD - Server performing acceptably with minor issues.");
}
else
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("✗ OVERALL: NEEDS ATTENTION - Server performance should be investigated.");
}
Console.ResetColor();

Console.WriteLine();
Console.WriteLine("Test complete!");

// Exit code based on success
Environment.Exit(results.ConnectionSuccessRate >= 0.80 ? 0 : 1);
