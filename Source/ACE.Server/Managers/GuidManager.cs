using System;
using System.Collections.Generic;
using System.Threading;

using log4net;

using ACE.Entity;
using System.Collections.Concurrent;
using System.Security.Policy;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Used to assign global guids and ensure they are unique to server.
    /// </summary>
    public static class GuidManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Running server is guid master - database only read as startup to get current max per range.
        // weenie class templates Max 65,535 - took Turbine 17 years to get to 10K
        // these will be added by developers and not in game.
        // Nothing in this range is persisted by the game.   Only developers or content creators can create them to be persisted.
        // this is only here for documentation purposes.
        // Fragmentation: None

        /// <summary>
        /// Is equal to uint.MaxValue
        /// </summary>
        public static uint InvalidGuid { get; } = uint.MaxValue;

        private const uint LowIdLimit = 0x1000;

        private class PlayerGuidAllocator
        {
            private readonly uint max;
            private uint current;
            private readonly string name;

            public PlayerGuidAllocator(uint min, uint max, string name)
            {
                this.max = max;

                // Read current value out of ShardDatabase
                lock (this)
                {
                    bool done = false;
                    Database.DatabaseManager.Shard.GetMaxGuidFoundInRange(min, max, dbVal =>
                    {
                        lock (this)
                        {
                            current = dbVal;
                            done = true;
                            Monitor.Pulse(this);
                        }
                    });

                    while (!done)
                        Monitor.Wait(this);

                    if (current == InvalidGuid)
                        current = min;
                    else
                        // Need to start allocating at current value in db +1
                        current++;

                    log.Debug($"{name} GUID Allocator current is now {current:X8} of {max:X8}");

                    if ((max - current) < LowIdLimit)
                        log.Warn($"Dangerously low on {name} GUIDs: {current:X8} of {max:X8}");
                }

                this.name = name;
            }

            public uint Alloc()
            {
                lock (this)
                {
                    if (current == max)
                    {
                        log.Fatal($"Out of {name} GUIDs!");
                        return InvalidGuid;
                    }

                    if (current == max - LowIdLimit)
                        log.Warn($"Running dangerously low on {name} GUIDs, need to defrag");

                    uint ret = current;
                    current += 1;

                    return ret;
                }
            }

            /// <summary>
            /// For information purposes only, do not use the result. Use Alloc() instead
            /// This value represents the current DbMax + 1
            /// </summary>
            public uint Current()
            {
                return current;
            }
        }

        private class DynamicGuidIterator
        {
            private readonly uint _max;
            private readonly ConcurrentQueue<Tuple<DateTime, uint>> recycledGuids = new ConcurrentQueue<Tuple<DateTime, uint>>();
            private readonly ConcurrentQueue<Tuple<uint, uint>> sequenceGapIDs = new ConcurrentQueue<Tuple<uint, uint>>();
            private const int limitAvailableIDsReturnedInGetSequenceGaps = 10000000;
            private uint currentGuid;
            private Tuple<uint, uint> currentSequenceGap;
            private static readonly TimeSpan recycleTime = TimeSpan.FromMinutes(15); //360

            public DynamicGuidIterator(uint min, uint max, string name)
            {
                _max = max;
                Database.DatabaseManager.Shard.GetMaxGuidFoundInRange(min, max, dbVal =>
                {
                    currentGuid = dbVal;
                    if (currentGuid == InvalidGuid)
                        currentGuid = min;
                    else
                        // Need to start allocating at current value in db +1
                        currentGuid++;
                });

                Database.DatabaseManager.Shard.GetSequenceGaps(ObjectGuid.DynamicMin, limitAvailableIDsReturnedInGetSequenceGaps, gaps =>
                {
                    uint total = 0;
                    foreach (var pair in gaps)
                    {
                        total += (pair.end - pair.start) + 1;
                        sequenceGapIDs.Enqueue(new Tuple<uint, uint>(pair.start, pair.end));
                    }
                    log.Debug($"{name} GUID Sequence gaps initialized with total availableIDs of {total:N0}");
                });                

            }

            public void Recycle(uint guid)
            {
                recycledGuids.Enqueue(new Tuple<DateTime, uint>(DateTime.UtcNow, guid));
            }

            public uint Current()
            {
                return currentGuid;
            }

            public uint Alloc()
            {
                lock(recycledGuids)
                {
                    if (recycledGuids.Count > 0)
                    {
                        for (int i = 0; i < recycledGuids.Count && i < 50; i++)
                        {
                            if (recycledGuids.TryDequeue(out var recycledGuid) && recycledGuid != null)
                            {
                                if (DateTime.UtcNow - recycledGuid.Item1 > recycleTime)
                                    return recycledGuid.Item2;
                                else
                                    recycledGuids.Enqueue(recycledGuid);
                            }
                        }
                    }
                }

                lock (sequenceGapIDs)
                {
                    if (currentSequenceGap != null && currentSequenceGap.Item1 < currentSequenceGap.Item2)
                    {
                        var id = currentSequenceGap.Item1;
                        currentSequenceGap = new Tuple<uint, uint>(id + 1, currentSequenceGap.Item2);
                        return id;
                    }
                    else
                    {
                        if (sequenceGapIDs.Count > 0)
                        {
                            if (sequenceGapIDs.TryDequeue(out currentSequenceGap))
                            {
                                var id = currentSequenceGap.Item1;
                                currentSequenceGap = new Tuple<uint, uint>(id + 1, currentSequenceGap.Item2);
                                return id;
                            }
                        }                        
                    }
                    return currentGuid++;
                }
                
            }

            public override string ToString()
            {
                lock (sequenceGapIDs)
                {
                    uint total = 0;
                    foreach (var pair in sequenceGapIDs)
                        total += (pair.Item2 - pair.Item1) + 1;

                    return $"DynamicGuidIterator: current: 0x{currentGuid:X8}, max: 0x{_max:X8}, sequence gap GUIDs available: {total:N0}, recycled GUIDs available: {recycledGuids.Count:N0}";
                }
            }

            public (DateTime nextRecycleTime, int totalPendingRecycledGuids, uint totalSequenceGapGuids) GetRecycleDebugInfo()
            {
                var nextRecycleTime = DateTime.MinValue;
                int totalPendingRecycledGuids;
                uint totalSequenceGapGuids = 0;


                if (recycledGuids.TryPeek(out var firstRecycledGuid))
                    nextRecycleTime = firstRecycledGuid.Item1 + recycleTime;

                totalPendingRecycledGuids = recycledGuids.Count;

                foreach (var pair in sequenceGapIDs)
                    totalSequenceGapGuids += (pair.Item1 - pair.Item2) + 1;


                return (nextRecycleTime, totalPendingRecycledGuids, totalSequenceGapGuids);
            }
        }


        /// <summary>
        /// On a server with ~500 players, about 10,000,000 dynamic GUID's will be requested every 24hr period.
        /// </summary>
        private class DynamicGuidAllocator
        {
            private readonly uint max;
            private uint current;
            private readonly string name;

            private static readonly TimeSpan recycleTime = TimeSpan.FromMinutes(15); //360

            private readonly ConcurrentQueue<Tuple<DateTime, uint>> recycledGuids = new ConcurrentQueue<Tuple<DateTime, uint>>();

            /// <summary>
            /// The value here is the result of two factors:
            /// - A: The total number of GUIDs that are generated during a period of recycledTime (defined above)
            /// - B: The total number of GUIDs that are consumed and saved to the shard between server resets
            /// A safe value might be (2 * A) + (2 * B)
            /// On a shard with severe id fragmentation, this can end up eating more memory to store all the smaller gaps
            /// Once sequence gaps are depleted and there are no available id's in the recycle queue, DB Max + 1 is used
            /// You can monitor the amount of available id's using /serverstatus
            /// </summary>
            private const int limitAvailableIDsReturnedInGetSequenceGaps = 10000000;
            private bool useSequenceGapExhaustedMessageDisplayed;
            private LinkedList<(uint start, uint end)> availableIDs = new LinkedList<(uint start, uint end)>();

            public DynamicGuidAllocator(uint min, uint max, string name)
            {
                this.max = max;

                // Read current value out of ShardDatabase
                lock (availableIDs)
                {
                    bool done = false;
                    Database.DatabaseManager.Shard.GetMaxGuidFoundInRange(min, max, dbVal =>
                    {
                        lock (this)
                        {
                            current = dbVal;
                            done = true;
                            Monitor.Pulse(this);
                        }
                    });

                    while (!done)
                        Monitor.Wait(this);

                    if (current == InvalidGuid)
                        current = min;
                    else
                        // Need to start allocating at current value in db +1
                        current++;

                    log.Debug($"{name} GUID Allocator current is now {current:X8} of {max:X8}");

                    if ((max - current) < LowIdLimit)
                        log.Warn($"Dangerously low on {name} GUIDs: {current:X8} of {max:X8}");
                }

                // Get available ids in the form of sequence gaps
                lock (availableIDs)
                {
                    bool done = false;
                    Database.DatabaseManager.Shard.GetSequenceGaps(ObjectGuid.DynamicMin, limitAvailableIDsReturnedInGetSequenceGaps, gaps =>
                    {
                        availableIDs = new LinkedList<(uint start, uint end)>(gaps);
                        uint total = 0;
                        foreach (var pair in availableIDs)
                            total += (pair.end - pair.start) + 1;
                        log.Debug($"{name} GUID Sequence gaps initialized with total availableIDs of {total:N0}");
                        done = true;
                        Monitor.Pulse(this);                        
                    });

                    while (!done)
                        Monitor.Wait(this);
                }

                this.name = name;
            }

            public uint Alloc()
            {

                // First, try to use a recycled Guid
                lock (recycledGuids)
                {
                    if (recycledGuids.TryPeek(out var result) && DateTime.UtcNow - result.Item1 > recycleTime)
                    {
                        if (recycledGuids.TryDequeue(out var recycledGuid) && recycledGuid != null)
                            return recycledGuid.Item2;
                        //Console.WriteLine(result.Item2 + " Recycled Guids: " + recycledGuids.Count);
                        //return result.Item2;
                    }
                }

                lock(availableIDs)
                { 
                // Second, try to use a known available Guid
                    if (availableIDs.First != null)
                    {
                        var id = availableIDs.First.Value.start;

                        if (id == availableIDs.First.Value.end)
                        {
                            try
                            {
                                availableIDs?.RemoveFirst();
                            }
                            catch (Exception ex)
                            {
                                log.Error($"Error removing first available id from {name} GUIDs: {ex.Message}");
                                if (availableIDs == null)
                                {
                                    lock (availableIDs)
                                    {
                                        bool done = false;
                                        Database.DatabaseManager.Shard.GetSequenceGaps(ObjectGuid.DynamicMin, limitAvailableIDsReturnedInGetSequenceGaps, gaps =>
                                        {
                                            availableIDs = new LinkedList<(uint start, uint end)>(gaps);
                                            uint total = 0;
                                            foreach (var pair in availableIDs)
                                                total += (pair.end - pair.start) + 1;
                                            log.Debug($"{name} GUID Sequence gaps initialized with total availableIDs of {total:N0}");
                                            done = true;
                                            Monitor.Pulse(availableIDs);
                                        });

                                        while (!done)
                                            Monitor.Wait(availableIDs);
                                    }
                                }
                            }
                            

                            //if (availableIDs.First == null)
                            //    log.Warn($"Sequence gap GUIDs depleted on {name}");
                        }
                        else
                            availableIDs.First.Value = (id + 1, availableIDs.First.Value.end);

                        return id;
                    }
                    else
                    {
                        if (!useSequenceGapExhaustedMessageDisplayed)
                        {
                            log.Debug($"{name} GUID Sequence gaps exhausted. Any new, non-recycled GUID will be current + 1. current is now {current:X8}");
                            useSequenceGapExhaustedMessageDisplayed = true;
                        }
                    }
                }
                // Lastly, use an id that increments our max
                if (current == max)
                {
                    log.Fatal($"Out of {name} GUIDs!");
                    return InvalidGuid;
                }

                if (current == max - LowIdLimit)
                    log.Error($"Running dangerously low on {name} GUIDs, need to defrag");

                uint ret = current;
                current += 1;

                return ret;
                
            }

            /// <summary>
            /// For information purposes only, do not use the result. Use Alloc() instead
            /// This is the value that might be used in the event that there are no recycled guid available and sequence gap guids have been exhausted
            /// This value represents the current DbMax + 1
            /// </summary>
            public uint Current()
            {
                return current;
            }

            public void Recycle(uint guid)
            {
                recycledGuids.Enqueue(new Tuple<DateTime, uint>(DateTime.UtcNow, guid));
            }

            public override string ToString()
            {
                lock (availableIDs)
                {
                    uint total = 0;
                    foreach (var pair in availableIDs)
                        total += (pair.end - pair.start) + 1;

                    return $"DynamicGuidAllocator: {name}, current: 0x{current:X8}, max: 0x{max:X8}, sequence gap GUIDs available: {total:N0}, recycled GUIDs available: {recycledGuids.Count:N0}";
                }
            }

            public (DateTime nextRecycleTime, int totalPendingRecycledGuids, uint totalSequenceGapGuids) GetRecycleDebugInfo()
            {
                var nextRecycleTime = DateTime.MinValue;
                int totalPendingRecycledGuids;
                uint totalSequenceGapGuids = 0;


                if (recycledGuids.TryPeek(out var firstRecycledGuid))
                    nextRecycleTime = firstRecycledGuid.Item1 + recycleTime;

                totalPendingRecycledGuids = recycledGuids.Count;

                foreach (var pair in availableIDs)
                    totalSequenceGapGuids += (pair.end - pair.start) + 1;
                

                return (nextRecycleTime, totalPendingRecycledGuids, totalSequenceGapGuids);
            }
        }

        private static PlayerGuidAllocator playerAlloc;
        private static DynamicGuidAllocator dynamicAlloc;
        private static DynamicGuidIterator dynamicIterator;

        public static void Initialize()
        {
            playerAlloc = new PlayerGuidAllocator(ObjectGuid.PlayerMin, ObjectGuid.PlayerMax, "player");
            //dynamicAlloc = new DynamicGuidAllocator(ObjectGuid.DynamicMin, ObjectGuid.DynamicMax, "dynamic");
            dynamicIterator = new DynamicGuidIterator(ObjectGuid.DynamicMin, ObjectGuid.DynamicMax, "dynamicI");

        }

        /// <summary>
        /// Returns New Player Guid
        /// </summary>
        public static ObjectGuid NewPlayerGuid()
        {
            return new ObjectGuid(playerAlloc.Alloc());
        }

        /// <summary>
        /// These represent items are generated in the world.
        /// Some of them will be saved to the Shard db.
        /// They can be monsters, loot, etc..
        /// </summary>
        public static ObjectGuid NewDynamicGuid()
        {
            return new ObjectGuid(dynamicIterator.Alloc());
        }

        /// <summary>
        /// Guid will be added to the recycle queue, and available for use in GuidAllocator.recycleTime
        /// </summary>
        /// <param name="guid"></param>
        public static void RecycleDynamicGuid(ObjectGuid guid)
        {
            dynamicIterator.Recycle(guid.Full);
        }


        public static string GetDynamicGuidDebugInfo()
        {
            return dynamicIterator.ToString();
        }

        public static string GetIdListCommandOutput()
        {
            var playerGuidCurrent = playerAlloc.Current();
            var dynamicGuidCurrent = dynamicIterator.Current();
            var dynamicDebugInfo = dynamicIterator.GetRecycleDebugInfo();

            string message = $"The next Player GUID to be allocated is expected to be: 0x{playerGuidCurrent:X}\n";

            if (dynamicDebugInfo.nextRecycleTime == DateTime.MinValue)
                message += $"After {dynamicDebugInfo.totalSequenceGapGuids:N0} sequence gap ids have been consumed, and {dynamicDebugInfo.totalPendingRecycledGuids:N0} recycled ids have been consumed, the next id will be {dynamicGuidCurrent:X8}";
            else
            {
                var nextDynamicIsAvailIn = dynamicDebugInfo.nextRecycleTime - DateTime.UtcNow;

                if (nextDynamicIsAvailIn.TotalSeconds <= 0)
                    message += $"After {dynamicDebugInfo.totalSequenceGapGuids:N0} sequence gap ids have been consumed, and {dynamicDebugInfo.totalPendingRecycledGuids:N0} recycled ids have been consumed, the next of which are available now, the next id will be: 0x{dynamicGuidCurrent:X8}";
                else
                    message += $"After {dynamicDebugInfo.totalSequenceGapGuids:N0} sequence gap ids have been consumed, and {dynamicDebugInfo.totalPendingRecycledGuids:N0} recycled ids have been consumed, the next of which is available in {nextDynamicIsAvailIn.TotalMinutes:N1} m, the next id will be: 0x{dynamicGuidCurrent:X8}";
            }

            return message;
        }
    }
}
