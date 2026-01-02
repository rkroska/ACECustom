using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using log4net;

using ACE.Database.Entity;
using ACE.Database.Models.Shard;
using ACE.Entity.Enum;
using System.Diagnostics;
using System.Linq;

namespace ACE.Database
{
    public class SerializedShardDatabase
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Work item that can be executed inline on worker threads.
        /// </summary>
        private sealed class WorkItem
        {
            public readonly Action Work;
            public readonly string Key;

            public WorkItem(Action work, string key)
            {
                Work = work ?? throw new ArgumentNullException(nameof(work));
                Key = key ?? throw new ArgumentNullException(nameof(key));
            }
        }

        /// <summary>
        /// This is the base database that SerializedShardDatabase is a wrapper for.
        /// </summary>
        public readonly ShardDatabase BaseDatabase;

        // Reused per worker thread - safe because DoSaves is single-threaded
        // Must never be accessed from other threads
        protected readonly Stopwatch stopwatch = new Stopwatch();

        private readonly BlockingCollection<WorkItem> _readOnlyQueue = new BlockingCollection<WorkItem>();
        private readonly UniqueQueue<WorkItem, string> _uniqueQueue = new(item => item.Key);
        private volatile bool _workerThreadRunning = true;

        private enum CoalesceState
        {
            Idle = 0,
            Queued = 1,
            Saving = 2,
            Dirty = 3
        }

        private sealed class CoalesceTracker
        {
            public int State = (int)CoalesceState.Idle;
            // Use Interlocked for thread-safe updates
            public long LastUsedTicks = DateTime.UtcNow.Ticks;
            // Track if state was dirty when save started (to handle redundant saves correctly)
            public int WasDirtyWhenStarted = 0; // 0 = false, 1 = true
            
            public DateTime LastUsedUtc
            {
                get => new DateTime(Interlocked.Read(ref LastUsedTicks));
            }
            
            public void UpdateLastUsed()
            {
                Interlocked.Exchange(ref LastUsedTicks, DateTime.UtcNow.Ticks);
            }
        }

        private readonly ConcurrentDictionary<string, CoalesceTracker> _coalesce = new();
        
        /// <summary>
        /// Removes idle trackers that haven't been used recently to prevent unbounded growth.
        /// Should be called periodically (e.g., every few minutes).
        /// Includes a minimum lifetime check to prevent removing trackers that were just created
        /// and might be between enqueue and start.
        /// 
        /// IMPORTANT: This method must be called periodically from external code (e.g., a timer or periodic task).
        /// If this is never called, the _coalesce dictionary will grow unbounded over time.
        /// Recommended: Call every 5-10 minutes with maxIdleTime of 10-15 minutes.
        /// </summary>
        public void CleanupIdleTrackers(TimeSpan maxIdleTime)
        {
            // Add a minimum lifetime margin to prevent removing trackers that were just created
            // This prevents race conditions where a tracker is created, enqueued, then removed
            // before the work actually starts
            const int minimumLifetimeSeconds = 30; // Minimum 30 seconds before cleanup
            var minimumLifetime = TimeSpan.FromSeconds(minimumLifetimeSeconds);
            var cutoff = DateTime.UtcNow - maxIdleTime;
            var minimumCutoff = DateTime.UtcNow - minimumLifetime;
            var keysToRemove = new List<string>();
            
            foreach (var kvp in _coalesce)
            {
                var state = (CoalesceState)Volatile.Read(ref kvp.Value.State);
                var lastUsed = kvp.Value.LastUsedUtc; // Thread-safe read via property
                
                // Only remove if: Idle, older than maxIdleTime, AND older than minimum lifetime
                if (state == CoalesceState.Idle && lastUsed < cutoff && lastUsed < minimumCutoff)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var key in keysToRemove)
            {
                // Double-check state is still Idle before removing
                if (_coalesce.TryGetValue(key, out var tracker))
                {
                    var state = (CoalesceState)Volatile.Read(ref tracker.State);
                    if (state == CoalesceState.Idle)
                    {
                        _coalesce.TryRemove(key, out _);
                    }
                }
            }
        }

        private Thread _workerThreadReadOnly;
        private Thread _workerThread;

