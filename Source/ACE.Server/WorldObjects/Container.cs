using System;
using System.Collections.Generic;
using System.Linq;

using log4net;

using ACE.Database;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages;
using ACE.Server.Entity;

namespace ACE.Server.WorldObjects
{
    public partial class Container : WorldObject
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // NOTE:
        // Container inventory mutation logic is NOT responsible for persistence guarantees.
        // It ensures in-memory consistency only.
        // Persistence correctness is enforced by higher-level save orchestration
        // (Player saves, Bank saves, SerializedShardDatabase).
        // 
        // This prevents future devs from "fixing" this by adding forced saves everywhere
        // and breaking performance. Container mutations should set ChangesDetected = true
        // and let the save orchestration layer handle when and how to persist.

        /// <summary>
        /// Cache for side containers to avoid repeated LINQ scans with large inventories
        /// </summary>
        private List<Container> _cachedSideContainers = null;
        private bool _sideContainersCacheDirty = true;

        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public Container(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            InitializePropertyDictionaries();
            SetEphemeralValues(false);

            InventoryLoaded = true;
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public Container(Biota biota) : base(biota)
        {
            if (Biota.TryRemoveProperty(PropertyBool.Open, BiotaDatabaseLock))
                ChangesDetected = true;

            // This is a temporary fix for objects that were loaded with this PR when EncumbranceVal was not treated as ephemeral. 2020-03-28
            // This can be removed later.
            //if (Biota.PropertiesInt.ContainsKey(PropertyInt.EncumbranceVal))
            //{
            //    var weenie = DatabaseManager.World.GetCachedWeenie(biota.WeenieClassId);

            //    if (weenie != null && weenie.PropertiesInt.TryGetValue(PropertyInt.EncumbranceVal, out var value))
            //    {
            //        if (biota.PropertiesInt[PropertyInt.EncumbranceVal] != value)
            //        {
            //            biota.PropertiesInt[PropertyInt.EncumbranceVal] = value;
            //            ChangesDetected = true;
            //        }
            //    }
            //    else
            //    {
            //        biota.PropertiesInt.Remove(PropertyInt.EncumbranceVal);
            //        ChangesDetected = true;
            //    }
            //}

            // This is a temporary fix for objects that were loaded with this PR when Value was not treated as ephemeral. 2020-03-28
            // This can be removed later.
            //if (this is not Creature && Biota.PropertiesInt.ContainsKey(PropertyInt.Value))
            //{
            //    var weenie = DatabaseManager.World.GetCachedWeenie(biota.WeenieClassId);

            //    if (weenie != null && weenie.PropertiesInt.TryGetValue(PropertyInt.Value, out var value))
            //    {
            //        if (biota.PropertiesInt[PropertyInt.Value] != value)
            //        {
            //            biota.PropertiesInt[PropertyInt.Value] = value;
            //            ChangesDetected = true;
            //        }
            //    }
            //    else
            //    {
            //        biota.PropertiesInt.Remove(PropertyInt.Value);
            //        ChangesDetected = true;
            //    }
            //}

            InitializePropertyDictionaries();
            SetEphemeralValues(true);

            // A player has their possessions passed via the ctor. All other world objects must load their own inventory
            if (this is not Player && !ObjectGuid.IsPlayer(ContainerId ?? 0))
            {
                DatabaseManager.Shard.GetInventoryInParallel(biota.Id, false, biotas =>
                {
                    EnqueueAction(new ActionEventDelegate(ActionType.Container_SortBiotasIntoInventory, () => SortBiotasIntoInventory(biotas)));
                });
            }
        }

        private void InitializePropertyDictionaries()
        {
            if (ephemeralPropertyInts == null)
                ephemeralPropertyInts = new Dictionary<PropertyInt, int?>();
        }

        private void SetEphemeralValues(bool fromBiota)
        {
            ephemeralPropertyInts.TryAdd(PropertyInt.EncumbranceVal, EncumbranceVal ?? 0); // Containers are init at 0 burden or their initial value from database. As inventory/equipment is added the burden will be increased
            if (!(this is Creature) && !(this is Corpse)) // Creatures/Corpses do not have a value
                ephemeralPropertyInts.TryAdd(PropertyInt.Value, Value ?? 0);

            //CurrentMotionState = motionStateClosed; // What container defaults to open?

            if (!fromBiota && !(this is Creature))
                GenerateContainList();

            if (!ContainerCapacity.HasValue)
                ContainerCapacity = 0;

            if (!UseRadius.HasValue)
                UseRadius = 0.5f;

            IsOpen = false;
        }


        public bool InventoryLoaded { get; private set; }

        /// <summary>
        /// This will contain all main pack items, and all side slot items.<para />
        /// To access items inside of the side slot items, you'll need to access that items.Inventory dictionary.<para />
        /// Do not manipulate this dictionary directly.
        /// </summary>
        public Dictionary<ObjectGuid, WorldObject> Inventory { get; } = new Dictionary<ObjectGuid, WorldObject>();

        /// <summary>
        /// The only time this should be used is to populate Inventory from the ctor.
        /// </summary>
        protected void SortBiotasIntoInventory(IEnumerable<ACE.Database.Models.Shard.Biota> biotas)
        {
            var worldObjects = new List<WorldObject>();

            foreach (var biota in biotas)
            {
                if (biota == null)
                {
                    log.Error($"Null biota detected in inventory loading for container {Name} (0x{Guid:X8}). Skipping null biota.");
                    continue;
                }
                
                // DEBUG: Check ContainerId in the biota BEFORE creating WorldObject
                uint? biotaContainerId = null;
                if (biota.BiotaPropertiesIID != null)
                {
                    var containerProp = biota.BiotaPropertiesIID.FirstOrDefault(p => p.Type == (ushort)PropertyInstanceId.Container);
                    if (containerProp != null)
                        biotaContainerId = containerProp.Value;
                }

                var worldObject = WorldObjectFactory.CreateWorldObject(biota);
                if (worldObject != null)
                {
                    // DEBUG: Check ContainerId after WorldObject creation
                    log.Debug($"[LOAD DEBUG] Creating WorldObject from biota {biota.Id} (0x{biota.Id:X8}) in container {Name} (0x{Guid:X8}) | Biota ContainerId={biotaContainerId} (0x{(biotaContainerId ?? 0):X8}) | WorldObject ContainerId={worldObject.ContainerId} (0x{(worldObject.ContainerId ?? 0):X8}) | Match={biotaContainerId == worldObject.ContainerId}");
                    
                    worldObjects.Add(worldObject);
                }
                else
                    log.Warn($"Failed to create WorldObject from biota {biota.Id} (WeenieClassId: {biota.WeenieClassId}, WeenieType: {biota.WeenieType}) in container {Guid}");
            }

            SortWorldObjectsIntoInventory(worldObjects);

            if (worldObjects.Count > 0)
                log.Error("Inventory detected without a container to put it in to.");
        }

        /// <summary>
        /// The only time this should be used is to populate Inventory from the ctor.
        /// This will remove from worldObjects as they're sorted.
        /// </summary>
        private void SortWorldObjectsIntoInventory(IList<WorldObject> worldObjects)
        {
            var player = this as Player;

            // This will pull out all of our main pack items and side slot items (foci & containers)
            for (int i = worldObjects.Count - 1; i >= 0; i--)
            {
                var itemContainerId = worldObjects[i].ContainerId ?? 0;
                var thisContainerId = Biota.Id;
                var matches = itemContainerId == thisContainerId;
                
                if (player != null)
                {
                    log.Debug($"[LOAD DEBUG] SortWorldObjectsIntoInventory checking {worldObjects[i].Name} (0x{worldObjects[i].Guid}) | Item ContainerId={itemContainerId} (0x{itemContainerId:X8}) | This ContainerId={thisContainerId} (0x{thisContainerId:X8}) | Matches={matches}");
                }
                
                if (matches)
                {
                    Inventory[worldObjects[i].Guid] = worldObjects[i];
                    worldObjects[i].Container = this;

                    if (worldObjects[i].WeenieType != WeenieType.Container) // We skip over containers because we'll add their burden/value in the next loop.
                    {
                        EncumbranceVal += (worldObjects[i].EncumbranceVal ?? 0);
                        Value += (worldObjects[i].Value ?? 0);
                    }

                    worldObjects.RemoveAt(i);
                }
            }

            // Make sure placement positions are correct. They could get out of sync from a client issue, server issue, or orphaned biota
            var mainPackItems = Inventory.Values.Where(wo => !wo.UseBackpackSlot).OrderBy(wo => wo.PlacementPosition).ToList();
            for (int i = 0; i < mainPackItems.Count; i++)
                mainPackItems[i].PlacementPosition = i;
            var sidPackItems = Inventory.Values.Where(wo => wo.UseBackpackSlot).OrderBy(wo => wo.PlacementPosition).ToList();
            for (int i = 0; i < sidPackItems.Count; i++)
                sidPackItems[i].PlacementPosition = i;

            InventoryLoaded = true;

            // All that should be left are side pack sub contents.

            var sideContainers = GetCachedSideContainers();
            if (player != null)
            {
                log.Debug($"[LOAD DEBUG] Player {player.Name} has {sideContainers.Count} side containers, {worldObjects.Count} remaining items to sort");
            }
            foreach (var container in sideContainers)
            {
                if (player != null)
                {
                    log.Debug($"[LOAD DEBUG] Processing side container {container.Name} (0x{container.Guid}) | Biota.Id={container.Biota.Id} (0x{container.Biota.Id:X8}) | Remaining items={worldObjects.Count}");
                }
                container.SortWorldObjectsIntoInventory(worldObjects); // This will set the InventoryLoaded flag for this sideContainer
                EncumbranceVal += container.EncumbranceVal; // This value includes the containers burden itself + all child items
                Value += container.Value; // This value includes the containers value itself + all child items
            }
            
            if (player != null && worldObjects.Count > 0)
            {
                log.Warn($"[LOAD DEBUG] Player {player.Name} has {worldObjects.Count} items that couldn't be sorted into any container:");
                foreach (var wo in worldObjects)
                {
                    log.Warn($"[LOAD DEBUG]   - {wo.Name} (0x{wo.Guid}) | ContainerId={wo.ContainerId} (0x{(wo.ContainerId ?? 0):X8})");
                }
            }

            OnInitialInventoryLoadCompleted();
        }

        /// <summary>
        /// Counts the number of actual inventory items, ignoring Packs/Foci.
        /// </summary>
        private int CountPackItems()
        {
            int count = 0;
            foreach (var wo in Inventory.Values)
            {
                if (!wo.UseBackpackSlot)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Counts the number of containers in inventory, including Foci.
        /// </summary>
        private int CountContainers()
        {
            int count = 0;
            foreach (var wo in Inventory.Values)
            {
                if (wo.UseBackpackSlot)
                    count++;
            }
            return count;
        }

        public int GetFreeInventorySlots(bool includeSidePacks = true)
        {
            int freeSlots = (ItemCapacity ?? 0) - CountPackItems();

            if (includeSidePacks)
            {
                foreach (var item in Inventory.Values)
                {
                    if (item is Container sidePack)
                        freeSlots += (sidePack.ItemCapacity ?? 0) - sidePack.CountPackItems();
                }
            }

            return freeSlots;
        }

        public int GetFreeContainerSlots()
        {
            return (ContainerCapacity ?? 0) - CountContainers();
        }

        /// <summary>
        /// This method will check all containers in our possession
        /// in main inventory or any side packs
        /// </summary>
        public bool HasInventoryItem(ObjectGuid objectGuid)
        {
            return GetInventoryItem(objectGuid) != null;
        }

        /// <summary>
        /// This method will check all containers in our possession
        /// in main inventory or any side packs
        /// </summary>
        public WorldObject GetInventoryItem(ObjectGuid objectGuid)
        {
            // Defensive guard: fail safe if inventory not loaded
            if (!InventoryLoaded)
                return null;

            return GetInventoryItem(objectGuid, out _);
        }

        /// <summary>
        /// This method will check all containers in our possession
        /// in main inventory or any side packs
        /// </summary>
        public WorldObject GetInventoryItem(uint objectGuid)
        {
            // Defensive guard: fail safe if inventory not loaded
            if (!InventoryLoaded)
                return null;

            return GetInventoryItem(new ObjectGuid(objectGuid), out _); // todo remove this so it doesnt' create a new ObjectGuid
        }

        /// <summary>
        /// This method will check all containers in our possession
        /// in main inventory or any side packs
        /// </summary>
        public WorldObject GetInventoryItem(ObjectGuid objectGuid, out Container container)
        {
            // Defensive guard: fail safe if inventory not loaded
            // In practice, ACE already assumes this, but now that logic is more complex,
            // we add this guard to prevent issues during async load.
            if (!InventoryLoaded)
            {
                container = null;
                return null;
            }

            // First search my main pack for this item..
            if (Inventory.TryGetValue(objectGuid, out var value))
            {
                container = this;
                return value;
            }

            // Next search all containers for item.. run function again for each container.
            var sideContainers = GetCachedSideContainers();
            foreach (var sideContainer in sideContainers)
            {
                var containerItem = ((Container)sideContainer).GetInventoryItem(objectGuid);

                if (containerItem != null)
                {
                    container = (Container)sideContainer;
                    return containerItem;
                }
            }

            container = null;
            return null;
        }

        /// <summary>
        /// Gets side containers from cache, rebuilding if necessary.
        /// Avoids repeated LINQ allocations with large inventories.
        /// </summary>
        private List<Container> GetCachedSideContainers()
        {
            // Defensive guard: fail safe if inventory not loaded
            // Return empty list to avoid null reference issues
            if (!InventoryLoaded)
                return new List<Container>();

            if (_sideContainersCacheDirty || _cachedSideContainers == null)
            {
                if (_cachedSideContainers == null)
                    _cachedSideContainers = new List<Container>();
                else
                    _cachedSideContainers.Clear();

                foreach (var item in Inventory.Values)
                {
                    if (item is Container container)
                        _cachedSideContainers.Add(container);
                }

                // Sort by PlacementPosition to maintain consistent ordering
                _cachedSideContainers.Sort((a, b) => (a.PlacementPosition ?? 0).CompareTo(b.PlacementPosition ?? 0));
                _sideContainersCacheDirty = false;
            }
            return _cachedSideContainers;
        }

        /// <summary>
        /// Invalidates the side containers cache when containers are added or removed
        /// </summary>
        private void InvalidateSideContainersCache()
        {
            _sideContainersCacheDirty = true;
        }

        /// <summary>
        /// This method is used to get all inventory items of a type in this container (example of usage get all items of coin on player)
        /// </summary>
        public List<WorldObject> GetInventoryItemsOfTypeWeenieType(WeenieType type)
        {
            var items = new List<WorldObject>();

            // first search me / add all items of type.
            foreach (var item in Inventory.Values)
            {
                if (item.WeenieType == type)
                    items.Add(item);
            }

            // Sort by PlacementPosition
            items.Sort((a, b) => (a.PlacementPosition ?? 0).CompareTo(b.PlacementPosition ?? 0));

            // next search all containers for type.. run function again for each container.
            var sideContainers = GetCachedSideContainers();
            foreach (var container in sideContainers)
                items.AddRange(container.GetInventoryItemsOfTypeWeenieType(type));

            return items;
        }

        /// <summary>
        /// Returns the inventory items matching a weenie class id
        /// </summary>
        public List<WorldObject> GetInventoryItemsOfWCID(uint weenieClassId)
        {
            // Defensive guard: fail safe if inventory not loaded
            if (!InventoryLoaded)
                return new List<WorldObject>();

            var items = new List<WorldObject>();

            // search main pack / creature
            foreach (var item in Inventory.Values)
            {
                if (item.WeenieClassId == weenieClassId)
                    items.Add(item);
            }

            // Sort by PlacementPosition
            items.Sort((a, b) => (a.PlacementPosition ?? 0).CompareTo(b.PlacementPosition ?? 0));

            // next search any side containers
            var sideContainers = GetCachedSideContainers();
            foreach (var container in sideContainers)
                items.AddRange(container.GetInventoryItemsOfWCID(weenieClassId));

            return items;
        }

        /// <summary>
        /// Returns the total # of inventory items matching a wcid
        /// </summary>
        public int GetNumInventoryItemsOfWCID(uint weenieClassId)
        {
            return GetInventoryItemsOfWCID(weenieClassId).Select(i => i.StackSize ?? 1).Sum();
        }

        /// <summary>
        /// Returns the inventory items matching a weenie class name
        /// </summary>
        public List<WorldObject> GetInventoryItemsOfWeenieClass(string weenieClassName)
        {
            // Defensive guard: fail safe if inventory not loaded
            if (!InventoryLoaded)
                return new List<WorldObject>();

            var items = new List<WorldObject>();

            // search main pack / creature
            foreach (var item in Inventory.Values)
            {
                if (item.WeenieClassName.Equals(weenieClassName, StringComparison.OrdinalIgnoreCase))
                    items.Add(item);
            }

            // Sort by PlacementPosition
            items.Sort((a, b) => (a.PlacementPosition ?? 0).CompareTo(b.PlacementPosition ?? 0));

            // next search any side containers
            var sideContainers = GetCachedSideContainers();
            foreach (var container in sideContainers)
                items.AddRange(container.GetInventoryItemsOfWeenieClass(weenieClassName));

            return items;
        }

        /// <summary>
        /// Returns the total # of inventory items matching a weenie class name
        /// </summary>
        public int GetNumInventoryItemsOfWeenieClass(string weenieClassName)
        {
            return GetInventoryItemsOfWeenieClass(weenieClassName).Select(i => i.StackSize ?? 1).Sum();
        }

        /// <summary>
        /// Returns all of the trade notes from inventory + side packs
        /// </summary>
        public List<WorldObject> GetTradeNotes()
        {
            // Defensive guard: fail safe if inventory not loaded
            if (!InventoryLoaded)
                return new List<WorldObject>();

            var items = new List<WorldObject>();

            // search main pack / creature
            foreach (var item in Inventory.Values)
            {
                if (item.ItemType == ItemType.PromissoryNote && item.WeenieClassId != 43901)
                    items.Add(item);
            }

            // Sort by PlacementPosition
            items.Sort((a, b) => (a.PlacementPosition ?? 0).CompareTo(b.PlacementPosition ?? 0));

            // next search any side containers
            var sideContainers = GetCachedSideContainers();
            foreach (var container in sideContainers)
                items.AddRange(container.GetTradeNotes());

            return items;
        }

        /// <summary>
        /// If enough burden is available, this will try to add an item to the main pack. If the main pack is full, it will try to add it to the first side pack with room.<para />
        /// It will also increase the EncumbranceVal and Value.
        /// </summary>
        public bool TryAddToInventory(WorldObject worldObject, int placementPosition = 0, bool limitToMainPackOnly = false, bool burdenCheck = true)
        {
            if (worldObject == null) return false;

            return TryAddToInventory(worldObject, out _, placementPosition, limitToMainPackOnly, burdenCheck);
        }

        /// <summary>
        /// Returns TRUE if there are enough free inventory slots and burden available to add items
        /// </summary>
        public bool CanAddToInventory(int totalContainerObjectsToAdd, int totalInventoryObjectsToAdd, int totalBurdenToAdd)
        {
            if (this is Player player && !player.HasEnoughBurdenToAddToInventory(totalBurdenToAdd))
                return false;

            return (GetFreeContainerSlots() >= totalContainerObjectsToAdd) && (GetFreeInventorySlots() >= totalInventoryObjectsToAdd);
        }

        /// <summary>
        /// Returns TRUE if there are enough free inventory slots and burden available to add item
        /// </summary>
        public bool CanAddToInventory(WorldObject worldObject)
        {
            if (this is Player player && !player.HasEnoughBurdenToAddToInventory(worldObject))
                return false;

            if (worldObject.UseBackpackSlot)
                return GetFreeContainerSlots() > 0;
            else
                return GetFreeInventorySlots() > 0;
        }

        /// <summary>
        /// Returns TRUE if there are enough free inventory slots and burden available to add all items
        /// </summary>
        public bool CanAddToInventory(List<WorldObject> worldObjects)
        {
            return CanAddToInventory(worldObjects, out _, out _);
        }

        /// <summary>
        /// Returns TRUE if there are enough free inventory slots and burden available to add all items
        /// </summary>
        public bool CanAddToInventory(List<WorldObject> worldObjects, out bool TooEncumbered, out bool NotEnoughFreeSlots)
        {
            TooEncumbered = false;
            NotEnoughFreeSlots = false;

            if (worldObjects.Count == 0) // There are no objects to add (e.g. 1 way trade)
                return true;

            if (this is Player player && !player.HasEnoughBurdenToAddToInventory(worldObjects))
            {
                TooEncumbered = true;
                return false;
            }

            var containers = worldObjects.Where(w => w.UseBackpackSlot).ToList();
            if (containers.Count > 0)
            {
                if (GetFreeContainerSlots() < containers.Count)
                {
                    NotEnoughFreeSlots = true;
                    return false;
                }
            }

            if (GetFreeInventorySlots() < (worldObjects.Count - containers.Count))
            {
                NotEnoughFreeSlots = true;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns TRUE if there are enough free inventory slots and burden available to add item
        /// </summary>
        public bool CanAddToContainer(WorldObject worldObject, bool includeSidePacks = true)
        {
            if (this is Player player && !player.HasEnoughBurdenToAddToInventory(worldObject))
                return false;

            if (worldObject.UseBackpackSlot)
                return GetFreeContainerSlots() > 0;
            else
                return GetFreeInventorySlots(includeSidePacks) > 0;
        }

        /// <summary>
        /// Returns TRUE if there are enough free burden available to merge item and merge target will not exceed maximum stack size
        /// </summary>
        public bool CanMergeToInventory(WorldObject worldObject, WorldObject mergeTarget, int mergeAmout)
        {
            if (this is Player player && !player.HasEnoughBurdenToAddToInventory(worldObject))
                return false;

            var currentStackSize = mergeTarget.StackSize;
            var maxStackSize = mergeTarget.MaxStackSize;
            var newStackSize = currentStackSize + mergeAmout;

            return newStackSize <= maxStackSize;
        }

        /// <summary>
        /// If enough burden is available, this will try to add an item to the main pack. If the main pack is full, it will try to add it to the first side pack with room.<para />
        /// It will also increase the EncumbranceVal and Value.
        /// </summary>
        public virtual bool TryAddToInventory(WorldObject worldObject, out Container container, int placementPosition = 0, bool limitToMainPackOnly = false, bool burdenCheck = true)
        {
            var containerInfo = this is Player p ? $"Player {p.Name}" : $"{Name} (0x{Guid})";
            var itemInfo = worldObject is Player itemPlayer ? $"Player {itemPlayer.Name}" : $"{worldObject.Name} (0x{worldObject.Guid})";
            log.Debug($"[SAVE DEBUG] TryAddToInventory START for {itemInfo} | Target container={containerInfo} | limitToMainPackOnly={limitToMainPackOnly} | burdenCheck={burdenCheck} | placementPosition={placementPosition}");
            
            // Step 1: Capture container identity from biota (not live object state)
            // This value is stable across the whole move
            uint? oldContainerBiotaId = null;
            worldObject.BiotaDatabaseLock.EnterReadLock();
            try
            {
                if (worldObject.Biota.PropertiesIID != null &&
                    worldObject.Biota.PropertiesIID.TryGetValue(PropertyInstanceId.Container, out var cid))
                    oldContainerBiotaId = cid;
            }
            finally
            {
                worldObject.BiotaDatabaseLock.ExitReadLock();
            }
            
            // Step 2: Begin mutation before any removal
            // Always begin mutation tracking for all adds (ground→container, newly spawned→container,
            // loot→inventory, split created stacks, moves, etc.) to ensure atomicity
            // The oldContainerBiotaId is used for logging/diagnostics, not for gating
            worldObject.BeginContainerMutation(oldContainerBiotaId);
            
            // Step 3: Wrap the entire body in try/finally for proper cleanup
            try
            {
                // bug: should be root owner
                if (this is Player player && burdenCheck)
                {
                    if (!player.HasEnoughBurdenToAddToInventory(worldObject))
                    {
                        log.Debug($"[SAVE DEBUG] TryAddToInventory FAILED for {itemInfo} - insufficient burden in {containerInfo}");
                        container = null;
                        // End mutation on failure
                        worldObject.EndContainerMutation(oldContainerBiotaId, null);
                        return false;
                    }
                }

            IList<WorldObject> containerItems;

            if (worldObject.UseBackpackSlot)
            {
                containerItems = Inventory.Values.Where(i => i.UseBackpackSlot).ToList();

                if ((ContainerCapacity ?? 0) <= containerItems.Count)
                {
                    log.Debug($"[SAVE DEBUG] TryAddToInventory FAILED for {itemInfo} - container capacity full in {containerInfo} ({containerItems.Count}/{ContainerCapacity ?? 0})");
                    container = null;
                    // End mutation on failure
                    worldObject.EndContainerMutation(oldContainerBiotaId, null);
                    return false;
                }
            }
            else
            {
                containerItems = Inventory.Values.Where(i => !i.UseBackpackSlot).ToList();

                if ((ItemCapacity ?? 0) <= containerItems.Count)
                {
                    // Can we add this to any side pack?
                    if (!limitToMainPackOnly)
                    {
                        var containers = Inventory.Values.OfType<Container>().ToList();
                        containers.Sort((a, b) => (a.Placement ?? 0).CompareTo(b.Placement ?? 0));
                        
                        log.Debug($"[SAVE DEBUG] TryAddToInventory main pack full for {itemInfo} in {containerInfo} ({containerItems.Count}/{ItemCapacity ?? 0}), trying {containers.Count} side packs");

                        foreach (var sidePack in containers)
                        {
                            log.Debug($"[SAVE DEBUG] TryAddToInventory trying side pack {sidePack.Name} (0x{sidePack.Guid}) for {itemInfo}");
                            if (sidePack.TryAddToInventory(worldObject, out container, placementPosition, true))
                            {
                                EncumbranceVal += (worldObject.EncumbranceVal ?? 0);
                                Value += (worldObject.Value ?? 0);
                                
                                // FIX (PR #323): End mutation (decrement depth) when successfully added to side pack (recursive case)
                                // This was missing and caused SaveInProgress to get stuck during logout
                                worldObject.EndContainerMutation(oldContainerBiotaId, sidePack.Biota.Id);
                                
                                log.Debug($"[SAVE DEBUG] TryAddToInventory SUCCESS - {itemInfo} added to side pack {sidePack.Name} (0x{sidePack.Guid})");
                                return true;
                            }
                        }

                        
                        log.Debug($"[SAVE DEBUG] TryAddToInventory FAILED for {itemInfo} - all side packs full in {containerInfo}");
                    }
                    else
                    {
                        log.Debug($"[SAVE DEBUG] TryAddToInventory FAILED for {itemInfo} - main pack full and limitToMainPackOnly=true in {containerInfo}");
                    }

                    container = null;
                    // End mutation on failure
                    worldObject.EndContainerMutation(oldContainerBiotaId, null);
                    return false;
                }
            }

            if (Inventory.ContainsKey(worldObject.Guid))
            {
                container = null;
                // End mutation on failure
                worldObject.EndContainerMutation(oldContainerBiotaId, null);
                return false;
            }

            worldObject.Location = null;
            worldObject.Placement = ACE.Entity.Enum.Placement.Resting;

            worldObject.OwnerId = Guid.Full;
                // CRITICAL FIX: Use Biota.Id instead of Guid.Full for ContainerId
                // SortWorldObjectsIntoInventory compares against Biota.Id, so ContainerId must match Biota.Id
                // For players, Biota.Id == Guid.Full, but for side packs, Biota.Id is the database ID (not the GUID)
                worldObject.ContainerId = Biota.Id;
                worldObject.Container = this;
                worldObject.PlacementPosition = placementPosition; // Server only variable that we use to remember/restore the order in which items exist in a container
            
            // Verify ContainerId was set correctly
            var newContainerId = worldObject.ContainerId;
            var containerBiotaId = Biota.Id;
            log.Debug($"[SAVE DEBUG] TryAddToInventory setting ContainerId for {itemInfo} | Old ContainerBiotaId={oldContainerBiotaId} (0x{(oldContainerBiotaId ?? 0):X8}) | Set ContainerId={Biota.Id} (0x{Biota.Id:X8}) | Read back ContainerId={newContainerId} (0x{(newContainerId ?? 0):X8}) | Container={containerInfo} | Container Biota.Id={containerBiotaId} (0x{containerBiotaId:X8})");
            
            // Ensure ContainerId property matches Container's Biota.Id - if they don't match, fix it
            if (worldObject.ContainerId != Biota.Id)
            {
                log.Warn($"[SAVE DEBUG] TryAddToInventory ContainerId mismatch detected for {itemInfo} | ContainerId property={worldObject.ContainerId} (0x{(worldObject.ContainerId ?? 0):X8}) | Container.Biota.Id={Biota.Id} (0x{Biota.Id:X8}) | Fixing...");
                worldObject.ContainerId = Biota.Id;
            }

            // Move all the existing items PlacementPosition over.
            if (!worldObject.UseBackpackSlot)
            {
                foreach (var item in containerItems)
                {
                    if (!item.UseBackpackSlot && item.PlacementPosition >= placementPosition)
                        item.PlacementPosition++;
                }
            }
            else
            {
                foreach (var item in containerItems)
                {
                    if (item.UseBackpackSlot && item.PlacementPosition >= placementPosition)
                        item.PlacementPosition++;
                }
            }

                Inventory.Add(worldObject.Guid, worldObject);

                // Invalidate side containers cache if we added a container
                if (worldObject is Container)
                    InvalidateSideContainersCache();

                EncumbranceVal += (worldObject.EncumbranceVal ?? 0);
                Value += (worldObject.Value ?? 0);

                container = this;

                OnAddItem(worldObject);

                // Step 4: End mutation after successful add
                worldObject.EndContainerMutation(oldContainerBiotaId, Biota.Id);

                return true;
            }
            catch
            {
                // Step 5: On failure, end mutation (rollback already handled by caller)
                worldObject.EndContainerMutation(oldContainerBiotaId, null);
                throw;
            }
        }

        /// <summary>
        /// Removes all items from an inventory
        /// </summary>
        /// <returns>TRUE if all items were removed successfully</returns>
        public bool ClearInventory(bool forceSave = false)
        {
            InvalidateSideContainersCache(); // Cache will be stale after clearing

            var success = true;
            var itemGuids = Inventory.Keys.ToList();
            foreach (var itemGuid in itemGuids)
            {
                if (!TryRemoveFromInventory(itemGuid, out var item, forceSave))
                    success = false;

                if (success)
                    item.Destroy();
            }
            if (forceSave)
                SaveBiotaToDatabase();

            return success;
        }

        /// <summary>
        /// Removes all items from an inventory that are unmanaged/controlled
        /// </summary>
        /// <returns>TRUE if all unmanaged items were removed successfully</returns>
        public bool ClearUnmanagedInventory(bool forceSave = false)
        {
            if (this is Storage || WeenieClassId == (uint)ACE.Entity.Enum.WeenieClassName.W_STORAGE_CLASS)
                return false; // Do not clear storage, ever.

            var success = true;
            // Build list of unmanaged item GUIDs (optimized from Where + Select + ToList)
            var itemGuids = new List<ObjectGuid>();
            foreach (var kvp in Inventory)
            {
                if (kvp.Value.GeneratorId == null)
                    itemGuids.Add(kvp.Key);
            }

            // Now safe to modify inventory during this iteration
            foreach (var itemGuid in itemGuids)
            {
                if (!TryRemoveFromInventory(itemGuid, out var item, forceSave))
                    success = false;

                if (success)
                    item.Destroy();
            }
            if (forceSave)
                SaveBiotaToDatabase();

            return success;
        }

        /// <summary>
        /// This will clear the ContainerId and PlacementPosition properties.<para />
        /// It will also subtract the EncumbranceVal and Value.
        /// </summary>
        public bool TryRemoveFromInventory(ObjectGuid objectGuid, bool forceSave = false)
        {
            return TryRemoveFromInventory(objectGuid, out _, forceSave);
        }

        /// <summary>
        /// This will clear the ContainerId and PlacementPosition properties and remove the object from the Inventory dictionary.<para />
        /// It will also subtract the EncumbranceVal and Value.
        /// </summary>
        public bool TryRemoveFromInventory(ObjectGuid objectGuid, out WorldObject item, bool forceSave = false)
        {
            // first search me / add all items of type.
            if (Inventory.Remove(objectGuid, out item))
            {
                int removedItemsPlacementPosition = item.PlacementPosition ?? 0;

                // Do NOT increment mutation depth here - we cannot distinguish between:
                // 1. A move operation (remove then add to different container) - should suppress side effects
                // 2. A final removal (drop, unequip, etc.) - should NOT suppress side effects
                // 
                // Mutation depth should only be incremented in TryAddToInventory when we detect
                // an actual move (oldContainerId != new container). This ensures we only suppress
                // side effects during moves, not during legitimate final removals.

                item.OwnerId = null;
                item.ContainerId = null;
                item.Container = null;
                item.PlacementPosition = null;

                // Move all the existing items PlacementPosition over (optimized: single loop)
                var useBackpackSlot = item.UseBackpackSlot;
                foreach (var invItem in Inventory.Values)
                {
                    // Only adjust items in same category (pack items OR containers)
                    if (invItem.UseBackpackSlot == useBackpackSlot && invItem.PlacementPosition > removedItemsPlacementPosition)
                        invItem.PlacementPosition--;
                }

                EncumbranceVal -= (item.EncumbranceVal ?? 0);
                Value -= (item.Value ?? 0);

                // Invalidate side containers cache if we removed a container
                if (item is Container)
                    InvalidateSideContainersCache();

                // Guard forceSave during mutation to prevent saves with ContainerId = null during moves
                // During a move sequence (TryRemoveFromInventory -> TryAddToInventory), if forceSave == true,
                // we could enqueue a save with ContainerId = null followed by a save with the new container.
                // This is usually coalesced away, but it's a real ordering risk.
                // Only allow forceSave for final removals, not during moves.
                if (forceSave && !item.IsInContainerMutation)
                    item.SaveBiotaToDatabase();

                OnRemoveItem(item);

                return true;
            }

            // next search all containers for item.. run function again for each container.
            var sideContainers = GetCachedSideContainers();
            foreach (var container in sideContainers)
            {
                if (container.TryRemoveFromInventory(objectGuid, out item))
                {
                    EncumbranceVal -= (item.EncumbranceVal ?? 0);
                    Value -= (item.Value ?? 0);

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// This is raised by Player.HandleActionUseItem.<para />
        /// The item does not exist in the players possession.<para />
        /// If the item was outside of range, the player will have been commanded to move using DoMoveTo before ActOnUse is called.<para />
        /// When this is called, it should be assumed that the player is within range.
        /// </summary>
        public override void ActOnUse(WorldObject wo)
        {
            if (!(wo is Player player))
                return;

            // If we have a previous container open, let's close it
            if (player.LastOpenedContainerId != ObjectGuid.Invalid && player.LastOpenedContainerId != Guid)
            {
                var lastOpenedContainer = CurrentLandblock?.GetObject(player.LastOpenedContainerId) as Container;

                if (lastOpenedContainer != null && lastOpenedContainer.IsOpen && lastOpenedContainer.Viewer == player.Guid.Full)
                    lastOpenedContainer.Close(player);
            }

            if ((OwnerId.HasValue && OwnerId.Value > 0) || (ContainerId.HasValue && ContainerId.Value > 0))
                return; // Do nothing else if container is owned by something.

            if (!IsOpen)
            {
                Open(player);
            }
            else
            {
                if (Viewer == 0)
                    Close(null);
                else if (Viewer == player.Guid.Full)
                    Close(player);
                else
                    player.SendTransientError(InUseMessage);
            }
        }

        public string InUseMessage
        {
            get
            {
                // verified this message was sent for corpses, instead of WeenieErrorWithString.The_IsCurrentlyInUse
                var currentViewer = "someone else";

                if (ServerConfig.container_opener_name.Value)
                {
                    var name = CurrentLandblock?.GetObject(Viewer)?.Name;
                    if (name != null)
                        currentViewer = name;
                }
                return $"The {Name} is already in use by {currentViewer}!";
            }
        }

        public virtual void Open(Player player)
        {
            if (IsOpen)
            {
                player.SendTransientError(InUseMessage);
                return;
            }

            player.LastOpenedContainerId = Guid;

            IsOpen = true;

            Viewer = player.Guid.Full;

            DoOnOpenMotionChanges();

            SendInventory(player);

            if (!(this is Chest) && !ResetMessagePending && ResetInterval.HasValue)
            {
                var actionChain = new ActionChain();
                if (ResetInterval.Value < 15)
                    actionChain.AddDelaySeconds(15);
                else
                    actionChain.AddDelaySeconds(ResetInterval.Value);
                actionChain.AddAction(this, ActionType.Container_Reset, Reset);
                //actionChain.AddAction(this, () =>
                //{
                //    Close(player);
                //});
                actionChain.EnqueueChain();

                ResetMessagePending = true;
            }
        }

        protected virtual float DoOnOpenMotionChanges()
        {
            return 0;
        }

        private void SendInventory(Player player)
        {
            // send createobject for all objects in this container's inventory to player
            var itemsToSend = new List<OutboundGameMessage>();
            var containerViews = new List<OutboundGameMessage>();

            // Optimized: Single loop instead of two separate scans
            foreach (var item in Inventory.Values)
            {
                // FIXME: only send messages for unknown objects
                itemsToSend.Add(new GameMessageCreateObject(item));

                if (item is Container container)
                {
                    foreach (var containerItem in container.Inventory.Values)
                        itemsToSend.Add(new GameMessageCreateObject(containerItem));
                    
                    // Send sub-container view (previously done in second loop)
                    containerViews.Add(new GameEventViewContents(player.Session, container));
                }
            }

            player.Session.Network.EnqueueSend(new GameEventViewContents(player.Session, this));
            
            // Send all container views
            if (containerViews.Count > 0)
                player.Session.Network.EnqueueSend(containerViews.ToArray());

            player.Session.Network.EnqueueSend(itemsToSend.ToArray());
        }

        private void SendDeletesForMyInventory(Player player)
        {
            // send deleteobjects for all objects in this container's inventory to player
            var itemsToSend = new List<OutboundGameMessage>();

            foreach (var item in Inventory.Values)
            {
                // FIXME: only send messages for known objects
                itemsToSend.Add(new GameMessageDeleteObject(item));

                if (item is Container container)
                {
                    foreach (var containerItem in container.Inventory.Values)
                        itemsToSend.Add(new GameMessageDeleteObject(containerItem));
                }
            }

            player.Session.Network.EnqueueSend(itemsToSend.ToArray());
        }

        public virtual void Close(Player player)
        {
            if (!IsOpen) return;

            var animTime = DoOnCloseMotionChanges();

            if (animTime <= 0)
                FinishClose(player);
            else
            {
                var actionChain = new ActionChain();
                actionChain.AddDelaySeconds(animTime / 2.0f);
                actionChain.AddAction(this, ActionType.Container_FinishClose, () => FinishClose(player));
                actionChain.EnqueueChain();
            }
        }

        protected virtual float DoOnCloseMotionChanges()
        {
            return 0;
        }

        public virtual void FinishClose(Player player)
        {
            IsOpen = false;
            Viewer = 0;

            if (player != null)
            {
                player.Session.Network.EnqueueSend(new GameEventCloseGroundContainer(player.Session, this));

                if (player.LastOpenedContainerId == Guid)
                    player.LastOpenedContainerId = ObjectGuid.Invalid;

                // send deleteobject for all objects in this container's inventory to player
                // this seems logical, but it bugs out the client for re-opening chests w/ respawned items
                /*var itemsToSend = new List<GameMessage>();

                foreach (var item in Inventory.Values)
                    itemsToSend.Add(new GameMessageDeleteObject(item));

                player.Session.Network.EnqueueSend(itemsToSend.ToArray());*/
            }

        }

        public virtual void Reset()
        {
            InvalidateSideContainersCache(); // Cache will be stale after reset

            var player = CurrentLandblock.GetObject(Viewer) as Player;

            if (IsOpen)
                Close(player);

            //if (IsGenerator)
            //{
            //    ResetGenerator();
            //    if (InitCreate > 0)
            //        Generator_Regeneration();
            //}

            ClearUnmanagedInventory();

            ResetMessagePending = false;
        }

        public void GenerateContainList()
        {
            if (Biota.PropertiesCreateList == null)
                return;

            foreach (var item in Biota.PropertiesCreateList.Where(x => x.DestinationType == DestinationType.Contain || x.DestinationType == DestinationType.ContainTreasure))
            {
                var wo = WorldObjectFactory.CreateNewWorldObject(item.WeenieClassId);

                if (wo == null)
                    continue;

                if (!Guid.IsPlayer())
                    wo.GeneratorId = Guid.Full; // add this to mark item as "managed" so container resets don't delete it.

                if (item.Palette > 0)
                    wo.PaletteTemplate = item.Palette;
                if (item.Shade > 0)
                    wo.Shade = item.Shade;
                if (item.StackSize > 1)
                    wo.SetStackSize(item.StackSize);

                TryAddToInventory(wo);
            }
        }

        public void MergeAllStackables()
        {
            var inventory = Inventory.Values.ToList();

            for (int i = inventory.Count - 1; i > 0; i--)
            {
                var sourceItem = inventory[i];

                if (sourceItem.MaxStackSize == null || sourceItem.MaxStackSize <= 1)
                    continue;

                for (int j = 0; j < i; j++)
                {
                    var destinationItem = inventory[j];

                    if (destinationItem.WeenieClassId != sourceItem.WeenieClassId || destinationItem.StackSize == destinationItem.MaxStackSize)
                        continue;

                    var amount = Math.Min(sourceItem.StackSize ?? 0, (destinationItem.MaxStackSize - destinationItem.StackSize) ?? 0);

                    sourceItem.SetStackSize(sourceItem.StackSize - amount);

                    destinationItem.SetStackSize(destinationItem.StackSize + amount);

                    if (sourceItem.StackSize == 0)
                    {
                        TryRemoveFromInventory(sourceItem.Guid);
                        if (!sourceItem.IsDestroyed)
                            sourceItem.Destroy();
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// This event is raised after the containers items have been completely loaded from the database
        /// </summary>
        protected virtual void OnInitialInventoryLoadCompleted()
        {
            // empty base
        }

        /// <summary>
        /// This event is raised when player adds item to container
        /// </summary>
        protected virtual void OnAddItem(WorldObject addedItem)
        {
            if (addedItem != null)
                UpdateCharms(true, addedItem);
        }

        /// <summary>
        /// This event is raised when player removes item from container
        /// </summary>
        protected virtual void OnRemoveItem(WorldObject removedItem)
        {
            // Suppress enchant invalidation during container churn
            if (removedItem.IsInContainerMutation)
                return;

            if (removedItem != null)
                UpdateCharms(false, removedItem);
        }

        private void UpdateCharms(bool adding, WorldObject item)
        {
            if (GetRootOwner() is not Player player) return;
            ProcessCharmRecursively(player, item, adding);
        }

        private void ProcessCharmRecursively(Player player,  WorldObject item, bool adding) { 
            bool isCharm = item.GetProperty(PropertyBool.IsCharm) ?? false;
            if (isCharm)
            {
                List<uint> spells = [];
                if (item.SpellDID.HasValue) spells.Add(item.SpellDID.Value);
                List<int> knownSpells = item.Biota.GetKnownSpellsIds(item.BiotaDatabaseLock);
                foreach(int spellId in knownSpells) spells.Add((uint)spellId);

                foreach (uint spellId in spells)
                {
                    if (adding)
                    {
                        if(player.EnchantmentManager.GetEnchantment(spellId, item.Guid.Full) == null)
                            player.CreateItemSpell(item, spellId);
                    }
                    else
                    {
                        player.RemoveItemSpell(item, spellId);
                    }
                }
            }

            if (item is not Container container) return;
            List<WorldObject> children = [.. container.Inventory.Values];
            foreach (var child in children) ProcessCharmRecursively(player, child, adding);
        }

        /// <summary>
        /// Helper to find the root owner (Player usually)
        /// </summary>
        public WorldObject GetRootOwner()
        {
            WorldObject current = this;
            int safety = 0;
            while (current.Container != null && safety < 100)
            {
                current = current.Container;
                safety++;
            }
            return current;
        }

        /// <summary>
        /// This event is raised when a stackable item's size changes within this container (merge or split)
        /// </summary>
        public virtual void OnStackSizeChanged(WorldObject stack, int amount)
        {
            // empty base
        }

        public virtual MotionCommand MotionPickup => MotionCommand.Pickup;

        public override bool IsAttunedOrContainsAttuned => base.IsAttunedOrContainsAttuned || Inventory.Values.Any(i => i.IsAttunedOrContainsAttuned);

        public override bool IsStickyAttunedOrContainsStickyAttuned => base.IsStickyAttunedOrContainsStickyAttuned || Inventory.Values.Any(i => i.IsStickyAttunedOrContainsStickyAttuned);

        public override bool IsUniqueOrContainsUnique => base.IsUniqueOrContainsUnique || Inventory.Values.Any(i => i.IsUniqueOrContainsUnique);

        public override bool IsBeingTradedOrContainsItemBeingTraded(HashSet<ObjectGuid> guidList) => base.IsBeingTradedOrContainsItemBeingTraded(guidList) || Inventory.Values.Any(i => i.IsBeingTradedOrContainsItemBeingTraded(guidList));

        public override List<WorldObject> GetUniqueObjects()
        {
            var uniqueObjects = new List<WorldObject>();

            if (Unique != null)
                uniqueObjects.Add(this);

            foreach (var item in Inventory.Values)
                uniqueObjects.AddRange(item.GetUniqueObjects());

            return uniqueObjects;
        }

        public override void OnTalk(WorldObject activator)
        {
            if (activator is Player player)
            {
                if (IsOpen)
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat(ActivationTalk, ChatMessageType.Broadcast));
            }
        }
    }
}
