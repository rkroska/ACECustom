using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using log4net;

using ACE.Common.Performance;
using ACE.Database;
using ACE.Database.Models.World;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Physics.Common;
using ACE.Server.Network.GameMessages;
using ACE.Server.WorldObjects;

using Position = ACE.Entity.Position;

namespace ACE.Server.Entity
{
    /// <summary>
    /// the gist of a landblock is that, generally, everything on it publishes
    /// to and subscribes to everything else in the landblock.  x/y in an outdoor
    /// landblock goes from 0 to 192.  "indoor" (dungeon) landblocks have no
    /// functional limit as players can't freely roam in/out of them
    /// </summary>
    public class Landblock : IActor
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public int? VariationId { get; set; }

        public LandblockId Id { get; }

        public override int GetHashCode()
        {
            var hash = base.GetHashCode(); hash ^= VariationId.GetHashCode();
            return hash;
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj) && this.VariationId == ((Landblock)obj).VariationId;
        }

        /// <summary>
        /// Flag indicates if this landblock is permanently loaded (for example, towns on high-traffic servers)
        /// </summary>
        public bool Permaload = false;

        /// <summary>
        /// Flag indicates if this landblock has no keep alive objects
        /// </summary>
        public bool HasNoKeepAliveObjects = true;

        /// <summary>
        /// This must be true before a player enters a landblock.
        /// This prevents a player from possibly pasing through a door that hasn't spawned in yet, and other scenarios.
        /// </summary>
        public bool CreateWorldObjectsCompleted { get; private set; }

        private DateTime lastActiveTime;

        /// <summary>
        /// Dormant landblocks suppress Monster AI ticking and physics processing
        /// </summary>
        public bool IsDormant;

        private readonly ConcurrentDictionary<ObjectGuid, WorldObject> worldObjects = new ConcurrentDictionary<ObjectGuid, WorldObject>();
        private readonly ConcurrentDictionary<ObjectGuid, WorldObject> pendingAdditions = new ConcurrentDictionary<ObjectGuid, WorldObject>();
        private readonly List<ObjectGuid> pendingRemovals = new List<ObjectGuid>();

        // Cache used for Tick efficiency
        public readonly List<Player> players = new List<Player>();
        private readonly LinkedList<Creature> sortedCreaturesByNextTick = new LinkedList<Creature>();
        private readonly LinkedList<WorldObject> sortedWorldObjectsByNextHeartbeat = new LinkedList<WorldObject>();
        private readonly LinkedList<WorldObject> sortedGeneratorsByNextGeneratorUpdate = new LinkedList<WorldObject>();
        private readonly LinkedList<WorldObject> sortedGeneratorsByNextRegeneration = new LinkedList<WorldObject>();

        /// <summary>
        /// This is used to detect and manage cross-landblock group (which is potentially cross-thread) operations.
        /// </summary>
        public LandblockGroup CurrentLandblockGroup { get; internal set; }

        public List<Landblock> Adjacents = new List<Landblock>();

        private readonly ActionQueue actionQueue = new ActionQueue();

        public int WorldObjectCount
        { get
            {
                lock (worldObjects)
                    return worldObjects.Count;
            }
        }

        public int PhysicsObjectCount
        { get
            {
                if (PhysicsLandblock != null)
                    return PhysicsLandblock.ServerObjects.Count;
                else
                    return 0;
            }
        }

        /// <summary>
        /// Landblocks heartbeat every 5 seconds
        /// </summary>
        private static readonly TimeSpan heartbeatInterval = TimeSpan.FromSeconds(5);

        private DateTime lastHeartBeat = DateTime.MinValue;

        /// <summary>
        /// Landblock items will be saved to the database every 5 minutes
        /// </summary>
        private static readonly TimeSpan databaseSaveInterval = TimeSpan.FromMinutes(5);

        private DateTime lastDatabaseSave = DateTime.MinValue;

        /// <summary>
        /// Landblocks which have been inactive for this many seconds will be dormant
        /// </summary>
        private static readonly TimeSpan dormantInterval = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Landblocks which have been inactive for this many seconds will be unloaded
        /// </summary>
        public static readonly TimeSpan UnloadInterval = TimeSpan.FromMinutes(5);


        /// <summary>
        /// The clientlib backing store landblock
        /// Eventually these classes could be merged, but for now they are separate...
        /// </summary>
        public Physics.Common.Landblock PhysicsLandblock { get; }

        public CellLandblock CellLandblock { get; }
        public LandblockInfo LandblockInfo { get; }

        /// <summary>
        /// The landblock static meshes for
        /// collision detection and physics simulation
        /// </summary>
        public LandblockMesh LandblockMesh { get; private set; }

        public List<ModelMesh> Buildings { get; private set; }


        public readonly RateMonitor Monitor5m = new RateMonitor();
        private readonly TimeSpan last5mClearInteval = TimeSpan.FromMinutes(5);
        private DateTime last5mClear;
        public readonly RateMonitor Monitor1h = new RateMonitor();
        private readonly TimeSpan last1hClearInteval = TimeSpan.FromHours(1);
        private DateTime last1hClear;
        private bool monitorsRequireEventStart = true;

        // Used for cumulative ServerPerformanceMonitor event recording
        private readonly Stopwatch stopwatch = new Stopwatch();


        private EnvironChangeType fogColor;

        public EnvironChangeType FogColor
        {
            get
            {
                if (LandblockManager.GlobalFogColor.HasValue)
                    return LandblockManager.GlobalFogColor.Value;

                return fogColor;
            }
            set => fogColor = value;
        }


        public Landblock(LandblockId id, int? variation = null)
        {
            //log.Debug($"Landblock({(id.Raw | 0xFFFF):X8})");

            Id = id;
            VariationId = variation;

            CellLandblock = DatManager.CellDat.ReadFromDat<CellLandblock>(Id.Raw | 0xFFFF);
            LandblockInfo = DatManager.CellDat.ReadFromDat<LandblockInfo>((uint)Id.Landblock << 16 | 0xFFFE);

            lastActiveTime = DateTime.UtcNow;

            var cellLandblock = DBObj.GetCellLandblock(Id.Raw | 0xFFFF);
            PhysicsLandblock = new Physics.Common.Landblock(cellLandblock, variation);
        }


        /// <summary>
        /// Initializes a landblock
        /// TODO: Make this variation aware
        /// </summary>
        /// <param name="reload"></param>
        public void Init(int? variationId, bool reload = false)
        {
            if (!reload)
                PhysicsLandblock.PostInit();
            
            Task.Run(() =>
            {
                //Console.WriteLine($"Landblock.Init({Id}) task started, variation: {VariationId}, v: {variationId}");
                CreateWorldObjects(variationId);
                
                SpawnDynamicShardObjects();

                SpawnEncounters();

                SetEnvironmentConditions();
            });

            //LoadMeshes(objects);
        }

        public void SetEnvironmentConditions()
        {
            if (darkIsleZoneLandblocks.Contains(this.Id.Landblock))
            {
                this.SendEnvironChange(EnvironChangeType.BlackFog2);
                this.SetFogColor(EnvironChangeType.BlackFog2);
            }

            if (ThaelarynIslandLandblocks.Contains(this.Id.Landblock))
            {
                this.SendEnvironChange(EnvironChangeType.BlackFog2);
                this.SetFogColor(EnvironChangeType.BlackFog2);
            }
        }

        public static readonly HashSet<ushort> connectionExemptLandblocks = new()
        {
            //MP
            0x016C,
            //Appartments
            0x7200, 0x7300, 0x7400, 0x7500, 0x7600, 0x7700, 0x7800, 0x7900, 0x7A00, 0x7B00, 0x7C00, 0x7D00, 0x7E00, 0x7F00, 0x8000, 0x8100, 0x8200, 0x8300, 0x8400, 0x8500, 0x8600, 0x8700, 0x8800, 0x8900, 0x8A00, 0x8B00, 0x8C00, 0x8D00, 0x8E00, 0x8F00, 0x9000, 0x9100, 0x9200, 0x9300, 0x9400, 0x9500, 0x9600, 0x9700, 0x9800, 0x9900, 0x5360, 0x5361, 0x5362, 0x5363, 0x5364, 0x5365, 0x5366, 0x5367, 0x5368, 0x5369
        };

        public static readonly HashSet<ushort> darkIsleZoneLandblocks = new()
        {
            0xC7EB, 0xC8EB, 0xC9EB, 0xCAEB, 0xCAEA, 0xCBEA, 0xCBEB, 0xCBEC, 0xCCEC, 0xCAEC, 0xC9EC, 0xC8EC, 0xC7EC, 0xC6EC, 0xC5EC, 0xC4EC,
            0xC4ED, 0xC5ED, 0xC6ED, 0xC7ED, 0xC8ED, 0xC9ED, 0xCAED, 0xCBED, 0xCCED,
            0xC4EE, 0xC5EE, 0xC6EE, 0xC7EE, 0xC8EE, 0xC9EE, 0xCAEE, 0xCBEE,
            0xC4EF, 0xC5EF, 0xC6EF, 0xC7EF, 0xC8EF, 0xC9EF, 0xCAEF, 0xCBEF,
            0xC5F0, 0xC6F0, 0xC7F0, 0xC8F0, 0xC9F0, 0xC9EA, 0xCAF0,
            0xC5F1, 0xC6F1, 0xC7F1, 0xC8F1, 0xC9F1,
            0xC4F2, 0xC5F2, 0xC6F2, 0xC7F2, 0xC8F2,
            0xC3F3, 0xC4F3, 0xC5F3, 0xC6F3, 0xC7F3, 0xC8F3,
            0xC3F4, 0xC4F4, 0xC5F4, 0xC6F4, 0xC7F4,
            0xC3F5, 0xC4F5, 0xC5F5, 0xC6F5, 0xC7F5,
            0xC2F6, 0xC3F6, 0xC4F6, 0xC5F6, 0xC6F6,
            0xC2F7, 0xC3F7, 0xC4F7, 0xC5F7,
            0xC1F8, 0xC2F8, 0xC3F8, 0xC4F8
        };

        public static readonly HashSet<ushort> ThaelarynIslandLandblocks = new()
        {
            0xF66C, 0xF76C, 0xF86C, 0xF66B, 0xF76B, 0xF86B, 0xF76A, 0xF86A, 0xF96A, 0xF669, 0xF769, 0xF869, 0xF969, 0xF668, 0xF768, 0xF868, 0xF968,
            0xF467, 0xF567, 0xF667, 0xF767, 0xF867, 0xF967, 0xF666, 0xF766, 0xF866, 0xF966, 0xF565, 0xF665, 0xF765, 0xF865, 0xF965, 0xF564, 0xF664,
            0xF764, 0xF864, 0xF964, 0xF563, 0xF663, 0xF763, 0xF863, 0xF963, 0xF462, 0xF562, 0xF662, 0xF762, 0xF862, 0xF962, 0xF361, 0xF461, 0xF561,
            0xF661, 0xF761
        };

        /// <summary>
        /// Monster Locations, Generators<para />
        /// This will be called from a separate task from our constructor. Use thread safety when interacting with this landblock.
        /// TODO: Make this variation aware
        /// </summary>
        private void CreateWorldObjects(int? variationId)
        {
            if (VariationId == null && variationId != null)
            {
                VariationId = variationId.Value;
            }
            //Console.WriteLine($"CreateWOs in landblock {this.Id} v:{variationId}, group: {this.CurrentLandblockGroup}\n");
            //if (this.Id.ToString().StartsWith("019E"))
            //{
                
            //    Console.WriteLine($"From: {new StackTrace()}");
            //}
            var objects = DatabaseManager.World.GetCachedInstancesByLandblock(Id.Landblock, variationId);
            var shardObjects = DatabaseManager.Shard.BaseDatabase.GetStaticObjectsByLandblock(Id.Landblock, variationId);
            var factoryObjects = WorldObjectFactory.CreateNewWorldObjects(objects, shardObjects, null, variationId);


            actionQueue.EnqueueAction(new ActionEventDelegate(() =>
            {
                // for mansion linking
                var houses = new List<House>();
                foreach (var fo in factoryObjects)
                {
                    WorldObject parent = null;
                    if (fo.WeenieType == WeenieType.House)
                    {
                        var house = fo as House;
                        Houses.Add(house);

                        if (fo.HouseType == HouseType.Mansion)
                        {
                            houses.Add(house);
                            house.LinkedHouses.Add(houses[0]);

                            if (houses.Count > 1)
                            {
                                houses[0].LinkedHouses.Add(house);
                                parent = houses[0];
                            }
                        }
                    }

                    var res = AddWorldObject(fo, variationId);
                    if (!res)
                    {
                        Console.WriteLine($"Failed to add world object {fo.Name}, {fo.Guid} to landblock {Id.Landblock}");
                    }
                    fo.ActivateLinks(objects, shardObjects, parent);

                    if (fo.PhysicsObj != null)
                        fo.PhysicsObj.Order = 0;
                }

                CreateWorldObjectsCompleted = true;

                PhysicsLandblock.SortObjects();
            }));
        }

        /// <summary>
        /// Corpses<para />
        /// This will be called from a separate task from our constructor. Use thread safety when interacting with this landblock.
        /// </summary>
        private void SpawnDynamicShardObjects()
        {
            var dynamics = DatabaseManager.Shard.BaseDatabase.GetDynamicObjectsByLandblock(Id.Landblock, VariationId);
            var factoryShardObjects = WorldObjectFactory.CreateWorldObjects(dynamics);

            actionQueue.EnqueueAction(new ActionEventDelegate(() =>
            {
                foreach (var fso in factoryShardObjects)
                    AddWorldObject(fso, VariationId);
            }));
        }

        /// <summary>
        /// Spawns the semi-randomized monsters scattered around the outdoors<para />
        /// This will be called from a separate task from our constructor. Use thread safety when interacting with this landblock.
        /// </summary>
        private void SpawnEncounters()
        {
            // get the encounter spawns for this landblock
            var encounters = DatabaseManager.World.GetCachedEncountersByLandblock(Id.Landblock);

            foreach (var encounter in encounters)
            {
                var wo = WorldObjectFactory.CreateNewWorldObject(encounter.WeenieClassId);

                if (wo == null) continue;

                actionQueue.EnqueueAction(new ActionEventDelegate(() =>
                {
                    var xPos = Math.Clamp(encounter.CellX * 24.0f, 0.5f, 191.5f);
                    var yPos = Math.Clamp(encounter.CellY * 24.0f, 0.5f, 191.5f);

                    var pos = new Physics.Common.Position();
                    pos.ObjCellID = (uint)(Id.Landblock << 16) | 1;
                    pos.Variation = VariationId;
                    pos.Frame = new Physics.Animation.AFrame(new Vector3(xPos, yPos, 0), Quaternion.Identity);
                    pos.adjust_to_outside();

                    pos.Frame.Origin.Z = PhysicsLandblock.GetZ(pos.Frame.Origin);

                    wo.Location = new Position(pos.ObjCellID, pos.Frame.Origin, pos.Frame.Orientation, pos.Variation);
                    wo.Location.Variation = VariationId;

                    if (LScape.get_landcell(pos.ObjCellID, pos.Variation) is SortCell sortCell && sortCell.has_building())
                    {
                        wo.Destroy();
                        return;
                    }

                    if (PropertyManager.GetBool("override_encounter_spawn_rates").Item)
                    {
                        wo.RegenerationInterval = PropertyManager.GetDouble("encounter_regen_interval").Item;

                        wo.ReinitializeHeartbeats();

                        if (wo.Biota.PropertiesGenerator != null)
                        {
                            // While this may be ugly, it's done for performance reasons.
                            // Common weenie properties are not cloned into the bota on creation. Instead, the biota references simply point to the weenie collections.
                            // The problem here is that we want to update one of those common collection properties. If the biota is referencing the weenie collection,
                            // then we'll end up updating the global weenie (from the cache), instead of just this specific biota.
                            if (wo.Biota.PropertiesGenerator == wo.Weenie.PropertiesGenerator)
                            {
                                wo.Biota.PropertiesGenerator = new List<PropertiesGenerator>(wo.Weenie.PropertiesGenerator.Count);

                                foreach (var record in wo.Weenie.PropertiesGenerator)
                                    wo.Biota.PropertiesGenerator.Add(record.Clone());
                            }

                            foreach (var profile in wo.Biota.PropertiesGenerator)
                                profile.Delay = (float)PropertyManager.GetDouble("encounter_delay").Item;
                        }
                    }

                    if (!AddWorldObject(wo, VariationId))
                        wo.Destroy();
                }));
            }
        }

        /// <summary>
        /// Loads the meshes for the landblock<para />
        /// This isn't used by ACE, but we still retain it for the following reason:<para />
        /// its useful, concise, high level overview code for everything needed to load landblocks, all their objects, scenery, polygons
        /// without getting into all of the low level methods that acclient uses to do it
        /// </summary>
        //private void LoadMeshes(List<LandblockInstance> objects)
        //{
        //    LandblockMesh = new LandblockMesh(Id);
        //    LoadLandObjects();
        //    LoadBuildings();
        //    LoadWeenies(objects);
        //    LoadScenery();
        //}

        /// <summary>
        /// Loads the meshes for the static landblock objects,
        /// also known as obstacles
        /// </summary>
        //private void LoadLandObjects()
        //{
        //    LandObjects = new List<ModelMesh>();

        //    foreach (var obj in LandblockInfo.Objects)
        //        LandObjects.Add(new ModelMesh(obj.Id, obj.Frame));
        //}

        /// <summary>
        /// Loads the meshes for the buildings on the landblock
        /// </summary>
        //private void LoadBuildings()
        //{
        //    Buildings = new List<ModelMesh>();

        //    foreach (var obj in LandblockInfo.Buildings)
        //        Buildings.Add(new ModelMesh(obj.ModelId, obj.Frame));
        //}

        /// <summary>
        /// Loads the meshes for the weenies on the landblock
        /// </summary>
        //private void LoadWeenies(List<LandblockInstance> objects)
        //{
        //    WeenieMeshes = new List<ModelMesh>();

        //    foreach (var obj in objects)
        //    {
        //        var weenie = DatabaseManager.World.GetCachedWeenie(obj.WeenieClassId);
        //        WeenieMeshes.Add(
        //            new ModelMesh(weenie.GetProperty(PropertyDataId.Setup) ?? 0,
        //            new DatLoader.Entity.Frame(new Position(obj.ObjCellId, obj.OriginX, obj.OriginY, obj.OriginZ, obj.AnglesX, obj.AnglesY, obj.AnglesZ, obj.AnglesW))));
        //    }
        //}

        /// <summary>
        /// Loads the meshes for the scenery on the landblock
        /// </summary>
        //private void LoadScenery()
        //{
        //    Scenery = Entity.Scenery.Load(this);
        //}

        /// <summary>
        /// This should be called before TickLandblockGroupThreadSafeWork() and before Tick()
        /// </summary>
        public void TickPhysics(double portalYearTicks, ConcurrentBag<WorldObject> movedObjects)
        {
            if (IsDormant)
                return;

            Monitor5m.Restart();
            Monitor1h.Restart();
            monitorsRequireEventStart = false;

            ProcessPendingWorldObjectAdditionsAndRemovals();

            foreach (WorldObject wo in worldObjects.Values)
            {
                // set to TRUE if object changes landblock
                var landblockUpdate = wo.UpdateObjectPhysics();

                if (landblockUpdate)
                {
                    movedObjects.Add(wo);
                    //Console.WriteLine($"Ticking Physics Landblock: {Id}, v: {VariationId}");
                }
                    
            }

            Monitor5m.Pause();
            Monitor1h.Pause();
        }

        /// <summary>
        /// This will tick anything that can be multi-threaded safely using LandblockGroups as thread boundaries
        /// This should be called after TickPhysics() and before Tick()
        /// </summary>
        public void TickMultiThreadedWork(double currentUnixTime)
        {
            if (monitorsRequireEventStart)
            {
                Monitor5m.Restart();
                Monitor1h.Restart();
            }
            else
            {
                Monitor5m.Resume();
                Monitor1h.Resume();
            }

            stopwatch.Restart();
            // This will consist of the following work:
            // - this.CreateWorldObjects
            // - this.SpawnDynamicShardObjects
            // - this.SpawnEncounters
            // - Adding items back onto the landblock from failed player movements: Player_Inventory.cs DoHandleActionPutItemInContainer()
            // - Executing trade between two players: Player_Trade.cs FinalizeTrade()
            //var actionQueueCount = actionQueue.Count();
            actionQueue.RunActions();
            ServerPerformanceMonitor.AddToCumulativeEvent(ServerPerformanceMonitor.CumulativeEventHistoryType.Landblock_Tick_RunActions, stopwatch.Elapsed.TotalSeconds);
            //if (stopwatch.Elapsed.TotalSeconds > 0.100f)
            //{
            //    log.Warn($"Landblock {Id.ToString()}.Tick({currentUnixTime}).Landblock_Tick_RunActions: {stopwatch.Elapsed.TotalSeconds} seconds, Count: {actionQueueCount}");
            //}
            ProcessPendingWorldObjectAdditionsAndRemovals();

            // When a WorldObject Ticks, it can end up adding additional WorldObjects to this landblock
            if (!IsDormant)
            {
                stopwatch.Restart();
                while (sortedCreaturesByNextTick.Count > 0) // Monster_Tick()
                {
                    var first = sortedCreaturesByNextTick.First.Value;

                    // If they wanted to run before or at now
                    if (first.NextMonsterTickTime <= currentUnixTime)
                    {
                        sortedCreaturesByNextTick.RemoveFirst();
                        first.Monster_Tick(currentUnixTime);
                        sortedCreaturesByNextTick.AddLast(first); // All creatures tick at a fixed interval
                    }
                    else
                    {
                        break;
                    }
                }
                ServerPerformanceMonitor.AddToCumulativeEvent(ServerPerformanceMonitor.CumulativeEventHistoryType.Landblock_Tick_Monster_Tick, stopwatch.Elapsed.TotalSeconds);
            }

            stopwatch.Restart();
            while (sortedGeneratorsByNextGeneratorUpdate.Count > 0)
            {
                var first = sortedGeneratorsByNextGeneratorUpdate.First.Value;

                // If they wanted to run before or at now
                if (first.NextGeneratorUpdateTime <= currentUnixTime)
                {
                    sortedGeneratorsByNextGeneratorUpdate.RemoveFirst();
                    first.GeneratorUpdate(currentUnixTime);
                    //InsertWorldObjectIntoSortedGeneratorUpdateList(first);
                    sortedGeneratorsByNextGeneratorUpdate.AddLast(first);
                }
                else
                {
                    break;
                }
            }
            ServerPerformanceMonitor.AddToCumulativeEvent(ServerPerformanceMonitor.CumulativeEventHistoryType.Landblock_Tick_GeneratorUpdate, stopwatch.Elapsed.TotalSeconds);

            stopwatch.Restart();
            while (sortedGeneratorsByNextRegeneration.Count > 0) // GeneratorRegeneration()
            {
                var first = sortedGeneratorsByNextRegeneration.First.Value;

                //Console.WriteLine($"{first.Name}.Landblock_Tick_GeneratorRegeneration({currentUnixTime})");

                // If they wanted to run before or at now
                if (first.NextGeneratorRegenerationTime <= currentUnixTime)
                {
                    sortedGeneratorsByNextRegeneration.RemoveFirst();
                    first.GeneratorRegeneration(currentUnixTime);
                    InsertWorldObjectIntoSortedGeneratorRegenerationList(first); // Generators can have regnerations at different intervals
                }
                else
                {
                    break;
                }
            }
            ServerPerformanceMonitor.AddToCumulativeEvent(ServerPerformanceMonitor.CumulativeEventHistoryType.Landblock_Tick_GeneratorRegeneration, stopwatch.Elapsed.TotalSeconds);

            // Heartbeat
            stopwatch.Restart();
            if (lastHeartBeat + heartbeatInterval <= DateTime.UtcNow)
            {
                var thisHeartBeat = DateTime.UtcNow;

                ProcessPendingWorldObjectAdditionsAndRemovals();

                // Decay world objects
                if (lastHeartBeat != DateTime.MinValue)
                {
                    foreach (var wo in worldObjects.Values)
                    {
                        if (wo.IsDecayable())
                            wo.Decay(thisHeartBeat - lastHeartBeat);
                    }
                }

                if (!Permaload && HasNoKeepAliveObjects)
                {
                    if (lastActiveTime + dormantInterval < thisHeartBeat)
                    {
                        if (!IsDormant)
                        {
                            var spellProjectiles = worldObjects.Values.Where(i => i is SpellProjectile).ToList();
                            foreach (var spellProjectile in spellProjectiles)
                            {
                                spellProjectile.PhysicsObj.set_active(false);
                                spellProjectile.Destroy();
                            }
                        }

                        IsDormant = true;
                    }
                    if (lastActiveTime + UnloadInterval < thisHeartBeat)
                        LandblockManager.AddToDestructionQueue(this, this.VariationId);
                }

                //log.Info($"Landblock {Id.ToString()}.Tick({currentUnixTime}).Landblock_Tick_Heartbeat: thisHeartBeat: {thisHeartBeat.ToString()} | lastHeartBeat: {lastHeartBeat.ToString()} | worldObjects.Count: {worldObjects.Count()}");
                lastHeartBeat = thisHeartBeat;
            }
            ServerPerformanceMonitor.AddToCumulativeEvent(ServerPerformanceMonitor.CumulativeEventHistoryType.Landblock_Tick_Heartbeat, stopwatch.Elapsed.TotalSeconds);

            // Database Save
            stopwatch.Restart();
            if (lastDatabaseSave + databaseSaveInterval <= DateTime.UtcNow)
            {
                ProcessPendingWorldObjectAdditionsAndRemovals();

                SaveDB();
                lastDatabaseSave = DateTime.UtcNow;
            }
            ServerPerformanceMonitor.AddToCumulativeEvent(ServerPerformanceMonitor.CumulativeEventHistoryType.Landblock_Tick_Database_Save, stopwatch.Elapsed.TotalSeconds);

            Monitor5m.Pause();
            Monitor1h.Pause();
        }

        /// <summary>
        /// This will tick everything that should be done single threaded on the main ACE World thread
        /// This should be called after TickPhysics() and after Tick()
        /// </summary>
        public void TickSingleThreadedWork(double currentUnixTime)
        {
            if (monitorsRequireEventStart)
            {
                Monitor5m.Restart();
                Monitor1h.Restart();
            }
            else
            {
                Monitor5m.Resume();
                Monitor1h.Resume();
            }

            ProcessPendingWorldObjectAdditionsAndRemovals();

            stopwatch.Restart();
            foreach (var player in players)
                player.Player_Tick(currentUnixTime);
            ServerPerformanceMonitor.AddToCumulativeEvent(ServerPerformanceMonitor.CumulativeEventHistoryType.Landblock_Tick_Player_Tick, stopwatch.Elapsed.TotalSeconds);

            stopwatch.Restart();
            while (sortedWorldObjectsByNextHeartbeat.Count > 0) // Heartbeat()
            {
                var first = sortedWorldObjectsByNextHeartbeat.First.Value;

                // If they wanted to run before or at now
                if (first.NextHeartbeatTime <= currentUnixTime)
                {
                    sortedWorldObjectsByNextHeartbeat.RemoveFirst();
                    first.Heartbeat(currentUnixTime);
                    InsertWorldObjectIntoSortedHeartbeatList(first); // WorldObjects can have heartbeats at different intervals
                }
                else
                {
                    break;
                }
            }
            ServerPerformanceMonitor.AddToCumulativeEvent(ServerPerformanceMonitor.CumulativeEventHistoryType.Landblock_Tick_WorldObject_Heartbeat, stopwatch.Elapsed.TotalSeconds);

            Monitor5m.RegisterEventEnd();
            Monitor1h.RegisterEventEnd();
            monitorsRequireEventStart = true;

            if (DateTime.UtcNow - last5mClear >= last5mClearInteval)
            {
                Monitor5m.ClearEventHistory();
                last5mClear = DateTime.UtcNow;
            }

            if (DateTime.UtcNow - last1hClear >= last1hClearInteval)
            {
                Monitor1h.ClearEventHistory();
                last1hClear = DateTime.UtcNow;
            }
        }

        private void ProcessPendingWorldObjectAdditionsAndRemovals()
        {
            if (!pendingAdditions.IsEmpty)
            {
                foreach (var kvp in pendingAdditions)
                {
                    worldObjects[kvp.Key] = kvp.Value;

                    if (kvp.Value is Player player)
                        players.Add(player);
                    else if (kvp.Value is Creature creature)
                        sortedCreaturesByNextTick.AddLast(creature);

                    InsertWorldObjectIntoSortedHeartbeatList(kvp.Value);
                    InsertWorldObjectIntoSortedGeneratorUpdateList(kvp.Value);
                    InsertWorldObjectIntoSortedGeneratorRegenerationList(kvp.Value);

                    if (kvp.Value.WeenieClassId == 80007) // Landblock KeepAlive weenie (ACE custom)
                        HasNoKeepAliveObjects = false;
                }

                pendingAdditions.Clear();
            }

            if (pendingRemovals.Count > 0)
            {
                foreach (var objectGuid in pendingRemovals)
                {
                    if (worldObjects.Remove(objectGuid, out var wo))
                    {
                        if (wo is Player player)
                            players.Remove(player);
                        else if (wo is Creature creature)
                            sortedCreaturesByNextTick.Remove(creature);

                        sortedWorldObjectsByNextHeartbeat.Remove(wo);
                        sortedGeneratorsByNextGeneratorUpdate.Remove(wo);
                        sortedGeneratorsByNextRegeneration.Remove(wo);

                        if (wo.WeenieClassId == 80007) // Landblock KeepAlive weenie (ACE custom)
                        {
                            var keepAliveObject = worldObjects.Values.FirstOrDefault(w => w.WeenieClassId == 80007);

                            if (keepAliveObject == null)
                                HasNoKeepAliveObjects = true;
                        }
                    }
                }

                pendingRemovals.Clear();
            }
        }

        private void InsertWorldObjectIntoSortedHeartbeatList(WorldObject worldObject)
        {
            // If you want to add checks to exclude certain object types from heartbeating, you would do it here
            if (worldObject.NextHeartbeatTime == double.MaxValue)
                return;

            if (sortedWorldObjectsByNextHeartbeat.Count == 0)
            {
                sortedWorldObjectsByNextHeartbeat.AddFirst(worldObject);
                return;
            }

            if (sortedWorldObjectsByNextHeartbeat.Last.Value.NextHeartbeatTime <= worldObject.NextHeartbeatTime)
            {
                sortedWorldObjectsByNextHeartbeat.AddLast(worldObject);
                return;
            }

            var currentNode = sortedWorldObjectsByNextHeartbeat.First;

            while (currentNode != null)
            {
                if (worldObject.NextHeartbeatTime <= currentNode.Value.NextHeartbeatTime)
                {
                    sortedWorldObjectsByNextHeartbeat.AddBefore(currentNode, worldObject);
                    return;
                }

                currentNode = currentNode.Next;
            }

            sortedWorldObjectsByNextHeartbeat.AddLast(worldObject); // This line really shouldn't be hit
        }

        private void InsertWorldObjectIntoSortedGeneratorUpdateList(WorldObject worldObject)
        {
            // If you want to add checks to exclude certain object types from heartbeating, you would do it here
            if (worldObject.NextGeneratorUpdateTime == double.MaxValue)
                return;

            if (sortedGeneratorsByNextGeneratorUpdate.Count == 0)
            {
                sortedGeneratorsByNextGeneratorUpdate.AddFirst(worldObject);
                return;
            }

            if (sortedGeneratorsByNextGeneratorUpdate.Last.Value.NextGeneratorUpdateTime <= worldObject.NextGeneratorUpdateTime)
            {
                sortedGeneratorsByNextGeneratorUpdate.AddLast(worldObject);
                return;
            }

            var currentNode = sortedGeneratorsByNextGeneratorUpdate.First;

            while (currentNode != null)
            {
                if (worldObject.NextGeneratorUpdateTime <= currentNode.Value.NextGeneratorUpdateTime)
                {
                    sortedGeneratorsByNextGeneratorUpdate.AddBefore(currentNode, worldObject);
                    return;
                }

                currentNode = currentNode.Next;
            }

            sortedGeneratorsByNextGeneratorUpdate.AddLast(worldObject); // This line really shouldn't be hit
        }

        private void InsertWorldObjectIntoSortedGeneratorRegenerationList(WorldObject worldObject)
        {
            // If you want to add checks to exclude certain object types from heartbeating, you would do it here
            if (worldObject.NextGeneratorRegenerationTime == double.MaxValue)
                return;

            if (sortedGeneratorsByNextRegeneration.Count == 0)
            {
                sortedGeneratorsByNextRegeneration.AddFirst(worldObject);
                return;
            }

            if (sortedGeneratorsByNextRegeneration.Last.Value.NextGeneratorRegenerationTime <= worldObject.NextGeneratorRegenerationTime)
            {
                sortedGeneratorsByNextRegeneration.AddLast(worldObject);
                return;
            }

            var currentNode = sortedGeneratorsByNextRegeneration.First;

            while (currentNode != null)
            {
                if (worldObject.NextGeneratorRegenerationTime <= currentNode.Value.NextGeneratorRegenerationTime)
                {
                    sortedGeneratorsByNextRegeneration.AddBefore(currentNode, worldObject);
                    return;
                }

                currentNode = currentNode.Next;
            }

            sortedGeneratorsByNextRegeneration.AddLast(worldObject); // This line really shouldn't be hit
        }

        public void ResortWorldObjectIntoSortedGeneratorRegenerationList(WorldObject worldObject)
        {
            if (sortedGeneratorsByNextRegeneration.Contains(worldObject))
            {
                sortedGeneratorsByNextRegeneration.Remove(worldObject);
                InsertWorldObjectIntoSortedGeneratorRegenerationList(worldObject);
            }
        }

        public void EnqueueAction(IAction action)
        {
            actionQueue.EnqueueAction(action);
        }

        /// <summary>
        /// This will fail if the wo doesn't have a valid location.
        /// </summary>
        public bool AddWorldObject(WorldObject wo, int? VariationId)
        {
            if (wo.Location == null)
            {
                Console.WriteLine("Landblock 0x{0} failed to add 0x{1:X8} {2}. Invalid Location", Id, wo.Biota.Id, wo.Name);
                return false;
            }

            return AddWorldObjectInternal(wo, VariationId);
        }

        public void AddWorldObjectForPhysics(WorldObject wo, int? VariationId)
        {
            AddWorldObjectInternal(wo, VariationId);
        }

        private bool AddWorldObjectInternal(WorldObject wo, int? VariationId)
        {
            if (LandblockManager.CurrentlyTickingLandblockGroupsMultiThreaded)
            {
                if (CurrentLandblockGroup != null && CurrentLandblockGroup != LandblockManager.CurrentMultiThreadedTickingLandblockGroup.Value)
                {
                    log.Error($"Landblock 0x{Id} entered AddWorldObjectInternal in a cross-thread operation.");
                    log.Error($"Landblock 0x{Id} CurrentLandblockGroup: {CurrentLandblockGroup}");
                    log.Error($"Variation: {VariationId}");
                    log.Error($"LandblockManager.CurrentMultiThreadedTickingLandblockGroup.Value: {LandblockManager.CurrentMultiThreadedTickingLandblockGroup.Value}");

                    log.Error($"wo: 0x{wo.Guid}:{wo.Name} [{wo.WeenieClassId} - {wo.WeenieType}], v:{wo.Location.Variation}, previous landblock 0x{wo.CurrentLandblock?.Id}");

                    if (wo.WeenieType == WeenieType.ProjectileSpell)
                    {
                        if (wo.ProjectileSource != null)
                            log.Error($"wo.ProjectileSource: 0x{wo.ProjectileSource?.Guid}:{wo.ProjectileSource?.Name}, position: {wo.ProjectileSource?.Location}");

                        if (wo.ProjectileTarget != null)
                            log.Error($"wo.ProjectileTarget: 0x{wo.ProjectileTarget?.Guid}:{wo.ProjectileTarget?.Name}, position: {wo.ProjectileTarget?.Location}");
                    }

                    log.Error(System.Environment.StackTrace);

                    log.Error("PLEASE REPORT THIS TO THE ACE DEV TEAM !!!");

                    // Prevent possible multi-threaded crash
                    if (wo.WeenieType == WeenieType.ProjectileSpell)
                        return false;

                    // This may still crash...
                }
            }

            wo.CurrentLandblock = this;
            //if (this.Id.ToString().StartsWith("019E"))
            //{
            //    Console.WriteLine($"{wo.Name}, {wo.WeenieClassId} is spawning in landblock {this.Id} v:{wo.CurrentLandblock.VariationId} wo.v:{wo.Location.Variation}");
            //        //$"From: {new StackTrace()}");
            //}                     
            //wo.Location.Variation = VariationId;

            if (wo.PhysicsObj == null)
                wo.InitPhysicsObj(VariationId);
            else
                wo.PhysicsObj.set_object_guid(wo.Guid);  // re-add to ServerObjectManager

            if (wo.PhysicsObj.CurCell == null)
            {
                var success = wo.AddPhysicsObj(VariationId);
                if (!success)
                {
                    wo.CurrentLandblock = null;

                    if (wo.Generator != null)
                    {
                        log.Debug($"AddWorldObjectInternal: couldn't spawn 0x{wo.Guid}:{wo.Name} [{wo.WeenieClassId} - {wo.WeenieType}] at {wo.Location.ToLOCString()} from generator {wo.Generator.WeenieClassId} - 0x{wo.Generator.Guid}:{wo.Generator.Name}");
                        wo.NotifyOfEvent(RegenerationType.PickUp); // Notify generator the generated object is effectively destroyed, use Pickup to catch both cases.
                    }
                    else if (wo.IsGenerator) // Some generators will fail random spawns if they're circumference spans over water or cliff edges
                        log.Debug($"AddWorldObjectInternal: couldn't spawn generator 0x{wo.Guid}:{wo.Name} [{wo.WeenieClassId} - {wo.WeenieType}] at {wo.Location.ToLOCString()}");
                    else if (wo.ProjectileTarget == null && wo is not SpellProjectile)
                        Console.WriteLine($"AddWorldObjectInternal: couldn't spawn 0x{wo.Guid}:{wo.Name} [{wo.WeenieClassId} - {wo.WeenieType}] at {wo.Location.ToLOCString()}");

                    return false;
                }
            }

            if (!worldObjects.ContainsKey(wo.Guid))
                pendingAdditions[wo.Guid] = wo;
            else
                pendingRemovals.Remove(wo.Guid);

            // broadcast to nearby players
            wo.NotifyPlayers();

            if (wo is Player player)
                player.SetFogColor(FogColor);

            if (wo is Corpse && wo.Level.HasValue)
            {
                var corpseLimit = PropertyManager.GetLong("corpse_spam_limit").Item;
                var corpseList = worldObjects.Values.Union(pendingAdditions.Values).Where(w => w is Corpse && w.Level.HasValue && w.VictimId == wo.VictimId).OrderBy(w => w.CreationTimestamp);

                if (corpseList.Count() > corpseLimit)
                {
                    var corpse = GetObject(corpseList.First(w => w.TimeToRot > Corpse.EmptyDecayTime).Guid);

                    if (corpse != null)
                    {
                        log.Warn($"[CORPSE] Landblock.AddWorldObjectInternal(): {wo.Name} (0x{wo.Guid}) exceeds the per player limit of {corpseLimit} corpses for 0x{Id.Landblock:X4}. Adjusting TimeToRot for oldest {corpse.Name} (0x{corpse.Guid}), CreationTimestamp: {corpse.CreationTimestamp} ({Common.Time.GetDateTimeFromTimestamp(corpse.CreationTimestamp ?? 0).ToLocalTime():yyyy-MM-dd HH:mm:ss}), to Corpse.EmptyDecayTime({Corpse.EmptyDecayTime}).");
                        corpse.TimeToRot = Corpse.EmptyDecayTime;
                    }
                }
            }

            return true;
        }

        public void RemoveWorldObject(ObjectGuid objectId, bool adjacencyMove = false, bool fromPickup = false, bool showError = true)
        {
            RemoveWorldObjectInternal(objectId, adjacencyMove, fromPickup, showError);
        }

        /// <summary>
        /// Should only be called by physics/relocation engines -- not from player
        /// </summary>
        /// <param name="objectId">The object ID to be removed from the current landblock</param>
        /// <param name="adjacencyMove">Flag indicates if object is moving to an adjacent landblock</param>
        public void RemoveWorldObjectForPhysics(ObjectGuid objectId, bool adjacencyMove = false)
        {
            RemoveWorldObjectInternal(objectId, adjacencyMove);
        }

        private void RemoveWorldObjectInternal(ObjectGuid objectId, bool adjacencyMove = false, bool fromPickup = false, bool showError = true)
        {
            if (LandblockManager.CurrentlyTickingLandblockGroupsMultiThreaded)
            {
                if (CurrentLandblockGroup != null && CurrentLandblockGroup != LandblockManager.CurrentMultiThreadedTickingLandblockGroup.Value)
                {
                    log.Error($"Landblock 0x{Id} entered RemoveWorldObjectInternal in a cross-thread operation.");
                    log.Error($"Landblock 0x{Id} CurrentLandblockGroup: {CurrentLandblockGroup}");
                    log.Error($"LandblockManager.CurrentMultiThreadedTickingLandblockGroup.Value: {LandblockManager.CurrentMultiThreadedTickingLandblockGroup.Value}");

                    log.Error($"objectId: 0x{objectId}");

                    log.Error(System.Environment.StackTrace);

                    log.Error("PLEASE REPORT THIS TO THE ACE DEV TEAM !!!");

                    // This may still crash...
                }
            }

            if (worldObjects.TryGetValue(objectId, out var wo))
                pendingRemovals.Add(objectId);
            else if (!pendingAdditions.Remove(objectId, out wo))
            {
                if (showError)
                    log.Warn($"RemoveWorldObjectInternal: Couldn't find {objectId.Full:X8}");
                return;
            }

            wo.CurrentLandblock = null;

            // Weenies can come with a default of 0 (Instant Rot) or -1 (Never Rot). If they still have that value, we want to retain it.
            // We also want to make sure fromPickup is true so that we're not clearing out TimeToRot on server shutdown (unloads all landblocks and removed all objects).
            if (fromPickup && wo.TimeToRot.HasValue && wo.TimeToRot != 0 && wo.TimeToRot != -1)
                wo.TimeToRot = null;

            if (!adjacencyMove)
            {
                // really remove it - send message to client to remove object
                wo.EnqueueActionBroadcast(p => p.RemoveTrackedObject(wo, fromPickup));

                wo.PhysicsObj.DestroyObject();
            }
        }

        public void EmitSignal(WorldObject emitter, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            foreach (var wo in worldObjects.Values.Where(w => w.HearLocalSignals).ToList())
            {
                if (emitter == wo) continue;

                if (emitter.IsWithinUseRadiusOf(wo, wo.HearLocalSignalsRadius))
                {
                    //Console.WriteLine($"{wo.Name}.EmoteManager.OnLocalSignal({emitter.Name}, {message})");
                    wo.EmoteManager.OnLocalSignal(emitter, message);
                }
            }
        }

        /// <summary>
        /// Check to see if we are close enough to interact.   Adds a fudge factor of 1.5f
        /// </summary>
        public bool WithinUseRadius(Player player, ObjectGuid targetGuid, out bool validTargetGuid, float? useRadius = null)
        {
            var target = GetObject(targetGuid);

            validTargetGuid = target != null;

            if (target != null)
                return player.IsWithinUseRadiusOf(target, useRadius);

            return false;
        }

        /// <summary>
        /// Returns landblock objects with physics initialized
        /// </summary>
        public ICollection<WorldObject> GetWorldObjectsForPhysicsHandling()
        {
            // If a missile is destroyed when it runs it's UpdateObjectPhysics(), it will remove itself from the landblock, thus, modifying the worldObjects collection.

            ProcessPendingWorldObjectAdditionsAndRemovals();

            return worldObjects.Values;
        }

        public List<WorldObject> GetAllWorldObjectsForDiagnostics()
        {
            // We do not ProcessPending here, and we return ToList() to avoid cross-thread issues.
            // This can happen if we "loadalllandblocks" and do a "serverstatus".
            return worldObjects.Values.ToList();
        }

        public WorldObject GetObject(uint objectId)
        {
            return GetObject(new ObjectGuid(objectId));
        }

        /// <summary>
        /// This will return null if the object was not found in the current or adjacent landblocks.
        /// </summary>
        public WorldObject GetObject(ObjectGuid guid, bool searchAdjacents = true, bool searchVariations = false)
        {
            if (pendingRemovals.Contains(guid))
                return null;

            if (worldObjects.TryGetValue(guid, out var worldObject) || pendingAdditions.TryGetValue(guid, out worldObject))
                return worldObject;

            if (searchAdjacents)
            {
                foreach (Landblock lb in Adjacents)
                {
                    if (lb != null)
                    {
                        var wo = lb.GetObject(guid, false);

                        if (wo != null)
                            return wo;
                    }
                }
            }

            return null;
        }

        public WorldObject GetWieldedObject(uint objectGuid, bool searchAdjacents = true)
        {
            return GetWieldedObject(new ObjectGuid(objectGuid), searchAdjacents); // todo fix
        }

        /// <summary>
        /// Searches this landblock (and possibly adjacents) for an ObjectGuid wielded by a creature
        /// </summary>
        public WorldObject GetWieldedObject(ObjectGuid guid, bool searchAdjacents = true)
        {
            // search creature wielded items in current landblock
            var creatures = worldObjects.Values.OfType<Creature>();
            foreach (var creature in creatures)
            {
                var wieldedItem = creature.GetEquippedItem(guid);
                if (wieldedItem != null)
                {
                    if ((wieldedItem.CurrentWieldedLocation & EquipMask.Selectable) != 0)
                        return wieldedItem;

                    return null;
                }
            }

            // try searching adjacent landblocks if not found
            if (searchAdjacents)
            {
                foreach (var adjacent in Adjacents)
                {
                    if (adjacent == null) continue;

                    var wieldedItem = adjacent.GetWieldedObject(guid, false);
                    if (wieldedItem != null)
                        return wieldedItem;
                }
            }
            return null;
        }

        /// <summary>
        /// Sets a landblock to active state, with the current time as the LastActiveTime
        /// </summary>
        /// <param name="isAdjacent">Public calls to this function should always set isAdjacent to false</param>
        public void SetActive(bool isAdjacent = false)
        {
            lastActiveTime = DateTime.UtcNow;
            IsDormant = false;

            if (isAdjacent || PhysicsLandblock == null || PhysicsLandblock.IsDungeon) return;

            // for outdoor landblocks, recursively call 1 iteration to set adjacents to active
            foreach (var landblock in Adjacents)
            {
                if (landblock != null)
                    landblock.SetActive(true);
            }
        }

        /// <summary>
        /// Handles the cleanup process for a landblock
        /// This method is called by LandblockManager
        /// </summary>
        public void Unload(int? VariationId)
        {
            var landblockID = Id.Raw | 0xFFFF;

            //log.Debug($"Landblock.Unload({landblockID:X8})");

            ProcessPendingWorldObjectAdditionsAndRemovals();

            SaveDB();
            //Console.WriteLine($"Landblock.Unload({landblockID:X8}), removing {worldObjects.Count}");
            // remove all objects
            foreach (var wo in worldObjects.ToList())
            {
                if (!wo.Value.BiotaOriginatedFromOrHasBeenSavedToDatabase())
                    wo.Value.Destroy(false, true);
                else
                    RemoveWorldObjectInternal(wo.Key);
            }

            ProcessPendingWorldObjectAdditionsAndRemovals();

            actionQueue.Clear();

            // remove physics landblock
            LScape.unload_landblock(landblockID, VariationId);

            PhysicsLandblock.release_shadow_objs();
        }

        public void DestroyAllNonPlayerObjects()
        {
            ProcessPendingWorldObjectAdditionsAndRemovals();

            SaveDB();

            // remove all objects
            foreach (var wo in worldObjects.Where(i => i.Value is not Player).ToList())
            {
                if (!wo.Value.BiotaOriginatedFromOrHasBeenSavedToDatabase())
                    wo.Value.Destroy(false);
                else
                    RemoveWorldObjectInternal(wo.Key);
            }

            ProcessPendingWorldObjectAdditionsAndRemovals();

            actionQueue.Clear();
        }

        private void SaveDB()
        {
            var biotas = new Collection<(Biota biota, ReaderWriterLockSlim rwLock)>();

            foreach (var wo in worldObjects.Values)
            {
                if (wo.IsStaticThatShouldPersistToShard() || wo.IsDynamicThatShouldPersistToShard())
                    AddWorldObjectToBiotasSaveCollection(wo, biotas);
            }

            if (biotas.Count > 0)
            {
                DatabaseManager.Shard.SaveBiotasInParallel(
                    biotas,
                    result => { },
                    $"SaveDB:Landblock:{this.Id.Raw}{(this.VariationId.HasValue ? $":v{this.VariationId.Value}" : string.Empty)}"
                );
            }
        }

        private static void AddWorldObjectToBiotasSaveCollection(WorldObject wo, Collection<(Biota biota, ReaderWriterLockSlim rwLock)> biotas)
        {
            if (wo.ChangesDetected)
            {
                wo.SaveBiotaToDatabase(false);
                biotas.Add((wo.Biota, wo.BiotaDatabaseLock));
            }

            if (wo is Container container)
            {
                foreach (var item in container.Inventory.Values)
                    AddWorldObjectToBiotasSaveCollection(item, biotas);
            }
        }

        /// <summary>
        /// This is only used for very specific instances, such as broadcasting player deaths to the destination lifestone block
        /// This is a rarely used method to broadcast network messages to all of the players within a landblock,
        /// and possibly the adjacent landblocks.
        /// </summary>
        public void EnqueueBroadcast(ICollection<Player> excludeList, bool adjacents, Position pos = null, float? maxRangeSq = null, params GameMessage[] msgs)
        {
            var players = worldObjects.Values.OfType<Player>();

            // for landblock death broadcasts:
            // exclude players that have already been broadcast to within range of the death
            if (excludeList != null)
                players = players.Except(excludeList);

            // broadcast messages to player in this landblock
            foreach (var player in players)
            {
                if (pos != null && maxRangeSq != null)
                {
                    var distSq = player.Location.SquaredDistanceTo(pos);
                    if (distSq > maxRangeSq)
                        continue;
                }
                player.Session.Network.EnqueueSend(msgs);
            }

            // if applicable, iterate into adjacent landblocks
            if (adjacents)
            {
                foreach (var adjacent in this.Adjacents.Where(adj => adj != null))
                    adjacent.EnqueueBroadcast(excludeList, false, pos, maxRangeSq, msgs);
            }
        }

        private bool? isDungeon;

        /// <summary>
        /// Returns TRUE if this landblock is a dungeon,
        /// with no traversable overworld
        /// </summary>
        public bool IsDungeon
        {
            get
            {
                // return cached value
                if (isDungeon != null)
                    return isDungeon.Value;

                // hack for NW island
                // did a worldwide analysis for adding watercells into the formula,
                // but they are inconsistently defined for some of the edges of map unfortunately
                if (Id.LandblockX < 0x08 && Id.LandblockY > 0xF8)
                {
                    isDungeon = false;
                    return isDungeon.Value;
                }

                // a dungeon landblock is determined by:
                // - all heights being 0
                // - having at least 1 EnvCell (0x100+)
                // - contains no buildings
                foreach (var height in CellLandblock.Height)
                {
                    if (height != 0)
                    {
                        isDungeon = false;
                        return isDungeon.Value;
                    }
                }
                isDungeon = LandblockInfo != null && LandblockInfo.NumCells > 0 && LandblockInfo.Buildings != null && LandblockInfo.Buildings.Count == 0;
                return isDungeon.Value;
            }
        }

        private bool? hasDungeon;

        /// <summary>
        /// Returns TRUE if this landblock contains a dungeon
        //
        /// If a landblock contains both a dungeon + traversable overworld,
        /// this field will return TRUE, whereas IsDungeon will return FALSE
        /// 
        /// This property should only be used in very specific scenarios,
        /// such as determining if a landblock contains a mansion basement
        /// </summary>
        public bool HasDungeon
        {
            get
            {
                // return cached value
                if (hasDungeon != null)
                    return hasDungeon.Value;

                hasDungeon = LandblockInfo != null && LandblockInfo.NumCells > 0 && LandblockInfo.Buildings != null && LandblockInfo.Buildings.Count == 0;
                return hasDungeon.Value;
            }
        }


        public List<House> Houses = new List<House>();

        public void SetFogColor(EnvironChangeType environChangeType)
        {
            if (environChangeType.IsFog())
            {
                FogColor = environChangeType;

                foreach (var lb in Adjacents)
                    lb.FogColor = environChangeType;

                foreach(var player in players)
                {
                    player.SetFogColor(FogColor);
                }
            }
        }

        public void SendEnvironSound(EnvironChangeType environChangeType)
        {
            if (environChangeType.IsSound())
            {
                SendEnvironChange(environChangeType);

                foreach (var lb in Adjacents)
                    lb.SendEnvironChange(environChangeType);
            }
        }

        public void SendEnvironChange(EnvironChangeType environChangeType)
        {
            foreach (var player in players)
            {
                player.SendEnvironChange(environChangeType);
            }
        }

        public void SendCurrentEnviron()
        {
            foreach (var player in players)
            {
                if (FogColor.IsFog())
                {
                    player.SetFogColor(FogColor);
                }
                else
                {
                    player.SendEnvironChange(FogColor);
                }
            }
        }

        public void DoEnvironChange(EnvironChangeType environChangeType)
        {
            if (environChangeType.IsFog())
                SetFogColor(environChangeType);
            else
                SendEnvironSound(environChangeType);
        }
    }
}
