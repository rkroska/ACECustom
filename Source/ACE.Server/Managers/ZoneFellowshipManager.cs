using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using ACE.Common;
using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

using Newtonsoft.Json;

namespace ACE.Server.Managers
{
    /// <summary>
    /// AUTO FELLOWSHIP (owner 2026-11-05): "With /autofellow on, you automatically join a fellow in specific zones ... the server
    /// holds the fellow name and players with /autofellow on join it (first leaving their current fellow) when entering the zone."
    ///
    /// AUTO FELLOW AREAS - its own data, apart from Zone Control zones and Zone Share (owner: "we dont need them intertwined";
    /// "T10 or VoD, and really any group of landblocks, or dungeon, we should be able to use these features"). An area is a named
    /// set of landblocks and the layers it covers (none listed = every layer), kept in the shard config string
    /// <see cref="StoreKey"/>. Tou Tou T11-T15 = Tou Tou's landblocks on layers 11-15 ("all in one giant fellow"); a Vaulted
    /// Dungeon = its landblock on its layer. The most specific area (fewest landblocks) wins where two cover a spot.
    ///
    /// Each area holds one fellowship the server keeps, a normal Fellowship marked with ZoneKey ("af:<area>"):
    ///   - created by the first /autofellow player to arrive (its first leader); a leader who leaves passes it on at random
    ///     (Fellowship.AssignNewLeader); forgotten once empty; named by the area's FellowName, else the area's name;
    ///   - nobody disbands, dismisses, locks, opens or recruits into it (Player_Fellowship refusals; Disband = Quit);
    ///   - loot is never shared in it (strangers do not loot each other's kills);
    ///   - kill XP: fellows standing in the same area share in full, any layer, any landblock (Fellowship.GetDistanceScalar,
    ///     via Player.AutoFellowAreaKey) - "landblock sharing + tier sharing together";
    ///   - ANTI-LEECH (per area): "measure total fellow damage, and have a rolling % of that damage, where the leech would have
    ///     to contribute a set % to be eligible ... no lum / xp from fellow. Own kills is still fine" (DropLeeches);
    ///   - BARRED players (per area): "blacklist players from auto joining a fellow, per zone. If we find them leeching too
    ///     often" - never joined, out at once (the panel lists who was cut for leeching, with a Bar button).
    ///
    /// /autofellow is ON by default (PropertyBool.AutoFellowOff absent). Walking in: the player leaves any other fellowship (with
    /// a line saying so) and joins. Walking out: they stay for <see cref="LeaveGraceSeconds"/> (owner: 2 minutes - a portal hop,
    /// a recall, a death and run back) and then leave. Quit by hand: they stay out until they leave the area. /autofellow off:
    /// they leave at once and never join. Staff, cloaked characters, mules and Olthoi players are never in one.
    ///
    /// Runs from Player.Tick every <see cref="TickSeconds"/>; all joins, leaves and area edits go through one lock, the landblock
    /// index is published whole for lock-free reads, and Fellowship's member list is a ConcurrentDictionary, because an area
    /// spans many landblock threads.
    /// </summary>
    public static class ZoneFellowshipManager
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private const double TickSeconds = 2.0;
        private const double LeaveGraceSeconds = 120.0;   // owner 2026-11-05: "2 minutes is fine"

        private static readonly object _lock = new();

        /// <summary>Area key ("af:<area>") -> its fellowship.</summary>
        private static readonly Dictionary<string, Fellowship> _fellowships = new();

        public static bool IsOn(Player player) => !(player.GetProperty(PropertyBool.AutoFellowOff) ?? false);

        // ------------------------------------------------------------------------------------------------------------------
        // Areas
        // ------------------------------------------------------------------------------------------------------------------

        public class Area
        {
            public string Name;
            public bool Enabled;
            public List<ushort> Landblocks = new();
            public List<int> Layers = new();                            // empty = every layer
            public string FellowName;                                   // null = the area's name
            public bool AntiLeech;
            public bool Dungeon;                                        // a Vaulted Dungeon copy's area (the panel lists dungeons first)
            public double LeechPercent = DefaultLeechPercent;           // a member's damage must reach this % of an even split (total / members)
            [JsonProperty] public bool EvenSplit;                       // saved true since 2026-11-06: LeechPercent is of an even split (a row without it is older)
            public int LeechWindowMinutes = DefaultLeechWindowMinutes;  // over this rolling window
            public Dictionary<uint, string> Barred = new();             // character guid -> name

            [JsonIgnore] public string Key => KeyOf(Name);
            // owner 2026-11-05: "Just use the Zone name as the default. Dont allow a blank fellowship name" - a stored name that is
            // blank or no longer valid falls back to the area's own name
            [JsonIgnore] public string ShownName => string.IsNullOrWhiteSpace(FellowName) || !ValidName(FellowName.Trim()) ? BaseName : FellowName.Trim();
            // owner 2026-11-05: "Remove the v3 etc from the Fellowship" - the default fellowship name drops a last word like v3,
            // T11 or T11-T15 (the area's variant says it); the area's own Name (its key) keeps it
            [JsonIgnore] public string BaseName
            {
                get
                {
                    var i = Name.LastIndexOf(' ');
                    if (i <= 0) return Name;
                    var last = Name[(i + 1)..];
                    static bool Word(string t, char p) => t.Length > 1 && char.ToLowerInvariant(t[0]) == p && t[1..].All(char.IsDigit);
                    var dash = last.Split('-');
                    var tier = dash.Length == 2 ? Word(dash[0], 't') && Word(dash[1], 't') : Word(last, 't');
                    return Word(last, 'v') || tier ? Name[..i].TrimEnd() : Name;
                }
            }

            public Area Clone() => new Area
            {
                Name = Name, Enabled = Enabled, Landblocks = new List<ushort>(Landblocks), Layers = new List<int>(Layers), FellowName = FellowName,
                AntiLeech = AntiLeech, Dungeon = Dungeon, LeechPercent = LeechPercent, EvenSplit = EvenSplit, LeechWindowMinutes = LeechWindowMinutes, Barred = new Dictionary<uint, string>(Barred),
            };
        }

