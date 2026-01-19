using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
    /// public bool Execute()
    /// {
    ///     // Read live state at execution time
    ///     if (!_object.IsDirty) return true; // Already clean, skip save
    ///     var success = SaveToDatabase(_object);
    ///     if (success) _object.ClearDirtyFlag(); // Only clear after success
    ///     return success;
    /// }
    ///
    /// Example of INCORRECT pattern (DO NOT DO THIS):
    /// public MySaveJob(MyObject obj)
    /// {
    ///     _wasDirty = obj.IsDirty; // BAD: Captures snapshot in constructor
    ///     _object = obj;
    /// }
    /// public bool Execute() { ... } // Too late, already captured stale state
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
    /// Interface for save jobs that need cleanup when discarded (replaced by a newer job).
    /// When a job is replaced in SaveScheduler, Cancel() is called to ensure proper resource cleanup.
    /// This prevents stuck SaveInProgress flags and other state issues.
    /// </summary>
    public interface ICancellableSaveJob : ISaveJob
    {
        /// <summary>
        /// Called when this job is being discarded (replaced by a newer job).
        /// The job should invoke its completion callback with false and clean up any state.
        /// This method will NOT be called if Execute() has already been called.
        /// </summary>
        void Cancel();
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

        /// <summary>
        /// Hook for getting the world thread action queue count.
        /// Set by WorldManager.Initialize() to avoid circular dependencies.
        /// Used by DrainAndStop() to ensure all callbacks are processed before shutdown.
        /// </summary>
        public static Func<int> GetWorldQueueCount { get; set; }

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
        private volatile bool _disposed; // Set to true after queues are disposed

        private const int DefaultWorkerCount = 3;

        // High priority queue for player saves (Periodic)
        private readonly BlockingCollection<string> _periodicPlayerQueue =
            new BlockingCollection<string>();

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

        private readonly ConcurrentQueue<uint> _roundRobinQueue = new ConcurrentQueue<uint>();
        private readonly ConcurrentQueue<uint> _agingQueue = new ConcurrentQueue<uint>();

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

        // Track last cleanup time for coalesce tracker cleanup
        private DateTime _lastCoalesceCleanupUtc = DateTime.MinValue;
        private const int CoalesceCleanupIntervalSeconds = 45; // Run cleanup every 45 seconds

        private DateTime _lastIdleStateCleanupUtc = DateTime.MinValue;
        private const int IdleStateCleanupIntervalSeconds = 60; // Run idle cleanup every 60 seconds

        // Stuck save coalescing to reduce log spam
        private DateTime _lastStuckSummaryLogUtc = DateTime.MinValue;
        private const int StuckSummaryIntervalSeconds = 30; // Log summary every 30 seconds
        private int _stuckSaveCountPrio0 = 0; // Atomic
        private int _stuckSaveCountPrio1 = 0; // Critical
        private int _stuckSaveCountPrio2 = 0; // Periodic

        // Metrics counters
        private long _enqueued;
        private long _executedAtomic;
        private long _executedCritical;
        private long _executedPeriodic;
        private long _failedAtomic;
        private long _failedCritical;
        private long _failedPeriodic;
        private long _requeuedDirty; // Requeued due to dirty flag (new work arrived during execution)
        private long _reroutedPriority; // Rerouted due to priority upgrade (moved between queues)
        private long _executingKeys; // Count of keys currently executing (for accurate drain detection)

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

            // Start remaining workers for periodic queue with priority logic
            for (int i = 2; i < count; i++)
            {
                var thread = new Thread(() => PeriodicWorkerLoop())
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
        /// Requests a save operation for an individual item (non-player biota).
        /// Routes individual saves through SaveScheduler to prevent UniqueQueue starvation.
        /// Uses specified priority for ordering (default Periodic).
        /// </summary>
        /// <param name="biotaId">The biota ID to save</param>
        /// <param name="saveJob">The save job to execute</param>
        /// <param name="priority">The priority to use (default: Periodic)</param>
        /// <returns>True if save was enqueued or coalesced, false if shutdown requested</returns>
        public bool RequestItemSave(uint biotaId, ISaveJob saveJob, SaveType priority = SaveType.Periodic)
        {
            if (IsShutdownRequested)
                return false;

            if (saveJob == null)
                return false;

            // Use standardized key format for individual item saves
            var key = SaveKeys.Biota(biotaId);
            
            // Route as requested priority (default Periodic for backward compatibility)
            // Coalescing is handled by the SaveScheduler state machine (no UniqueQueue starvation)
            return RequestSaveInternal(key, priority, saveJob);
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

            // Bump generation for this key to track state updates
            Interlocked.Increment(ref state.Generation);

            // Upgrade priority if needed (lower number = higher priority)
            int oldPriority;
            bool priorityUpgraded = false;
            do
            {
                oldPriority = state.Priority;
                if (prio >= oldPriority) break; // New priority is not higher, no upgrade needed
                priorityUpgraded = true; // Priority was upgraded
            }
            while (Interlocked.CompareExchange(ref state.Priority, prio, oldPriority) != oldPriority);

            // HARDENING: If key is executing, never enqueue - only set Dirty and return
            // This guarantees "one execution at a time per key" and keeps all requeues
            // coming from the worker finally block, where CharacterSaveState math is safest
            // CRITICAL: Replace Job when executing so requeue uses latest job
            if (Volatile.Read(ref state.Executing) == 1)
            {
                // Job field is volatile, so assignment has proper memory barriers
                state.Job = saveJob;
                Interlocked.Exchange(ref state.Dirty, 1);
                return true;
            }

            // Enqueue only once per key (unless priority is upgraded - see below)
            // Use CompareExchange on Queued to atomically claim the slot (set to 1 only if it was 0)
            // Then enqueue, and only keep Queued=1 if enqueue succeeds
            // NOTE: When priority is upgraded, we may temporarily have the key in multiple queues
            // The invariant is "Queued=1 means logically queued somewhere" - physical duplicates are allowed during upgrades
            // Worker dequeue logic safely discards stale entries via Executing check and Queued==0 check
            // CRITICAL: Only replace Job if this is a NEW enqueue (CompareExchange succeeded)
            // If key is already queued, preserve the existing job that did prep work
            var wasNewlyEnqueued = Interlocked.CompareExchange(ref state.Queued, 1, 0) == 0;
            if (wasNewlyEnqueued)
            {
                // CRITICAL: This is a new enqueue, so set the job
                // Job field is volatile, so assignment has proper memory barriers
                // NOTE: The saveJob.Execute() method must read live state (dirty flags, etc.) at execution time,
                // not capture it in the constructor. See ISaveJob documentation for details.
                state.Job = saveJob;
                
                var enqueuedOk = EnqueueByPriority(key, state.Priority);
                if (enqueuedOk)
                {
                    // Enqueue succeeded, keep Queued=1
                    var nowTicks = DateTime.UtcNow.Ticks;
                    Volatile.Write(ref state.FirstEnqueuedTicksUtc, nowTicks);
                    Volatile.Write(ref state.EnqueuedTicksUtc, nowTicks);
                    // Set FirstEverEnqueuedTicksUtc only if not already set (hot key detector)
                    if (Volatile.Read(ref state.FirstEverEnqueuedTicksUtc) == 0)
                        Volatile.Write(ref state.FirstEverEnqueuedTicksUtc, nowTicks);

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

                    // Do not remove state here - let watchdog clean up idle states after sufficient idle time
                    // Removing here can cause race conditions where RequestSave has a reference but state is deleted
                    return false;
                }
            }
            else
            {
                // Key is already queued (CompareExchange failed - was already 1)
                // Check if executing (defensive check in case state changed between CompareExchange and here)
                var queued = Volatile.Read(ref state.Queued);
                var executing = Volatile.Read(ref state.Executing);

                if (executing == 1)
                {
                    // Currently executing (race condition - started between CompareExchange and here)
                    // Set Dirty to trigger requeue in finally block
                    // CRITICAL: Replace job to ensure latest snapshot is used for requeue
                    // NOTE: Do NOT call Cancel() here. The job is already running and cannot be aborted.
                    // Letting it finish naturally is safer. The new job will run immediately after via Dirty flag.
                    state.Job = saveJob;
                    Interlocked.Exchange(ref state.Dirty, 1);
                }
                else if (queued == 1)
                {
                    // Key is queued but not yet executing
                    // CRITICAL FIX: Replace the job with the new one to ensure latest snapshot is saved
                    // This prevents data loss when a newer save request arrives while an older one is queued
                    // Example: Periodic save (Snapshot A) is queued, then player drops item and immediate save (Snapshot B) arrives
                    // Without replacement: Snapshot A (old) would run, losing the item drop
                    // With replacement: Snapshot B (new) runs, preserving the item drop
                    // CRITICAL: Cancel old job first to clean up its SaveInProgress flags
                    var oldJob = Volatile.Read(ref state.Job);
                    if (oldJob is ICancellableSaveJob cancellable)
                    {
                        try { cancellable.Cancel(); }
                        catch (Exception ex) { log.Error($"[SAVESCHEDULER] Cancel() failed for queued job key={key}", ex); }
                    }
                    state.Job = saveJob;
                    
                    if (priorityUpgraded)
                    {
                        // Priority was upgraded - re-enqueue to higher priority queue to jump the line
                        // This creates a temporary duplicate entry (key in both old and new queues)
                        // Worker dequeue logic safely handles this via Executing check
                        var reenqueuedOk = EnqueueByPriority(key, state.Priority);
                        if (reenqueuedOk)
                        {
                            // CRITICAL: Increment Pending count because we added a second physical queue entry.
                            // The worker will process both entries (one real, one ghost).
                            // Balance the accounting so StartExecution doesn't warn about underflow.
                            var cid = ExtractCharacterIdFromKey(key);
                            if (cid.HasValue)
                            {
                                var charState = GetOrCreateCharacterSaveState(cid.Value);
                                charState.IncrementPending();
                            }

                            var nowTicks = DateTime.UtcNow.Ticks;
                            Volatile.Write(ref state.EnqueuedTicksUtc, nowTicks);
                            // Reset FirstEnqueuedTicksUtc for priority upgrades (work is being rerouted)
                            Volatile.Write(ref state.FirstEnqueuedTicksUtc, nowTicks);
                            Interlocked.Increment(ref _reroutedPriority);
                        }
                    }
                    // If priority wasn't upgraded, job replacement is sufficient
                    // The existing queue entry will execute the new job (latest snapshot)
                }
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

            BlockingCollection<string> queue;

            if (priority == 0) // Atomic
                queue = _atomicQueue;
            else if (priority == 1) // Critical
                queue = _criticalQueue;
            else // Periodic
            {
                // PRIORITY SPLIT: Route player saves to high-priority queue
                // This prevents bulk item saves from blocking player saves
                if (ExtractCharacterIdFromKey(key).HasValue)
                {
                    queue = _periodicPlayerQueue;
                }
                else
                {
                    queue = _periodicQueue;
                }
            }

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

        /// <summary>
        /// Specialized worker loop for periodic saves with strict priority.
        /// Always processes player saves before item saves to prevent bulk items from blocking players.
        /// </summary>
        private void PeriodicWorkerLoop()
        {
            var queues = new[] { _periodicPlayerQueue, _periodicQueue };

            while (!_disposed)
            {
                string key = null;
                try
                {
                    // STRICT PRIORITY: Try player queue first (non-blocking)
                    if (_periodicPlayerQueue.TryTake(out key))
                    {
                        // Process player save immediately
                        ProcessPeriodicKey(key);
                    }
                    // Then try item queue (non-blocking)
                    else if (_periodicQueue.TryTake(out key))
                    {
                        // Process item save only if no players waiting
                        ProcessPeriodicKey(key);
                    }
                    // Both empty: block until any work arrives
                    else
                    {
                        BlockingCollection<string>.TakeFromAny(queues, out key);
                        if (key != null)
                            ProcessPeriodicKey(key);
                    }
                }
                catch (ArgumentException)
                {
                    // Queue disposed during shutdown
                    break;
                }
                catch (InvalidOperationException)
                {
                    // Queue completed during shutdown
                    break;
                }
            }
        }

        /// <summary>
        /// Processes a single periodic save key. Extracted for reuse by PeriodicWorkerLoop.
        /// </summary>
        private void ProcessPeriodicKey(string key)
        {
            const int myPriority = 2; // Periodic priority

            // Throttling for Periodic saves (backpressure)
            var shard = DatabaseManager.Shard;
            while (!IsShutdownRequested && shard != null && shard.QueueCount > 50)
            {
                Thread.Sleep(50);
            }

            if (!_states.TryGetValue(key, out var state))
                return;

            if (Volatile.Read(ref state.Queued) == 0)
                return;

            var currentPriority = Volatile.Read(ref state.Priority);
            if (currentPriority < myPriority)
            {
                var ok = EnqueueByPriority(key, currentPriority);
                if (ok)
                {
                    var nowTicks = DateTime.UtcNow.Ticks;
                    Volatile.Write(ref state.EnqueuedTicksUtc, nowTicks);
                    Volatile.Write(ref state.FirstEnqueuedTicksUtc, nowTicks);
                    Interlocked.Increment(ref _reroutedPriority);
                    return;
                }
                else
                {
                    log.Fatal($"[SAVESCHEDULER] CRITICAL: Early reroute failed for key={key} from myPriority={myPriority} to newPriority={currentPriority}. Processing on current worker to prevent save loss.");
                }
            }

            if (Interlocked.Exchange(ref state.Executing, 1) == 1)
                return;

            Interlocked.Increment(ref _executingKeys);
            var processingGen = Volatile.Read(ref state.Generation);

            var characterId = ExtractCharacterIdFromKey(key);
            CharacterSaveState charState = null;
            if (characterId.HasValue)
            {
                charState = GetOrCreateCharacterSaveState(characterId.Value);
                charState.StartExecution();
            }

            ISaveJob executedJob = null;

            try
            {
                executedJob = Volatile.Read(ref state.Job);
                var startTime = DateTime.UtcNow;
                var result = executedJob?.Execute() ?? false;
                var duration = DateTime.UtcNow - startTime;
                var durationMs = (long)duration.TotalMilliseconds;

                Interlocked.Increment(ref _executedPeriodic);
                Interlocked.Add(ref _totalWorkMsPeriodic, durationMs);
                Interlocked.Increment(ref _workCountPeriodic);
                if (!result)
                    Interlocked.Increment(ref _failedPeriodic);

                if (duration.TotalMilliseconds >= 1000)
                {
                    log.Warn($"[SAVESCHEDULER] Save operation '{key}' took {duration.TotalMilliseconds:N0}ms");
                }

                if (log.IsDebugEnabled)
                {
                    log.Debug($"[SAVESCHEDULER] Save '{key}' completed (result={result}, duration={duration.TotalMilliseconds:N0}ms)");
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _failedPeriodic);
                log.Error($"[SAVESCHEDULER] Save operation '{key}' threw exception", ex);
            }
            finally
            {
                Volatile.Write(ref state.LastProcessedGen, processingGen);
                Interlocked.Decrement(ref _executingKeys);
                Volatile.Write(ref state.Executing, 0);
                Interlocked.Exchange(ref state.Queued, 0);

                var wasDirty = Interlocked.Exchange(ref state.Dirty, 0) == 1;
                var currentGen = Volatile.Read(ref state.Generation);
                var hasNewWork = wasDirty || (currentGen != processingGen);
                var willRequeue = hasNewWork && (Interlocked.CompareExchange(ref state.Queued, 1, 0) == 0);

                var isTransactionKey = key.Contains("_tx:");

                if (!hasNewWork && executedJob != null)
                {
                    Interlocked.CompareExchange(ref state.Job, null, executedJob);
                    if (isTransactionKey && state.Job == null)
                    {
                        _states.TryRemove(key, out _);
                    }
                }

                List<Action> callbacksToInvoke = null;
                if (charState != null)
                {
                    callbacksToInvoke = charState.FinishExecutionMaybeRequeueAndDrain(willRequeue);
                    if (callbacksToInvoke != null)
                    {
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
                            });
                        }
                        else
                        {
                            log.Fatal("[SAVESCHEDULER] CRITICAL: EnqueueToWorldThread not set! Cannot invoke callbacks safely.");
                            throw new InvalidOperationException("EnqueueToWorldThread delegate not set.");
                        }

                        if (characterId.HasValue)
                        {
                            TryRemoveIdleCharacterSaveState(characterId.Value);
                        }
                    }
                }

                if (willRequeue)
                {
                    var ok = EnqueueByPriority(key, state.Priority);
                    if (ok)
                    {
                        Volatile.Write(ref state.EnqueuedTicksUtc, DateTime.UtcNow.Ticks);
                    }
                    else
                    {
                        Interlocked.Exchange(ref state.Queued, 0);
                        if (charState != null)
                        {
                            charState.DecrementPending();
                        }
                    }
                }
            }
        }

        private void WorkerLoop(BlockingCollection<string> myQueue, int myPriority)
        {
            foreach (var key in myQueue.GetConsumingEnumerable())
            {
                // Throttling for Periodic saves (backpressure) to prevent flooding the DB queue.
                // SerializedShardDatabase processes saves sequentially on a single thread.
                // If we flood it with 4000+ periodic saves, Critical saves (logout) get stuck 
                // behind them in the DB queue, rendering SaveScheduler priorities useless.
                // By waiting here, we keep the DB queue short allow Critical saves to skip the line.
                // 50 items = ~1-2 seconds of DB work.
                if (myPriority >= 2)
                {
                    var shard = DatabaseManager.Shard;
                    while (!IsShutdownRequested && shard != null && shard.QueueCount > 50)
                    {
                        Thread.Sleep(50);
                    }
                }

                if (!_states.TryGetValue(key, out var state))
                    continue;

                // Skip stale queue entries - if Queued is already 0, this entry was superseded
                // This prevents old entries from executing when a newer enqueue already happened
                // Also handles duplicate entries from priority upgrades: if another worker already processed
                // and cleared Queued during reroute, this entry is stale and should be skipped
                if (Volatile.Read(ref state.Queued) == 0)
                    continue;

                // Early reroute before Executing claim
                // Check priority BEFORE claiming Executing to avoid creating a window where
                // Queued is temporarily 0 (which would allow RequestSave to increment pending again)
                var currentPriority = Volatile.Read(ref state.Priority);
                if (currentPriority < myPriority)
                {
                    // Key belongs in a higher priority queue.
                    // Do NOT change state.Queued here.
                    // Do NOT claim state.Executing.
                    var ok = EnqueueByPriority(key, currentPriority);

                    if (ok)
                    {
                        var nowTicks = DateTime.UtcNow.Ticks;
                        Volatile.Write(ref state.EnqueuedTicksUtc, nowTicks);
                        Volatile.Write(ref state.FirstEnqueuedTicksUtc, nowTicks);
                        Interlocked.Increment(ref _reroutedPriority);
                        continue; // Successfully rerouted, let higher priority worker handle it
                    }
                    else
                    {
                        // Reroute failed (shutdown, queue completed, or edge case).
                        // SAFETY: Don't drop the save - process it on this worker instead.
                        // Priority correctness is less important than "never lose a save".
                        log.Fatal($"[SAVESCHEDULER] CRITICAL: Early reroute failed for key={key} from myPriority={myPriority} to newPriority={currentPriority}. Processing on current worker to prevent save loss.");
                        // Fall through to normal processing below - do NOT continue or clear Queued
                    }
                }

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

                // Only count real executions (after priority reroute check to prevent leaks)
                // This ensures _executingKeys is only incremented when we will actually execute the try/finally block
                Interlocked.Increment(ref _executingKeys);

                // Capture generation to detect requests that arrive during teardown window
                // This prevents lost saves when RequestSave hits between clearing Executing and Queued
                var processingGen = Volatile.Read(ref state.Generation);

                // Track character saves: save is starting execution
                var characterId = ExtractCharacterIdFromKey(key);
                CharacterSaveState charState = null;
                if (characterId.HasValue)
                {
                    charState = GetOrCreateCharacterSaveState(characterId.Value);
                    charState.StartExecution(); // pending--, active++
                }

                // Declare outside try so it is available in finally for safe job clearing
                ISaveJob executedJob = null;

                try
                {
                    // Capture job reference to avoid race where Job changes between null check and Execute call
                    // Store in executedJob for safe clearing in finally block
                    executedJob = Volatile.Read(ref state.Job);
                    var startTime = DateTime.UtcNow;
                    var result = executedJob?.Execute() ?? false;
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

                    // Record the generation we processed (for detecting requests during teardown window)
                    // Write this in finally to ensure it's always updated, even on exceptions
                    Volatile.Write(ref state.LastProcessedGen, processingGen);

                    // Decrement executing key count (track actual executing work for drain detection)
                    Interlocked.Decrement(ref _executingKeys);

                    // Clear executing flag first (natural lifecycle ordering)
                    Volatile.Write(ref state.Executing, 0);

                    // Clear Queued flag for this execution (always clear, regardless of current value)
                    // This ensures we don't get stuck in an infinite requeue loop
                    Interlocked.Exchange(ref state.Queued, 0);

                    // Check if dirty flag was set (new request arrived while executing)
                    // Dirty is only set when Executing == 1, preventing double requeue from Queued + Dirty
                    // Also check Generation change to catch requests that arrive during teardown window
                    // (between clearing Executing and Queued, where RequestSave sees Executing=0, Queued=1
                    // and doesn't set Dirty or enqueue, but does increment Generation)
                    var wasDirty = Interlocked.Exchange(ref state.Dirty, 0) == 1;
                    var currentGen = Volatile.Read(ref state.Generation);

                    // Dirty OR generation change means we must run again
                    var hasNewWork = wasDirty || (currentGen != processingGen);

                    // CRITICAL: Claim the Queued slot BEFORE updating CharacterSaveState to prevent "ghost pending"
                    // If someone else already enqueued it (RequestSave sneaked in), we don't want to increment pending
                    // This prevents double enqueue from causing pending count drift (incremented twice, decremented once)
                    var willRequeue = hasNewWork && (Interlocked.CompareExchange(ref state.Queued, 1, 0) == 0);

                    // FIX #3: Transaction keys (vendor_tx, storage_tx, etc.) are unique per operation and should
                    // be cleaned up aggressively to prevent memory growth during high transaction volume
                    var isTransactionKey = key.Contains("_tx:");

                    // MEMORY OPTIMIZATION: Clear job reference if no new work arrived and job hasn't been replaced
                    // This releases closure references (biotas, callbacks, Player object) immediately
                    // instead of keeping them alive for up to 5 minutes until watchdog cleanup
                    // 
                    // CRITICAL: Only clear if NO new work arrived AND the job is still the same one we executed
                    // hasNewWork is the true "more work exists" signal
                    // willRequeue is only "I personally claimed the requeue slot"
                    // If a new RequestSave replaced the job, CAS fails and we keep the newest job
                    if (!hasNewWork && executedJob != null)
                    {
                        // Only clear if it is still the same job we executed
                        // CompareExchange will fail if job was replaced, keeping the newer job
                        Interlocked.CompareExchange(ref state.Job, null, executedJob);
                        
                        // FIX #3: For transaction keys, also remove the SaveState immediately to prevent accumulation
                        // Transaction keys are never reused, so keeping the state serves no purpose
                        // Only remove if we successfully cleared the job (CAS succeeded) AND no work exists
                        if (isTransactionKey && state.Job == null)
                        {
                            _states.TryRemove(key, out _);
                        }
                    }

                    // Atomically finish execution, optionally requeue, and drain callbacks if ready
                    // This ensures all counter changes and callback draining happen under one lock,
                    // preventing race conditions where callbacks drain at a bad moment
                    // Use willRequeue (not hasNewWork) so we only increment pending if we actually claimed the slot
                    List<Action> callbacksToInvoke = null;
                    if (charState != null)
                    {
                        callbacksToInvoke = charState.FinishExecutionMaybeRequeueAndDrain(willRequeue);
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
                                });
                            }
                            else
                            {
                                // CRITICAL ERROR: EnqueueToWorldThread should always be set by WorldManager.Initialize()
                                // Fail fast - do not execute callbacks on DB thread (violates world thread contract)
                                log.Fatal("[SAVESCHEDULER] CRITICAL: EnqueueToWorldThread not set! Cannot invoke callbacks safely. This indicates WorldManager.Initialize() was not called or failed.");
                                throw new InvalidOperationException("EnqueueToWorldThread delegate not set - cannot safely invoke callbacks on world thread. This indicates a fatal initialization error in WorldManager.Initialize().");
                            }

                            // All saves complete and callbacks drained - try to clean up idle CharacterSaveState
                            if (characterId.HasValue)
                            {
                                TryRemoveIdleCharacterSaveState(characterId.Value);
                            }
                        }
                    }

                    // Handle actual queue re-enqueue (only if we successfully claimed the Queued slot)
                    // If we didn't claim it, someone else (RequestSave) already enqueued it, so we don't need to
                    if (willRequeue)
                    {
                        // Enqueue the work (we already claimed Queued=1 above)
                        // This maintains the invariant "Queued=1 means logically queued somewhere"
                        var ok = EnqueueByPriority(key, state.Priority);
                        if (ok)
                        {
                            var nowTicks = DateTime.UtcNow.Ticks;
                            Volatile.Write(ref state.EnqueuedTicksUtc, nowTicks); // Update enqueue time for stuck detection
                            // Reset FirstEnqueuedTicksUtc on requeue so "stuck" means actually stuck, not "busy but healthy"
                            // This prevents frequently dirtied keys from looking "stuck" forever
                            // Keep FirstEverEnqueuedTicksUtc unchanged (hot key detector)
                            Volatile.Write(ref state.FirstEnqueuedTicksUtc, nowTicks);

                            if (wasDirty)
                                Interlocked.Increment(ref _requeuedDirty);
                        }
                        else
                        {
                            log.Error($"[SAVESCHEDULER] Failed to enqueue requeued key={key}, clearing queued flag");
                            // Rollback: clear Queued slot and decrement pending
                            Interlocked.Exchange(ref state.Queued, 0);
                            if (characterId.HasValue && charState != null)
                            {
                                charState.DecrementPending();

                                // CRITICAL: After rollback, callbacks might now be ready to fire
                                // If pendingCount and activeCount both hit zero, we must drain callbacks
                                // Otherwise callbacks can get stuck forever (login callback never fires)
                                var lateCallbacks = charState.CheckAndDrainCallbacks();
                                if (lateCallbacks != null)
                                {
                                    InvokeCallbacks(characterId.Value, lateCallbacks);
                                }
                            }
                        }
                    }

                    // Do not remove state here - let watchdog clean up idle states after sufficient idle time
                    // Removing here can cause race conditions where RequestSave has a reference but state is deleted
                    // The watchdog will clean up truly idle states (queued=0, executing=0, dirty=0) after 5-10 minutes
                }
            }
        }

        /// <summary>
        /// Gets the ActionQueue count.
        /// Uses GetWorldQueueCount hook if set, otherwise returns 0.
        /// </summary>
        private int GetActionQueueCount()
        {
            if (GetWorldQueueCount != null)
                return GetWorldQueueCount();
            return 0;
        }

        /// <summary>
        /// Drains all pending saves and callbacks, then stops the scheduler.
        /// This is the recommended method to call during server shutdown.
        ///
        /// The drain process:
        /// 1. Prevents new saves from being enqueued (sets shutdown flag)
        /// 2. Reduces stuck threshold to 5 seconds for aggressive ghost cleanup
        /// 3. Waits for all queued saves to complete (QueueCount == 0)
        /// 4. Waits for all executing saves to complete (ExecutingKeyCount == 0)
        /// 5. Waits for all callbacks to be processed (ActionQueue.Count == 0)
        /// 6. Calls Stop() to gracefully shut down worker threads
        ///
        /// This ensures callbacks can drain and completion signals fire before world thread stops.
        /// Note: Uses ExecutingKeyCount instead of StateCount because StateCount includes idle states
        /// that haven't been cleaned up yet (watchdog waits 5 minutes), which would cause false timeouts.
        /// </summary>
        /// <param name="timeout">Maximum time to wait for drain. Defaults to 30 seconds.</param>
        public void DrainAndStop(TimeSpan timeout)
        {
            // Prevent new saves from being enqueued
            Interlocked.Exchange(ref _shutdownRequested, 1);
            SetStuckThreshold(TimeSpan.FromSeconds(5));

            var start = DateTime.UtcNow;
            while (true)
            {
                if (QueueCount == 0 && ExecutingKeyCount == 0 && GetActionQueueCount() == 0)
                    break;

                if (DateTime.UtcNow - start > timeout)
                {
                    log.Warn("[SAVESCHEDULER] Drain timeout");
                    break;
                }

                Thread.Sleep(10);
            }

            Stop();
        }

        /// <summary>
        /// Stops the scheduler gracefully, allowing current work to complete.
        /// This method is idempotent and can be called multiple times safely.
        ///
        /// Note: _shutdownRequested means "do not accept new work"
        /// _disposed means "shutdown has already been completed"
        /// These are different states - we only early return if already disposed.
        /// </summary>
        public void Stop()
        {
            // If already fully stopped, do nothing
            if (_disposed)
                return;

            log.Info("[SAVESCHEDULER] Stopping scheduler...");

            // Ensure shutdown flag is set (idempotent - may already be set by DrainAndStop)
            Interlocked.Exchange(ref _shutdownRequested, 1);

            // Complete adding to all queues (allows GetConsumingEnumerable to exit)
            _atomicQueue.CompleteAdding();
            _criticalQueue.CompleteAdding();
            _periodicQueue.CompleteAdding();
            _periodicPlayerQueue.CompleteAdding();

            // Wait for worker threads to finish processing current items
            foreach (var thread in _workers)
            {
                thread.Join();
            }

            // Signal watchdog thread to exit and then wait for it to finish.
            // IMPORTANT: _disposed is what controls the watchdog loop condition:
            // while (!_disposed) { ... }
            // If we waited for the watchdog before setting _disposed, we would
            // deadlock here because the watchdog would never see the exit signal.
            _disposed = true;
            _watchdogThread?.Join();

            // Capture queue counts before disposal (QueueCount returns 0 after _disposed is set)
            var remainingQueued = _atomicQueue.Count + _criticalQueue.Count + _periodicQueue.Count + _periodicPlayerQueue.Count;

            // Now safe to dispose collections
            _atomicQueue.Dispose();
            _criticalQueue.Dispose();
            _periodicQueue.Dispose();
            _periodicPlayerQueue.Dispose();

            log.Info($"[SAVESCHEDULER] Stopped. Remaining states={_states.Count} Remaining queued={remainingQueued}");
        }

        /// <summary>
        /// Gets the number of save states currently tracked (may include completed states awaiting cleanup).
        /// </summary>
        public int StateCount => _states.Count;

        public long ExecutingKeyCount => Interlocked.Read(ref _executingKeys);

        /// <summary>
        /// Gets the total number of queued save requests across all queues.
        /// Returns 0 if disposed to prevent ObjectDisposedException.
        ///
        /// Note: _shutdownRequested blocks new saves but does not affect QueueCount.
        /// Only _disposed suppresses observability (returns 0 after disposal).
        /// This ensures QueueCount remains truthful during DrainAndStop().
        /// </summary>
        public int QueueCount
        {
            get
            {
                if (_disposed)
                    return 0;

                try
                {
                    return _atomicQueue.Count + _criticalQueue.Count + _periodicQueue.Count + _periodicPlayerQueue.Count;
                }
                catch (ObjectDisposedException)
                {
                    return 0;
                }
            }
        }

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
                var nowTicks = DateTime.UtcNow.Ticks;
                return new PlayerSaveState
                {
                    PlayerId = id,
                    LastAttemptTicksUtc = nowTicks,
                    LastSavedTicksUtc = nowTicks
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

            // Clamp basePerTick first to prevent minPerTick from exceeding the cap
            // This ensures the cap is respected even when playerCount is very large
            basePerTick = Math.Min(basePerTick, MaxSavesPerTickCap);

            int minPerTick = Math.Max(1, basePerTick);
            int maxPerTick = Math.Min(basePerTick * 2, MaxSavesPerTickCap);

            int issued = 0;
            var now = DateTime.UtcNow;

            // Step 1: aging enforcement
            // Use LastAttemptTicksUtc for scheduling to prevent retry storms when DB fails
            // This ensures failed saves don't immediately trigger another aging save
            foreach (var kvp in _players)
            {
                var state = kvp.Value;

                // Use Volatile.Read for thread-safe access (written on SaveScheduler worker thread, read on world thread)
                var lastAttemptTicks = Volatile.Read(ref state.LastAttemptTicksUtc);
                var lastAttempt = new DateTime(lastAttemptTicks, DateTimeKind.Utc);

                if ((now - lastAttempt).TotalSeconds >= SaveWindowSeconds && Interlocked.CompareExchange(ref state.Enqueued, 1, 0) == 0)
                {
                    _agingQueue.Enqueue(state.PlayerId);
                }
            }

            // Step 2: drain aging queue
            // Aging saves should force dirty marking to ensure they execute even if flags were cleared
            while (issued < maxPerTick && _agingQueue.TryDequeue(out var agedId))
            {
                if (!_players.TryGetValue(agedId, out var state))
                {
                    // Player was removed between Step 1 and Step 2
                    // Enqueued flag was set in Step 1, but we can't clear it if state is gone
                    // This is safe because if the player was unregistered, the state should be removed
                    continue;
                }

                // EnqueuePlayerSave will clear Enqueued if it fails (factory returns null, RequestSave fails, etc.)
                // This prevents players from getting stuck "enqueued" forever
                if (EnqueuePlayerSave(state, saveFactory, forceDirty: true))
                    issued++;
            }

            // Step 3: round robin fairness
            // Round robin saves are normal periodic saves - don't force dirty
            // IMPORTANT: cap attempts so we cannot spin forever if everyone is already enqueued
            // This prevents infinite loop that could freeze the world thread
            int rrAttempts = 0;
            int rrMaxAttempts = playerCount; // safe upper bound - at most one pass over player population

            while (issued < minPerTick && rrAttempts < rrMaxAttempts && _roundRobinQueue.TryDequeue(out var rrId))
            {
                rrAttempts++;

                if (!_players.TryGetValue(rrId, out var state))
                {
                    // Player was removed between enqueue and dequeue
                    // Enqueued flag was set, but we can't clear it if state is gone
                    // This is safe because if the player was unregistered, the state should be removed
                    continue;
                }

                // Use Enqueued gate to prevent duplicate work (same as aging queue)
                // This ensures "one in flight per player" behavior and prevents wasting CPU
                // on inventory snapshots when the player is already queued at SaveScheduler layer
                if (Interlocked.CompareExchange(ref state.Enqueued, 1, 0) != 0)
                {
                    // Already enqueued, put back in round robin queue and skip
                    _roundRobinQueue.Enqueue(rrId);
                    continue;
                }

                // EnqueuePlayerSave will clear Enqueued if it fails (factory returns null, RequestSave fails, etc.)
                // PlayerSaveJobWrapper will clear Enqueued on completion (success or failure)
                // This prevents players from getting stuck "enqueued" forever
                if (EnqueuePlayerSave(state, saveFactory, forceDirty: false))
                    issued++;

                // Always rotate forward so fairness stays intact
                // This ensures round robin fairness even if the save failed
                _roundRobinQueue.Enqueue(rrId);
            }
        }

        /// <summary>
        /// Enqueues a player save using the existing RequestSave logic.
        /// Wraps the save job with ForcedPeriodicSaveJob to ensure aging saves always execute,
        /// even if dirty flags were cleared prematurely or changes were masked.
        ///
        /// CRITICAL: This method MUST clear state.Enqueued in all failure cases to prevent players
        /// from getting stuck "enqueued" forever. Step 1 sets Enqueued=1 before we actually enqueue,
        /// so if this method fails (factory returns null, RequestSave fails, exception), we must
        /// clear the flag so the player can be scheduled again.
        /// </summary>
        /// <param name="state">Player save state</param>
        /// <param name="saveFactory">Factory function that creates the save job</param>
        /// <param name="forceDirty">True if this save was triggered by the aging queue (should force dirty), false for round robin saves</param>
        /// <returns>True if the save was successfully enqueued, false otherwise (factory returned null, enqueue failed, etc.)</returns>
        private bool EnqueuePlayerSave(PlayerSaveState state, Func<uint, ISaveJob> saveFactory, bool forceDirty)
        {
            try
            {
                var job = saveFactory(state.PlayerId);

                // Factory returned null (player not found, already saving, etc.)
                // CRITICAL: Clear Enqueued flag so player can be scheduled again
                if (job == null)
                {
                    Interlocked.Exchange(ref state.Enqueued, 0);
                    return false;
                }

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

                var enqueued = RequestSave(SaveKeys.Player(state.PlayerId), SaveType.Periodic, new PlayerSaveJobWrapper(forcedJob, state));

                // If enqueue failed, clear the Enqueued flag so the player can be scheduled again
                // CRITICAL: Step 1 set Enqueued=1, so we must clear it if RequestSave fails
                if (!enqueued)
                {
                    Interlocked.Exchange(ref state.Enqueued, 0);
                    return false;
                }

                // LastSavedTicksUtc will be updated when the job completes (in PlayerSaveJobWrapper.Execute)
                // This prevents the "10 minute gap" problem by tracking when saves actually finish
                return true;
            }
            catch (Exception ex)
            {
                // On any exception, clear Enqueued flag and return false
                // CRITICAL: Step 1 set Enqueued=1, so we must clear it on exceptions to prevent stuck players
                log.Error($"[SAVESCHEDULER] Exception in EnqueuePlayerSave for player {state.PlayerId}", ex);
                Interlocked.Exchange(ref state.Enqueued, 0);
                return false;
            }
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
        private sealed class ForcedPeriodicSaveJob : ISaveJob, ICancellableSaveJob
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

            public void Cancel()
            {
                if (_inner is ICancellableSaveJob cancellable)
                {
                    cancellable.Cancel();
                }
            }
        }

        /// <summary>
        /// Wrapper that updates player save state after execution.
        /// </summary>
        private sealed class PlayerSaveJobWrapper : ISaveJob, ICancellableSaveJob
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
                try
                {
                    var result = _innerJob?.Execute() ?? false;
                    var nowTicks = DateTime.UtcNow.Ticks;

                    // Always update LastAttemptTicksUtc to track when saves are attempted
                    // This prevents retry storms when DB fails - aging uses attempt time for scheduling
                    // Use Volatile.Write for thread-safe access (written on SaveScheduler worker thread, read on world thread)
                    Volatile.Write(ref _playerState.LastAttemptTicksUtc, nowTicks);
                    
                    // Only update LastSavedTicksUtc when the save actually succeeds
                    // This tracks when saves actually completed successfully
                    // Can be used for forceDirty enforcement or other success-based logic
                    if (result)
                        Volatile.Write(ref _playerState.LastSavedTicksUtc, nowTicks);
                    return result;
                }
                finally
                {
                    // Always clear Enqueued flag when job completes (success or failure)
                    // This ensures players can be scheduled again even if the save failed
                    Interlocked.Exchange(ref _playerState.Enqueued, 0);
                }
            }

            public void Cancel()
            {
                // Forward cancellation to inner job
                if (_innerJob is ICancellableSaveJob cancellable)
                {
                    cancellable.Cancel();
                }

                // CRITICAL: We also need to clear the Enqueued flag on the PlayerSaveState
                // because this job is being discarded and will never run.
                // If we don't, the player will look "Enqueued" forever (until Watchdog fix).
                Interlocked.Exchange(ref _playerState.Enqueued, 0);
            }
        }

        private sealed class SaveState
        {
            public volatile int Priority; // 0 = atomic, 1 = critical, 2 = periodic
            public volatile int Queued; // 0 = no, 1 = yes (only set after successful enqueue - means "logically queued somewhere", physical duplicates allowed during priority upgrades)
            public volatile int Executing; // 0 = no, 1 = yes
            public volatile int Dirty; // 0 = clean, 1 = dirty (new request arrived while in flight)
            public volatile ISaveJob Job; // Save job to execute (reduces delegate allocations) - volatile for thread-safe updates
            public long FirstEnqueuedTicksUtc; // When this state was last enqueued (progress detector - reset on requeue) - 0 = not set
            public long FirstEverEnqueuedTicksUtc; // When this state was first ever enqueued (hot key detector - never reset) - 0 = not set
            public long EnqueuedTicksUtc; // When this state was last enqueued (updated on requeue) - 0 = not set
            public long LastStuckLogTicksUtc; // When we last logged a stuck warning (for rate limiting) - 0 = not set
            public int Generation; // Increments per RequestSave to track state updates
            public int LastProcessedGen; // Last generation a worker finished processing
        }

        private sealed class PlayerSaveState
        {
            public uint PlayerId;
            public long LastAttemptTicksUtc; // atomic friendly - updated on every save attempt (success or fail)
            public long LastSavedTicksUtc; // atomic friendly - updated only on successful saves
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
            private int _pendingCount; // Number of saves queued but not yet executing
            private int _activeCount; // Number of saves currently executing
            private DateTime _lastActivityUtc;
            private readonly object _lock = new object();
            private readonly List<Action> _callbacks = new List<Action>();

            public int PendingCount
            {
                get
                {
                    lock (_lock)
                    {
                        return _pendingCount;
                    }
                }
            }

            public int ActiveCount
            {
                get
                {
                    lock (_lock)
                    {
                        return _activeCount;
                    }
                }
            }

            public DateTime LastActivityUtc
            {
                get
                {
                    lock (_lock)
                    {
                        return _lastActivityUtc;
                    }
                }
            }

            public void TouchActivity()
            {
                lock (_lock)
                {
                    _lastActivityUtc = DateTime.UtcNow;
                }
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
                if (callback == null)
                    return false;

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
            /// Atomically finishes execution, optionally requeues, and drains callbacks if ready.
            /// This ensures all counter changes and callback draining happen under one lock,
            /// preventing race conditions where callbacks drain at a bad moment.
            /// </summary>
            /// <param name="willRequeue">If true, increments pending count (requeue). If false, only finishes execution.</param>
            /// <returns>List of callbacks to invoke if all saves are complete, or null if not ready.</returns>
            public List<Action> FinishExecutionMaybeRequeueAndDrain(bool willRequeue)
            {
                lock (_lock)
                {
                    // Optionally requeue (increment pending)
                    if (willRequeue)
                    {
                        _pendingCount++;
                    }

                    // Finish execution (decrement active)
                    if (_activeCount > 0)
                        _activeCount--;
                    else
                    {
                        log.Warn($"[SAVESCHEDULER] Character {CharacterId} FinishExecutionMaybeRequeueAndDrain with active=0, clamping");
                    }

                    _lastActivityUtc = DateTime.UtcNow;

                    // Check if all work is complete and drain callbacks if so
                    if (_pendingCount == 0 && _activeCount == 0)
                        return DrainCallbacksLocked();

                    return null;
                }
            }

            /// <summary>
            /// Force clears all counters and callbacks. Used for self-healing when ghost state is detected.
            /// </summary>
            public List<Action> ForceClearIfNoWorkAndDrainCallbacks()
            {
                lock (_lock)
                {
                    _pendingCount = 0;
                    _activeCount = 0;
                    _lastActivityUtc = DateTime.UtcNow;

                    if (_callbacks.Count == 0)
                        return null;

                    var callbacks = new List<Action>(_callbacks);
                    _callbacks.Clear();
                    return callbacks;
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
            // character:123:player
            // character:123:character
            // character:123:item:456
            // character:123:storage_tx:guid
            // character:123:vendor_tx:guid
            // character:123:bank_tx:guid
            // character:123:logout
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
                var callbacks = state.ForceClearIfNoWorkAndDrainCallbacks();
                TryRemoveIdleCharacterSaveState(characterId);
                if (callbacks != null)
                {
                    InvokeCallbacks(characterId, callbacks);
                }
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
            // HARDENING: Snapshot keys before iteration to prevent missing newly added saves
            // ConcurrentDictionary enumeration is thread-safe but not atomic - keys added during
            // iteration might be skipped due to internal bucket rehashing
            var keysSnapshot = _states.Keys.ToArray();
            
            foreach (var key in keysSnapshot)
            {
                var cid = ExtractCharacterIdFromKey(key);
                if (!cid.HasValue || cid.Value != characterId)
                    continue;

                // Key might have been removed between snapshot and now - handle gracefully
                if (!_states.TryGetValue(key, out var s))
                    continue;
                var executing = Volatile.Read(ref s.Executing);
                var queued = Volatile.Read(ref s.Queued);

                // Executing or Queued always indicate real work.
                // Dirty is only meaningful while one of these is set, so we intentionally ignore Dirty
                // unless work is executing or queued to avoid theoretical edge cases.
                if (executing == 1)
                    return true;

                if (queued == 1)
                    return true;
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
            // HARDENING: Snapshot keys before iteration to prevent missing newly added saves
            // ConcurrentDictionary enumeration is thread-safe but not atomic - keys added during
            // iteration might be skipped due to internal bucket rehashing
            var keysSnapshot = _states.Keys.ToArray();
            
            foreach (var key in keysSnapshot)
            {
                var cid = ExtractCharacterIdFromKey(key);
                if (!cid.HasValue || cid.Value != characterId)
                    continue;

                // Key might have been removed between snapshot and now - handle gracefully
                if (!_states.TryGetValue(key, out var s))
                    continue;

                if (Volatile.Read(ref s.Executing) == 1 || Volatile.Read(ref s.Queued) == 1)
                    return false; // Real work exists, cannot clear
            }

            // All conditions met - safe to clear
            log.Warn($"[SAVESCHEDULER] Admin command clearing ghost save state for character {characterId}");
            var callbacks = state.ForceClearIfNoWorkAndDrainCallbacks();
            TryRemoveIdleCharacterSaveState(characterId);
            if (callbacks != null)
            {
                InvokeCallbacks(characterId, callbacks);
            }

            return true;
        }

        /// <summary>
        /// Attempts to remove a CharacterSaveState if it's idle and has no real work.
        /// Safe to call after ForceClearIfNoWorkAndDrainCallbacks or after callbacks drain.
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
                    // Fail fast - do not execute callback on calling thread (violates world thread contract)
                    log.Fatal("[SAVESCHEDULER] CRITICAL: EnqueueToWorldThread not set! Cannot invoke callback safely. This indicates WorldManager.Initialize() was not called or failed.");
                    throw new InvalidOperationException("EnqueueToWorldThread delegate not set - cannot safely invoke callback on world thread. This indicates a fatal initialization error in WorldManager.Initialize().");
                }
            }
            // Otherwise callback is registered and will be invoked when saves complete
        }

        /// <summary>
        /// Centralized callback invocation with world-thread-safe execution.
        /// </summary>
        private void InvokeCallbacks(uint characterId, List<Action> callbacks)
        {
            if (callbacks == null || callbacks.Count == 0)
                return;

            if (EnqueueToWorldThread != null)
            {
                EnqueueToWorldThread(() =>
                {
                    foreach (var cb in callbacks)
                    {
                        try
                        {
                            cb();
                        }
                        catch (Exception ex)
                        {
                            log.Error($"[SAVESCHEDULER] Callback failed after force clear for character {characterId}", ex);
                        }
                    }
                });
            }
            else
            {
                // CRITICAL ERROR: EnqueueToWorldThread should always be set by WorldManager.Initialize()
                // Fail fast - do not execute callbacks on DB thread (violates world thread contract)
                log.Fatal("[SAVESCHEDULER] CRITICAL: EnqueueToWorldThread not set during force clear! Cannot invoke callbacks safely. This indicates WorldManager.Initialize() was not called or failed.");
                throw new InvalidOperationException("EnqueueToWorldThread delegate not set - cannot safely invoke callbacks on world thread. This indicates a fatal initialization error in WorldManager.Initialize().");
            }
        }

        /// <summary>
        /// Watchdog loop that periodically scans for stuck saves.
        /// </summary>
        private void WatchdogLoop()
        {
            while (!_disposed)
            {
                try
                {
                    var now = DateTime.UtcNow;

                    // First pass: detect stuck saves and accumulate counts
                    int stuckPrio0 = 0;
                    int stuckPrio1 = 0;
                    int stuckPrio2 = 0;

                    foreach (var kvp in _states)
                    {
                        var key = kvp.Key;
                        var state = kvp.Value;

                        // Skip if not queued or executing (Queued and Executing are volatile ints: 0 or 1)
                        if (state.Queued == 0 && state.Executing == 0)
                            continue;

                        var firstEnqTicks = Volatile.Read(ref state.FirstEnqueuedTicksUtc);
                        if (firstEnqTicks == 0)
                            continue; // Never enqueued, skip

                        var firstEnq = new DateTime(firstEnqTicks, DateTimeKind.Utc);
                        var age = (now - firstEnq).TotalSeconds;

                        if (age >= _stuckThreshold.TotalSeconds)
                        {
                            // Count stuck saves by priority
                            switch (state.Priority)
                            {
                                case 0: stuckPrio0++; break;
                                case 1: stuckPrio1++; break;
                                case 2: stuckPrio2++; break;
                            }

                            // Still log individual critical/atomic saves, but less frequently
                            if (state.Priority <= 1)
                            {
                                var lastLogTicks = Volatile.Read(ref state.LastStuckLogTicksUtc);
                                var lastLog = lastLogTicks != 0 ? new DateTime(lastLogTicks, DateTimeKind.Utc) : DateTime.MinValue;
                                var timeSinceLastLog = (now - lastLog).TotalSeconds;

                                if (timeSinceLastLog >= 30.0)
                                {
                                    LogStuck(key, state);
                                    Volatile.Write(ref state.LastStuckLogTicksUtc, now.Ticks);
                                }
                            }
                        }
                    }

                    // Update running totals
                    _stuckSaveCountPrio0 = stuckPrio0;
                    _stuckSaveCountPrio1 = stuckPrio1;
                    _stuckSaveCountPrio2 = stuckPrio2;

                    // Log coalesced summary periodically
                    if ((now - _lastStuckSummaryLogUtc).TotalSeconds >= StuckSummaryIntervalSeconds)
                    {
                        if (stuckPrio0 > 0 || stuckPrio1 > 0 || stuckPrio2 > 0)
                        {
                            var dbQueueCount = DatabaseManager.Shard?.QueueCount ?? 0;
                            log.Warn($"[SAVESCHEDULER] Stuck save summary: Atomic={stuckPrio0} Critical={stuckPrio1} Periodic={stuckPrio2} | " +
                                     $"Queues: atomicQ={_atomicQueue.Count} criticalQ={_criticalQueue.Count} periodicQ={_periodicQueue.Count} dbQ={dbQueueCount}");
                        }
                        _lastStuckSummaryLogUtc = now;
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
                            var callbacks = cs.ForceClearIfNoWorkAndDrainCallbacks();
                            TryRemoveIdleCharacterSaveState(charId);
                            if (callbacks != null)
                            {
                                InvokeCallbacks(charId, callbacks);
                            }
                        }
                    }

                    // Third pass: cleanup idle SaveState entries
                    // Only remove states that are completely idle (queued=0, executing=0, dirty=0)
                    // and have been idle for at least 5 minutes to avoid race conditions
                    // where RequestSave has a reference but state is deleted
                    // Rate limit this cleanup to every 60 seconds to reduce GC pressure
                    if ((now - _lastIdleStateCleanupUtc).TotalSeconds >= IdleStateCleanupIntervalSeconds)
                    {
                        try
                        {
                            const int IdleStateCleanupAgeSeconds = 300; // 5 minutes

                            var keysToRemove = new List<string>();

                            foreach (var kvp in _states)
                            {
                                var key = kvp.Key;
                                var state = kvp.Value;

                                // Read volatile fields to establish memory barrier
                                var queued = Volatile.Read(ref state.Queued);
                                var executing = Volatile.Read(ref state.Executing);
                                var dirty = Volatile.Read(ref state.Dirty);

                                // Only consider for removal if completely idle
                                if (queued == 0 && executing == 0 && dirty == 0)
                                {
                                    // Check if state has been idle long enough
                                    // Use EnqueuedTicksUtc if available, otherwise FirstEverEnqueuedTicksUtc
                                    var lastActivityTicks = Volatile.Read(ref state.EnqueuedTicksUtc);
                                    if (lastActivityTicks == 0)
                                        lastActivityTicks = Volatile.Read(ref state.FirstEverEnqueuedTicksUtc);

                                    if (lastActivityTicks == 0)
                                        continue; // Never enqueued, skip

                                    var lastActivity = new DateTime(lastActivityTicks, DateTimeKind.Utc);
                                    var idleTime = (now - lastActivity).TotalSeconds;

                                    if (idleTime >= IdleStateCleanupAgeSeconds)
                                    {
                                        keysToRemove.Add(key);
                                    }
                                }
                            }

                            // Remove idle states (outside enumeration to avoid modification during iteration)
                            foreach (var key in keysToRemove)
                            {
                                _states.TryRemove(key, out _);
                            }

                            if (keysToRemove.Count > 0)
                            {
                                log.Debug($"[SAVESCHEDULER] Watchdog cleaned up {keysToRemove.Count} idle SaveState entries");
                            }
                            _lastIdleStateCleanupUtc = now;
                        }
                        catch (Exception cleanupEx)
                        {
                            log.Error("[SAVESCHEDULER] Watchdog idle state cleanup threw exception", cleanupEx);
                        }
                    }

                    // Fourth pass: cleanup idle coalesce trackers in SerializedShardDatabase
                    // This prevents unbounded growth of the _coalesce dictionary
                    // Run every 30-60 seconds (using 45 seconds as a middle ground)
                    if ((now - _lastCoalesceCleanupUtc).TotalSeconds >= CoalesceCleanupIntervalSeconds)
                    {
                        try
                        {
                            // Cleanup trackers that have been idle for 10 minutes
                            DatabaseManager.Shard?.CleanupIdleTrackers(TimeSpan.FromMinutes(10));
                            _lastCoalesceCleanupUtc = now;
                        }
                        catch (Exception cleanupEx)
                        {
                            log.Error("[SAVESCHEDULER] Watchdog coalesce cleanup threw exception", cleanupEx);
                            // Continue watchdog loop even if cleanup fails
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
            var now = DateTime.UtcNow;
            var firstEnqTicks = Volatile.Read(ref state.FirstEnqueuedTicksUtc);
            var firstEverEnqTicks = Volatile.Read(ref state.FirstEverEnqueuedTicksUtc);
            var lastEnqTicks = Volatile.Read(ref state.EnqueuedTicksUtc);
            var firstAge = firstEnqTicks != 0 ? (now - new DateTime(firstEnqTicks, DateTimeKind.Utc)).TotalSeconds : 0;
            var firstEverAge = firstEverEnqTicks != 0 ? (now - new DateTime(firstEverEnqTicks, DateTimeKind.Utc)).TotalSeconds : 0;
            var lastAge = lastEnqTicks != 0 ? (now - new DateTime(lastEnqTicks, DateTimeKind.Utc)).TotalSeconds : 0;

            var currentGen = Volatile.Read(ref state.Generation);
            var lastProcessedGen = Volatile.Read(ref state.LastProcessedGen);
            var genLag = currentGen - lastProcessedGen;

            // firstEnqueued = progress detector (resets on requeue, shows how long since last progress)
            // firstEverEnqueued = hot key detector (never resets, shows total time key has been active)
            log.Warn($"[SAVESCHEDULER] Stuck save detected key={key} prio={state.Priority} executing={state.Executing} " +
                     $"firstEnqueued={firstAge:F1}s ago (progress) firstEverEnqueued={firstEverAge:F1}s ago (hot key) lastEnqueued={lastAge:F1}s ago " +
                     $"gen={currentGen} lastProcessedGen={lastProcessedGen} genLag={genLag} " +
                     $"atomicQ={_atomicQueue.Count} criticalQ={_criticalQueue.Count} periodicQ={_periodicQueue.Count} periodicPlayerQ={_periodicPlayerQueue.Count}");
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
