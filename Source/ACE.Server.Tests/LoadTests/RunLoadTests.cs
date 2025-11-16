using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ACE.Server.Tests.LoadTests
{
    /// <summary>
    /// Standalone runner for executing all load tests and generating reports
    /// </summary>
    public class LoadTestRunner
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("ACE Server Load Test Suite");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();

            var serverHost = args.Length > 0 ? args[0] : "127.0.0.1";

            var serverPort = 9000;
            if (args.Length > 1 && !int.TryParse(args[1], out serverPort))
            {
                Console.WriteLine($"Invalid port '{args[1]}', defaulting to 9000.");
                serverPort = 9000;
            }

            Console.WriteLine($"Target Server: {serverHost}:{serverPort}");
            Console.WriteLine();

            var allResults = new List<(string TestName, LoadTestResults Results)>();

            // Test 1: Small Scale Idle Test
            Console.WriteLine("[1/6] Running Small Scale Idle Test (5 clients, 1 minute)...");
            try
            {
                var result1 = await RunSmallScaleIdleTest(serverHost, serverPort);
                allResults.Add(("SmallScaleIdle", result1));
                PrintTestSummary(result1);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ERROR: {ex.Message}");
            }
            Console.WriteLine();

            // Test 2: Medium Scale Chat Test
            Console.WriteLine("[2/6] Running Medium Scale Chat Test (25 clients, 2 minutes)...");
            try
            {
                var result2 = await RunMediumScaleChatTest(serverHost, serverPort);
                allResults.Add(("MediumScaleChat", result2));
                PrintTestSummary(result2);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ERROR: {ex.Message}");
            }
            Console.WriteLine();

            // Test 3: Movement Stress Test
            Console.WriteLine("[3/6] Running Movement Stress Test (50 clients, 3 minutes)...");
            try
            {
                var result3 = await RunMovementStressTest(serverHost, serverPort);
                allResults.Add(("MovementStress", result3));
                PrintTestSummary(result3);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ERROR: {ex.Message}");
            }
            Console.WriteLine();

            // Test 4: Combat Load Test
            Console.WriteLine("[4/6] Running Combat Load Test (30 clients, 3 minutes)...");
            try
            {
                var result4 = await RunCombatLoadTest(serverHost, serverPort);
                allResults.Add(("CombatLoad", result4));
                PrintTestSummary(result4);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ERROR: {ex.Message}");
            }
            Console.WriteLine();

            // Test 5: Connection Scaling Test
            Console.WriteLine("[5/6] Running Connection Scaling Test (10-100 clients, 5 minutes)...");
            try
            {
                var result5 = await RunConnectionScalingTest(serverHost, serverPort);
                allResults.Add(("ConnectionScaling", result5));
                PrintTestSummary(result5);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ERROR: {ex.Message}");
            }
            Console.WriteLine();

            // Test 6: Large Scale Mixed Test
            Console.WriteLine("[6/6] Running Large Scale Mixed Test (100 clients, 5 minutes)...");
            try
            {
                var result6 = await RunLargeScaleMixedTest(serverHost, serverPort);
                allResults.Add(("LargeScaleMixed", result6));
                PrintTestSummary(result6);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ERROR: {ex.Message}");
            }
            Console.WriteLine();

            // Generate comprehensive report
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("Generating Load Test Report...");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();

            var reportPath = await GenerateReport(allResults, serverHost, serverPort);

            Console.WriteLine();
            Console.WriteLine("Load test suite completed!");
            Console.WriteLine($"Report saved to: {reportPath}");
        }

        private static async Task<LoadTestResults> RunSmallScaleIdleTest(string host, int port)
        {
            var config = new LoadTestConfiguration
            {
                ServerHost = host,
                ServerPort = port,
                ConcurrentClients = 5,
                TestDuration = TimeSpan.FromMinutes(1),
                Scenario = LoadTestScenario.IdleClients,
                VerboseLogging = false
            };

            var orchestrator = new LoadTestOrchestrator(config);
            return await orchestrator.RunLoadTestAsync();
        }

        private static async Task<LoadTestResults> RunMediumScaleChatTest(string host, int port)
        {
            var config = new LoadTestConfiguration
            {
                ServerHost = host,
                ServerPort = port,
                ConcurrentClients = 25,
                TestDuration = TimeSpan.FromMinutes(2),
                Scenario = LoadTestScenario.ChatFlood,
                VerboseLogging = false
            };

            var orchestrator = new LoadTestOrchestrator(config);
            return await orchestrator.RunLoadTestAsync();
        }

        private static async Task<LoadTestResults> RunMovementStressTest(string host, int port)
        {
            var config = new LoadTestConfiguration
            {
                ServerHost = host,
                ServerPort = port,
                ConcurrentClients = 50,
                TestDuration = TimeSpan.FromMinutes(3),
                Scenario = LoadTestScenario.Movement,
                VerboseLogging = false
            };

            var orchestrator = new LoadTestOrchestrator(config);
            return await orchestrator.RunLoadTestAsync();
        }

        private static async Task<LoadTestResults> RunCombatLoadTest(string host, int port)
        {
            var config = new LoadTestConfiguration
            {
                ServerHost = host,
                ServerPort = port,
                ConcurrentClients = 30,
                TestDuration = TimeSpan.FromMinutes(3),
                Scenario = LoadTestScenario.Combat,
                VerboseLogging = false
            };

            var orchestrator = new LoadTestOrchestrator(config);
            return await orchestrator.RunLoadTestAsync();
        }

        private static async Task<LoadTestResults> RunConnectionScalingTest(string host, int port)
        {
            var config = new LoadTestConfiguration
            {
                ServerHost = host,
                ServerPort = port,
                ConcurrentClients = 100,
                TestDuration = TimeSpan.FromMinutes(5),
                Scenario = LoadTestScenario.IdleClients,
                ConnectionBatchSize = 10,
                ConnectionBatchDelay = 600,
                VerboseLogging = false
            };

            var orchestrator = new LoadTestOrchestrator(config);
            return await orchestrator.RunLoadTestAsync();
        }

        private static async Task<LoadTestResults> RunLargeScaleMixedTest(string host, int port)
        {
            var config = new LoadTestConfiguration
            {
                ServerHost = host,
                ServerPort = port,
                ConcurrentClients = 100,
                TestDuration = TimeSpan.FromMinutes(5),
                Scenario = LoadTestScenario.Mixed,
                ConnectionBatchSize = 20,
                ConnectionBatchDelay = 300,
                VerboseLogging = false
            };

            var orchestrator = new LoadTestOrchestrator(config);
            return await orchestrator.RunLoadTestAsync();
        }

        private static void PrintTestSummary(LoadTestResults results)
        {
            Console.WriteLine($"  Duration: {results.Duration:mm\\:ss}");
            Console.WriteLine($"  Connections: {results.SuccessfulConnections}/{results.TotalClients} ({results.ConnectionSuccessRate:P1})");
            Console.WriteLine($"  Actions: {results.TotalActions:N0} ({results.ActionsPerSecond:F1}/sec)");
            Console.WriteLine($"  Action Time: Avg={results.AverageActionTime.TotalMilliseconds:F1}ms");
            Console.WriteLine($"  Network: Sent={results.TotalPacketsSent}, Recv={results.TotalPacketsReceived}");
            Console.WriteLine($"  Errors: {results.TotalErrors}");
        }

        private static async Task<string> GenerateReport(List<(string TestName, LoadTestResults Results)> allResults, string host, int port)
        {
            var reportPath = $"LoadTestReport_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            using (var writer = new StreamWriter(reportPath))
            {
                await writer.WriteLineAsync("=".PadRight(80, '='));
                await writer.WriteLineAsync("ACE SERVER LOAD TEST REPORT");
                await writer.WriteLineAsync("=".PadRight(80, '='));
                await writer.WriteLineAsync();
                await writer.WriteLineAsync($"Report Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                await writer.WriteLineAsync($"Target Server: {host}:{port}");
                await writer.WriteLineAsync();

                await writer.WriteLineAsync("=".PadRight(80, '='));
                await writer.WriteLineAsync("EXECUTIVE SUMMARY");
                await writer.WriteLineAsync("=".PadRight(80, '='));
                await writer.WriteLineAsync();

                var totalConnections = 0;
                var totalSuccessful = 0;
                var totalActions = 0L;
                var totalErrors = 0;
                var totalDuration = TimeSpan.Zero;

                foreach (var (testName, results) in allResults)
                {
                    totalConnections += results.TotalClients;
                    totalSuccessful += results.SuccessfulConnections;
                    totalActions += results.TotalActions;
                    totalErrors += results.TotalErrors;
                    totalDuration += results.Duration;
                }

                await writer.WriteLineAsync($"Total Tests Run: {allResults.Count}");
                await writer.WriteLineAsync($"Total Duration: {totalDuration:hh\\:mm\\:ss}");
                var connectionRate = totalConnections > 0 ? $"{(double)totalSuccessful / totalConnections:P1}" : "N/A";
                await writer.WriteLineAsync($"Total Connections: {totalSuccessful}/{totalConnections} ({connectionRate})");
                await writer.WriteLineAsync($"Total Actions: {totalActions:N0}");
                await writer.WriteLineAsync($"Total Errors: {totalErrors}");
                await writer.WriteLineAsync();

                await writer.WriteLineAsync("=".PadRight(80, '='));
                await writer.WriteLineAsync("DETAILED TEST RESULTS");
                await writer.WriteLineAsync("=".PadRight(80, '='));
                await writer.WriteLineAsync();

                foreach (var (testName, results) in allResults)
                {
                    await writer.WriteLineAsync($"Test: {testName}");
                    await writer.WriteLineAsync("-".PadRight(80, '-'));
                    await writer.WriteLineAsync($"  Configuration:");
                    await writer.WriteLineAsync($"    Concurrent Clients: {results.Configuration.ConcurrentClients}");
                    await writer.WriteLineAsync($"    Scenario: {results.Configuration.Scenario}");
                    await writer.WriteLineAsync($"    Duration: {results.Configuration.TestDuration:mm\\:ss}");
                    await writer.WriteLineAsync();
                    await writer.WriteLineAsync($"  Results:");
                    await writer.WriteLineAsync($"    Actual Duration: {results.Duration:mm\\:ss\\.ff}");
                    await writer.WriteLineAsync($"    Connections: {results.SuccessfulConnections}/{results.TotalClients} ({results.ConnectionSuccessRate:P2})");
                    await writer.WriteLineAsync($"    Total Actions: {results.TotalActions:N0}");
                    await writer.WriteLineAsync($"    Actions/Second: {results.ActionsPerSecond:F2}");
                    await writer.WriteLineAsync();
                    await writer.WriteLineAsync($"  Performance Metrics:");
                    await writer.WriteLineAsync($"    Average Action Time: {results.AverageActionTime.TotalMilliseconds:F2} ms");
                    await writer.WriteLineAsync($"    Min Action Time: {results.MinActionTime.TotalMilliseconds:F2} ms");
                    await writer.WriteLineAsync($"    Max Action Time: {results.MaxActionTime.TotalMilliseconds:F2} ms");
                    await writer.WriteLineAsync($"    Average Connection Time: {results.AverageConnectionTime.TotalMilliseconds:F2} ms");
                    await writer.WriteLineAsync();
                    await writer.WriteLineAsync($"  Network:");
                    await writer.WriteLineAsync($"    Packets Sent: {results.TotalPacketsSent:N0}");
                    await writer.WriteLineAsync($"    Packets Received: {results.TotalPacketsReceived:N0}");
                    await writer.WriteLineAsync($"    Packets/Second: {results.PacketsPerSecond:F2}");
                    await writer.WriteLineAsync();
                    await writer.WriteLineAsync($"  Errors:");
                    await writer.WriteLineAsync($"    Total Errors: {results.TotalErrors}");
                    await writer.WriteLineAsync($"    Connection Failures: {results.FailedConnections}");
                    await writer.WriteLineAsync($"    Action Failures: {results.FailedActions}");
                    await writer.WriteLineAsync($"    Success Rate: {results.SuccessRate:P2}");
                    await writer.WriteLineAsync();
                    await writer.WriteLineAsync();
                }

                await writer.WriteLineAsync("=".PadRight(80, '='));
                await writer.WriteLineAsync("CSV DATA EXPORT");
                await writer.WriteLineAsync("=".PadRight(80, '='));
                await writer.WriteLineAsync();

                foreach (var (testName, results) in allResults)
                {
                    await writer.WriteLineAsync($"# {testName}");
                    await writer.WriteLineAsync(results.ToCsv());
                    await writer.WriteLineAsync();
                }

                await writer.WriteLineAsync("=".PadRight(80, '='));
                await writer.WriteLineAsync("END OF REPORT");
                await writer.WriteLineAsync("=".PadRight(80, '='));
            }

            Console.WriteLine($"Full report written to: {reportPath}");

            // Also print summary to console
            Console.WriteLine();
            Console.WriteLine("OVERALL SUMMARY:");
            Console.WriteLine("-".PadRight(80, '-'));
            
            int summaryConnections = 0;
            int summarySuccessful = 0;
            long summaryActions = 0L;
            int summaryErrors = 0;

            foreach (var (testName, results) in allResults)
            {
                summaryConnections += results.TotalClients;
                summarySuccessful += results.SuccessfulConnections;
                summaryActions += results.TotalActions;
                summaryErrors += results.TotalErrors;
            }

            Console.WriteLine($"Total Tests: {allResults.Count}");
            var summaryConnectionRate = summaryConnections > 0 ? $"{(double)summarySuccessful / summaryConnections:P1}" : "N/A";
            Console.WriteLine($"Total Connections: {summarySuccessful}/{summaryConnections} ({summaryConnectionRate})");
            Console.WriteLine($"Total Actions: {summaryActions:N0}");
            Console.WriteLine($"Total Errors: {summaryErrors}");
            
            return reportPath;
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:F2} {sizes[order]}";
        }
    }
}
