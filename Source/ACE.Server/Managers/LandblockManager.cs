using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using log4net;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.WorldObjects;
using ACE.Database;
using ACE.Database.Models.World;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Handles loading/unloading landblocks, and their adjacencies
    /// </summary>
    public static class LandblockManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Locking mechanism provides concurrent access to collections
        /// </summary>
        private static readonly ReaderWriterLockSlim landblockLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        /// <summary>
        /// A table of all the landblocks in the world map
        /// Landblocks which aren't currently loaded will be null here
        /// X, Y, Variation dimensions
        /// </summary>
        //private static readonly Landblock[,,] landblocks = new Landblock[255, 255, 999];
        public static readonly ConcurrentDictionary<VariantCacheId, Landblock> landblocks = new ConcurrentDictionary<VariantCacheId, Landblock>();

        /// <summary>
        /// A lookup table of all the currently loaded landblocks
        /// </summary>
        public static readonly ConcurrentDictionary<VariantCacheId, Landblock> loadedLandblocks = new ConcurrentDictionary<VariantCacheId, Landblock>();

        private static readonly ConcurrentDictionary<VariantCacheId, Landblock> landblockGroupPendingAdditions = new ConcurrentDictionary<VariantCacheId, Landblock>();
        public static readonly List<LandblockGroup> landblockGroups = new List<LandblockGroup>();

        /// <summary>
        /// Variant review 2026-09-17: serializes "not loaded -> construct -> register" in <see cref="GetLandblock(LandblockId, bool, int?, bool)"/>.
        /// Two group threads first-loading the same (landblock, variation) used to both construct; the loser's
        /// registration then REPLACED the winner in <see cref="landblocks"/> while <see cref="loadedLandblocks"/> kept
        /// the winner, and both ran Init - two live instances of one layer. One lock for all keys: it is held only for
        /// construction (dat reads, no DB) and the three table adds, so first-loads of different blocks on different group
        /// threads serialize briefly; Init runs outside it as before.
        /// </summary>
        private static readonly object landblockCreateLock = new object();

        /// <summary>
        /// Unload gate (2026-09-18): open for one key from the moment <see cref="UnloadLandblocks"/> starts tearing that
        /// instance down until the teardown is finished. It exists because the teardown now DEREGISTERS the key first: a
        /// lookup during the window therefore misses and would build a replacement while the old instance is still
        /// releasing shared state - and <c>LScape.unload_landblock</c> would then evict the REPLACEMENT's
        /// <c>AdjustCell</c> entry, which is keyed by (landblock, variation) and shared between them. A foreign lookup
        /// waits here instead, so the replacement is built after the old instance is completely gone.
        /// <para/>
        /// Only <see cref="UnloadLandblocks"/> opens one, only on the world thread, and only one at a time, so a waiter
        /// can never be another unloader and there is no gate-versus-gate wait to deadlock. The owner's own re-entrant
        /// lookups are handed the dying instance rather than waiting on themselves (see the caller).
        /// <para/>
        /// NOT covered: an instance that is registered but whose <see cref="Landblock.Init"/> has not finished. Closing
        /// that needs the tick path too - <c>landblockGroupPendingAdditions</c> is populated at registration and
        /// <c>ProcessPendingLandblockGroupAdditions</c> groups and ticks a block without ever calling GetLandblock - so
        /// it is a separate change, not something a lookup gate can do. See the research record.
        /// </summary>
        private sealed class LandblockUnloadGate
        {
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
            public readonly int OwnerThreadId = Environment.CurrentManagedThreadId;
            public readonly Landblock Instance;

            public LandblockUnloadGate(Landblock instance)
            {
                Instance = instance;
            }
        }

        private static readonly ConcurrentDictionary<VariantCacheId, LandblockUnloadGate> unloadGates = new ConcurrentDictionary<VariantCacheId, LandblockUnloadGate>();

        /// <summary>Fast-path counter so a lookup costs one volatile read while nothing is unloading.</summary>
        private static int unloadGatesActive;

        /// <summary>
        /// Longest ONE lookup waits for a teardown, in total across all of its retries (the deadline is created once per
        /// lookup and shared). An unload is an in-memory walk (SaveDB queues its writes rather than blocking), so this is
        /// generous; it is kept short because a waiter can be a group tick thread and <see cref="Tick"/> joins its
        /// Parallel.ForEach - a long wait here stalls the whole world tick. On timeout the caller proceeds exactly as it
        /// did before this gate existed.
        /// </summary>
        private static readonly TimeSpan unloadGateTimeout = TimeSpan.FromMilliseconds(250);

        private static LandblockUnloadGate OpenUnloadGate(VariantCacheId cacheKey, Landblock instance)
        {
            var gate = new LandblockUnloadGate(instance);

            // Counter BEFORE the dictionary: WaitForUnloadGate reads the counter first, so a gate must never be findable
            // in the dictionary while the counter still reads zero (a lookup would take the fast path and not wait).
            Interlocked.Increment(ref unloadGatesActive);

            if (!unloadGates.TryAdd(cacheKey, gate))
            {
                // Only the world thread opens these, one at a time, so a key cannot already be unloading.
                Interlocked.Decrement(ref unloadGatesActive);
                log.Error($"LandblockManager: 0x{cacheKey.Landblock:X4}, v:{cacheKey.Variant?.ToString() ?? "null"} is already being unloaded by another thread - proceeding without a gate.");
                return null;
            }

            return gate;
        }

        private static void CloseUnloadGate(VariantCacheId cacheKey, LandblockUnloadGate gate)
        {
            if (gate == null)
                return;

            if (unloadGates.TryRemove(new KeyValuePair<VariantCacheId, LandblockUnloadGate>(cacheKey, gate)))
                Interlocked.Decrement(ref unloadGatesActive);

            // Set AFTER the removal and unconditionally: a waiter that read the gate just before the removal is about to
            // call Wait and must not miss the signal. Done is deliberately not disposed - Wait on a set event returns at
            // once, Wait on a disposed one throws, and one idle event per unload is cheap.
            gate.Done.Set();
        }

        /// <summary>
        /// Called before the registry lookup in <see cref="GetLandblock(LandblockId, bool, int?, bool)"/>. Returns non-null
        /// ONLY when the caller is the thread performing the unload (its own re-entrant lookup, which must not wait on
        /// itself); every other caller has waited for the teardown to finish, so by the time this returns null the key is
        /// free and a fresh instance can be built. Loops because a fresh gate could in principle open for the same key
        /// between the wait and the lookup.
        /// <para/>
        /// <paramref name="deadline"/> and <paramref name="timedOut"/> belong to the calling lookup and are shared across
        /// its retries, so one lookup never waits longer than <see cref="unloadGateTimeout"/> in total. The deadline is
        /// only created when a wait is actually needed, keeping the no-unload fast path at one volatile read.
        /// </summary>
        private static LandblockUnloadGate WaitForUnloadGate(VariantCacheId cacheKey, ref DateTime? deadline, ref bool timedOut)
        {
            if (Volatile.Read(ref unloadGatesActive) == 0)
                return null;

            while (unloadGates.TryGetValue(cacheKey, out var gate))
            {
                if (gate.OwnerThreadId == Environment.CurrentManagedThreadId)
                    return gate;

                if (timedOut)
                    return null;   // this lookup already gave up once: do not wait or log again

                deadline ??= DateTime.UtcNow + unloadGateTimeout;

                var remaining = deadline.Value - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero || !gate.Done.Wait(remaining))
                {
                    timedOut = true;
                    log.Error($"LandblockManager: waited {unloadGateTimeout.TotalMilliseconds:N0}ms for 0x{cacheKey.Landblock:X4}, v:{cacheKey.Variant?.ToString() ?? "null"} to finish unloading on thread {gate.OwnerThreadId} - proceeding anyway.");
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// DestructionQueue is concurrent because it can be added to by multiple threads at once, publicly via AddToDestructionQueue()
        /// </summary>
        private static readonly ConcurrentDictionary<VariantCacheId, Landblock> destructionQueue = new ConcurrentDictionary<VariantCacheId, Landblock>();


        /// <summary>One line per landblock group: its bounds/variation/count and its members as XXXX:v (variant review
        /// 2026-09-12, item 6 - the in-game check that no group ever mixes variations). Read-locked snapshot.</summary>
        public static List<string> DumpLandblockGroups()
        {
            var lines = new List<string>();
            landblockLock.EnterReadLock();
            try
            {
                for (var i = 0; i < landblockGroups.Count; i++)
                {
                    var group = landblockGroups[i];
                    lines.Add($"[{i}] {group}");
                    // members in chunks of 20 per line - a 200-block base group would otherwise be one 2 KB chat line
                    var members = group.Select(lb => $"{lb.Id.Landblock:X4}:{lb.VariationId?.ToString() ?? "null"}").ToList();
                    for (var m = 0; m < members.Count; m += 20)
                        lines.Add("    " + string.Join(" ", members.Skip(m).Take(20)));
                }
            }
            finally
            {
                landblockLock.ExitReadLock();
            }
            return lines;
        }

        public static int LandblockGroupsCount
        {
            get
            {
               landblockLock.EnterReadLock();
               try
               {
                   return landblockGroups.Count;
               }
               finally
               {
                    landblockLock.ExitReadLock();
                }
                    
            }
        }

        /// <summary>
        /// Drops THIS instance from <see cref="landblocks"/> - a same-key replacement is left alone. Variant review
        /// 2026-09-17: this replaced AddUpdateLandblock, whose add branch moved under <see cref="landblockCreateLock"/> in
        /// GetLandblock and whose TryUpdate branch was how the loser of a first-load race evicted the winner. Registration
        /// happens in exactly one place now; an existing registration is never replaced.
        /// </summary>
        private static bool RemoveLandblock(VariantCacheId landblockKey, Landblock instance)
        {
            return landblocks.TryRemove(new KeyValuePair<VariantCacheId, Landblock>(landblockKey, instance));
        }

        private static Landblock GetLandblock(VariantCacheId landblockKey)
        {

            if (landblocks.TryGetValue(landblockKey, out Landblock landblock))
                return landblock;
            
            return null;
        }

        /// <summary>
        /// Permaloads a list of configurable landblocks if server option is set
        /// </summary>
        public static void PreloadConfigLandblocks()
        {
            if (!ConfigManager.Config.Server.LandblockPreloading)
            {
                log.Info("Preloading Landblocks Disabled...");
                log.Warn("Events may not function correctly as Preloading of Landblocks has disabled.");
                return;
            }

            log.Info("Preloading Landblocks...");

            if (ConfigManager.Config.Server.PreloadedLandblocks == null)
            {
                log.Info("No configuration found for PreloadedLandblocks, please refer to Config.js.example");
                log.Warn("Initializing PreloadedLandblocks with single default for Hebian-To (Global Events)");
                log.Warn("Add a PreloadedLandblocks section to your Config.js file and adjust to meet your needs");
                ConfigManager.Config.Server.PreloadedLandblocks = new List<PreloadedLandblocks> { new PreloadedLandblocks { Id = "E74EFFFF", Description = "Hebian-To (Global Events)", Permaload = true, IncludeAdjacents = true, Enabled = true } };
            }

            log.InfoFormat("Found {0} landblock entries in PreloadedLandblocks configuration, {1} are set to preload.", ConfigManager.Config.Server.PreloadedLandblocks.Count, ConfigManager.Config.Server.PreloadedLandblocks.Count(x => x.Enabled == true));

            foreach (var preloadLandblock in ConfigManager.Config.Server.PreloadedLandblocks)
            {
                if (!preloadLandblock.Enabled)
                {
                    log.DebugFormat("Landblock {0:X4} specified but not enabled in config, skipping", preloadLandblock.Id);
                    continue;
                }

                if (uint.TryParse(preloadLandblock.Id, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out uint rawLandblock))
                {
                    ushort landblock = (ushort)(rawLandblock > 0xFFFF ? rawLandblock >> 16 : rawLandblock);

                    if (landblock == 0)
                    {
                        switch (preloadLandblock.Description)
                        {
                            case "Apartment Landblocks":
                                log.InfoFormat("Preloading landblock group: {0}, IncludeAdjacents = {1}, Permaload = {2}", preloadLandblock.Description, preloadLandblock.IncludeAdjacents, preloadLandblock.Permaload);
                                foreach (var apt in LandblockCollections.ApartmentBlocks.Keys)
                                    PreloadLandblock(apt, preloadLandblock);
                                break;
                        }
                    }
                    else
                        PreloadLandblock(landblock, preloadLandblock);
                }
            }
        }

        private static void PreloadLandblock(ushort landblock, PreloadedLandblocks preloadLandblock)
        {
            var landblockID = new LandblockId((uint)landblock << 16 | 0xFFFF);
            GetLandblock(landblockID, preloadLandblock.IncludeAdjacents, null, preloadLandblock.Permaload);
            log.DebugFormat("Landblock {0:X4}, ({1}) preloaded. IncludeAdjacents = {2}, Permaload = {3}", landblockID.Landblock, preloadLandblock.Description, preloadLandblock.IncludeAdjacents, preloadLandblock.Permaload);
        }

        /// <summary>
        /// Loads landblocks when they are needed
        /// TODO: Come back and make this Variation aware
        /// </summary>
        private static void ProcessPendingLandblockGroupAdditions()
        {
            if (landblockGroupPendingAdditions.IsEmpty)
                return;

            // Snapshot keys and TryRemove-first: index-based ElementAt over a ConcurrentDictionary while
            // physics worker threads TryAdd concurrently can skip an entry (delayed a tick) or process one
            // TWICE (same landblock in two groups = double-ticked, and its group-removal at unload leaves
            // a dead entry ticking forever). TryRemove guarantees exactly-once.
            foreach (var pendingKey in landblockGroupPendingAdditions.Keys.ToList())
            {
                if (!landblockGroupPendingAdditions.TryRemove(pendingKey, out var landlockToAdd))
                    continue;
                // Variant review 2026-09-12 (item 6): variant OUTDOOR blocks group by proximity exactly like base blocks,
                // one variation per group. They used to go solo like dungeons while SetAdjacents still wired same-variation
                // neighbours together, so two adjacent v11 blocks ticked on different threads while writing each other's
                // dormancy state, spawning across the border into the other's group (the "PLEASE REPORT THIS" branch),
                // trading melee/projectile damage cross-thread, and a v11 caster never cast at a target one block over
                // (Monster_Magic's cross-group guard threw the cast away). Groups are the thread boundary; adjacency is
                // per (id, exact variation); so the group key is the exact variation too. Dungeons stay solo.
                if (landlockToAdd.IsDungeon)
                {
                    // Each dungeon exists in its own group
                    var landblockGroup = new LandblockGroup(landlockToAdd, landlockToAdd.VariationId);
                    landblockGroups.Add(landblockGroup);
                }
                else
                {
                    // Find out how many groups this landblock is eligible for
                    var landblockGroupsIndexMatchesByDistance = new List<int>();

                    for (int j = 0; j < landblockGroups.Count; j++)
                    {
                        // never a dungeon group, never another variation's group (this second test also ends the old
                        // silent bug where a BASE block within 5 of a v11 solo group was merged into it)
                        if (landblockGroups[j].IsDungeon || landblockGroups[j].VariationId != landlockToAdd.VariationId)
                            continue;

                        var distance = landblockGroups[j].BoundaryDistance(landlockToAdd);

                        if (distance < LandblockGroup.LandblockGroupMinSpacing)
                            landblockGroupsIndexMatchesByDistance.Add(j);
                    }

                    var groupDiag = ServerConfig.landblock_group_diag_verbose.Value;
                    var lbTag = $"{landlockToAdd.Id.Landblock:X4} v={landlockToAdd.VariationId?.ToString() ?? "null"}";

                    if (landblockGroupsIndexMatchesByDistance.Count > 0)
                    {
                        // Add the landblock to the first eligible group
                        landblockGroups[landblockGroupsIndexMatchesByDistance[0]].Add(landlockToAdd, landlockToAdd.VariationId);
                        if (groupDiag)
                            log.Warn($"[LANDBLOCK GROUP] add {lbTag} -> group[{landblockGroupsIndexMatchesByDistance[0]}] {landblockGroups[landblockGroupsIndexMatchesByDistance[0]]}");

                        if (landblockGroupsIndexMatchesByDistance.Count > 1)
                        {
                            // Merge the additional eligible groups into the first one
                            for (int j = landblockGroupsIndexMatchesByDistance.Count - 1; j > 0; j--)
                            {
                                if (groupDiag)
                                    log.Warn($"[LANDBLOCK GROUP] merge {lbTag} bridges group[{landblockGroupsIndexMatchesByDistance[j]}] {landblockGroups[landblockGroupsIndexMatchesByDistance[j]]} into group[{landblockGroupsIndexMatchesByDistance[0]}]");

                                // Copy the j down into 0. A refused Add would leave that block in NO group (never ticked, still
                                // walkable) - unreachable while candidates are filtered by exact variation, but never silent
                                // (review 2026-09-13).
                                foreach (var landblock in landblockGroups[landblockGroupsIndexMatchesByDistance[j]])
                                    if (!landblockGroups[landblockGroupsIndexMatchesByDistance[0]].Add(landblock, landblock.VariationId))
                                        log.Error($"[LANDBLOCK GROUP] merge REFUSED {landblock.Id.Landblock:X4} v={landblock.VariationId?.ToString() ?? "null"} into group {landblockGroups[landblockGroupsIndexMatchesByDistance[0]]} - block is now in no group");

                                landblockGroups.RemoveAt(landblockGroupsIndexMatchesByDistance[j]);
                            }
                        }
                    }
                    else
                    {
                        // No close groups were found
                        var landblockGroup = new LandblockGroup(landlockToAdd, landlockToAdd.VariationId);
                        landblockGroups.Add(landblockGroup);
                        if (groupDiag)
                            log.Warn($"[LANDBLOCK GROUP] new {lbTag} -> group[{landblockGroups.Count - 1}] {landblockGroup} (no same-variation group within {LandblockGroup.LandblockGroupMinSpacing})");
                    }
                }
            }

            // Debugging todo: comment this out after enough testing
            var count = 0;
            foreach (var group in landblockGroups)
            {
                count += group.Count;
                // item 6 invariant: one variation per group (a refused Add would also show up as a count mismatch below)
                foreach (var member in group)
                    if (member.VariationId != group.VariationId)
                        log.Error($"[LANDBLOCK GROUP] mixed variations: {member.Id.Landblock:X4} v={member.VariationId?.ToString() ?? "null"} sits in group {group}");
            }
            if (count != loadedLandblocks.Count)
                log.Error($"[LANDBLOCK GROUP] ProcessPendingAdditions count ({count}) != loadedLandblocks.Count ({loadedLandblocks.Count})");
            
        }

        private static DateTime _groupSummaryLast = DateTime.MinValue;

        /// <summary>Variant review 2026-09-12 (item 6) test diag, every 60 s while landblock_group_diag_verbose is on:
        /// groups per variation, the largest group, and any two same-variation non-dungeon groups within
        /// LandblockGroupMinSpacing of each other - those should have merged, so a hit is a grouping defect.</summary>
        private static void LogGroupSummaryIfDue()
        {
            if (!ServerConfig.landblock_group_diag_verbose.Value) return;
            if ((DateTime.UtcNow - _groupSummaryLast).TotalSeconds < 60) return;
            _groupSummaryLast = DateTime.UtcNow;

            landblockLock.EnterReadLock();
            try
            {
                var perVariation = new SortedDictionary<string, (int groups, int blocks, int largest)>();
                foreach (var group in landblockGroups)
                {
                    var key = group.VariationId?.ToString() ?? "null";
                    perVariation.TryGetValue(key, out var acc);
                    perVariation[key] = (acc.groups + 1, acc.blocks + group.Count, Math.Max(acc.largest, group.Count));
                }
                var parts = perVariation.Select(kv => $"v={kv.Key}: {kv.Value.groups} group(s), {kv.Value.blocks} block(s), largest {kv.Value.largest}");
                log.Warn($"[LANDBLOCK GROUP] summary: {landblockGroups.Count} groups, {loadedLandblocks.Count} loaded | {string.Join(" | ", parts)}");

                // The thread-safety invariant is "ADJACENT blocks share a group" (review 2026-09-13: a rectangle-distance
                // test asserted more than the system promises - a split can legitimately leave an island inside another
                // group's rectangle). Two same-variation outdoor blocks at Chebyshev distance 1 in different groups is the
                // real defect, so that is what this scans for.
                for (var i = 0; i < landblockGroups.Count; i++)
                {
                    var a = landblockGroups[i];
                    if (a.IsDungeon) continue;
                    for (var j = i + 1; j < landblockGroups.Count; j++)
                    {
                        var b = landblockGroups[j];
                        if (b.IsDungeon || a.VariationId != b.VariationId) continue;
                        var hit = false;
                        foreach (var la in a)
                        {
                            foreach (var lb in b)
                            {
                                if (Math.Abs(la.Id.LandblockX - lb.Id.LandblockX) <= 1 && Math.Abs(la.Id.LandblockY - lb.Id.LandblockY) <= 1)
                                {
                                    log.Warn($"[LANDBLOCK GROUP] adjacent blocks in different groups: {la.Id.Landblock:X4} (group[{i}] {a}) and {lb.Id.Landblock:X4} (group[{j}] {b}) - same variation, touching, different threads");
                                    hit = true;
                                    break;
                                }
                            }
                            if (hit) break;
                        }
                    }
                }
            }
            finally
            {
                landblockLock.ExitReadLock();
            }
        }

        public static void Tick(double portalYearTicks)
        {
            LogGroupSummaryIfDue();

            // update positions through physics engine
            ServerPerformanceMonitor.RestartEvent(ServerPerformanceMonitor.MonitorType.LandblockManager_TickPhysics);
            TickPhysics(portalYearTicks);
            ServerPerformanceMonitor.RegisterEventEnd(ServerPerformanceMonitor.MonitorType.LandblockManager_TickPhysics);

            // Tick all of our Landblocks and WorldObjects (Work that can be multi-threaded)
            ServerPerformanceMonitor.RestartEvent(ServerPerformanceMonitor.MonitorType.LandblockManager_TickMultiThreadedWork);
            TickMultiThreadedWork();
            ServerPerformanceMonitor.RegisterEventEnd(ServerPerformanceMonitor.MonitorType.LandblockManager_TickMultiThreadedWork);

            // Tick all of our Landblocks and WorldObjects (Work that must be single threaded)
            ServerPerformanceMonitor.RestartEvent(ServerPerformanceMonitor.MonitorType.LandblockManager_TickSingleThreadedWork);
            TickSingleThreadedWork();
            ServerPerformanceMonitor.RegisterEventEnd(ServerPerformanceMonitor.MonitorType.LandblockManager_TickSingleThreadedWork);

            // clean up inactive landblocks
            UnloadLandblocks();
        }

        /// <summary>
        /// Used to debug cross-landblock group (and potentially cross-thread) operations
        /// </summary>
        public static bool CurrentlyTickingLandblockGroupsMultiThreaded { get; private set; }

        /// <summary>
        /// Used to debug cross-landblock group (and potentially cross-thread) operations
        /// </summary>
        public static readonly ThreadLocal<LandblockGroup> CurrentMultiThreadedTickingLandblockGroup = new ThreadLocal<LandblockGroup>();

        /// <summary>
        /// Thread audit: true when work that writes <paramref name="wo"/> may run on the calling thread - landblock groups
        /// are not ticking in parallel (world thread, or the single-threaded physics phase), or this thread is ticking the
        /// group that owns wo's landblock. Compares against the EXECUTING group, never against some other object's group,
        /// because a caller can run on a thread that owns neither. Use <see cref="RunOnThreadFor"/> to act on the answer.
        /// </summary>
        public static bool IsOnThreadFor(WorldObject wo)
        {
            if (!CurrentlyTickingLandblockGroupsMultiThreaded)
                return true;

            var current = CurrentMultiThreadedTickingLandblockGroup.Value;
            return current != null && ReferenceEquals(wo?.CurrentLandblock?.CurrentLandblockGroup, current);
        }

        /// <summary>
        /// Thread audit: runs <paramref name="work"/> now when <see cref="IsOnThreadFor"/> says the calling thread owns
        /// <paramref name="wo"/>, otherwise on the WORLD action queue. The world queue runs on the world loop between
        /// landblock ticks (WorldManager.UpdateWorld: ActionQueue.RunActions, then LandblockManager.Tick), never beside
        /// them, so when the work runs no group thread can be touching or removing wo. Never wo's own queue: a Player's
        /// queue only drains while a landblock ticks the player, and a player removed by logout between a landblock check
        /// and the enqueue would never run it.
        /// </summary>
        public static void RunOnThreadFor(WorldObject wo, ACE.Server.Entity.Actions.ActionType type, Action work)
        {
            if (IsOnThreadFor(wo))
            {
                work();
                return;
            }

            WorldManager.EnqueueAction(new ACE.Server.Entity.Actions.ActionEventDelegate(type, work));
        }

        /// <summary>
        /// Processes physics objects in all active landblocks for updating
        /// </summary>
        private static void TickPhysics(double portalYearTicks)
        {
            ProcessPendingLandblockGroupAdditions();

            var movedObjects = new ConcurrentBag<WorldObject>();

            if (ConfigManager.Config.Server.Threading.MultiThreadedLandblockGroupPhysicsTicking)
            {
                CurrentlyTickingLandblockGroupsMultiThreaded = true;

                Parallel.ForEach(landblockGroups, ConfigManager.Config.Server.Threading.LandblockManagerParallelOptions, landblockGroup =>
                {
                    CurrentMultiThreadedTickingLandblockGroup.Value = landblockGroup;

                    foreach (var landblock in landblockGroup)
                        landblock.TickPhysics(portalYearTicks, movedObjects);

                    CurrentMultiThreadedTickingLandblockGroup.Value = null;
                });

                CurrentlyTickingLandblockGroupsMultiThreaded = false;
            }
            else
            {
                foreach (var landblockGroup in landblockGroups)
                {
                    foreach (var landblock in landblockGroup)
                        landblock.TickPhysics(portalYearTicks, movedObjects);
                }
            }

            // iterate through objects that have changed landblocks
            foreach (var movedObject in movedObjects)
            {
                // NOTE: The object's Location can now be null, if a player logs out, or an item is picked up
                if (movedObject.Location == null)
                    continue;

                // assume adjacency move here?
                RelocateObjectForPhysics(movedObject, true);
            }
        }

        /// <summary>
        /// Landblock ids holding at least one online player, snapshotted once at the start of each
        /// multi-threaded tick batch. Per-landblock dormancy checks (Landblock.HasPhysicalPlayerOnOrAdjacent)
        /// read this shared set instead of each rescanning every online player, turning what was an
        /// O(landblocks x players) tick-path cost into a single O(players) build plus O(9) lookups.
        /// Rebuilt before the parallel tick and only read during it, so no synchronization is needed.
        /// </summary>
        internal static HashSet<(ushort Landblock, int? Variation)> OccupiedLandblocks { get; private set; } = new();

        private static void RefreshOccupiedLandblocks()
        {
            var set = new HashSet<(ushort Landblock, int? Variation)>();
            foreach (var player in PlayerManager.GetAllOnline())
            {
                var loc = player.Location;
                if (loc != null)
                    set.Add(((ushort)loc.LandblockId.Landblock, VariationManager.NormalizeBase(loc.Variation)));
            }
            OccupiedLandblocks = set;
        }

        private static void TickMultiThreadedWork()
        {
            ProcessPendingLandblockGroupAdditions();

            // Snapshot online-player landblocks once for this batch (read by Landblock dormancy checks below).
            RefreshOccupiedLandblocks();

            if (ConfigManager.Config.Server.Threading.MultiThreadedLandblockGroupTicking)
            {
                CurrentlyTickingLandblockGroupsMultiThreaded = true;

                Parallel.ForEach(landblockGroups, ConfigManager.Config.Server.Threading.LandblockManagerParallelOptions, landblockGroup =>
                {
                    CurrentMultiThreadedTickingLandblockGroup.Value = landblockGroup;

                    foreach (var landblock in landblockGroup)
                        landblock.TickMultiThreadedWork(Time.GetUnixTime());

                    CurrentMultiThreadedTickingLandblockGroup.Value = null;
                });

                CurrentlyTickingLandblockGroupsMultiThreaded = false;
            }
            else
            {
                foreach (var landblockGroup in landblockGroups)
                {
                    foreach (var landblock in landblockGroup)
                        landblock.TickMultiThreadedWork(Time.GetUnixTime());
                }
            }
        }

        private static void TickSingleThreadedWork()
        {
            ProcessPendingLandblockGroupAdditions();

            foreach (var landblockGroup in landblockGroups)
            {
                foreach (var landblock in landblockGroup)
                    landblock.TickSingleThreadedWork(Time.GetUnixTime());
            }
        }

        /// <summary>
        /// Adds a WorldObject to the landblock defined by the object's location
        /// </summary>
        /// <param name="loadAdjacents">If TRUE, ensures all of the adjacent landblocks for this WorldObject are loaded</param>
        public static bool AddObject(WorldObject worldObject, bool loadAdjacents = false)
        {
            var block = GetLandblock(worldObject.Location.LandblockId, loadAdjacents, worldObject.Location.Variation, false);

            return block.AddWorldObject(worldObject, worldObject.Location.Variation);
        }

        /// <summary>
        /// Relocates an object to the appropriate landblock -- Should only be called from physics/worldmanager -- not player!
        /// </summary>
        public static void RelocateObjectForPhysics(WorldObject worldObject, bool adjacencyMove)
        {
            var oldBlock = worldObject.CurrentLandblock;
            var newBlock = GetLandblock(worldObject.Location.LandblockId, true, worldObject.Location.Variation, false );

            if (newBlock.IsDormant && worldObject is SpellProjectile)
            {
                worldObject.PhysicsObj.set_active(false);
                worldObject.Destroy();
                return;
            }

            var variation = worldObject.Location.Variation;

            // During multi-threaded landblock ticking, Remove/Add must run on each landblock's group thread.
            // Physics (movedObjects) runs after Parallel.ForEach with CurrentlyTickingLandblockGroupsMultiThreaded = false.
            // Monster AI can call RelocateObjectForPhysics from TickMultiThreadedWork on one group while the
            // object's CurrentLandblock belongs to another — defer remove onto oldBlock's queue when needed.
            var mt = CurrentlyTickingLandblockGroupsMultiThreaded;
            var curGroup = CurrentMultiThreadedTickingLandblockGroup.Value;
            var oldGroup = oldBlock?.CurrentLandblockGroup;
            var newGroup = newBlock?.CurrentLandblockGroup;

            var removeNeedsDefer = mt && oldBlock != null && oldGroup != null && !ReferenceEquals(oldGroup, curGroup);
            var addNeedsDefer = mt && newGroup != null && !ReferenceEquals(newGroup, curGroup);

            if (removeNeedsDefer)
            {
                oldBlock.EnqueueAction(new ActionEventDelegate(ActionType.Landblock_Relocate_RemoveForPhysics, () =>
                {
                    oldBlock.RemoveWorldObjectForPhysics(worldObject.Guid, adjacencyMove);

                    // TLS now matches oldGroup; defer add if destination is owned by a different group.
                    var addDeferredHere = CurrentlyTickingLandblockGroupsMultiThreaded
                        && newGroup != null
                        && !ReferenceEquals(newGroup, CurrentMultiThreadedTickingLandblockGroup.Value);

                    worldObject.CurrentLandblock = newBlock;

                    if (addDeferredHere)
                        newBlock.EnqueueAddWorldObjectForPhysics(worldObject, variation);
                    else
                        newBlock.AddWorldObjectForPhysics(worldObject, variation);
                }));
                return;
            }

            oldBlock?.RemoveWorldObjectForPhysics(worldObject.Guid, adjacencyMove);

            if (addNeedsDefer)
            {
                worldObject.CurrentLandblock = newBlock;
                newBlock.EnqueueAddWorldObjectForPhysics(worldObject, variation);
            }
            else
                newBlock.AddWorldObjectForPhysics(worldObject, variation);
        }

        public static bool IsLoaded(LandblockId landblockId, int? variationId = null)
        {
            return GetLandblock(new VariantCacheId() {Landblock = landblockId.Landblock, Variant = VariationManager.NormalizeBase(variationId) }) != null;
        }

        /// <summary>No-create lookup of the LOADED landblock instance for (landblock, variation) -
        /// null when not loaded. Lets callers verify a held Landblock reference is still the live
        /// one (see Player.ValidateCurrentLandblockTick's stale-instance void heal, 2026-08-10).</summary>
        public static Landblock GetLoadedLandblock(LandblockId landblockId, int? variationId)
        {
            return GetLandblock(new VariantCacheId() { Landblock = landblockId.Landblock, Variant = VariationManager.NormalizeBase(variationId) });
        }

        /// <summary>
        /// Enqueues <see cref="Landblock.RefreshPrestigeBoundaryMarkers"/> on each matching loaded landblock (async per landblock queue); does not wait for completion.
        /// </summary>
        /// <returns>Number of landblocks that had a refresh enqueued.</returns>
        public static int EnqueueRefreshLoadedPrestigeBoundaryMarkers(int? variation = null)
        {
            var queued = 0;
            var loaded = loadedLandblocks.Values.ToList();

            foreach (var landblock in loaded)
            {
                if (landblock == null)
                    continue;

                if (variation.HasValue && landblock.VariationId != variation.Value)
                    continue;

                landblock.RefreshPrestigeBoundaryMarkers();
                queued++;
            }

            return queued;
        }

        /// <summary>
        /// Enqueues <see cref="Landblock.RefreshZoneBoundaryMarkers"/> on each matching loaded landblock (async per landblock queue); does not wait for completion.
        /// Called by ZoneControlManager whenever a mutation changes a variation's bounded union.
        /// </summary>
        /// <returns>Number of landblocks that had a refresh enqueued.</returns>
        public static int EnqueueRefreshLoadedZoneBoundaryMarkers(int? variation = null)
        {
            var queued = 0;
            var loaded = loadedLandblocks.Values.ToList();

            foreach (var landblock in loaded)
            {
                if (landblock == null)
                    continue;

                if (variation.HasValue && landblock.VariationId != variation.Value)
                    continue;

                landblock.RefreshZoneBoundaryMarkers();
                queued++;
            }

            return queued;
        }

        /// <summary>
        /// Returns a reference to a landblock, loading the landblock if not already active
        /// TODO: Make this Variation Aware
        /// </summary>
        public static Landblock GetLandblock(LandblockId landblockId, bool loadAdjacents, int? variation, bool permaload = false)
        {
            // Variation 0 is always base (owner ruling 2026-09-14): the cache, the group key and the adjacency wiring are
            // EXACT on int?, while visibility treats 0 and null as one bucket. A raw 0 arriving here used to build a
            // separate "(id, 0)" instance - its own group, its own empty cells - whose objects could still see and hit base
            // objects ticked by another thread. Normalize once at this choke point; the log finds the producers.
            if (variation.HasValue && variation.Value == 0)
            {
                if (ACE.Server.Diagnostics.LogRateLimiter.ShouldEmit("landblock_variation_zero", TimeSpan.FromMinutes(5), out var suppressedZero))
                    log.Warn($"LandblockManager: explicit variation 0 requested for 0x{landblockId.Landblock:X4} - treated as base (null)." +
                             (suppressedZero > 0 ? $" {suppressedZero} similar suppressed since the last report." : string.Empty) +
                             $" Stack: {Environment.StackTrace}");
                variation = null;
            }

            Landblock landblock;

            bool setAdjacents = false;
            var cacheKey = new VariantCacheId() { Landblock = landblockId.Landblock, Variant = variation };

            var created = false;

            // One wait budget for this whole lookup, however many times the loop below comes back round.
            DateTime? unloadDeadline = null;
            var unloadWaitTimedOut = false;

            while (true)
            {
                // If this key is being torn down, wait for that to finish so the instance built below replaces it cleanly
                // rather than racing its release of shared state (see LandblockUnloadGate).
                var unloadGate = WaitForUnloadGate(cacheKey, ref unloadDeadline, ref unloadWaitTimedOut);
                landblock = GetLandblock(cacheKey);

                if (landblock == null && unloadGate != null)
                {
                    // The unloading thread looked up the block it is tearing down. Nothing does this today (Unload's re-home
                    // targets the SIBLING layer's key), and creating a second instance mid-teardown would double the block's
                    // spawns, so hand back the dying instance - what this lookup returned before the deregistration moved first.
                    if (ACE.Server.Diagnostics.LogRateLimiter.ShouldEmit($"lb_selflookup_unload:{cacheKey.Landblock:X4}:{cacheKey.Variant?.ToString() ?? "null"}", TimeSpan.FromMinutes(5), out var suppressedSelf))
                        log.Error($"LandblockManager: 0x{cacheKey.Landblock:X4}, v:{variation?.ToString() ?? "null"} was looked up from inside its own unload - returning the unloading instance." +
                                  (suppressedSelf > 0 ? $" {suppressedSelf} similar suppressed since the last report." : string.Empty) +
                                  $" Stack: {Environment.StackTrace}");
                    return unloadGate.Instance;
                }

                if (landblock != null)
                    break;

                var unloadInProgress = false;

                // See landblockCreateLock: the miss is re-checked under the lock so exactly one thread constructs and
                // registers an instance for this key; every other thread that missed gets that instance back. The unloader
                // deregisters under this same lock (with its gate already open), so a miss WITH a gate present here means
                // a teardown is in flight - never build over it; release the lock, wait for the gate, and come back.
                // Once this lookup's wait budget is spent (already logged by WaitForUnloadGate) it builds anyway, which
                // is what every lookup did before the gate existed.
                lock (landblockCreateLock)
                {
                    landblock = GetLandblock(cacheKey);
                    if (landblock == null)
                    {
                        if (!unloadWaitTimedOut && unloadGates.ContainsKey(cacheKey))
                            unloadInProgress = true;
                        else
                        {
                            // load up this landblock
                            landblock = new Landblock(landblockId, variation);

                            // The key was just seen absent from `landblocks`, and the only remover (UnloadLandblocks) clears
                            // `loadedLandblocks` before `landblocks`, so all three adds succeed. If one ever does not, the tables
                            // disagree about this key: log it and make them agree on this instance rather than tick an instance
                            // that some lookups cannot reach.
                            var addedActive = landblocks.TryAdd(cacheKey, landblock);
                            var addedLoaded = loadedLandblocks.TryAdd(cacheKey, landblock);
                            var addedPending = landblockGroupPendingAdditions.TryAdd(cacheKey, landblock);
                            if (!addedActive || !addedLoaded || !addedPending)
                            {
                                log.Error($"LandblockManager: registration of {landblock.Id.Raw:X8}, v:{variation?.ToString() ?? "null"} found a stale entry under the create lock " +
                                          $"(landblocks={addedActive}, loadedLandblocks={addedLoaded}, pendingGroupAdditions={addedPending}) - overwriting so the three tables agree.");
                                landblocks[cacheKey] = landblock;
                                loadedLandblocks[cacheKey] = landblock;
                                landblockGroupPendingAdditions[cacheKey] = landblock;
                            }

                            created = true;
                        }
                    }
                }

                if (unloadInProgress)
                    continue;   // back to WaitForUnloadGate, which blocks until the teardown closes its gate or the budget runs out

                break;
            }

            if (created)
            {
                // Registration precedes Init, as it always has: PostInit places the dat's statics through
                // LScape.get_landcell, which comes back here for this very key, so the instance must be findable
                // while it initializes. A concurrent lookup can still see it mid-PostInit - unchanged by this branch,
                // and not closable from here (the tick path reaches a block without a lookup at all).
                landblock.Init(variation);

                setAdjacents = true;
            }

            if (permaload)
                landblock.Permaload = true;

            // load adjacents, if applicable
            if (loadAdjacents)
            {
                var adjacents = GetAdjacentIDs(landblock);
                foreach (var adjacent in adjacents)
                    GetLandblock(adjacent, false, variation, permaload);

                setAdjacents = true;
            }

            // cache adjacencies
            if (setAdjacents)
                SetAdjacents(landblock, true, true);
            

            return landblock;
        }

        /// <summary>
        /// Returns the list of all loaded landblocks
        /// </summary>
        public static ConcurrentDictionary<VariantCacheId, Landblock> GetLoadedLandblocks()
        {
            return loadedLandblocks;
        }

        /// <summary>
        /// Returns the active, non-null adjacents for a landblock
        /// </summary>
        private static List<Landblock> GetAdjacents(Landblock landblock, int? variationId = null)
        {
            var adjacentIDs = GetAdjacentIDs(landblock);

            var adjacents = new List<Landblock>();

            foreach (var adjacentID in adjacentIDs)
            {
                var adjacent = GetLandblock(new VariantCacheId() { Landblock = adjacentID.Landblock, Variant = variationId });
                // Thread audit 2026-09-13 (C8): dungeon ids occupy grid slots (ocean coordinates); a loaded dungeon is a solo
                // group and must never receive SetActive/dormancy writes from an outdoor neighbour's thread.
                if (adjacent != null && !adjacent.IsDungeon)
                    adjacents.Add(adjacent);
            }

            return adjacents;
        }

        /// <summary>
        /// Returns the list of adjacent landblock IDs for a landblock
        /// </summary>
        private static List<LandblockId> GetAdjacentIDs(Landblock landblock)
        {
            var adjacents = new List<LandblockId>();

            if (landblock.IsDungeon)
                return adjacents;   // dungeons have no adjacents

            var north = GetAdjacentID(landblock.Id, Adjacency.North);
            var south = GetAdjacentID(landblock.Id, Adjacency.South);
            var west = GetAdjacentID(landblock.Id, Adjacency.West);
            var east = GetAdjacentID(landblock.Id, Adjacency.East);
            var northwest = GetAdjacentID(landblock.Id, Adjacency.NorthWest);
            var northeast = GetAdjacentID(landblock.Id, Adjacency.NorthEast);
            var southwest = GetAdjacentID(landblock.Id, Adjacency.SouthWest);
            var southeast = GetAdjacentID(landblock.Id, Adjacency.SouthEast);

            if (north != null)
                adjacents.Add(north.Value);
            if (south != null)
                adjacents.Add(south.Value);
            if (west != null)
                adjacents.Add(west.Value);
            if (east != null)
                adjacents.Add(east.Value);
            if (northwest != null)
                adjacents.Add(northwest.Value);
            if (northeast != null)
                adjacents.Add(northeast.Value);
            if (southwest != null)
                adjacents.Add(southwest.Value);
            if (southeast != null)
                adjacents.Add(southeast.Value);

            return adjacents;
        }

        /// <summary>
        /// Returns an adjacent landblock ID for a landblock
        /// </summary>
        private static LandblockId? GetAdjacentID(LandblockId landblock, Adjacency adjacency)
        {
            int lbx = landblock.LandblockX;
            int lby = landblock.LandblockY;

            switch (adjacency)
            {
                case Adjacency.North:
                    lby += 1;
                    break;
                case Adjacency.South:
                    lby -= 1;
                    break;
                case Adjacency.West:
                    lbx -= 1;
                    break;
                case Adjacency.East:
                    lbx += 1;
                    break;
                case Adjacency.NorthWest:
                    lby += 1;
                    lbx -= 1;
                    break;
                case Adjacency.NorthEast:
                    lby += 1;
                    lbx += 1;
                    break;
                case Adjacency.SouthWest:
                    lby -= 1;
                    lbx -= 1;
                    break;
                case Adjacency.SouthEast:
                    lby -= 1;
                    lbx += 1;
                    break;
            }

            if (lbx < 0 || lbx > 254 || lby < 0 || lby > 254)
                return null;

            return new LandblockId((byte)lbx, (byte)lby);
        }

        /// <summary>
        /// Rebuilds the adjacency cache for a landblock, and optionally rebuilds the adjacency caches
        /// for the adjacent landblocks if traverse is true
        /// </summary>
        private static void SetAdjacents(Landblock landblock, bool traverse = true, bool pSync = false)
        {
            landblock.Adjacents = GetAdjacents(landblock, landblock.VariationId);

            if (pSync)
                landblock.PhysicsLandblock.SetAdjacents(landblock.Adjacents);

            if (traverse)
            {
                foreach (var adjacent in landblock.Adjacents)
                    SetAdjacents(adjacent, false, pSync);
            }
        }

        /// <summary>
        /// Queues a landblock for thread-safe unloading
        /// </summary>
        public static void AddToDestructionQueue(Landblock landblock, int? VariationId)
        {
            var cacheKey = new VariantCacheId() { Landblock = landblock.Id.Landblock, Variant = VariationId };
            destructionQueue.TryAdd(cacheKey, landblock);
        }

        private static readonly System.Diagnostics.Stopwatch swTrySplitEach = new System.Diagnostics.Stopwatch();

        /// <summary>
        /// Processes the destruction queue in a thread-safe manner
        /// </summary>
        private static void UnloadLandblocks()
        {
            while (!destructionQueue.IsEmpty)
            {
                var cacheKey = destructionQueue.Keys.First();
                if (destructionQueue.TryGetValue(cacheKey, out Landblock landblock))
                {
                    //Console.WriteLine($"UnloadLandblock: {landblock.Id}, v: {cacheKey.Variant}, d-queue: {destructionQueue.Count}");

                    // Lifecycle (2026-09-18): the key comes OUT of the registry and its group before Unload() tears the instance
                    // down, and only the queued instance is removed (never a same-key replacement). A lookup during the
                    // teardown waits on the gate and then builds a fresh instance; it no longer receives an emptied one.
                    // Everything from opening the gate to closing it sits inside one try/finally: an escape in between would
                    // leave the gate open forever, and every later lookup of this key would then block for the full timeout
                    // for the rest of the session.
                    bool unloadFailed = false;
                    LandblockUnloadGate unloadGate = null;

                    try
                    {
                        // Plain key removal on purpose: `landblock` IS the queued value (read above) and the queue is TryAdd-only,
                        // so an instance-checked remove could only ever fail by leaving the key queued - and the enclosing
                        // `while (!IsEmpty) Keys.First()` would then re-pick it forever on the world thread.
                        destructionQueue.TryRemove(cacheKey, out _);

                        // Gate open and deregistration are atomic with respect to GetLandblock's check-and-register, which
                        // runs under this same lock: a creator that misses the registry while a gate is present knows a
                        // teardown is in flight and waits; one that misses with no gate present knows the key is truly free.
                        // Unload() itself runs OUTSIDE the create lock - it may create the sibling layer while re-homing.
                        lock (landblockCreateLock)
                        {
                            unloadGate = OpenUnloadGate(cacheKey, landblock);

                            landblockLock.EnterWriteLock();
                            try
                            {
                                // remove from list of managed landblocks - this instance only
                                if (loadedLandblocks.TryRemove(new KeyValuePair<VariantCacheId, Landblock>(cacheKey, landblock)))
                                {
                                    RemoveLandblock(cacheKey, landblock);

                                    // remove from landblock group
                                    for (int i = landblockGroups.Count - 1; i >= 0; i--)
                                    {
                                        if (landblockGroups[i].Remove(landblock, landblock.VariationId))
                                        {
                                            if (landblockGroups[i].Count == 0)
                                                landblockGroups.RemoveAt(i);
                                            else if (ConfigManager.Config.Server.Threading
                                                         .MultiThreadedLandblockGroupPhysicsTicking ||
                                                     ConfigManager.Config.Server.Threading
                                                         .MultiThreadedLandblockGroupTicking) // Only try to split if multi-threading is enabled
                                            {
                                                swTrySplitEach.Restart();
                                                var splits = landblockGroups[i].TryThrottledSplit();
                                                swTrySplitEach.Stop();

                                                if (swTrySplitEach.Elapsed.TotalMilliseconds > 3)
                                                    log.Warn(
                                                        $"[LANDBLOCK GROUP] TrySplit for {landblockGroups[i]} took: {swTrySplitEach.Elapsed.TotalMilliseconds:N2} ms");
                                                else if (swTrySplitEach.Elapsed.TotalMilliseconds > 1)
                                                    log.Debug(
                                                        $"[LANDBLOCK GROUP] TrySplit for {landblockGroups[i]} took: {swTrySplitEach.Elapsed.TotalMilliseconds:N2} ms");

                                                if (splits != null)
                                                {
                                                    if (splits.Count > 0)
                                                    {
                                                        log.Debug(
                                                            $"[LANDBLOCK GROUP] TrySplit resulted in {splits.Count} split(s) and took: {swTrySplitEach.Elapsed.TotalMilliseconds:N2} ms");
                                                        if (ServerConfig.landblock_group_diag_verbose.Value) log.Warn($"[LANDBLOCK GROUP] split for old: {landblockGroups[i]}"); else log.Debug($"[LANDBLOCK GROUP] split for old: {landblockGroups[i]}");
                                                    }

                                                    foreach (var split in splits)
                                                    {
                                                        landblockGroups.Add(split);
                                                        if (ServerConfig.landblock_group_diag_verbose.Value) log.Warn($"[LANDBLOCK GROUP] split and new: {split}"); else log.Debug($"[LANDBLOCK GROUP] split and new: {split}");
                                                    }
                                                }
                                            }

                                            break;
                                        }
                                    }

                                    NotifyAdjacents(landblock);
                                }
                                else
                                    unloadFailed = true;
                            }
                            finally
                            {
                                landblockLock.ExitWriteLock();
                            }
                        }

                        // The instance is unreachable by key now (or never was, if unloadFailed): release its objects and physics.
                        landblock.Unload(cacheKey.Variant);

                        if (unloadFailed)
                            log.Error($"LandblockManager: 0x{cacheKey.Landblock:X4}, v:{cacheKey.Variant?.ToString() ?? "null"} was queued for destruction but is not the registered instance in loadedLandblocks - unloaded the queued instance, registry untouched");
                        else
                        {
                            var clearedCount = DatabaseManager.World.ClearLandblockCache(landblock.Id.Landblock, cacheKey.Variant);
                            log.Debug($"[Cache Cleanup] Unloaded Landblock {landblock.Id.Raw:X8}. Cleared {clearedCount} cached entities.");
                        }
                    }
                    finally
                    {
                        CloseUnloadGate(cacheKey, unloadGate);
                    }
                }
            }
        }

        /// <summary>
        /// Notifies the adjacent landblocks to rebuild their adjacency cache
        /// Called when a landblock is unloaded
        /// </summary>
        private static void NotifyAdjacents(Landblock landblock)
        {
            var adjacents = GetAdjacents(landblock, landblock.VariationId);

            foreach (var adjacent in adjacents)
                SetAdjacents(adjacent, false, true);
        }

        /// <summary>
        /// Used on server shutdown
        /// </summary>
        public static void AddAllActiveLandblocksToDestructionQueue()
        {
            landblockLock.EnterWriteLock();
            try
            {
                foreach (var landblock in loadedLandblocks)
                    AddToDestructionQueue(landblock.Value, landblock.Key.Variant);
            }
            finally
            {
                landblockLock.ExitWriteLock();
            }
        }

        public static EnvironChangeType? GlobalFogColor;

        private static void SetGlobalFogColor(EnvironChangeType environChangeType)
        {
            if (environChangeType.IsFog())
            {
                if (environChangeType == EnvironChangeType.Clear)
                    GlobalFogColor = null;
                else
                    GlobalFogColor = environChangeType;

                foreach (var landblock in loadedLandblocks)
                    landblock.Value.SendCurrentEnviron();
            }
        }

        private static void SendGlobalEnvironSound(EnvironChangeType environChangeType)
        {
            if (environChangeType.IsSound())
            {
                foreach (var landblock in loadedLandblocks)
                    landblock.Value.SendEnvironChange(environChangeType);
            }
        }

        public static void DoEnvironChange(EnvironChangeType environChangeType)
        {
            landblockLock.EnterWriteLock();
            try
            {
                if (environChangeType.IsFog())
                    SetGlobalFogColor(environChangeType);
                else
                    SendGlobalEnvironSound(environChangeType);
            }
            finally
            {
                landblockLock.ExitWriteLock();
            }
        }
    }
}
