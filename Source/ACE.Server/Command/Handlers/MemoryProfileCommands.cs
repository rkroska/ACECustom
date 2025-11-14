using ACE.Database;
using System;
using System.Diagnostics;
using System.Text;
using ACE.Entity.Enum;
using ACE.Server.Network;
using ACE.Server.Network.Managers;
using ACE.Server.Managers;

namespace ACE.Server.Command.Handlers
{
    public static class MemoryProfileCommands
    {
        [CommandHandler("memprofile", AccessLevel.Admin, CommandHandlerFlag.None, 0,
            "Displays detailed memory profile information",
            "")]
        public static void HandleMemProfile(Session session, params string[] parameters)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== ACE Memory Profile ===");
            sb.AppendLine();

            // GC Information
            var gcMemInfo = GC.GetGCMemoryInfo();
            sb.AppendLine("=== Garbage Collection ===");
            sb.AppendLine($"Total Memory: {GC.GetTotalMemory(false) / 1024 / 1024:N2} MB");
            sb.AppendLine($"Heap Size: {gcMemInfo.HeapSizeBytes / 1024 / 1024:N2} MB");
            sb.AppendLine($"Fragmented: {gcMemInfo.FragmentedBytes / 1024 / 1024:N2} MB");
            sb.AppendLine($"High Memory Load Threshold: {gcMemInfo.HighMemoryLoadThresholdBytes / 1024 / 1024:N2} MB");
            sb.AppendLine($"Memory Load: {gcMemInfo.MemoryLoadBytes / 1024 / 1024:N2} MB");
            sb.AppendLine($"Gen 0 Collections: {GC.CollectionCount(0):N0}");
            sb.AppendLine($"Gen 1 Collections: {GC.CollectionCount(1):N0}");
            sb.AppendLine($"Gen 2 Collections: {GC.CollectionCount(2):N0}");
            sb.AppendLine($"Pinned Objects: {gcMemInfo.PinnedObjectsCount:N0}");
            sb.AppendLine();

            // Process Information
            var process = Process.GetCurrentProcess();
            sb.AppendLine("=== Process Information ===");
            sb.AppendLine($"Working Set: {process.WorkingSet64 / 1024 / 1024:N2} MB");
            sb.AppendLine($"Private Memory: {process.PrivateMemorySize64 / 1024 / 1024:N2} MB");
            sb.AppendLine($"Virtual Memory: {process.VirtualMemorySize64 / 1024 / 1024:N2} MB");
            sb.AppendLine($"Paged Memory: {process.PagedMemorySize64 / 1024 / 1024:N2} MB");
            sb.AppendLine($"Threads: {process.Threads.Count}");
            sb.AppendLine($"Handles: {process.HandleCount:N0}");
            sb.AppendLine();

            // Cache Statistics
            sb.AppendLine("=== Cache Statistics ===");
            try
            {
                sb.AppendLine($"Weenie Cache: {DatabaseManager.World.GetWeenieCacheCount():N0} items");
                sb.AppendLine($"Landblocks Loaded: {LandblockManager.GetLoadedLandblocks().Count:N0}");
                sb.AppendLine($"Active Sessions: {NetworkManager.GetSessionCount():N0}");
                sb.AppendLine($"Online Players: {PlayerManager.GetOnlineCount():N0}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error retrieving cache stats: {ex.Message}");
            }
            sb.AppendLine();

            // Database Queue
            sb.AppendLine("=== Database ===");
            sb.AppendLine($"Shard Queue Count: {DatabaseManager.Shard.QueueCount:N0}");
            sb.AppendLine();

            CommandHandlerHelper.WriteOutputInfo(session, sb.ToString());
        }

        [CommandHandler("membaseline", AccessLevel.Admin, CommandHandlerFlag.None, 0,
            "Sets a memory baseline for comparison",
            "")]
        public static void HandleMemBaseline(Session session, params string[] parameters)
        {
            // Force a full GC before taking baseline
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true, true);

            var baseline = GC.GetTotalMemory(true);
            session.MemoryBaseline = (long)baseline;
            session.MemoryBaselineTimestamp = (long)DateTime.UtcNow.Ticks;

