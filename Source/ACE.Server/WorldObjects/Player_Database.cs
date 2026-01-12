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
        public readonly ReaderWriterLockSlim CharacterDatabaseLock = new ReaderWriterLockSlim();

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

        private readonly object _saveGateLock = new object();

        // Next time we are allowed to enqueue a player save (biota and character)
        private DateTime _nextAllowedSaveUtc = DateTime.MinValue;

        // When a normal save request is blocked by the gate, remember we owe a save
        private bool _saveOwed = false;

        // Used only for logging or diagnostics
        private DateTime _lastSaveRequestUtc = DateTime.MinValue;

        // Last time EnsureSaveIfOwed was called (for rate limiting)
        private DateTime _lastEnsureSaveCheckUtc = DateTime.MinValue;

        // Debounced save for player-to-player gives
        private int _giveSaveChainCounter = 0;
        private int _lastCompletedGiveSave = 0;
        
        // Flag to prevent delayed saves from executing on destroyed/logged-out players
        private volatile bool _isShuttingDownOrOffline = false;

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
            
            // Use SaveScheduler with a unique key per player for coalescing
            // Architecture: Gameplay → SaveScheduler → SerializedShardDatabase → _uniqueQueue → DB
            var saveKey = $"give:{Guid}";
            
            // Schedule the save with delay using Task
            Task.Delay(TimeSpan.FromSeconds(ForcedShortWindowSeconds)).ContinueWith(_ =>
            {
                if (_isShuttingDownOrOffline || ACE.Server.Managers.ServerManager.ShutdownInProgress)
                    return;

                // Only execute if this is still the latest save request
                if (thisSaveChainNumber > _lastCompletedGiveSave)
                {
                    _lastCompletedGiveSave = thisSaveChainNumber;
                    SavePlayerToDatabase(reason: SaveReason.ForcedShortWindow);
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
        /// Ensures a save happens if one is owed, even if the player stops generating events.
        /// This prevents blocked save requests from getting stuck forever.
        /// Rate limited to prevent thrashing if called too frequently.
        /// Sets next allowed time to now so the next Normal save can pass immediately,
        /// and ShouldEnqueueSave will then re-arm the interval correctly.
        /// </summary>
        public void EnsureSaveIfOwed()
        {
            var now = DateTime.UtcNow;

            lock (_saveGateLock)
            {
                if (!_saveOwed)
                    return;

                if ((now - _lastEnsureSaveCheckUtc).TotalSeconds < 2)
                    return;

                _lastEnsureSaveCheckUtc = now;

                // Allow a save now by setting next allowed time to current time
                // The next Normal save will pass immediately, and ShouldEnqueueSave will
                // then re-arm the interval correctly (setting it to now + PlayerSaveIntervalSecs)
                _nextAllowedSaveUtc = now;
                _saveOwed = false;
            }

            SavePlayerToDatabase(reason: SaveReason.Normal);
        }

        /// <summary>
        /// Determines whether a save should be enqueued based on the save reason and gating logic.
        /// </summary>
        private bool ShouldEnqueueSave(SaveReason reason, out DateTime requestedTimeUtc)
        {
            requestedTimeUtc = DateTime.UtcNow;

            lock (_saveGateLock)
            {
                _lastSaveRequestUtc = requestedTimeUtc;

                if (reason == SaveReason.ForcedImmediate)
                {
                    _saveOwed = false;
                    _nextAllowedSaveUtc = requestedTimeUtc.AddSeconds(PlayerSaveIntervalSecs);
                    return true;
                }

                if (reason == SaveReason.ForcedShortWindow)
                {
                    // Pull the next allowed time forward, but do not force an immediate burst
                    var target = requestedTimeUtc.AddSeconds(ForcedShortWindowSeconds);
                    if (_nextAllowedSaveUtc > target)
                        _nextAllowedSaveUtc = target;

                    // If we can save now, do it, otherwise mark owed
                    if (requestedTimeUtc >= _nextAllowedSaveUtc)
                    {
                        _saveOwed = false;
                        _nextAllowedSaveUtc = requestedTimeUtc.AddSeconds(PlayerSaveIntervalSecs);
                        return true;
                    }

                    _saveOwed = true;
                    return false;
                }

                // Normal
                if (requestedTimeUtc < _nextAllowedSaveUtc)
                {
                    _saveOwed = true;
                    return false;
                }

                _saveOwed = false;
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

            if (!ShouldEnqueueSave(reason, out var requestedTime))
            {
                return;
            }

            // Now that we have decided to enqueue, keep character and biota in step
            if (CharacterChangesDetected)
                SaveCharacterToDatabaseInternal(duringLogout);

            var requestedTimeUtc = requestedTime;

            // Track which possessions were actually prepared for this batch save
            // This prevents clearing flags for items that were already SaveInProgress from another save
            // IMPORTANT: This must be captured by reference, not cloned, so getBiotas can populate it
            var preparedGuidsForCallback = new HashSet<uint>();
            
            // Track if preparation failed - if true, callback should treat result as false
            // This prevents clearing ChangesDetected when nothing was actually saved
            // Using array to enable Volatile operations for thread-safe cross-thread visibility
            // (Cannot use ref on captured local, so array element provides ref-able location)
            bool[] preparationFailed = new bool[1] { false };
            
            // Func that builds the biotas list at execution time to avoid stale snapshots
            // 
            // CRITICAL ARCHITECTURAL CONSTRAINT:
            // This function has side effects - it calls SaveBiotaToDatabase(false) which:
            // - Sets SaveInProgress = true
            // - Clears ChangesDetected = false
            // - Modifies biota state
            // 
            // This function MUST only be called exactly once per execution by the coalescer.
            // If called multiple times (due to refactoring, retries, metrics, etc.), you get:
            // - SaveInProgress set multiple times
            // - ChangesDetected cleared prematurely
            // - Double side effects
            // 
            // Player code cannot enforce this contract - it relies on SerializedShardDatabase
            // calling it exactly once. If the DB layer changes, this assumption may break.
            //
            // EXCEPTION SAFETY: If this function throws, it will clean up all flags it set
            // to prevent stranded SaveInProgress flags and lost ChangesDetected signals.
            Func<IEnumerable<(ACE.Entity.Models.Biota biota, ReaderWriterLockSlim rwLock)>> getBiotas = () =>
            {
                var biotas = new Collection<(Biota biota, ReaderWriterLockSlim rwLock)>();
                bool playerPrepared = false;

                try
                {
                    // Save player biota - this sets SaveInProgress for the player
                    SaveBiotaToDatabase(false);
                    playerPrepared = true;
                    biotas.Add((Biota, BiotaDatabaseLock));

                    // Get all possessions and prepare them for batch save
                    var allPossessions = GetAllPossessions();
                    
                    // For possessions, prepare them for batch save
                    // Track items BEFORE calling SaveBiotaToDatabase to ensure cleanup on exception
                    foreach (var possession in allPossessions)
                    {
                        if (possession.ChangesDetected)
                        {
                            // Track this item BEFORE calling SaveBiotaToDatabase
                            // If SaveBiotaToDatabase throws after setting flags, we still have it tracked
                            var possessionGuid = possession.Guid.Full;
                            bool wasAlreadyInProgress = possession.SaveInProgress;
                            
                            // Add to tracking set BEFORE the call that might throw
                            // If SaveInProgress was already true, SaveBiotaToDatabase will return early
                            // and we'll remove it from tracking below
                            preparedGuidsForCallback.Add(possessionGuid);
                            
                            try
                            {
                                // Sync position cache and prepare biota for save
                                // This sets SaveInProgress = true (but doesn't clear ChangesDetected when enqueueSave=false)
                                // But it may return early if SaveInProgress was already true
                                possession.SaveBiotaToDatabase(false);
                                
                                // Check if SaveInProgress was actually set by this call
                                if (possession.SaveInProgress && !wasAlreadyInProgress)
                                {
                                    // Successfully prepared - add to biotas list
                                    biotas.Add((possession.Biota, possession.BiotaDatabaseLock));
                                }
                                else if (wasAlreadyInProgress)
                                {
                                    // Was already in progress, SaveBiotaToDatabase returned early
                                    // Remove from tracking since we didn't actually prepare it
                                    preparedGuidsForCallback.Remove(possessionGuid);
                                }
                            }
                            catch
                            {
                                // Keep it tracked so outer cleanup can clear flags if they were partially set
                                // If SaveBiotaToDatabase threw after setting SaveInProgress=true, we need
                                // the guid in preparedGuidsForCallback so the outer catch can clear it
                                throw; // Re-throw to trigger outer cleanup
                            }
                        }
                    }

                    return biotas;
                }
                catch (Exception ex)
                {
                    // CRITICAL: Clean up all flags set during preparation
                    // If we don't do this, SaveInProgress will be stuck and items will appear to be saving forever
                    // Note: SaveBiotaToDatabase(false) doesn't clear ChangesDetected, so that's preserved automatically
                    log.Error($"[SAVE] getBiotas threw exception for {Name} (0x{Guid}), cleaning up flags", ex);
                    
                    // Mark preparation as failed so callback treats result as false
                    // Use Volatile.Write for thread-safe cross-thread visibility
                    Volatile.Write(ref preparationFailed[0], true);
                    
                    // Clear player flags if we set them
                    if (playerPrepared)
                    {
                        SaveInProgress = false;
                        SaveStartTime = DateTime.MinValue;
                        // Note: SaveBiotaToDatabase(false) doesn't clear ChangesDetected, so no need to restore
                    }
                    
                    // Clear possession flags for all items we prepared
                    try
                    {
                        var currentPossessions = GetAllPossessions();
                        foreach (var possession in currentPossessions)
                        {
                            if (!possession.IsDestroyed && preparedGuidsForCallback.Contains(possession.Guid.Full))
                            {
                                possession.SaveInProgress = false;
                                possession.SaveStartTime = DateTime.MinValue;
                                // Note: SaveBiotaToDatabase(false) doesn't clear ChangesDetected, so no need to restore
                            }
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        // Log but don't throw - we're in exception handler
                        log.Error($"[SAVE] Exception during getBiotas cleanup for {Name} (0x{Guid})", cleanupEx);
                    }
                    
                    // Clear tracking set
                    preparedGuidsForCallback.Clear();
                    
                    // Return empty collection - DB layer will handle the failure
                    return new Collection<(Biota biota, ReaderWriterLockSlim rwLock)>();
                }
            };
            
            // Common callback logic for both logout and non-logout saves
            Action<bool> saveCallback = (result) =>
            {
                try
                {
                    // If preparation failed, treat result as false to prevent clearing ChangesDetected
                    // when nothing was actually saved
                    // Use Volatile.Read for thread-safe cross-thread visibility
                    if (Volatile.Read(ref preparationFailed[0]))
                        result = false;
                    
                    var currentPossessions = GetAllPossessions();

                    foreach (var possession in currentPossessions)
                    {
                        if (!possession.IsDestroyed && preparedGuidsForCallback.Contains(possession.Guid.Full))
                        {
                            possession.SaveInProgress = false;
                            possession.SaveStartTime = DateTime.MinValue;

                            if (result)
                                possession.ChangesDetected = false;
                            else
                                possession.ChangesDetected = true;
                        }
                    }

                    if (result)
                    {
                        ChangesDetected = false;

                        if (duringLogout)
                        {
                            // SAFE: does not enqueue gameplay, only mutates dictionaries
                            PlayerManager.SwitchPlayerFromOnlineToOffline(this);
                        }
                    }
                    else
                    {
                        ChangesDetected = true;
                        BiotaSaveFailed = true;

                        if (!CharacterChangesDetected)
                            CharacterChangesDetected = true;

                        if (duringLogout)
                            PlayerManager.SwitchPlayerFromOnlineToOffline(this);
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"[SAVE] Player save callback failed for {Name} (0x{Guid})", ex);
                }
                finally
                {
                    // ALWAYS clear SaveInProgress, even if callback throws
                    SaveInProgress = false;
                    SaveStartTime = DateTime.MinValue;

                    // Clear possessions' SaveInProgress flags safely
                    try
                    {
                        var safePossessions = GetAllPossessions();
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
            };

            // Use SaveScheduler to schedule the database save
            // Architecture: Gameplay → SaveScheduler → SerializedShardDatabase → _uniqueQueue → DB
            var saveKey = duringLogout ? SaveKeys.Logout(Character.Id) : SaveKeys.Player(Character.Id);
            SaveScheduler.Instance.RequestSave(saveKey, ACE.Database.SaveScheduler.SaveType.Periodic, () =>
            {
                // This runs on SaveScheduler worker thread
                // Call SerializedShardDatabase method which enqueues to _uniqueQueue
                DatabaseManager.Shard.SavePlayerBiotasCoalesced(
                    Guid.Full,
                    getBiotas,
                    saveCallback,
                    duringLogout ? $"logout:{Guid}" : Guid.ToString());
                return true; // Indicates successful enqueue to _uniqueQueue
            });
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
            CharacterChangesDetected = false;

            // Func that gets the current Character state at execution time
            Func<(ACE.Database.Models.Shard.Character character, ReaderWriterLockSlim rwLock)> getCharacter = () =>
                (Character, CharacterDatabaseLock);

            Action<bool> saveCallback = (result) =>
            {
                if (!result)
                    CharacterSaveFailed = true;
            };

            // Use SaveScheduler to schedule the database save
            // Architecture: Gameplay → SaveScheduler → SerializedShardDatabase → _uniqueQueue → DB
            var saveKey = SaveKeys.Character(Character.Id);
            SaveScheduler.Instance.RequestSave(saveKey, ACE.Database.SaveScheduler.SaveType.Periodic, () =>
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
}
