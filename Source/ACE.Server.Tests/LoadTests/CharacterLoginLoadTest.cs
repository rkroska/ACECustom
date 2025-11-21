using System;
using System.IO;
using System.Threading.Tasks;

namespace ACE.Server.Tests.LoadTests
{
    /// <summary>
    /// Dedicated load test for character login and loading performance
    /// </summary>
    public class CharacterLoginLoadTest
    {
        /// <summary>
        /// Runs a character login-focused load test
        /// </summary>
        public static async Task<LoadTestResults> RunAsync(
            string serverHost = "127.0.0.1",
            int serverPort = 9000,
            int clientCount = 25,
            int durationMinutes = 3,
            bool verbose = false)
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("  ACE Character Login & Loading Performance Test");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine($"Configuration:");
            Console.WriteLine($"  Server:          {serverHost}:{serverPort}");
            Console.WriteLine($"  Test Clients:    {clientCount}");
            Console.WriteLine($"  Duration:        {durationMinutes} minutes");
            Console.WriteLine($"  Test Focus:      Character connection, authentication, and world entry");
            Console.WriteLine();

            var config = new LoadTestConfiguration
            {
                ServerHost = serverHost,
                ServerPort = serverPort,
                ConcurrentClients = clientCount,
                TestDuration = TimeSpan.FromMinutes(durationMinutes),
                Scenario = LoadTestScenario.IdleClients, // Focus on login/connection
                ConnectionBatchSize = 5,  // Connect 5 clients at a time
                ConnectionBatchDelay = 800, // Wait 800ms between batches
                VerboseLogging = verbose,
                TestName = "Character Login Performance Test"
            };

            Console.WriteLine("Starting load test...");
            Console.WriteLine("This test will:");
            Console.WriteLine("  1. Connect multiple clients to the server");
            Console.WriteLine("  2. Authenticate each client");
            Console.WriteLine("  3. Enter world with characters");
            Console.WriteLine("  4. Monitor connection stability");
            Console.WriteLine("  5. Measure login and loading performance");
            Console.WriteLine();

            var orchestrator = new LoadTestOrchestrator(config);
            var results = await orchestrator.RunLoadTestAsync();

            DisplayResults(results);
            SaveResults(results);

            return results;
        }