        internal SerializedShardDatabase(ShardDatabase shardDatabase)
        {
            BaseDatabase = shardDatabase;
        }

        public void Start()
        {

            _workerThreadReadOnly = new Thread(DoReadOnlyWork)
            {
                Name = "Serialized Shard Database - Reading"
            };
            _workerThread = new Thread(DoSaves)
            {
                Name = "Serialized Shard Database - Character Saves"
            };

            _workerThreadReadOnly.Start();
            _workerThread.Start();
            stopwatch.Start();
        }

        public void Stop()
        {
            // Signal worker threads to stop
            Volatile.Write(ref _workerThreadRunning, false);
            
            // Complete adding to read-only queue (allows Take() to exit)
            _readOnlyQueue.CompleteAdding();
            
            // Wait for worker threads to finish processing current items
            _workerThreadReadOnly.Join();
            _workerThread.Join();

            // Now safe to dispose collections - all work has completed
            _readOnlyQueue.Dispose();
            _uniqueQueue.Dispose();
        }

        public List<string> QueueReport()
        {
            return _uniqueQueue.ToArray().Select(item => item.Key).ToList();
        }

        public List<string> ReadOnlyQueueReport()
        {
            return _readOnlyQueue.Select(item => item.Key).ToList();
        }

        private void DoReadOnlyWork()
        {
            while (!_readOnlyQueue.IsAddingCompleted)
            {
                try
                {
                    // Use blocking Take() to avoid busy spinning
                    // This will block until an item is available or CompleteAdding is called
                    WorkItem item = _readOnlyQueue.Take();
                    
                    try
                    {
                        // Execute work inline on this worker thread (not ThreadPool)
                        item.Work();
                    }
                    catch (Exception e)
                    {
                        log.Error($"[DATABASE] DoReadOnlyWork failed for item '{item.Key}'", e);
                        // Continue processing other items
                    }
                }
                catch (ObjectDisposedException)
                {
                    // the _readOnlyQueue has been disposed, we're good
                    break;
                }
                catch (InvalidOperationException)
                {
                    // _readOnlyQueue is empty and CompleteAdding has been called -- we're done here
                    break;
                }
            }
        }
        private void DoSaves()
        {
            while (Volatile.Read(ref _workerThreadRunning) || _uniqueQueue.Count > 0)
            {
                try
                {
                    // Use TryDequeue to avoid Count race condition
                    if (!_uniqueQueue.TryDequeue(out WorkItem item))
                    {
                        // Queue is empty, sleep briefly to avoid busy waiting
                        // Note: This is suboptimal but necessary until UniqueQueue supports blocking dequeue
                        Thread.Sleep(10);
                        continue;
                    }

                    try
                    {
                        if (item == null)
                        {
                            log.Warn("[DATABASE] DoSaves: Dequeued null work item, skipping");
                            continue;
                        }
                        
                        stopwatch.Restart();
                        
                        // Execute work inline on this worker thread (not ThreadPool)
                        item.Work();
                        
                        var workDuration = stopwatch.ElapsedMilliseconds;

                        if (workDuration >= 5000)
                        {
                            log.Error(
                                $"[DATABASE] Work item '{item.Key}' took {workDuration}ms, queue: {_uniqueQueue.Count}");
                        }
                        else if (workDuration >= 1000)
                        {
                            // Warn if work takes more than 1 second (may indicate callback blocking)
                            log.Warn(
                                $"[DATABASE] Work item '{item.Key}' took {workDuration}ms (callback may be blocking DB pipeline)");
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"[DATABASE] DoSaves work item '{item?.Key ?? "unknown"}' failed", ex);
                        // Continue processing other items - can't block other db work because 1 fails
                    }

                }
                catch (ObjectDisposedException)
                {
                    // the _uniqueQueue has been disposed, we're good
                    break;
                }
                catch (InvalidOperationException)
                {
                    // _uniqueQueue is empty - check if we should exit
                    if (!Volatile.Read(ref _workerThreadRunning))
                    {
                        log.Info("[DATABASE] DoSaves: No more tasks to process, exiting.");
                        break;
                    }
                    else
                    {
                        log.Warn("[DATABASE] DoSaves: Queue is empty but worker thread is still running.");
                        Thread.Sleep(10); // Brief sleep before retry
                        continue;
                    }
                }
            }
        }