            CommandHandlerHelper.WriteOutputInfo(session,
                $"Memory baseline set: {baseline / 1024 / 1024:N2} MB");
        }

        [CommandHandler("memcompare", AccessLevel.Admin, CommandHandlerFlag.None, 0,
            "Compares current memory to baseline",
            "")]
        public static void HandleMemCompare(Session session, params string[] parameters)
        {
            var baselineObj = session.MemoryBaseline;
            if (baselineObj == null || (long)baselineObj == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session,
                    "No baseline set. Use @membaseline first.");
                return;
            }

            var baseline = (long)baselineObj;
            var baselineTime = session.MemoryBaselineTimestamp;
            var current = GC.GetTotalMemory(false);
            var growth = current - baseline;
            var growthPercent = (growth / (double)baseline) * 100;

            var timeSpan = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - (long)baselineTime);

            var sb = new StringBuilder();
            sb.AppendLine("=== Memory Comparison ===");
            sb.AppendLine($"Time Since Baseline: {timeSpan.TotalMinutes:N1} minutes");
            sb.AppendLine($"Baseline: {baseline / 1024 / 1024:N2} MB");
            sb.AppendLine($"Current: {current / 1024 / 1024:N2} MB");
            sb.AppendLine($"Growth: {growth / 1024 / 1024:N2} MB ({growthPercent:N2}%)");
            sb.AppendLine($"Growth Rate: {(growth / timeSpan.TotalHours) / 1024 / 1024:N2} MB/hour");
            sb.AppendLine();

            if (growthPercent > 50)
            {
                sb.AppendLine("⚠️ WARNING: Significant memory growth detected!");
                sb.AppendLine("Run @memleakcheck for detailed analysis");
            }
            else if (growthPercent > 25)
            {
                sb.AppendLine("⚠️ CAUTION: Moderate memory growth detected");
            }
            else
            {
                sb.AppendLine("✓ Memory growth within acceptable range");
            }

            CommandHandlerHelper.WriteOutputInfo(session, sb.ToString());
        }

        [CommandHandler("memleakcheck", AccessLevel.Admin, CommandHandlerFlag.None, 0,
            "Performs a memory leak analysis",
            "")]
        public static void HandleMemLeakCheck(Session session, params string[] parameters)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Memory Leak Analysis ===");
            sb.AppendLine();

            // Force GC and check what remains
            var beforeGC = GC.GetTotalMemory(false);
            sb.AppendLine($"Before GC: {beforeGC / 1024 / 1024:N2} MB");

            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true, true);

            var afterGC = GC.GetTotalMemory(true);
            sb.AppendLine($"After Full GC: {afterGC / 1024 / 1024:N2} MB");
            sb.AppendLine($"Retained (potential leaks): {afterGC / 1024 / 1024:N2} MB");
            sb.AppendLine($"Collected: {(beforeGC - afterGC) / 1024 / 1024:N2} MB");
            sb.AppendLine();

            // Check known caches
            sb.AppendLine("=== Cache Sizes ===");
            try
            {
                sb.AppendLine($"Weenie Cache: {DatabaseManager.World.GetWeenieCacheCount():N0} items");
                sb.AppendLine($"Landblocks Loaded: {LandblockManager.GetLoadedLandblocks().Count:N0}");
                sb.AppendLine($"Active Sessions: {NetworkManager.GetSessionCount():N0}");
                sb.AppendLine($"Online Players: {PlayerManager.GetOnlineCount():N0}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error: {ex.Message}");
            }
            sb.AppendLine();

            // GC stats
            var gcMemInfo = GC.GetGCMemoryInfo();
            sb.AppendLine("=== GC Statistics ===");
            sb.AppendLine($"Gen 0: {GC.CollectionCount(0):N0} collections");
            sb.AppendLine($"Gen 1: {GC.CollectionCount(1):N0} collections");
            sb.AppendLine($"Gen 2: {GC.CollectionCount(2):N0} collections");
            sb.AppendLine($"Fragmented Memory: {gcMemInfo.FragmentedBytes / 1024 / 1024:N2} MB");
            sb.AppendLine($"Pinned Objects: {gcMemInfo.PinnedObjectsCount:N0}");
            sb.AppendLine();

            // Process info
            var process = Process.GetCurrentProcess();
            sb.AppendLine("=== Process Statistics ===");
            sb.AppendLine($"Working Set: {process.WorkingSet64 / 1024 / 1024:N2} MB");
            sb.AppendLine($"Private Memory: {process.PrivateMemorySize64 / 1024 / 1024:N2} MB");
            sb.AppendLine($"Threads: {process.Threads.Count}");

            CommandHandlerHelper.WriteOutputInfo(session, sb.ToString());
        }

        [CommandHandler("memgc", AccessLevel.Admin, CommandHandlerFlag.None, 0,
            "Forces garbage collection",
            "[generation]")]
        public static void HandleMemGC(Session session, params string[] parameters)
        {
            var generation = 2; // Full collection by default

            if (parameters.Length > 0 && int.TryParse(parameters[0], out var gen))
            {
                if (gen >= 0 && gen <= 2)
                    generation = gen;
            }

            var before = GC.GetTotalMemory(false);
            var sw = Stopwatch.StartNew();

            GC.Collect(generation, GCCollectionMode.Forced, true, generation == 2);
            if (generation == 2)
                GC.WaitForPendingFinalizers();

            sw.Stop();
            var after = GC.GetTotalMemory(true);
            var freed = before - after;

            CommandHandlerHelper.WriteOutputInfo(session,
                $"Gen {generation} GC completed in {sw.ElapsedMilliseconds}ms\n" +
                $"Before: {before / 1024 / 1024:N2} MB\n" +
                $"After: {after / 1024 / 1024:N2} MB\n" +
                $"Freed: {freed / 1024 / 1024:N2} MB");
        }

        [CommandHandler("memmonitor", AccessLevel.Admin, CommandHandlerFlag.None, 0,
            "Starts/stops continuous memory monitoring",
            "[start|stop]")]
        public static void HandleMemMonitor(Session session, params string[] parameters)
        {
            if (parameters.Length == 0 || parameters[0].ToLower() == "start")
            {
                MemoryMonitor.Start(session);
                CommandHandlerHelper.WriteOutputInfo(session,
                    "Memory monitoring started. Stats will be reported every 30 seconds.\n" +
                    "Use '@memmonitor stop' to end monitoring.");
            }
            else if (parameters[0].ToLower() == "stop")
            {
                MemoryMonitor.Stop(session);
                CommandHandlerHelper.WriteOutputInfo(session,
                    "Memory monitoring stopped.");
            }
        }
    }
}
