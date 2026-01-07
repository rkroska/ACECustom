using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

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

        /// <summary>
        /// Schedules a debounced save for player-to-player gives.
        /// If another give happens within 4 seconds, the timer resets.
        /// Save happens 4 seconds after the last give.
        /// </summary>
        private void ScheduleDebouncedGiveSave()
        {
            // Increment counter to cancel any pending save
            var thisSaveChainNumber = Interlocked.Increment(ref _giveSaveChainCounter);
            
            // Mark player as dirty
            CharacterChangesDetected = true;
            
            // Schedule save 4 seconds later
            var saveChain = new ActionChain();
            saveChain.AddDelaySeconds(ForcedShortWindowSeconds);
            saveChain.AddAction(WorldManager.ActionQueue, ActionType.ControlFlowDelay, () =>
            {
                // Only execute if this is still the latest save request
                if (thisSaveChainNumber > _lastCompletedGiveSave)
                {
                    _lastCompletedGiveSave = thisSaveChainNumber;
                    SavePlayerToDatabase(reason: SaveReason.ForcedShortWindow);
                }
            });
            saveChain.EnqueueChain();
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
            Func<IEnumerable<(ACE.Entity.Models.Biota biota, ReaderWriterLockSlim rwLock)>> getBiotas = () =>
            {
                var biotas = new Collection<(Biota biota, ReaderWriterLockSlim rwLock)>();

                // Save player biota - this sets SaveInProgress for the player
                // Track that player was prepared (always true since we're calling it)
                SaveBiotaToDatabase(false);
                biotas.Add((Biota, BiotaDatabaseLock));

                // Get all possessions and prepare them for batch save
                var allPossessions = GetAllPossessions();
                
                // For possessions, prepare them for batch save
                // Only track items that were actually prepared (SaveInProgress was set by this call)
                foreach (var possession in allPossessions)
                {
                    if (possession.ChangesDetected)
                    {
                        // Check if SaveInProgress is already true (from another save)
                        // NOTE: SaveInProgress is advisory, not a hard lock. Overlap is tolerated
                        // but tracked. SerializedShardDatabase prevents overlap at DB level, but
                        // Player-level cannot prevent concurrent save requests from different sources.
                        bool wasAlreadyInProgress = possession.SaveInProgress;
                        
                        // Sync position cache and prepare biota for save
                        // This sets SaveInProgress = true and clears ChangesDetected = false
                        // But it may return early if SaveInProgress was already true
                        possession.SaveBiotaToDatabase(false);
                        
                        // Only add to batch and track if SaveInProgress was set by this call
                        // If it was already true, SaveBiotaToDatabase returned early and didn't prepare it
                        if (possession.SaveInProgress && !wasAlreadyInProgress)
                        {
                            biotas.Add((possession.Biota, possession.BiotaDatabaseLock));
                            preparedGuidsForCallback.Add(possession.Guid.Full);
                        }
                    }
                }

                return biotas;
            };
            
            // Common callback logic for both logout and non-logout saves
            Action<bool> saveCallback = (result) =>
            {
                var clearFlagsAction = new ACE.Server.Entity.Actions.ActionChain();
                clearFlagsAction.AddAction(WorldManager.ActionQueue, ActionType.PlayerDatabase_SaveBiotasInParallelCallback, () =>
                {
                    SaveInProgress = false;
                    SaveStartTime = DateTime.MinValue; // Reset for next save
                    // Re-fetch possessions to avoid stale references
                    var currentPossessions = GetAllPossessions();
                    
                    // Only process possessions that were actually prepared for THIS batch save
                    // This prevents clearing flags for items that were SaveInProgress from another save
                    foreach (var possession in currentPossessions)
                    {
                        if (!possession.IsDestroyed && preparedGuidsForCallback.Contains(possession.Guid.Full))
                        {
                            // This possession was prepared for this batch, clear its flags
                            possession.SaveInProgress = false;
                            possession.SaveStartTime = DateTime.MinValue; // Reset for next save
                            if (result)
                                possession.ChangesDetected = false;
                            else
                            {
                                possession.ChangesDetected = true;
                                log.Warn($"[SAVE] Batch save failed for {Name} - restored ChangesDetected for {possession.Name} (0x{possession.Guid}) to prevent data loss");
                            }
                        }
                    }
                    
                    if (result)
                    {
                        // Clear ChangesDetected if it was set (we saved those changes)
                        // If new changes occurred, they'll set it back to true
                        if (ChangesDetected)
                            ChangesDetected = false;
                        
                        if (duringLogout)
                        {
                            // Don't set the player offline until they have been successfully saved
                            // This prevents login until save completes
                            // NOTE: Due to coalescing, logout saves are asynchronous. Login attempts
                            // may block until the coalesced save completes, which could add latency
                            // under high save churn. This is intentional to ensure data consistency.
                            PlayerManager.SwitchPlayerFromOnlineToOffline(this);
                        }
                        log.Debug($"{Name} has been saved. It took {(DateTime.UtcNow - requestedTimeUtc).TotalMilliseconds:N0} ms to process the request.");
                    }
                    else
                    {
                        // Restore ChangesDetected so it can be retried
                        ChangesDetected = true;
                        // This will trigger a boot on next player tick
                        BiotaSaveFailed = true;
                        
                        // If character save succeeded but biota save failed, restore character dirty state
                        // so character changes can be retried as part of the same logical operation
                        // This prevents character and biota from diverging on partial failure.
                        // NOTE: This is manual coordination - character and biota saves are separate
                        // and ordering relies on call sequencing, not structural guarantees.
                        if (!CharacterChangesDetected)
                        {
                            CharacterChangesDetected = true;
                            log.Warn($"[SAVE] Biota save failed for {Name} - restored CharacterChangesDetected to prevent desync");
                        }
                        
                        // ARCHITECTURAL LIMITATION: If a possession mutates after getBiotas runs
                        // but before this failure callback, correctness depends on:
                        // - ChangesDetected being set later by the mutation
                        // - Another save request being generated
                        // - EnsureSaveIfOwed eventually running
                        // Recovery is improved but not guaranteed at Player level.
                        // The DB coalescer can handle this better.
                        
                        if (duringLogout)
                        {
                            // Still set player offline even on failure, but mark as failed
                            // This will trigger a boot on next player tick if they log back in
                            PlayerManager.SwitchPlayerFromOnlineToOffline(this);
                        }
                    }
                });
                clearFlagsAction.EnqueueChain();
            };

            // Use coalesced save for both logout and non-logout saves
            DatabaseManager.Shard.SavePlayerBiotasCoalesced(
                Guid.Full,
                getBiotas,
                saveCallback,
                duringLogout ? $"logout:{Guid}" : Guid.ToString());
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
                {
                    // Marshal CharacterSaveFailed to world thread (same pattern as BiotaSaveFailed)
                    var setFailedAction = new ACE.Server.Entity.Actions.ActionChain();
                    setFailedAction.AddAction(WorldManager.ActionQueue, ActionType.PlayerDatabase_CharacterSaveFailed, () =>
                    {
                        // This will trigger a boot on next player tick
                        CharacterSaveFailed = true;
                    });
                    setFailedAction.EnqueueChain();
                }
            };

            DatabaseManager.Shard.SaveCharacterCoalesced(
                Character.Id,
                getCharacter,
                duringLogout ? null : saveCallback);
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
    }
}