        public int QueueCount => _uniqueQueue.Count;

        /// <summary>
        /// Attempts to enqueue work with coalescing support.
        /// Returns true if the work was enqueued, false if it was marked dirty instead.
        /// </summary>
        private bool TryEnqueueCoalesced(string coalesceKey, Func<WorkItem> workItemFactory)
        {
            var tracker = _coalesce.GetOrAdd(coalesceKey, _ => new CoalesceTracker());
            tracker.UpdateLastUsed();

            while (true)
            {
                var state = (CoalesceState)Volatile.Read(ref tracker.State);

                if (state == CoalesceState.Idle)
                {
                    if (Interlocked.CompareExchange(ref tracker.State, (int)CoalesceState.Queued, (int)CoalesceState.Idle)
                        == (int)CoalesceState.Idle)
                    {
                        var workItem = workItemFactory?.Invoke();
                        if (workItem == null)
                        {
                            Interlocked.Exchange(ref tracker.State, (int)CoalesceState.Idle);
                            return false;
                        }

                        _uniqueQueue.Enqueue(workItem);
                        return true;
                    }

                    continue;
                }

                if (state == CoalesceState.Queued)
                {
                    // There's already work queued but not started yet
                    // Mark as Dirty to ensure a followup save happens after the queued work completes
                    // This prevents losing work if mutations happen after the queued work is created
                    Interlocked.Exchange(ref tracker.State, (int)CoalesceState.Dirty);
                    return false;
                }

                if (state == CoalesceState.Saving)
                {
                    // A save is in progress, mark as Dirty so it will enqueue a followup
                    Interlocked.Exchange(ref tracker.State, (int)CoalesceState.Dirty);
                    return false;
                }

                if (state == CoalesceState.Dirty)
                    return false;
            }
        }

        private void OnCoalescedSaveStarted(string coalesceKey)
        {
            if (!_coalesce.TryGetValue(coalesceKey, out var tracker))
                return;

            // Update LastUsedUtc to prevent premature cleanup during long saves
            tracker.UpdateLastUsed();

            // Transition Queued -> Saving, or Dirty -> Saving (preserving dirty signal)
            // Use CAS loop to handle both cases atomically
            while (true)
            {
                var currentState = (CoalesceState)Volatile.Read(ref tracker.State);
                
                if (currentState == CoalesceState.Queued)
                {
                    // Normal case: Queued -> Saving
                    var prev = (CoalesceState)Interlocked.CompareExchange(
                        ref tracker.State,
                        (int)CoalesceState.Saving,
                        (int)CoalesceState.Queued);
                    
                    if (prev == CoalesceState.Queued)
                    {
                        // Successfully transitioned, clear dirty flag
                        Interlocked.Exchange(ref tracker.WasDirtyWhenStarted, 0);
                        return;
                    }
                    // State changed, retry
                    continue;
                }
                
                if (currentState == CoalesceState.Dirty)
                {
                    // State was Dirty (marked dirty while queued)
                    // Transition Dirty -> Saving, but remember it was dirty
                    var prev = (CoalesceState)Interlocked.CompareExchange(
                        ref tracker.State,
                        (int)CoalesceState.Saving,
                        (int)CoalesceState.Dirty);
                    
                    if (prev == CoalesceState.Dirty)
                    {
                        // Successfully transitioned, mark that it was dirty
                        Interlocked.Exchange(ref tracker.WasDirtyWhenStarted, 1);
                        return;
                    }
                    // State changed, retry
                    continue;
                }
                
                // Unexpected state, just return
                return;
            }
        }