        // owner 2026-11-06 ("Share of an even split"): the bar is a percent of what an even split of the fellowship's damage would give a
        // member (total / members), not of the total - a fixed share of the total cut everyone past ~50 members, and T11 members beside T15
        public const double DefaultLeechPercent = 10.0;
        private const double OldDefaultLeechPercent = 2.0;   // the % of the TOTAL before 2026-11-06 - read as the new default
        public const int DefaultLeechWindowMinutes = 10;
        private const string StoreKey = "autofellow_areas";

        public static string KeyOf(string areaName) => "af:" + (areaName ?? "").Trim().ToLowerInvariant();

        private static Dictionary<string, Area> _areas;                 // lower-case name -> area; guarded by _lock
        private static volatile Dictionary<ushort, Area[]> _byLandblock = new();   // published copies, read lock-free
        private static volatile Dictionary<string, Area> _byKey = new();           // the same copies by key, read lock-free

        // review 2026-11-06: a failed first read used to be cached as "no areas", and the next panel list (EnsureDungeonAreas) saved
        // that over every stored area. Now it is read again at most every 30 s, and nothing is saved until a read works
        private static bool _loadFailed;
        private static DateTime _retryLoadAt;

        /// <summary>True while the stored areas could not be read: edits are refused so nothing is saved over them.</summary>
        public static bool LoadFailed { get { lock (_lock) { Areas(); return _loadFailed; } } }

        private static Dictionary<string, Area> Areas()
        {
            if (_areas != null && !(_loadFailed && DateTime.UtcNow >= _retryLoadAt)) return _areas;
            try
            {
                var json = DatabaseManager.ShardConfig.GetString(StoreKey)?.Value;
                var list = string.IsNullOrWhiteSpace(json) ? new List<Area>() : JsonConvert.DeserializeObject<List<Area>>(json) ?? new List<Area>();
                foreach (var a in list.Where(a => a != null))
                {
                    if (!a.EvenSplit && a.LeechPercent == OldDefaultLeechPercent) a.LeechPercent = DefaultLeechPercent;   // the old default, never tuned
                    a.EvenSplit = true;   // read once in the old meaning; saved with the marker from now on
                    a.Landblocks ??= new List<ushort>();   // a hand-edited row with a null list threw in Publish / Tick
                    a.Layers ??= new List<int>();
                    a.Barred ??= new Dictionary<uint, string>();
                }
                _areas = list.Where(a => a != null && !string.IsNullOrWhiteSpace(a.Name))
                    .GroupBy(a => a.Name.Trim().ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First());
                if (_loadFailed) log.Warn("[AutoFellow] areas read on a retry - edits are saved again.");
                _loadFailed = false;
            }
            catch (Exception ex)
            {
                log.Error($"[AutoFellow] areas could not be read - none are active and no edit is saved until they are (retry in 30 s): {ex.GetBaseException().Message}");
                _areas = new Dictionary<string, Area>();
                _loadFailed = true;
                _retryLoadAt = DateTime.UtcNow.AddSeconds(30);
            }
            Publish();
            return _areas;
        }

        /// <summary>Rebuild the landblock and key indexes from the areas (call under _lock). Both hold the same published copies, which
        /// are never changed after this - an edit publishes new ones.</summary>
        private static void Publish()
        {
            var index = new Dictionary<ushort, List<Area>>();
            var byKey = new Dictionary<string, Area>();
            foreach (var a in _areas.Values)
            {
                var copy = a.Clone();
                byKey[copy.Key] = copy;
                foreach (var lb in copy.Landblocks.Distinct())
                {
                    if (!index.TryGetValue(lb, out var l)) index[lb] = l = new List<Area>();
                    l.Add(copy);
                }
            }
            _byLandblock = index.ToDictionary(kv => kv.Key, kv => kv.Value.OrderBy(a => a.Landblocks.Count).ToArray());
            _byKey = byKey;
        }

        private static readonly object _saveLock = new object();
        private static long _saveVersion;

        /// <summary>Publish the index and write the areas (call under _lock). The JSON is taken here; the DB write runs on a worker
        /// outside _lock (review 2026-11-06: every area player's tick and every area kill waited on it) - the newest one wins.</summary>
        private static void Save()
        {
            Publish();
            if (_loadFailed) { log.Error("[AutoFellow] not saved: the stored areas could not be read."); return; }
            var json = JsonConvert.SerializeObject(_areas.Values.OrderBy(a => a.Name).ToList());
            var version = System.Threading.Interlocked.Increment(ref _saveVersion);
            System.Threading.Tasks.Task.Run(() =>
            {
                lock (_saveLock)
                {
                    if (version != System.Threading.Interlocked.Read(ref _saveVersion)) return;   // a newer save is on its way
                    try
                    {
                        const string desc = "Auto Fellow areas: landblocks, layers, fellowship name, anti-leech, barred players (JSON)";
                        if (DatabaseManager.ShardConfig.StringExists(StoreKey))
                            DatabaseManager.ShardConfig.SaveString(new ConfigPropertiesString { Key = StoreKey, Value = json, Description = desc });
                        else
                            DatabaseManager.ShardConfig.AddString(StoreKey, json, desc);
                    }
                    catch (Exception ex) { log.Error($"[AutoFellow] areas not saved: {ex.GetBaseException().Message}"); }
                }
            });
        }

        /// <summary>Copies of every area, by name.</summary>
        public static List<Area> ListAreas()
        {
            lock (_lock)
                return Areas().Values.OrderBy(a => a.Name).Select(a => a.Clone()).ToList();
        }

        /// <summary>The published copy of the area with this key ("af:<name>"), or null. READ ONLY - it is shared. Lock-free (CodeRabbit
        /// #596, 2026-11-07): every area kill (DropLeeches) and every member's 2 s tick (IsBarred) used to queue on _lock and clone.</summary>
        public static Area AreaByKey(string key)
        {
            if (key == null) return null;
            if (_areas == null || (_loadFailed && DateTime.UtcNow >= _retryLoadAt)) lock (_lock) Areas();
            return _byKey.TryGetValue(key, out var a) ? a : null;
        }

