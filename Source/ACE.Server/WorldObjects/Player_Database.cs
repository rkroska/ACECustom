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
        public bool CharacterChangesDetected { get; set; }

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

                // Allow a save now by resetting gate
                _nextAllowedSaveUtc = DateTime.MinValue;
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

            // Func that builds the biotas list at execution time to avoid stale snapshots
            Func<IEnumerable<(ACE.Entity.Models.Biota biota, ReaderWriterLockSlim rwLock)>> getBiotas = () =>
            {
                var biotas = new Collection<(Biota biota, ReaderWriterLockSlim rwLock)>();

                // Save player biota - this sets SaveInProgress for the player
                SaveBiotaToDatabase(false);
                biotas.Add((Biota, BiotaDatabaseLock));

                // Get all possessions and prepare them for batch save
                var allPossessions = GetAllPossessions();
                
                // For possessions, prepare them for batch save
                // We call SaveBiotaToDatabase(false) to sync position cache and prepare the biota,
                // but we don't want to block individual saves with newer changes, so we'll handle
                // SaveInProgress differently - the individual save logic will check for stale data
                foreach (var possession in allPossessions)
                {
                    if (possession.ChangesDetected)
                    {
                        // Sync position cache and prepare biota for save
                        // This sets SaveInProgress = true and clears ChangesDetected = false
                        possession.SaveBiotaToDatabase(false);
                        biotas.Add((possession.Biota, possession.BiotaDatabaseLock));
                        // Note: SaveInProgress remains true, which will block individual saves
                        // But the individual save logic in SaveBiotaToDatabase will check for stale data
                        // and allow saves with newer changes (ChangesDetected = true) to proceed
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
                    
                    // Process all possessions that have SaveInProgress=true (they were in the batch)
                    foreach (var possession in currentPossessions)
                    {
                        if (!possession.IsDestroyed && possession.SaveInProgress)
                        {
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
            CharacterLastRequestedDatabaseSave = DateTime.UtcNow;
            CharacterChangesDetected = false;

            // Func that gets the current Character state at execution time
            Func<(ACE.Database.Models.Shard.Character character, ReaderWriterLockSlim rwLock)> getCharacter = () =>
                (Character, CharacterDatabaseLock);

            Action<bool> saveCallback = (result) =>
            {
                if (!result)
                {
                    if (this is Player player)
                    {
                        // This will trigger a boot on next player tick
                        CharacterSaveFailed = true;
                    }
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