        private void OnCoalescedSaveFinished(string coalesceKey, Func<WorkItem> enqueueFollowup)
        {
            if (!_coalesce.TryGetValue(coalesceKey, out var tracker))
                return;

            // Use CAS loop to atomically transition from Dirty to Queued, avoiding race window
            // We're finishing a save, so we're in Saving state. Check if it became Dirty.
            while (true)
            {
                var currentState = (CoalesceState)Volatile.Read(ref tracker.State);
                
                if (currentState == CoalesceState.Dirty)
                {
                    // Atomically transition Dirty -> Queued (don't go through Idle)
                    var prev = (CoalesceState)Interlocked.CompareExchange(
                        ref tracker.State, 
                        (int)CoalesceState.Queued, 
                        (int)CoalesceState.Dirty);
                    
                    if (prev == CoalesceState.Dirty)
                    {
                        // Successfully transitioned Dirty -> Queued, enqueue followup
                        // Update LastUsedUtc when enqueuing followup to prevent premature cleanup
                        tracker.UpdateLastUsed();
                        
                        var next = enqueueFollowup?.Invoke();
                        if (next != null)
                        {
                            _uniqueQueue.Enqueue(next);
                        }
                        return;
                    }
                    // State changed, retry
                    continue;
                }
                
                if (currentState == CoalesceState.Saving)
                {
#if DEBUG
                    // Invariant: Saving is the expected state when OnCoalescedSaveFinished is called
                    // If we see something else, it indicates a logic error or race condition
                    System.Diagnostics.Debug.Assert(currentState == CoalesceState.Saving, 
                        $"OnCoalescedSaveFinished: Expected Saving state but found {currentState}");
#endif
                    // Check if it was dirty when we started (to handle redundant saves correctly)
                    // WasDirtyWhenStarted is an int, use Volatile.Read for thread-safe read
                    bool wasDirtyWhenStarted = Volatile.Read(ref tracker.WasDirtyWhenStarted) != 0;
                    
                    // If it was dirty when started, enqueue followup (even if state is still Saving)
                    if (wasDirtyWhenStarted)
                    {
                        // Transition Saving -> Queued and enqueue followup
                        var prevState = (CoalesceState)Interlocked.CompareExchange(
                            ref tracker.State,
                            (int)CoalesceState.Queued,
                            (int)CoalesceState.Saving);
                        
                        if (prevState == CoalesceState.Saving)
                        {
                            // Successfully transitioned, enqueue followup
                            tracker.UpdateLastUsed();
                            var next = enqueueFollowup?.Invoke();
                            if (next != null)
                            {
                                _uniqueQueue.Enqueue(next);
                            }
                            // Clear the flag
                            Interlocked.Exchange(ref tracker.WasDirtyWhenStarted, 0);
                            return;
                        }
                        // State changed (probably became Dirty), retry
                        continue;
                    }
                    
                    // Not dirty when started, transition Saving -> Idle
                    var prevState2 = (CoalesceState)Interlocked.CompareExchange(
                        ref tracker.State, 
                        (int)CoalesceState.Idle, 
                        (int)CoalesceState.Saving);
                    
                    if (prevState2 == CoalesceState.Saving)
                    {
                        // Successfully transitioned to Idle
                        return;
                    }
                    // State changed (probably became Dirty), retry
                    continue;
                }
                
                // Unexpected state, just return
                return;
            }
        }

        public void GetCurrentQueueWaitTime(Action<TimeSpan> callback)
        {
            var initialCallTime = DateTime.UtcNow;
            // Use unique key per request to prevent callback dropping
            var workItemKey = $"GetCurrentQueueWaitTime:{initialCallTime.Ticks}";

            _uniqueQueue.Enqueue(new WorkItem(() =>
            {
                try
                {
                    callback?.Invoke(DateTime.UtcNow - initialCallTime);
                }
                catch (Exception ex)
                {
                    log.Error("[DATABASE] GetCurrentQueueWaitTime callback threw exception", ex);
                }
            }, workItemKey));
        }


        /// <summary>
        /// Will return uint.MaxValue if no records were found within the range provided.
        /// </summary>
        public void GetMaxGuidFoundInRange(uint min, uint max, Action<uint> callback)
        {
            _readOnlyQueue.Add(new WorkItem(() =>
            {
                try
                {
                    var result = BaseDatabase.GetMaxGuidFoundInRange(min, max);
                    callback?.Invoke(result);
                }
                catch (Exception ex)
                {
                    log.Error("[DATABASE] GetMaxGuidFoundInRange callback threw exception", ex);
                }
            }, "GetMaxGuidFoundInRange: " + min));
        }