        /// <summary>The enabled area covering a spot (landblock + layer), most specific first; null when none.</summary>
        private static Area AreaAt(ushort landblock, int layer)
        {
            if (_areas == null || (_loadFailed && DateTime.UtcNow >= _retryLoadAt)) lock (_lock) Areas();   // a failed read: the tick retries too
            if (!_byLandblock.TryGetValue(landblock, out var list)) return null;
            foreach (var a in list)
                if (a.Enabled && (a.Layers.Count == 0 || a.Layers.Contains(layer)))
                    return a;
            return null;
        }

        /// <summary>
        /// The auto fellowship area a player stands in - its key and the fellowship's name - or null. Staff, cloaked characters,
        /// mules and Olthoi players are never in one.
        /// </summary>
        public static (string Key, string Name)? AreaFor(Player player)
        {
            if (player == null || player.IsMule || player.IsOlthoiPlayer)
                return null;

            var location = player.Location;   // read once: another thread may move the player while we look
            if (location == null)
                return null;

            var area = AreaAt((ushort)location.Landblock, location.Variation ?? 0);
            if (area == null)
                return null;

            if (RoomAssignManager.IsStaff(player) || player.CloakStatus == CloakStatus.On || player.CloakStatus == CloakStatus.Ghost)
                return null;
            if (!RoomAssignManager.TestAdminCounts && (player.Session?.AccessLevel ?? AccessLevel.Player) >= AccessLevel.Sentinel)
                return null;

            return (area.Key, area.ShownName);
        }

        public static bool IsBarred(string key, uint guid) => AreaByKey(key)?.Barred.ContainsKey(guid) ?? false;

        // ------------------------------------------------------------------------------------------------------------------
        // The tick: join / leave / rename
        // ------------------------------------------------------------------------------------------------------------------

        public static void Tick(Player player, double now)
        {
            if (player == null || now < player.ZoneFellowNextTick)
                return;
            player.ZoneFellowNextTick = now + TickSeconds;

            if (!_builtInsMade && DateTime.UtcNow >= _builtInsNextTry)
                EnsureBuiltIns();

            // a portal or a death keeps the fellowship as it is - the next look decides. In portal space they are in no area for the
            // share rule (review 2026-11-06: a member who recalled out kept full shares while the old key stood)
            if (player.Teleporting)
                player.AutoFellowAreaKey = null;
            if (player.Teleporting || player.IsLoggingOut || player.IsDead || player.Session == null)
                return;

            var area = AreaFor(player);
            player.AutoFellowAreaKey = area?.Key;   // the cross-layer XP rule reads it, whatever this player's own setting

            string key = null, name = null;
            if (area != null && IsOn(player))
                (key, name) = area.Value;

            // barred from this area's fellowship: out at once if in it, never joined, told once per area
            if (key != null && IsBarred(key, player.Guid.Full))
            {
                if (player.Fellowship?.ZoneKey == key)
                    Leave(player);
                if (player.ZoneFellowBarredKey != key)
                {
                    player.ZoneFellowBarredKey = key;
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You are blacklisted from the {name} fellowship here.", ChatMessageType.Broadcast));
                }
                key = null;
            }

            // quit by hand: stays out until they leave that area
            if (player.ZoneFellowSkipKey != null && player.ZoneFellowSkipKey != key)
                player.ZoneFellowSkipKey = null;
            if (key != null && key == player.ZoneFellowSkipKey)
                key = null;

            var current = player.Fellowship;

            if (current?.ZoneKey != null)
            {
                if (current.ZoneKey == key)
                {
                    player.ZoneFellowOutsideSince = 0;
                    // renamed live: the first member to look renames it for everyone
                    if (!string.IsNullOrWhiteSpace(name) && current.FellowshipName != name)
                        Rename(current, name);
                    return;
                }

                // walked out (still /autofellow on): a grace period first
                if (key == null && IsOn(player) && !(area != null && IsBarred(area.Value.Key, player.Guid.Full)))
                {
                    if (player.ZoneFellowOutsideSince <= 0)
                    {
                        player.ZoneFellowOutsideSince = now;
                        return;
                    }
                    if (now - player.ZoneFellowOutsideSince < LeaveGraceSeconds)
                        return;
                }

                Leave(player);
                if (key == null)
                    return;
            }

            if (key == null || string.IsNullOrWhiteSpace(name))
                return;

            // full: a quick look first (told once); Join checks again under the lock before they leave their own fellowship
            if (CountIn(key) >= Fellowship.MaxFellows)
            {
                if (player.ZoneFellowFullKey != key)
                {
                    player.ZoneFellowFullKey = key;
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The {name} fellowship is full ({Fellowship.MaxFellows} members).", ChatMessageType.Broadcast));
                }
                return;
            }
            player.ZoneFellowFullKey = null;

            Join(player, key, name);
        }

        private static void Join(Player player, string key, string name)
        {
            lock (_lock)
            {
                if (player.Fellowship?.ZoneKey != null)
                    return;

                _fellowships.TryGetValue(key, out var fellowship);
                var live = fellowship != null && !fellowship.FellowshipMembers.IsEmpty;
                // full (fellowship_max_size, live): they stay out - and keep the fellowship they had - told once per area. Checked here,
                // under the lock, BEFORE their own fellowship is left (review 2026-11-06: left it, then found this one full - in neither)
                if (live && fellowship.FellowshipMembers.Count >= Fellowship.MaxFellows)
                {
                    if (player.ZoneFellowFullKey != key)
                    {
                        player.ZoneFellowFullKey = key;
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The {name} fellowship is full ({Fellowship.MaxFellows} members).", ChatMessageType.Broadcast));
                    }
                    return;
                }

                // a LOCKED fellowship (a quest group) is never left by itself (owner 2026-11-06, "Stay in locked group"): told once per area
                if (player.Fellowship != null && player.Fellowship.IsLocked)
                {
                    if (player.ZoneFellowLockedKey != key)
                    {
                        player.ZoneFellowLockedKey = key;
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your fellowship is locked, so you stay in it. Leave it to join the {name} fellowship here.", ChatMessageType.Broadcast));
                    }
                    return;
                }
                player.ZoneFellowLockedKey = null;

                // in a fellowship of their own: leave it first (owner: "first leaving their current fellow")
                if (player.Fellowship != null)
                {
                    var old = player.Fellowship.FellowshipName;
                    player.Fellowship.QuitFellowship(player, false);
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You left the {old} fellowship to join the {name} fellowship. Type /autofellow off to stay out of it.", ChatMessageType.Broadcast));
                    if (player.Fellowship != null)
                        return;
                }

                if (live)
                {
                    // the leader left on a path outside this lock (logout, /fship leave, enlightenment) while the list still held others -
                    // nobody would ever lead it again (review 2026-11-06): the joiner leads
                    var leaderless = !fellowship.FellowshipMembers.ContainsKey(fellowship.FellowshipLeaderGuid);
                    if (leaderless)
                        fellowship.FellowshipLeaderGuid = player.Guid.Full;
                    fellowship.AddZoneMember(player);
                    if (leaderless)
                        foreach (var m in fellowship.GetFellowshipMembers().Values)
                            if (m != player && m.Fellowship == fellowship) m.Session?.Network.EnqueueSend(new GameEventFellowshipFullUpdate(m.Session));
                }
                else
                {
                    fellowship = new Fellowship(player, name, true) { ZoneKey = key, ShareLoot = false, Open = false };
                    _fellowships[key] = fellowship;
                    player.Fellowship = fellowship;
                    player.Session.Network.EnqueueSend(new GameEventFellowshipFullUpdate(player.Session));
                    player.Session.Network.EnqueueSend(new GameEventFellowshipFellowUpdateDone(player.Session));
                }
            }

            player.ZoneFellowOutsideSince = 0;
            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You joined the {name} fellowship with everyone here. Type /autofellow off to leave it.", ChatMessageType.Broadcast));
        }

