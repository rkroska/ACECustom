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
using ACE.Server.Physics;
using ACE.Server.Physics.Common;
using ACE.Server.Network.GameMessages;
using ACE.Server.WorldObjects;

using Position = ACE.Entity.Position;
using ACE.Common;

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

        private static readonly ConcurrentDictionary<uint, long> _landblockReaddDiagLastLogMs = new();

        private static void LandblockReaddDiagMaybePrune(long nowMs)
        {
            const int maxEntries = 4096;
            const long staleMs = 120_000;
            if (_landblockReaddDiagLastLogMs.Count < maxEntries)
                return;

            foreach (var kv in _landblockReaddDiagLastLogMs.ToArray())
            {
                var age = nowMs - kv.Value;
                if (age > staleMs || age < -staleMs)
                    _landblockReaddDiagLastLogMs.TryRemove(kv.Key, out _);
            }
        }

        public int? VariationId { get; set; }

        public LandblockId Id { get; }

        public override int GetHashCode()
        {
            var hash = base.GetHashCode(); hash ^= VariationId.GetHashCode();
            return hash;
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj) && VariationId == ((Landblock)obj).VariationId;
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
        private readonly object pendingRemovalsLock = new object();
        private readonly HashSet<ObjectGuid> pendingRemovals = new HashSet<ObjectGuid>();

        // Cache used for Tick efficiency
        public readonly List<Player> players = new List<Player>();
        private readonly LinkedList<Creature> sortedCreaturesByNextTick = new LinkedList<Creature>();
        private readonly LinkedList<WorldObject> sortedWorldObjectsByNextHeartbeat = new LinkedList<WorldObject>();
        private readonly LinkedList<WorldObject> sortedGeneratorsByNextGeneratorUpdate = new LinkedList<WorldObject>();
        private readonly LinkedList<WorldObject> sortedGeneratorsByNextRegeneration = new LinkedList<WorldObject>();
        
        // Monster tick throttle monitoring
        private int monsterTickThrottleWarningCount = 0;
        private DateTime lastMonsterThrottleWarning = DateTime.MinValue;

        /// <summary>
        /// This is used to detect and manage cross-landblock group (which is potentially cross-thread) operations.
        /// </summary>
        public LandblockGroup CurrentLandblockGroup { get; internal set; }

        public List<Landblock> Adjacents = new List<Landblock>();

        /// <summary>
        /// Prestige boundary markers spawned at landblock load time.
        /// These are static objects visible to all players in this variation.
        /// </summary>
        private readonly List<WorldObject> _prestigeBoundaryMarkers = new List<WorldObject>();

        private double _empowerSourcesRefreshedAt = -1;
        private List<Creature> _empowerSources = new();

        /// <summary>The IsEmpowerSource creatures on this landblock, rescanned at most once per <paramref name="maxAgeSeconds"/>.
        /// Shared by every CanBeEmpowered creature here, so the per-second aura check is O(sources) rather than a full
        /// physics-object scan per creature. Called from the landblock's own tick thread only.</summary>
        internal List<Creature> GetEmpowerSources(double now, double maxAgeSeconds)
        {
            if (now - _empowerSourcesRefreshedAt < maxAgeSeconds)
                return _empowerSources;
            var list = new List<Creature>();
            foreach (var obj in GetWorldObjectsForPhysicsHandling())
                if (obj is Creature c && c.GetProperty(ACE.Entity.Enum.Properties.PropertyBool.IsEmpowerSource) == true)
                    list.Add(c);
            _empowerSources = list;
            _empowerSourcesRefreshedAt = now;
            return list;
        }
        private readonly List<WorldObject> _zoneBoundaryMarkers = new List<WorldObject>();

        private readonly ActionQueue actionQueue = new();

        public int WorldObjectCount
        { get
            {
                lock (worldObjects)
                    return worldObjects.Count;
            }
        }

        /// <summary>
        /// Returns a snapshot of world objects for diagnostic purposes.
        /// </summary>
        public IEnumerable<WorldObject> GetWorldObjectsForDiagnostics()
        {
            return worldObjects.Values;
        }

        public int PhysicsObjectCount
        { get
            {
                if (PhysicsLandblock != null)
                    return PhysicsLandblock.ServerObjectsCount;
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

        public readonly RateMonitor Monitor5m = new();
        private readonly TimeSpan last5mClearInteval = TimeSpan.FromMinutes(5);
        private DateTime last5mClear;
        public readonly RateMonitor Monitor1h = new();
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

            // Void detection (2026-07-26): the watchdog below needs to know how long this instance has
            // existed, NOT how long a spawn task has been running - a landblock whose Init never ran has
            // no task to measure. See CheckInitSpawnWatchdog.
            constructedTime = DateTime.UtcNow;

            var cellLandblock = DBObj.GetCellLandblock(Id.Raw | 0xFFFF);
            PhysicsLandblock = new Physics.Common.Landblock(cellLandblock, variation);

            if (ServerConfig.landblock_lifecycle_diag_verbose.Value)
                log.Warn($"[LbLife] CONSTRUCT {Id.Landblock:X4} v={variation?.ToString() ?? "null"} raw={Id.Raw:X8}");
        }


        /// <summary>
        /// Initializes a landblock
        /// TODO: Make this variation aware
        /// </summary>
        /// <param name="reload"></param>
        public void Init(int? variationId, bool reload = false)
        {
            initCalled = true;

            if (ServerConfig.landblock_lifecycle_diag_verbose.Value)
                log.Warn($"[LbLife] INIT {Id.Landblock:X4} v={variationId?.ToString() ?? "null"} reload={reload}");

            if (!reload)
                PhysicsLandblock.PostInit();
            else
            {
                // A deliberate reload (/reload-landblock) reuses THIS Landblock object. The
                // CreateWorldObjectsCompleted guard below exists to swallow duplicate watchdog
                // RETRIES of the same load - but on a reload it swallowed the fresh spawn payload
                // too, leaving the block EMPTY (found 2026-07-21 testing terrain-override camp
                // swaps). Clear it so the rebuilt payload lands; restart the watchdog counters.
                CreateWorldObjectsCompleted = false;
                initSpawnAttempts = 0;
                initSpawnStillRunningLogged = false;
                initSpawnGaveUpLogged = false;   // each reload may log the watchdog outcome again
            }

            StartInitSpawnTask(variationId);

            //LoadMeshes(objects);
        }

        // Void-landblock root cause (2026-07-18, F658 v11): this spawn work used to run in a bare
        // Task.Run. An exception thrown before CreateWorldObjects could enqueue its spawn action
        // (DB instance/shard queries, object factory) killed the task with ZERO trace — the landblock
        // stayed registered, ticking and walkable (physics is built in the ctor) but permanently
        // EMPTY until its scheduled unload. TaskScheduler.UnobservedTaskException was not hooked, so
        // nothing could ever surface it. The catch below makes the death visible and the heartbeat
        // watchdog (CheckInitSpawnWatchdog) retries it.
        private Task initSpawnTask;
        private int? initSpawnVariationId;
        private int initSpawnAttempts;
        private DateTime initSpawnStartedTime;
        private bool initSpawnStillRunningLogged;
        private bool initSpawnGaveUpLogged;   // the terminal VOID log has its own latch (#30)

        // 2026-07-26 (F559 v11): the above only guards a task that STARTED. A landblock constructed but
        // never Init()'d has initSpawnTask == null, so the watchdog returned on its first line every
        // heartbeat and the block stayed a walkable void for the whole session - exactly the state found
        // on F559 v11 (base variation ticking, not one v11 guid ever created). These two track the
        // never-began case so the watchdog can heal it.
        private readonly DateTime constructedTime;
        private bool initCalled;
        private bool neverInitLogged;

        private void StartInitSpawnTask(int? variationId)
        {
            initSpawnVariationId = variationId;
            initSpawnAttempts++;
            initSpawnStartedTime = DateTime.UtcNow;
            initSpawnStillRunningLogged = false;

            initSpawnTask = Task.Run(() =>
            {
                try
                {
                    CreateWorldObjects(variationId);

                    SpawnDynamicShardObjects();

                    SpawnEncounters();

                    SetEnvironmentConditions();
                }
                catch (Exception ex)
                {
                    log.Error($"[VoidHeal] Landblock {Id.Landblock:X4} (Var {VariationId?.ToString() ?? "null"}) init spawn task FAILED (attempt {initSpawnAttempts}) - block would be a void without retry: {ex}");
                }
            });
        }

        /// <summary>
        /// Heartbeat watchdog for the init spawn task: if the task ended (faulted or otherwise) without
        /// CreateWorldObjects completing, the landblock is a void — retry up to 3 times. A task still
        /// running past the threshold is logged once (DB stall) but not retried until it ends.
        /// </summary>
        private static readonly TimeSpan initSpawnWatchdogThreshold = TimeSpan.FromSeconds(60);

        private void CheckInitSpawnWatchdog(DateTime thisHeartBeat)
        {
            if (CreateWorldObjectsCompleted)
                return;

            // NEVER-BEGAN void (2026-07-26): no spawn task was ever started on this instance. The old
            // guard bailed here on `initSpawnTask == null` and could never see this, which is how F559
            // v11 stayed empty for an entire session while the player stood on it. Anything registered
            // this long with nothing created is a void by definition - start the spawn work.
            if (initSpawnTask == null)
            {
                if (constructedTime + initSpawnWatchdogThreshold > thisHeartBeat)
                    return;

                if (!neverInitLogged)
                {
                    neverInitLogged = true;
                    log.Error($"[VoidHeal] Landblock {Id.Landblock:X4} (Var {VariationId?.ToString() ?? "null"}) has been registered for " +
                              $"{(thisHeartBeat - constructedTime).TotalSeconds:N0}s with NO init spawn task (Init called={initCalled}) - " +
                              $"walkable VOID, starting spawn now.");
                }

                // Init() never ran: PostInit() (physics cells, CurLandblock) has to precede the spawn task
                if (!initCalled)
                    Init(VariationId);
                else
                    StartInitSpawnTask(VariationId);
                return;
            }

            if (initSpawnStartedTime + initSpawnWatchdogThreshold > thisHeartBeat)
                return;

            if (!initSpawnTask.IsCompleted)
            {
                if (!initSpawnStillRunningLogged)
                {
                    initSpawnStillRunningLogged = true;
                    log.Warn($"[VoidHeal] Landblock {Id.Landblock:X4} (Var {VariationId?.ToString() ?? "null"}) init spawn task still running after {initSpawnWatchdogThreshold.TotalSeconds:N0}s (attempt {initSpawnAttempts})");
                }
                return;
            }

            if (initSpawnAttempts >= 3)
            {
                if (!initSpawnGaveUpLogged)
                {
                    initSpawnGaveUpLogged = true;
                    log.Error($"[VoidHeal] Landblock {Id.Landblock:X4} (Var {VariationId?.ToString() ?? "null"}) init spawn task never completed after {initSpawnAttempts} attempts - GIVING UP, block is a VOID (faulted={initSpawnTask.IsFaulted})");
                }
                return;
            }

            log.Error($"[VoidHeal] Landblock {Id.Landblock:X4} (Var {VariationId?.ToString() ?? "null"}) init spawn task ended without completing spawns (faulted={initSpawnTask.IsFaulted}) - RETRYING (attempt {initSpawnAttempts + 1}/3)");
            StartInitSpawnTask(initSpawnVariationId);
        }

        public void SetEnvironmentConditions()
        {
            if (darkIsleZoneLandblocks.Contains(this.Id.Landblock))
            {
                this.SendEnvironChange(EnvironChangeType.BlackFog2);
                this.SetFogColor(EnvironChangeType.BlackFog2);
            }

            /*if (ThaelarynIslandLandblocks.Contains(this.Id.Landblock))
            {
                this.SendEnvironChange(EnvironChangeType.BlackFog2);
                this.SetFogColor(EnvironChangeType.BlackFog2);
            }*/
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

        /*public static readonly HashSet<ushort> ThaelarynIslandLandblocks = new()
        {
            0xF66C, 0xF76C, 0xF86C, 0xF66B, 0xF76B, 0xF86B, 0xF76A, 0xF86A, 0xF96A, 0xF669, 0xF769, 0xF869, 0xF969, 0xF668, 0xF768, 0xF868, 0xF968,
            0xF467, 0xF567, 0xF667, 0xF767, 0xF867, 0xF967, 0xF666, 0xF766, 0xF866, 0xF966, 0xF565, 0xF665, 0xF765, 0xF865, 0xF965, 0xF564, 0xF664,
            0xF764, 0xF864, 0xF964, 0xF563, 0xF663, 0xF763, 0xF863, 0xF963, 0xF462, 0xF562, 0xF662, 0xF762, 0xF862, 0xF962, 0xF361, 0xF461, 0xF561,
            0xF661, 0xF761
        };*/

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
            // Greater Rifts: instance rows are per-variation EXACT-match and rift variations (negative) have
            // none — load the run's configured source variation's rows instead. The source copy may be LIVE
            // at the same time, so rift copies get the rows CLONED with fresh dynamic guids (static guids
            // shared across two live landblocks break guid-keyed lookups: selection, attack targeting).
            // Shard statics stay out of rift copies. The factory below still stamps every spawned object
            // with THIS landblock's variation. Pass-through for non-rift loads.
            var instanceVariationId = ACE.Server.Managers.Rifts.RiftManager.ResolveInstanceSourceVariation(variationId);

            List<ACE.Database.Models.World.LandblockInstance> objects;
            List<ACE.Database.Models.Shard.Biota> shardObjects;
            if (!Nullable.Equals(instanceVariationId, variationId))
            {
                objects = ACE.Server.Managers.Rifts.RiftManager.CloneInstancesForRift(
                    DatabaseManager.World.GetCachedInstancesByLandblock(Id.Landblock, instanceVariationId));
                shardObjects = new List<ACE.Database.Models.Shard.Biota>();
            }
            else
            {
                objects = DatabaseManager.World.GetCachedInstancesByLandblock(Id.Landblock, variationId);
                shardObjects = DatabaseManager.Shard.BaseDatabase.GetStaticObjectsByLandblock(Id.Landblock, variationId);

                // Zone Control terrain-override redirection (owner 2026-07-21): a zone that overrides
                // this block's terrain at this variation swaps its standalone camp GENERATORS for ones
                // drawn from the zone's blocks whose REAL terrain matches the override ("mark it
                // obsidian, get the obsidian camps"). Rows are cloned (cache untouched), positions and
                // guids stay, linked/quest instances are never swapped. No-op without an override.
                objects = Managers.ZoneControl.ZoneControlManager.RedirectInstancesForTerrainOverride(
                    Id.Landblock, variationId, objects);
            }

            var factoryObjects = WorldObjectFactory.CreateNewWorldObjects(objects, shardObjects, null, variationId);


            actionQueue.EnqueueAction(new ActionEventDelegate(ActionType.Landblock_CreateWorldObjects, () =>
            {
                // idempotence: the [VoidHeal] watchdog can retry the init spawn task; if a previous
                // attempt's action already populated the block, drop this duplicate payload.
                if (CreateWorldObjectsCompleted)
                {
                    // a fully built payload is being discarded: release its physics + dynamic guids
                    foreach (var dup in factoryObjects)
                        dup.Destroy(false);
                    return;
                }

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
                    if (!res && ServerConfig.spawn_diag_verbose.Value)
                    {
                        // WARN, not console: a static lost here is invisible/unusable until the landblock
                        // reloads, and the console does not survive restarts (Guttering Ward-Lantern
                        // incident, 2026-07-18 20:21 — fingerprint was lost with the console).
                        log.Warn($"[SpawnDiag] CreateWorldObjects failed to add 0x{fo.Guid}:{fo.Name} " +
                                 $"[{fo.WeenieClassId} - {fo.WeenieType}] to landblock {Id.Landblock:X4} " +
                                 $"lbVar={VariationId?.ToString() ?? "null"} param={variationId?.ToString() ?? "null"} " +
                                 $"loc={fo.Location}");
                    }
                    fo.ActivateLinks(objects, shardObjects, parent);

                    if (fo.PhysicsObj != null)
                        fo.PhysicsObj.Order = 0;
                }

                CreateWorldObjectsCompleted = true;

                if (ServerConfig.landblock_lifecycle_diag_verbose.Value)
                    // pendingAdditions included: AddWorldObject always stages new objects there
                    // (they merge into worldObjects NEXT tick), so worldObjects.Count alone reads
                    // 0 on a healthy fresh spawn — a false void-block alarm (owner 2026-08-02).
                    log.Warn($"[LbLife] SPAWN-COMPLETE {Id.Landblock:X4} v={VariationId?.ToString() ?? "null"} " +
                             $"attempt={initSpawnAttempts} objects={worldObjects.Count + pendingAdditions.Count}");

                // Spawn boundary markers after normal objects (two independent systems, each self-gating)
                SpawnPrestigeBoundaryMarkers();
                SpawnZoneBoundaryMarkers();

                PhysicsLandblock.SortObjects();
            }, ActionPriority.Low));
        }

        /// <summary>
        /// Spawns boundary markers for prestige landblocks.
        /// Markers appear on edges adjacent to forbidden landblocks, forming a perimeter.
        /// </summary>
        private void SpawnPrestigeBoundaryMarkers()
        {
            // Only spawn for prestige variations (11+)
            var tier = PrestigeManager.GetTier(VariationId);
            if (tier <= 0) return;

            if (log.IsDebugEnabled)
                log.Debug($"[Prestige] Landblock {Id.Landblock:X4} (Var {VariationId}, Tier {tier}): Checking boundary markers...");

            // Check if this landblock is allowed for this tier
            bool thisAllowed = PrestigeManager.IsLandblockAllowed(VariationId, Id.Landblock);

            // Only spawn markers in approved landblocks
            if (!thisAllowed)
            {
                if (log.IsDebugEnabled)
                    log.Debug($"[Prestige] Landblock {Id.Landblock:X4} is NOT allowed - skipping boundary markers.");
                return;
            }

            var weenie = DatabaseManager.World.GetCachedWeenie((uint)ACE.Entity.Enum.WeenieClassName.W_SHOLANTERN_CLASS);
            if (weenie == null)
            {
                log.Warn($"[Prestige] SpawnPrestigeBoundaryMarkers: W_SHOLANTERN_CLASS weenie not found!");
                return;
            }

            // Edge boundaries (inset 10m from actual edge)
            const float edgeMin = 10.0f;
            const float edgeMax = 182.0f;
            const int markersPerEdge = 6;
            
            // Calculate evenly spaced positions including corners
            float edgeLength = edgeMax - edgeMin;
            float spacing = edgeLength / (markersPerEdge - 1);

            void SpawnMarkerAtPosition(float x, float y)
            {
                const float cornerEpsilon = 0.25f;
                // both marker systems can run on the same 11+ variation: never stack a lantern on a zone marker either
                foreach (var existing in _prestigeBoundaryMarkers.Concat(_zoneBoundaryMarkers))
                {
                    if (existing?.Location == null)
                        continue;
                    if (Math.Abs(existing.Location.PositionX - x) < cornerEpsilon && Math.Abs(existing.Location.PositionY - y) < cornerEpsilon)
                        return;
                }

                var marker = WorldObjectFactory.CreateNewWorldObject(weenie);
                if (marker == null)
                {
                    log.Warn($"[Prestige] Failed to create marker WorldObject");
                    return;
                }

                marker.Name = "Boundary Marker";
                marker.SetProperty(PropertyFloat.DefaultScale, 2.5f);

                // Prevent despawning - make it persistent like static objects
                marker.SetProperty(PropertyBool.Stuck, true);
                marker.TimeToRot = -1;  // Never rot/despawn

                // Ephemeral: re-derived at every load — must never be saved to the shard (persisted
                // markers become permanent ghosts; 7000+ had accumulated in biota before this guard).
                marker.SuppressShardPersistence = true;

                // Use physics system to calculate proper position (like encounters do)
                var pos = new Physics.Common.Position();
                pos.ObjCellID = (uint)(Id.Landblock << 16) | 1;
                pos.Variation = VariationId;
                pos.Frame = new Physics.Animation.AFrame(new Vector3(x, y, 0), Quaternion.Identity);
                pos.adjust_to_outside();

                // Get terrain Z height
                pos.Frame.Origin.Z = PhysicsLandblock.GetZ(pos.Frame.Origin);

                marker.Location = new Position(pos.ObjCellID, pos.Frame.Origin, pos.Frame.Orientation, pos.Variation);
                marker.Location.Variation = VariationId;

                if (AddWorldObject(marker, VariationId))
                {
                    _prestigeBoundaryMarkers.Add(marker);
                }
                else
                {
                    marker.Destroy();
                }
            }

            void SpawnMarkersOnEdge(float fixedCoord, bool isXFixed)
            {
                for (int i = 0; i < markersPerEdge; i++)
                {
                    float offset = edgeMin + (i * spacing);
                    float x = isXFixed ? fixedCoord : offset;
                    float y = isXFixed ? offset : fixedCoord;
                    SpawnMarkerAtPosition(x, y);
                }
            }

            bool ShouldSpawnOnEdge(ushort neighborLB)
            {
                bool neighborAllowed = PrestigeManager.IsLandblockAllowed(VariationId, neighborLB);
                return !neighborAllowed;
            }

            // Check each cardinal direction and spawn markers on edges facing forbidden landblocks
            // East edge (X = edgeMax)
            if (Id.LandblockX < 255 && ShouldSpawnOnEdge(Id.East.Landblock))
                SpawnMarkersOnEdge(edgeMax, true);

            // West edge (X = edgeMin)
            if (Id.LandblockX > 0 && ShouldSpawnOnEdge(Id.West.Landblock))
                SpawnMarkersOnEdge(edgeMin, true);

            // North edge (Y = edgeMax)
            if (Id.LandblockY < 255 && ShouldSpawnOnEdge(Id.North.Landblock))
                SpawnMarkersOnEdge(edgeMax, false);

            // South edge (Y = edgeMin)
            if (Id.LandblockY > 0 && ShouldSpawnOnEdge(Id.South.Landblock))
                SpawnMarkersOnEdge(edgeMin, false);

            if (_prestigeBoundaryMarkers.Count > 0)
            {
                log.Info($"[Prestige] Landblock {Id.Landblock:X4} (Var {VariationId}): Spawned {_prestigeBoundaryMarkers.Count} boundary markers.");
            }
        }

        public void RefreshPrestigeBoundaryMarkers()
        {
            actionQueue.EnqueueAction(new ActionEventDelegate(ActionType.Landblock_CreateWorldObjects, () =>
            {
                foreach (var marker in _prestigeBoundaryMarkers.ToList())
                {
                    if (marker == null)
                        continue;

                    RemoveWorldObject(marker.Guid, false, false, false);
                    marker.Destroy();
                }

                _prestigeBoundaryMarkers.Clear();
                SpawnPrestigeBoundaryMarkers();
            }));
        }

        /// <summary>
        /// Spawns Zone Control boundary perimeter markers (lanterns). Standalone system: consults only
        /// <see cref="Managers.ZoneControl.ZoneControlManager"/> — markers appear on the edges of allowed
        /// (bounded-zone member) landblocks that face landblocks outside the bounded union.
        /// </summary>
        private void SpawnZoneBoundaryMarkers()
        {
            // Boot-time landblocks (permaload) can reach here before any command touched Zone Control.
            Managers.ZoneControl.ZoneControlManager.EnsureLoaded();

            // Only spawn when a bounded zone exists at this landblock's variation
            if (!Managers.ZoneControl.ZoneControlManager.HasBoundedZonesAt(VariationId))
                return;

            if (log.IsDebugEnabled)
                log.Debug($"[ZoneControl] Landblock {Id.Landblock:X4} (Var {VariationId}): Checking boundary markers...");

            // Only spawn markers in allowed (member) landblocks
            if (!Managers.ZoneControl.ZoneControlManager.IsLandblockAllowed(VariationId, Id.Landblock))
            {
                if (log.IsDebugEnabled)
                    log.Debug($"[ZoneControl] Landblock {Id.Landblock:X4} is outside the bounded union - skipping boundary markers.");
                return;
            }

            var weenie = DatabaseManager.World.GetCachedWeenie((uint)ACE.Entity.Enum.WeenieClassName.W_SHOLANTERN_CLASS);
            if (weenie == null)
            {
                log.Warn($"[ZoneControl] SpawnZoneBoundaryMarkers: W_SHOLANTERN_CLASS weenie not found!");
                return;
            }

            // Edge boundaries (inset 10m from actual edge)
            const float edgeMin = 10.0f;
            const float edgeMax = 182.0f;
            const int markersPerEdge = 6;

            // Calculate evenly spaced positions including corners
            float edgeLength = edgeMax - edgeMin;
            float spacing = edgeLength / (markersPerEdge - 1);

            bool MarkerExistsAt(List<WorldObject> markers, float x, float y)
            {
                const float cornerEpsilon = 0.25f;
                foreach (var existing in markers)
                {
                    if (existing?.Location == null)
                        continue;
                    if (Math.Abs(existing.Location.PositionX - x) < cornerEpsilon && Math.Abs(existing.Location.PositionY - y) < cornerEpsilon)
                        return true;
                }
                return false;
            }

            void SpawnMarkerAtPosition(float x, float y)
            {
                if (MarkerExistsAt(_zoneBoundaryMarkers, x, y))
                    return;
                // Cosmetic only: when another boundary system already placed a lantern on this exact spot
                // (e.g. an overlapping perimeter at the same variation), don't stack a second one on top.
                if (MarkerExistsAt(_prestigeBoundaryMarkers, x, y))
                    return;

                var marker = WorldObjectFactory.CreateNewWorldObject(weenie);
                if (marker == null)
                {
                    log.Warn($"[ZoneControl] Failed to create marker WorldObject");
                    return;
                }

                marker.Name = "Boundary Marker";
                marker.SetProperty(PropertyFloat.DefaultScale, 2.5f);

                // Prevent despawning - make it persistent like static objects
                marker.SetProperty(PropertyBool.Stuck, true);
                marker.TimeToRot = -1;  // Never rot/despawn

                // Ephemeral: re-derived from the bounded union at every load — must never be saved to the
                // shard (persisted markers become permanent ghosts that ignore later boundary changes).
                marker.SuppressShardPersistence = true;

                // Cosmetic best-effort: perimeter lanterns on a water-facing edge can land over open water
                // where placement finds no floor (NoValidPosition). Skip those quietly instead of a [SpawnDiag] WARN.
                marker.SuppressSpawnPlacementDiag = true;

                // Use physics system to calculate proper position (like encounters do)
                var pos = new Physics.Common.Position();
                pos.ObjCellID = (uint)(Id.Landblock << 16) | 1;
                pos.Variation = VariationId;
                pos.Frame = new Physics.Animation.AFrame(new Vector3(x, y, 0), Quaternion.Identity);
                pos.adjust_to_outside();

                // Get terrain Z height
                pos.Frame.Origin.Z = PhysicsLandblock.GetZ(pos.Frame.Origin);

                marker.Location = new Position(pos.ObjCellID, pos.Frame.Origin, pos.Frame.Orientation, pos.Variation);
                marker.Location.Variation = VariationId;

                if (AddWorldObject(marker, VariationId))
                {
                    _zoneBoundaryMarkers.Add(marker);
                }
                else
                {
                    marker.Destroy();
                }
            }

            void SpawnMarkersOnEdge(float fixedCoord, bool isXFixed)
            {
                for (int i = 0; i < markersPerEdge; i++)
                {
                    float offset = edgeMin + (i * spacing);
                    float x = isXFixed ? fixedCoord : offset;
                    float y = isXFixed ? offset : fixedCoord;
                    SpawnMarkerAtPosition(x, y);
                }
            }

            bool ShouldSpawnOnEdge(ushort neighborLB)
            {
                return !Managers.ZoneControl.ZoneControlManager.IsLandblockAllowed(VariationId, neighborLB);
            }

            // Check each cardinal direction and spawn markers on edges facing outside landblocks
            // East edge (X = edgeMax)
            if (Id.LandblockX < 255 && ShouldSpawnOnEdge(Id.East.Landblock))
                SpawnMarkersOnEdge(edgeMax, true);

            // West edge (X = edgeMin)
            if (Id.LandblockX > 0 && ShouldSpawnOnEdge(Id.West.Landblock))
                SpawnMarkersOnEdge(edgeMin, true);

            // North edge (Y = edgeMax)
            if (Id.LandblockY < 255 && ShouldSpawnOnEdge(Id.North.Landblock))
                SpawnMarkersOnEdge(edgeMax, false);

            // South edge (Y = edgeMin)
            if (Id.LandblockY > 0 && ShouldSpawnOnEdge(Id.South.Landblock))
                SpawnMarkersOnEdge(edgeMin, false);

            if (_zoneBoundaryMarkers.Count > 0)
            {
                log.Info($"[ZoneControl] Landblock {Id.Landblock:X4} (Var {VariationId}): Spawned {_zoneBoundaryMarkers.Count} boundary markers.");
            }
        }

        /// <summary>
        /// Despawns and re-derives this landblock's Zone Control perimeter markers. Enqueued by
        /// <see cref="Managers.LandblockManager.EnqueueRefreshLoadedZoneBoundaryMarkers"/> whenever a
        /// mutation changes the bounded union at this landblock's variation.
        /// </summary>
        public void RefreshZoneBoundaryMarkers()
        {
            actionQueue.EnqueueAction(new ActionEventDelegate(ActionType.Landblock_CreateWorldObjects, () =>
            {
                foreach (var marker in _zoneBoundaryMarkers.ToList())
                {
                    if (marker == null)
                        continue;

                    RemoveWorldObject(marker.Guid, false, false, false);
                    marker.Destroy();
                }

                _zoneBoundaryMarkers.Clear();
                SpawnZoneBoundaryMarkers();
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

            actionQueue.EnqueueAction(new ActionEventDelegate(ActionType.Landblock_SpawnDynamicShardObjects, () =>
            {
                foreach (var fso in factoryShardObjects)
                    AddWorldObject(fso, VariationId);
            }, ActionPriority.Low));
        }

        /// <summary>
        /// Spawns the semi-randomized monsters scattered around the outdoors<para />
        /// This will be called from a separate task from our constructor. Use thread safety when interacting with this landblock.
        /// </summary>
        private void SpawnEncounters()
        {
            // World DB encounter table has no per-variation rows; by default these generators spawn on every landblock load.
            // Optional: align with unlayered base (VariationId null only) — skips explicit layers including retail 0 and prestige.
            if (ServerConfig.encounter_spawn_base_layer_only.Value && VariationId.HasValue)
                return;

            // encounter rows have no variation_Id; optionally skip only prestige layers (not retail 1–10)
            if (ServerConfig.encounter_spawn_base_variation_only.Value && PrestigeManager.IsPrestigeVariation(VariationId))
                return;

            // get the encounter spawns for this landblock
            var encounters = DatabaseManager.World.GetCachedEncountersByLandblock(Id.Landblock);

            // Zone Control terrain-override redirection (owner 2026-07-21): a zone that overrides this
            // block's terrain at this variation swaps in encounter generators from the zone's blocks
            // whose REAL terrain matches the override ("mark it obsidian, get the obsidian camps").
            // No-op when no override applies; independent of the zone's Enabled flag.
            encounters = Managers.ZoneControl.ZoneControlManager.RedirectEncountersForTerrainOverride(
                Id.Landblock, VariationId, encounters);

            foreach (var encounter in encounters)
            {
                var wo = WorldObjectFactory.CreateNewWorldObject(encounter.WeenieClassId);

                if (wo == null) continue;

                actionQueue.EnqueueAction(new ActionEventDelegate(ActionType.Landblock_SpawnEncounters, () =>
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

                    if (ServerConfig.override_encounter_spawn_rates.Value)
                    {
                        wo.RegenerationInterval = ServerConfig.encounter_regen_interval.Value;

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
                                profile.Delay = (float)ServerConfig.encounter_delay.Value;
                        }
                    }

                    if (!AddWorldObject(wo, VariationId))
                        wo.Destroy();
                }, ActionPriority.Low));
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
                
                // Throttle monster processing to prevent multi-second spikes during mass spawns
                // Without this, 400+ creatures can cause 4+ second freezes
                // Tuning: Lower = safer spikes (50-60), Higher = faster AI reactions (75-100)
                // At 75: ~0.2s max spike, 447 creatures = 1.8s total delay for last creature
                // Increased from 50 to 75 based on production saturation warnings
                // Configurable via: /modifylong monster_tick_throttle_limit <value> (min: 50, recommended: 75-125)
                int monstersProcessed = 0;
                var throttleValue = (int)ServerConfig.monster_tick_throttle_limit.Value;
                var maxMonstersPerTick = Math.Max(50, throttleValue); // Enforce minimum of 50 to prevent server lockup
                
                if (throttleValue < 50 && throttleValue != maxMonstersPerTick)
                    log.Warn($"[PERFORMANCE] monster_tick_throttle_limit set to {throttleValue}, enforcing minimum of 50. This value is too low and may cause server performance issues.");

                
                // Time Slicing: Budget 15ms per tick for monster processing
                // This prevents server lockup (lag/rubberbanding) when hundreds of monsters are active.
                // Monsters that miss this slice will remain at the front of the priority queue for the next tick.
                long monsterProcessingBudgetMs = 15;
                if (ServerConfig.monster_tick_throttle_limit.Value > 500) // Increase budget if they really want tons of monsters
                     monsterProcessingBudgetMs = 30;

                while (sortedCreaturesByNextTick.Count > 0 && monstersProcessed < maxMonstersPerTick) // Monster_Tick()
                {
                    var monster = sortedCreaturesByNextTick.First.Value;

                    // If they wanted to run before or at now
                    if (monster.NextMonsterTickTime <= currentUnixTime)
                    {
                        sortedCreaturesByNextTick.RemoveFirst();
                        // If the monster is dead, remove from the sorted tick list and don't re-add it.
                        if (monster.IsDead) continue;
                        monster.Monster_Tick(currentUnixTime);
                        sortedCreaturesByNextTick.AddLast(monster); // All creatures tick at a fixed interval
                        monstersProcessed++;

                        // Check time budget (every 5 monsters to avoid excessive stopwatch overhead)
                        if (monstersProcessed % 5 == 0 && stopwatch.ElapsedMilliseconds > monsterProcessingBudgetMs)
                            break;
                    }
                    else
                    {
                        break;
                    }
                }
                
                // Check if throttle is saturated by counting remaining creatures that are overdue
                int remainingDueCount = 0;
                if (monstersProcessed >= maxMonstersPerTick && sortedCreaturesByNextTick.Count > 0)
                {
                    // Count how many remaining creatures are overdue for processing
                    foreach (var creature in sortedCreaturesByNextTick)
                    {
                        if (creature.NextMonsterTickTime <= currentUnixTime)
                            remainingDueCount++;
                        else
                            break; // List is sorted, so we can stop once we hit a future tick time
                    }
                }
                
                // Alert if throttle is consistently maxed out (queue saturation)
                // Only alert after 3+ consecutive ticks of saturation to filter out temporary bursts
                if (monstersProcessed >= maxMonstersPerTick && remainingDueCount > 0)
                {
                    monsterTickThrottleWarningCount++;
                    
                    // Only warn if saturated for 3+ consecutive ticks AND 60 seconds since last warning
                    // This filters out expected initial dungeon load bursts (1-2 ticks) while catching sustained issues
                    if (monsterTickThrottleWarningCount >= 3 && DateTime.UtcNow - lastMonsterThrottleWarning > TimeSpan.FromSeconds(60))
                    {
                        var warningMsg = $"[PERFORMANCE] Landblock {Id:X8} Monster_Tick throttle saturated for {monsterTickThrottleWarningCount} consecutive ticks! Processed {monstersProcessed}, {remainingDueCount} overdue creatures remain. Total creatures: {sortedCreaturesByNextTick.Count}. Consider increasing maxMonstersPerTick from {maxMonstersPerTick}.";
                        log.Warn(warningMsg);
                        
                        // Send to Discord if configured
                        if (ACE.Server.Managers.ServerConfig.discord_performance_level.Value >= (long)ACE.Common.DiscordLogLevel.Info && ConfigManager.Config.Chat.PerformanceAlertsChannelId > 0)
                            {
                                var msg = $"[High Load] Landblock {Id:X8} Monster_Tick throttle saturated for {monsterTickThrottleWarningCount} consecutive ticks! Processed {monstersProcessed}, {remainingDueCount} overdue creatures remain.";
                                _ = Managers.DiscordChatManager.SendDiscordMessage("ServerMonitor", msg, ConfigManager.Config.Chat.PerformanceAlertsChannelId);
                            }

                        
                        lastMonsterThrottleWarning = DateTime.UtcNow;
                        // Don't reset counter here - let it continue tracking consecutive saturations
                    }
                }
                else
                {
                    // Reset counter when not saturated
                    monsterTickThrottleWarningCount = 0;
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

                CheckInitSpawnWatchdog(thisHeartBeat);

                ProcessPendingWorldObjectAdditionsAndRemovals();

                // Decay world objects
                if (lastHeartBeat != DateTime.MinValue)
                {
                    // Make a copy since objects might get removed during decay
                    var worldObjectsCopy = worldObjects.Values.ToList();
                    foreach (var wo in worldObjectsCopy)
                    {
                        if (wo.IsDecayable())
                            wo.Decay(thisHeartBeat - lastHeartBeat);
                    }
                }

                // players.Count guard: an occupied landblock must never go dormant or unload, even when the
                // player-heartbeat SetActive refresh fails to reach it (observed on variant instances entered
                // via a same-landblock variation teleport: lastActiveTime froze at creation, the instance went
                // dormant at +1min — passive mobs — and unloaded at exactly +5min WITH the player inside,
                // destroying every non-persisted creature; rift test-5's vanishing guardian, and the all-day
                // 0148 cell-raw unloads in the 2026-07-11 log during authoring).
                if (!Permaload && HasNoKeepAliveObjects && players.Count == 0)
                {
                    // Variation-instance guard: a player can be physically standing on this landblock
                    // (or an adjacent one) while registered on a DIFFERENT variation instance, so this
                    // instance's `players` list is empty even though someone is right here. Without this
                    // the v11 instance the player is actually interacting with goes dormant at +1min ->
                    // Monster AI + physics ticking suppressed -> mobs go passive (missiles/magic pass
                    // through stale physics, no aggro/attack) while melee still works. Judge presence by
                    // physical position across all variations, not the (drifting) instance bookkeeping.
                    if (lastActiveTime + dormantInterval < thisHeartBeat && HasPhysicalPlayerOnOrAdjacent())
                    {
                        SetActive();
                    }
                    else
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
                        {
                            // log.Info($"[Landblock Unload] Landblock {Id.Raw:X8} queuing for destruction. (UnloadInterval: {UnloadInterval})");
                            LandblockManager.AddToDestructionQueue(this, this.VariationId);
                        }
                        else if (IsDormant && (DateTime.UtcNow.Second % 10 == 0)) // Log periodically if dormant but not unloading
                        {
                            // Debug logging to see why it's not unloading
                            // log.Warn($"[Landblock Debug] {Id.Raw:X8} Dormant. Time until unload: {(lastActiveTime + UnloadInterval - thisHeartBeat).TotalSeconds:F1}s");
                        }
                    }
                }
                else if (!Permaload && thisHeartBeat.Second % 15 == 0)
                {
                     // Debug logging to see why it's kept alive
                     // log.Warn($"[Landblock Debug] {Id.Raw:X8} kept alive. Permaload: {Permaload}, NoKeepAlive: {HasNoKeepAliveObjects}, LastActive: {(thisHeartBeat - lastActiveTime).TotalSeconds:F1}s ago");
                }
                else if (Permaload && thisHeartBeat.Second % 30 == 0)
                {
                    // log.Warn($"[Landblock Debug] {Id.Raw:X8} is PERMALOAD.");
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
            // Iterate backwards to avoid issues when players disconnect during tick
            for (int i = players.Count - 1; i >= 0; i--)
            {
                // Add bounds check to prevent IndexOutOfRangeException
                if (i >= players.Count)
                    break;
                    
                var player = players[i];
                player.Player_Tick(currentUnixTime);
            }
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

        private bool HasPendingRemovals()
        {
            lock (pendingRemovalsLock)
                return pendingRemovals.Count > 0;
        }

        private bool IsPendingRemoval(ObjectGuid guid)
        {
            lock (pendingRemovalsLock)
                return pendingRemovals.Contains(guid);
        }

        private void QueuePendingRemoval(ObjectGuid guid)
        {
            lock (pendingRemovalsLock)
                pendingRemovals.Add(guid);
        }

        private void CancelPendingRemoval(ObjectGuid guid)
        {
            lock (pendingRemovalsLock)
                pendingRemovals.Remove(guid);
        }

        private ObjectGuid[] SnapshotPendingRemovals()
        {
            lock (pendingRemovalsLock)
                return pendingRemovals.Count == 0 ? Array.Empty<ObjectGuid>() : pendingRemovals.ToArray();
        }

        private bool TryClaimPendingRemoval(ObjectGuid guid)
        {
            lock (pendingRemovalsLock)
                return pendingRemovals.Remove(guid);
        }

        private void ProcessPendingWorldObjectAdditionsAndRemovals()
        {
            // Early exit optimization - this method is called 11 times per tick
            // Most calls find nothing to process, so avoid the iteration overhead
            if (pendingAdditions.IsEmpty && !HasPendingRemovals())
                return;
            
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

            var removals = SnapshotPendingRemovals();
            if (removals.Length > 0)
            {
                foreach (var objectGuid in removals)
                {
                    // Skip if another thread (or a re-add) cancelled this removal after the snapshot.
                    if (!TryClaimPendingRemoval(objectGuid))
                        continue;

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

        /// <summary>
        /// Enqueues an AddWorldObject operation to be processed on this landblock's thread.
        /// Use this when adding objects during multi-threaded physics ticking to avoid cross-thread errors.
        /// </summary>
        public void EnqueueAddWorldObjectForPhysics(WorldObject wo, int? variationId)
        {
            actionQueue.EnqueueAction(new ActionEventDelegate(ActionType.Landblock_CreateWorldObjects, () =>
            {
                AddWorldObjectInternal(wo, variationId);
            }));
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

            var alreadyPresent = wo != null && (worldObjects.ContainsKey(wo.Guid) || pendingAdditions.ContainsKey(wo.Guid));
            if (alreadyPresent && wo is Player p && ServerConfig.landblock_readd_diag_verbose.Value)
            {
                var nowMs = System.Environment.TickCount64;
                var lastMs = _landblockReaddDiagLastLogMs.GetOrAdd(p.Guid.Full, 0);
                if (nowMs - lastMs >= 5000)
                {
                    _landblockReaddDiagLastLogMs[p.Guid.Full] = nowMs;
                    LandblockReaddDiagMaybePrune(nowMs);
                    log.Warn($"[LandblockReAddDiag] Redundant AddWorldObjectInternal(Player): player={p.Name}({p.Guid.Full:X8}) lb={Id.Raw:X8} lbVar={VariationId?.ToString() ?? "null"} loc={p.Location} currentLb={p.CurrentLandblock?.Id.Raw:X8} currentLbVar={p.CurrentLandblock?.VariationId?.ToString() ?? "null"}");
                    log.Warn(System.Environment.StackTrace);
                }
            }

            // Defensive: If a player is already resident in this landblock (and not pending removal), do not re-add/re-init physics or re-notify.
            // This prevents accidental high-frequency re-add call sites from spamming CreateObject tracking.
            if (alreadyPresent && wo is Player alreadyHerePlayer && ReferenceEquals(alreadyHerePlayer.CurrentLandblock, this) && !IsPendingRemoval(wo.Guid))
                return true;

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
                        if (ServerConfig.log_generator_debug.Value &&
                            ACE.Server.Diagnostics.LogRateLimiter.ShouldEmit($"landblock_spawn_fail_gen:{wo.Generator.Guid.Full}", TimeSpan.FromMinutes(5), out var suppressed))
                        {
                            log.Debug($"AddWorldObjectInternal: couldn't spawn 0x{wo.Guid}:{wo.Name} [{wo.WeenieClassId} - {wo.WeenieType}] at {wo.Location} from generator {wo.Generator.WeenieClassId} - 0x{wo.Generator.Guid}:{wo.Generator.Name} | [SpawnDiag] landblock=0x{Id.Landblock:X4} this.VariationId={this.VariationId} addWorldObject_VariationId_param={VariationId} wo.Location.Variation={wo.Location.Variation}");
                            if (suppressed > 0)
                                log.Debug($"[GENERATOR] Rate-limiter: {suppressed} similar placement failures were suppressed for this generator in the last 5m.");
                        }
                        wo.NotifyOfEvent(RegenerationType.PickUp); // Notify generator the generated object is effectively destroyed, use Pickup to catch both cases.
                    }
                    else if (wo.IsGenerator) // Some generators will fail random spawns if they're circumference spans over water or cliff edges
                    {
                        if (ServerConfig.log_generator_debug.Value &&
                            ACE.Server.Diagnostics.LogRateLimiter.ShouldEmit($"landblock_spawn_fail_isgen:{wo.Guid.Full}", TimeSpan.FromMinutes(5), out var suppressed))
                        {
                            log.Debug($"AddWorldObjectInternal: couldn't spawn generator 0x{wo.Guid}:{wo.Name} [{wo.WeenieClassId} - {wo.WeenieType}] at {wo.Location}");
                            if (suppressed > 0)
                                log.Debug($"[GENERATOR] Rate-limiter: {suppressed} similar generator placement failures were suppressed in the last 5m.");
                        }
                    }
                    else if (wo.ProjectileTarget == null && wo is not SpellProjectile && ServerConfig.spawn_diag_verbose.Value)
                        // WARN, not console (see CreateWorldObjects) — non-generator statics failing physics
                        // placement are rare and each one is a quest object someone can't use.
                        log.Warn($"[SpawnDiag] AddWorldObjectInternal: couldn't spawn 0x{wo.Guid}:{wo.Name} " +
                                 $"[{wo.WeenieClassId} - {wo.WeenieType}] at {wo.Location} | " +
                                 $"lb={Id.Landblock:X4} lbVar={this.VariationId?.ToString() ?? "null"} param={VariationId?.ToString() ?? "null"}");

                    return false;
                }
            }

            if (!worldObjects.ContainsKey(wo.Guid))
                pendingAdditions[wo.Guid] = wo;
            else
                CancelPendingRemoval(wo.Guid);

            // broadcast to nearby players
            if (!alreadyPresent)
                wo.NotifyPlayers();

            if (wo is Player player)
            {
                if (ServerConfig.prestige_interaction_diag_verbose.Value)
                {
                    // Helpful when one player can't see another: shows whether physics/ObjMaint is even populated yet.
                    var physVar = player.PhysicsObj?.Position?.Variation;
                    log.Warn($"[PrestigeInteraction] Landblock.AddWorldObjectInternal(Player): player={player.Name}({player.Guid.Full:X8}) " +
                             $"lb={Id.Raw:X8} lbVar={VariationId?.ToString() ?? "null"} " +
                             $"locCell={player.Location?.Cell:X8} locVar={player.Location?.Variation?.ToString() ?? "null"} physVar={physVar?.ToString() ?? "null"} " +
                             $"knownPlayers={player.ObjMaint?.GetKnownPlayersCount().ToString() ?? "null"} knownObjs={player.ObjMaint?.GetKnownObjectsCount().ToString() ?? "null"}");
                }
                player.SetFogColor(FogColor);
            }
            // For player corpses, we prevent a single player from spamming corpses on a single
            // landblock. We search for and mark their oldest corpse for early decay if they have  
            // more than corpse_spam_limit corpses on a single landblock.
            else if (wo is Corpse new_corpse && !new_corpse.IsMonster)
            {
                long perPlayerCorpseLimit = ServerConfig.corpse_spam_limit.Value;
                int corpsesForThisPlayer = 0;

                Corpse oldestCorpseNotDecayingSoon = null;
                foreach (WorldObject w in worldObjects.Values.Union(pendingAdditions.Values))
                {
                    if (w is not Corpse existingCorpse) continue; // Not a corpse.
                    if (existingCorpse.VictimId != new_corpse.VictimId) continue; // Not this player's corpse.
                    corpsesForThisPlayer++;
                    if (existingCorpse.TimeToRot <= Corpse.EmptyDecayTime) continue; // Corpse already decaying soon.
                    if (oldestCorpseNotDecayingSoon == null || oldestCorpseNotDecayingSoon.CreationTimestamp > existingCorpse.CreationTimestamp)
                    {
                        oldestCorpseNotDecayingSoon = existingCorpse;
                    }
                }

                if (corpsesForThisPlayer > perPlayerCorpseLimit && oldestCorpseNotDecayingSoon != null)
                {
                    var corpse = GetObject(oldestCorpseNotDecayingSoon.Guid);

                    if (corpse != null)
                    {
                        log.Warn($"[CORPSE] Landblock.AddWorldObjectInternal(): {wo.Name} (0x{wo.Guid}) exceeds the per player limit of {perPlayerCorpseLimit} corpses for 0x{Id.Landblock:X4}. Adjusting TimeToRot for oldest {corpse.Name} (0x{corpse.Guid}), CreationTimestamp: {corpse.CreationTimestamp} ({Common.Time.GetDateTimeFromTimestamp(corpse.CreationTimestamp ?? 0).ToLocalTime():yyyy-MM-dd HH:mm:ss}), to Corpse.EmptyDecayTime({Corpse.EmptyDecayTime}).");
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
                QueuePendingRemoval(objectId);
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

                // Ghost-mob safety net (2026-07-17): the broadcast above only reaches the object's
                // KnownPlayers. That inverse link can be torn while a client still holds the CreateObject
                // (transient variation refusal in ObjectMaint.AddKnownPlayer during leash teleports;
                // stale-known CO resends never re-added it) — that client then keeps a frozen "ghost"
                // that never receives this delete. Sweep every player on this landblock instance and its
                // same-variation adjacents and send the delete to any player the object did NOT know:
                // a delete for a guid the client doesn't hold is a no-op, so over-sending is harmless.
                if (!fromPickup)
                {
                    var notified = new HashSet<uint>();
                    if (wo.PhysicsObj != null)
                        foreach (var kp in wo.PhysicsObj.ObjMaint.GetKnownPlayersValuesAsPlayer())
                            notified.Add(kp.Guid.Full);

                    var missed = 0;
                    void SweepLandblock(Landblock lb)
                    {
                        foreach (var p in lb.players.ToList())
                        {
                            if (p == null || p.Guid == wo.Guid || !notified.Add(p.Guid.Full))
                                continue;
                            missed++;
                            var player = p;
                            player.EnqueueAction(new ActionEventDelegate(ActionType.WorldObjectNetworking_BroadcastOther,
                                () => player.RemoveTrackedObject(wo, false)));
                        }
                    }

                    SweepLandblock(this);

                    if (missed > 0 && wo is Creature && ServerConfig.prestige_interaction_diag_verbose.Value)
                        log.Warn($"[GhostMob] {wo.Name}(0x{wo.Guid.Full:X8}) destroy on lb 0x{Id.Landblock:X4} v={VariationId?.ToString() ?? "null"}: " +
                                 $"delete sent to {missed} nearby same-variation player(s) missing from KnownPlayers (one-way CO healed).");
                    // Adjacent instances tick on their own group threads and mutate their own `players` lists, so each
                    // adjacent sweep runs ON THAT LANDBLOCK'S QUEUE with a read-only snapshot of who was already told.
                    var alreadyNotified = new HashSet<uint>(notified);
                    var deletedWo = wo;
                    foreach (var adj in Adjacents.ToList())
                    {
                        if (adj == null)
                            continue;
                        var target = adj;
                        target.EnqueueAction(new ActionEventDelegate(ActionType.WorldObjectNetworking_BroadcastOther, () =>
                        {
                            foreach (var p in target.players.ToList())
                            {
                                if (p == null || p.Guid == deletedWo.Guid || alreadyNotified.Contains(p.Guid.Full))
                                    continue;
                                var player = p;
                                player.EnqueueAction(new ActionEventDelegate(ActionType.WorldObjectNetworking_BroadcastOther,
                                    () => player.RemoveTrackedObject(deletedWo, false)));
                            }
                        }));
                    }
                }

                wo.PhysicsObj?.DestroyObject();
            }
        }

        public void EmitSignal(WorldObject emitter, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            foreach (var wo in worldObjects.Values)
            {
                if (!wo.HearLocalSignals) continue;
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
            if (IsPendingRemoval(guid))
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
        /// True when an online player is physically standing on this landblock id or an adjacent one,
        /// regardless of which variation instance they are registered on. Keeps a landblock awake when
        /// variation-instance player bookkeeping drifts (this instance's `players` list can be empty even
        /// with a player right here). Only reached once a block has passed its dormant threshold.
        /// </summary>
        /// <remarks>
        /// Reads <see cref="LandblockManager.OccupiedLandblocks"/>, the online-player landblock set snapshotted
        /// once per multi-threaded tick batch, so each call is O(9) rather than O(online players): with many
        /// variation instances all re-checking every dormant interval, a raw GetAllOnline() scan here was
        /// O(landblocks x players) on the tick path.
        /// </remarks>
        private bool HasPhysicalPlayerOnOrAdjacent()
        {
            var occupied = LandblockManager.OccupiedLandblocks;
            if (occupied.Count == 0)
                return false;
            // variation-aware: a player on the base twin must not keep every rift / prestige instance of this block alive
            var myVariation = Managers.VariationManager.NormalizeBase(VariationId);

            int thisX = (Id.Landblock >> 8) & 0xFF;
            int thisY = Id.Landblock & 0xFF;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int nx = thisX + dx;
                    int ny = thisY + dy;
                    if (nx < 0 || nx > 255 || ny < 0 || ny > 255)
                        continue;
                    if (occupied.Contains(((ushort)((nx << 8) | ny), myVariation)))
                        return true;
                }
            }
            return false;
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

            // Cross-variation guard (2026-08-23): objects that belong to a SIBLING variation instance of
            // this landblock can be parked in THIS instance's physics cells (stale PhysicsObj.Position
            // .Variation - see WorldObject_Teleport.cs "Final authoritative variation reconcile"). Tearing
            // those cells down orphaned them: still in the sibling's worldObjects, never streamed to any
            // client ("Tou Tou v11 empty after the base twin unloaded", 2026-08-23 12:24). Re-home them to
            // their own variation's physics before anything here is released.
            var rehomed = 0;
            try
            {
                foreach (var cell in PhysicsLandblock.LandCells.Values.ToList())
                    foreach (var pobj in cell.ObjectList.ToList())   // leave_cell below removes from ObjectList
                    {
                        var fwo = pobj?.WeenieObj?.WorldObject;
                        if (fwo == null || fwo.IsDestroyed || fwo is Player || fwo.Location == null)
                            continue;
                        if (Managers.VariationManager.SameVariationForVisibility(fwo.Location.Variation, VariationId))
                            continue;
                        // out of OUR cells (shadows + cell membership), then back in under ITS variation
                        pobj.remove_shadows_from_cells();
                        pobj.leave_cell(true);
                        if (pobj.Position != null)
                            pobj.Position.Variation = fwo.Location.Variation;
                        if (!fwo.AddPhysicsObj(fwo.Location.Variation))
                        {
                            log.Warn($"[LbLife] UNLOAD {Id.Landblock:X4} v={VariationId?.ToString() ?? "null"}: re-home of 0x{fwo.Guid}:{fwo.Name} (v={fwo.Location.Variation?.ToString() ?? "null"}) did not re-enter physics");
                            // AddPhysicsObj nulls PhysicsObj on failure; the sibling instance would NRE on its next tick,
                            // so take the object out of the world instead of leaving a physics-less ghost behind
                            fwo.Destroy(false);
                            continue;
                        }
                        rehomed++;
                    }
            }
            catch (Exception e)
            {
                log.Warn($"[LbLife] UNLOAD {Id.Landblock:X4} v={VariationId?.ToString() ?? "null"}: cross-variation re-home failed: {e.Message}");
            }
            if (rehomed > 0)
                log.Warn($"[LbLife] UNLOAD {Id.Landblock:X4} v={VariationId?.ToString() ?? "null"}: re-homed {rehomed} object(s) belonging to a sibling variation out of this instance's physics cells");

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

            // Clear collections to release memory and break reference cycles
            sortedCreaturesByNextTick.Clear();
            sortedWorldObjectsByNextHeartbeat.Clear();
            sortedGeneratorsByNextGeneratorUpdate.Clear();
            sortedGeneratorsByNextRegeneration.Clear();
            players.Clear();
            Adjacents.Clear();
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
            var savedObjects = new List<WorldObject>();

            // First pass: only process objects that have changes or are containers
            // This avoids iterating through static objects that never change
            foreach (var wo in worldObjects.Values)
            {
                // Skip objects that don't need persistence entirely
                if (!wo.IsStaticThatShouldPersistToShard() && !wo.IsDynamicThatShouldPersistToShard())
                    continue;

                // Early exit optimization: skip objects with no changes and no inventory
                if (!wo.ChangesDetected && wo is not Container)
                    continue;

                AddWorldObjectToBiotasSaveCollection(wo, biotas, savedObjects);
            }

            if (biotas.Count > 0)
            {
                DatabaseManager.Shard.SaveBiotasInParallel(
                    biotas,
                    result =>
                    {
                        // Clear SaveInProgress flags on world thread for thread safety
                        var clearFlagsAction = new ACE.Server.Entity.Actions.ActionChain();
                        clearFlagsAction.AddAction(WorldManager.ActionQueue, ActionType.Landblock_ClearFlagsAfterSave, () =>
                        {
                            foreach (var wo in savedObjects)
                            {
                                if (!wo.IsDestroyed)
                                    wo.SaveInProgress = false;
                            }

                            if (!result)
                            {
                                log.Warn($"[LANDBLOCK SAVE] Bulk save for landblock {Id.Raw}{(VariationId.HasValue ? $":v{VariationId.Value}" : string.Empty)} returned false; SaveInProgress flags cleared to avoid stuck state.");
                            }
                        });
                        clearFlagsAction.EnqueueChain();
                    },
                    $"SaveDB:Landblock:{this.Id.Raw}{(this.VariationId.HasValue ? $":v{this.VariationId.Value}" : string.Empty)}"
                );
            }
        }

        private static void AddWorldObjectToBiotasSaveCollection(WorldObject wo, Collection<(Biota biota, ReaderWriterLockSlim rwLock)> biotas, List<WorldObject> savedObjects)
        {
            if (wo.ChangesDetected && !wo.SaveInProgress)
            {
                wo.SaveBiotaToDatabase(false);
                biotas.Add((wo.Biota, wo.BiotaDatabaseLock));
                savedObjects.Add(wo);
            }

            // Only recurse into containers - most objects don't have inventory
            if (wo is Container container)
            {
                // Early exit if container is empty
                if (container.Inventory.Count == 0)
                    return;

                foreach (var item in container.Inventory.Values)
                    AddWorldObjectToBiotasSaveCollection(item, biotas, savedObjects);
            }
        }

        /// <summary>
        /// This is only used for very specific instances, such as broadcasting player deaths to the destination lifestone block
        /// This is a rarely used method to broadcast network messages to all of the players within a landblock,
        /// and possibly the adjacent landblocks.
        /// </summary>
        public void EnqueueBroadcast(ICollection<Player> excludeList, bool adjacents, Position pos = null, float? maxRangeSq = null, params OutboundGameMessage[] msgs)
        {
            // broadcast messages to player in this landblock
            foreach (var player in this.players)
            {
                // for landblock death broadcasts:
                // exclude players that have already been broadcast to within range of the death
                if (excludeList != null && excludeList.Contains(player))
                    continue;

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
                foreach (var adjacent in this.Adjacents)
                {
                    if (adjacent == null) continue;
                    adjacent.EnqueueBroadcast(excludeList, false, pos, maxRangeSq, msgs);
                }
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