        /// <summary>
        /// This will return available id's, in the form of sequence gaps starting from min.<para />
        /// If a gap is just 1 value wide, then both start and end will be the same number.
        /// </summary>
        public void GetSequenceGaps(uint min, uint limitAvailableIDsReturned, Action<List<(uint start, uint end)>> callback)
        {
            _readOnlyQueue.Add(new WorkItem(() =>
            {
                try
                {
                    var result = BaseDatabase.GetSequenceGaps(min, limitAvailableIDsReturned);
                    callback?.Invoke(result);
                }
                catch (Exception ex)
                {
                    log.Error($"[DATABASE] GetSequenceGaps callback threw exception: {ex}", ex);
                }
            }, "GetSequenceGaps: " + min));
        }


        public void SaveBiota(ACE.Entity.Models.Biota biota, ReaderWriterLockSlim rwLock, Action<bool> callback)
        {
            _uniqueQueue.Enqueue(new WorkItem(() =>
            {
                try
                {
                    var result = BaseDatabase.SaveBiota(biota, rwLock);
                    callback?.Invoke(result);
                }
                catch (Exception ex)
                {
                    log.Error($"[DATABASE] SaveBiota callback threw exception for biota {biota.Id}", ex);
                }
            }, "SaveBiota: " + biota.Id));
        }


        public void SaveBiotasInParallel(IEnumerable<(ACE.Entity.Models.Biota biota, ReaderWriterLockSlim rwLock)> biotas, Action<bool> callback, string sourceTrace)
        {
            _uniqueQueue.Enqueue(new WorkItem(() =>
            {
                try
                {
                    var result = BaseDatabase.SaveBiotasInParallel(biotas);
                    callback?.Invoke(result);
                }
                catch (Exception ex)
                {
                    log.Error("[DATABASE] SaveBiotasInParallel callback threw exception", ex);
                }
            }, "SaveBiotasInParallel " + sourceTrace));
        }

        /// <summary>
        /// Saves player biotas with coalescing support. Callback runs on DB worker thread and must not touch world state directly.
        /// </summary>
        public void SavePlayerBiotasCoalesced(
            uint playerId,
            Func<IEnumerable<(ACE.Entity.Models.Biota biota, ReaderWriterLockSlim rwLock)>> getBiotas,
            Action<bool> callback, // WARNING: Runs on DB worker thread - must not touch world state directly
            string sourceTrace)
        {
            var coalesceKey = "player:" + playerId;

            // Create follow-up work item factory (must be reusable to chain if marked Dirty during followup)
            // Declare as null first, then assign the lambda that references itself
            Func<WorkItem> createFollowupWorkItem = null;
            createFollowupWorkItem = () => new WorkItem(() =>
            {
                OnCoalescedSaveStarted(coalesceKey);
                bool r2 = false;
                try
                {
                    var biotas = getBiotas();
                    r2 = BaseDatabase.SaveBiotasInParallel(biotas);
                }
                finally
                {
                    // Pass the same factory again so it can chain if marked Dirty during followup
                    OnCoalescedSaveFinished(coalesceKey, createFollowupWorkItem);
                }
                // Invoke callback after state transition is complete to avoid reentrancy surprises
                // If callback enqueues another save, state will be Idle/Queued, not Saving
                try
                {
                    callback?.Invoke(r2);
                }
                catch (Exception ex)
                {
                    log.Error($"[DATABASE] SavePlayerBiotasCoalesced followup callback threw exception for player {playerId}", ex);
                }
            }, "SavePlayerBiotasCoalesced followup:" + playerId);

            TryEnqueueCoalesced(coalesceKey, () =>
                new WorkItem(() =>
                {
                    OnCoalescedSaveStarted(coalesceKey);

                    bool result = false;
                    try
                    {
                        var biotas = getBiotas();
                        result = BaseDatabase.SaveBiotasInParallel(biotas);
                    }
                    finally
                    {
                        OnCoalescedSaveFinished(coalesceKey, createFollowupWorkItem);
                    }
                    // Invoke callback after state transition is complete to avoid reentrancy surprises
                    // If callback enqueues another save, state will be Idle/Queued, not Saving
                    try
                    {
                        callback?.Invoke(result);
                    }
                    catch (Exception ex)
                    {
                        log.Error($"[DATABASE] SavePlayerBiotasCoalesced callback threw exception for player {playerId}", ex);
                    }
                }, "SavePlayerBiotasCoalesced:" + playerId));
        }