        /// <summary>Takes the player out of their zone fellowship, if they are in one.</summary>
        public static void Leave(Player player)
        {
            string name;
            lock (_lock)
            {
                var fellowship = player.Fellowship;
                if (fellowship?.ZoneKey == null)
                    return;

                name = fellowship.FellowshipName;
                fellowship.QuitFellowship(player, false);
                player.Fellowship = null;

                if (fellowship.FellowshipMembers.IsEmpty && _fellowships.TryGetValue(fellowship.ZoneKey, out var held) && held == fellowship)
                    _fellowships.Remove(fellowship.ZoneKey);
            }

            player.ZoneFellowOutsideSince = 0;
            player.Session?.Network.EnqueueSend(new GameMessageSystemChat($"You left the {name} fellowship.", ChatMessageType.Broadcast));
        }

        /// <summary>A live rename: the fellowship takes the new name and every member's panel gets the whole list again once.</summary>
        private static void Rename(Fellowship fellowship, string name)
        {
            lock (_lock)
            {
                if (fellowship.FellowshipName == name)
                    return;
                fellowship.FellowshipName = name;
            }

            foreach (var member in fellowship.GetFellowshipMembers().Values)
            {
                if (member.Fellowship == fellowship) member.Session?.Network.EnqueueSend(new GameEventFellowshipFullUpdate(member.Session));   // not one who just left
                member.Session?.Network.EnqueueSend(new GameMessageSystemChat($"Your area fellowship is now called {name}.", ChatMessageType.Broadcast));
            }
        }

        /// <summary>How many are in an area's fellowship now (0 = none held).</summary>
        public static int CountIn(string key)
        {
            lock (_lock)
                return _fellowships.TryGetValue(key, out var f) ? f.FellowshipMembers.Count : 0;
        }

        // ------------------------------------------------------------------------------------------------------------------
        // Anti-leech
        // ------------------------------------------------------------------------------------------------------------------

        private const int BucketSeconds = 20;
        public const int MaxWindowMinutes = 30;
        public const int Buckets = MaxWindowMinutes * 60 / BucketSeconds;   // 90 x 20 s

        /// <summary>Area key -> character guid -> (name, times cut from a share, last time): who leeches, for the panel.</summary>
        private static readonly Dictionary<string, Dictionary<uint, (string Name, int Cuts, double Last)>> _cuts = new();

        /// <summary>
        /// Damage a player (or their pet) did to a monster: counted toward their share of their area fellowship's damage. Only damage
        /// done INSIDE that fellowship's own area counts (CodeRabbit #596, owner 2026-11-07 "Fix both"): not damage elsewhere, not in
        /// the 2-minute leave grace, not in another area. The buckets count for one area at a time - damage in a different area
        /// starts them over, so damage done in area A never counts in area B. Called from DamageHistory.Add (melee, missile,
        /// spells, DoT ticks).
        /// </summary>
        public static void RecordDamage(WorldObject attacker, uint amount)
        {
            var player = attacker as Player ?? (attacker as CombatPet)?.P_PetOwner;
            var key = player?.Fellowship?.ZoneKey;
            if (key == null || amount == 0 || player.AutoFellowAreaKey != key)   // the area key = the area they stand in (2 s tick)
                return;
            var slot = (long)(Time.GetUnixTime() / BucketSeconds);
            lock (player.LeechLock)
            {
                if (player.LeechBucketKey != key)
                {
                    Array.Clear(player.LeechBucketSlot);
                    Array.Clear(player.LeechBucketDamage);
                    player.LeechBucketKey = key;
                }
                var i = (int)(slot % Buckets);
                if (player.LeechBucketSlot[i] != slot) { player.LeechBucketSlot[i] = slot; player.LeechBucketDamage[i] = 0; }
                player.LeechBucketDamage[i] += amount;
            }
        }

        /// <summary>A player's damage over the last <paramref name="minutes"/> (rolling, 20 s steps) inside the area <paramref name="key"/>
        /// (null = the area the buckets count for now); 0 when the buckets count for another area.</summary>
        public static double RollingDamage(Player player, int minutes, string key = null)
        {
            var now = (long)(Time.GetUnixTime() / BucketSeconds);
            var oldest = now - Math.Clamp(minutes, 1, MaxWindowMinutes) * 60 / BucketSeconds + 1;
            double sum = 0;
            lock (player.LeechLock)
            {
                if (key != null && player.LeechBucketKey != key)
                    return 0;
                for (var i = 0; i < Buckets; i++)
                    if (player.LeechBucketSlot[i] >= oldest && player.LeechBucketSlot[i] <= now)
                        sum += player.LeechBucketDamage[i];
            }
            return sum;
        }

