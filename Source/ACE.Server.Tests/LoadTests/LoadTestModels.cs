using System;
using System.Collections.Generic;

namespace ACE.Server.Tests.LoadTests
{
    /// <summary>
    /// Configuration for load test execution
    /// </summary>
    public class LoadTestConfiguration
    {
        /// <summary>
        /// Server hostname or IP address
        /// </summary>
        public string ServerHost { get; set; } = "127.0.0.1";

        /// <summary>
        /// Server port (default ACE port is 9000)
        /// </summary>
        public int ServerPort { get; set; } = 9000;

        /// <summary>
        /// Number of concurrent clients to simulate
        /// </summary>
        public int ConcurrentClients { get; set; } = 10;

        /// <summary>
        /// Duration of the test
        /// </summary>
        public TimeSpan TestDuration { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Test scenario to execute
        /// </summary>
        public LoadTestScenario Scenario { get; set; } = LoadTestScenario.Mixed;

        /// <summary>
        /// Delay between actions in milliseconds
        /// </summary>
        public int ActionDelay { get; set; } = 1000;

        /// <summary>
        /// Number of clients to connect in each batch
        /// </summary>
        public int ConnectionBatchSize { get; set; } = 10;

        /// <summary>
        /// Delay between connection batches in milliseconds
        /// </summary>
        public int ConnectionBatchDelay { get; set; } = 500;

        /// <summary>
        /// Enable verbose logging
        /// </summary>
        public bool VerboseLogging { get; set; } = false;

        /// <summary>
        /// Custom test name
        /// </summary>
        public string TestName { get; set; } = "ACE Load Test";
    }

    /// <summary>
    /// Available load test scenarios
    /// </summary>
    public enum LoadTestScenario
    {
        /// <summary>
        /// Clients connect and remain idle
        /// </summary>
        IdleClients,

        /// <summary>
        /// Continuous chat message flooding
        /// </summary>
        ChatFlood,

        /// <summary>
        /// Constant player movement
        /// </summary>
        Movement,

        /// <summary>
        /// Combat actions (attacks, spells)
        /// </summary>
        Combat,

        /// <summary>
        /// Item pickup, drop, and use actions
        /// </summary>
        ItemManipulation,

        /// <summary>
        /// Realistic mix of all action types
        /// </summary>
        Mixed
    }

    /// <summary>
    /// Internal metrics collection during load test
    /// </summary>
    internal class LoadTestMetrics
    {
        public int SuccessfulConnections { get; set; }
        public int FailedConnections { get; set; }
        public List<TimeSpan> ConnectionTimes { get; } = new List<TimeSpan>();

        public int ActionCount { get; set; }
        public int FailedActions { get; set; }
        public List<TimeSpan> ActionTimes { get; } = new List<TimeSpan>();

        public List<Exception> Errors { get; } = new List<Exception>();
    }

    /// <summary>
    /// Comprehensive results from a load test execution
    /// </summary>
    public class LoadTestResults
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public LoadTestConfiguration Configuration { get; set; }

        // Connection metrics
        public int TotalClients { get; set; }
        public int SuccessfulConnections { get; set; }
        public int FailedConnections { get; set; }
        public TimeSpan AverageConnectionTime { get; set; }

        // Action metrics
        public int TotalActions { get; set; }
        public int FailedActions { get; set; }
        public double ActionsPerSecond { get; set; }
        public TimeSpan AverageActionTime { get; set; }
        public TimeSpan MinActionTime { get; set; }
        public TimeSpan MaxActionTime { get; set; }

        // Network metrics
        public int TotalPacketsSent { get; set; }
        public int TotalPacketsReceived { get; set; }
        public double PacketsPerSecond { get; set; }

        // Error metrics
        public int TotalErrors { get; set; }
        public List<Exception> Errors { get; set; }

        /// <summary>
        /// Overall success rate (0.0 to 1.0)
        /// </summary>
        public double SuccessRate =>
            TotalActions > 0 ? (TotalActions - FailedActions) / (double)TotalActions : 0.0;

        /// <summary>
        /// Connection success rate (0.0 to 1.0)
        /// </summary>
        public double ConnectionSuccessRate =>
            TotalClients > 0 ? SuccessfulConnections / (double)TotalClients : 0.0;

        /// <summary>
        /// Export results to CSV format
        /// </summary>
        public string ToCsv()
        {
            var csv = "Metric,Value\n";
            csv += $"Test Start,{StartTime:O}\n";
            csv += $"Test End,{EndTime:O}\n";
            csv += $"Duration (seconds),{Duration.TotalSeconds:F2}\n";
            csv += $"Scenario,{Configuration.Scenario}\n";
            csv += $"Total Clients,{TotalClients}\n";
            csv += $"Successful Connections,{SuccessfulConnections}\n";
            csv += $"Failed Connections,{FailedConnections}\n";
            csv += $"Connection Success Rate,{ConnectionSuccessRate:P2}\n";
            csv += $"Avg Connection Time (ms),{AverageConnectionTime.TotalMilliseconds:F2}\n";
            csv += $"Total Actions,{TotalActions}\n";
            csv += $"Failed Actions,{FailedActions}\n";
            csv += $"Success Rate,{SuccessRate:P2}\n";
            csv += $"Actions Per Second,{ActionsPerSecond:F2}\n";
            csv += $"Avg Action Time (ms),{AverageActionTime.TotalMilliseconds:F2}\n";
            csv += $"Min Action Time (ms),{MinActionTime.TotalMilliseconds:F2}\n";
            csv += $"Max Action Time (ms),{MaxActionTime.TotalMilliseconds:F2}\n";
            csv += $"Total Packets Sent,{TotalPacketsSent}\n";
            csv += $"Total Packets Received,{TotalPacketsReceived}\n";
            csv += $"Packets Per Second,{PacketsPerSecond:F2}\n";
            csv += $"Total Errors,{TotalErrors}\n";
            return csv;
        }

        /// <summary>
        /// Export results to JSON format
        /// </summary>
        public string ToJson()
        {
            return System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
    }
}