        /// <summary>
        /// Saves character with coalescing support. Callback runs on DB worker thread and must not touch world state directly.
        /// </summary>
        public void SaveCharacterCoalesced(
            uint characterId,
            Func<(Character character, ReaderWriterLockSlim rwLock)> getCharacter,
            Action<bool> callback) // WARNING: Runs on DB worker thread - must not touch world state directly
        {
            var coalesceKey = "character:" + characterId;

            // Create follow-up work item factory (must be reusable to chain if marked Dirty during followup)
            // Declare as null first, then assign the lambda that references itself
            Func<WorkItem> createFollowupWorkItem = null;
            createFollowupWorkItem = () => new WorkItem(() =>
            {
                OnCoalescedSaveStarted(coalesceKey);
                bool r2 = false;
                try
                {
                    var (character, rwLock) = getCharacter();
                    r2 = BaseDatabase.SaveCharacter(character, rwLock);
                }
                finally
                {
                    // Pass the same factory again so it can chain if marked Dirty during followup
                    OnCoalescedSaveFinished(coalesceKey, createFollowupWorkItem);
                }
                // Invoke callback after state transition is complete to avoid reentrancy surprises
                // If callback enqueues another save, state will be Idle/Queued, not Saving
                try
                {
                    callback?.Invoke(r2);
                }
                catch (Exception ex)
                {
                    log.Error($"[DATABASE] SaveCharacterCoalesced followup callback threw exception for character {characterId}", ex);
                }
            }, "SaveCharacterCoalesced followup:" + characterId);

            TryEnqueueCoalesced(coalesceKey, () =>
                new WorkItem(() =>
                {
                    OnCoalescedSaveStarted(coalesceKey);

                    bool result = false;
                    try
                    {
                        var (character, rwLock) = getCharacter();
                        result = BaseDatabase.SaveCharacter(character, rwLock);
                    }
                    finally
                    {
                        OnCoalescedSaveFinished(coalesceKey, createFollowupWorkItem);
                    }
                    // Invoke callback after state transition is complete to avoid reentrancy surprises
                    // If callback enqueues another save, state will be Idle/Queued, not Saving
                    try
                    {
                        callback?.Invoke(result);
                    }
                    catch (Exception ex)
                    {
                        log.Error($"[DATABASE] SaveCharacterCoalesced callback threw exception for character {characterId}", ex);
                    }
                }, "SaveCharacterCoalesced:" + characterId));
        }

        public void RemoveBiota(uint id, Action<bool> callback)
        {
            _uniqueQueue.Enqueue(new WorkItem(() =>
            {
                try
                {
                    var result = BaseDatabase.RemoveBiota(id);
                    callback?.Invoke(result);
                }
                catch (Exception ex)
                {
                    log.Error($"[DATABASE] RemoveBiota callback threw exception for id {id}", ex);
                }
            }, "RemoveBiota: " + id));
        }

        public void RemoveBiota(uint id, Action<bool> callback, Action<TimeSpan, TimeSpan> performanceResults)
        {
            var initialCallTime = DateTime.UtcNow;

            _uniqueQueue.Enqueue(new WorkItem(() =>
            {
                try
                {
                    var taskStartTime = DateTime.UtcNow;
                    var result = BaseDatabase.RemoveBiota(id);
                    var taskCompletedTime = DateTime.UtcNow;
                    callback?.Invoke(result);
                    performanceResults?.Invoke(taskStartTime - initialCallTime, taskCompletedTime - taskStartTime);
                }
                catch (Exception ex)
                {
                    log.Error($"[DATABASE] RemoveBiota callback threw exception for id {id}", ex);
                }
            }, "RemoveBiota2:" + id));
        }