        /// <summary>
        /// Anti-leech, at a kill's share: in an area fellowship whose area has it on, a member whose share of the fellowship's
        /// damage over the window is under the area's percent gets nothing from this kill - XP and luminance alike. The earner
        /// always keeps their own. Members cut are counted (the panel's leecher list) and told at most every 5 minutes.
        /// </summary>
        public static void DropLeeches(Fellowship fellowship, Player earner, List<(Player Member, double Scalar)> eligible)
        {
            if (fellowship?.ZoneKey == null || eligible.Count < 2)
                return;
            var area = AreaByKey(fellowship.ZoneKey);
            if (area == null || !area.AntiLeech)
                return;

            var damage = new Dictionary<uint, double>();
            double total = 0;
            foreach (var m in fellowship.GetFellowshipMembers().Values)
            {
                var d = RollingDamage(m, area.LeechWindowMinutes, fellowship.ZoneKey);
                damage[m.Guid.Full] = d;
                total += d;
            }
            if (total <= 0)
                return;

            var now = Time.GetUnixTime();
            var evenShare = total / damage.Count;   // what each member would have done in an even split
            eligible.RemoveAll(e =>
            {
                if (e.Member == earner) return false;
                var pct = (damage.TryGetValue(e.Member.Guid.Full, out var d) ? d : 0) * 100.0 / evenShare;
                if (pct >= area.LeechPercent) return false;
                NoteCut(fellowship.ZoneKey, e.Member, now, pct, area);
                return true;
            });
        }

        private static void NoteCut(string key, Player member, double now, double pct, Area area)
        {
            lock (_lock)
            {
                if (!_cuts.TryGetValue(key, out var byGuid)) _cuts[key] = byGuid = new();
                byGuid.TryGetValue(member.Guid.Full, out var c);
                byGuid[member.Guid.Full] = (member.Name, c.Cuts + 1, now);
            }
            if (now - member.LeechWarnedAt < 300)
                return;
            member.LeechWarnedAt = now;
            member.Session?.Network.EnqueueSend(new GameMessageSystemChat(
                $"You are doing {pct:0.0}% of an even share of your fellowship's damage here ({area.LeechPercent:0.#}% needed over {area.LeechWindowMinutes} min): no share of your fellows' kills until you do more. Your own kills still count.",
                ChatMessageType.Broadcast));
        }

        private static string Ago(double s) => s < 90 ? "just now" : s < 5400 ? $"{(int)(s / 60)} min ago" : $"{(int)(s / 3600)} h ago";

        /// <summary>The leechers of an area, most cut first.</summary>
        public static List<(uint Guid, string Name, int Cuts, double Ago)> Leechers(string key)
        {
            var now = Time.GetUnixTime();
            lock (_lock)
                return _cuts.TryGetValue(key, out var byGuid)
                    ? byGuid.OrderByDescending(kv => kv.Value.Cuts).Select(kv => (kv.Key, kv.Value.Name, kv.Value.Cuts, now - kv.Value.Last)).ToList()
                    : new List<(uint, string, int, double)>();
        }

        // ------------------------------------------------------------------------------------------------------------------
        // The panel's line and the admin commands (/zonecontrol afarea ...)
        // ------------------------------------------------------------------------------------------------------------------

        private static string Clean(string s)
        {
            var t = new StringBuilder(s ?? "");
            foreach (var c in new[] { '|', '~', ':', ';', ',', '=' }) t.Replace(c, ' ');
            return t.ToString().Trim();
        }

        /// <summary>
        /// [[ZCAF]]name~on~layers~fellowName~inFellowship~leech~pct~window~barred~leechers~landblocks~kind(d|o)|...
        /// layers = "11,12" ("" = every layer); barred = guid:name;...  leechers = guid:name:cuts:secondsAgo;...  landblocks = hex,hex,...
        /// </summary>
        public static string AreasWire()
        {
            var sb = new StringBuilder("[[ZCAF]]");
            var first = true;
            foreach (var a in ListAreas())
            {
                if (!first) sb.Append('|');
                first = false;
                sb.Append(Clean(a.Name)).Append('~').Append(a.Enabled ? 1 : 0).Append('~').Append(string.Join(",", a.Layers.OrderBy(v => v)))
                  .Append('~').Append(Clean(a.FellowName)).Append('~').Append(CountIn(a.Key))
                  .Append('~').Append(a.AntiLeech ? 1 : 0).Append('~').Append(a.LeechPercent.ToString("0.##", CultureInfo.InvariantCulture))
                  .Append('~').Append(a.LeechWindowMinutes)
                  .Append('~').Append(string.Join(";", a.Barred.Select(b => $"{b.Key}:{Clean(b.Value)}")))
                  .Append('~').Append(string.Join(";", Leechers(a.Key).Take(15).Select(l => $"{l.Guid}:{Clean(l.Name)}:{l.Cuts}:{(long)l.Ago}")))
                  .Append('~').Append(string.Join(",", a.Landblocks.OrderBy(l => l).Select(l => l.ToString("X4"))))
                  .Append('~').Append(a.Dungeon ? 'd' : 'o');   // appended 2026-11-05: d = Vaulted Dungeon, o = outdoors / other
            }
            return sb.ToString();
        }