        private static void DisplayResults(LoadTestResults results)
        {
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("                    TEST RESULTS");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine();

            // Connection Performance
            Console.WriteLine("┌─ CONNECTION PERFORMANCE ──────────────────────────────────┐");
            Console.WriteLine($"│ Total Clients:          {results.TotalClients,10}                    │");
            Console.WriteLine($"│ Successful Logins:      {results.SuccessfulConnections,10} ({results.ConnectionSuccessRate * 100,6:F2}%)       │");
            Console.WriteLine($"│ Failed Logins:          {results.FailedConnections,10}                    │");
            Console.WriteLine($"│ Avg Connection Time:    {results.AverageConnectionTime.TotalMilliseconds,10:F2} ms               │");
            Console.WriteLine("└───────────────────────────────────────────────────────────┘");
            Console.WriteLine();

            // Action Performance
            Console.WriteLine("┌─ RUNTIME PERFORMANCE ─────────────────────────────────────┐");
            Console.WriteLine($"│ Total Actions:          {results.TotalActions,10}                    │");
            Console.WriteLine($"│ Failed Actions:         {results.FailedActions,10}                    │");
            Console.WriteLine($"│ Actions/Second:         {results.ActionsPerSecond,10:F2}                    │");
            Console.WriteLine($"│ Avg Action Time:        {results.AverageActionTime.TotalMilliseconds,10:F2} ms               │");
            Console.WriteLine($"│ Min Action Time:        {results.MinActionTime.TotalMilliseconds,10:F2} ms               │");
            Console.WriteLine($"│ Max Action Time:        {results.MaxActionTime.TotalMilliseconds,10:F2} ms               │");
            Console.WriteLine("└───────────────────────────────────────────────────────────┘");
            Console.WriteLine();

            // Network Performance
            Console.WriteLine("┌─ NETWORK METRICS ─────────────────────────────────────────┐");
            Console.WriteLine($"│ Packets Sent:           {results.TotalPacketsSent,10}                    │");
            Console.WriteLine($"│ Packets Received:       {results.TotalPacketsReceived,10}                    │");
            Console.WriteLine($"│ Packets/Second:         {results.PacketsPerSecond,10:F2}                    │");
            Console.WriteLine("└───────────────────────────────────────────────────────────┘");
            Console.WriteLine();

            // Error Metrics
            Console.WriteLine("┌─ ERROR ANALYSIS ──────────────────────────────────────────┐");
            Console.WriteLine($"│ Total Errors:           {results.TotalErrors,10}                    │");
            Console.WriteLine($"│ Success Rate:           {results.SuccessRate * 100,10:F2}%                  │");
            Console.WriteLine("└───────────────────────────────────────────────────────────┘");
            Console.WriteLine();

            // Test Duration
            Console.WriteLine("┌─ TEST DURATION ───────────────────────────────────────────┐");
            Console.WriteLine($"│ Start:     {results.StartTime:yyyy-MM-dd HH:mm:ss}                      │");
            Console.WriteLine($"│ End:       {results.EndTime:yyyy-MM-dd HH:mm:ss}                      │");
            Console.WriteLine($"│ Duration:  {results.Duration:hh\\:mm\\:ss}                                  │");
            Console.WriteLine("└───────────────────────────────────────────────────────────┘");
            Console.WriteLine();

            // Performance Assessment
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("               PERFORMANCE ASSESSMENT");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine();

            AssessMetric("Connection Success Rate",
                results.ConnectionSuccessRate,
                0.95, 0.90,
                v => $"{v * 100:F2}%");

            AssessMetric("Avg Connection Time",
                results.AverageConnectionTime.TotalMilliseconds,
                500, 1000,
                v => $"{v:F2}ms",
                invertComparison: true);

            AssessMetric("Error Rate",
                (double)results.TotalErrors / results.TotalClients,
                0.01, 0.05,
                v => $"{v * 100:F2}%",
                invertComparison: true);

            AssessMetric("Success Rate",
                results.SuccessRate,
                0.98, 0.95,
                v => $"{v * 100:F2}%");

            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════");

            // Overall verdict
            Console.WriteLine();
            var overallScore = CalculateOverallScore(results);
            if (overallScore >= 90)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ VERDICT: EXCELLENT - Server is performing very well!");
            }
            else if (overallScore >= 75)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ VERDICT: GOOD - Server is performing acceptably with minor issues.");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ VERDICT: NEEDS ATTENTION - Server performance should be investigated.");
            }
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void AssessMetric(string name, double value, double excellent, double acceptable,
            Func<double, string> formatter, bool invertComparison = false)
        {
            string status;
            ConsoleColor color;

            bool isExcellent = invertComparison ? value <= excellent : value >= excellent;
            bool isAcceptable = invertComparison ? value <= acceptable : value >= acceptable;

            if (isExcellent)
            {
                status = "EXCELLENT";
                color = ConsoleColor.Green;
            }
            else if (isAcceptable)
            {
                status = "ACCEPTABLE";
                color = ConsoleColor.Yellow;
            }
            else
            {
                status = "NEEDS WORK";
                color = ConsoleColor.Red;
            }

            Console.Write($"  {name,-25} ");
            Console.ForegroundColor = color;
            Console.Write($"{formatter(value),-15}");
            Console.Write($"[{status}]");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static int CalculateOverallScore(LoadTestResults results)
        {
            int score = 0;

            // Connection success (40 points)
            if (results.ConnectionSuccessRate >= 0.95) score += 40;
            else if (results.ConnectionSuccessRate >= 0.90) score += 30;
            else if (results.ConnectionSuccessRate >= 0.80) score += 20;

            // Connection speed (30 points)
            if (results.AverageConnectionTime.TotalMilliseconds <= 500) score += 30;
            else if (results.AverageConnectionTime.TotalMilliseconds <= 1000) score += 20;
            else if (results.AverageConnectionTime.TotalMilliseconds <= 2000) score += 10;

            // Error rate (30 points)
            double errorRate = (double)results.TotalErrors / results.TotalClients;
            if (errorRate <= 0.01) score += 30;
            else if (errorRate <= 0.05) score += 20;
            else if (errorRate <= 0.10) score += 10;

            return score;
        }

        private static void SaveResults(LoadTestResults results)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var csvFilename = $"CharacterLogin_LoadTest_{timestamp}.csv";
            var jsonFilename = $"CharacterLogin_LoadTest_{timestamp}.json";

            try
            {
                File.WriteAllText(csvFilename, results.ToCsv());
                File.WriteAllText(jsonFilename, results.ToJson());

                Console.WriteLine("Results saved:");
                Console.WriteLine($"  CSV:  {Path.GetFullPath(csvFilename)}");
                Console.WriteLine($"  JSON: {Path.GetFullPath(jsonFilename)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not save results to file: {ex.Message}");
            }
        }
    }
}