        public void RemoveBiotasInParallel(IEnumerable<uint> ids, Action<bool> callback, Action<TimeSpan, TimeSpan> performanceResults)
        {
            var initialCallTime = DateTime.UtcNow;
            // Create a deterministic key so repeated calls for the same set coalesce
            var idKey = "RemoveBiotasInParallel:" + string.Join(",", ids.OrderBy(i => i));

            _uniqueQueue.Enqueue(new WorkItem(() =>
            {
                try
                {
                    var taskStartTime = DateTime.UtcNow;
                    var result = BaseDatabase.RemoveBiotasInParallel(ids);
                    var taskCompletedTime = DateTime.UtcNow;
                    callback?.Invoke(result);
                    performanceResults?.Invoke(taskStartTime - initialCallTime, taskCompletedTime - taskStartTime);
                }
                catch (Exception ex)
                {
                    log.Error($"[DATABASE] RemoveBiotasInParallel callback threw exception: {ex}", ex);
                }
            }, idKey));
        }


        public void GetPossessedBiotasInParallel(uint id, Action<PossessedBiotas> callback)
        {
            _readOnlyQueue.Add(new WorkItem(() =>
            {
                try
                {
                    var c = BaseDatabase.GetPossessedBiotasInParallel(id);
                    callback?.Invoke(c);
                }
                catch (Exception ex)
                {
                    log.Error($"[DATABASE] GetPossessedBiotasInParallel callback threw exception for id {id}", ex);
                }
            }, "GetPossessedBiotasInParallel: " + id));
        }

        public void GetInventoryInParallel(uint parentId, bool includedNestedItems, Action<List<Biota>> callback)
        {
            _readOnlyQueue.Add(new WorkItem(() =>
            {
                try
                {
                    var c = BaseDatabase.GetInventoryInParallel(parentId, includedNestedItems);
                    callback?.Invoke(c);
                }
                catch (Exception ex)
                {
                    log.Error($"[DATABASE] GetInventoryInParallel callback threw exception for parentId {parentId}", ex);
                }
            }, "GetInventoryInParallel: " + parentId));

        }


        public void IsCharacterNameAvailable(string name, Action<bool> callback)
        {
            _readOnlyQueue.Add(new WorkItem(() =>
            {
                try
                {
                    var result = BaseDatabase.IsCharacterNameAvailable(name);
                    callback?.Invoke(result);
                }
                catch (Exception ex)
                {
                    log.Error($"[DATABASE] IsCharacterNameAvailable callback threw exception for name {name}", ex);
                }
            }, "IsCharacterNameAvailable: " + name));
        }

        public void GetCharacters(uint accountId, bool includeDeleted, Action<List<Character>> callback)
        {
            _readOnlyQueue.Add(new WorkItem(() =>
            {
                try
                {
                    var result = BaseDatabase.GetCharacters(accountId, includeDeleted);
                    callback?.Invoke(result);
                }
                catch (Exception ex)
                {
                    log.Error($"[DATABASE] GetCharacters callback threw exception for accountId {accountId}", ex);
                }
            }, "GetCharacters: " + accountId));
        }

        public void GetLoginCharacters(uint accountId, bool includeDeleted, Action<List<LoginCharacter>> callback)
        {
            _readOnlyQueue.Add(new WorkItem(() =>
            {
                try
                {
                    var result = BaseDatabase.GetCharacterListForLogin(accountId, includeDeleted);
                    callback?.Invoke(result);
                }
                catch (Exception ex)
                {
                    log.Error($"[DATABASE] GetLoginCharacters callback threw exception for accountId {accountId}", ex);
                }
            }, "GetCharacterListForLogin: " + accountId));
        }

        public void GetCharacter(uint characterId, Action<Character> callback)
        {
            _readOnlyQueue.Add(new WorkItem(() =>
            {
                try
                {
                    var result = BaseDatabase.GetCharacter(characterId);
                    callback?.Invoke(result);
                }
                catch (Exception ex)
                {
                    log.Error($"[DATABASE] GetCharacter callback threw exception for characterId {characterId}", ex);
                }
            }, "GetCharacter: " + characterId));
        }

        public Character GetCharacterSynchronous(uint characterId)
        {
            return BaseDatabase.GetCharacter(characterId);
        }

