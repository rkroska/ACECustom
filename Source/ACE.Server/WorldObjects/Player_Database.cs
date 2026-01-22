using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ACE.Common;
using ACE.Database;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using log4net;
using LogManager = log4net.LogManager;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        public DateTime CharacterLastRequestedDatabaseSave { get; protected set; }

        /// <summary>
        /// This variable is set to true when a change is made, and set to false before a save is requested.<para />
        /// The primary use for this is to trigger save on add/modify/remove of properties.
        /// </summary>
        private bool _characterChangesDetected;
        public bool CharacterChangesDetected 
        { 
            get => _characterChangesDetected;
            set
            {
                _characterChangesDetected = value;
            }
        }

        /// <summary>
        /// Set to true when SaveCharacter() returns a failure
        /// </summary>
        public bool CharacterSaveFailed { get; set; }

        /// <summary>
        /// Set to true when SaveBiotaToDatabase() returns a failure
        /// </summary>
        public bool BiotaSaveFailed { get; set; }

        /// <summary>
        /// The time period between automatic saving of player character changes
        /// </summary>
        public long PlayerSaveIntervalSecs
        {
            get => ServerConfig.player_save_interval.Value;
        }

        /// <summary>
        /// Best practice says you should use this lock any time you read/write the Character.<para />
        /// <para />
        /// For absolute maximum performance, if you're willing to assume (and risk) the following:<para />
        ///  - that the character in the database will not be modified (in a way that adds or removes properties) outside of ACE while ACE is running with a reference to that character<para />
        ///  - that the character will only be read/modified by a single thread in ACE<para />
        /// You can remove the lock usage for any Get/GetAll Property functions. You would simply use it for Set/Remove Property functions because each of these could end up adding/removing to the collections.<para />
        /// The critical thing is that the collections are not added to or removed from while Entity Framework is iterating over them.<para />
        /// Mag-nus 2018-08-19
        /// </summary>
        public readonly ReaderWriterLockSlim CharacterDatabaseLock = new();

        public enum SaveReason
        {
            Normal = 0,
            ForcedImmediate = 1,
            ForcedShortWindow = 2,
        }

        /// <summary>
        /// Time window in seconds for ForcedShortWindow saves.
        /// Allows coalescing of rapid save requests while ensuring saves complete within this window.
        /// </summary>
        private const int ForcedShortWindowSeconds = 4;

        private readonly object _saveGateLock = new();

        // Next time we are allowed to enqueue a player save (biota and character)
        private DateTime _nextAllowedSaveUtc = DateTime.MinValue;


        // Debounced save for player-to-player gives
        private int _giveSaveChainCounter = 0;
        private int _lastCompletedGiveSave = 0;
        
        // Flag to prevent delayed saves from executing on destroyed/logged-out players
        private volatile bool _isShuttingDownOrOffline = false;

        // Logout save completion tracking
        private readonly ManualResetEventSlim _logoutSaveCompleted = new(false);
        private int _logoutSaveStarted; // 0 or 1
        private volatile bool _logoutPending = false; // Set when logout save is requested, cleared when logout completes

        /// <summary>
        /// Returns true if the logout save has completed.
        /// Used to gate character select screen until save is complete.
        /// </summary>
        public bool IsLogoutSaveComplete => _logoutSaveCompleted.IsSet;

        /// <summary>
        /// Signals that the logout save has completed.
        /// Called from the save callback to unblock character select screen.
        /// </summary>
        internal void SignalLogoutSaveComplete()
        {
            _logoutSaveCompleted.Set();
        }

        /// <summary>
        /// Begins the logout save process. Marks player immutable and fires immediate save.
        /// This must be called at the start of logout/disconnect to prevent item loss.
        /// Thread-safe: Uses Interlocked to prevent double-firing.
        /// </summary>
        public void BeginLogoutSave()
        {
            // Prevent double-firing - only one thread can start the logout save
            if (Interlocked.Exchange(ref _logoutSaveStarted, 1) != 0)
                return;

            // Mark the player immutable - blocks delayed give saves, mutation chains, deferred inventory operations
            _isShuttingDownOrOffline = true;
            
            // Reset completion signal - will be set when save callback completes
            _logoutSaveCompleted.Reset();
            
            // Set logout pending flag - callback will check this even if request gets coalesced
            _logoutPending = true;
            
            // Fire the save immediately - must happen before session cleanup, world removal, offline transfer
            // Uses same save key as normal saves so coalescing works correctly
            SavePlayerToDatabase(duringLogout: true, reason: SaveReason.ForcedImmediate);
        }

        /// <summary>
        /// Schedules a debounced save for player-to-player gives.
        /// If another give happens within 4 seconds, the timer resets.
        /// Save happens 4 seconds after the last give.
        /// Uses SaveScheduler instead of ActionChain.
        /// </summary>
        private void ScheduleDebouncedGiveSave()
        {
            // Increment counter to track save requests
            var thisSaveChainNumber = Interlocked.Increment(ref _giveSaveChainCounter);
            
            // Mark player as dirty
            CharacterChangesDetected = true;
            
            // Schedule the save with delay using Task
            // CRITICAL: The continuation runs on a threadpool thread, but SavePlayerToDatabase()
            // must run on the world thread because it does prep work (GetAllPossessions(), SaveBiotaToDatabase(), etc.)
            Task.Delay(TimeSpan.FromSeconds(ForcedShortWindowSeconds)).ContinueWith(_ =>
            {
                if (_isShuttingDownOrOffline || ACE.Server.Managers.ServerManager.ShutdownInProgress)
                    return;

                // Only execute if this is still the latest save request
                // Use Interlocked.CompareExchange to prevent race condition where two delayed tasks
                // both pass the check before either assigns _lastCompletedGiveSave, causing double fires
                var observed = Volatile.Read(ref _lastCompletedGiveSave);
                if (thisSaveChainNumber > observed &&
                    Interlocked.CompareExchange(
                        ref _lastCompletedGiveSave,
                        thisSaveChainNumber,
                        observed) == observed)
                {
                    // CRITICAL: Route to world thread - SavePlayerToDatabase() assumes world thread context
                    // This prevents deadlocks and inventory snapshot races when called from threadpool
                    if (SaveScheduler.EnqueueToWorldThread != null)
                    {
                        SaveScheduler.EnqueueToWorldThread(() =>
                            SavePlayerToDatabase(reason: SaveReason.ForcedShortWindow));
                    }
                    else
                    {
                        // CRITICAL ERROR: EnqueueToWorldThread should always be set by WorldManager.Initialize()
                        log.Error("[SAVE] CRITICAL: EnqueueToWorldThread not set! Debounced give save cannot safely execute. This indicates WorldManager.Initialize() was not called or failed.");
                    }
                }
            }, TaskScheduler.Default);
        }

        private void SetPropertiesAtLogOut()
        {
            LogoffTimestamp = Time.GetUnixTime();
            // These properties are used with offline players to determine passup rates
            SetProperty(PropertyInt.CurrentLoyaltyAtLastLogoff, (int)GetCreatureSkill(Skill.Loyalty).Current);
            SetProperty(PropertyInt.CurrentLeadershipAtLastLogoff, (int)GetCreatureSkill(Skill.Leadership).Current);
        }

        /// <summary>
        /// This will make sure a player save happens no later than the current time + seconds
        /// </summary>
        public void RushNextPlayerSave(int seconds)
        {
            if (LastRequestedDatabaseSave.AddSeconds(PlayerSaveIntervalSecs) <= DateTime.UtcNow.AddSeconds(seconds))
                return;

            LastRequestedDatabaseSave = DateTime.UtcNow.AddSeconds(seconds).AddSeconds(-1 * PlayerSaveIntervalSecs);
        }


        /// <summary>
        /// Determines whether a save should be enqueued based on the save reason and gating logic.
        /// </summary>
        private bool ShouldEnqueueSave(SaveReason reason, out DateTime requestedTimeUtc)
        {
            requestedTimeUtc = DateTime.UtcNow;

            lock (_saveGateLock)
            {
                if (reason == SaveReason.ForcedImmediate)
                {
                    _nextAllowedSaveUtc = requestedTimeUtc.AddSeconds(PlayerSaveIntervalSecs);
                    return true;
                }

                if (reason == SaveReason.ForcedShortWindow)
                {
                    // Pull the next allowed time forward, but do not force an immediate burst
                    var target = requestedTimeUtc.AddSeconds(ForcedShortWindowSeconds);
                    if (_nextAllowedSaveUtc > target)
                        _nextAllowedSaveUtc = target;

                    // If we can save now, do it
                    if (requestedTimeUtc >= _nextAllowedSaveUtc)
                    {
                        _nextAllowedSaveUtc = requestedTimeUtc.AddSeconds(PlayerSaveIntervalSecs);
                        return true;
                    }

                    return false;
                }

                // Normal
                if (requestedTimeUtc < _nextAllowedSaveUtc)
                {
                    return false;
                }

                _nextAllowedSaveUtc = requestedTimeUtc.AddSeconds(PlayerSaveIntervalSecs);
                return true;
            }
        }

        /// <summary>
        /// Saves the character to the persistent database. Includes Stats, Position, Skills, etc.<para />
        /// Will also save any possessions that are marked with ChangesDetected.
        /// 
        /// ARCHITECTURAL LIMITATIONS (not bugs, but inherent constraints):
        /// 
        /// A. Save overlap is still possible in principle:
        ///    - SaveInProgress is advisory, not a hard lock
        ///    - Overlap is tolerated but tracked (wasAlreadyInProgress check)
        ///    - SerializedShardDatabase prevents overlap at DB level, but Player-level cannot
        /// 
        /// B. Dirty during execution is probabilistic:
        ///    - If a possession mutates after getBiotas runs but before save finishes,
        ///      correctness depends on ChangesDetected being set later and another save being generated
        ///    - Recovery is improved but not guaranteed at Player level
        ///    - DB coalescer can handle this better
        /// 
        /// C. Correctness depends on getBiotas being called exactly once:
        ///    - getBiotas has side effects (SaveInProgress, ChangesDetected)
        ///    - If refactored/retried/called multiple times, double side effects occur
        ///    - Player code cannot enforce this contract, only document it
        /// 
        /// D. Character and biota are coordinated manually:
        ///    - Character save requested separately from biota save
        ///    - Ordering relies on call sequencing, not structural guarantees
        ///    - Fixes reduce divergence risk but don't eliminate it structurally
        ///    - Only unified DB execution path could fully solve this
        /// 
        /// E. Gameplay thread participates in correctness:
        ///    - Relies on ActionChain execution, WorldManager.ActionQueue, player tick recovery
        ///    - If those stall, saves stall
        ///    - Not fully fixable at Player level
        /// </summary>
        public void SavePlayerToDatabase(bool duringLogout = false, SaveReason reason = SaveReason.Normal)
        {
            // Logout always forces immediate
            if (duringLogout)
                reason = SaveReason.ForcedImmediate;

            // CRITICAL: Do not allow "clean" periodic saves to run getBiotas
            // getBiotas has side effects (clears flags, sets SaveInProgress) and running it when clean
            // can mask later correctness and make a real change look like it never saved.
            // 
            // IMPORTANT: Check this BEFORE ShouldEnqueueSave to avoid consuming the gate state
            // when we're going to no-op. If we check after, we advance _nextAllowedSaveUtc without
            // actually enqueuing, which blocks later real changes from saving.
            // Logout saves must always run (even if clean) to ensure final snapshot is captured.
            if (!duringLogout && !ChangesDetected && !CharacterChangesDetected)
                return;

            // CRITICAL: Prevent consuming the gate while player is already saving
            // This avoids burning the gate window without actually enqueuing a save
            // Logout saves must always proceed even if already saving to ensure final snapshot
            // 
            // NOTE: This early return prevents fast followups (changes during execution won't trigger
            // immediate requeue - will wait for next periodic tick). However, SaveScheduler already
            // protects against job overwrite (preserves job when Queued=1, Executing=0), so this
            // early return is primarily a gate optimization, not a correctness requirement.
            // CRITICAL: Only ForcedImmediate bypasses SaveInProgress (used for logout snapshots)
            // ForcedShortWindow now respects SaveInProgress to prevent race conditions
            // It maintains urgency via the 4-second window in ShouldEnqueueSave
            bool isHighPrioritySave = (reason == SaveReason.ForcedImmediate);
            if (!duringLogout && !isHighPrioritySave && SaveInProgress)
                return;

            if (!ShouldEnqueueSave(reason, out var requestedTime))
            {
                return;
            }

            // CRITICAL: During logout, save character and biota atomically in the same job
            // This ensures logout completion waits for both saves, providing a fully consistent snapshot
            // For normal saves, character and biota can save separately (character may save first)
            // HARDENING: Logout always includes character save (even if clean) to ensure final snapshot is captured
            bool characterNeedsSave = CharacterChangesDetected;
            bool includeCharacterSaveInJob = duringLogout; // Always true on logout - final snapshot must include character
            if (duringLogout && includeCharacterSaveInJob)
            {
                // Character save will be included in the player biota save job during logout
                // Skip separate character save call to ensure atomic completion
            }
            else if (characterNeedsSave)
            {
                // Normal save: character and biota can save separately
                SaveCharacterToDatabaseInternal(duringLogout);
            }

            // CRITICAL: All prep work must happen on the world thread, not the DB worker thread
            // This prevents deadlocks and world thread stalls when getBiotas() runs on DB worker
            // Build the biotas collection and prepare everything before passing to DB layer
            
            // Track which possessions were actually prepared for this batch save
            // This prevents clearing flags for items that were already SaveInProgress from another save
            var preparedGuidsForCallback = new HashSet<uint>();
            
            // Track whether THIS job actually started the player save
            // This prevents clearing SaveInProgress if the player was already saving from another request
            bool preparedPlayerForCallback = false;
            
            // Snapshot inventory on world thread to prevent race condition
            // Force materialization with .ToList() to ensure we have a concrete snapshot
            var possessionsSnapshot = GetAllPossessions().ToList();
            
            // Build biotas collection on world thread (not DB worker thread)
            var biotas = new Collection<(Biota biota, ReaderWriterLockSlim rwLock)>();

            // Prepare player biota on world thread
            // CRITICAL: During logout, always attempt to save player biota to ensure final snapshot is captured
            // For normal saves, only add to biotas and mark as prepared if THIS job actually started the save
            // If SaveInProgress was already true on normal saves, another save is in flight and we should not clear its flags
            if (duringLogout)
            {
                // Check if save was already in progress before attempting to save
                // Only mark as prepared if THIS job actually started the save
                var wasAlreadySavingPlayer = SaveInProgress;
                
                // FIX #4: Clear ChangesDetected BEFORE SaveBiotaToDatabase (Clear-Before-Enqueue pattern)
                ChangesDetected = false;
                
                SaveBiotaToDatabase(false);
                if (!wasAlreadySavingPlayer && SaveInProgress)
                {
                    preparedPlayerForCallback = true;
                    biotas.Add((Biota, BiotaDatabaseLock));
                }
                else if (!SaveInProgress)
                {
                    // FIX #4: SaveBiotaToDatabase rejected (mutation guard, etc.) - restore flag
                    ChangesDetected = true;
                }
            }
            else
            {
                // Only ForcedImmediate bypasses SaveInProgress (used for logout critical snapshots)
                // ForcedShortWindow now respects SaveInProgress to prevent concurrent modification
                bool isHighPriority = (reason == SaveReason.ForcedImmediate);
                
                if (!SaveInProgress || isHighPriority)
                {
                    // FIX #4: Clear ChangesDetected BEFORE SaveBiotaToDatabase (Clear-Before-Enqueue pattern)
                    ChangesDetected = false;
                    
                    SaveBiotaToDatabase(false);
                    if (SaveInProgress)
                    {
                        preparedPlayerForCallback = true;
                        biotas.Add((Biota, BiotaDatabaseLock));
                    }
                    else
                    {
                        // FIX #4: SaveBiotaToDatabase rejected (mutation guard, etc.) - restore flag
                        ChangesDetected = true;
                    }
                }
            }

            // Prepare possession biotas on world thread
            // FIX #4: Clear ChangesDetected BEFORE adding to biotas (Clear-Before-Enqueue pattern)
            // If a mutation happens DURING the save, it will set ChangesDetected=true again, preventing loss
            foreach (var possession in possessionsSnapshot)
            {
                if (possession.IsDestroyed)
                    continue;
                
                if (!possession.ChangesDetected)
                    continue;

                // Skip if already saving to prevent noisy behavior and concurrency issues
                if (possession.SaveInProgress)
                    continue;

                var possessionGuid = possession.Guid.Full;

                // FIX #4: Clear ChangesDetected BEFORE SaveBiotaToDatabase (optimistic clear)
                // If save fails or new mutation occurs, flags will be restored/set
                possession.ChangesDetected = false;
                
                possession.SaveBiotaToDatabase(false);

                // Only add to preparedGuidsForCallback after confirming it was actually included in the batch
                // SaveBiotaToDatabase can early return for various reasons (container guard, destroyed, etc.)
                if (possession.SaveInProgress)
                {
                    preparedGuidsForCallback.Add(possessionGuid);
                    biotas.Add((possession.Biota, possession.BiotaDatabaseLock));
                }
                else
                {
                    // FIX #4: SaveBiotaToDatabase rejected the save (mutation guard, early return, etc.)
                    // Restore ChangesDetected since we cleared it optimistically but didn't actually save
                    possession.ChangesDetected = true;
                }
            }

            // CRITICAL: If nothing was actually prepared for DB, do not enqueue a save
            // This prevents "pretend save succeeded" where we clear dirty flags but nothing was written
            // During logout, we require at least the player biota to be prepared to ensure final snapshot is saved
            // 
            // WARNING: This return happens AFTER ShouldEnqueueSave() advanced _nextAllowedSaveUtc,
            // which means we consumed the gate window without actually saving. This can delay dirty data
            // until the next full interval. The early SaveInProgress check above helps prevent this.
            if (biotas.Count == 0)
            {
                if (duringLogout)
                {
                    // CRITICAL: Logout requires player biota to be saved - this should never happen
                    // If it does, it means SaveBiotaToDatabase failed to prepare the player biota
                    log.Error($"[SAVE] CRITICAL: Logout save prepared 0 biotas for {Name} (0x{Guid}). Player biota must be saved during logout. This may indicate SaveInProgress race condition or SaveBiotaToDatabase failure.");
                    
                    // CRITICAL: Even though save failed, we must complete logout to prevent player from hanging at character select
                    // Switch offline and signal completion so player can disconnect
                    if (_logoutPending)
                    {
                        _logoutPending = false;
                        PlayerManager.SwitchPlayerFromOnlineToOffline(this);
                        SignalLogoutSaveComplete();
                    }
                }
                else
                {
                    // Nothing actually prepared for DB, keep dirty flags and try later
                    // This can happen if SaveBiotaToDatabase() early returns (mutation guard, already saving, etc.)
                    // and all possessions also early return
                    log.Warn($"[SAVE] SavePlayerToDatabase prepared 0 biotas for {Name} (0x{Guid}) after gate approval. Save will retry later.");
                }
                return;
            }

            // getBiotas just returns the pre-built collection (runs on DB worker thread)
            // No world-thread work happens here - everything is already prepared
            IEnumerable<(Biota biota, ReaderWriterLockSlim rwLock)> getBiotas() => biotas;

            // CRITICAL: Callback runs on DB worker thread, so we must route all world state access to world thread
            // This prevents deadlocks and world thread stalls when DB worker touches live world objects
            void saveCallback(bool result)
            {
                // Capture result and logout intent in closure for world thread execution
                // Capture logout intent once to ensure completion signal always fires when logout was requested
                var saveResult = result;
                var wasLogoutPending = Volatile.Read(ref _logoutPending);

                if (SaveScheduler.EnqueueToWorldThread != null)
                {
                    SaveScheduler.EnqueueToWorldThread(() =>
                    {
                        try
                        {
                            var currentPossessions = GetAllPossessions();

                            foreach (var possession in currentPossessions)
                            {
                                if (!possession.IsDestroyed && preparedGuidsForCallback.Contains(possession.Guid.Full))
                                {
                                    possession.SaveInProgress = false;
                                    possession.SaveStartTime = DateTime.MinValue;

                                    // FIX #4: On failure, restore ChangesDetected to ensure retry
                                    // On success, DON'T modify ChangesDetected (preserves concurrent mutations)
                                    if (!saveResult)
                                        possession.ChangesDetected = true;
                                }
                            }

                            if (saveResult)
                            {
                                // FIX #4: DON'T clear ChangesDetected here (Clear-Before-Enqueue pattern)
                                // Flags were optimistically cleared before DoSave
                                // If mutations occurred during save, they already set ChangesDetected=true
                                // Clearing here would clobber those concurrent changes

                                // Check captured logout intent - works even if this callback is from a coalesced save
                                // This ensures logout completes even if the logout request got merged into an existing save
                                if (wasLogoutPending)
                                {
                                    // Already on world thread, can call directly
                                    PlayerManager.SwitchPlayerFromOnlineToOffline(this);
                                }
                            }
                            else
                            {
                                ChangesDetected = true;
                                BiotaSaveFailed = true;

                                if (!CharacterChangesDetected)
                                    CharacterChangesDetected = true;

                                // On save failure, still switch offline if logout was pending
                                // If this was a logout save, we must verify the logout completed effectively
                                if (wasLogoutPending || duringLogout)
                                {
                                    // Ensure we are removed from PlayerManager
                                    // This is the critical step that allows the player to log back in
                                    PlayerManager.SwitchPlayerFromOnlineToOffline(this);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Error($"[SAVE] Player save callback failed for {Name} (0x{Guid})", ex);
                        }
                        finally
                        {
                            // Always signal completion if logout was pending, even if callback logic throws
                            // This unblocks character select screen even on save failure
                            // Use captured value to ensure signal fires even if flag was cleared in try block
                            if (wasLogoutPending)
                            {
                                Volatile.Write(ref _logoutPending, false);
                                SignalLogoutSaveComplete();
                            }

                            // CRITICAL: Only clear SaveInProgress if THIS job actually started the player save
                            // This prevents clearing flags if the player was already saving from another request
                            if (preparedPlayerForCallback)
                            {
                                SaveInProgress = false;
                                SaveStartTime = DateTime.MinValue;
                            }

                            // Clear possessions' SaveInProgress flags safely (only for possessions this job prepared)
                            try
                            {
                                var safePossessions = GetAllPossessions().ToList();
                                foreach (var possession in safePossessions)
                                {
                                    if (!possession.IsDestroyed && preparedGuidsForCallback.Contains(possession.Guid.Full))
                                    {
                                        possession.SaveInProgress = false;
                                        possession.SaveStartTime = DateTime.MinValue;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // Log but don't throw - we're in finally block
                                log.Error($"[SAVE] Failed to clear possession SaveInProgress flags for {Name} (0x{Guid})", ex);
                            }
                        }
                    });
                }
                else
                {
                    // CRITICAL ERROR: EnqueueToWorldThread should always be set by WorldManager.Initialize()
                    // This fallback is unsafe and should never occur in production
                    log.Error("[SAVE] CRITICAL: EnqueueToWorldThread not set! Save callback cannot safely execute. This indicates WorldManager.Initialize() was not called or failed.");

                    // CRITICAL: Even if EnqueueToWorldThread is null (fatal mis-init), signal logout completion
                    // to prevent players from hanging at character select screen forever
                    // Use captured logout intent instead of duringLogout parameter since callback may be from coalesced save
                    if (wasLogoutPending)
                    {
                        Volatile.Write(ref _logoutPending, false);
                        SignalLogoutSaveComplete();
                    }
                }
            }

            // Always use the same player save key to allow coalescing
            // Use Critical save type during logout to ensure final snapshot is persisted
            // The _logoutPending flag ensures logout completes even if request gets coalesced into existing save
            var saveKey = SaveKeys.Player(Character.Id);
            var saveType = duringLogout ? ACE.Database.SaveScheduler.SaveType.Critical : ACE.Database.SaveScheduler.SaveType.Periodic;
            
            // Create save job that handles forced dirty marking for aging saves
            // This keeps SaveScheduler generic - it doesn't know about players or PlayerManager
            // During logout, include character save in the same job for atomic completion
            var saveJob = new PlayerSaveJob(this, getBiotas, saveCallback, duringLogout, duringLogout && characterNeedsSave);
            SaveScheduler.Instance.RequestSave(saveKey, saveType, saveJob);
        }

        /// <summary>
        /// Internal helper that saves character without gate check.
        /// Used when the gate has already been checked by the caller (e.g., from SavePlayerToDatabase).
        /// </summary>
        private void SaveCharacterToDatabaseInternal(bool duringLogout)
        {
            // Update timestamp when we request the save (after gate check passes)
            // The coalescer will eventually enqueue this (either immediately or as a followup),
            // so this timestamp represents when a save was requested, not necessarily when it completed.
            // Note: If coalescing delays the enqueue, the timestamp may be slightly ahead of actual execution.
            CharacterLastRequestedDatabaseSave = DateTime.UtcNow;
            // Note: CharacterChangesDetected is cleared in the callback on success, not here.
            // This matches biota handling and prevents clearing the flag if the save fails early
            // (before DB enqueue, SaveScheduler delay, or exception). If save fails, CharacterSaveFailed
            // will be set and aging saves will eventually retry.

            // Func that gets the current Character state at execution time
            (Database.Models.Shard.Character character, ReaderWriterLockSlim rwLock) getCharacter() =>
                (Character, CharacterDatabaseLock);

            // CRITICAL: Callback runs on DB worker thread, so we must route all world state access to world thread
            // This prevents deadlocks and world thread stalls when DB worker touches live world objects
            void saveCallback(bool result)
            {
                // Capture result in closure for world thread execution
                var saveResult = result;

                if (SaveScheduler.EnqueueToWorldThread != null)
                {
                    SaveScheduler.EnqueueToWorldThread(() =>
                    {
                        if (saveResult)
                        {
                            // Clear CharacterChangesDetected on successful save
                            // Note: If new mutations occurred during save execution, they will set CharacterChangesDetected=true again
                            // and coalescing/followup saves will handle them
                            CharacterChangesDetected = false;
                        }
                        else
                        {
                            CharacterSaveFailed = true;
                        }
                    });
                }
                else
                {
                    // CRITICAL ERROR: EnqueueToWorldThread should always be set by WorldManager.Initialize()
                    // This fallback is unsafe and should never occur in production
                    log.Error("[SAVE] CRITICAL: EnqueueToWorldThread not set! Character save callback cannot safely execute. This indicates WorldManager.Initialize() was not called or failed.");
                }
            }

            // Use Critical save type during logout to ensure final snapshot is persisted
            var saveKey = SaveKeys.Character(Character.Id);
            var saveType = duringLogout ? ACE.Database.SaveScheduler.SaveType.Critical : ACE.Database.SaveScheduler.SaveType.Periodic;
            SaveScheduler.Instance.RequestSave(saveKey, saveType, () =>
            {
                // This runs on SaveScheduler worker thread
                // Call SerializedShardDatabase method which enqueues to _uniqueQueue
                DatabaseManager.Shard.SaveCharacterCoalesced(
                    Character.Id,
getCharacter,
                    duringLogout ? null : saveCallback);
                return true; // Indicates successful enqueue to _uniqueQueue
            });
        }

        /// <summary>
        /// Saves the character to the persistent database. Includes Stats, Position, Skills, etc.
        /// This method checks the save gate and should be used for direct calls (e.g., from logout).
        /// </summary>
        public void SaveCharacterToDatabase(bool duringLogout = false, SaveReason reason = SaveReason.Normal)
        {
            if (duringLogout)
                reason = SaveReason.ForcedImmediate;

            // Use the same gate so character cannot get ahead of biota
            if (!ShouldEnqueueSave(reason, out _))
                return;

            SaveCharacterToDatabaseInternal(duringLogout);
        }

        /// <summary>
        /// Override Destroy to set shutdown flag before destruction
        /// </summary>
        public override void Destroy(bool raiseNotifyOfDestructionEvent = true, bool fromLandblockUnload = false)
        {
            _isShuttingDownOrOffline = true;
            base.Destroy(raiseNotifyOfDestructionEvent, fromLandblockUnload);
        }
    }

    /// <summary>
    /// Save job for player biotas that handles forced dirty marking for aging saves.
    /// This keeps SaveScheduler generic - it doesn't know about players or PlayerManager.
    /// The dirty marking logic stays in ACE.Server where Player knowledge belongs.
    /// </summary>
    internal sealed class PlayerSaveJob(
        Player player,
        Func<IEnumerable<(Biota biota, ReaderWriterLockSlim rwLock)>> getBiotas,
        Action<bool> saveCallback,
        bool duringLogout,
        bool includeCharacterSave = false) : IForcedPeriodicSaveJob, ICancellableSaveJob
    {
        private readonly Player _player = player ?? throw new ArgumentNullException(nameof(player));
        private readonly Func<IEnumerable<(ACE.Entity.Models.Biota biota, ReaderWriterLockSlim rwLock)>> _getBiotas = getBiotas ?? throw new ArgumentNullException(nameof(getBiotas));
        private readonly Action<bool> _saveCallback = saveCallback ?? throw new ArgumentNullException(nameof(saveCallback));
        private readonly bool _duringLogout = duringLogout;
        private readonly bool _includeCharacterSave = includeCharacterSave;
        private bool _forceDirty;
        // Two-stage completion latch for logout: wait for both character and biota saves
        private int _pendingSaves = includeCharacterSave ? 2 : 0; // Counter for two-stage logout completion
        private bool _biotaSaveResult;
        private bool _characterSaveResult;
        private int _completionFired; // Guard to ensure callback fires only once (0 = not fired, 1 = fired)

        /// <summary>
        /// Forces the player to be marked as dirty before save execution.
        /// Called by ForcedPeriodicSaveJob when aging requires a save even if the player thinks it's clean.
        /// </summary>
        public void ForceDirty()
        {
            _forceDirty = true;
        }

        /// <summary>
        /// Executes the save operation.
        /// If ForceDirty() was called, marks the player as dirty before checking dirty flags.
        /// During logout with character save included, uses a two-stage latch to wait for both saves.
        /// </summary>
        public bool Execute()
        {
            // If forced dirty (aging save), mark player as dirty to ensure save executes
            // This prevents aging saves from becoming no-ops when dirty flags were cleared prematurely
            // or changes were masked by mutation logic
            if (_forceDirty)
            {
                _player.ChangesDetected = true;
                _player.CharacterChangesDetected = true;
                _forceDirty = false; // Clear flag to prevent accidental reuse if job instance is ever reused
            }

            // During logout with character save, use two-stage completion latch
            if (_includeCharacterSave)
            {
                // Save character first (runs on DB worker thread, callback on DB worker thread)
                (Database.Models.Shard.Character character, ReaderWriterLockSlim rwLock) getCharacter() =>
                    (_player.Character, _player.CharacterDatabaseLock);

                void characterCallback(bool result)
                {
                    _characterSaveResult = result;
                    // Thread-safe decrement and check if both saves are complete
                    var remaining = Interlocked.Decrement(ref _pendingSaves);
                    // HARDENING: Use CompareExchange to guarantee callback fires only once
                    if (remaining == 0 && Interlocked.CompareExchange(ref _completionFired, 1, 0) == 0)
                    {
                        // Both saves complete - invoke callback with combined result
                        // Both must succeed for overall success (consistent logout snapshot)
                        var combinedResult = _biotaSaveResult && _characterSaveResult;
                        _saveCallback?.Invoke(combinedResult);
                    }
                }

                // HARDENING: Wrap enqueue in try/catch to prevent hanging logout if enqueue fails
                try
                {
                    DatabaseManager.Shard.SaveCharacterCoalesced(
                        _player.Character.Id,
getCharacter,
characterCallback);
                }
                catch (Exception ex)
                {
                    // Force latch forward on failure to prevent hanging logout
                    // Log error via player's logger if available, otherwise use default
                    try
                    {
                        var playerLog = LogManager.GetLogger(typeof(Player));
                        playerLog.Error($"[SAVE] Logout character save enqueue failed for player {_player.Name} (0x{_player.Guid})", ex);
                    }
                    catch
                    {
                        // Fallback if logging fails
                    }
                    characterCallback(false);
                }
            }

            // Save biotas (always runs, even during logout)
            // During logout with character save, callback is deferred until character save completes
            Action<bool> biotaCallback;
            if (_includeCharacterSave)
            {
                // Two-stage completion: wait for both saves before invoking callback
                biotaCallback = (result) =>
                {
                    _biotaSaveResult = result;
                    // Thread-safe decrement and check if both saves are complete
                    var remaining = Interlocked.Decrement(ref _pendingSaves);
                    // HARDENING: Use CompareExchange to guarantee callback fires only once
                    if (remaining == 0 && Interlocked.CompareExchange(ref _completionFired, 1, 0) == 0)
                    {
                        // Both saves complete - invoke callback with combined result
                        // Both must succeed for overall success (consistent logout snapshot)
                        var combinedResult = _biotaSaveResult && _characterSaveResult;
                        _saveCallback?.Invoke(combinedResult);
                    }
                };
            }
            else
            {
                // Normal save: wrap callback with guard to prevent double invocation
                // This protects against race conditions where Cancel() fires during DB execution
                biotaCallback = (result) =>
                {
                    _biotaSaveResult = result;
                    // HARDENING: Use CompareExchange to guarantee callback fires only once
                    if (Interlocked.CompareExchange(ref _completionFired, 1, 0) == 0)
                    {
                        _saveCallback?.Invoke(result);
                    }
                };
            }

            // This runs on SaveScheduler worker thread
            // Call SerializedShardDatabase method which enqueues to _uniqueQueue
            // HARDENING: Wrap enqueue in try/catch to prevent hanging logout if enqueue fails
            try
            {
                DatabaseManager.Shard.SavePlayerBiotasCoalesced(
                    _player.Guid.Full,
                    _getBiotas,
                    biotaCallback,
                    _duringLogout ? $"logout:{_player.Guid}" : _player.Guid.ToString());
            }
            catch (Exception ex)
            {
                // Force latch forward on failure to prevent hanging logout
                // Log error via player's logger if available, otherwise use default
                try
                {
                    var playerLog = LogManager.GetLogger(typeof(Player));
                    playerLog.Error($"[SAVE] Logout biota save enqueue failed for player {_player.Name} (0x{_player.Guid})", ex);
                }
                catch
                {
                    // Fallback if logging fails
                }
                biotaCallback(false);
            }
            
            return true; // Indicates successful enqueue to _uniqueQueue
        }

        /// <summary>
        /// Cancels this job when it is replaced by a newer job.
        /// Invokes the callback with false to ensure SaveInProgress flags are cleaned up.
        /// Uses _completionFired guard to prevent double-invocation if Cancel() is called
        /// while the job is transitioning to execution.
        /// </summary>
        public void Cancel()
        {
            // CRITICAL: Use CompareExchange to ensure callback fires only once
            // If the job has already started executing (or was already cancelled), this will fail and we skip
            if (Interlocked.CompareExchange(ref _completionFired, 1, 0) == 0)
            {
                // Successfully claimed the callback slot - invoke with failure
                // This triggers the finally block in Player_Database.SavePlayerToDatabase
                // which clears SaveInProgress flags for this job
                try
                {
                    _saveCallback?.Invoke(false);
                }
                catch (Exception ex)
                {
                    // Log but don't throw - we're already in cleanup mode
                    var playerLog = LogManager.GetLogger(typeof(Player));
                    playerLog.Error($"[SAVE] PlayerSaveJob.Cancel() callback threw exception for player {_player.Name} (0x{_player.Guid})", ex);
                }
            }
        }
    }
}
