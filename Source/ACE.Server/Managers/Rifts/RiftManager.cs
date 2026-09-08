using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

using log4net;
using Newtonsoft.Json;

using ACE.Common;
using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Factories;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Managers.ZoneScaling;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers.Rifts
{
    /// <summary>
    /// Greater Rifts: private, timed, tier-scaled dungeon runs. Each run owns an ephemeral NEGATIVE landblock
    /// variation — the prestige system treats the entire negative range as retail tier 0 by construction, so
    /// rifts share no code or state with prestige. Config lives in shard DB key <c>rift_config</c> (JSON);
    /// runs are in-memory only. Solo (phase 1): one run per player, whole run owned by its opener.
    /// </summary>
    public static class RiftManager
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const string StoreKey = "rift_config";

        /// <summary>Player PropertyInt: highest rift tier cleared (rift custom prop block 50400-50449).</summary>
        public const int PropHighestClearedTier = 50400;

        // first allocated variation is -1000 (leaves -1..-999 free for any future manual use)
        private static int _nextVariation = -999;

        private static readonly ConcurrentDictionary<int, RiftRun> _runsByVariation = new();
        private static readonly ConcurrentDictionary<uint, RiftRun> _runsByOwner = new();
        private static readonly ConcurrentDictionary<uint, ushort> _lastDungeonByOwner = new();

        private static readonly object _configLock = new object();
        private static volatile RiftConfig _config;

        private static double _nextTickTime;

        // time-remaining chat calls, in seconds remaining (descending)
        private static readonly int[] TimeAnnounceThresholds = { 600, 300, 60 };

        #region config store

        public static RiftConfig Config
        {
            get
            {
                var cfg = _config;
                if (cfg != null)
                    return cfg;

                lock (_configLock)
                {
                    if (_config == null)
                        LoadConfig();
                    return _config;
                }
            }
        }

        private static void LoadConfig()
        {
            RiftConfig cfg = null;
            try
            {
                string json = null;
                if (DatabaseManager.ShardConfig.StringExists(StoreKey))
                    json = DatabaseManager.ShardConfig.GetString(StoreKey)?.Value;

                if (!string.IsNullOrWhiteSpace(json))
                    cfg = JsonConvert.DeserializeObject<RiftConfig>(json);
            }
            catch (Exception ex)
            {
                log.Error($"[RIFT] Failed to load {StoreKey}, starting with defaults. {ex}");
            }

            _config = cfg ?? new RiftConfig();
        }

        public static void SaveConfig()
        {
            lock (_configLock)
            {
                var jsonOut = JsonConvert.SerializeObject(Config);
                if (DatabaseManager.ShardConfig.StringExists(StoreKey))
                    DatabaseManager.ShardConfig.SaveString(new ConfigPropertiesString { Key = StoreKey, Value = jsonOut, Description = "Greater Rifts config (JSON)" });
                else
                    DatabaseManager.ShardConfig.AddString(StoreKey, jsonOut, "Greater Rifts config (JSON)");
            }
        }

        public static void ReloadConfig()
        {
            lock (_configLock)
                LoadConfig();
        }

        #endregion

        #region registry / hot-path reads

        /// <summary>Rift instances own the entire negative variation range. Single compare — hot-path safe.</summary>
        public static bool IsRiftVariation(int? variation) => variation.HasValue && variation.Value < 0;

        public static bool TryGetRun(int? variation, out RiftRun run)
        {
            run = null;
            return variation.HasValue && variation.Value < 0 && _runsByVariation.TryGetValue(variation.Value, out run);
        }

        public static RiftRun GetRunByOwner(uint playerGuid)
        {
            return _runsByOwner.TryGetValue(playerGuid, out var run) ? run : null;
        }

        public static IReadOnlyCollection<RiftRun> GetActiveRuns() => _runsByVariation.Values.ToList();

        /// <summary>
        /// Landblock-loader hook: world-DB landblock instances (statics + monster generators) are
        /// per-variation EXACT-match rows, and a rift's ephemeral negative variation has none. For rift
        /// variations this returns the run's configured SOURCE variation (null = unlayered base rows) so the
        /// private copy loads that population; every spawned object is still stamped with the rift's own
        /// variation by the factory. Non-rift variations pass through untouched.
        /// </summary>
        public static int? ResolveInstanceSourceVariation(int? variationId)
        {
            if (!variationId.HasValue || variationId.Value >= 0)
                return variationId;

            if (_runsByVariation.TryGetValue(variationId.Value, out var run))
                return run.SourceVariation;

            // Unregistered negative variation: pass through to its own rows. This makes -1..-999 usable as
            // DESIGN variations — admins author rift layouts there with the normal content tools (rows save
            // at that variation, load back exactly, static guids, no cloning) and pool entries point at
            // them via SourceVariation. A stale rift variation (<= -1000) just loads empty. Prestige
            // ignores all negatives by construction.
            return variationId;
        }

        /// <summary>
        /// Clone source landblock-instance rows for a rift copy, remapping every static guid to a freshly
        /// allocated dynamic guid (link parent/child references remapped consistently). The source rows'
        /// home variation may be LIVE at the same time — two world objects must never share a guid, or
        /// guid-keyed lookups (selection, attack targeting, ObjMaint) resolve to the wrong copy.
        /// The cached source list is never mutated.
        /// </summary>
        public static List<ACE.Database.Models.World.LandblockInstance> CloneInstancesForRift(List<ACE.Database.Models.World.LandblockInstance> source)
        {
            var clones = new List<ACE.Database.Models.World.LandblockInstance>(source.Count);
            var guidMap = new Dictionary<uint, uint>(source.Count);

            foreach (var row in source)
                guidMap[row.Guid] = GuidManager.NewDynamicGuid().Full;

            foreach (var row in source)
            {
                var clone = new ACE.Database.Models.World.LandblockInstance
                {
                    Guid = guidMap[row.Guid],
                    Landblock = row.Landblock,
                    WeenieClassId = row.WeenieClassId,
                    ObjCellId = row.ObjCellId,
                    OriginX = row.OriginX, OriginY = row.OriginY, OriginZ = row.OriginZ,
                    AnglesW = row.AnglesW, AnglesX = row.AnglesX, AnglesY = row.AnglesY, AnglesZ = row.AnglesZ,
                    IsLinkChild = row.IsLinkChild,
                    VariationId = row.VariationId,
                };

                foreach (var link in row.LandblockInstanceLink)
                {
                    clone.LandblockInstanceLink.Add(new ACE.Database.Models.World.LandblockInstanceLink
                    {
                        ParentGuid = guidMap.TryGetValue(link.ParentGuid, out var pg) ? pg : link.ParentGuid,
                        ChildGuid = guidMap.TryGetValue(link.ChildGuid, out var cg) ? cg : link.ChildGuid,
                    });
                }

                clones.Add(clone);
            }

            return clones;
        }

        /// <summary>
        /// Spawn-site hook (GeneratorProfile / WorldObjectFactory): rift monster scaling, applied AFTER the
        /// retail/Zone-Control spawn pass. No-op unless the variation belongs to a live run.
        /// </summary>
        public static void TryApplyRiftScaling(Creature creature, int? variation)
        {
            if (!variation.HasValue || variation.Value >= 0 || creature == null)
                return;

            if (!_runsByVariation.TryGetValue(variation.Value, out var run))
                return;

            if (!creature.IsMonster || !creature.Attackable)
                return;

            RiftScaling.Apply(creature, run, Config);
        }

        #endregion

        #region open / enter / leave

        /// <summary>Opens a rift run for a player. Returns null on success, else a player-facing error.</summary>
        public static string OpenRun(Player player, int tier, ushort? dungeonOverride = null)
        {
            if (player == null)
                return "No player.";
            if (tier < 1)
                return "Tier must be 1 or higher.";

            var cfg = Config;

            if (_runsByOwner.ContainsKey(player.Guid.Full))
                return "You already have an open rift. Use /rift return to re-enter it, or /rift abandon to end it.";

            if (_runsByVariation.Count >= cfg.MaxActiveRuns)
                return "Too many rifts are open right now. Try again shortly.";

            if (cfg.DungeonPool.Count == 0)
                return "No rift dungeons are configured.";

            RiftDungeonEntry dungeon;
            if (dungeonOverride.HasValue)
            {
                dungeon = cfg.DungeonPool.FirstOrDefault(d => d.Landblock == dungeonOverride.Value);
                if (dungeon == null)
                    return $"Landblock {dungeonOverride.Value:X4} is not in the rift dungeon pool.";
            }
            else
            {
                var candidates = cfg.DungeonPool;
                if (candidates.Count > 1 && _lastDungeonByOwner.TryGetValue(player.Guid.Full, out var lastLb))
                {
                    var filtered = candidates.Where(d => d.Landblock != lastLb).ToList();
                    if (filtered.Count > 0)
                        candidates = filtered;
                }
                dungeon = candidates[ThreadSafeRandom.Next(0, candidates.Count - 1)];
            }

            if (dungeon.Entry == null)
                return $"Rift dungeon {dungeon.Name ?? dungeon.Landblock.ToString("X4")} has no entry position set.";

            var variation = Interlocked.Decrement(ref _nextVariation);

            // Defensive: rift variations only ever go more negative and reset on restart. Wrapping past
            // int.MinValue would flip positive and collide with real variations. In practice this needs
            // ~2 billion runs per process lifetime, but log loudly well before the boundary so it's caught.
            if (variation < int.MinValue + 10_000)
                log.Error($"[RIFT] _nextVariation is nearing int.MinValue ({variation}); restart the server to reset rift variation allocation before it wraps.");

            var run = new RiftRun
            {
                RunId = -variation,
                Variation = variation,
                Tier = tier,
                OwnerGuid = player.Guid.Full,
                OwnerName = player.Name,
                DungeonLb = dungeon.Landblock,
                DungeonName = dungeon.Name ?? dungeon.Landblock.ToString("X4"),
                Entry = dungeon.Entry,
                GuardianPos = dungeon.Guardian ?? dungeon.Entry,
                SourceVariation = dungeon.SourceVariation,
                ReturnPos = RiftPos.FromPosition(player.Location),
                ReturnVariation = player.Location?.Variation,
                StartTime = Time.GetUnixTime(),
                DurationSeconds = Math.Max(60, cfg.TimerSeconds),
                ProgressRequired = Math.Max(1, cfg.ProgressBase + cfg.ProgressPerTier * tier),
                ZoneName = $"rift_{-variation}",
            };

            // skip time announcements that are at/above the full duration
            while (run.NextTimeAnnounceIdx < TimeAnnounceThresholds.Length &&
                   TimeAnnounceThresholds[run.NextTimeAnnounceIdx] >= run.DurationSeconds)
                run.NextTimeAnnounceIdx++;

            RegisterRuntimeLootZone(run, cfg);

            _runsByVariation[variation] = run;
            _runsByOwner[player.Guid.Full] = run;
            _lastDungeonByOwner[player.Guid.Full] = dungeon.Landblock;

            log.Info($"[RIFT] Run {run.RunId}: {player.Name} opened tier {tier} in {run.DungeonName} ({run.DungeonLb:X4}) v{variation}");

            Message(run, $"The rift swirls open - Tier {tier} ({run.DungeonName}). Slay its denizens! {FormatTime(run.DurationSeconds)} remaining.");
            TeleportPlayer(player, run.Entry.ToPosition(variation));

            return null;
        }

        /// <summary>Re-enter a live run (after death or /rift leave). Returns null on success.</summary>
        public static string ReEnter(Player player)
        {
            var run = GetRunByOwner(player.Guid.Full);
            if (run == null)
                return "You have no open rift.";
            if (run.State != RiftRunState.Active && run.State != RiftRunState.GuardianUp)
                return "Your rift has ended.";
            if (player.Location != null && player.Location.Variation == run.Variation)
                return "You are already inside your rift.";

            TeleportPlayer(player, run.Entry.ToPosition(run.Variation));
            return null;
        }

        /// <summary>Teleport out of the rift without ending it. Returns null on success.</summary>
        public static string Leave(Player player)
        {
            var run = GetRunByOwner(player.Guid.Full);
            if (run == null || player.Location == null || player.Location.Variation != run.Variation)
                return "You are not inside a rift.";

            ReturnPlayer(player, run);
            return null;
        }

        /// <summary>End the player's run early (counts as failed, immediate teardown). Returns null on success.</summary>
        public static string Abandon(Player player)
        {
            var run = GetRunByOwner(player.Guid.Full);
            if (run == null)
                return "You have no open rift.";
            if (!TryEndRun(run, RiftRunState.Failed))
                return "Your rift has already ended.";

            Message(run, "You abandon the rift.");
            run.CloseAtTime = Time.GetUnixTime(); // next tick tears it down
            return null;
        }

        /// <summary>Dev: force-close a run regardless of state.</summary>
        public static bool ForceClose(RiftRun run)
        {
            if (run == null)
                return false;
            TryEndRun(run, RiftRunState.Failed);
            run.CloseAtTime = 0;
            CloseRun(run);
            return true;
        }

        /// <summary>
        /// Login guard, biota-level (same shape as Player.HandleNoLogLandblock, called right after it):
        /// a player logging in inside a rift instance whose run is gone (or isn't theirs / has ended) is
        /// relocated to their lifestone before entering the world.
        /// </summary>
        public static void HandleLoginInRiftInstance(ACE.Entity.Models.Biota biota)
        {
            if (biota == null || !biota.PropertiesPosition.TryGetValue(PositionType.Location, out var location))
                return;

            if (!location.VariationId.HasValue || location.VariationId.Value >= 0)
                return;

            if (_runsByVariation.TryGetValue(location.VariationId.Value, out var run) &&
                run.OwnerGuid == biota.Id &&
                (run.State == RiftRunState.Active || run.State == RiftRunState.GuardianUp))
                return; // their rift is still live — let them come back in where they left

            if (!biota.PropertiesPosition.TryGetValue(PositionType.Sanctuary, out var lifestone))
                return;

            var staleVariation = location.VariationId;
            location.ObjCellId = lifestone.ObjCellId;
            location.PositionX = lifestone.PositionX;
            location.PositionY = lifestone.PositionY;
            location.PositionZ = lifestone.PositionZ;
            location.RotationX = lifestone.RotationX;
            location.RotationY = lifestone.RotationY;
            location.RotationZ = lifestone.RotationZ;
            location.RotationW = lifestone.RotationW;
            location.VariationId = lifestone.VariationId;

            log.Info($"[RIFT] Player 0x{biota.Id:X8} logged in inside a closed rift instance (v{staleVariation}) - relocated to lifestone.");
        }

        #endregion

        #region kill hook / progress / guardian

        /// <summary>
        /// Called once per creature death from Creature_Death.OnDeath. Fast-bails for the whole normal world
        /// (variation null / >= 0). Runs on the instance landblock's thread, so run mutation is single-threaded.
        /// </summary>
        public static void OnCreatureDeath(Creature creature, Server.Entity.DamageHistoryInfo lastDamager)
        {
            var variation = creature?.Location?.Variation;
            if (!variation.HasValue || variation.Value >= 0)
                return;

            if (creature is Player)
                return;

            if (!_runsByVariation.TryGetValue(variation.Value, out var run))
                return;

            if (run.State == RiftRunState.GuardianUp && creature.Guid.Full == run.GuardianGuid)
            {
                HandleCleared(run);
                return;
            }

            if (run.State != RiftRunState.Active || !creature.IsMonster)
                return;

            var cfg = Config;
            run.Progress += cfg.ProgressPerKill;

            var pct = run.ProgressPercent;
            var step = pct / 10;
            if (step > run.LastAnnouncedStep && pct < 100)
            {
                run.LastAnnouncedStep = step;
                Message(run, $"Rift progress: {pct}% ({FormatTime(RemainingSeconds(run))} remaining)");
            }

            if (run.Progress >= run.ProgressRequired)
                SpawnGuardian(run, cfg);
        }

        private static void SpawnGuardian(RiftRun run, RiftConfig cfg)
        {
            var pool = cfg.GuardianPools.FirstOrDefault(p => p.Wcids.Count > 0 && run.Tier >= p.MinTier && run.Tier <= p.MaxTier)
                    ?? cfg.GuardianPools.FirstOrDefault(p => p.Wcids.Count > 0);

            if (pool == null)
            {
                log.Warn($"[RIFT] Run {run.RunId}: no guardian pool configured - treating full progress as a clear.");
                HandleCleared(run);
                return;
            }

            var wcid = pool.Wcids[ThreadSafeRandom.Next(0, pool.Wcids.Count - 1)];
            var guardian = WorldObjectFactory.CreateNewWorldObject(wcid) as Creature;
            if (guardian == null)
            {
                log.Error($"[RIFT] Run {run.RunId}: failed to create guardian wcid {wcid} - treating full progress as a clear.");
                HandleCleared(run);
                return;
            }

            RiftScaling.Apply(guardian, run, cfg, isGuardian: true);

            // enter_world placement fails at an exact captured floor-z for scaled models (test-5: Vicky,
            // scale 1.2, rejected the middle of a wide-open room at z=floor, placed fine at +0.25z), so the
            // primary attempt spawns 0.25 above the configured spot — she settles onto the floor instantly.
            // A failed EnterWorld leaves the WO retryable (AddPhysicsObj nulls PhysicsObj; the next add
            // re-inits it), so walk a fallback ladder before conceding: nudged spot → exact spot → run
            // entry → the owner's feet (a spot a player demonstrably fits).
            var attempts = new List<Position>
            {
                (run.GuardianPos ?? run.Entry).ToPosition(run.Variation),
                (run.GuardianPos ?? run.Entry).ToPosition(run.Variation),
                run.Entry.ToPosition(run.Variation),
            };
            attempts[0].PositionZ += 0.25f;

            var owner = PlayerManager.GetOnlinePlayer(run.OwnerGuid);
            if (owner?.Location != null && owner.Location.Variation == run.Variation)
                attempts.Add(new Position(owner.Location));

            var entered = false;
            foreach (var pos in attempts)
            {
                guardian.Location = pos;
                if (guardian.EnterWorld())
                {
                    entered = true;
                    break;
                }
                log.Warn($"[RIFT] Run {run.RunId}: guardian {wcid} placement failed at {pos} - trying fallback.");
            }

            if (!entered)
            {
                log.Error($"[RIFT] Run {run.RunId}: guardian {wcid} failed to enter world (all {attempts.Count} placements) - treating full progress as a clear.");
                guardian.Destroy();
                HandleCleared(run);
                return;
            }

            // Same lock as TryEndRun: the timer (world thread) or /rift abandon can end the run while
            // the guardian was being placed; an unlocked write here would overwrite that Failed state
            // and the next tick would fail the run a second time.
            lock (run)
            {
                if (run.State != RiftRunState.Active)
                {
                    log.Info($"[RIFT] Run {run.RunId}: run ended ({run.State}) while the guardian was spawning - guardian discarded.");
                    guardian.Destroy();
                    return;
                }
                run.GuardianGuid = guardian.Guid.Full;
                run.State = RiftRunState.GuardianUp;
            }
            Message(run, $"The rift is at full power! {guardian.Name} has appeared - defeat it! {FormatTime(RemainingSeconds(run))} remaining.");
            log.Info($"[RIFT] Run {run.RunId}: guardian {guardian.Name} ({wcid}) spawned.");
        }

        /// <summary>Atomic end-state transition: guardian death (instance thread) and timer expiry (world tick
        /// thread) can race — exactly one wins.</summary>
        private static bool TryEndRun(RiftRun run, RiftRunState endState)
        {
            lock (run)
            {
                if (run.State == RiftRunState.Cleared || run.State == RiftRunState.Failed)
                    return false;
                run.State = endState;
                return true;
            }
        }

        private static void HandleCleared(RiftRun run)
        {
            if (!TryEndRun(run, RiftRunState.Cleared))
                return;

            var cfg = Config;
            run.CloseAtTime = Time.GetUnixTime() + Math.Max(5, cfg.GraceSeconds);

            var elapsed = (int)(Time.GetUnixTime() - run.StartTime);
            Message(run, $"Rift cleared in {FormatTime(elapsed)}! You will be returned in {Math.Max(5, cfg.GraceSeconds)} seconds.");
            log.Info($"[RIFT] Run {run.RunId}: CLEARED tier {run.Tier} by {run.OwnerName} in {elapsed}s.");

            var player = PlayerManager.GetOnlinePlayer(run.OwnerGuid);
            if (player != null)
            {
                // OnCreatureDeath runs on the rift instance's landblock thread; the owner may be standing in another
                // landblock group (Leave keeps the run Active), so the property write and the inventory grant hop onto
                // the owner's own queue and re-resolve the player there in case they logged off in between.
                var ownerGuid = run.OwnerGuid; var clearedRun = run; var clearedCfg = cfg;
                player.EnqueueAction(new ACE.Server.Entity.Actions.ActionEventDelegate(ACE.Server.Entity.Actions.ActionType.ControlFlowDelay, () =>
                {
                    var owner = PlayerManager.GetOnlinePlayer(ownerGuid);
                    if (owner == null)
                        return;
                    var best = owner.GetProperty((PropertyInt)PropHighestClearedTier) ?? 0;
                    if (clearedRun.Tier > best)
                    {
                        owner.SetProperty((PropertyInt)PropHighestClearedTier, clearedRun.Tier);
                        Message(clearedRun, $"New personal best - Tier {clearedRun.Tier}!");
                    }

                    GiveCurrencyReward(owner, clearedRun, clearedCfg);
                }));
            }
        }

        private static void HandleFailed(RiftRun run)
        {
            if (!TryEndRun(run, RiftRunState.Failed))
                return;

            var cfg = Config;
            run.CloseAtTime = Time.GetUnixTime() + Math.Max(5, cfg.GraceSeconds);
            Message(run, $"The rift's power fades - time has run out. You keep what you have won, but the rift is lost. You will be returned in {Math.Max(5, cfg.GraceSeconds)} seconds.");
            log.Info($"[RIFT] Run {run.RunId}: FAILED (timer) tier {run.Tier} by {run.OwnerName} at {run.ProgressPercent}%.");
        }

        private static void GiveCurrencyReward(Player player, RiftRun run, RiftConfig cfg)
        {
            if (cfg.CurrencyWcid == 0)
                return;

            var amount = (int)Math.Floor(cfg.CurrencyBase + cfg.CurrencyPerTier * run.Tier);
            if (amount < 1)
                return;

            var token = WorldObjectFactory.CreateNewWorldObject(cfg.CurrencyWcid);
            if (token == null)
            {
                log.Error($"[RIFT] Run {run.RunId}: failed to create reward currency wcid {cfg.CurrencyWcid}.");
                return;
            }

            var maxStack = token.MaxStackSize ?? 1;
            amount = Math.Min(amount, (int)maxStack);
            if (amount > 1)
                token.SetStackSize(amount);

            if (player.TryCreateInInventoryWithNetworking(token))
                Message(run, $"You receive {amount:N0} {token.Name} for clearing the rift!");
            else
                token.Destroy();
        }

        #endregion

        #region tick / teardown

        /// <summary>Called from WorldManager.UpdateGameWorld. Self-throttles to ~1/s; O(active runs).</summary>
        public static void Tick()
        {
            if (_runsByVariation.IsEmpty)
                return;

            var now = Time.GetUnixTime();
            if (now < _nextTickTime)
                return;
            _nextTickTime = now + 1;

            foreach (var run in _runsByVariation.Values)
            {
                switch (run.State)
                {
                    case RiftRunState.Active:
                    case RiftRunState.GuardianUp:
                        // Keep the live instance warm for the whole run: SetActive blocks dormancy/unload
                        // (the player-heartbeat refresh doesn't reliably reach variant instances — test-5
                        // unload-under-player), and it must survive the owner being dead/outside mid-run.
                        // GetLandblock re-materializes it if something still tore it down (re-entry works).
                        LandblockManager.GetLandblock(new LandblockId((uint)((run.DungeonLb << 16) | 0xFFFF)),
                            false, run.Variation, false)?.SetActive();

                        while (run.NextTimeAnnounceIdx < TimeAnnounceThresholds.Length &&
                               RemainingSeconds(run) <= TimeAnnounceThresholds[run.NextTimeAnnounceIdx])
                        {
                            Message(run, $"{FormatTime(TimeAnnounceThresholds[run.NextTimeAnnounceIdx])} remaining in the rift!");
                            run.NextTimeAnnounceIdx++;
                        }

                        if (now >= run.EndTime)
                            HandleFailed(run);
                        break;

                    case RiftRunState.Cleared:
                    case RiftRunState.Failed:
                        if (now >= run.CloseAtTime)
                            CloseRun(run);
                        break;
                }
            }
        }

        private static void CloseRun(RiftRun run)
        {
            if (!_runsByVariation.TryRemove(run.Variation, out _))
                return; // already closed by another path

            _runsByOwner.TryRemove(run.OwnerGuid, out _);

            try
            {
                ZoneControlManager.RemoveRuntimeZone(run.ZoneName);
            }
            catch (Exception ex)
            {
                log.Error($"[RIFT] Run {run.RunId}: failed to remove runtime zone {run.ZoneName}. {ex}");
            }

            var player = PlayerManager.GetOnlinePlayer(run.OwnerGuid);
            if (player != null && player.Location != null && player.Location.Variation == run.Variation)
                ReturnPlayer(player, run);

            log.Info($"[RIFT] Run {run.RunId}: closed ({run.State}). Instance {run.DungeonLb:X4} v{run.Variation} will unload when empty.");
        }

        private static void ReturnPlayer(Player player, RiftRun run)
        {
            var dest = run.ReturnPos?.ToPosition(run.ReturnVariation);
            if (dest == null)
            {
                var fallback = player.Sanctuary ?? player.Instantiation;
                if (fallback == null)
                    return;
                dest = new Position(fallback);
            }

            TeleportPlayer(player, dest);
        }

        #endregion

        #region runtime loot zone

        /// <summary>Register the run's ephemeral ZoneControl zone: tier-scaled loot stats on (dungeon, variation).
        /// Never persisted; removed at teardown.</summary>
        private static void RegisterRuntimeLootZone(RiftRun run, RiftConfig cfg)
        {
            var stats = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in cfg.LootStatBase)
                stats[kv.Key] = kv.Value;

            foreach (var kv in cfg.LootStatPerTier)
            {
                stats.TryGetValue(kv.Key, out var baseVal);
                stats[kv.Key] = baseVal + kv.Value * run.Tier;
            }

            var area = new ControlledArea
            {
                Name = run.ZoneName,
                Landblocks = new HashSet<ushort> { run.DungeonLb },
                Variation = run.Variation,
                Enabled = true,
            };

            var variant = area.Profile.Minion;
            foreach (var kv in stats)
            {
                if (!ZoneStat.All.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
                {
                    log.Warn($"[RIFT] Ignoring unknown loot stat '{kv.Key}' in rift config.");
                    continue;
                }
                variant.Stats[kv.Key] = new StatCurve { Base = kv.Value };
            }

            try
            {
                ZoneControlManager.RegisterRuntimeZone(area);
            }
            catch (Exception ex)
            {
                log.Error($"[RIFT] Run {run.RunId}: failed to register runtime zone. Loot scaling disabled for this run. {ex}");
            }
        }

        #endregion

        #region helpers

        private static int RemainingSeconds(RiftRun run)
        {
            return (int)Math.Max(0, run.EndTime - Time.GetUnixTime());
        }

        public static string FormatTime(int seconds)
        {
            var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}" : $"{t.Minutes}:{t.Seconds:D2}";
        }

        private static void TeleportPlayer(Player player, Position dest)
        {
            // Do NOT pre-flip player.Location.Variation here (the old Portal.ActOnUse mirror). Teleport →
            // UpdatePosition detects the landblock transfer as (cell changed || Location.Variation !=
            // dest.Variation); pre-flipping erases the variation delta, so a same-landblock hop (opening a
            // rift while standing inside the pool dungeon — the normal case) skips RelocateObjectForPhysics:
            // the player never leaves the source instance and the rift instance never loads (test-2/3 bug).
            // @televariant proves the plain path handles same-landblock variation hops correctly.
            WorldManager.ThreadSafeTeleport(player, dest);
        }

        private static void Message(RiftRun run, string msg)
        {
            var player = PlayerManager.GetOnlinePlayer(run.OwnerGuid);
            player?.Session?.Network.EnqueueSend(new GameMessageSystemChat($"[Rift] {msg}", ChatMessageType.Broadcast));
        }

        /// <summary>Player-facing status block for /rift.</summary>
        public static string BuildStatus(Player player)
        {
            var run = GetRunByOwner(player.Guid.Full);
            var best = player.GetProperty((PropertyInt)PropHighestClearedTier) ?? 0;

            if (run == null)
                return $"You have no open rift.{(best > 0 ? $" Highest tier cleared: {best}." : "")}";

            var state = run.State switch
            {
                RiftRunState.Active => $"{run.ProgressPercent}% progress",
                RiftRunState.GuardianUp => "the guardian awaits",
                RiftRunState.Cleared => "CLEARED",
                RiftRunState.Failed => "failed",
                _ => run.State.ToString(),
            };

            return $"Rift Tier {run.Tier} ({run.DungeonName}): {state}, {FormatTime(RemainingSeconds(run))} remaining." +
                   (best > 0 ? $" Highest tier cleared: {best}." : "");
        }

        #endregion
    }
}
