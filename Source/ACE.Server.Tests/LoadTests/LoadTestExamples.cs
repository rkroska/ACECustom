using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ACE.Server.Tests.LoadTests
{
    /// <summary>
    /// Example test suite demonstrating how to use the load test framework
    /// </summary>
    public class LoadTestExamples
    {
        private readonly ITestOutputHelper output;

        public LoadTestExamples(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact(Skip = "Manual load test - run explicitly")]
        [Trait("Category", "LoadTest")]
        public async Task SmallScaleIdleTest()
        {
            var config = new LoadTestConfiguration
            {
                ServerHost = "127.0.0.1",
                ServerPort = 9000,
                ConcurrentClients = 5,
                TestDuration = TimeSpan.FromMinutes(1),
                Scenario = LoadTestScenario.IdleClients,
                VerboseLogging = true
            };

            var orchestrator = new LoadTestOrchestrator(config);
            var results = await orchestrator.RunLoadTestAsync();

            output.WriteLine(results.ToCsv());
            Assert.True(results.ConnectionSuccessRate > 0.8, "Connection success rate should be > 80%");
        }

        [Fact(Skip = "Manual load test - run explicitly")]
        [Trait("Category", "LoadTest")]
        public async Task MediumScaleChatTest()
        {
            var config = new LoadTestConfiguration
            {
                ServerHost = "127.0.0.1",
                ServerPort = 9000,
                ConcurrentClients = 25,
                TestDuration = TimeSpan.FromMinutes(2),
                Scenario = LoadTestScenario.ChatFlood,
                ActionDelay = 500,
                VerboseLogging = false
            };

            var orchestrator = new LoadTestOrchestrator(config);
            var results = await orchestrator.RunLoadTestAsync();

            output.WriteLine($"Actions per second: {results.ActionsPerSecond:F2}");
            output.WriteLine($"Success rate: {results.SuccessRate:P2}");
            
            Assert.True(results.SuccessRate > 0.95, "Success rate should be > 95%");
        }

        [Fact(Skip = "Manual load test - run explicitly")]
        [Trait("Category", "LoadTest")]
        public async Task LargeScaleMixedTest()
        {
            var config = new LoadTestConfiguration
            {
                ServerHost = "127.0.0.1",
                ServerPort = 9000,
                ConcurrentClients = 100,
                TestDuration = TimeSpan.FromMinutes(5),
                Scenario = LoadTestScenario.Mixed,
                ActionDelay = 1000,
                ConnectionBatchSize = 10,
                ConnectionBatchDelay = 1000,
                VerboseLogging = false
            };

            var orchestrator = new LoadTestOrchestrator(config);
            var results = await orchestrator.RunLoadTestAsync();

            // Save results to file
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filename = $"loadtest_results_{timestamp}.csv";
            System.IO.File.WriteAllText(filename, results.ToCsv());
            output.WriteLine($"Results saved to: {filename}");

            Assert.True(results.ConnectionSuccessRate > 0.9, "Connection success rate should be > 90%");
            Assert.True(results.ActionsPerSecond > 50, "Should handle at least 50 actions/second");
        }

        [Fact(Skip = "Manual load test - run explicitly")]
        [Trait("Category", "LoadTest")]
        public async Task MovementStressTest()
        {
            var config = new LoadTestConfiguration
            {
                ServerHost = "127.0.0.1",
                ServerPort = 9000,
                ConcurrentClients = 50,
                TestDuration = TimeSpan.FromMinutes(3),
                Scenario = LoadTestScenario.Movement,
                ActionDelay = 100, // Very frequent updates
                VerboseLogging = false
            };

            var orchestrator = new LoadTestOrchestrator(config);
            var results = await orchestrator.RunLoadTestAsync();

            output.WriteLine($"Packets per second: {results.PacketsPerSecond:F2}");
            output.WriteLine($"Average action time: {results.AverageActionTime.TotalMilliseconds:F2}ms");

            Assert.True(results.AverageActionTime.TotalMilliseconds < 100, 
                "Average action time should be < 100ms");
        }

        [Fact(Skip = "Manual load test - run explicitly")]
        [Trait("Category", "LoadTest")]
        public async Task CombatLoadTest()
        {
            var config = new LoadTestConfiguration
            {
                ServerHost = "127.0.0.1",
                ServerPort = 9000,
                ConcurrentClients = 30,
                TestDuration = TimeSpan.FromMinutes(2),
                Scenario = LoadTestScenario.Combat,
                ActionDelay = 500,
                VerboseLogging = false
            };

            var orchestrator = new LoadTestOrchestrator(config);
            var results = await orchestrator.RunLoadTestAsync();

            output.WriteLine($"Total actions: {results.TotalActions}");
            output.WriteLine($"Failed actions: {results.FailedActions}");
            output.WriteLine($"Total errors: {results.TotalErrors}");

            Assert.True(results.FailedActions < results.TotalActions * 0.05,
                "Failed actions should be < 5% of total");
        }

        [Fact(Skip = "Manual load test - run explicitly")]
        [Trait("Category", "LoadTest")]
        public async Task ConnectionScalingTest()
        {
            // Test how the server handles gradual connection scaling
            var clientCounts = new[] { 10, 25, 50, 75, 100 };
            
            foreach (var clientCount in clientCounts)
            {
                output.WriteLine($"\n=== Testing with {clientCount} clients ===");
                
                var config = new LoadTestConfiguration
                {
                    ServerHost = "127.0.0.1",
                    ServerPort = 9000,
                    ConcurrentClients = clientCount,
                    TestDuration = TimeSpan.FromSeconds(30),
                    Scenario = LoadTestScenario.IdleClients,
                    VerboseLogging = false
                };

                var orchestrator = new LoadTestOrchestrator(config);
                var results = await orchestrator.RunLoadTestAsync();

                output.WriteLine($"Connection success rate: {results.ConnectionSuccessRate:P2}");
                output.WriteLine($"Average connection time: {results.AverageConnectionTime.TotalMilliseconds:F2}ms");
                
                // Brief pause between tests
                await Task.Delay(5000);
            }
        }

        [Fact(Skip = "Manual load test - run explicitly")]
        [Trait("Category", "LoadTest")]
        public async Task CharacterLoginLoadTest()
        {
            // This test focuses on character login and loading performance
            var config = new LoadTestConfiguration
            {
                ServerHost = "127.0.0.1",
                ServerPort = 9000,
                ConcurrentClients = 25,
                TestDuration = TimeSpan.FromMinutes(3),
                Scenario = LoadTestScenario.IdleClients, // Focus on login/connection
                ConnectionBatchSize = 5,
                ConnectionBatchDelay = 800,
                VerboseLogging = false,
                TestName = "Character Login Performance Test"
            };

            output.WriteLine("═══════════════════════════════════════════════════════════");
            output.WriteLine("  ACE Character Login & Loading Performance Test");
            output.WriteLine("═══════════════════════════════════════════════════════════");
            output.WriteLine("");
            output.WriteLine($"Server: {config.ServerHost}:{config.ServerPort}");
            output.WriteLine($"Clients: {config.ConcurrentClients}");
            output.WriteLine($"Duration: {config.TestDuration.TotalMinutes} minutes");
            output.WriteLine("");

            var orchestrator = new LoadTestOrchestrator(config);
            var results = await orchestrator.RunLoadTestAsync();

            // Display results
            output.WriteLine("");
            output.WriteLine("═══════════════════════════════════════════════════════════");
            output.WriteLine("                    TEST RESULTS");
            output.WriteLine("═══════════════════════════════════════════════════════════");
            output.WriteLine("");
            output.WriteLine($"CONNECTION METRICS:");
            output.WriteLine($"  Total Clients:         {results.TotalClients}");
            output.WriteLine($"  Successful:            {results.SuccessfulConnections} ({results.ConnectionSuccessRate:P2})");
            output.WriteLine($"  Failed:                {results.FailedConnections}");
            output.WriteLine($"  Avg Connection Time:   {results.AverageConnectionTime.TotalMilliseconds:F2}ms");
            output.WriteLine("");
            output.WriteLine($"ACTION METRICS:");
            output.WriteLine($"  Total Actions:         {results.TotalActions}");
            output.WriteLine($"  Actions/Second:        {results.ActionsPerSecond:F2}");
            output.WriteLine($"  Avg Action Time:       {results.AverageActionTime.TotalMilliseconds:F2}ms");
            output.WriteLine("");
            output.WriteLine($"NETWORK METRICS:");
            output.WriteLine($"  Packets Sent:          {results.TotalPacketsSent}");
            output.WriteLine($"  Packets Received:      {results.TotalPacketsReceived}");
            output.WriteLine($"  Packets/Second:        {results.PacketsPerSecond:F2}");
            output.WriteLine("");
            output.WriteLine($"ERROR METRICS:");
            output.WriteLine($"  Total Errors:          {results.TotalErrors}");
            output.WriteLine($"  Success Rate:          {results.SuccessRate:P2}");
            output.WriteLine("");

            // Save results
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var csvFilename = $"CharacterLogin_LoadTest_{timestamp}.csv";
            System.IO.File.WriteAllText(csvFilename, results.ToCsv());
            output.WriteLine($"Results saved to: {csvFilename}");
            output.WriteLine("");

            // Assertions
            Assert.True(results.ConnectionSuccessRate > 0.80, 
                $"Connection success rate should be > 80% (was {results.ConnectionSuccessRate:P2})");
            Assert.True(results.AverageConnectionTime.TotalMilliseconds < 2000,
                $"Average connection time should be < 2000ms (was {results.AverageConnectionTime.TotalMilliseconds:F2}ms)");
        }
    }
}