        /// <summary>
        /// Every Vaulted Dungeon copy has its own area (owner 2026-11-05: "These areas should already be made"): any copy without
        /// one gets it here - "<dungeon> v<layer>", its landing landblock, its layer, OFF. Run on "afarea list" (the panel's
        /// page open / Refresh), so a new dungeon shows up by itself. An area the admin deleted is made again, off.
        /// </summary>
        private static void EnsureDungeonAreas()
        {
            List<(string Name, ushort Landblock, int Variation)> dungeons;
            try { dungeons = RoomAssignManager.KnownDungeonsForAutoFellow(); }
            catch (Exception ex) { log.Warn($"[AutoFellow] the Vaulted Dungeon list could not be read: {ex.GetBaseException().Message}"); return; }

            lock (_lock)
            {
                var areas = Areas();
                var added = 0;
                foreach (var d in dungeons)
                {
                    var clean = new StringBuilder();
                    foreach (var ch in d.Name ?? "") clean.Append(char.IsLetterOrDigit(ch) || ch == ' ' || ch == '\'' || ch == '-' ? ch : ' ');
                    // the base is cut, never the " v<layer>" (review 2026-11-06: a long name lost its suffix, and its copies collided)
                    var suffix = " v" + d.Variation;
                    var baseName = System.Text.RegularExpressions.Regex.Replace(clean.ToString(), " +", " ").Trim();
                    if (baseName.Length + suffix.Length > 40) baseName = baseName.Substring(0, 40 - suffix.Length).Trim();
                    var name = baseName + suffix;
                    if (!ValidName(name)) continue;
                    if (areas.TryGetValue(name.ToLowerInvariant(), out var had))
                    {
                        if (!had.Dungeon) { had.Dungeon = true; added++; }   // made before the flag existed (2026-11-05)
                        continue;
                    }
                    areas[name.ToLowerInvariant()] = new Area { EvenSplit = true, Name = name, Landblocks = new List<ushort> { d.Landblock }, Layers = new List<int> { d.Variation }, Dungeon = true };
                    added++;
                }
                added += AddMissingBuiltIns(areas);
                if (added > 0)
                {
                    Save();
                    log.Info($"[AutoFellow] {added} Vaulted Dungeon / built-in area(s) made.");
                }
            }
        }

        // the built-ins are made by the tick too, not only on a panel list (2026-11-07: a fresh shard had no Tou Tou area, so it
        // could not start on). Tried at most every 30 s until all three exist - Tou Tou waits for its Zone Control zone to load
        private static volatile bool _builtInsMade;
        private static DateTime _builtInsNextTry;

        private static void EnsureBuiltIns()
        {
            lock (_lock)
            {
                if (_builtInsMade || DateTime.UtcNow < _builtInsNextTry) return;
                _builtInsNextTry = DateTime.UtcNow.AddSeconds(30);
                var areas = Areas();
                if (_loadFailed) return;   // never save over areas that could not be read
                var added = AddMissingBuiltIns(areas);
                if (added > 0)
                {
                    Save();
                    log.Info($"[AutoFellow] {added} built-in area(s) made.");
                }
                _builtInsMade = BuiltInNames.All(n => areas.ContainsKey(n.ToLowerInvariant()));
            }
        }

        /// <summary>Make any built-in area that does not exist yet (call under _lock; the caller saves). Owner 2026-11-05 ("Just add
        /// them to the list uptop. Dont allow these 3 areas to be deleted"): Tou Tou, T10 and VoD always exist. Owner 2026-11-07
        /// ("The T11-T15 start on by default"): Tou Tou T11-T15 is made ON; T10 and VoD are made off. Only when an area is first
        /// made - one an admin switched off stays off.</summary>
        private static int AddMissingBuiltIns(Dictionary<string, Area> areas)
        {
            var added = 0;
            foreach (var (name, layers, src) in BuiltIns())
            {
                if (areas.ContainsKey(name.ToLowerInvariant()) || src == null) continue;
                var lbs = src.Distinct().ToList();
                if (lbs.Count == 0) continue;
                areas[name.ToLowerInvariant()] = new Area { EvenSplit = true, Name = name, Landblocks = lbs, Layers = layers, Enabled = name == BuiltInNames[0] };
                added++;
            }
            return added;
        }

        /// <summary>The areas that always exist and cannot be deleted (owner 2026-11-05): Tou Tou (its Zone Control zone, variants
        /// 11-15 - one fellowship across T11-T15), T10 (Thaelaryn Island) and VoD (Valley of Death).</summary>
        public static readonly string[] BuiltInNames = { "Tou Tou T11-T15", "T10", "VoD" };

        private static IEnumerable<(string Name, List<int> Layers, IEnumerable<ushort> Landblocks)> BuiltIns()
        {
            IEnumerable<ushort> touTou = null;
            try { var zone = ZoneControlManager.GetArea("Tou Tou"); if (zone != null) touTou = ZoneControlManager.SnapshotLandblocks(zone); } catch { /* zone not loaded yet: made on a later list */ }
            yield return (BuiltInNames[0], Enumerable.Range(11, 5).ToList(), touTou);
            yield return (BuiltInNames[1], new List<int>(), LandblockCollections.ThaelarynIslandLandblocks);
            yield return (BuiltInNames[2], new List<int>(), LandblockCollections.ValleyOfDeathLandblocks);
        }

        public static bool IsBuiltIn(string name) => BuiltInNames.Any(b => b.Equals(name?.Trim(), StringComparison.OrdinalIgnoreCase));

        private static bool ValidName(string v) => v.Length > 0 && v.Length <= 40 && System.Text.RegularExpressions.Regex.IsMatch(v, @"^[A-Za-z0-9 '\-]+$");

        public const string Usage = "afarea list | afarea create <name> | afarea delete <name> | afarea <name> show | on | off | addlb here|<hex> [<hex>...] | removelb here|<hex> | clearlbs"
            + " | import t10|vod|zone <ZC zone> | layers any|<n>[,<n>|<a>-<b>] | fellowname <name> (the area's name = default) | leech on|off | leech percent <n> | leech window <minutes>"
            + " | bar <character> (blacklist) | unbar <character> | clearleechers";

