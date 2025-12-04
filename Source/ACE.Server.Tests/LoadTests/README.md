# ACE Load Test Suite

A comprehensive load testing framework for ACE (Asheron's Call Emulator) that simulates real game clients by reverse engineering the network protocol and game events.

## Overview

This load test suite mimics actual game client behavior by:
- Implementing the complete client-server handshake protocol
- Simulating realistic player actions (movement, chat, combat, item manipulation)
- Measuring server performance under various load scenarios
- Providing detailed metrics and reporting

## Features

### Supported Game Actions

The framework can simulate:
- **Connection & Authentication**: Full 3-way handshake, account login, character selection
- **Movement**: Player position updates with realistic frequency
- **Chat**: Public chat, direct messages, emotes
- **Combat**: Melee attacks, spell casting, targeting
- **Items**: Pickup, drop, use, inventory manipulation
- **Social**: Friend management, allegiance actions
- **Ping/Heartbeat**: Keep-alive packets

### Test Scenarios

1. **IdleClients**: Clients connect and remain idle (tests baseline server load)
2. **ChatFlood**: Continuous chat message flooding (tests message processing)
3. **Movement**: Constant player movement (tests position update handling)
4. **Combat**: Combat actions and attacks (tests game logic processing)
5. **ItemManipulation**: Item interactions (tests inventory system)
6. **Mixed**: Realistic blend of all actions (tests real-world performance)

### Metrics Collected

- **Connection Metrics**: Success rate, connection time, authentication latency
- **Action Metrics**: Actions/second, success rate, response times (min/avg/max)
- **Network Metrics**: Packets sent/received, throughput, packet loss
- **Error Metrics**: Error counts, error types, stack traces

## Usage

### Quick Start

```csharp
using ACE.Server.Tests.LoadTests;

// Create configuration
var config = new LoadTestConfiguration
{
    ServerHost = "127.0.0.1",
    ServerPort = 9000,
    ConcurrentClients = 50,
    TestDuration = TimeSpan.FromMinutes(5),
    Scenario = LoadTestScenario.Mixed,
    ActionDelay = 1000
};

// Run load test
var orchestrator = new LoadTestOrchestrator(config);
var results = await orchestrator.RunLoadTestAsync();

// View results
Console.WriteLine($"Success Rate: {results.SuccessRate:P2}");
Console.WriteLine($"Actions/Second: {results.ActionsPerSecond:F2}");
Console.WriteLine($"Avg Response Time: {results.AverageActionTime.TotalMilliseconds:F2}ms");
```

### Running Pre-built Test Examples

```bash
# Run all load tests
dotnet test --filter "Category=LoadTest"

# Run specific test
dotnet test --filter "FullyQualifiedName~SmallScaleIdleTest"

# Note: Tests are marked with Skip attribute - remove it to enable
```

### Configuration Options

```csharp
var config = new LoadTestConfiguration
{
    // Server connection
    ServerHost = "127.0.0.1",
    ServerPort = 9000,
    
    // Load parameters
    ConcurrentClients = 100,        // Number of simulated clients
    TestDuration = TimeSpan.FromMinutes(5),
    
    // Scenario selection
    Scenario = LoadTestScenario.Mixed,
    
    // Timing controls
    ActionDelay = 1000,              // Delay between actions (ms)
    ConnectionBatchSize = 10,        // Clients per connection batch
    ConnectionBatchDelay = 500,      // Delay between batches (ms)
    
    // Debugging
    VerboseLogging = false,          // Enable detailed logging
    TestName = "Custom Test"
};
```

## Test Scenarios in Detail

### 1. Idle Clients
Tests baseline server capacity by connecting clients that only send periodic pings.

**Use Case**: Determine maximum concurrent connections the server can sustain.

**Expected Behavior**: Server should maintain thousands of idle connections with minimal CPU/memory usage.

### 2. Chat Flood
All clients continuously send chat messages at high frequency.

**Use Case**: Stress test message broadcasting and chat system.

**Expected Behavior**: Server should handle message routing without significant lag or message loss.

### 3. Movement
Clients continuously update their position (simulating active players moving around).

**Use Case**: Test position update handling and landblock management.

**Expected Behavior**: Server should process position updates and broadcast them to nearby players efficiently.

### 4. Combat
Clients perform continuous combat actions (attacks, spell casts).

**Use Case**: Stress test game logic processing and combat calculations.

**Expected Behavior**: Server should handle combat calculations and apply results without significant delays.

### 5. Item Manipulation
Clients continuously interact with items (pickup, drop, use).

**Use Case**: Test inventory system and item state management.

**Expected Behavior**: Server should handle item state changes and persist them correctly.

### 6. Mixed (Realistic)
Blend of all actions with realistic distribution:
- 30% movement
- 20% chat
- 20% combat
- 20% item interaction
- 10% idle

**Use Case**: Simulate realistic player behavior for comprehensive testing.

**Expected Behavior**: Server should handle mixed workload smoothly representing real-world usage.

## Example Results

```
=== Load Test Results ===
Duration: 00:05:00
Scenario: Mixed

Connection Metrics:
  Total Clients: 100
  Successful: 98
  Failed: 2
  Avg Connection Time: 245.32ms

Action Metrics:
  Total Actions: 28,547
  Failed Actions: 23
  Actions/Second: 95.16
  Avg Action Time: 15.43ms
  Min Action Time: 2.15ms
  Max Action Time: 234.67ms

Network Metrics:
  Total Packets Sent: 85,641
  Total Packets Received: 171,282
  Packets/Second: 856.41

Errors: 2
```

## Exporting Results

```csharp
// Export to CSV
var csv = results.ToCsv();
File.WriteAllText("results.csv", csv);

// Export to JSON
var json = results.ToJson();
File.WriteAllText("results.json", json);
```

## Performance Benchmarks

Based on testing with the optimized ACE server:

| Metric | Target | Acceptable | Poor |
|--------|--------|------------|------|
| Connection Success Rate | > 99% | > 95% | < 95% |
| Action Success Rate | > 99% | > 98% | < 98% |
| Actions/Second (100 clients) | > 100 | > 75 | < 75 |
| Avg Response Time | < 50ms | < 100ms | > 100ms |
| Packets/Second | > 1000 | > 500 | < 500 |

## Architecture

### LoadTestClient
Simulates a single game client with full protocol implementation:
- UDP packet handling
- Sequence number management
- Message fragmentation/reassembly
- Game action encoding

### LoadTestOrchestrator
Manages multiple clients and coordinates test execution:
- Client lifecycle management
- Scenario execution
- Metrics collection
- Result aggregation

### LoadTestConfiguration
Configures test parameters and scenarios.

### LoadTestResults
Comprehensive test results with exportable formats.

## Advanced Usage

### Custom Scenarios

```csharp
// Create custom client behavior
var client = new LoadTestClient("127.0.0.1", 9000);
await client.ConnectAsync("testaccount", "password");
await client.EnterWorldAsync("TestChar");

// Custom action sequence
await client.MoveAsync(10.5f, 20.3f, 0f);
await Task.Delay(500);
await client.SendChatAsync("Hello world!");
await Task.Delay(500);
await client.UseItemAsync(0x70000001);
```

### Monitoring and Hooks

```csharp
var client = new LoadTestClient("127.0.0.1", 9000);

// Hook into events
client.OnLog += msg => Console.WriteLine(msg);
client.OnError += ex => LogError(ex);
client.OnConnected += () => Console.WriteLine("Client connected!");
client.OnDisconnected += () => Console.WriteLine("Client disconnected!");
```

## Best Practices

1. **Start Small**: Begin with 5-10 clients and gradually increase
2. **Stagger Connections**: Use ConnectionBatchSize to avoid overwhelming the server
3. **Monitor Server**: Watch CPU, memory, and network usage during tests
4. **Baseline First**: Run idle tests to establish baseline before stress testing
5. **Save Results**: Export results to track performance over time
6. **Clean Environment**: Run tests on clean server state for consistent results

## Troubleshooting

### Clients fail to connect
- Verify server is running and accessible
- Check firewall rules
- Ensure correct server host/port

### Low actions per second
- Increase `ActionDelay` for more frequent actions
- Reduce `ConcurrentClients` if server is overloaded

### High error rates
- Check server logs for exceptions
- Verify client protocol implementation matches server version
- Ensure adequate system resources

## Contributing

To add new game actions:

1. Implement the action in `LoadTestClient`
2. Add scenario support in `LoadTestOrchestrator`
3. Create test example in `LoadTestExamples`
4. Update this README

## License

This load test suite is part of the ACE project and follows the same license terms.
