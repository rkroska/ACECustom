using System;
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
        public static readonly long DefaultPlayerSaveIntervalSecs = 300; // default to 5 minutes

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
            get => PropertyManager.GetLong("player_save_interval", DefaultPlayerSaveIntervalSecs);
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
        /// Saves the character to the persistent database. Includes Stats, Position, Skills, etc.<para />
        /// Will also save any possessions that are marked with ChangesDetected.
        /// </summary>
        public void SavePlayerToDatabase(bool duringLogout = false)
        {
            if (CharacterChangesDetected)
                SaveCharacterToDatabase();

            var biotas = new Collection<(Biota biota, ReaderWriterLockSlim rwLock)>();

            // Track player ChangesDetected state at batch collection time
            var playerHadChanges = ChangesDetected;

            // Save player biota - this sets SaveInProgress for the player
            SaveBiotaToDatabase(false);
            biotas.Add((Biota, BiotaDatabaseLock));

            // Get all possessions once and reuse for batch collection and flag clearing
            var allPossessions = GetAllPossessions();
            var itemsInBatch = new System.Collections.Generic.HashSet<uint>();
            
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
                    itemsInBatch.Add(possession.Guid.Full);
                    // Note: SaveInProgress remains true, which will block individual saves
                    // But the individual save logic in SaveBiotaToDatabase will check for stale data
                    // and allow saves with newer changes (ChangesDetected = true) to proceed
                }
            }

            var requestedTime = DateTime.UtcNow;

            // During logout, use async callback but ensure player is marked as saving to prevent quick relogin
            // We don't block the logout thread to avoid crashes, but we ensure the player stays "online" 
            // until save completes, which prevents login until save is done
            if (duringLogout)
            {
                // Mark that logout save is in progress - this prevents login until save completes
                // The player will remain "online" until SwitchPlayerFromOnlineToOffline is called in the callback
                DatabaseManager.Shard.SaveBiotasInParallel(biotas, result =>
                {
                    var clearFlagsAction = new ACE.Server.Entity.Actions.ActionChain();
                    clearFlagsAction.AddAction(WorldManager.ActionQueue, ActionType.PlayerDatabase_SaveBiotasInParallelCallback, () =>
                    {
                        SaveInProgress = false;
                        // Re-fetch possessions to avoid stale references, but only process items in batch
                        var currentPossessions = GetAllPossessions();
                        
                        // Consolidate all flag updates into a single loop for efficiency
                        foreach (var possession in currentPossessions)
                        {
                            if (!possession.IsDestroyed && itemsInBatch.Contains(possession.Guid.Full))
                            {
                                possession.SaveInProgress = false;
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
                            if (playerHadChanges)
                                ChangesDetected = false;
                            // Don't set the player offline until they have been successfully saved
                            // This prevents login until save completes
                            PlayerManager.SwitchPlayerFromOnlineToOffline(this);
                            log.Debug($"{Name} has been saved. It took {(DateTime.UtcNow - requestedTime).TotalMilliseconds:N0} ms to process the request.");
                        }
                        else
                        {
                            ChangesDetected = playerHadChanges;
                            // Still set player offline even on failure, but mark as failed
                            // This will trigger a boot on next player tick if they log back in
                            BiotaSaveFailed = true;
                            PlayerManager.SwitchPlayerFromOnlineToOffline(this);
                        }
                    });
                    clearFlagsAction.EnqueueChain();
                }, this.Guid.ToString());
                
                // Return immediately - don't block the logout thread
                // The player will remain "online" until the save callback completes and calls SwitchPlayerFromOnlineToOffline
                // This prevents login until save is done, without blocking threads
                return;
            }

            // For non-logout saves, use async callback as before
            DatabaseManager.Shard.SaveBiotasInParallel(biotas, result =>
            {
                var clearFlagsAction = new ACE.Server.Entity.Actions.ActionChain();
                clearFlagsAction.AddAction(WorldManager.ActionQueue, ActionType.PlayerDatabase_SaveBiotasInParallelCallback, () =>
                {
                    SaveInProgress = false;
                    // Re-fetch possessions to avoid stale references, but only process items in batch
                    var currentPossessions = GetAllPossessions();
                    
                    // Consolidate all flag updates into a single loop for efficiency
                    foreach (var possession in currentPossessions)
                    {
                        if (!possession.IsDestroyed && itemsInBatch.Contains(possession.Guid.Full))
                        {
                            possession.SaveInProgress = false;
                            if (result)
                                // Item was in the batch - clear ChangesDetected since it was successfully saved
                                // Items that weren't in the batch but have ChangesDetected=true are newer changes that should be preserved
                                possession.ChangesDetected = false;
                            else
                            {
                                // Item was in the batch - restore ChangesDetected so it can be retried
                                possession.ChangesDetected = true;
                                log.Warn($"[SAVE] Batch save failed for {Name} - restored ChangesDetected for {possession.Name} (0x{possession.Guid}) to prevent data loss");
                            }
                        }
                    }
                    
                    if (result)
                    {
                        // Only clear player ChangesDetected if it was true at batch time
                        // (we saved those changes; if new changes occurred, they'll set it back to true)
                        if (playerHadChanges)
                            ChangesDetected = false;
                        log.Debug($"{Name} has been saved. It took {(DateTime.UtcNow - requestedTime).TotalMilliseconds:N0} ms to process the request.");
                    }
                    else
                    {
                        // Restore player ChangesDetected to the value it had at batch time
                        ChangesDetected = playerHadChanges;
                        // This will trigger a boot on next player tick
                        BiotaSaveFailed = true;
                    }
                });
                clearFlagsAction.EnqueueChain();
            }, this.Guid.ToString());
        }

        public void SaveCharacterToDatabase(bool duringLogout = false)
        {
            CharacterLastRequestedDatabaseSave = DateTime.UtcNow;
            CharacterChangesDetected = false;

            if (duringLogout)
            {
                DatabaseManager.Shard.SaveCharacter(Character, CharacterDatabaseLock, null);
            }
            else
            {
                DatabaseManager.Shard.SaveCharacter(Character, CharacterDatabaseLock, result =>
                {
                    if (!result)
                    {
                        if (this is Player player)
                        {
                            // This will trigger a boot on next player tick
                            CharacterSaveFailed = true;
                        }
                    }
                });
            }
            
        }
    }
}