        /// <summary>
        /// /zonecontrol afarea ... - every Auto Fellow area verb. Returns the lines to show. Changes need Admin; list / show do not.
        /// </summary>
        public static List<string> Command(Player player, AccessLevel level, List<string> args)
        {
            var lines = new List<string>();
            if (args.Count == 0) { lines.Add("Usage: " + Usage); return lines; }
            var verb = args[0].ToLowerInvariant();

            if (verb == "list")
            {
                if (LoadFailed) { lines.Add("The Auto Fellow areas could not be read from the database - none are active, and nothing can be changed until they are (see the server log)."); return lines; }
                EnsureDungeonAreas();
                var all = ListAreas();
                lines.Add(all.Count == 0 ? "No Auto Fellow areas yet: /zonecontrol afarea create <name>" : $"Auto Fellow areas ({all.Count}):");
                foreach (var a in all)
                    lines.Add($"  {a.Name}: {(a.Enabled ? "ON" : "off")}, {a.Landblocks.Count} landblock(s), layers {(a.Layers.Count == 0 ? "any" : string.Join(",", a.Layers.OrderBy(v => v)))}, fellowship '{a.ShownName}' ({CountIn(a.Key)} in it)"
                        + (a.AntiLeech ? $", anti-leech {a.LeechPercent:0.#}% / {a.LeechWindowMinutes} min" : "") + (a.Barred.Count > 0 ? $", {a.Barred.Count} blacklisted" : ""));
                return lines;
            }

            const string NeedsAdmin = "Changing Auto Fellow areas needs Admin access.";
            // review 2026-11-06: refused while the stored areas cannot be read - an edit now would save over all of them
            if (LoadFailed) { lines.Add("The Auto Fellow areas could not be read from the database - none are active, and nothing can be shown or changed until they are (see the server log)."); return lines; }

            if (verb == "create" || verb == "delete")
            {
                if (level < AccessLevel.Admin) { lines.Add(NeedsAdmin); return lines; }
                var n = string.Join(" ", args.Skip(1)).Trim();
                if (!ValidName(n)) { lines.Add("An area name is up to 40 letters, digits, spaces, ' or -."); return lines; }
                lock (_lock)
                {
                    var areas = Areas();
                    var k = n.ToLowerInvariant();
                    if (verb == "create")
                    {
                        if (areas.ContainsKey(k)) { lines.Add($"There is already an area '{n}'."); return lines; }
                        if (k == "list" || k == "create" || k == "delete") { lines.Add($"'{n}' is a command word - pick another name."); return lines; }
                        areas[k] = new Area { EvenSplit = true, Name = n };
                        lines.Add($"Area '{n}' created (off, no landblocks yet): add landblocks, set its layers, then switch it on.");
                    }
                    else
                    {
                        if (IsBuiltIn(n)) { lines.Add($"'{n}' is a built-in area and cannot be deleted - switch it off instead."); return lines; }
                        if (!areas.Remove(k)) { lines.Add($"No area '{n}'."); return lines; }
                        lines.Add($"Area '{n}' deleted - its fellowship empties within 2 minutes.");
                    }
                    Save();
                }
                PlayerManager.BroadcastToAuditChannel(player, "Auto Fellow: " + (lines.Count > 0 ? lines[lines.Count - 1] : $"{verb} {n}"));
                return lines;
            }

            // afarea <name> <op> [args]: a multi-word area name is the LONGEST run of words naming an area (as Zone Control's
            // CollapseZoneNameTokens does for zones) - quotes are stripped and the words split again before they get here
            lock (_lock)
            {
                var areas = Areas();
                for (var k = args.Count; k >= 2; k--)
                {
                    var joined = string.Join(" ", args.Take(k));
                    if (!areas.ContainsKey(joined.Trim().ToLowerInvariant())) continue;
                    args = new List<string> { joined }.Concat(args.Skip(k)).ToList();
                    break;
                }
            }
            if (args.Count < 2) { lines.Add("Usage: " + Usage); return lines; }
            var name = args[0];
            var op = args[1].ToLowerInvariant();
            var rest = args.Skip(2).ToList();
            // on the resolved verb (review 2026-11-06: the raw second word was read before a multi-word name was joined)
            if (level < AccessLevel.Admin && op != "show") { lines.Add(NeedsAdmin); return lines; }

            lock (_lock)
            {
                if (!Areas().TryGetValue(name.Trim().ToLowerInvariant(), out var a)) { lines.Add($"No area '{name}' (/zonecontrol afarea create {name})."); return lines; }

                switch (op)
                {
                    case "show":
                        lines.Add($"{a.Name}: {(a.Enabled ? "ON" : "off")}, layers {(a.Layers.Count == 0 ? "any" : string.Join(",", a.Layers.OrderBy(v => v)))}, fellowship '{a.ShownName}' ({CountIn(a.Key)} in it)");
                        lines.Add($"  landblocks ({a.Landblocks.Count}): {string.Join(" ", a.Landblocks.OrderBy(l => l).Select(l => l.ToString("X4")))}");
                        lines.Add($"  anti-leech {(a.AntiLeech ? "ON" : "off")}: {a.LeechPercent:0.#}% of an even share of the fellowship's damage over {a.LeechWindowMinutes} min");
                        lines.Add($"  blacklisted: {(a.Barred.Count == 0 ? "nobody" : string.Join(", ", a.Barred.Values))}");
                        var lc = Leechers(a.Key);
                        lines.Add($"  blacklisted for leeching: {(lc.Count == 0 ? "nobody yet" : string.Join(", ", lc.Take(10).Select(l => $"{l.Name} {Ago(l.Ago)} (x{l.Cuts})")))}");
                        return lines;

                    case "on":
                    case "off":
                        if (op == "on" && a.Landblocks.Count == 0) { lines.Add($"'{a.Name}' has no landblocks yet."); return lines; }
                        a.Enabled = op == "on";
                        lines.Add(a.Enabled ? $"'{a.Name}' ON: players with /autofellow on join the {a.ShownName} fellowship there." : $"'{a.Name}' off: members leave within 2 minutes.");
                        break;

                    case "addlb":
                    case "removelb":
                    {
                        var lbs = new List<ushort>();
                        foreach (var t in rest)
                        {
                            if (t.Equals("here", StringComparison.OrdinalIgnoreCase) && player?.Location != null) { lbs.Add((ushort)player.Location.Landblock); continue; }
                            if (ushort.TryParse(t.Replace("0x", "").Replace("0X", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var lb)) lbs.Add(lb);
                            else { lines.Add($"'{t}' is not a landblock (hex, e.g. F65B, or here)."); return lines; }
                        }
                        if (lbs.Count == 0) { lines.Add($"Usage: afarea {a.Name} {op} here|<hex> [<hex>...]"); return lines; }
                        if (op == "addlb") foreach (var lb in lbs) { if (!a.Landblocks.Contains(lb)) a.Landblocks.Add(lb); }
                        else a.Landblocks.RemoveAll(lbs.Contains);
                        lines.Add($"'{a.Name}' {(op == "addlb" ? "+" : "-")}{string.Join(" ", lbs.Select(l => l.ToString("X4")))}: {a.Landblocks.Count} landblock(s).");
                        break;
                    }

                    case "clearlbs":
                        a.Landblocks.Clear();
                        lines.Add($"'{a.Name}' has no landblocks now.");
                        break;

                    case "import":
                    {
                        var what = rest.Count > 0 ? rest[0].ToLowerInvariant() : "";
                        IEnumerable<ushort> src = null;
                        if (what == "t10") src = LandblockCollections.ThaelarynIslandLandblocks;
                        else if (what == "vod") src = LandblockCollections.ValleyOfDeathLandblocks;
                        else if (what == "zone" && rest.Count > 1)
                        {
                            var zone = ZoneControlManager.GetArea(string.Join(" ", rest.Skip(1)));
                            if (zone != null) src = ZoneControlManager.SnapshotLandblocks(zone);   // its set changes under addlb / removelb
                        }
                        if (src == null) { lines.Add("Usage: afarea <name> import t10 | vod | zone <Zone Control zone>"); return lines; }
                        var before = a.Landblocks.Count;
                        foreach (var lb in src) if (!a.Landblocks.Contains(lb)) a.Landblocks.Add(lb);
                        lines.Add($"'{a.Name}' +{a.Landblocks.Count - before} landblock(s) from {string.Join(" ", rest)}: {a.Landblocks.Count} in all.");
                        break;
                    }

                    case "layers":
                    {
                        var text = string.Join("", rest).Trim();
                        if (text.Length == 0) { lines.Add($"Usage: afarea {a.Name} layers any|<n>[,<n>|<a>-<b>]"); return lines; }
                        var layers = new List<int>();
                        if (!text.Equals("any", StringComparison.OrdinalIgnoreCase))
                            foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
                            {
                                var range = part.Split('-');
                                if (range.Length == 2 && int.TryParse(range[0], out var lo) && int.TryParse(range[1], out var hi) && lo <= hi && hi - lo < 100)
                                    for (var v = lo; v <= hi; v++) layers.Add(v);
                                else if (int.TryParse(part, out var one)) layers.Add(one);
                                else { lines.Add("Layers: any, or numbers and ranges like 11-15 or 0,3,11-15."); return lines; }
                            }
                        a.Layers = layers.Distinct().OrderBy(v => v).ToList();
                        lines.Add($"'{a.Name}' layers: {(a.Layers.Count == 0 ? "any" : string.Join(",", a.Layers))}.");
                        break;
                    }

                    case "fellowname":
                    {
                        // owner 2026-11-05: no blank name, no "none" - a valid name or nothing changes; the area's own name = the default
                        var n = string.Join(" ", rest).Trim();
                        if (!ValidName(n) || n.Equals("none", StringComparison.OrdinalIgnoreCase))
                        {
                            lines.Add($"Not renamed: a fellowship name is 1-40 letters, digits, spaces, ' or -. It stays '{a.ShownName}'. (The default is '{a.BaseName}'.)");
                            return lines;
                        }
                        a.FellowName = n.Equals(a.Name, StringComparison.OrdinalIgnoreCase) || n.Equals(a.BaseName, StringComparison.OrdinalIgnoreCase) ? null : n;
                        lines.Add(a.FellowName == null
                            ? $"'{a.Name}' fellowship is back to its default name, '{a.BaseName}'. Members see it within a few seconds."
                            : $"'{a.Name}' fellowship renamed to '{a.FellowName}'. Members see it within a few seconds.");
                        break;
                    }

                    case "leech":
                    {
                        var sub = rest.Count > 0 ? rest[0].ToLowerInvariant() : "";
                        if (sub == "on" || sub == "off") a.AntiLeech = sub == "on";
                        else if (sub == "percent" && rest.Count > 1 && double.TryParse(rest[1].TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var pct) && pct >= 0 && pct <= 100) a.LeechPercent = pct;
                        else if (sub == "window" && rest.Count > 1 && int.TryParse(rest[1], out var min) && min >= 1 && min <= MaxWindowMinutes) a.LeechWindowMinutes = min;
                        else { lines.Add($"Usage: afarea {a.Name} leech on|off | leech percent <0-100> | leech window <1-{MaxWindowMinutes} minutes>"); return lines; }
                        lines.Add($"'{a.Name}' anti-leech {(a.AntiLeech ? "ON" : "off")}: {a.LeechPercent:0.#}% of an even share of the fellowship's damage over {a.LeechWindowMinutes} min.");
                        break;
                    }

                    case "bar":
                    case "unbar":
                    {
                        var who = string.Join(" ", rest).Trim();
                        if (who.Length == 0) { lines.Add($"Usage: afarea {a.Name} {op} <character name>"); return lines; }
                        uint guid = 0; string shown = who;
                        if (uint.TryParse(who, out var g))
                        {
                            // by id (the panel's Leechers list sends one): the character's name, never the digits (panel review 2026-11-07)
                            var byId = a.Barred.TryGetValue(g, out var n) ? n : PlayerManager.FindByGuid(g)?.Name;
                            if (byId != null) { guid = g; shown = byId; }
                        }
                        else
                        {
                            var found = PlayerManager.FindByName(who);
                            if (found != null) { guid = found.Guid.Full; shown = found.Name; }
                            else if (op == "unbar") { var hit = a.Barred.FirstOrDefault(b => b.Value.Equals(who, StringComparison.OrdinalIgnoreCase)); guid = hit.Key; shown = hit.Value ?? who; }
                        }
                        if (guid == 0) { lines.Add($"No character '{who}'."); return lines; }
                        if (op == "bar") { a.Barred[guid] = shown; lines.Add($"{shown} is blacklisted from the {a.ShownName} fellowship (out within 2 seconds if in it)."); }
                        else { a.Barred.Remove(guid); lines.Add($"{shown} may join the {a.ShownName} fellowship again."); }
                        break;
                    }

                    case "clearleechers":
                        _cuts.Remove(a.Key);
                        lines.Add($"'{a.Name}' leecher counts cleared.");
                        return lines;

                    default:
                        lines.Add("Usage: " + Usage);
                        return lines;
                }

                Save();
            }

            // what happened, in words (not the raw command)
            PlayerManager.BroadcastToAuditChannel(player, "Auto Fellow: " + (lines.Count > 0 ? lines[0] : string.Join(" ", args)));
            return lines;
        }
    }
}
