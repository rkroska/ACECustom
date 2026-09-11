using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;

using ACE.Common;
using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity.Actions;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Coordinates boss-group generators (PropertyInt.BossGroupId): any number of generators, placed
    /// on any landblocks, share exactly ONE live spawn. The group has a single respawn clock — when
    /// the boss dies, one delay (the generator profile's Delay, unless overridden) passes, then ONE
    /// randomly-chosen candidate generator spawns the next boss ("combined" model, owner 2026-08-08).
    ///
    /// Generators poll via TryClaimSpawn from GeneratorProfile.Spawn; a denial simply drops that
    /// spawn attempt and the generator retries on its next cycle, so the candidate set is naturally
    /// limited to generators on LOADED landblocks. A boss destroyed without a Death notification
    /// (landblock unload) is caught by the weak-reference liveness check instead of wedging the group.
    ///
    /// RUNTIME OVERRIDES (rev 2, 2026-08-08): per-group Delay / Radius / Enabled, persisted in the
    /// shard store (config_properties_string key "bossgroup_data") and applied live — the plugin's
    /// hunt-window tuning path. Weenie values remain the defaults when no override is set.
    /// </summary>
    public static class BossGroupManager
    {
        private class Candidate
        {
            public DateTime LastSeen;
            public WeakReference<WorldObject> Gen;
        }

        private class GroupState
        {
            public WeakReference<WorldObject> AliveBoss;
            public uint SpawnerGenGuid;
            public DateTime LastDeath = DateTime.MinValue;
            public uint? ChosenGenGuid;
            public readonly Dictionary<uint, Candidate> Candidates = new Dictionary<uint, Candidate>();

            /// <summary>Respawn <see cref="ForceRespawn"/> pending: the NEXT death/vanish event zeroes
            /// the respawn clock instead of starting it.</summary>
            public bool PendingForceRespawn;

            // weenie defaults as last seen at claim time — the wire's "(weenie default N)" hint
            public float DefaultDelay;
            public float DefaultRadius;
        }

        /// <summary>Per-group runtime override, persisted in the shard store. Null field = no override
        /// (weenie value applies).</summary>
        public class GroupOverride
        {
            public float? Delay;
            public float? Radius;
            public bool Enabled = true;
        }

        private static readonly ConcurrentDictionary<int, GroupState> groups = new ConcurrentDictionary<int, GroupState>();

        /// <summary>How long a polling generator stays a live election candidate; the status/wire readouts use the same window.</summary>
        private static readonly TimeSpan CandidateFreshWindow = TimeSpan.FromMinutes(15);

        // ── overrides store ──────────────────────────────────────────────────
        private const string StoreKey = "bossgroup_data";
        private static readonly object storeLock = new object();
        private static Dictionary<int, GroupOverride> overrides;   // lazy-loaded

        private static void EnsureLoaded()
        {
            if (overrides != null) return;
            lock (storeLock)
            {
                if (overrides != null) return;
                string json = null;
                if (DatabaseManager.ShardConfig.StringExists(StoreKey))
                    json = DatabaseManager.ShardConfig.GetString(StoreKey)?.Value;
                overrides = string.IsNullOrWhiteSpace(json)
                    ? new Dictionary<int, GroupOverride>()
                    : (JsonConvert.DeserializeObject<Dictionary<int, GroupOverride>>(json) ?? new Dictionary<int, GroupOverride>());
            }
        }

        private static void SaveOverrides()
        {
            lock (storeLock)
            {
                var json = JsonConvert.SerializeObject(overrides);
                if (DatabaseManager.ShardConfig.StringExists(StoreKey))
                    DatabaseManager.ShardConfig.SaveString(new ConfigPropertiesString { Key = StoreKey, Value = json, Description = "Boss group runtime overrides (JSON)" });
                else
                    DatabaseManager.ShardConfig.AddString(StoreKey, json, "Boss group runtime overrides (JSON)");
            }
        }

        private static GroupOverride GetOverrideOrNull(int groupId)
        {
            EnsureLoaded();
            lock (storeLock)
            {
                if (!overrides.TryGetValue(groupId, out var ov))
                    return null;
                // snapshot: callers read the fields outside the lock while MutateOverride writes them under it
                return new GroupOverride { Delay = ov.Delay, Radius = ov.Radius, Enabled = ov.Enabled };
            }
        }

        /// <summary>Mutate (creating if needed) a group's override and persist. Used by /bossgroup set etc.</summary>
        public static void MutateOverride(int groupId, Action<GroupOverride> mutate)
        {
            EnsureLoaded();
            lock (storeLock)
            {
                if (!overrides.TryGetValue(groupId, out var ov))
                    overrides[groupId] = ov = new GroupOverride();
                mutate(ov);
                // an all-default override row is just noise — drop it
                if (ov.Delay == null && ov.Radius == null && ov.Enabled)
                    overrides.Remove(groupId);
            }
            SaveOverrides();
        }

        /// <summary>Radius override for a boss-group generator, read by Spawn_Scatter. Null = use the
        /// weenie's GeneratorRadius.</summary>
        public static float? GetRadiusOverride(int groupId) => GetOverrideOrNull(groupId)?.Radius;

        // ── spawn coordination ───────────────────────────────────────────────

        /// <summary>
        /// Called by a boss-group generator that wants to spawn. Returns TRUE only for the group's
        /// elected generator, and only when the group is enabled, no group boss is alive, and the
        /// respawn delay (override ?? profile) has passed.
        /// </summary>
        public static bool TryClaimSpawn(int groupId, WorldObject generator, float delaySeconds)
        {
            var ov = GetOverrideOrNull(groupId);
            var state = groups.GetOrAdd(groupId, _ => new GroupState());

            lock (state)
            {
                var now = DateTime.UtcNow;
                if (state.Candidates.TryGetValue(generator.Guid.Full, out var cand))
                {
                    cand.LastSeen = now;
                    cand.Gen = new WeakReference<WorldObject>(generator);
                }
                else
                    state.Candidates[generator.Guid.Full] = new Candidate { LastSeen = now, Gen = new WeakReference<WorldObject>(generator) };
                state.DefaultDelay = delaySeconds;
                state.DefaultRadius = (float)(generator.GetProperty(PropertyFloat.GeneratorRadius) ?? 0f);

                if (ov != null && !ov.Enabled)
                    return false;

                if (state.AliveBoss != null)
                {
                    if (state.AliveBoss.TryGetTarget(out var boss) && !boss.IsDestroyed)
                        return false;

                    // boss vanished without a Death notification — treat as a death so the delay
                    // applies (unless a force-respawn is pending)
                    state.AliveBoss = null;
                    state.LastDeath = state.PendingForceRespawn ? DateTime.MinValue : now;
                    state.PendingForceRespawn = false;
                }

                var effDelay = ov?.Delay ?? delaySeconds;
                if (effDelay < 0)
                    effDelay = 0;   // weenie data: DateTime.MinValue.AddSeconds(negative) would throw out of Spawn()
                if (now < state.LastDeath.AddSeconds(effDelay))
                    return false;

                // election: sticky random winner among recently-polling generators; re-pick if the
                // winner went stale (its landblock unloaded)
                var freshWindow = TimeSpan.FromSeconds(Math.Max(600, effDelay * 3));

                if (state.ChosenGenGuid == null ||
                    !state.Candidates.TryGetValue(state.ChosenGenGuid.Value, out var chosen) ||
                    now - chosen.LastSeen > freshWindow)
                {
                    foreach (var stale in state.Candidates.Where(c => now - c.Value.LastSeen > freshWindow).Select(c => c.Key).ToList())
                        state.Candidates.Remove(stale);

                    if (state.Candidates.Count == 0)
                        return false;

                    var fresh = state.Candidates.Keys.ToList();
                    state.ChosenGenGuid = fresh[ThreadSafeRandom.Next(0, fresh.Count - 1)];
                }

                return generator.Guid.Full == state.ChosenGenGuid.Value;
            }
        }

        /// <summary>
        /// Called after the elected generator successfully spawns the boss.
        /// </summary>
        public static void OnBossSpawned(int groupId, WorldObject generator, WorldObject boss)
        {
            var state = groups.GetOrAdd(groupId, _ => new GroupState());

            lock (state)
            {
                state.AliveBoss = new WeakReference<WorldObject>(boss);
                state.SpawnerGenGuid = generator.Guid.Full;
                state.ChosenGenGuid = null;
                state.PendingForceRespawn = false;
            }
        }

        /// <summary>
        /// Called when a generator is notified its boss-group spawn died (or was removed).
        /// Starts the group's respawn clock — or zeroes it if a force-respawn is pending.
        /// </summary>
        public static void OnBossRemoved(int groupId, uint bossGuid)
        {
            if (!groups.TryGetValue(groupId, out var state))
                return;

            lock (state)
            {
                if (state.AliveBoss != null && state.AliveBoss.TryGetTarget(out var boss) && boss.Guid.Full != bossGuid)
                    return; // not the boss this group is tracking

                state.AliveBoss = null;
                var force = state.PendingForceRespawn;
                state.LastDeath = force ? DateTime.MinValue : DateTime.UtcNow;
                state.PendingForceRespawn = false;
                if (force)
                    KickSpawnLocked(state);
            }
        }

        /// <summary>
        /// Immediately triggers the group's next spawn instead of waiting for a generator's regen tick
        /// (/bossgroup respawn felt like "nothing happened" - gens only poll every RegenerationInterval,
        /// and the profile that just lost its boss times itself out for a full Delay on top of that).
        /// Elects a live candidate generator, then - on its own action queue, so all generator state is
        /// touched on the right thread - clears the profile timeouts and runs a generate pass.
        /// Caller must hold lock(state). Returns false when no live candidate is reachable.
        /// </summary>
        private static bool KickSpawnLocked(GroupState state)
        {
            var now = DateTime.UtcNow;
            var freshWindow = CandidateFreshWindow;
            var live = new List<(uint Guid, WorldObject Gen)>();
            foreach (var kvp in state.Candidates)
            {
                if (now - kvp.Value.LastSeen > freshWindow)
                    continue;
                if (kvp.Value.Gen != null && kvp.Value.Gen.TryGetTarget(out var gen) &&
                    !gen.IsDestroyed && gen.CurrentLandblock != null)
                    live.Add((kvp.Key, gen));
            }
            if (live.Count == 0)
                return false;

            var pick = live[ThreadSafeRandom.Next(0, live.Count - 1)];
            state.ChosenGenGuid = pick.Guid;

            var target = pick.Gen;
            var chain = new ActionChain();
            chain.AddAction(target, ActionType.BossGroupManager_KickSpawn, () =>
            {
                // the action runs later on the target's queue; the generator can be reset or unloaded in between
                if (target.IsDestroyed || target.GeneratorProfiles == null)
                    return;
                foreach (var profile in target.GeneratorProfiles)
                    profile.NextAvailable = DateTime.UtcNow;
                target.Generator_Generate();
            });
            chain.EnqueueChain();
            return true;
        }

        /// <summary>The group's live boss, if any (for /bossgroup respawn to smite).</summary>
        public static WorldObject GetAliveBoss(int groupId)
        {
            if (!groups.TryGetValue(groupId, out var state))
                return null;
            lock (state)
            {
                if (state.AliveBoss != null && state.AliveBoss.TryGetTarget(out var boss) && !boss.IsDestroyed)
                    return boss;
                return null;
            }
        }

        /// <summary>
        /// /bossgroup respawn: zero the respawn clock, re-elect, and kick the spawn immediately. Call
        /// BEFORE smiting the live boss — the pending flag makes the incoming death event zero the
        /// clock and kick, instead of starting the delay. Returns TRUE when a spawn was kicked now
        /// (no live boss); FALSE means either a smite is pending or no live candidate was reachable.
        /// </summary>
        public static bool ForceRespawn(int groupId)
        {
            var state = groups.GetOrAdd(groupId, _ => new GroupState());
            lock (state)
            {
                state.ChosenGenGuid = null;
                if (state.AliveBoss != null && state.AliveBoss.TryGetTarget(out var boss) && !boss.IsDestroyed)
                {
                    state.PendingForceRespawn = true;   // death lands async; the death event kicks
                    return false;
                }
                state.AliveBoss = null;
                state.LastDeath = DateTime.MinValue;
                return KickSpawnLocked(state);
            }
        }

        // ── status ───────────────────────────────────────────────────────────

        /// <summary>Human-readable status for every boss group seen this session (/bossgroup status).</summary>
        public static List<string> GetStatusLines()
        {
            var lines = new List<string>();

            if (groups.IsEmpty)
            {
                lines.Add("No boss groups active yet - no BossGroupId generator has attempted a spawn this session.");
                return lines;
            }

            foreach (var kvp in groups.OrderBy(k => k.Key))
            {
                var state = kvp.Value;
                var ov = GetOverrideOrNull(kvp.Key);

                lock (state)
                {
                    var now = DateTime.UtcNow;
                    var freshWindow = CandidateFreshWindow;
                    var candidates = state.Candidates.Count(c => now - c.Value.LastSeen <= freshWindow);
                    var effDelay = ov?.Delay ?? state.DefaultDelay;
                    var knobs = $"delay {effDelay:0}s{(ov?.Delay != null ? " (override)" : "")}" +
                                $", radius {(ov?.Radius ?? state.DefaultRadius):0}{(ov?.Radius != null ? " (override)" : "")}" +
                                (ov != null && !ov.Enabled ? ", DISABLED" : "");

                    if (ov != null && !ov.Enabled && GetAliveBossLocked(state) == null)
                    {
                        lines.Add($"Group {kvp.Key}: DISABLED - {candidates} candidate gen(s) polling ({knobs})");
                    }
                    else if (GetAliveBossLocked(state) is WorldObject boss)
                    {
                        lines.Add($"Group {kvp.Key}: ALIVE - {boss.Name} ({boss.WeenieClassId}) at {boss.Location?.ToString() ?? "unknown"} " +
                                  $"(spawned by gen 0x{state.SpawnerGenGuid:X8}, {candidates} gen(s) polling, {knobs})");
                    }
                    else
                    {
                        var since = state.LastDeath == DateTime.MinValue
                            ? "never spawned this session"
                            : $"died {(int)(now - state.LastDeath).TotalSeconds}s ago";
                        var chosen = state.ChosenGenGuid != null ? $", next spawn elected to gen 0x{state.ChosenGenGuid.Value:X8}" : "";
                        lines.Add($"Group {kvp.Key}: DEAD - {since}, {candidates} candidate gen(s) polling{chosen} ({knobs})");
                    }
                }
            }

            return lines;
        }

        private static WorldObject GetAliveBossLocked(GroupState state)
        {
            if (state.AliveBoss != null && state.AliveBoss.TryGetTarget(out var boss) && !boss.IsDestroyed)
                return boss;
            return null;
        }

        /// <summary>[[ZCB]] wire lines for the plugin (one per group):
        /// g=id~state~bossWcid~bossName~lbHex~x~y~z~secsSinceDeath~delay~radius~enabled~candidates~delayOv~radiusOv~defDelay~defRadius
        /// state: 0 dead, 1 alive, 2 disabled. secsSinceDeath -1 = never spawned this session.</summary>
        public static List<string> GetWireLines()
        {
            var lines = new List<string>();

            foreach (var kvp in groups.OrderBy(k => k.Key))
            {
                var state = kvp.Value;
                var ov = GetOverrideOrNull(kvp.Key);

                lock (state)
                {
                    var now = DateTime.UtcNow;
                    var freshWindow = CandidateFreshWindow;
                    var candidates = state.Candidates.Count(c => now - c.Value.LastSeen <= freshWindow);
                    var boss = GetAliveBossLocked(state);
                    var enabled = ov == null || ov.Enabled;

                    var st = !enabled ? 2 : (boss != null ? 1 : 0);
                    uint bw = 0; var bn = ""; var lb = ""; float x = 0, y = 0, z = 0;
                    if (boss != null)
                    {
                        bw = boss.WeenieClassId;
                        bn = (boss.Name ?? "").Replace('|', ' ').Replace('~', ' ').Replace('=', ' ');
                        if (boss.Location != null)
                        {
                            lb = boss.Location.LandblockId.Landblock.ToString("X4");
                            x = boss.Location.PositionX; y = boss.Location.PositionY; z = boss.Location.PositionZ;
                        }
                    }
                    var since = state.LastDeath == DateTime.MinValue ? -1 : (int)(now - state.LastDeath).TotalSeconds;
                    var effDelay = ov?.Delay ?? state.DefaultDelay;
                    var effRadius = ov?.Radius ?? state.DefaultRadius;

                    lines.Add($"[[ZCB]]g={kvp.Key}~{st}~{bw}~{bn}~{lb}~{x:0.#}~{y:0.#}~{z:0.#}~{since}~{effDelay:0.#}~{effRadius:0.#}~{(enabled ? 1 : 0)}~{candidates}" +
                              $"~{(ov?.Delay != null ? 1 : 0)}~{(ov?.Radius != null ? 1 : 0)}~{state.DefaultDelay:0.#}~{state.DefaultRadius:0.#}");
                }
            }

            return lines;
        }
    }
}
