using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using log4net;

namespace ACE.Database
{
    /// <summary>
    /// Interface for save jobs that can be executed by SaveScheduler.
    /// Reduces delegate allocations by allowing job instances to be reused or resolved at execution time.
    /// 
    /// CRITICAL IMPLEMENTATION REQUIREMENT:
    /// Save jobs must read "live" state (e.g., dirty flags, current values) at execution time in Execute(),
    /// NOT capture them in the constructor. This is essential for proper coalescing semantics.
    /// 
    /// Rules for ISaveJob implementations:
    /// 1. A save job MUST check dirty flags at execution time (in Execute()), not in the constructor
    /// 2. Dirty flags must only be cleared after persistence success
    /// 3. If a job captures a dirty snapshot in its constructor, coalescing can suppress needed saves
    /// 
    /// Example of CORRECT pattern:
    ///   public bool Execute()
    ///   {
    ///       // Read live state at execution time
    ///       if (!_object.IsDirty) return true; // Already clean, skip save
    ///       var success = SaveToDatabase(_object);
    ///       if (success) _object.ClearDirtyFlag(); // Only clear after success
    ///       return success;
    ///   }
    /// 
    /// Example of INCORRECT pattern (DO NOT DO THIS):
    ///   public MySaveJob(MyObject obj)
    ///   {
    ///       _wasDirty = obj.IsDirty; // BAD: Captures snapshot in constructor
    ///       _object = obj;
    ///   }
    ///   public bool Execute() { ... } // Too late, already captured stale state
    /// </summary>
    public interface ISaveJob
    {
        /// <summary>
        /// Executes the save operation.
        /// Must read live state (dirty flags, current values) at this point, not from constructor.
        /// Returns true if the save was successfully enqueued or completed.
        /// </summary>
        bool Execute();
    }

    /// <summary>
    /// Interface for save jobs that support forced dirty marking for aging saves.
    /// Used by ForcedPeriodicSaveJob to signal that dirty marking should occur.
    /// This keeps SaveScheduler generic - it doesn't know how to mark objects dirty.
    /// </summary>
    public interface IForcedPeriodicSaveJob : ISaveJob
    {
        /// <summary>
        /// Forces the object to be marked as dirty before save execution.
        /// Called by ForcedPeriodicSaveJob when aging requires a save even if the object thinks it's clean.
        /// </summary>
        void ForceDirty();
    }

    /// <summary>
    /// Wrapper to convert Func&lt;bool&gt; delegates to ISaveJob for backward compatibility.
    /// This allows gradual migration while reducing allocations for new code paths.
    /// </summary>
    public sealed class DelegateSaveJob : ISaveJob
    {
        private readonly Func<bool> _work;

        public DelegateSaveJob(Func<bool> work)
        {
            _work = work ?? throw new ArgumentNullException(nameof(work));
        }

        public bool Execute() => _work();
    }

    /// <summary>
    /// SaveScheduler
    /// Responsible for executing persistence operations asynchronously
    /// and coalescing save requests by key.
    ///
    /// This intentionally decouples database IO from the ActionQueue
    /// to prevent gameplay starvation during high save volume scenarios.
    /// </summary>
    public sealed class SaveScheduler
    {
        public enum SaveType
        {
            Atomic,
            Critical,
            Periodic
        }

        private static readonly Lazy<SaveScheduler> _instance =
            new Lazy<SaveScheduler>(() => new SaveScheduler());

        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static SaveScheduler Instance => _instance.Value;

        /// <summary>
        /// Hook for routing actions back to the world thread from DB callbacks.
        /// Set by WorldManager.Initialize() to ensure thread-safe execution of callbacks
        /// that mutate shared state (e.g., PlayerManager dictionaries).
        /// </summary>
        public static Action<Action> EnqueueToWorldThread { get; set; }

        private readonly ConcurrentDictionary<string, SaveState> _states =
            new ConcurrentDictionary<string, SaveState>();

        private readonly BlockingCollection<string> _atomicQueue =
            new BlockingCollection<string>();

        private readonly BlockingCollection<string> _criticalQueue =
            new BlockingCollection<string>();

        private readonly BlockingCollection<string> _periodicQueue =
            new BlockingCollection<string>();

        private readonly List<Thread> _workers = new List<Thread>();
        private Thread _watchdogThread;

        private volatile int _shutdownRequested; // 0 = false, 1 = true

        private const int DefaultWorkerCount = 3;

        /// <summary>
        /// Returns true if shutdown has been requested.
        /// </summary>
        public bool IsShutdownRequested => _shutdownRequested != 0;

        /// <summary>
        /// Sets the stuck save detection threshold.
        /// Call this from server initialization to configure from ServerConfig.
        /// </summary>
        public void SetStuckThreshold(TimeSpan threshold)
        {
            _stuckThreshold = threshold;
        }

        // Player save tracking for periodic saves
        private readonly ConcurrentDictionary<uint, PlayerSaveState> _players =
            new ConcurrentDictionary<uint, PlayerSaveState>();

        private readonly ConcurrentQueue<uint> _roundRobinQueue =
            new ConcurrentQueue<uint>();

        private readonly ConcurrentQueue<uint> _agingQueue =
            new ConcurrentQueue<uint>();

        // Per-character save tracking for login blocking
        // Tracks pending and active saves per character to provide authoritative answers
        private readonly ConcurrentDictionary<uint, CharacterSaveState> _characterSaves =
            new ConcurrentDictionary<uint, CharacterSaveState>();

        private const int SaveWindowSeconds = 180;
        private const int TickIntervalSeconds = 3;
        private const int MaxSavesPerTickCap = 16;

        // Configurable stuck save threshold (default: 30 seconds)
        // Can be set externally via SetStuckThreshold() to read from ServerConfig
        private TimeSpan _stuckThreshold = TimeSpan.FromSeconds(30);

        // Metrics counters
        private long _enqueued;
        private long _executedAtomic;
        private long _executedCritical;
        private long _executedPeriodic;
        private long _failedAtomic;
        private long _failedCritical;
        private long _failedPeriodic;
        private long _requeuedDirty;      // Requeued due to dirty flag (new work arrived during execution)
        private long _reroutedPriority;   // Rerouted due to priority upgrade (moved between queues)

        // Average work time tracking (total milliseconds and count per priority)
        private long _totalWorkMsAtomic;
        private long _totalWorkMsCritical;
        private long _totalWorkMsPeriodic;
        private long _workCountAtomic;
        private long _workCountCritical;
        private long _workCountPeriodic;