        public void SaveCharacter(Character character, ReaderWriterLockSlim rwLock, Action<bool> callback)
        {
            _uniqueQueue.Enqueue(new WorkItem(() =>
            {
                try
                {
                    var result = BaseDatabase.SaveCharacter(character, rwLock);
                    callback?.Invoke(result);
                }
                catch (Exception ex)
                {
                    log.Error($"[DATABASE] SaveCharacter callback threw exception for character {character.Id}", ex);
                }
            }, "SaveCharacter: " + character.Id));
        }

        public void RenameCharacter(Character character, string newName, ReaderWriterLockSlim rwLock, Action<bool> callback)
        {
            _uniqueQueue.Enqueue(new WorkItem(() =>
            {
                try
                {
                    var result = BaseDatabase.RenameCharacter(character, newName, rwLock);
                    callback?.Invoke(result);
                }
                catch (Exception ex)
                {
                    log.Error($"[DATABASE] RenameCharacter callback threw exception for character {character.Id}", ex);
                }
            }, "RenameCharacter: " + character.Id));
        }

        public void SetCharacterAccessLevelByName(string name, AccessLevel accessLevel, Action<uint> callback)
        {
            // TODO
            throw new NotImplementedException();
        }


        public void AddCharacterInParallel(ACE.Entity.Models.Biota biota, ReaderWriterLockSlim biotaLock, IEnumerable<(ACE.Entity.Models.Biota biota, ReaderWriterLockSlim rwLock)> possessions, Character character, ReaderWriterLockSlim characterLock, Action<bool> callback)
        {
            _uniqueQueue.Enqueue(new WorkItem(() =>
            {
                try
                {
                    var result = BaseDatabase.AddCharacterInParallel(biota, biotaLock, possessions, character, characterLock);
                    callback?.Invoke(result);
                }
                catch (Exception ex)
                {
                    log.Error($"[DATABASE] AddCharacterInParallel callback threw exception for character {character.Id}", ex);
                }
            }, "AddCharacterInParallel: " + character.Id));
        }

        /// <summary>
        /// Queues offline player saves to be processed in the background
        /// </summary>
        public void QueueOfflinePlayerSaves(Action<bool> callback = null)
        {
            // Use a stable key so multiple enqueues collapse to the most recent
            var workItemKey = "OfflinePlayerSaves";
            _uniqueQueue.Enqueue(new WorkItem(() =>
            {
                var success = false;
                var startTime = DateTime.UtcNow;
                try
                {
                    // Import the PlayerManager to avoid circular dependencies
                    var playerManagerType = Type.GetType("ACE.Server.Managers.PlayerManager, ACE.Server");
                    if (playerManagerType == null)
                    {
                        log.Warn("[DATABASE] PlayerManager type not found; offline save skipped.");
                    }
                    else
                    {
                        // Call the existing SaveOfflinePlayersWithChanges method directly
                        var saveMethod = playerManagerType.GetMethod(
                            "PerformOfflinePlayerSaves",
                            System.Reflection.BindingFlags.Public
                              | System.Reflection.BindingFlags.Static
                              | System.Reflection.BindingFlags.NonPublic,
                            null,
                            Type.EmptyTypes,
                            null);

                        if (saveMethod == null)
                        {
                            log.Warn("[DATABASE] PerformOfflinePlayerSaves method not found on PlayerManager; offline save skipped.");
                        }
                        else
                        {
                            log.Info("[DATABASE] Calling PlayerManager.PerformOfflinePlayerSaves() via reflection");
                            saveMethod.Invoke(null, null);
                            success = true;
                            log.Info("[DATABASE] Successfully called PerformOfflinePlayerSaves");
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error("[DATABASE] Offline player save work item failed", ex);
                }
                finally
                {
                    var elapsed = DateTime.UtcNow - startTime;
                    if (elapsed.TotalMilliseconds >= 5000)
                    {
                        log.Warn($"[DATABASE] OfflinePlayerSaves work item took {elapsed.TotalMilliseconds:N0}ms to complete");
                    }
                    try
                    {
                        callback?.Invoke(success);
                    }
                    catch (Exception ex)
                    {
                        log.Error($"[DATABASE] QueueOfflinePlayerSaves callback threw exception: {ex}", ex);
                    }
                }
            }, workItemKey));
        }


    }
}
