using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using log4net;

using ACE.Common.Performance;
using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Database.Models.World;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Structure;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    public static class HouseManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// A lookup table of HouseId => HouseGuid
        /// </summary>
        private static Dictionary<uint, List<uint>> HouseIdToGuid { get; set; }

        /// <summary>
        /// A list of all player-owned houses on the server,
        /// sorted by RentDue times
        /// </summary>
        private static SortedSet<PlayerHouse> RentQueue;

        /// <summary>
        /// HouseManager actions to run when slumlord inventory has completed loading
        /// </summary>
        private static readonly Dictionary<uint, List<HouseCallback>> SlumlordCallbacks = new Dictionary<uint, List<HouseCallback>>();

        /// <summary>
        /// The rate at which HouseManager.Tick() executes
        /// </summary>
        private static readonly RateLimiter updateHouseManagerRateLimiter = new RateLimiter(1, TimeSpan.FromMinutes(1));

        public static void Initialize()
        {
            BuildHouseIdToGuid();

            BuildRentQueue();
        }

        /// <summary>
        /// Builds the lookup table for HouseId => HouseGuid
        /// </summary>
        private static void BuildHouseIdToGuid()
        {
            using (var ctx = new WorldDbContext())
            {
                var query = from weenie in ctx.Weenie
                            join inst in ctx.LandblockInstance on weenie.ClassId equals inst.WeenieClassId
                            where weenie.Type == (int)WeenieType.House
                            select new
                            {
                                Weenie = weenie,
                                Instance = inst
                            };

                var results = query.ToList();

                HouseIdToGuid = new Dictionary<uint, List<uint>>();

                foreach (var result in results)
                {
                    var classname = result.Weenie.ClassName;
                    var guid = result.Instance.Guid;

                    if (!uint.TryParse(Regex.Match(classname, @"\d+").Value, out var houseId))
                    {
                        log.Error($"[HOUSE] HouseManager.BuildHouseIdToGuid(): couldn't parse {classname}");
                        continue;
                    }

                    if (!HouseIdToGuid.TryGetValue(houseId, out var houseGuids))
                    {
                        houseGuids = new List<uint>();
                        HouseIdToGuid.Add(houseId, houseGuids);
                    }
                    houseGuids.Add(guid);
                }
                //log.Info($"BuildHouseIdToGuid: {HouseIdToGuid.Count}");
            }
        }

        /// <summary>
        /// Builds a list of all the player-owned houses on the server, sorted by RentDue timestamp
        /// </summary>
        public static void BuildRentQueue()
        {
            RentQueue = new SortedSet<PlayerHouse>();

            //var allPlayers = PlayerManager.GetAllPlayers();
            //var houseOwners = allPlayers.Where(i => i.HouseInstance != null);

            //foreach (var houseOwner in houseOwners)
                //AddRentQueue(houseOwner);

            var slumlordBiotas = DatabaseManager.Shard.BaseDatabase.GetBiotasByType(WeenieType.SlumLord);

            foreach (var slumlord in slumlordBiotas)
                AddRentQueue(slumlord);

            //log.Info($"Loaded {RentQueue.Count} active houses.");
            QueryMultiHouse();
        }

        /// <summary>
        /// Adds a player-owned house to the rent queue
        /// </summary>
        private static void AddRentQueue(Biota slumlord)
        {
            var biotaOwner = slumlord.BiotaPropertiesIID.FirstOrDefault(i => i.Type == (ushort)PropertyInstanceId.HouseOwner);
            if (biotaOwner == null)
            {
                // this is fine. this is just a house that was purchased, and then later abandoned
                //Console.WriteLine($"HouseManager.AddRentQueue(): couldn't find owner for house {slumlord.Id:X8}");
                return;
            }
            var owner = PlayerManager.FindByGuid(biotaOwner.Value);
            if (owner == null)
            {
                log.Error($"[HOUSE] HouseManager.AddRentQueue(): couldn't find owner {biotaOwner.Value:X8}");
                return;
            }
            var houseId = slumlord.BiotaPropertiesDID.FirstOrDefault(i => i.Type == (ushort)PropertyDataId.HouseId);
            if (houseId == null)
            {
                log.Error($"[HOUSE] HouseManager.AddRentQueue(): couldn't find house id for {slumlord.Id:X8}");
                return;
            }
            if (!HouseIdToGuid.TryGetValue(houseId.Value, out var houseGuids))
            {
                log.Error($"[HOUSE] HouseManager.AddRentQueue(): couldn't find house instance for {slumlord.Id:X8}");
                return;
            }
            var houseInstance = GetHouseGuid(slumlord.Id, houseGuids);
            if (houseInstance == 0)
            {
                log.Error($"[HOUSE] HouseManager.AddRentQueue(): couldn't find house guid for {slumlord.Id:X8}");
                return;
            }
            if (RentQueueContainsHouse(houseInstance))
            {
                log.Error($"[HOUSE] HouseManager.AddRentQueue(): rent queue already contains house {houseInstance}");
                return;
            }
            AddRentQueue(owner, houseInstance);
        }

        /// <summary>
        /// Returns TRUE if houseInstance is contained in the RentQueue
        /// </summary>
        private static bool RentQueueContainsHouse(uint houseInstance)
        {
            return RentQueue.Any(i => i.House.HouseInstance == houseInstance);
        }

        /// <summary>
        /// Adds a player-owned house to the rent queue
        /// </summary>
        public static void AddRentQueue(IPlayer player, uint houseGuid)
        {
            //Console.WriteLine($"AddRentQueue({player.Name}, {houseGuid:X8})");

            var house = House.Load(houseGuid);
            if (house == null)      // this can happen for basement dungeons
                return;

            var purchaseTime = (uint)(player.HousePurchaseTimestamp ?? 0);

            if (player.HouseRentTimestamp == null)
            {
                log.Warn($"[HOUSE] HouseManager.AddRentQueue({player.Name}, {houseGuid:X8}): player has null HouseRentTimestamp");
                player.HouseRentTimestamp = (int)house.GetRentDue(purchaseTime);
                //return;
            }
            AddRentQueue(player, house);
        }

        /// <summary>
        /// Adds a player-owned house to the rent queue
        /// </summary>
        private static void AddRentQueue(IPlayer player, House house)
        {
            var playerHouse = new PlayerHouse(player, house);

            RentQueue.Add(playerHouse);
        }

        /// <summary>
        /// Called when a player abandons a house
        /// </summary>
        public static void RemoveRentQueue(uint houseGuid)
        {
            RentQueue.RemoveWhere(i => i.House.Guid.Full == houseGuid);
        }

        /// <summary>
        /// Queries the status of multi-house owners on the server
        /// </summary>
        private static void QueryMultiHouse()
        {
            var slumlordBiotas = DatabaseManager.Shard.BaseDatabase.GetBiotasByType(WeenieType.SlumLord);

            var playerHouses = new Dictionary<IPlayer, List<Biota>>();
            var accountHouses = new Dictionary<string, List<Biota>>();

            foreach (var slumlord in slumlordBiotas)
            {
                var biotaOwner = slumlord.BiotaPropertiesIID.FirstOrDefault(i => i.Type == (ushort)PropertyInstanceId.HouseOwner);
                if (biotaOwner == null)
                {
                    // this is fine. this is just a house that was purchased, and then later abandoned
                    //Console.WriteLine($"HouseManager.QueryMultiHouse(): couldn't find owner for house {slumlord.Id:X8}");
                    continue;
                }
                var owner = PlayerManager.FindByGuid(biotaOwner.Value);
                if (owner == null)
                {
                    Console.WriteLine($"HouseManager.QueryMultiHouse(): couldn't find owner {biotaOwner.Value:X8}");
                    continue;
                }

                if (!playerHouses.TryGetValue(owner, out var houses))
                {
                    houses = new List<Biota>();
                    playerHouses.Add(owner, houses);
                }
                houses.Add(slumlord);

                var accountName = owner.Account != null ? owner.Account.AccountName : "NULL";

                if (!accountHouses.TryGetValue(accountName, out var aHouses))
                {
                    aHouses = new List<Biota>();
                    accountHouses.Add(accountName, aHouses);
                }
                aHouses.Add(slumlord);
            }


            if (ServerConfig.house_per_char.Value)
            {
                var results = playerHouses.Where(i => i.Value.Count > 1).OrderByDescending(i => i.Value.Count);

                if (results.Any())
                    Console.WriteLine("Multi-house owners:");

                foreach (var playerHouse in results)
                {
                    Console.WriteLine($"{playerHouse.Key.Name}: {playerHouse.Value.Count}");

                    for (var i = 0; i < playerHouse.Value.Count; i++)
                        Console.WriteLine($"{i + 1}. {GetCoords(playerHouse.Value[i])}");
                }
            }
            else
            {
                var results = accountHouses.Where(i => i.Value.Count > 1).OrderByDescending(i => i.Value.Count);

                if (results.Any())
                    Console.WriteLine("Multi-house owners:");

                foreach (var accountHouse in results)
                {
                    Console.WriteLine($"{accountHouse.Key}: {accountHouse.Value.Count}");

                    for (var i = 0; i < accountHouse.Value.Count; i++)
                        Console.WriteLine($"{i + 1}. {GetCoords(accountHouse.Value[i])}");
                }
            }
        }

        /// <summary>
        /// Returns a friendly string for the position of a biota
        /// </summary>
        private static string GetCoords(Biota biota)
        {
            var p = biota.BiotaPropertiesPosition.FirstOrDefault(i => i.PositionType == (ushort)PositionType.Location);

            return GetCoords(new Position(p.ObjCellId, p.OriginX, p.OriginY, p.OriginZ, p.AnglesX, p.AnglesY, p.AnglesZ, p.AnglesW));
        }

        /// <summary>
        /// Returns a friendly string a house / slumlord position
        /// </summary>
        public static string GetCoords(Position position)
        {
            var coords = position.GetMapCoordStr();

            if (coords == null)
            {
                // apartment slumlord?
                if (LandblockCollections.ApartmentBlocks.TryGetValue((ushort)position.Landblock, out var apartmentBlock))
                    coords = $"{apartmentBlock} - ";
                else
                    log.Error($"[HOUSE] HouseManager.GetCoords({position}) - couldn't find apartment block");

                coords += position;
            }
            return coords;
        }

        /// <summary>
        /// Runs every ~1 minute
        /// </summary>
        public static void Tick()
        {
            if (updateHouseManagerRateLimiter.GetSecondsToWaitBeforeNextEvent() > 0)
                return;

            updateHouseManagerRateLimiter.RegisterEvent();

            //log.Info($"HouseManager.Tick({RentQueue.Count})");

            var nextEntry = RentQueue.FirstOrDefault();

            if (nextEntry == null)
                return;

            var startTime = DateTime.UtcNow; // Track when processing begins
            // Limit processing time to 5 seconds to prevent blocking UpdateWorld loop.
            // With typical processing of ~50ms per house, this allows ~100 houses to be processed.
            // If this limit is hit, remaining houses will be processed on next tick (1 minute later).
            var maxProcessingTime = TimeSpan.FromSeconds(5);
            // Safety limit: prevent processing more than 100 houses in a single tick.
            // This protects against unexpected scenarios where ProcessRent() is very fast but queue is huge.
            var maxIterations = 100;
            var iterations = 0;

            while (DateTime.UtcNow > nextEntry.RentDue)
            {
                RentQueue.Remove(nextEntry);

                ProcessRent(nextEntry);

                nextEntry = RentQueue.FirstOrDefault();

                if (nextEntry == null)
                    return;

                iterations++;

                // Safety check to prevent indefinite blocking of UpdateWorld
                if (iterations >= maxIterations)
                {
                    log.Warn($"HouseManager.Tick() processed {iterations} entries and hit iteration limit. Breaking to prevent hang. Remaining queue: {RentQueue.Count}");
                    return;
                }

                var currentTime = DateTime.UtcNow;
                if (currentTime - startTime > maxProcessingTime)
                {
                    log.Warn($"HouseManager.Tick() exceeded {maxProcessingTime.TotalSeconds}s processing time after {iterations} entries. Breaking to prevent hang. Remaining queue: {RentQueue.Count}");
                    return;
                }
            }
        }

        /// <summary>
        /// Called when the RentDue timestamp has been reached for a player house
        /// Determines if rent has been paid, and if the owner currently meets the requirements for owning the dwelling,
        /// and then calls either HandleRentPaid or HandleEviction
        /// </summary>
        private static void ProcessRent(PlayerHouse playerHouse)
        {
            // load the most up-to-date copy of the house data
            GetHouse(playerHouse.House.Guid.Full, (house) =>
            {
                playerHouse.House = house;

                var isInActiveOrDisabled = playerHouse.House.HouseStatus <= HouseStatus.InActive;
                var isPaid = IsRentPaid(playerHouse);
                var hasRequirements = HasRequirements(playerHouse);
                log.Debug($"[HOUSE] {playerHouse.PlayerName}.ProcessRent(): isPaid = {isPaid} | HasRequirements = {hasRequirements} | MaintenanceFree = {house.HouseStatus == HouseStatus.InActive}");

                if (isInActiveOrDisabled || (isPaid && hasRequirements))
                    HandleRentPaid(playerHouse);
                else
                    HandleEviction(playerHouse);
            });
        }

        /// <summary>
        /// Handles a successful rent payment
        /// Updates the next rent due timestamp, and clears the slumlord inventory
        /// </summary>
        private static void HandleRentPaid(PlayerHouse playerHouse)
        {
            var player = PlayerManager.FindByGuid(playerHouse.PlayerGuid);
            if (player == null)
            {
                log.Warn($"[HOUSE] HouseManager.HandleRentPaid({playerHouse.PlayerName}): couldn't find player");
                return;
            }

            var purchaseTime = (uint)(player.HousePurchaseTimestamp ?? 0);
            var rentTime = (uint)(player.HouseRentTimestamp ?? 0);

            var nextRentTime = playerHouse.House.GetRentDue(purchaseTime);

            if (nextRentTime <= rentTime)
            {
                log.Warn($"[HOUSE] HouseManager.HandleRentPaid({playerHouse.PlayerName}): nextRentTime {nextRentTime} <= rentTime {rentTime}");
                return;
            }

            player.HouseRentTimestamp = (int)nextRentTime;

            player.SaveBiotaToDatabase();

            // clear out slumlord inventory
            var slumlord = playerHouse.House.SlumLord;
            slumlord.ClearInventory();

            slumlord.SaveBiotaToDatabase();

            log.Debug($"[HOUSE] HouseManager.HandleRentPaid({playerHouse.PlayerName}): rent payment successful!");

            // re-add item to queue
            AddRentQueue(player, playerHouse.House);

            var onlinePlayer = PlayerManager.GetOnlinePlayer(playerHouse.PlayerGuid);
            if (onlinePlayer != null)
            {
                var actionChain = new ActionChain();
                actionChain.AddDelaySeconds(3.0f);   // wait for slumlord inventory biotas above to save
                actionChain.AddAction(onlinePlayer, ActionType.PlayerHouse_HandleActionQueryHouse, onlinePlayer.HandleActionQueryHouse);
                actionChain.EnqueueChain();
            }
        }

        /// <summary>
        /// Handles the eviction process for a player house
        /// </summary>
        private static void HandleEviction(PlayerHouse playerHouse, bool force = false)
        {
            HandleEviction(playerHouse.House, playerHouse.PlayerGuid, false, force);
        }

        /// <summary>
        /// Handles the eviction process for a player house
        /// </summary>
        public static void HandleEviction(House house, uint playerGuid, bool multihouse = false, bool force = false)
        {
            // clear out slumlord inventory
            var slumlord = house.SlumLord;
            slumlord.ClearInventory();

            var player = PlayerManager.FindByGuid(playerGuid, out bool isOnline);

            if (!ServerConfig.house_rent_enabled.Value && !multihouse && !force)
            {
                // rent disabled, push forward
                var purchaseTime = (uint)(player.HousePurchaseTimestamp ?? 0);
                var nextRentTime = house.GetRentDue(purchaseTime);
                player.HouseRentTimestamp = (int)nextRentTime;

                log.Debug($"[HOUSE] HouseManager.HandleRentPaid({player.Name}): house rent disabled via config");

                // re-add item to queue
                AddRentQueue(player, house);
                return;
            }

            // handle eviction
            house.HouseOwner = null;
            house.MonarchId = null;
            house.HouseOwnerName = null;

            house.ClearPermissions();

            house.SaveBiotaToDatabase();

            // relink
            house.UpdateLinks();

            if (house.HasDungeon)
            {
                var dungeonHouse = house.GetDungeonHouse();
                if (dungeonHouse != null)
                    dungeonHouse.UpdateLinks();
            }

            // player slumlord 'off' animation
            slumlord.Off();

            // reset slumlord name
            slumlord.SetAndBroadcastName();

            slumlord.SaveBiotaToDatabase();

            HouseList.AddToAvailable(slumlord, house);

            // if evicting a multihouse owner's previous house,
            // no update for player properties
            if (player.HouseInstance == house.Guid.Full)
            {
                player.HouseId = null;
                player.HouseInstance = null;
                //player.HousePurchaseTimestamp = null;
                player.HouseRentTimestamp = null;
            }
            else
                log.Warn($"[HOUSE] HouseManager.HandleRentEviction({house.Guid}, {player.Name}, {multihouse}): house guids don't match {player.HouseInstance}");

            house.ClearRestrictions();

            log.Debug($"[HOUSE] HouseManager.HandleRentEviction({player.Name})");

            if (multihouse)
            {
                RemoveRentQueue(house.Guid.Full);

                player.SaveBiotaToDatabase();

                return;
            }

            if (!isOnline)
            {
                // inform player of eviction when they log in
                var offlinePlayer = PlayerManager.GetOfflinePlayer(playerGuid);
                if (offlinePlayer == null)
                {
                    log.Warn($"[HOUSE] {player.Name}.HandleEviction(): couldn't find offline player");
                    return;
                }
                offlinePlayer.SetProperty(PropertyBool.HouseEvicted, true);
                offlinePlayer.SaveBiotaToDatabase();
                return;
            }

            var onlinePlayer = PlayerManager.GetOnlinePlayer(playerGuid);

            onlinePlayer.House = null;

            // send text message
            onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat("Your house has reverted due to non-payment of the maintenance costs.  All items stored in the house have been lost.", ChatMessageType.Broadcast));
            onlinePlayer.RemoveDeed();

            onlinePlayer.SaveBiotaToDatabase();

            // clear house panel for online player
            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(3.0f);  // wait for slumlord inventory biotas above to save
            actionChain.AddAction(onlinePlayer, ActionType.PlayerHouse_HandleActionQueryHouse, onlinePlayer.HandleActionQueryHouse);
            actionChain.EnqueueChain();
        }

        /// <summary>
        /// Returns TRUE if the slumlord contains all of the required items for rent payment
        /// </summary>
        private static bool IsRentPaid(PlayerHouse playerHouse)
        {
            var houseData = GetHouseData(playerHouse.House);

            foreach (var rentItem in houseData.Rent)
            {
                if (rentItem.Paid < rentItem.Num)
                {
                    log.Debug($"[HOUSE] {playerHouse.PlayerName}.IsRentPaid() - required {rentItem.Num:N0}x {(rentItem.Num > 1 ? $"{rentItem.PluralName}" : $"{rentItem.Name}")} ({rentItem.WeenieID}), found {rentItem.Paid:N0}");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Returns TRUE if a player currently meets all of the requirements for owning their house (allegiance rank)
        /// </summary>
        private static bool HasRequirements(PlayerHouse playerHouse)
        {
            if (!ServerConfig.house_purchase_requirements.Value)
                return true;

            var slumlord = playerHouse.House.SlumLord;
            if (slumlord.AllegianceMinLevel == null)
                return true;

            var allegianceMinLevel = ServerConfig.mansion_min_rank.Value;
            if (allegianceMinLevel == -1)
                allegianceMinLevel = slumlord.AllegianceMinLevel.Value;

            var player = PlayerManager.FindByGuid(playerHouse.PlayerGuid);

            if (player == null)
            {
                log.Warn($"[HOUSE] {playerHouse.PlayerName}.HasRequirements() - couldn't find player");
                return false;
            }

            // ensure allegiance is loaded
            var allegiance = AllegianceManager.GetAllegiance(player);

            AllegianceNode allegianceNode = null;
            if (allegiance != null)
                allegiance.Members.TryGetValue(player.Guid, out allegianceNode);

            var rank = allegianceNode != null ? allegianceNode.Rank : 0;

            if (allegianceMinLevel > 0 && (allegiance == null || rank < allegianceMinLevel))
            {
                log.Debug($"[HOUSE] {playerHouse.PlayerName}.HasRequirements() - allegiance rank {rank} < {allegianceMinLevel}");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Returns the HouseData structure for a House (rent and paid items)
        /// </summary>
        private static HouseData GetHouseData(House house)
        {
            var houseData = new HouseData();
            houseData.Rent = house.SlumLord.GetRentItems();

            if (house.HouseStatus == HouseStatus.InActive)
                houseData.MaintenanceFree = true;

            return houseData;
        }

        // This function is called from a database callback.
        // We must add thread safety to prevent AllegianceManager corruption
        public static void HandlePlayerDelete(uint playerGuid)
        {
            WorldManager.EnqueueAction(new ActionEventDelegate(ActionType.HouseManager_HandlePlayerDelete, () => DoHandlePlayerDelete(playerGuid)));
        }

        /// <summary>
        /// Called on character delete, evicts from house
        /// </summary>
        private static void DoHandlePlayerDelete(uint playerGuid)
        {
            var player = PlayerManager.FindByGuid(playerGuid);
            if (player == null)
            {
                Console.WriteLine($"HouseManager.HandlePlayerDelete({playerGuid:X8}): couldn't find player guid");
                return;
            }

            if (player.HouseInstance == null)
                return;     

            var playerHouse = FindPlayerHouse(playerGuid);
            if (playerHouse == null)
                return;

            // load the most up-to-date copy of house data
            GetHouse(playerHouse.House.Guid.Full, (house) =>
            {
                playerHouse.House = house;

                HandleEviction(playerHouse, true);

                RemoveRentQueue(house.Guid.Full);
            });
        }

        /// <summary>
        /// Returns the house in the rent queue for a player guid, if exists
        /// </summary>
        private static PlayerHouse FindPlayerHouse(uint playerGuid)
        {
            return RentQueue.FirstOrDefault(i => i.PlayerGuid == playerGuid);
        }

        /// <summary>
        /// Returns all of the houses in the rent queue for a house id
        /// </summary>
        public static List<House> GetHouseById(uint houseId)
        {
            return RentQueue.Where(i => i.House.HouseId == houseId).Select(i => i.House).ToList();
        }

        /// <summary>
        /// Returns all of the houses in the rent queue for an account
        /// </summary>
        public static List<House> GetAccountHouses(uint accountId)
        {
            return RentQueue.Where(i => i.AccountId == accountId).Select(i => i.House).ToList();
        }

        /// <summary>
        /// Returns all of the houses in the rent queue for a character
        /// </summary>
        public static List<House> GetCharacterHouses(uint playerGuid)
        {
            return RentQueue.Where(i => i.PlayerGuid == playerGuid).Select(i => i.House).ToList();
        }

        /// <summary>
        /// Returns the house guid for a slumlord guid
        /// </summary>
        private static uint GetHouseGuid(uint slumlord_guid, List<uint> house_guids)
        {
            var slumlord_prefix = slumlord_guid >> 12;

            return house_guids.FirstOrDefault(i => slumlord_prefix == (i >> 12));
        }

        /// <summary>
        /// If the landblock is loaded, return a reference to the current House object
        /// else return a copy of the House biota from the latest info in the db
        ///
        /// <param name="callback">called when the slumlord inventory is fully loaded</param>
        public static void GetHouse(uint houseGuid, Action<House> callback)
        {
            var landblock = (ushort)((houseGuid >> 12) & 0xFFFF);

            var landblockId = new LandblockId((uint)(landblock << 16 | 0xFFFF));
            var isLoaded = LandblockManager.IsLoaded(landblockId);

            if (!isLoaded)
            {
                // landblock is unloaded
                // return a copy of the House biota from the latest info in the db
                var houseBiota = House.Load(houseGuid);

                RegisterCallback(houseBiota, callback);

                return;
            }

            // landblock is loaded, return a reference to the current House object
            var loaded = LandblockManager.GetLandblock(landblockId, false, null, false);
            var house = loaded.GetObject(new ObjectGuid(houseGuid)) as House;

            if (house != null && house.SlumLord != null)
            {
                if (!house.SlumLord.InventoryLoaded)
                    RegisterCallback(house, callback);
                else
                    callback(house);
            }
            else if (!loaded.CreateWorldObjectsCompleted)
            {
                var houseBiota = House.Load(houseGuid);

                RegisterCallback(houseBiota, callback);
            }
            else
                log.Error($"[HOUSE] HouseManager.GetHouse({houseGuid:X8}): couldn't find house on loaded landblock");
        }

        /// <summary>
        /// Registers a callback to run when the slumlord inventory has been loaded
        /// </summary>
        public static void RegisterCallback(House house, Action<House> callback)
        {
            if (!SlumlordCallbacks.TryGetValue(house.SlumLord.Guid.Full, out var callbacks))
            {
                callbacks = new List<HouseCallback>();
                SlumlordCallbacks.Add(house.SlumLord.Guid.Full, callbacks);
            }
            callbacks.Add(new HouseCallback(house, callback));
        }

        /// <summary>
        /// Runs any pending HouseManager callbacks for this slumlord house
        /// </summary>
        public static void OnInitialInventoryLoadCompleted(SlumLord slumlord)
        {
            //Console.WriteLine($"HouseManager.OnInitialInventoryLoadCompleted({slumlord.Name})");

            if (SlumlordCallbacks.TryGetValue(slumlord.Guid.Full, out var callbacks))
            {
                foreach (var callback in callbacks)
                    callback.Run();

                SlumlordCallbacks.Remove(slumlord.Guid.Full);
            }
        }

        /// <summary>
        /// Pay rent for a house
        /// </summary>
        private static void PayRent(PlayerHouse playerHouse)
        {
            // load the most up-to-date copy of the house data
            GetHouse(playerHouse.House.Guid.Full, (house) =>
            {
                playerHouse.House = house;

                var isPaid = IsRentPaid(playerHouse) || playerHouse.House.HouseStatus <= HouseStatus.InActive;

                if (!isPaid)
                {
                    var houseData = GetHouseData(playerHouse.House);

                    foreach (var rentItem in houseData.Rent)
                    {
                        if (rentItem.Paid < rentItem.Num)
                        {
                            var amountLeftToPay = rentItem.Num - rentItem.Paid;

                            while (amountLeftToPay > 0)
                            {
                                var payment = WorldObjectFactory.CreateNewWorldObject(rentItem.WeenieID);

                                if (payment == null)
                                {
                                    log.Error($"[HOUSE] HouseManager.PayRent({house.Guid}): couldn't create payment for WCID {rentItem.WeenieID}");
                                    return;
                                }

                                payment.SetStackSize(Math.Min(amountLeftToPay, payment.MaxStackSize ?? 1));

                                if (!house.SlumLord.TryAddToInventory(payment))
                                {
                                    log.Error($"[HOUSE] HouseManager.PayRent({house.Guid}): couldn't place {payment.Name} (0x{payment.Guid}) in SlumLord's Inventory");
                                    return;
                                }

                                amountLeftToPay -= (payment.StackSize ?? 1);
                            }
                        }
                    }

                    house.SlumLord.MergeAllStackables();

                    foreach (var item in house.SlumLord.Inventory.Values)
                        item.SaveBiotaToDatabase();

                    house.SlumLord.SaveBiotaToDatabase();

                    var onlinePlayer = PlayerManager.GetOnlinePlayer(playerHouse.PlayerGuid);
                    if (onlinePlayer != null)
                    {
                        var actionChain = new ActionChain();
                        actionChain.AddDelaySeconds(3.0f);   // wait for slumlord inventory biotas above to save
                        actionChain.AddAction(onlinePlayer, ActionType.PlayerHouse_HandleActionQueryHouse, onlinePlayer.HandleActionQueryHouse);
                        actionChain.EnqueueChain();
                    }

                    log.Debug($"[HOUSE] HouseManager.PayRent({house.Guid}): fully paid rent into SlumLord.");
                }
            });
        }

        /// <summary>
        /// Pay rent for a house
        /// </summary>
        public static bool PayRent(House house)
        {
            var foundHouse = RentQueue.FirstOrDefault(h => h.PlayerGuid == (house.HouseOwner ?? 0));

            if (foundHouse == null)
                return false;

            PayRent(foundHouse);

            return true;
        }

        /// <summary>
        /// Pay rent for all owned housing
        /// </summary>
        public static void PayAllRent()
        {
            foreach (var house in RentQueue)
            {
                PayRent(house);
            }
        }
    }
}