        private SaveScheduler()
        {
            StartWorkers(DefaultWorkerCount);
        }

        private void StartWorkers(int count)
        {
            // Start one worker per queue type for better priority handling
            var atomicThread = new Thread(() => WorkerLoop(_atomicQueue, 0))
            {
                IsBackground = true,
                Name = "SaveScheduler-Atomic"
            };
            _workers.Add(atomicThread);
            atomicThread.Start();

            var criticalThread = new Thread(() => WorkerLoop(_criticalQueue, 1))
            {
                IsBackground = true,
                Name = "SaveScheduler-Critical"
            };
            _workers.Add(criticalThread);
            criticalThread.Start();

            // Start remaining workers for periodic queue (or round-robin across all queues)
            for (int i = 2; i < count; i++)
            {
                var thread = new Thread(() => WorkerLoop(_periodicQueue, 2))
                {
                    IsBackground = true,
                    Name = $"SaveScheduler-Periodic-{i - 2}"
                };
                _workers.Add(thread);
                thread.Start();
            }

            log.Info($"[SAVESCHEDULER] Started {count} worker threads (1 atomic, 1 critical, {count - 2} periodic)");

            // Start watchdog thread to detect stuck saves
            _watchdogThread = new Thread(WatchdogLoop)
            {
                IsBackground = true,
                Name = "SaveScheduler-Watchdog"
            };
            _watchdogThread.Start();
        }

        /// <summary>
        /// Requests a save operation to be executed asynchronously.
        /// Prevents duplicate keys from existing in multiple queues at once.
        /// 
        /// SaveScheduler is generic and does not know about database implementations.
        /// The saveJob is responsible for calling the appropriate database methods.
        /// 
        /// Returns false if shutdown has been requested (caller should treat as "skip because shutting down", not "failed save").
        /// Returns true if the save was successfully enqueued or updated.
        /// </summary>
        public bool RequestSave(string key, SaveType type, ISaveJob saveJob)
        {
            return RequestSaveInternal(key, type, saveJob);
        }

        /// <summary>
        /// Overload that accepts Func&lt;bool&gt; for backward compatibility.
        /// Wraps the delegate in a DelegateSaveJob to reduce allocations in common cases.
        /// </summary>
        public bool RequestSave(string key, SaveType type, Func<bool> saveWork)
        {
            if (saveWork == null)
                return false;
            
            // For backward compatibility, wrap the delegate
            // Note: This still creates a wrapper object, but allows gradual migration
            return RequestSaveInternal(key, type, new DelegateSaveJob(saveWork));
        }

        /// <summary>
        /// Internal implementation that handles the actual save request logic.
        /// </summary>
        private bool RequestSaveInternal(string key, SaveType type, ISaveJob saveJob)
        {
            if (IsShutdownRequested)
                return false;

            if (string.IsNullOrWhiteSpace(key) || saveJob == null)
                return false;

            // Periodic saves are governed by coalescing, player tick pacing, and worker count
            // No token bucket rate limiting - let the natural flow control handle throughput

            var prio = (int)type;

            var state = _states.GetOrAdd(key, _ => new SaveState { Priority = prio, Job = saveJob });

            // Always keep the latest job instance (latest state wins)
            // Job field is volatile, so assignment has proper memory barriers
            // NOTE: The saveJob.Execute() method must read live state (dirty flags, etc.) at execution time,
            // not capture it in the constructor. See ISaveJob documentation for details.
            state.Job = saveJob;

            // Bump generation for this key to track state updates
            Interlocked.Increment(ref state.Generation);

            // Upgrade priority if needed (lower number = higher priority)
            int old;
            do
            {
                old = state.Priority;
                if (prio >= old) break; // New priority is not higher, no upgrade needed
            }
            while (Interlocked.CompareExchange(ref state.Priority, prio, old) != old);

            // Enqueue only once per key
            // Use CompareExchange on Queued to atomically claim the slot (set to 1 only if it was 0)
            // Then enqueue, and only keep Queued=1 if enqueue succeeds
            // This strictly maintains the invariant "Queued means present in exactly one queue"
            if (Interlocked.CompareExchange(ref state.Queued, 1, 0) == 0)
            {
                var enqueuedOk = EnqueueByPriority(key, state.Priority);
                if (enqueuedOk)
                {
                    // Enqueue succeeded, keep Queued=1
                    var now = DateTime.UtcNow;
                    state.FirstEnqueuedAt = now;
                    state.EnqueuedAt = now;
                    // Set FirstEverEnqueuedAt only if not already set (hot key detector)
                    if (state.FirstEverEnqueuedAt == null)
                        state.FirstEverEnqueuedAt = now;

                    Interlocked.Increment(ref _enqueued);

                    var characterId = ExtractCharacterIdFromKey(key);
                    if (characterId.HasValue)
                    {
                        var charState = GetOrCreateCharacterSaveState(characterId.Value);
                        charState.IncrementPending();
                    }
                }
                else
                {
                    // Enqueue failed, roll back Queued flag
                    Interlocked.Exchange(ref state.Queued, 0);
                    
                    // Optionally remove state if fully idle
                    // Safe because enqueue failed, so there is no queue entry to process
                    if (Volatile.Read(ref state.Executing) == 0 &&
                        Volatile.Read(ref state.Queued) == 0 &&
                        Volatile.Read(ref state.Dirty) == 0)
                    {
                        _states.TryRemove(key, out _);
                    }

                    return false;
                }
            }
            else
            {
                // Key is already queued or another thread is enqueueing
                // Check if already queued or executing, and set Dirty if executing
                var queued = Volatile.Read(ref state.Queued);
                var executing = Volatile.Read(ref state.Executing);
                
                if (executing == 1)
                {
                    // Currently executing, set Dirty to trigger requeue in finally block
                    Interlocked.Exchange(ref state.Dirty, 1);
                }
                // If Queued=1 but Executing=0, the key is already queued and will be processed
                // The Generation check in the finally block will catch any new work
            }

            return true;
        }

