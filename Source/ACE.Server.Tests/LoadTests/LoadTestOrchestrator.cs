using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ACE.Server.Tests.LoadTests
{
    /// <summary>
    /// Orchestrates load tests with multiple simulated clients performing various game actions.
    /// Provides comprehensive metrics and reporting on server performance under load.
    /// </summary>
    public class LoadTestOrchestrator
    {
        private readonly List<LoadTestClient> clients = new List<LoadTestClient>();
        private readonly LoadTestConfiguration config;
        private readonly LoadTestMetrics metrics;
        private readonly object metricsLock = new object();
        private CancellationTokenSource cancellationTokenSource;
        private bool isRunning;

        public LoadTestOrchestrator(LoadTestConfiguration configuration)
        {
            config = configuration ?? throw new ArgumentNullException(nameof(configuration));
            metrics = new LoadTestMetrics();
        }

        /// <summary>
        /// Executes a comprehensive load test scenario
        /// </summary>
        public async Task<LoadTestResults> RunLoadTestAsync()
        {
            Console.WriteLine("=== ACE Load Test Suite ===");
            Console.WriteLine($"Server: {config.ServerHost}:{config.ServerPort}");
            Console.WriteLine($"Concurrent Clients: {config.ConcurrentClients}");
            Console.WriteLine($"Duration: {config.TestDuration}");
            Console.WriteLine($"Scenario: {config.Scenario}");
            Console.WriteLine();

            var testStart = DateTime.UtcNow;
            cancellationTokenSource = new CancellationTokenSource();
            isRunning = true;

            try
            {
                // Phase 1: Connect all clients
                Console.WriteLine("Phase 1: Connecting clients...");
                await ConnectClientsAsync();

                // Phase 2: Enter world with all clients
                Console.WriteLine("Phase 2: Entering world...");
                await EnterWorldAsync();

                // Phase 3: Run test scenario
                Console.WriteLine($"Phase 3: Running {config.Scenario} scenario...");
                await RunScenarioAsync(cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Load test error: {ex.Message}");
                metrics.Errors.Add(ex);
            }
            finally
            {
                isRunning = false;
                
                // Phase 4: Disconnect all clients (always runs on success or failure)
                try
                {
                    Console.WriteLine("Phase 4: Disconnecting clients...");
                    await DisconnectClientsAsync();
                }
                catch (Exception cleanupEx)
                {
                    Console.WriteLine($"Error during cleanup: {cleanupEx.Message}");
                    lock (metricsLock)
                    {
                        metrics.Errors.Add(cleanupEx);
                    }
                }
            }

            var testEnd = DateTime.UtcNow;
            var results = GenerateResults(testStart, testEnd);
            
            PrintResults(results);
            
            // Clear clients list after results are generated
            clients.Clear();
            
            return results;
        }

        /// <summary>
        /// Stops the load test gracefully
        /// </summary>
        public void Stop()
        {
            Console.WriteLine("Stopping load test...");
            cancellationTokenSource?.Cancel();
            isRunning = false;
        }

        #region Client Management

        private async Task ConnectClientsAsync()
        {
            var connectTasks = new List<Task>();

            for (int i = 0; i < config.ConcurrentClients; i++)
            {
                var accountName = $"loadtest_{i:D4}";
                var password = "TestPassword123";
                
                var client = new LoadTestClient(config.ServerHost, config.ServerPort);
                client.OnLog += msg => { if (config.VerboseLogging) Console.WriteLine(msg); };
                client.OnError += ex => { lock (metricsLock) { metrics.Errors.Add(ex); } };
                
                clients.Add(client);

                // Stagger connections to avoid overwhelming the server
                if (i > 0 && i % config.ConnectionBatchSize == 0)
                {
                    await Task.Delay(config.ConnectionBatchDelay);
                }

                connectTasks.Add(ConnectClientAsync(client, accountName, password));
            }

            await Task.WhenAll(connectTasks);
            
            var successfulConnections = clients.Count(c => c.State == LoadTestClientState.Connected);
            Console.WriteLine($"Connected: {successfulConnections}/{config.ConcurrentClients} clients");
            lock (metricsLock)
            {
                metrics.SuccessfulConnections = successfulConnections;
            }
        }

        private async Task ConnectClientAsync(LoadTestClient client, string accountName, string password)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var success = await client.ConnectAsync(accountName, password);
                sw.Stop();

                lock (metricsLock)
                {
                    if (success)
                    {
                        metrics.ConnectionTimes.Add(sw.Elapsed);
                    }
                    else
                    {
                        metrics.FailedConnections++;
                    }
                }
            }
            catch (Exception ex)
            {
                lock (metricsLock)
                {
                    metrics.Errors.Add(ex);
                    metrics.FailedConnections++;
                }
            }
        }

        private async Task EnterWorldAsync()
        {
            var enterTasks = new List<Task>();

            for (int i = 0; i < clients.Count; i++)
            {
                var client = clients[i];
                if (client.State == LoadTestClientState.Connected)
                {
                    var characterName = $"LoadTest{i:D4}";
                    enterTasks.Add(EnterWorldClientAsync(client, characterName));

                    // Stagger world entry
                    if (i > 0 && i % 10 == 0)
                    {
                        await Task.Delay(50);
                    }
                }
            }

            await Task.WhenAll(enterTasks);
            
            var inWorld = clients.Count(c => c.State == LoadTestClientState.InWorld);
            Console.WriteLine($"In world: {inWorld} clients");
        }

        private async Task EnterWorldClientAsync(LoadTestClient client, string characterName)
        {
            try
            {
                await client.EnterWorldAsync(characterName);
            }
            catch (Exception ex)
            {
                lock (metricsLock)
                {
                    metrics.Errors.Add(ex);
                }
            }
        }

        private async Task DisconnectClientsAsync()
        {
            foreach (var client in clients)
            {
                try
                {
                    client.Disconnect();
                    client.Dispose();
                }
                catch (Exception ex)
                {
                    lock (metricsLock)
                    {
                        metrics.Errors.Add(ex);
                    }
                }
            }

            await Task.Delay(100);
            // Note: clients list is NOT cleared here to preserve data for result aggregation
            // It will be cleared after results are generated in RunLoadTestAsync
        }

        #endregion

        #region Test Scenarios

        private async Task RunScenarioAsync(CancellationToken cancellationToken)
        {
            var scenarioStart = DateTime.UtcNow;
            var endTime = scenarioStart.Add(config.TestDuration);

            switch (config.Scenario)
            {
                case LoadTestScenario.IdleClients:
                    await RunIdleScenarioAsync(endTime, cancellationToken);
                    break;

                case LoadTestScenario.ChatFlood:
                    await RunChatFloodScenarioAsync(endTime, cancellationToken);
                    break;

                case LoadTestScenario.Movement:
                    await RunMovementScenarioAsync(endTime, cancellationToken);
                    break;

                case LoadTestScenario.Combat:
                    await RunCombatScenarioAsync(endTime, cancellationToken);
                    break;

                case LoadTestScenario.ItemManipulation:
                    await RunItemManipulationScenarioAsync(endTime, cancellationToken);
                    break;

                case LoadTestScenario.Mixed:
                    await RunMixedScenarioAsync(endTime, cancellationToken);
                    break;

                default:
                    throw new ArgumentException($"Unknown scenario: {config.Scenario}");
            }
        }

        private async Task RunIdleScenarioAsync(DateTime endTime, CancellationToken cancellationToken)
        {
            Console.WriteLine("Running idle scenario (clients connected but inactive)...");
            
            while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
            {
                // Periodic pings to keep connections alive
                foreach (var client in clients.Where(c => c.State == LoadTestClientState.InWorld))
                {
                    await client.PingAsync();
                    lock (metricsLock)
                    {
                        metrics.ActionCount++;
                    }
                }

                await Task.Delay(5000, cancellationToken);
            }
        }

        private async Task RunChatFloodScenarioAsync(DateTime endTime, CancellationToken cancellationToken)
        {
            Console.WriteLine("Running chat flood scenario...");
            var random = new Random();

            while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
            {
                var tasks = new List<Task>();

                foreach (var client in clients.Where(c => c.State == LoadTestClientState.InWorld))
                {
                    var message = $"Load test message {random.Next(1000, 9999)}";
                    tasks.Add(SendChatWithMetricsAsync(client, message));
                }

                await Task.WhenAll(tasks);
                await Task.Delay(config.ActionDelay, cancellationToken);
            }
        }

        private async Task RunMovementScenarioAsync(DateTime endTime, CancellationToken cancellationToken)
        {
            Console.WriteLine("Running movement scenario...");
            var random = new Random();

            while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
            {
                var tasks = new List<Task>();

                foreach (var client in clients.Where(c => c.State == LoadTestClientState.InWorld))
                {
                    // Random movement in a small area
                    var x = (float)(random.NextDouble() * 100 - 50);
                    var y = (float)(random.NextDouble() * 100 - 50);
                    var z = 0f;

                    tasks.Add(MoveWithMetricsAsync(client, x, y, z));
                }

                await Task.WhenAll(tasks);
                await Task.Delay(config.ActionDelay, cancellationToken);
            }
        }

        private async Task RunCombatScenarioAsync(DateTime endTime, CancellationToken cancellationToken)
        {
            Console.WriteLine("Running combat scenario...");
            var random = new Random();

            while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
            {
                var tasks = new List<Task>();

                foreach (var client in clients.Where(c => c.State == LoadTestClientState.InWorld))
                {
                    // Simulate attacking a target
                    var targetId = unchecked((uint)(0x80000000 + random.Next(0, 0x10000000)));
                    var attackHeight = (uint)random.Next(1, 4);
                    var powerLevel = (float)random.NextDouble();

                    tasks.Add(AttackWithMetricsAsync(client, targetId, attackHeight, powerLevel));
                }

                await Task.WhenAll(tasks);
                await Task.Delay(config.ActionDelay, cancellationToken);
            }
        }

        private async Task RunItemManipulationScenarioAsync(DateTime endTime, CancellationToken cancellationToken)
        {
            Console.WriteLine("Running item manipulation scenario...");
            var random = new Random();

            while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
            {
                var tasks = new List<Task>();

                foreach (var client in clients.Where(c => c.State == LoadTestClientState.InWorld))
                {
                    var itemId = (uint)random.Next(0x70000000, 0x7FFFFFFF);
                    
                    // Alternate between pickup and drop
                    if (random.Next(2) == 0)
                    {
                        tasks.Add(PickupWithMetricsAsync(client, itemId));
                    }
                    else
                    {
                        tasks.Add(DropWithMetricsAsync(client, itemId));
                    }
                }

                await Task.WhenAll(tasks);
                await Task.Delay(config.ActionDelay, cancellationToken);
            }
        }

        private async Task RunMixedScenarioAsync(DateTime endTime, CancellationToken cancellationToken)
        {
            Console.WriteLine("Running mixed scenario (realistic player behavior)...");
            var random = new Random();

            while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
            {
                var tasks = new List<Task>();

                foreach (var client in clients.Where(c => c.State == LoadTestClientState.InWorld))
                {
                    var action = random.Next(100);

                    if (action < 30) // 30% movement
                    {
                        var x = (float)(random.NextDouble() * 100 - 50);
                        var y = (float)(random.NextDouble() * 100 - 50);
                        tasks.Add(MoveWithMetricsAsync(client, x, y, 0));
                    }
                    else if (action < 50) // 20% chat
                    {
                        tasks.Add(SendChatWithMetricsAsync(client, "Test message"));
                    }
                    else if (action < 70) // 20% combat
                    {
                        var targetId = unchecked((uint)(0x80000000 + random.Next(0, 0x10000000)));
                        tasks.Add(AttackWithMetricsAsync(client, targetId, 2, 0.7f));
                    }
                    else if (action < 90) // 20% item interaction
                    {
                        var itemId = (uint)random.Next(0x70000000, 0x7FFFFFFF);
                        tasks.Add(UseItemWithMetricsAsync(client, itemId));
                    }
                    // 10% idle
                }

                await Task.WhenAll(tasks);
                await Task.Delay(config.ActionDelay, cancellationToken);
            }
        }

        #endregion

        #region Metrics Collection

        private async Task SendChatWithMetricsAsync(LoadTestClient client, string message)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await client.SendChatAsync(message);
                sw.Stop();
                lock (metricsLock)
                {
                    metrics.ActionTimes.Add(sw.Elapsed);
                    metrics.ActionCount++;
                }
            }
            catch (Exception ex)
            {
                lock (metricsLock)
                {
                    metrics.Errors.Add(ex);
                    metrics.FailedActions++;
                }
            }
        }

        private async Task MoveWithMetricsAsync(LoadTestClient client, float x, float y, float z)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await client.MoveAsync(x, y, z);
                sw.Stop();
                lock (metricsLock)
                {
                    metrics.ActionTimes.Add(sw.Elapsed);
                    metrics.ActionCount++;
                }
            }
            catch (Exception ex)
            {
                lock (metricsLock)
                {
                    metrics.Errors.Add(ex);
                    metrics.FailedActions++;
                }
            }
        }

        private async Task AttackWithMetricsAsync(LoadTestClient client, uint targetId, uint height, float power)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await client.MeleeAttackAsync(targetId, height, power);
                sw.Stop();
                lock (metricsLock)
                {
                    metrics.ActionTimes.Add(sw.Elapsed);
                    metrics.ActionCount++;
                }
            }
            catch (Exception ex)
            {
                lock (metricsLock)
                {
                    metrics.Errors.Add(ex);
                    metrics.FailedActions++;
                }
            }
        }

        private async Task UseItemWithMetricsAsync(LoadTestClient client, uint itemId)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await client.UseItemAsync(itemId);
                sw.Stop();
                lock (metricsLock)
                {
                    metrics.ActionTimes.Add(sw.Elapsed);
                    metrics.ActionCount++;
                }
            }
            catch (Exception ex)
            {
                lock (metricsLock)
                {
                    metrics.Errors.Add(ex);
                    metrics.FailedActions++;
                }
            }
        }

        private async Task PickupWithMetricsAsync(LoadTestClient client, uint itemId)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await client.PickupItemAsync(itemId);
                sw.Stop();
                lock (metricsLock)
                {
                    metrics.ActionTimes.Add(sw.Elapsed);
                    metrics.ActionCount++;
                }
            }
            catch (Exception ex)
            {
                lock (metricsLock)
                {
                    metrics.Errors.Add(ex);
                    metrics.FailedActions++;
                }
            }
        }

        private async Task DropWithMetricsAsync(LoadTestClient client, uint itemId)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await client.DropItemAsync(itemId);
                sw.Stop();
                lock (metricsLock)
                {
                    metrics.ActionTimes.Add(sw.Elapsed);
                    metrics.ActionCount++;
                }
            }
            catch (Exception ex)
            {
                lock (metricsLock)
                {
                    metrics.Errors.Add(ex);
                    metrics.FailedActions++;
                }
            }
        }

        #endregion

        #region Results Generation

        private LoadTestResults GenerateResults(DateTime start, DateTime end)
        {
            var duration = end - start;

            return new LoadTestResults
            {
                StartTime = start,
                EndTime = end,
                Duration = duration,
                Configuration = config,
                
                // Connection metrics
                TotalClients = config.ConcurrentClients,
                SuccessfulConnections = metrics.SuccessfulConnections,
                FailedConnections = metrics.FailedConnections,
                AverageConnectionTime = metrics.ConnectionTimes.Any() 
                    ? TimeSpan.FromMilliseconds(metrics.ConnectionTimes.Average(t => t.TotalMilliseconds))
                    : TimeSpan.Zero,
                
                // Action metrics
                TotalActions = metrics.ActionCount,
                FailedActions = metrics.FailedActions,
                ActionsPerSecond = duration.TotalSeconds > 0 
                    ? metrics.ActionCount / duration.TotalSeconds
                    : 0,
                AverageActionTime = metrics.ActionTimes.Any()
                    ? TimeSpan.FromMilliseconds(metrics.ActionTimes.Average(t => t.TotalMilliseconds))
                    : TimeSpan.Zero,
                MinActionTime = metrics.ActionTimes.Any() 
                    ? metrics.ActionTimes.Min()
                    : TimeSpan.Zero,
                MaxActionTime = metrics.ActionTimes.Any()
                    ? metrics.ActionTimes.Max()
                    : TimeSpan.Zero,
                
                // Network metrics
                TotalPacketsSent = clients.Sum(c => c.PacketsSent),
                TotalPacketsReceived = clients.Sum(c => c.PacketsReceived),
                PacketsPerSecond = duration.TotalSeconds > 0
                    ? (clients.Sum(c => c.PacketsSent) + clients.Sum(c => c.PacketsReceived)) / duration.TotalSeconds
                    : 0,
                
                // Error metrics
                TotalErrors = metrics.Errors.Count,
                Errors = metrics.Errors.Take(20).ToList() // Limit to first 20 errors
            };
        }

        private void PrintResults(LoadTestResults results)
        {
            Console.WriteLine();
            Console.WriteLine("=== Load Test Results ===");
            Console.WriteLine($"Duration: {results.Duration}");
            Console.WriteLine($"Scenario: {results.Configuration.Scenario}");
            Console.WriteLine();
            
            Console.WriteLine("Connection Metrics:");
            Console.WriteLine($"  Total Clients: {results.TotalClients}");
            Console.WriteLine($"  Successful: {results.SuccessfulConnections}");
            Console.WriteLine($"  Failed: {results.FailedConnections}");
            Console.WriteLine($"  Avg Connection Time: {results.AverageConnectionTime.TotalMilliseconds:F2}ms");
            Console.WriteLine();
            
            Console.WriteLine("Action Metrics:");
            Console.WriteLine($"  Total Actions: {results.TotalActions}");
            Console.WriteLine($"  Failed Actions: {results.FailedActions}");
            Console.WriteLine($"  Actions/Second: {results.ActionsPerSecond:F2}");
            Console.WriteLine($"  Avg Action Time: {results.AverageActionTime.TotalMilliseconds:F2}ms");
            Console.WriteLine($"  Min Action Time: {results.MinActionTime.TotalMilliseconds:F2}ms");
            Console.WriteLine($"  Max Action Time: {results.MaxActionTime.TotalMilliseconds:F2}ms");
            Console.WriteLine();
            
            Console.WriteLine("Network Metrics:");
            Console.WriteLine($"  Total Packets Sent: {results.TotalPacketsSent}");
            Console.WriteLine($"  Total Packets Received: {results.TotalPacketsReceived}");
            Console.WriteLine($"  Packets/Second: {results.PacketsPerSecond:F2}");
            Console.WriteLine();
            
            Console.WriteLine($"Errors: {results.TotalErrors}");
            if (results.Errors.Any())
            {
                Console.WriteLine("  First errors:");
                foreach (var error in results.Errors.Take(5))
                {
                    Console.WriteLine($"    {error.Message}");
                }
            }
        }

        #endregion
    }
}
