using System;
using System.Linq;
using System.Threading;

using ACE.Common;
using ACE.Database;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    partial class WorldObject
    {
        private readonly bool biotaOriginatedFromDatabase;
        
        // Discord throttling for database diagnostics alerts
        private static readonly object dbAlertLock = new object();
        private static DateTime lastDbRaceAlert = DateTime.MinValue;
        private static System.Collections.Concurrent.ConcurrentBag<string> dbRacesThisMinute = new();
        
        private static DateTime lastDbSlowAlert = DateTime.MinValue;
        private static int dbSlowAlertsThisMinute = 0;
        
        private static DateTime lastDbQueueAlert = DateTime.MinValue;
        private static int dbQueueAlertsThisMinute = 0;

        public DateTime LastRequestedDatabaseSave { get; protected set; }
        
        private volatile bool _saveInProgress;
        internal bool SaveInProgress 
        { 
            get => _saveInProgress;
            set => _saveInProgress = value;
        }
        private DateTime SaveStartTime { get; set; }
        private int? LastSavedStackSize { get; set; }  // Track last saved value to detect corruption

        /// <summary>
        /// This variable is set to true when a change is made, and set to false before a save is requested.<para />
        /// The primary use for this is to trigger save on add/modify/remove of properties.
        /// </summary>
        public bool ChangesDetected { get; set; }
        
        private void DetectAndLogConcurrentSave()
        {
            if (!SaveInProgress)
                return;

            // Capture Name and Guid early to avoid potential lock recursion
            var itemName = Name;
            var itemGuid = Guid;

            if (SaveStartTime == DateTime.MinValue)
            {
                log.Error($"[DB RACE] SaveInProgress set but SaveStartTime uninitialized for {itemName} (0x{itemGuid})");
                SaveInProgress = false;
                SaveStartTime = DateTime.UtcNow;
                return;
            }
            
            var timeInFlight = (DateTime.UtcNow - SaveStartTime).TotalMilliseconds;
            var playerInfo = this is Player player ? $"{player.Name} (0x{player.Guid})" : $"Object 0x{itemGuid}";
            
            var currentStack = StackSize;
            var stackChanged = currentStack.HasValue && LastSavedStackSize.HasValue && currentStack != LastSavedStackSize;
            var severityMarker = stackChanged ? "🔴 DATA CHANGED" : "";
            
            var stackInfo = currentStack.HasValue ? $" | Stack: {LastSavedStackSize ?? 0}→{currentStack}" : "";
            log.Warn($"[DB RACE] {severityMarker} {playerInfo} {itemName} | In-flight: {timeInFlight:N0}ms{stackInfo}");
            
            if (stackChanged || timeInFlight > 50)
            {
                var ownerContext = this is Player p ? $"[{p.Name}] " : 
                                  (this.Container is Player owner ? $"[{owner.Name}] " : "");
                var raceInfo = stackChanged 
                    ? $"{ownerContext}{itemName} Stack:{LastSavedStackSize}→{currentStack} 🔴" 
                    : $"{ownerContext}{itemName} ({timeInFlight:N0}ms)";
                SendAggregatedDbRaceAlert(raceInfo);
            }
        }

        /// <summary>
        /// Best practice says you should use this lock any time you read/write the Biota.<para />
        /// However, it's only a requirement to do this for properties/collections that will be modified after the initial biota has been created.<para />
        /// There are several properties/collections of the biota that are simply duplicates of the original weenie and are never changed. You wouldn't need to use this lock to read those collections.<para />
        /// <para />
        /// For absolute maximum performance, if you're willing to assume (and risk) the following:<para />
        ///  - that the biota in the database will not be modified (in a way that adds or removes properties) outside of ACE while ACE is running with a reference to that biota<para />
        ///  - that the biota will only be read/modified by a single thread in ACE<para />
        /// You can remove the lock usage for any Get/GetAll Property functions. You would simply use it for Set/Remove Property functions because each of these could end up adding/removing to the collections.<para />
        /// The critical thing is that the collections are not added to or removed from while Entity Framework is iterating over them.<para />
        /// Mag-nus 2018-08-19
        /// </summary>
        public readonly ReaderWriterLockSlim BiotaDatabaseLock = new ReaderWriterLockSlim();

        public bool BiotaOriginatedFromOrHasBeenSavedToDatabase()
        {
            return biotaOriginatedFromDatabase || LastRequestedDatabaseSave != DateTime.MinValue;
        }

        /// <summary>
        /// This will set the LastRequestedDatabaseSave to UtcNow and ChangesDetected to false.<para />
        /// If enqueueSave is set to true, DatabaseManager.Shard.SaveBiota() will be called for the biota.<para />
        /// Set enqueueSave to false if you want to perform all the normal routines for a save but not the actual save. This is useful if you're going to collect biotas in bulk for bulk saving.
        /// </summary>
        public virtual void SaveBiotaToDatabase(bool enqueueSave = true)
        {
            // Capture name and guid early to avoid lock recursion when logging
            // The Name property getter calls GetProperty which tries to enter a read lock
            // If we're already in a read lock (like when checking biota properties below),
            // this would cause a LockRecursionException
            var itemName = Name;
            var itemGuid = Guid;

#if DEBUG
            string GetItemInfo() => this is Player p ? $"{p.Name}" : $"{itemName} (0x{itemGuid})";
            log.Debug($"[SAVE DEBUG] SaveBiotaToDatabase called for {GetItemInfo()} | enqueueSave={enqueueSave} | ChangesDetected={ChangesDetected} | SaveInProgress={SaveInProgress}");
#endif
            
            // For individual saves, check if this item belongs to a player with a batch save in progress
            // If the item has newer changes (ChangesDetected = true), we want to allow the save to proceed
            // even if SaveInProgress is true (set by the batch save)
            bool allowSaveDespiteInProgress = false;
            if (enqueueSave && ChangesDetected && this.Container is Player player && player.SaveInProgress)
            {
                // Get the current ContainerId from the property (in-memory state)
                var staleCheckPropertyContainerId = ContainerId;
                
                // Get the ContainerId from the biota (what's currently in the database)
                uint? staleCheckBiotaContainerId = null;
                BiotaDatabaseLock.EnterReadLock();
                try
                {
                    if (Biota.PropertiesIID != null && Biota.PropertiesIID.TryGetValue(PropertyInstanceId.Container, out var value))
                        staleCheckBiotaContainerId = value;
                }
                finally
                {
                    BiotaDatabaseLock.ExitReadLock();
                }
                
                // If property says player GUID but biota says side pack GUID, this is stale data
                // The batch save will have the correct side pack GUID, so skip this individual save
                // to avoid overwriting correct data with stale data
                // BUT: If property says side pack GUID and biota says player GUID, this is a legitimate move
                // from player inventory to side pack - allow it to proceed
                if (staleCheckPropertyContainerId.HasValue && staleCheckPropertyContainerId.Value == player.Guid.Full &&
                    staleCheckBiotaContainerId.HasValue && staleCheckBiotaContainerId.Value != player.Guid.Full)
                {
                    // This individual save would overwrite correct side pack ContainerId with stale player GUID
                    // Skip it - the batch save will save the correct state
#if DEBUG
                    log.Debug($"[SAVE] Skipping individual save for {Name} (0x{Guid}) - would overwrite correct ContainerId {staleCheckBiotaContainerId} (0x{staleCheckBiotaContainerId:X8}) with stale player GUID");
#endif
                    return;
                }
                // If property says side pack GUID and biota says player GUID, this is a legitimate move
                // from player inventory to side pack - allow it to proceed (don't skip)
                
                // Item has newer changes and doesn't have stale ContainerId data
                // Allow the save to proceed even if SaveInProgress is true
                // It will queue after the batch save and save the newer state
                allowSaveDespiteInProgress = true;
#if DEBUG
                log.Debug($"[SAVE] Allowing individual save for {Name} (0x{Guid}) during player batch save - has newer changes (Property ContainerId={staleCheckPropertyContainerId}, Biota ContainerId={staleCheckBiotaContainerId})");
#endif
            }
            
            // Detect concurrent saves at item level
            // But allow saves with newer changes during player batch saves
            if (SaveInProgress && !allowSaveDespiteInProgress)
            {
                DetectAndLogConcurrentSave();
                return; // Abort save attempt - already in progress
            }
            
            // Sync position cache to biota FIRST - this must happen before any property modifications
            // Log position cache contents for debugging (especially for players)
            if (this is Player playerObj)
            {
                var locationPos = positionCache.TryGetValue(PositionType.Location, out var loc) ? loc : null;
                var locationProperty = Location; // Read through property getter
#if DEBUG
                log.Debug($"[SAVE DEBUG] {GetItemInfo()} Position cache sync | Location in cache={locationPos != null} | Location property={locationProperty} | Cache count={positionCache.Count} | Match={locationPos == locationProperty}");
#endif
                
                // If Location property exists but isn't in cache, add it
                if (locationProperty != null && locationPos == null)
                {
#if DEBUG
                    log.Warn($"[SAVE DEBUG] {GetItemInfo()} Location property exists but not in cache! Adding to cache...");
#endif
                    positionCache[PositionType.Location] = locationProperty;
                }
            }
            
            foreach (var kvp in positionCache)
            {
                if (kvp.Value != null)
                {
                    Biota.SetPosition(kvp.Key, kvp.Value, BiotaDatabaseLock);
#if DEBUG
                    if (this is Player && kvp.Key == PositionType.Location)
                    {
                        log.Debug($"[SAVE DEBUG] {GetItemInfo()} Synced Location position to biota | Position={kvp.Value}");
                    }
#endif
                }
            }

            // Ensure ContainerId is set correctly before save (following Vendor's approach)
            // Container property is the most authoritative - use Biota.Id (not Guid.Full)
            // SortWorldObjectsIntoInventory compares against Biota.Id, so ContainerId must be Biota.Id
            // For players, Biota.Id == Guid.Full, but for side packs, Biota.Id is the database ID
            uint? expectedContainerId = null;
            if (Container != null)
            {
                expectedContainerId = Container.Biota.Id;
            }
            else if (WielderId.HasValue)
            {
                // Item is equipped - ContainerId should be null/cleared
                // Equipped items use Wielder, not ContainerId
                expectedContainerId = null;
            }
            // If Container is null and no Wielder, keep current ContainerId (might be on ground or orphaned)
            
            // Set ContainerId property directly (like Vendor does) - this updates biota and sets ChangesDetected
            // Since we're already saving, we'll clear ChangesDetected after if needed
            var hadChangesBeforeContainerId = ChangesDetected;
            if (ContainerId != expectedContainerId)
            {
                ContainerId = expectedContainerId;
#if DEBUG
                log.Debug($"[SAVE DEBUG] {GetItemInfo()} Set ContainerId property | Container={Container?.Name ?? (WielderId.HasValue ? $"Equipped (Wielder={WielderId:X8})" : "null")} | ContainerId={expectedContainerId} (0x{(expectedContainerId ?? 0):X8})");
#endif
                // Clear ChangesDetected if we just set it (we're already saving)
                if (!hadChangesBeforeContainerId)
                    ChangesDetected = false;
            }

            // WielderId and StackSize: These properties are already set correctly when items are equipped/stacked
            // Unlike ContainerId, we don't have a "source of truth" object to compare against, so we trust
            // that they were set correctly earlier (e.g., when item was equipped or stack was split)
            // Setting them here would be redundant and they're already correct from their respective operations

            LastRequestedDatabaseSave = DateTime.UtcNow;
            SaveInProgress = true;
            SaveStartTime = DateTime.UtcNow;
            LastSavedStackSize = StackSize;
            
            // For batch saves (enqueueSave=false), don't clear ChangesDetected here
            // The caller will handle clearing it after the batch completes successfully
            // For individual saves (enqueueSave=true), clear it now but restore on failure
            var hadChanges = ChangesDetected;
            if (enqueueSave)
            {
                ChangesDetected = false;
            }

            if (enqueueSave)
            {
#if DEBUG
                // Log final ContainerId before queuing save
                BiotaDatabaseLock.EnterReadLock();
                try
                {
                    uint? finalBiotaContainerId = null;
                    if (Biota.PropertiesIID != null && Biota.PropertiesIID.TryGetValue(PropertyInstanceId.Container, out var finalValue))
                    {
                        finalBiotaContainerId = finalValue;
                    }
                    string containerInfo = Container != null ? $"{Container.Name} (0x{Container.Guid})" : (WielderId.HasValue ? $"Equipped (Wielder={WielderId} (0x{WielderId:X8}))" : "null");
                    log.Debug($"[SAVE DEBUG] {GetItemInfo()} Queuing individual save | Final biota ContainerId={finalBiotaContainerId} (0x{(finalBiotaContainerId ?? 0):X8}) | Container={containerInfo}");
                }
                finally
                {
                    BiotaDatabaseLock.ExitReadLock();
                }
#endif
                
                CheckpointTimestamp = Time.GetUnixTime();
                //DatabaseManager.Shard.SaveBiota(Biota, BiotaDatabaseLock, null);
                DatabaseManager.Shard.SaveBiota(Biota, BiotaDatabaseLock, result =>
                {
                    try
                    {
                        if (IsDestroyed)
                        {
                            log.Debug($"[DB CALLBACK] Callback fired for destroyed {itemName} (0x{itemGuid}) after {(DateTime.UtcNow - SaveStartTime).TotalMilliseconds:N0}ms");
                            return;
                        }
                        
                        var saveTime = (DateTime.UtcNow - SaveStartTime).TotalMilliseconds;
                        var slowThreshold = PropertyManager.GetLong("db_slow_threshold_ms", 1000);
                        if (saveTime > slowThreshold && this is not Player)
                        {
                            var ownerInfo = this.Container is Player owner ? $" | Owner: {owner.Name}" : "";
                            log.Warn($"[DB SLOW] Item save took {saveTime:N0}ms for {itemName} (Stack: {StackSize}){ownerInfo}");
                            SendDbSlowDiscordAlert(itemName, saveTime, StackSize ?? 0, ownerInfo);
                        }
                        
                        CheckDatabaseQueueSize();
                        
#if DEBUG
                        // Log save result with ContainerId
                        BiotaDatabaseLock.EnterReadLock();
                        try
                        {
                            uint? savedBiotaContainerId = null;
                            if (Biota.PropertiesIID != null && Biota.PropertiesIID.TryGetValue(PropertyInstanceId.Container, out var savedValue))
                            {
                                savedBiotaContainerId = savedValue;
                            }
                            var callbackItemInfo = this is Player p ? $"{p.Name}" : $"{itemName} (0x{itemGuid})";
                            log.Debug($"[SAVE DEBUG] {callbackItemInfo} Individual save completed | Result={result} | Saved biota ContainerId={savedBiotaContainerId} (0x{(savedBiotaContainerId ?? 0):X8}) | Time={saveTime:N0}ms");
                        }
                        finally
                        {
                            BiotaDatabaseLock.ExitReadLock();
                        }
#endif
                        
                        if (!result)
                        {
                            // Restore ChangesDetected if save failed so changes aren't lost
                            if (hadChanges)
                            {
                                ChangesDetected = true;
#if DEBUG
                                var callbackItemInfo = this is Player p ? $"{p.Name}" : $"{itemName} (0x{itemGuid})";
                                log.Warn($"[SAVE DEBUG] {callbackItemInfo} Individual save FAILED - restored ChangesDetected to prevent data loss");
#endif
                            }
                            
                            if (this is Player player)
                            {
                                // This will trigger a boot on next player tick
                                player.BiotaSaveFailed = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Restore ChangesDetected if callback throws
                        if (hadChanges)
                        {
                            ChangesDetected = true;
#if DEBUG
                            var callbackItemInfo = this is Player p ? $"{p.Name}" : $"{itemName} (0x{itemGuid})";
                            log.Warn($"[SAVE DEBUG] {callbackItemInfo} Exception in save callback - restored ChangesDetected to prevent data loss: {ex.Message}");
#endif
                        }
                        log.Error($"Exception in save callback for {itemName} (0x{itemGuid}): {ex.Message}");
                    }
                    finally
                    {
                        // ALWAYS clear SaveInProgress, even if callback throws
                        SaveInProgress = false;
                    }
                });
            }
            // For bulk saves, SaveInProgress cleared by caller after bulk completes
        }

        /// <summary>
        /// If enqueueSave is set to true, DatabaseManager.Shard.SaveBiota() will be called for the biota.<para />
        /// Set enqueueSave to false if you want to perform all the normal routines for a save but not the actual save. This is useful if you're going to collect biotas in bulk for bulk saving.
        /// </summary>
        /// <param name="enqueueSave">Whether to enqueue the save operation</param>
        /// <param name="onCompleted">Optional callback to invoke when the save operation completes</param>
        public virtual void SaveBiotaToDatabase(bool enqueueSave, Action<bool> onCompleted)
        {
            // Capture name and guid early to avoid lock recursion in callbacks
            var itemName = Name;
            var itemGuid = Guid;
            
            // Detect concurrent saves
            if (SaveInProgress)
            {
                DetectAndLogConcurrentSave();
                onCompleted?.Invoke(false); // Notify caller that save was rejected
                return; // Abort save attempt - already in progress
            }
            
            foreach (var kvp in positionCache)
            {
                if (kvp.Value != null)
                    Biota.SetPosition(kvp.Key, kvp.Value, BiotaDatabaseLock);
            }

            LastRequestedDatabaseSave = DateTime.UtcNow;
            SaveInProgress = true;
            SaveStartTime = DateTime.UtcNow;
            LastSavedStackSize = StackSize;
            ChangesDetected = false;

            if (enqueueSave)
            {
                CheckpointTimestamp = Time.GetUnixTime();
                //DatabaseManager.Shard.SaveBiota(Biota, BiotaDatabaseLock, null);
                DatabaseManager.Shard.SaveBiota(Biota, BiotaDatabaseLock, result =>
                {
                    try
                    {
                        if (IsDestroyed)
                        {
                            log.Debug($"[DB CALLBACK] Callback fired for destroyed {itemName} (0x{itemGuid}) after {(DateTime.UtcNow - SaveStartTime).TotalMilliseconds:N0}ms");
                            return;
                        }
                        
                        var saveTime = (DateTime.UtcNow - SaveStartTime).TotalMilliseconds;
                        var slowThreshold = PropertyManager.GetLong("db_slow_threshold_ms", 1000);
                        if (saveTime > slowThreshold && this is not Player)
                        {
                            var ownerInfo = this.Container is Player owner ? $" | Owner: {owner.Name}" : "";
                            log.Warn($"[DB SLOW] Item save took {saveTime:N0}ms for {itemName} (Stack: {StackSize}){ownerInfo}");
                            SendDbSlowDiscordAlert(itemName, saveTime, StackSize ?? 0, ownerInfo);
                        }
                        
                        CheckDatabaseQueueSize();
                        
                        if (!result && this is Player player)
                            player.BiotaSaveFailed = true;
                        
                        onCompleted?.Invoke(result);
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Exception in save callback for {itemName} (0x{itemGuid}): {ex.Message}");
                    }
                    finally
                    {
                        SaveInProgress = false;
                    }
                });
            }
            else
            {
                // Note: For bulk saves (enqueueSave=false), SaveInProgress remains true
                // It will be cleared by the caller when the bulk save completes
            }
        }

        /// <summary>
        /// This will set the LastRequestedDatabaseSave to MinValue and ChangesDetected to true.<para />
        /// If enqueueRemove is set to true, DatabaseManager.Shard.RemoveBiota() will be called for the biota.<para />
        /// Set enqueueRemove to false if you want to perform all the normal routines for a remove but not the actual removal. This is useful if you're going to collect biotas in bulk for bulk removing.
        /// </summary>
        public void RemoveBiotaFromDatabase(bool enqueueRemove = true)
        {
            // If this entity doesn't exist in the database, let's not queue up work unnecessary database work.
            if (!BiotaOriginatedFromOrHasBeenSavedToDatabase())
            {
                ChangesDetected = true;
                return;
            }

            LastRequestedDatabaseSave = DateTime.MinValue;
            ChangesDetected = true;

            if (enqueueRemove)
                DatabaseManager.Shard.RemoveBiota(Biota.Id, null);
        }

        /// <summary>
        /// A static that should persist to the shard may be a hook with an item, or a house that's been purchased, or a housing chest that isn't empty, etc...<para />
        /// If the world object originated from the database or has been saved to the database, this will also return true.
        /// </summary>
        public bool IsStaticThatShouldPersistToShard()
        {
            if (!Guid.IsStatic())
                return false;

            if (BiotaOriginatedFromOrHasBeenSavedToDatabase())
                return true;

            if (WeenieType == WeenieType.SlumLord && this is SlumLord slumlord)
            {
                if (slumlord.House != null && slumlord.House.HouseOwner.HasValue && slumlord.House.HouseOwner != 0)
                    return true;
            }

            if (WeenieType == WeenieType.House && this is House house)
            {
                if (house.HouseOwner.HasValue && house.HouseOwner != 0)
                    return true;
            }

            if ((WeenieType == WeenieType.Hook || WeenieType == WeenieType.Storage) && this is Container container)
            {
                if (container.Inventory.Count > 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// This will filter out the following:<para />
        /// Ammunition and Spell projectiles.<para />
        /// Monster corpses.<para />
        /// Missiles that haven't been saved to the shard yet.<para />
        /// If the world object originated from the database or has been saved to the database, this will also return true.
        /// </summary>
        /// <returns></returns>
        public bool IsDynamicThatShouldPersistToShard()
        {
            if (!Guid.IsDynamic())
                return false;

            if (ChangesDetected && BiotaOriginatedFromOrHasBeenSavedToDatabase())
                return true;

            // Don't save generators, and items that were generated by a generator
            // If the item was generated by a generator and then picked up by a player, the wo.Generator property would be set to null.
            if (IsGenerator || Generator != null)
                return false;

            if (WeenieType == WeenieType.Missile || WeenieType == WeenieType.Ammunition || WeenieType == WeenieType.ProjectileSpell || WeenieType == WeenieType.GamePiece
                || WeenieType == WeenieType.Pet || WeenieType == WeenieType.CombatPet)
                return false;

            if (WeenieType == WeenieType.Corpse && this is Corpse corpse && corpse.IsMonster)
                return false;

            if (WeenieType == WeenieType.Portal && this is Portal portal && portal.IsGateway)
                return false;

            // Missiles are unique. The only missiles that are persistable are ones that already exist in the database.
            // TODO: See if we can remove this check by catching the WeenieType above.
            var missile = Missile;
            if (missile.HasValue && missile.Value)
            {
                log.Warn($"Missile: WeenieClassId: {WeenieClassId}, Name: {Name}, WeenieType: {WeenieType}, detected in IsDynamicThatShouldPersistToShard() that wasn't caught by prior check.");
                return false;
            }

            return true;
        }
        
        private static void SendAggregatedDbRaceAlert(string raceInfo = null)
        {
            lock (dbAlertLock)
            {
                if (raceInfo != null)
                    dbRacesThisMinute.Add(raceInfo);

                var now = DateTime.UtcNow;
                
                // Reset counter every minute and send summary
                if ((now - lastDbRaceAlert).TotalMinutes >= 1 && dbRacesThisMinute.Count > 0)
                {
                    // Check Discord is configured
                    if (ConfigManager.Config.Chat.EnableDiscordConnection && 
                        ConfigManager.Config.Chat.PerformanceAlertsChannelId > 0)
                    {
                        try
                        {
                            var topItems = dbRacesThisMinute.Take(10).ToList();
                            var msg = $"⚠️ **DB RACE**: {dbRacesThisMinute.Count} concurrent saves detected in last minute\n" +
                                     $"Top items: `{string.Join("`, `", topItems)}`";
                            
                            DiscordChatManager.SendDiscordMessage("DB DIAGNOSTICS", msg, 
                                ConfigManager.Config.Chat.PerformanceAlertsChannelId);
                            
                            lastDbRaceAlert = now;
                        }
                        catch (Exception ex)
                        {
                            log.Error($"Failed to send DB race alert to Discord: {ex.Message}");
                        }
                    }
                    
                    // Clear the bag for next minute
                    dbRacesThisMinute = new System.Collections.Concurrent.ConcurrentBag<string>();
                }
            }
        }
        
        private static void SendDbSlowDiscordAlert(string itemName, double saveTime, int stackSize, string ownerInfo)
        {
            lock (dbAlertLock)
            {
                var now = DateTime.UtcNow;
                
                // Reset counter every minute
                if ((now - lastDbSlowAlert).TotalMinutes >= 1)
                {
                    dbSlowAlertsThisMinute = 0;
                }
                
                // Check rate limit (configurable via /modifylong db_slow_discord_max_alerts_per_minute)
                var maxAlerts = PropertyManager.GetLong("db_slow_discord_max_alerts_per_minute", 5);
                if (maxAlerts <= 0 || dbSlowAlertsThisMinute >= maxAlerts)
                    return;  // Drop alert to prevent Discord API spam
                
                // Check Discord is configured
                if (!ConfigManager.Config.Chat.EnableDiscordConnection || 
                    ConfigManager.Config.Chat.PerformanceAlertsChannelId <= 0)
                    return;
                
                try
                {
                    var msg = $"🔴 **DB SLOW**: `{itemName}` (Stack: {stackSize}) took **{saveTime:N0}ms** to save{ownerInfo}";
                    
                    DiscordChatManager.SendDiscordMessage("DB DIAGNOSTICS", msg, 
                        ConfigManager.Config.Chat.PerformanceAlertsChannelId);
                    
                    dbSlowAlertsThisMinute++;
                    lastDbSlowAlert = now;
                }
                catch (Exception ex)
                {
                    log.Error($"Failed to send DB slow alert to Discord: {ex.Message}");
                }
            }
        }
        
        private static void CheckDatabaseQueueSize()
        {
            var queueThreshold = PropertyManager.GetLong("db_queue_alert_threshold", 100);
            if (queueThreshold <= 0)
                return;  // Monitoring disabled
            
            var queueCount = DatabaseManager.Shard.QueueCount;
            if (queueCount <= queueThreshold)
                return;  // Queue size acceptable
            
            lock (dbAlertLock)
            {
                var now = DateTime.UtcNow;
                
                // Reset counter every minute
                if ((now - lastDbQueueAlert).TotalMinutes >= 1)
                {
                    dbQueueAlertsThisMinute = 0;
                }
                
                // Check rate limit (configurable via /modifylong db_queue_discord_max_alerts_per_minute)
                var maxAlerts = PropertyManager.GetLong("db_queue_discord_max_alerts_per_minute", 2);
                if (maxAlerts <= 0 || dbQueueAlertsThisMinute >= maxAlerts)
                    return;
                
                // Check Discord is configured
                if (!ConfigManager.Config.Chat.EnableDiscordConnection || 
                    ConfigManager.Config.Chat.PerformanceAlertsChannelId <= 0)
                    return;
                
                try
                {
                    var msg = $"🔴 **DB QUEUE HIGH**: Queue count at **{queueCount}** (threshold: {queueThreshold}). Potential save delays and item loss risk!";
                    
                    DiscordChatManager.SendDiscordMessage("DB DIAGNOSTICS", msg, 
                        ConfigManager.Config.Chat.PerformanceAlertsChannelId);
                    
                    dbQueueAlertsThisMinute++;
                    lastDbQueueAlert = now;
                    
                    log.Warn($"[DB QUEUE] Database queue count: {queueCount} exceeds threshold {queueThreshold}");
                }
                catch (Exception ex)
                {
                    log.Error($"Failed to send DB queue alert to Discord: {ex.Message}");
                }
            }
        }
    }
}