        /// <summary>
        /// Enqueues a key to the appropriate queue based on priority.
        /// Returns true if successfully enqueued, false if enqueue failed (shutdown, queue completed, etc.).
        /// </summary>
        private bool EnqueueByPriority(string key, int priority)
        {
            if (IsShutdownRequested)
                return false;

            BlockingCollection<string> queue =
                (priority == 0) ? _atomicQueue :
                (priority == 1) ? _criticalQueue :
                _periodicQueue;

            if (IsShutdownRequested || queue.IsAddingCompleted)
                return false;

            try
            {
                queue.Add(key);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private void WorkerLoop(BlockingCollection<string> myQueue, int myPriority)
        {
            foreach (var key in myQueue.GetConsumingEnumerable())
            {
                if (!_states.TryGetValue(key, out var state))
                    continue;

                // CRITICAL: Set Executing=1 immediately after dequeue to close race window
                // Between dequeue and setting Executing, RequestSave can arrive and see Queued=1, Executing=0
                // If we don't set Executing here, RequestSave won't set Dirty, and new work might be missed
                // If another thread is already executing, skip this dequeue
                if (Interlocked.Exchange(ref state.Executing, 1) == 1)
                {
                    // Already executing on another thread - skip this dequeue
                    // The executing thread will handle requeue via dirty flag if needed
                    continue;
                }

                // Check priority after claiming Executing
                // If priority was upgraded since enqueue, clear Executing and requeue to correct queue
                var currentPriority = Volatile.Read(ref state.Priority);
                if (currentPriority < myPriority)
                {
                    // Clear Executing before requeueing since we're not executing at this priority
                    Volatile.Write(ref state.Executing, 0);
                    
                    // Clear Queued flag before requeueing to maintain invariant:
                    // Queued means "present in exactly one queue"
                    // Without this, the higher priority worker may never claim it because
                    // Queued already indicates it's in a queue, or future upgrades behave incorrectly
                    Interlocked.Exchange(ref state.Queued, 0);
                    var ok = EnqueueByPriority(key, currentPriority);
                    if (ok)
                    {
                        Interlocked.Exchange(ref state.Queued, 1);
                        var now = DateTime.UtcNow;
                        state.EnqueuedAt = now;
                        // Reset FirstEnqueuedAt for upgraded requeues the same way as dirty requeues
                        // because the work is not stuck, it is being rerouted
                        // Keep FirstEverEnqueuedAt unchanged (hot key detector)
                        state.FirstEnqueuedAt = now;
                        Interlocked.Increment(ref _reroutedPriority);
                        continue;
                    }
                    else
                    {
                        log.Error($"[SAVESCHEDULER] Failed to requeue upgraded key={key}, restoring queued flag");
                        Interlocked.Exchange(ref state.Queued, 1);
                        // Clear Executing since requeue failed - RequestSave will retry when it detects the state
                        Volatile.Write(ref state.Executing, 0);
                        continue;
                    }
                }

                // Track character saves: save is starting execution
                var characterId = ExtractCharacterIdFromKey(key);
                CharacterSaveState charState = null;
                if (characterId.HasValue)
                {
                    charState = GetOrCreateCharacterSaveState(characterId.Value);
                    charState.StartExecution(); // pending--, active++
                }

                // Capture the generation we are processing to detect if state was updated during execution
                var processingGen = Volatile.Read(ref state.Generation);

                try
                {
                    // Capture job reference to avoid race where Job changes between null check and Execute call
                    var job = Volatile.Read(ref state.Job);
                    var startTime = DateTime.UtcNow;
                    var result = job?.Execute() ?? false;
                    var duration = DateTime.UtcNow - startTime;
                    var durationMs = (long)duration.TotalMilliseconds;

                    // Update metrics based on priority
                    if (currentPriority == 0) // Atomic
                    {
                        Interlocked.Increment(ref _executedAtomic);
                        Interlocked.Add(ref _totalWorkMsAtomic, durationMs);
                        Interlocked.Increment(ref _workCountAtomic);
                        if (!result)
                            Interlocked.Increment(ref _failedAtomic);
                    }
                    else if (currentPriority == 1) // Critical
                    {
                        Interlocked.Increment(ref _executedCritical);
                        Interlocked.Add(ref _totalWorkMsCritical, durationMs);
                        Interlocked.Increment(ref _workCountCritical);
                        if (!result)
                            Interlocked.Increment(ref _failedCritical);
                    }
                    else // Periodic
                    {
                        Interlocked.Increment(ref _executedPeriodic);
                        Interlocked.Add(ref _totalWorkMsPeriodic, durationMs);
                        Interlocked.Increment(ref _workCountPeriodic);
                        if (!result)
                            Interlocked.Increment(ref _failedPeriodic);
                    }

                    if (duration.TotalMilliseconds >= 1000)
                    {
                        log.Warn($"[SAVESCHEDULER] Save operation '{key}' took {duration.TotalMilliseconds:N0}ms");
                    }

                    // Log completion at debug level - SaveScheduler doesn't interpret what false means
                    // The save logic itself decides what failure means
                    if (log.IsDebugEnabled)
                    {
                        log.Debug($"[SAVESCHEDULER] Save '{key}' completed (result={result}, duration={duration.TotalMilliseconds:N0}ms)");
                    }

                    // Record the generation we processed
                    Volatile.Write(ref state.LastProcessedGen, processingGen);
                }
                catch (Exception ex)
                {
                    // Count exception as failure based on priority
                    if (currentPriority == 0)
                        Interlocked.Increment(ref _failedAtomic);
                    else if (currentPriority == 1)
                        Interlocked.Increment(ref _failedCritical);
                    else
                        Interlocked.Increment(ref _failedPeriodic);

                    log.Error($"[SAVESCHEDULER] Save operation '{key}' threw exception", ex);
                }
                finally
                {
                    // CRITICAL ORDERING: Check for dirty work BEFORE draining callbacks
                    // This ensures CharacterSaveState reflects all pending work (including requeues)
                    // before we decide if callbacks should fire. Callbacks must only fire when
                    // it's impossible for more work to exist.

                    // Clear executing flag first (natural lifecycle ordering)
                    Volatile.Write(ref state.Executing, 0);

                    // Clear Queued flag for this execution (always clear, regardless of current value)
                    // This ensures we don't get stuck in an infinite requeue loop
                    // Note: The "poke dirty" logic in RequestSaveInternal handles the race where
                    // RequestSave arrives between clearing Executing and Queued (sees Queued=1, Executing=0)
                    Interlocked.Exchange(ref state.Queued, 0);

                    // Check if dirty flag was set (new request arrived while executing)
                    // Dirty is only set when Executing == 1, preventing double requeue from Queued + Dirty
                    var wasDirty = Interlocked.Exchange(ref state.Dirty, 0) == 1;

                    // Check if new work arrived during execution
                    // wasDirty indicates a new request arrived while Executing == 1
                    // Generation check catches any new requests that incremented Generation
                    var currentGen = Volatile.Read(ref state.Generation);
                    var hasNewWork = wasDirty || (currentGen != processingGen);

                    // If new work arrived, reflect it in CharacterSaveState BEFORE finishing execution
                    // This ensures pendingCount is incremented before we check if callbacks should drain
                    if (hasNewWork && characterId.HasValue && charState != null)
                    {
                        charState.Requeue(); // pending++ - reflects the requeue in CharacterSaveState
                    }

                    // Now finish execution and check if callbacks should drain
                    // At this point, CharacterSaveState accurately reflects all pending work
                    if (charState != null)
                    {
                        charState.FinishExecution(); // Decrements _activeCount
                        var callbacksToInvoke = charState.CheckAndDrainCallbacks();
                        if (callbacksToInvoke != null)
                        {
                            // All saves complete, invoke callbacks on world thread for thread safety
                            if (EnqueueToWorldThread != null)
                            {
                                EnqueueToWorldThread(() =>
                                {
                                    foreach (var callback in callbacksToInvoke)
                                    {
                                        try
                                        {
                                            callback();
                                        }
                                        catch (Exception ex)
                                        {
                                            log.Error($"[SAVESCHEDULER] Callback invocation failed for character {charState.CharacterId}", ex);
                                        }
                                    }
                                }
                                );
                            }
                            else
                            {
                                // CRITICAL ERROR: EnqueueToWorldThread should always be set by WorldManager.Initialize()
                                // This fallback is unsafe and should never occur in production
                                log.Error("[SAVESCHEDULER] CRITICAL: EnqueueToWorldThread not set! Invoking callbacks directly (unsafe - may cause thread safety issues). This indicates WorldManager.Initialize() was not called or failed.");
                                foreach (var callback in callbacksToInvoke)
                                {
                                    try
                                    {
                                        callback();
                                    }
                                    catch (Exception ex)
                                    {
                                        log.Error($"[SAVESCHEDULER] Callback invocation failed for character {charState.CharacterId}", ex);
                                    }
                                }
                            }

                            // All saves complete and callbacks drained - try to clean up idle CharacterSaveState
                            if (characterId.HasValue)
                            {
                                TryRemoveIdleCharacterSaveState(characterId.Value);
                            }
                        }
                    }

                    // Handle actual queue re-enqueue (after CharacterSaveState is updated)
                    // Requeue only if new work arrived during execution
                    // If new work arrives after we clear Executing, RequestSaveInternal will handle it
                    if (hasNewWork)
                    {
                        // Enqueue first, then set Queued flag only on success
                        // This maintains the invariant "Queued means present in exactly one queue"
                        var ok = EnqueueByPriority(key, state.Priority);
                        if (ok)
                        {
                            Interlocked.Exchange(ref state.Queued, 1);
                            var now = DateTime.UtcNow;
                            state.EnqueuedAt = now; // Update enqueue time for stuck detection
                            // Reset FirstEnqueuedAt on requeue so "stuck" means actually stuck, not "busy but healthy"
                            // This prevents frequently dirtied keys from looking "stuck" forever
                            // Keep FirstEverEnqueuedAt unchanged (hot key detector)
                            state.FirstEnqueuedAt = now;
                            if (wasDirty)
                                Interlocked.Increment(ref _requeuedDirty);
                        }
                        else
                        {
                            log.Error($"[SAVESCHEDULER] Failed to enqueue requeued key={key}, clearing queued flag");
                            Interlocked.Exchange(ref state.Queued, 0);
                            // If we already incremented pending via Requeue(), we need to decrement it
                            if (characterId.HasValue && charState != null)
                            {
                                charState.DecrementPending();
                            }
                        }
                    }
                    else
                    {
                        // Remove only if no new generation arrived after we started and state is completely idle
                        // Must check Dirty to ensure we don't remove state when a new request arrived during execution
                        // Re-read Generation to ensure we have the latest value (state could have changed)
                        var latestGen = Volatile.Read(ref state.Generation);
                        var currentExecuting = Volatile.Read(ref state.Executing);
                        var currentQueued = Volatile.Read(ref state.Queued);
                        var currentDirty = Volatile.Read(ref state.Dirty);
                        
                        if (latestGen == processingGen && currentExecuting == 0 && currentQueued == 0 && currentDirty == 0)
                        {
                            _states.TryRemove(key, out _);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Stops the scheduler gracefully, allowing current work to complete.
        /// This method is idempotent and can be called multiple times safely.
        /// </summary>
        public void Stop()
        {
            // Set shutdown flag atomically - if already set, return early (idempotent)
            if (Interlocked.Exchange(ref _shutdownRequested, 1) != 0)
                return;

            log.Info("[SAVESCHEDULER] Stopping scheduler...");

            // Complete adding to all queues (allows GetConsumingEnumerable to exit)
            _atomicQueue.CompleteAdding();
            _criticalQueue.CompleteAdding();
            _periodicQueue.CompleteAdding();

            // Wait for worker threads to finish processing current items
            foreach (var thread in _workers)
            {
                thread.Join();
            }

            // Wait for watchdog thread to finish
            _watchdogThread?.Join();

            // Now safe to dispose collections
            _atomicQueue.Dispose();
            _criticalQueue.Dispose();
            _periodicQueue.Dispose();

            log.Info($"[SAVESCHEDULER] Stopped. Remaining states={_states.Count} Remaining queued={QueueCount}");
        }

        /// <summary>
        /// Gets the number of save states currently tracked (may include completed states awaiting cleanup).
        /// </summary>
        public int StateCount => _states.Count;

        /// <summary>
        /// Gets the total number of queued save requests across all queues.
        /// </summary>
        public int QueueCount => _atomicQueue.Count + _criticalQueue.Count + _periodicQueue.Count;

        /// <summary>
        /// Gets the number of save requests currently queued and pending execution.
        /// </summary>
        public int PendingQueuedCount => QueueCount;

        /// <summary>
        /// Registers a player for periodic save tracking.
        /// Call this when a player logs in.
        /// </summary>
        public void RegisterPlayer(uint playerId)
        {
            var added = false;

            _players.GetOrAdd(playerId, id =>
            {
                added = true;
                return new PlayerSaveState
                {
                    PlayerId = id,
                    LastSavedUtc = DateTime.UtcNow
                };
            });

            if (added)
                _roundRobinQueue.Enqueue(playerId);
        }

        /// <summary>
        /// Unregisters a player from periodic save tracking.
        /// Call this when a player logs out.
        /// </summary>
        public void UnregisterPlayer(uint playerId)
        {
            _players.TryRemove(playerId, out _);
        }

        /// <summary>
        /// Tick-driven scheduler entry point for periodic player saves.
        /// Call this once per world tick (every 3 seconds).
        /// </summary>
        /// <param name="saveFactory">Factory function that takes playerId and returns an ISaveJob</param>
        public void TickPlayerSaves(Func<uint, ISaveJob> saveFactory)
        {
            int playerCount = _players.Count;
            if (playerCount == 0)
                return;

            int ticksPerWindow = SaveWindowSeconds / TickIntervalSeconds;
            int basePerTick = (int)Math.Ceiling((double)playerCount / ticksPerWindow);

            int minPerTick = Math.Max(1, basePerTick);
            int maxPerTick = Math.Min(basePerTick * 2, MaxSavesPerTickCap);

            int issued = 0;
            var now = DateTime.UtcNow;

            // Step 1: aging enforcement
            foreach (var kvp in _players)
            {
                var state = kvp.Value;
                if ((now - state.LastSavedUtc).TotalSeconds >= SaveWindowSeconds &&
                    Interlocked.CompareExchange(ref state.Enqueued, 1, 0) == 0)
                {
                    _agingQueue.Enqueue(state.PlayerId);
                }
            }

            // Step 2: drain aging queue
            // Aging saves should force dirty marking to ensure they execute even if flags were cleared
            while (issued < maxPerTick && _agingQueue.TryDequeue(out var agedId))
            {
                if (!_players.TryGetValue(agedId, out var state))
                    continue;

                EnqueuePlayerSave(state, saveFactory, forceDirty: true);
                issued++;
            }

            // Step 3: round robin fairness
            // Round robin saves are normal periodic saves - don't force dirty
            while (issued < minPerTick && _roundRobinQueue.TryDequeue(out var rrId))
            {
                if (!_players.TryGetValue(rrId, out var state))
                    continue;

                EnqueuePlayerSave(state, saveFactory, forceDirty: false);
                _roundRobinQueue.Enqueue(rrId);
                issued++;
            }
        }

        /// <summary>
        /// Enqueues a player save using the existing RequestSave logic.
        /// Wraps the save job with ForcedPeriodicSaveJob to ensure aging saves always execute,
        /// even if dirty flags were cleared prematurely or changes were masked.
        /// </summary>
        /// <param name="state">Player save state</param>
        /// <param name="saveFactory">Factory function that creates the save job</param>
        /// <param name="forceDirty">True if this save was triggered by the aging queue (should force dirty), false for round robin saves</param>
        private void EnqueuePlayerSave(PlayerSaveState state, Func<uint, ISaveJob> saveFactory, bool forceDirty)
        {
            var job = saveFactory(state.PlayerId);
            
            // Wrap with forced save job to ensure aging saves bypass "I'm clean" logic
            // This prevents periodic saves from becoming no-ops when:
            // - Dirty flag was cleared prematurely
            // - SaveInProgress suppressed earlier
            // - Mutation logic masked changes
            // - Player inventory mutated without a subsequent flag
            // 
            // ARCHITECTURAL: SaveScheduler does not know about players - the save job handles dirty marking
            // forceDirty is determined by which queue triggered the save (aging vs round robin), not time math
            // This makes forced dirty marking deterministic and avoids timing edge cases
            var forcedJob = new ForcedPeriodicSaveJob(job, forceDirty);
            
            RequestSave(SaveKeys.Player(state.PlayerId), SaveType.Periodic, new PlayerSaveJobWrapper(forcedJob, state));
        }

        /// <summary>
        /// Wrapper that forces a save to execute even if the object believes it is clean.
        /// Used for aging/periodic saves to prevent silent no-ops when dirty flags are cleared prematurely
        /// or changes are masked by mutation logic.
        /// 
        /// ARCHITECTURAL: SaveScheduler is generic and does not know about players.
        /// The inner save job (created in ACE.Server) handles the actual dirty marking logic.
        /// This wrapper just signals that dirty marking should occur.
        /// </summary>
        private sealed class ForcedPeriodicSaveJob : ISaveJob
        {
            private readonly ISaveJob _inner;
            private readonly bool _forceDirty;

            public ForcedPeriodicSaveJob(ISaveJob inner, bool forceDirty)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                _forceDirty = forceDirty;
            }

            public bool Execute()
            {
                // Signal to inner job that it should force dirty if needed
                // The inner job (created in ACE.Server) knows how to mark players dirty
                // SaveScheduler does not know about PlayerManager or players
                if (_forceDirty && _inner is IForcedPeriodicSaveJob forcedJob)
                {
                    forcedJob.ForceDirty();
                }

                return _inner.Execute();
            }
        }

        /// <summary>
        /// Wrapper that updates player save state after execution.
        /// </summary>
        private sealed class PlayerSaveJobWrapper : ISaveJob
        {
            private readonly ISaveJob _innerJob;
            private readonly PlayerSaveState _playerState;

            public PlayerSaveJobWrapper(ISaveJob innerJob, PlayerSaveState playerState)
            {
                _innerJob = innerJob;
                _playerState = playerState;
            }

            public bool Execute()
            {
                var result = _innerJob?.Execute() ?? false;
                // Only update LastSavedUtc if save succeeded - otherwise failed saves can delay retries
                if (result)
                    _playerState.LastSavedUtc = DateTime.UtcNow;
                Interlocked.Exchange(ref _playerState.Enqueued, 0);
                return result;
            }
        }

        private sealed class SaveState
        {
            public volatile int Priority;   // 0 = atomic, 1 = critical, 2 = periodic
            public volatile int Queued;     // 0 = no, 1 = yes (only set after successful enqueue - maintains invariant "Queued means present in exactly one queue")
            public volatile int Executing;  // 0 = no, 1 = yes
            public volatile int Dirty;      // 0 = clean, 1 = dirty (new request arrived while in flight)
            public volatile ISaveJob Job;   // Save job to execute (reduces delegate allocations) - volatile for thread-safe updates
            public DateTime? FirstEnqueuedAt;     // When this state was last enqueued (progress detector - reset on requeue) - accessed via Volatile.Read/Write
            public DateTime? FirstEverEnqueuedAt;   // When this state was first ever enqueued (hot key detector - never reset) - accessed via Volatile.Read/Write
            public DateTime? EnqueuedAt;           // When this state was last enqueued (updated on requeue) - accessed via Volatile.Read/Write
            public DateTime? LastStuckLogAt; // When we last logged a stuck warning (for rate limiting) - accessed via Volatile.Read/Write
            public int Generation;          // Increments per RequestSave to track state updates
            public int LastProcessedGen;    // Last generation a worker finished processing
        }

        private sealed class PlayerSaveState
        {
            public uint PlayerId;
            public DateTime LastSavedUtc;
            public int Enqueued; // 0 or 1
        }

        /// <summary>
        /// Tracks pending and active saves for a character.
        /// Used to provide authoritative answers about save state for login blocking.
        /// All operations that touch PendingCount, ActiveCount, or callbacks must occur under the same lock
        /// to guarantee atomicity and prevent race conditions.
        /// </summary>
        private sealed class CharacterSaveState
        {
            public uint CharacterId;
            private int _pendingCount;  // Number of saves queued but not yet executing
            private int _activeCount;   // Number of saves currently executing
            private DateTime _lastActivityUtc;
            private readonly object _lock = new object();
            private readonly List<Action> _callbacks = new List<Action>();

            public int PendingCount
            {
                get { lock (_lock) { return _pendingCount; } }
            }

            public int ActiveCount
            {
                get { lock (_lock) { return _activeCount; } }
            }

            public DateTime LastActivityUtc
            {
                get { lock (_lock) { return _lastActivityUtc; } }
            }

            public void TouchActivity()
            {
                lock (_lock) { _lastActivityUtc = DateTime.UtcNow; }
            }

            /// <summary>
            /// Atomically checks if there are any pending or active saves.
            /// Returns true if either count is greater than zero.
            /// This method ensures the check is atomic, preventing TOCTOU race conditions.
            /// </summary>
            public bool HasPendingOrActive()
            {
                lock (_lock)
                {
                    return _pendingCount > 0 || _activeCount > 0;
                }
            }

            /// <summary>
            /// Increments pending count. Must be called when a save is enqueued.
            /// </summary>
            public void IncrementPending()
            {
                lock (_lock)
                {
                    _pendingCount++;
                    _lastActivityUtc = DateTime.UtcNow;
                }
            }

            /// <summary>
            /// Decrements pending count and increments active count. Must be called when a save starts executing.
            /// Includes defensive clamping to prevent negative counts.
            /// </summary>
            public void StartExecution()
            {
                lock (_lock)
                {
                    if (_pendingCount > 0)
                        _pendingCount--;
                    else
                    {
                        // Defensive: this should not happen, but do not go negative
                        log.Warn($"[SAVESCHEDULER] Character {CharacterId} StartExecution with pending=0, clamping");
                    }

                    _activeCount++;
                    _lastActivityUtc = DateTime.UtcNow;
                }
            }

            /// <summary>
            /// Decrements active count. Must be called when a save finishes executing.
            /// Includes defensive clamping to prevent negative counts.
            /// </summary>
            public void FinishExecution()
            {
                lock (_lock)
                {
                    if (_activeCount > 0)
                        _activeCount--;
                    else
                    {
                        log.Warn($"[SAVESCHEDULER] Character {CharacterId} FinishExecution with active=0, clamping");
                    }

                    _lastActivityUtc = DateTime.UtcNow;
                }
            }

            /// <summary>
            /// Increments pending count for requeue. Must be called when a save is requeued.
            /// </summary>
            public void Requeue()
            {
                lock (_lock)
                {
                    _pendingCount++;
                    _lastActivityUtc = DateTime.UtcNow;
                }
            }

            /// <summary>
            /// Decrements pending count. Used for rollback when enqueue fails after Requeue().
            /// Includes defensive clamping to prevent negative counts.
            /// </summary>
            public void DecrementPending()
            {
                lock (_lock)
                {
                    if (_pendingCount > 0)
                        _pendingCount--;
                    else
                        log.Warn($"[SAVESCHEDULER] Character {CharacterId} DecrementPending with pending=0, clamping");
                    _lastActivityUtc = DateTime.UtcNow;
                }
            }

            /// <summary>
            /// Adds a callback to be invoked when all saves are complete.
            /// If saves are already complete (both counts zero), returns true to indicate immediate invocation.
            /// Otherwise returns false and registers the callback for later.
            /// </summary>
            public bool AddCallbackIfNotComplete(Action callback)
            {
                if (callback == null) return false;
                lock (_lock)
                {
                    _lastActivityUtc = DateTime.UtcNow;
                    if (_pendingCount == 0 && _activeCount == 0)
                    {
                        // Already complete, signal immediate invocation
                        return true;
                    }
                    // Not complete, register callback
                    _callbacks.Add(callback);
                    return false;
                }
            }

            /// <summary>
            /// Drains and returns all registered callbacks. Must be called under lock.
            /// </summary>
            private List<Action> DrainCallbacksLocked()
            {
                if (_callbacks.Count == 0)
                    return null;
                var toInvoke = new List<Action>(_callbacks);
                _callbacks.Clear();
                return toInvoke;
            }

            /// <summary>
            /// Checks if both counts are zero and drains callbacks if so.
            /// Returns list of callbacks to invoke, or null if not ready.
            /// </summary>
            public List<Action> CheckAndDrainCallbacks()
            {
                lock (_lock)
                {
                    _lastActivityUtc = DateTime.UtcNow;
                    if (_pendingCount == 0 && _activeCount == 0)
                        return DrainCallbacksLocked();
                    return null;
                }
            }

            /// <summary>
            /// Force clears all counters and callbacks. Used for self-healing when ghost state is detected.
            /// </summary>
            public void ForceClearIfNoWork()
            {
                lock (_lock)
                {
                    _pendingCount = 0;
                    _activeCount = 0;
                    _lastActivityUtc = DateTime.UtcNow;
                    _callbacks.Clear();
                }
            }
        }

        /// <summary>
        /// Extracts character ID from a save key if it's a character-related save.
        /// Returns null if the key is not character-related.
        /// All character-affecting saves now use the format: character:<id>:<type>[:<suffix>]
        /// </summary>
        private uint? ExtractCharacterIdFromKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            // All character-affecting saves use format: character:<id>:<type>[:<suffix>]
            // Examples:
            //   character:123:player
            //   character:123:character
            //   character:123:item:456
            //   character:123:storage_tx:guid
            //   character:123:vendor_tx:guid
            //   character:123:bank_tx:guid
            //   character:123:logout
            if (key.StartsWith("character:", StringComparison.OrdinalIgnoreCase))
            {
                var afterPrefix = key.Substring("character:".Length);
                var colonIndex = afterPrefix.IndexOf(':');
                if (colonIndex > 0)
                {
                    // Format: character:<id>:<type>[:<suffix>]
                    var idStr = afterPrefix.Substring(0, colonIndex);
                    if (uint.TryParse(idStr, out var characterId))
                        return characterId;
                }
                else
                {
                    // Legacy format: character:<id> (no type suffix)
                    // Try to parse the entire remainder as character ID
                    if (uint.TryParse(afterPrefix, out var characterId))
                        return characterId;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets or creates the CharacterSaveState for a character.
        /// </summary>
        private CharacterSaveState GetOrCreateCharacterSaveState(uint characterId)
        {
            return _characterSaves.GetOrAdd(characterId, id => new CharacterSaveState { CharacterId = id });
        }

        /// <summary>
        /// Checks if there are any pending or active saves for a character.
        /// Returns true if there are saves queued or currently executing.
        /// Includes self-healing logic to clear ghost state if counters are wrong.
        /// </summary>
        public bool HasPendingOrActiveSave(uint characterId)
        {
            if (!_characterSaves.TryGetValue(characterId, out var state))
                return false;

            if (!state.HasPendingOrActive())
                return false;

            // If counters say busy but no real work exists, self heal
            var idleTime = DateTime.UtcNow - state.LastActivityUtc;
            if (idleTime > _stuckThreshold && !CharacterHasRealWork(characterId))
            {
                log.Warn($"[SAVESCHEDULER] Clearing ghost save state for character {characterId}");
                state.ForceClearIfNoWork();
                TryRemoveIdleCharacterSaveState(characterId);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if a character actually has any queued or executing work by scanning _states.
        /// This is the reconciliation check that prevents permanent blocks from ghost state.
        /// Dirty is only meaningful while something is executing or queued, so we only treat it as real work in those cases.
        /// </summary>
        public bool CharacterHasRealWork(uint characterId)
        {
            foreach (var kvp in _states)
            {
                var key = kvp.Key;
                var cid = ExtractCharacterIdFromKey(key);
                if (!cid.HasValue || cid.Value != characterId)
                    continue;

                var s = kvp.Value;
                var executing = Volatile.Read(ref s.Executing);
                var queued = Volatile.Read(ref s.Queued);
                
                // Executing or Queued always indicate real work.
                // Dirty is only meaningful while one of these is set, so we intentionally ignore Dirty
                // unless work is executing or queued to avoid theoretical edge cases.
                if (executing == 1) return true;
                if (queued == 1) return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if a character has ghost save state (pending/active counters but no real work).
        /// Returns true if CharacterSaveState reports pending/active but CharacterHasRealWork returns false.
        /// </summary>
        public bool HasGhostSaveState(uint characterId)
        {
            if (!_characterSaves.TryGetValue(characterId, out var state))
                return false;

            if (!state.HasPendingOrActive())
                return false;

            return !CharacterHasRealWork(characterId);
        }

        /// <summary>
        /// Gets the last activity time for a character's save state.
        /// Returns null if no CharacterSaveState exists for the character.
        /// </summary>
        public DateTime? GetLastActivityUtc(uint characterId)
        {
            if (!_characterSaves.TryGetValue(characterId, out var state))
                return null;

            return state.LastActivityUtc;
        }

        /// <summary>
        /// Attempts to clear ghost save state for a character if safe.
        /// Only succeeds if all conditions are met:
        /// - CharacterSaveState reports pending or active
        /// - CharacterHasRealWork returns false
        /// - No SaveState exists with Executing or Queued for that character
        /// Returns true if cleared, false if conditions not met or already clear.
        /// </summary>
        public bool ClearGhostSaveState(uint characterId)
        {
            if (!_characterSaves.TryGetValue(characterId, out var state))
                return false;

            if (!state.HasPendingOrActive())
                return false;

            if (CharacterHasRealWork(characterId))
                return false;

            // Double-check: verify no SaveState has Executing or Queued for this character
            foreach (var kvp in _states)
            {
                var key = kvp.Key;
                var cid = ExtractCharacterIdFromKey(key);
                if (!cid.HasValue || cid.Value != characterId)
                    continue;

                var s = kvp.Value;
                if (Volatile.Read(ref s.Executing) == 1 || Volatile.Read(ref s.Queued) == 1)
                    return false; // Real work exists, cannot clear
            }

            // All conditions met - safe to clear
            log.Warn($"[SAVESCHEDULER] Admin command clearing ghost save state for character {characterId}");
            state.ForceClearIfNoWork();
            TryRemoveIdleCharacterSaveState(characterId);
            return true;
        }

        /// <summary>
        /// Attempts to remove a CharacterSaveState if it's idle and has no real work.
        /// Safe to call after ForceClearIfNoWork or after callbacks drain.
        /// </summary>
        private void TryRemoveIdleCharacterSaveState(uint characterId)
        {
            if (!_characterSaves.TryGetValue(characterId, out var state))
                return;

            // Only remove if completely idle and no real work exists
            if (!state.HasPendingOrActive() && !CharacterHasRealWork(characterId))
            {
                _characterSaves.TryRemove(characterId, out _);
            }
        }

        /// <summary>
        /// Registers a callback to be invoked when all pending and active saves for a character are complete.
        /// The callback will be invoked on the ActionQueue thread for thread safety.
        /// If there are no pending or active saves, the callback is invoked immediately.
        /// This operation is atomic with respect to save enqueue/start/finish operations.
        /// </summary>
        public void OnSavesDrained(uint characterId, Action callback)
        {
            if (callback == null)
                return;

            var state = GetOrCreateCharacterSaveState(characterId);
            
            // Atomic check-and-register: if complete, invoke immediately; otherwise register
            if (state.AddCallbackIfNotComplete(callback))
            {
                // No saves in flight, invoke immediately on world thread
                if (EnqueueToWorldThread != null)
                {
                    EnqueueToWorldThread(callback);
                }
                else
                {
                    // CRITICAL ERROR: EnqueueToWorldThread should always be set by WorldManager.Initialize()
                    // This fallback is unsafe and should never occur in production
                    log.Error("[SAVESCHEDULER] CRITICAL: EnqueueToWorldThread not set! Invoking callback directly (unsafe - may cause thread safety issues). This indicates WorldManager.Initialize() was not called or failed.");
                    callback();
                }
            }
            // Otherwise callback is registered and will be invoked when saves complete
        }

        /// <summary>
        /// Watchdog loop that periodically scans for stuck saves.
        /// </summary>
        private void WatchdogLoop()
        {
            while (!IsShutdownRequested)
            {
                try
                {
                    var now = DateTime.UtcNow;

                    foreach (var kvp in _states)
                    {
                        var key = kvp.Key;
                        var state = kvp.Value;

                        // Read volatile fields first to establish memory barrier
                        var queued = Volatile.Read(ref state.Queued);
                        var executing = Volatile.Read(ref state.Executing);

                        // Now read FirstEnqueuedAt (after memory barrier ensures visibility)
                        // Use FirstEnqueuedAt to detect truly stuck saves (progress detector - resets on requeue)
                        // This detects saves that are not making progress, not just busy keys
                        var firstEnq = state.FirstEnqueuedAt;
                        if (!firstEnq.HasValue) continue;

                        var age = now - firstEnq.Value;

                        // Only warn if it is still queued or executing and age exceeds threshold
                        if (age >= _stuckThreshold && (queued == 1 || executing == 1))
                        {
                            // Rate limit: only log if we haven't logged recently (every 30 seconds)
                            var lastLog = state.LastStuckLogAt;
                            var timeSinceLastLog = lastLog.HasValue ? (now - lastLog.Value).TotalSeconds : double.MaxValue;
                            
                            if (timeSinceLastLog >= 30.0)
                            {
                                LogStuck(key, state);

                                // Update LastStuckLogAt to rate limit warnings
                                // FirstEnqueuedAt resets on requeue (progress detector)
                                // FirstEverEnqueuedAt never resets (hot key detector - available in LogStuck)
                                // Write happens after volatile reads (memory barrier ensures visibility)
                                state.LastStuckLogAt = now;
                            }
                        }
                    }

                    // Second pass: reconcile ghost character states
                    // This handles cases where PlayerEnterWorld never calls HasPendingOrActiveSave again
                    // but the state should still self heal
                    foreach (var kvp in _characterSaves)
                    {
                        var charId = kvp.Key;
                        var cs = kvp.Value;

                        if (!cs.HasPendingOrActive())
                            continue;

                        if ((DateTime.UtcNow - cs.LastActivityUtc) < _stuckThreshold)
                            continue;

                        if (!CharacterHasRealWork(charId))
                        {
                            log.Warn($"[SAVESCHEDULER] Watchdog clearing ghost character save state for {charId}");
                            cs.ForceClearIfNoWork();
                            TryRemoveIdleCharacterSaveState(charId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error("[SAVESCHEDULER] Watchdog threw exception", ex);
                }

                Thread.Sleep(1000);
            }
        }

        /// <summary>
        /// Logs diagnostic information about a stuck save operation.
        /// </summary>
        private void LogStuck(string key, SaveState state)
        {
            var firstEnq = state.FirstEnqueuedAt;
            var firstEverEnq = state.FirstEverEnqueuedAt;
            var lastEnq = state.EnqueuedAt;
            var firstAge = firstEnq.HasValue ? (DateTime.UtcNow - firstEnq.Value).TotalSeconds : 0;
            var firstEverAge = firstEverEnq.HasValue ? (DateTime.UtcNow - firstEverEnq.Value).TotalSeconds : 0;
            var lastAge = lastEnq.HasValue ? (DateTime.UtcNow - lastEnq.Value).TotalSeconds : 0;
            
            var currentGen = Volatile.Read(ref state.Generation);
            var lastProcessedGen = Volatile.Read(ref state.LastProcessedGen);
            var genLag = currentGen - lastProcessedGen;
            
            // firstEnqueued = progress detector (resets on requeue, shows how long since last progress)
            // firstEverEnqueued = hot key detector (never resets, shows total time key has been active)
            log.Warn($"[SAVESCHEDULER] Stuck save detected key={key} prio={state.Priority} executing={state.Executing} " +
                     $"firstEnqueued={firstAge:F1}s ago (progress) firstEverEnqueued={firstEverAge:F1}s ago (hot key) lastEnqueued={lastAge:F1}s ago " +
                     $"gen={currentGen} lastProcessedGen={lastProcessedGen} genLag={genLag} " +
                     $"atomicQ={_atomicQueue.Count} criticalQ={_criticalQueue.Count} periodicQ={_periodicQueue.Count}");
        }

        /// <summary>
        /// Logs diagnostic metrics including counters and average work times by priority.
        /// </summary>
        public void LogMetrics()
        {
            var enqueued = Interlocked.Read(ref _enqueued);
            var executedAtomic = Interlocked.Read(ref _executedAtomic);
            var executedCritical = Interlocked.Read(ref _executedCritical);
            var executedPeriodic = Interlocked.Read(ref _executedPeriodic);
            var failedAtomic = Interlocked.Read(ref _failedAtomic);
            var failedCritical = Interlocked.Read(ref _failedCritical);
            var failedPeriodic = Interlocked.Read(ref _failedPeriodic);
            var requeuedDirty = Interlocked.Read(ref _requeuedDirty);
            var reroutedPriority = Interlocked.Read(ref _reroutedPriority);

            var totalMsAtomic = Interlocked.Read(ref _totalWorkMsAtomic);
            var totalMsCritical = Interlocked.Read(ref _totalWorkMsCritical);
            var totalMsPeriodic = Interlocked.Read(ref _totalWorkMsPeriodic);
            var countAtomic = Interlocked.Read(ref _workCountAtomic);
            var countCritical = Interlocked.Read(ref _workCountCritical);
            var countPeriodic = Interlocked.Read(ref _workCountPeriodic);

            var avgMsAtomic = countAtomic > 0 ? (double)totalMsAtomic / countAtomic : 0.0;
            var avgMsCritical = countCritical > 0 ? (double)totalMsCritical / countCritical : 0.0;
            var avgMsPeriodic = countPeriodic > 0 ? (double)totalMsPeriodic / countPeriodic : 0.0;

            log.Info($"[SAVESCHEDULER] Metrics - Enqueued: {enqueued}, RequeuedDirty: {requeuedDirty}, ReroutedPriority: {reroutedPriority}, PendingQueued: {PendingQueuedCount} | " +
                     $"Executed: Atomic={executedAtomic} Critical={executedCritical} Periodic={executedPeriodic} | " +
                     $"Failed: Atomic={failedAtomic} Critical={failedCritical} Periodic={failedPeriodic} | " +
                     $"AvgWorkMs: Atomic={avgMsAtomic:F1} Critical={avgMsCritical:F1} Periodic={avgMsPeriodic:F1}");
        }
    }
}
