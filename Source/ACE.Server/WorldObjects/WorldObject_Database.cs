using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using ACE.Common;
using ACE.Database;
using ACE.Entity;
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
        internal Guid? SaveServerBootId { get; set; }
        internal DateTime SaveStartTime { get; set; }
        private int? LastSavedStackSize { get; set; }  // Track last saved value to detect corruption

        /// <summary>
        /// This variable is set to true when a change is made, and set to false before a save is requested.<para />
        /// The primary use for this is to trigger save on add/modify/remove of properties.
        /// </summary>
        private bool _changesDetected;
        public bool ChangesDetected 
        { 
            get => _changesDetected;
            set
            {
                _changesDetected = value;
            }
        }
        
        private void DetectAndLogConcurrentSave(string itemName, ObjectGuid itemGuid, int? capturedStackSize)
        {
            if (!SaveInProgress)
                return;

            if (SaveStartTime == DateTime.MinValue)
            {
                log.Error($"[DB RACE] SaveInProgress set but SaveStartTime uninitialized for {itemName} (0x{itemGuid})");
                SaveInProgress = false;
                SaveStartTime = DateTime.UtcNow;
                return;
            }
            
            var timeInFlight = (DateTime.UtcNow - SaveStartTime).TotalMilliseconds;
            // Avoid property getters - use passed itemGuid and itemName
            
            // Use captured stack size from save snapshot (preserves save invariant)
            var currentStack = capturedStackSize;
            var stackChanged = currentStack.HasValue && LastSavedStackSize.HasValue && currentStack != LastSavedStackSize;
            var severityMarker = stackChanged ? "🔴 DATA CHANGED" : "";
            
            var stackInfo = currentStack.HasValue ? $" | Stack: {LastSavedStackSize ?? 0}→{currentStack}" : "";
            log.Warn($"[DB RACE] {severityMarker} {itemName ?? "item"} (0x{itemGuid:X8}) | In-flight: {timeInFlight:N0}ms{stackInfo}");
            
            if (stackChanged || timeInFlight > 50)
            {
                // Avoid Container property getter - use raw biota ContainerId if needed
                string ownerContext = "";
                if (this is Player p)
                {
                    // Player name from biota (avoid property getter)
                    string playerName = null;
                    p.BiotaDatabaseLock.EnterReadLock();
                    try
                    {
                        if (p.Biota.PropertiesString != null && p.Biota.PropertiesString.TryGetValue(PropertyString.Name, out var pn))
                            playerName = pn;
                    }
                    finally
                    {
                        p.BiotaDatabaseLock.ExitReadLock();
                    }
                    ownerContext = $"[{playerName ?? "player"}] ";
                }
                // Note: Container lookup removed to avoid property getter - owner context is optional for logging
                var raceInfo = stackChanged 
                    ? $"{ownerContext}{itemName} (0x{itemGuid:X8}) Stack:{LastSavedStackSize}→{currentStack} 🔴" 
                    : $"{ownerContext}{itemName} (0x{itemGuid:X8}) ({timeInFlight:N0}ms)";
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
            SaveBiotaInternal(enqueueSave, null);
        }

        /// <summary>
        /// If enqueueSave is set to true, DatabaseManager.Shard.SaveBiota() will be called for the biota.<para />
        /// Set enqueueSave to false if you want to perform all the normal routines for a save but not the actual save. This is useful if you're going to collect biotas in bulk for bulk saving.
        /// </summary>
        /// <param name="enqueueSave">Whether to enqueue the save operation</param>
        /// <param name="onCompleted">Optional callback to invoke when the save operation completes</param>
        public virtual void SaveBiotaToDatabase(bool enqueueSave, Action<bool> onCompleted)
        {
            SaveBiotaInternal(enqueueSave, onCompleted);
        }

        /// <summary>
        /// Internal implementation that contains all save logic in one place.
        /// This ensures consistency and prevents divergence between overloads.
        /// All raw biota state is captured here, SaveInProgress is set here, and ChangesDetected is cleared here.
        /// </summary>
        private void SaveBiotaInternal(bool enqueueSave, Action<bool> onCompleted)
        {
            // ARCHITECTURAL BOUNDARY:
            // WorldObject save logic is best-effort. It provides advisory flags (SaveInProgress, ChangesDetected)
            // and attempts to prevent obvious race conditions, but cannot provide strong correctness guarantees.
            // Strong correctness guarantees (coalescing, deduplication, serialization) are enforced by
            // SerializedShardDatabase at the database layer. This division allows WorldObject to remain
            // lightweight while the DB layer handles complex concurrency and batching logic.
            
            // SAVE INVARIANT:
            // - No property getters (Name, WielderId, Container, GetProperty, StackSize, Location)
            // - All biota reads captured once under a single read lock
            // - Operate on local variables only after lock is released
            // This prevents LockRecursionException and ensures lock-pure save paths.
            
            // Check for mutation depth leaks (safety valve)
            // If depth has been >0 for too long, force reset to prevent permanent save suppression
            if (IsInContainerMutation)
            {
                var timeSinceLastMutation = DateTime.UtcNow - _lastContainerMutationUtc;
                if (timeSinceLastMutation.TotalSeconds > MUTATION_LEAK_TIMEOUT_SECONDS)
                {
                    var currentDepth = Volatile.Read(ref _containerMutationDepth);
                    // Capture item name under biota lock before logging (Name getter is not safe without lock)
                    string leakItemName;
                    var leakItemGuid = Guid.Full; // safe, not locked
                    BiotaDatabaseLock.EnterReadLock();
                    try
                    {
                        if (Biota.PropertiesString != null && Biota.PropertiesString.TryGetValue(PropertyString.Name, out var name))
                            leakItemName = name;
                        else
                            leakItemName = $"0x{leakItemGuid:X8}";
                    }
                    finally
                    {
                        BiotaDatabaseLock.ExitReadLock();
                    }
                    // Now safe to log leakItemName because it is a plain local string
                    log.Warn($"[CONTAINER MUTATION LEAK] Mutation depth stuck at {currentDepth} for {leakItemName} (0x{Guid}) for {timeSinceLastMutation.TotalSeconds:F1}s - forcing reset");
                    Interlocked.Exchange(ref _containerMutationDepth, 0);
                    _lastContainerMutationUtc = DateTime.UtcNow; // Make leak breaker idempotent per incident
                    ChangesDetected = true;
                    // Continue with save now that depth is reset
                }
                else
                {
                    // Delay save during active mutation, but don't drop it
                    // ChangesDetected is already set, so save will be retried later
                    ChangesDetected = true; // Ensure changes are preserved for later save
                    return; // Skip save during mutation (will be retried when mutation completes)
                }
            }
            
#if DEBUG
            // Assert that we're not entering with a lock already held (catches regressions)
            if (BiotaDatabaseLock.IsReadLockHeld || BiotaDatabaseLock.IsWriteLockHeld)
            {
                log.Error("[SAVE] SaveBiotaToDatabase entered with BiotaDatabaseLock already held! This violates the save invariant.");
            }
#endif
            
            var itemGuid = Guid; // safe, not locked
            
            // Read ALL needed raw biota state in a single lock block to avoid recursion risk
            string itemName = null;
            uint? wielderId = null;
            uint? containerIdFromBiota = null;
            int? stackSize = null;
            BiotaDatabaseLock.EnterReadLock();
            try
            {
                // Read all needed properties in one lock acquisition
                if (Biota.PropertiesString != null && Biota.PropertiesString.TryGetValue(PropertyString.Name, out var name))
                    itemName = name;
                
                if (Biota.PropertiesIID != null)
                {
                    if (Biota.PropertiesIID.TryGetValue(PropertyInstanceId.Wielder, out var w))
                        wielderId = w;
                    if (Biota.PropertiesIID.TryGetValue(PropertyInstanceId.Container, out var c))
                        containerIdFromBiota = c;
                }
                
                if (Biota.PropertiesInt != null && Biota.PropertiesInt.TryGetValue(PropertyInt.StackSize, out var s))
                    stackSize = s;
            }
            finally
            {
                BiotaDatabaseLock.ExitReadLock();
            }
            
            // Now operate on local variables only - no more lock acquisitions

#if DEBUG
            // GetItemInfo uses itemName and itemGuid (already captured safely) - never use property getters
            string GetItemInfo() => $"{itemName ?? "item"} (0x{itemGuid})";
            log.Debug($"[SAVE DEBUG] SaveBiotaToDatabase called for {GetItemInfo()} | enqueueSave={enqueueSave} | ChangesDetected={ChangesDetected} | SaveInProgress={SaveInProgress}");
#endif
            
            // For individual saves, check if this item belongs to a player with a batch save in progress
            // If the item has newer changes (ChangesDetected = true), we want to allow the save to proceed
            // even if SaveInProgress is true (set by the batch save)
            // 
            // ARCHITECTURAL LIMITATION: There is a narrow race window where:
            // - Player batch save sets SaveInProgress = true
            // - Item has ChangesDetected = true
            // - allowSaveDespiteInProgress lets it through
            // - Multiple rapid calls can enqueue multiple individual saves
            // 
            // For player items, the coalescer downstream (SavePlayerBiotasCoalesced) will collapse these duplicates.
            // For non-player items, this can still enqueue duplicates if rapid calls occur.
            // 
            // This code assumes coalescing downstream handles deduplication. Without a per-object
            // "dirty since batch started" flag, we cannot fully prevent duplicate enqueues at this level.
            // The coalescer's TryEnqueueCoalesced logic will handle most cases, but non-player items
            // that use SaveBiota (non-coalesced) can still enqueue duplicates under rapid calls.
            bool allowSaveDespiteInProgress = false;
            
            // Use raw biota ContainerId to look up container (avoid property getter)
            if (enqueueSave && ChangesDetected && containerIdFromBiota.HasValue)
            {
                // Look up container by ContainerId (raw access, no property getter)
                var containerGuid = new ObjectGuid(containerIdFromBiota.Value);
                var containerObj = containerGuid.IsPlayer() ? PlayerManager.GetOnlinePlayer(containerGuid.Full) as WorldObject : null;
                if (containerObj is Player player && player.SaveInProgress)
                {
                    // Use the raw biota ContainerId we already have (no property getter)
                    var staleCheckBiotaContainerId = containerIdFromBiota;
                    
                    // If biota says player GUID, this is a legitimate save
                    // The batch save will handle it correctly
                    if (staleCheckBiotaContainerId.HasValue && staleCheckBiotaContainerId.Value == player.Guid.Full)
                    {
                        // Item has newer changes and ContainerId matches player
                        // Allow the save to proceed even if SaveInProgress is true
                        // It will queue after the batch save and save the newer state
                        // NOTE: Multiple rapid calls can still enqueue duplicates here.
                        // The coalescer will handle player items, but non-player items may enqueue duplicates.
                        allowSaveDespiteInProgress = true;
#if DEBUG
                        log.Debug($"[SAVE] Allowing individual save for {itemName ?? "item"} (0x{itemGuid:X8}) during player batch save - has newer changes (Biota ContainerId={staleCheckBiotaContainerId} (0x{staleCheckBiotaContainerId:X8}))");
#endif
                    }
                }
            }
            
            // Detect concurrent saves at item level
            // But allow saves with newer changes during player batch saves
            // Also check for container mutations that may have started after initial check
            // (defensive guard against race window between top-of-method check and here)
            if (IsInContainerMutation)
            {
                // Check for mutation depth leaks (safety valve)
                var timeSinceLastMutation = DateTime.UtcNow - _lastContainerMutationUtc;
                if (timeSinceLastMutation.TotalSeconds > MUTATION_LEAK_TIMEOUT_SECONDS)
                {
                    var currentDepth = Volatile.Read(ref _containerMutationDepth);
                    log.Warn($"[CONTAINER MUTATION LEAK] Mutation depth stuck at {currentDepth} for {itemName ?? "item"} (0x{itemGuid:X8}) for {timeSinceLastMutation.TotalSeconds:F1}s - forcing reset");
                    Interlocked.Exchange(ref _containerMutationDepth, 0);
                    _lastContainerMutationUtc = DateTime.UtcNow; // Make leak breaker idempotent per incident
                    ChangesDetected = true;
                    // Continue with save now that depth is reset
                }
                else
                {
                    // Delay save during active mutation, but don't drop it
                    ChangesDetected = true; // Ensure changes are preserved for later save
                    return; // Abort save attempt - mutation in progress (will be retried when mutation completes)
                }
            }
            
            if (SaveInProgress && !allowSaveDespiteInProgress)
            {
                DetectAndLogConcurrentSave(itemName, itemGuid, stackSize);
                return; // Abort save attempt - already in progress
            }
            
            // Sync position cache to biota FIRST - this must happen before any property modifications
            // Log position cache contents for debugging (especially for players)
            if (this is Player playerObj)
            {
                // Only use position cache - never use Location property getter (can cause lock recursion)
                positionCache.TryGetValue(PositionType.Location, out var locationPos);
#if DEBUG
                log.Debug($"[SAVE DEBUG] {GetItemInfo()} Position cache sync | Location in cache={locationPos != null} | Cache count={positionCache.Count}");
                // If Location is missing from cache, that's a logic error elsewhere, not fixed here
                if (locationPos == null)
                {
                    log.Warn($"[SAVE DEBUG] {GetItemInfo()} Location missing from position cache - this indicates a logic error elsewhere");
                }
#endif
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
            // Use raw biota ContainerId we already have (avoid property getters)
            // SortWorldObjectsIntoInventory compares against Biota.Id, so ContainerId must be Biota.Id
            // For players, Biota.Id == Guid.Full, but for side packs, Biota.Id is the database ID
            // 
            // CRITICAL: Initialize with existing ContainerId to trust it by default.
            // Only rewrite ContainerId when we can positively identify a live player container
            // and know the authoritative Biota.Id. For non-player containers or offline players,
            // we must preserve the existing ContainerId to avoid orphaning items.
            uint? expectedContainerId = containerIdFromBiota;
            
            if (containerIdFromBiota.HasValue)
            {
                var containerGuid = new ObjectGuid(containerIdFromBiota.Value);
                
                if (containerGuid.IsPlayer())
                {
                    // For player containers, we can look up the online player and get authoritative Biota.Id
                    var player = PlayerManager.GetOnlinePlayer(containerGuid.Full);
                    if (player != null)
                    {
                        // Player is online - use authoritative Biota.Id (fixes Guid.Full -> Biota.Id mismatch)
                        expectedContainerId = player.Biota.Id;
                    }
                    // else: Player is offline - keep existing containerIdFromBiota to avoid data loss
                }
                // else: Non-player container (housing storage, hooks, vendors, chests) - keep existing containerIdFromBiota
                // We cannot look up non-player containers from save context, so we must trust the stored value
            }
            else if (wielderId.HasValue)
            {
                // Item is equipped - ContainerId should be null/cleared
                // Equipped items use Wielder, not ContainerId
                expectedContainerId = null;
            }
            // If ContainerId is null and no Wielder, keep current ContainerId (might be on ground or orphaned)
            
            // Set ContainerId directly in biota (lock-pure, no property getters)
            // Since we're already saving, we'll clear ChangesDetected after if needed
            var hadChangesBeforeContainerId = ChangesDetected;
            if (containerIdFromBiota != expectedContainerId)
            {
                BiotaDatabaseLock.EnterWriteLock();
                try
                {
                    if (expectedContainerId.HasValue)
                    {
                        // Ensure PropertiesIID dictionary exists before indexing
                        if (Biota.PropertiesIID == null)
                            Biota.PropertiesIID = new Dictionary<PropertyInstanceId, uint>();
                        Biota.PropertiesIID[PropertyInstanceId.Container] = expectedContainerId.Value;
                    }
                    else
                    {
                        Biota.PropertiesIID?.Remove(PropertyInstanceId.Container);
                    }
                }
                finally
                {
                    BiotaDatabaseLock.ExitWriteLock();
                }

                // Update local variable to keep it in sync with biota (for logging and diagnostics)
                containerIdFromBiota = expectedContainerId;

#if DEBUG
                log.Debug($"[SAVE DEBUG] {itemName ?? "item"} (0x{itemGuid:X8}) Set raw biota ContainerId -> {(expectedContainerId.HasValue ? $"0x{expectedContainerId:X8}" : "null")}");
#endif

                if (!hadChangesBeforeContainerId)
                    ChangesDetected = false;
            }

            // WielderId and StackSize: These properties are already set correctly when items are equipped/stacked
            // Unlike ContainerId, we don't have a "source of truth" object to compare against, so we trust
            // that they were set correctly earlier (e.g., when item was equipped or stack was split)
            // Setting them here would be redundant and they're already correct from their respective operations

            LastRequestedDatabaseSave = DateTime.UtcNow;
            // SaveStartTime represents when the save was requested (before enqueue)
            // This is used for timing diagnostics and race detection
            SaveStartTime = DateTime.UtcNow;
            LastSavedStackSize = stackSize;
            
            // For batch saves (enqueueSave=false), don't clear ChangesDetected here
            // The caller will handle clearing it after the batch completes successfully
            // For individual saves (enqueueSave=true), clear it now but restore on failure
            var hadChanges = ChangesDetected;
            if (enqueueSave)
            {
                ChangesDetected = false;
            }

            // For bulk and coalesced paths, mark as included in the batch
            // Caller must clear this after SavePlayerBiotasCoalesced completes
            if (!enqueueSave)
            {
                SaveInProgress = true;
                SaveServerBootId = ServerRuntime.BootId;

                // If a mutation happened during batch prep (between top guard and here),
                // ensure a follow-up save is generated to capture the newer changes
                if (ChangesDetected)
                    LastRequestedDatabaseSave = DateTime.UtcNow;

                return;
            }

            CheckpointTimestamp = Time.GetUnixTime();
            
            // For individual saves, set just before enqueuing
            SaveInProgress = true;
            SaveServerBootId = ServerRuntime.BootId;
            
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
                string containerInfo = finalBiotaContainerId.HasValue ? (new ObjectGuid(finalBiotaContainerId.Value).IsPlayer() ? $"Player (0x{finalBiotaContainerId.Value:X8})" : $"Object (0x{finalBiotaContainerId.Value:X8})") : (wielderId.HasValue ? $"Equipped (Wielder={wielderId} (0x{wielderId:X8}))" : "null");
                log.Debug($"[SAVE DEBUG] {GetItemInfo()} Queuing individual save | Final biota ContainerId={finalBiotaContainerId} (0x{(finalBiotaContainerId ?? 0):X8}) | Container={containerInfo}");
            }
            finally
            {
                BiotaDatabaseLock.ExitReadLock();
            }
#endif
            
            //DatabaseManager.Shard.SaveBiota(Biota, BiotaDatabaseLock, null);
            DatabaseManager.Shard.SaveBiota(Biota, BiotaDatabaseLock, result =>
                {
                    try
                    {
                        if (IsDestroyed)
                        {
                            log.Debug($"[DB CALLBACK] Callback fired for destroyed {itemName} (0x{itemGuid}) after {(DateTime.UtcNow - SaveStartTime).TotalMilliseconds:N0}ms");
                            onCompleted?.Invoke(false);
                            return;
                        }
                        
                        // Calculate save time, but guard against overflow if SaveStartTime is invalid
                        double saveTime = 0;
                        if (SaveStartTime != DateTime.MinValue && SaveStartTime != default(DateTime))
                        {
                            var timeSpan = DateTime.UtcNow - SaveStartTime;
                            // Guard against negative time (shouldn't happen, but be defensive)
                            if (timeSpan.TotalMilliseconds >= 0 && timeSpan.TotalMilliseconds < double.MaxValue)
                                saveTime = timeSpan.TotalMilliseconds;
                        }
                        
                        var slowThreshold = ServerConfig.db_slow_threshold_ms.Value;
                        if (saveTime > slowThreshold && this is not Player)
                        {
                            // Get owner info using raw biota access (avoid property getters)
                            string ownerInfo = "";
                            if (containerIdFromBiota.HasValue)
                            {
                                var containerGuid = new ObjectGuid(containerIdFromBiota.Value);
                                if (containerGuid.IsPlayer())
                                {
                                    var owner = PlayerManager.GetOnlinePlayer(containerGuid.Full);
                                    if (owner != null)
                                    {
                                        // Get name from biota directly (avoid property getter)
                                        string ownerName = null;
                                        owner.BiotaDatabaseLock.EnterReadLock();
                                        try
                                        {
                                            if (owner.Biota.PropertiesString != null && owner.Biota.PropertiesString.TryGetValue(PropertyString.Name, out var on))
                                                ownerName = on;
                                        }
                                        finally
                                        {
                                            owner.BiotaDatabaseLock.ExitReadLock();
                                        }
                                        ownerInfo = $" | Owner: {ownerName ?? "player"}";
                                    }
                                }
                            }
                            log.Warn($"[DB SLOW] Item save took {saveTime:N0}ms for {itemName} (Stack: {stackSize}){ownerInfo}");
                            SendDbSlowDiscordAlert(itemName, saveTime, stackSize ?? 0, ownerInfo);
                        }
                        
                        CheckDatabaseQueueSize();
                        
#if DEBUG
                        // Log save result with ContainerId (use containerIdFromBiota we already have)
                        uint? savedBiotaContainerId = containerIdFromBiota;
                        // Use itemName and itemGuid (already captured safely) - never use property getters
                        var callbackItemInfo = $"{itemName ?? "item"} (0x{itemGuid})";
                        log.Debug($"[SAVE DEBUG] {callbackItemInfo} Individual save completed | Result={result} | Saved biota ContainerId={savedBiotaContainerId} (0x{(savedBiotaContainerId ?? 0):X8}) | Time={saveTime:N0}ms");
#endif
                        
                        if (!result)
                        {
                            // Restore ChangesDetected if save failed so changes aren't lost
                            if (hadChanges)
                            {
                                ChangesDetected = true;
#if DEBUG
                                // Use itemName and itemGuid (already captured safely) - never use property getters
                                var failedSaveItemInfo = $"{itemName ?? "item"} (0x{itemGuid})";
                                log.Warn($"[SAVE DEBUG] {failedSaveItemInfo} Individual save FAILED - restored ChangesDetected to prevent data loss");
#endif
                            }
                            
                            if (this is Player player)
                            {
                                // This will trigger a boot on next player tick
                                player.BiotaSaveFailed = true;
                            }
                        }
                        
                        onCompleted?.Invoke(result);
                    }
                    catch (Exception ex)
                    {
                        // Restore ChangesDetected if callback throws
                        if (hadChanges)
                        {
                            ChangesDetected = true;
#if DEBUG
                            // Use itemName and itemGuid (already captured safely) - never use property getters
                            var callbackItemInfo = $"{itemName ?? "item"} (0x{itemGuid})";
                            log.Warn($"[SAVE DEBUG] {callbackItemInfo} Exception in save callback - restored ChangesDetected to prevent data loss: {ex.Message}");
#endif
                        }
                        log.Error($"Exception in save callback for {itemName} (0x{itemGuid}): {ex.Message}");
                        onCompleted?.Invoke(false);
                    }
                    finally
                    {
                        // ALWAYS clear SaveInProgress, even if callback throws
                        SaveInProgress = false;
                        SaveStartTime = DateTime.MinValue; // Reset for next save
                    }
                });
            // For bulk saves, SaveInProgress cleared by caller after bulk completes
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
                var maxAlerts = ServerConfig.db_slow_discord_max_alerts_per_minute.Value;
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
            var queueThreshold = ServerConfig.db_queue_alert_threshold.Value;
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
                var maxAlerts = ServerConfig.db_queue_discord_max_alerts_per_minute.Value;
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
