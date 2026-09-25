using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using log4net;

using ACE.Common;
using ACE.Database;
using ACE.Database.Models.World;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Room Assign: one-player rooms handed out by a room portal, every recall to it, and optionally a plate.
    /// Purpose (owner 2026-09-16): every player gets their own
    /// monster spawn in a dungeon, nobody can steal it, and the kill rate stays at the dungeon's balance.
    ///
    /// A ROOM SOURCE is a weenie carrying PropertyString.RoomAssignRooms (50500), one line holding every room:
    /// "room|0xCELL [x y z] qw qx qy qz|0xCELL,0xCELL;room|...". All cells of a list are on one landblock.
    ///  - PORTAL-ONLY: a portal weenie carries the list itself. Every way in - use, walk-in, a summoned gateway, Portal
    ///    Recall, Primary/Secondary tie recall - lands straight in a room, or is refused (the player stays where they are).
    ///  - PLATE: a pressure plate weenie carries the list and sends a player who steps on it to a room; a portal may point
    ///    at it with PropertyDataId.RoomAssignPlate (50501).
    /// The list is re-read from the weenie cache on every use (owner ruling), so an /id upload changes rooms live; a list
    /// that does not parse is logged once and the last good one is kept. Every source in the world database is loaded once
    /// at server startup (retried in the background if that fails), filling a cell -> room map that teleport, logout and
    /// login use. A portal source's rooms exist in its destination's variation; a plate source's where the plate is
    /// placed. Only at variation 3 and up (IsRoomVariation): the base world and v1/v2 are retail, which is never touched.
    ///
    /// Claims belong to the ACCOUNT (owner 2026-09-16): one room per account at a time in a dungeon, across all its
    /// characters - an account whose character stands in, or is on the way to, a room of this dungeon gets no second one.
    /// (A trip anywhere counts; standing in a room of ANOTHER dungeon does not.)
    /// A room is TAKEN for a player when:
    ///  - a non-staff player of another account stands in it (dead and teleporting players count),
    ///  - another account holds a reservation on it (from the hand-out until that player has landed; 30 s cap), or
    ///  - another account holds it: a LEAVE hold (room_assign_leave_hold_seconds, made when teleporting out of a room -
    ///    recall, portal, death) or a LOGOUT hold (room_assign_logout_hold_minutes, made at logout inside a room while the
    ///    account has logout-hold credit). One hold per account.
    /// An account whose other character is standing in or travelling to a room gets no room at all.
    /// A player gets, in order: the room reserved for them, the room they stand in, the room held for their account, the
    /// room their account was last handed, the first free room. The last two are FRESH hand-outs; only a fresh hand-out of
    /// a room that is new to the account grants logout-hold credit (1 + room_assign_logout_hold_renewals).
    ///
    /// Threading: a plate step, portal or recall runs on its landblock group's thread, teleport on whatever thread
    /// teleports, login on the world thread, logout on whichever thread finalizes it. All claim state sits behind _lock.
    /// Nothing inside _lock does database work, teleports or takes a manager lock; the only locks taken inside it are a
    /// player's own property read locks. Lock order _scanLock -> _parseLock -> _lock, never reversed. The builder's
    /// _roomListWriteLock (database work) is always RELEASED before GetRooms takes _parseLock / _lock - never nested.
    /// PlayerManager.GetAllOnline takes its own lock, so occupancy is built BEFORE _lock.
    /// </summary>
    public static partial class RoomAssignManager   // the dungeon builder and the test tools are partials of this class
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>Hard cap on a reservation that never saw its player land (normally released on landing).</summary>
        private static readonly TimeSpan ReservationTime = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Hard cap on a PENDING reservation - one made before the trip is certain (a recall at cast, a portal at its use
        /// check). Short: while it stands, its player is "on the way" and every other trigger is refused.
        /// </summary>
        private static readonly TimeSpan PendingReservationTime = TimeSpan.FromSeconds(10);

        /// <summary>Owner 2026-09-16: was 10 s, too long to wait for the message.</summary>
        private static readonly TimeSpan FullMessageInterval = TimeSpan.FromSeconds(1);

        private const int FullMessagePruneThreshold = 64;

        // Setting caps: a huge /modifylong value must not overflow DateTime arithmetic in every check.
        private const long MaxLogoutHoldMinutes = 7 * 24 * 60;
        private const long MaxLeaveHoldSeconds = 24 * 60 * 60;
        private const long MaxLogoutHoldRenewals = 100;

        /// <summary>Server setting room_assign_logout_hold_minutes (0 - 10080). 0 = no logout holds.</summary>
        private static TimeSpan LogoutHoldTime => TimeSpan.FromMinutes(Math.Clamp(ServerConfig.room_assign_logout_hold_minutes.Value, 0, MaxLogoutHoldMinutes));

        /// <summary>Server setting room_assign_leave_hold_seconds (0 - 86400; owner 2026-09-23: 120, long enough to nip out for a trade). 0 = off.</summary>
        private static TimeSpan LeaveHoldTime => TimeSpan.FromSeconds(Math.Clamp(ServerConfig.room_assign_leave_hold_seconds.Value, 0, MaxLeaveHoldSeconds));

        /// <summary>
        /// 1 + server setting room_assign_logout_hold_renewals (0 - 100; owner 2026-09-16: 1). A fresh hand-out allows one
        /// logout hold plus this many more - a crash, and a second crash, keep the room; relogging forever does not.
        /// </summary>
        private static int LogoutHoldCredit => 1 + (int)Math.Clamp(ServerConfig.room_assign_logout_hold_renewals.Value, 0, MaxLogoutHoldRenewals);

        // No startup grace (review 2026-09-24, owner): a restart wipes every hold, but a character logging back in inside a
        // free chamber keeps it anyway (the 2026-09-23 keep-a-free-room rule), so the grace setting no longer decided anything.

        /// <summary>A landing coordinate further than this from the landblock's origin, either way, is refused as a typo.</summary>
        internal const float MaxLandingCoordinate = 10000f;   // internal: the builder's map pins are bounded the same way

        /// <summary>A landing rotation shorter than this is treated as all zero.</summary>
        private const double MinRotationLength = 0.01;

        private const string MessagePlateAllTaken = "Every chamber is taken. Move on the plate to try again.";
        private const string MessagePortalAllTaken = "Every chamber is taken. Try again later.";
        private const string MessageOnTheWay = "You are already on your way to a chamber.";
        private const string MessageAccountHasRoom = "Another character on your account already has a chamber.";
        private const string MessageNotReady = "The chambers beyond this portal are not ready.";
        private const string MessageMovedOut = "Your chamber was taken while you were away. You have been returned to your lifestone.";
        private const string MessageNoClaim = "You no longer hold a chamber. You have been returned to your lifestone.";
        private const string MessageCannotRemain = "You cannot remain here. You have been returned to your lifestone.";
        private const string MessageBackToRoom = "You have been returned to your chamber.";

        private static string MessageMovedToRoom(int room) => $"Your chamber was taken while you were away. You have been moved to chamber {room}.";

        /// <summary>Logged back in and the hold had kept the chamber (owner 2026-09-23: logging in said nothing at all).</summary>
        private static string MessageStillYours(int room) => $"Welcome back. Chamber {room} is still yours.";

        /// <summary>
        /// Logged back in inside a chamber nobody held and nobody else had (after a restart, or once a hold ran out): they
        /// keep it by being the one standing in it. Not "still yours" - it was never held for them, and saying so would be a lie.
        /// </summary>
        private static string MessageYoursAgain(int room) => $"Chamber {room} is yours again.";

        /// <summary>
        /// The arrival line (owner 2026-09-20), the same for every dungeon: "The Tyrant's Quarry opens Chamber 1 to you,
        /// and seals it behind you." The dungeon's name is the NAME of the weenie the player came in by - the room portal
        /// (a summoned gateway: the portal it was made from), or the plate. "The" is put in front unless the name has it.
        /// </summary>
        public static string MessageSentToRoom(uint entranceWcid, int room)
        {
            string name = null;
            var weenie = DatabaseManager.World.GetCachedWeenie(entranceWcid);
            if (weenie?.PropertiesString != null)
                weenie.PropertiesString.TryGetValue(PropertyString.Name, out name);

            name = string.IsNullOrWhiteSpace(name) ? "dungeon" : name.Trim();

            if (!name.StartsWith("The ", StringComparison.OrdinalIgnoreCase))
                name = "The " + name;

            return $"{name} opens Chamber {room} to you, and seals it behind you.";
        }

        public sealed class Room
        {
            public int Number;
            public uint LandingCell;
            public float X, Y, Z;
            public float QW, QX, QY, QZ;
            public HashSet<uint> Cells;

            /// <summary>The room's lowest cell - its identity, so an /id edit that only moves the landing keeps its claims.</summary>
            public uint Anchor;

            /// <summary>
            /// Base and v2 share every cell id, so a room is only unique together with its variation. No cell may belong to
            /// two rooms of a list, so the lowest cell names one physical room.
            /// </summary>
            public string Key(int? variation) => KeyFor(variation, Anchor);

            // The key is built for every room on every check, inside _lock: cache the string per (anchor, variation).
            private static readonly ConcurrentDictionary<(uint Anchor, int? Variation), string> _keys = new ConcurrentDictionary<(uint, int?), string>();

            internal static string KeyFor(int? variation, uint anchor)
                => _keys.GetOrAdd((anchor, VariationManager.NormalizeBase(variation)),
                    k => $"{k.Variation?.ToString(CultureInfo.InvariantCulture)}|{k.Anchor:X8}");
        }

        /// <summary>
        /// Rooms only exist at variation 3 and up (owner 2026-09-21: every auto-assigned dungeon is v3+). The base world and
        /// v1/v2 are retail layers: a room list authored there by hand - past the builder's own v3 gate - is ignored, and so
        /// is everything that hangs off a room source (Zone Share, Kill Reward). Review 2026-09-24: this used to admit v1/v2.
        /// </summary>
        public static bool IsRoomVariation(int? variation) => (VariationManager.NormalizeBase(variation) ?? 0) >= BuilderMinVariation;

        /// <summary>
        /// A dungeon's key for Zone Share and Kill Reward: its source AND variation, so two copies of one dungeon (a v3 and a
        /// v5, say) are two separate areas - never one share group, never one bounty count. "dungeon:777704023v3": no '|', ';'
        /// or '#', which the Kill Reward progress string on the character uses as separators.
        /// </summary>
        public static string DungeonAreaKey(uint sourceWcid, int? variation)
            => "dungeon:" + sourceWcid.ToString(CultureInfo.InvariantCulture) + "v" + (VariationManager.NormalizeBase(variation)?.ToString(CultureInfo.InvariantCulture) ?? "0");

        // ---------------------------------------------------------------------------------------------------------
        // Room lists: parsed from the weenie cache, last good list kept per source WCID
        // ---------------------------------------------------------------------------------------------------------

        private sealed class ParsedList
        {
            public string Raw;
            public List<Room> Rooms;
        }

        private static readonly object _parseLock = new object();
        private static readonly Dictionary<uint, ParsedList> _lastGood = new Dictionary<uint, ParsedList>();
        private static readonly Dictionary<uint, string> _lastBad = new Dictionary<uint, string>();   // source WCID -> the bad string (not parsed again)
        private static readonly List<Room> NoRooms = new List<Room>();
        private static volatile int _roomSourceCount;   // _lastGood.Count, readable without a lock on hot paths
        private static readonly ConcurrentDictionary<uint, byte> _roomSourceWcids = new ConcurrentDictionary<uint, byte>();

        private static readonly object _scanLock = new object();
        private static volatile int _sourcesScanned;   // 1 once every room source in the world database is loaded
        private static int _scanRunning;               // 1 while a background retry runs (Interlocked)
        private static long _scanRetryAtTicks;         // UTC ticks (Interlocked)
        private static readonly TimeSpan ScanRetry = TimeSpan.FromSeconds(60);

        // Strict on purpose: Position.TryParse silently falls back to a default facing when the rotation does not parse,
        // which is exactly what a pasted /location line does (its ", v:2" leaves a comma on the last number).
        /// <summary>A cell id as authored: 0x plus 1-8 hex digits, nothing else (NumberStyles.HexNumber would allow "0x 1F7").</summary>
        private static readonly Regex CellRx = new Regex(@"^0x[0-9A-Fa-f]{1,8}$", RegexOptions.Compiled);

        /// <summary>The landing: the same cell form as CellRx (1-8 hex digits), a bracketed position and a four-number rotation.</summary>
        private static readonly Regex LandingRx = new Regex(@"^0x([0-9A-Fa-f]{1,8})\s+\[\s*(\S+)\s+(\S+)\s+(\S+)\s*\]\s+(\S+)\s+(\S+)\s+(\S+)\s+(\S+)$", RegexOptions.Compiled);

        /// <summary>
        /// Program startup, before sockets open: loads every room source and where each is placed (database queries that must
        /// not run on a game thread).
        /// </summary>
        public static void Initialize()
        {
            ScanRoomSources();
        }

        /// <summary>WorldManager.Open: a startup scan that failed must not wait for the first teleport or login to retry it.</summary>
        public static void OnWorldOpened()
        {
            EnsureRoomSourcesLoaded();
        }

        /// <summary>
        /// Hot paths (teleport, logout, login) call this. After a successful load it is one int read. If the startup load
        /// failed, a retry runs on a background thread at most once per ScanRetry - never on the calling game thread.
        /// </summary>
        private static void EnsureRoomSourcesLoaded()
        {
            if (_sourcesScanned != 0)
                return;

            if (DateTime.UtcNow.Ticks < Interlocked.Read(ref _scanRetryAtTicks))
                return;

            if (Interlocked.CompareExchange(ref _scanRunning, 1, 0) != 0)
                return;

            Task.Run(() =>
            {
                try
                {
                    ScanRoomSources();
                }
                finally
                {
                    Interlocked.Exchange(ref _scanRunning, 0);
                }
            });
        }

        private static void ScanRoomSources()
        {
            lock (_scanLock)
            {
                if (_sourcesScanned != 0)
                    return;

                try
                {
                    var started = DateTime.UtcNow;
                    List<uint> ids;
                    using (var context = new WorldDbContext())
                        ids = context.WeeniePropertiesString
                            .Where(r => r.Type == (ushort)PropertyString.RoomAssignRooms)
                            .Select(r => r.ObjectId)
                            .Distinct()
                            .ToList();

                    foreach (var id in ids)
                        GetRooms(id);

                    var placements = RememberPlacements(ids);

                    _sourcesScanned = 1;
                    log.Info($"[RoomAssign] {ids.Count} room source(s) in the world database, {placements} placement(s) in room variations ({(DateTime.UtcNow - started).TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture)} s).");
                }
                catch (Exception ex)
                {
                    Interlocked.Exchange(ref _scanRetryAtTicks, (DateTime.UtcNow + ScanRetry).Ticks);
                    log.Error($"[RoomAssign] Loading room sources failed, retrying in {ScanRetry.TotalSeconds:0} s: {ex}");
                }
            }
        }

        /// <summary>
        /// Startup scan: where every room source is placed in the world database, so its rooms are known in that variation
        /// before its landblock loads (landblock loading does not run EnterWorld). A plate: its placement's variation. A
        /// portal - one carrying a list, or pointing at a plate - its destination's variation: the weenie's Destination, else
        /// its link spot's placement, else its own placement (a relative destination). Returns the count remembered.
        /// </summary>
        private static int RememberPlacements(List<uint> sourceIds)
        {
            List<WeeniePropertiesDID> pointers;
            List<LandblockInstance> placed;
            List<LandblockInstanceLink> links;
            List<LandblockInstance> linkSpots;

            using (var context = new WorldDbContext())
            {
                pointers = context.WeeniePropertiesDID.Where(d => d.Type == (ushort)PropertyDataId.RoomAssignPlate).ToList();

                var wcids = sourceIds.Concat(pointers.Select(d => d.ObjectId)).Distinct().ToList();
                placed = context.LandblockInstance.Where(i => wcids.Contains(i.WeenieClassId)).ToList();

                var guids = placed.Select(i => i.Guid).ToList();
                links = context.LandblockInstanceLink.Where(l => guids.Contains(l.ParentGuid)).ToList();

                var childGuids = links.Select(l => l.ChildGuid).Distinct().ToList();
                linkSpots = context.LandblockInstance.Where(i => childGuids.Contains(i.Guid)).ToList();
            }

            var sources = new HashSet<uint>(sourceIds);
            var spotVariation = linkSpots.ToDictionary(i => i.Guid, i => i.VariationId);
            var linkedTo = new Dictionary<uint, uint>();
            foreach (var link in links)
                linkedTo.TryAdd(link.ParentGuid, link.ChildGuid);

            var plateOf = new Dictionary<uint, uint>();
            foreach (var pointer in pointers)
                plateOf.TryAdd(pointer.ObjectId, pointer.Value);

            var remembered = 0;

            foreach (var instance in placed)
            {
                var weenie = DatabaseManager.World.GetCachedWeenie(instance.WeenieClassId);
                if (weenie == null)
                    continue;

                uint sourceWcid;
                int? variation;

                if (weenie.WeenieType == WeenieType.Portal)
                {
                    if (weenie.PropertiesString != null && weenie.PropertiesString.ContainsKey(PropertyString.RoomAssignRooms))
                        sourceWcid = instance.WeenieClassId;
                    else if (!plateOf.TryGetValue(instance.WeenieClassId, out sourceWcid))
                        continue;

                    // A link spot wins: Portal.SetLinkProperties overwrites the weenie's Destination with it when the portal loads.
                    if (linkedTo.TryGetValue(instance.Guid, out var child) && spotVariation.TryGetValue(child, out var linkVariation))
                        variation = linkVariation;
                    else if (weenie.PropertiesPosition != null && weenie.PropertiesPosition.TryGetValue(PositionType.Destination, out var destination))
                        variation = destination.VariationId;
                    else
                        variation = instance.VariationId;
                }
                else if (sources.Contains(instance.WeenieClassId))
                {
                    sourceWcid = instance.WeenieClassId;
                    variation = instance.VariationId;
                }
                else
                    continue;

                if (!IsRoomVariation(variation))
                    continue;

                lock (_lock)
                    if (_sourceSeen.Add(SourceKey(sourceWcid, variation)))
                        remembered++;
            }

            return remembered;
        }

        /// <summary>
        /// The rooms for a room source WCID (portal or plate): null when the weenie carries no room list, EMPTY when it
        /// carries one that has never parsed, else the last good list. Only a changed string is parsed, and a newly parsed
        /// list is registered in the cell map under the same lock, so the map always matches the last good list.
        /// </summary>
        public static List<Room> GetRooms(uint wcid)
        {
            var weenie = DatabaseManager.World.GetCachedWeenie(wcid);

            if (weenie?.PropertiesString == null || !weenie.PropertiesString.TryGetValue(PropertyString.RoomAssignRooms, out var raw))
            {
                // Every pressure plate on the server comes through here: only lock for a WCID that was a room source.
                if (_roomSourceWcids.ContainsKey(wcid))
                {
                    lock (_parseLock)
                    {
                        _lastBad.Remove(wcid);

                        if (_lastGood.Remove(wcid))
                        {
                            _roomSourceWcids.TryRemove(wcid, out _);
                            _roomSourceCount = _lastGood.Count;
                            lock (_lock)
                                RegisterRooms(wcid, NoRooms);
                            log.Info($"[RoomAssign] wcid {wcid}: room list removed - no longer a room source.");
                        }
                    }
                }

                return null;
            }

            lock (_parseLock)
            {
                _lastGood.TryGetValue(wcid, out var good);

                if (good != null && good.Raw == raw)
                    return good.Rooms;

                // A bad string is parsed once, not on every use.
                if (_lastBad.TryGetValue(wcid, out var lastBad) && lastBad == raw)
                    return good?.Rooms ?? NoRooms;

                if (!TryParseRooms(raw, out var parsed, out var error))
                {
                    _lastBad[wcid] = raw;
                    log.Warn($"[RoomAssign] wcid {wcid}: room list does not parse ({error}) - {(good != null ? "keeping the last good list" : "no good list yet, it hands out no rooms")}.");
                    return good?.Rooms ?? NoRooms;
                }

                _lastGood[wcid] = new ParsedList { Raw = raw, Rooms = parsed };
                _roomSourceWcids[wcid] = 0;
                _roomSourceCount = _lastGood.Count;
                _lastBad.Remove(wcid);

                lock (_lock)
                {
                    RegisterRooms(wcid, parsed);

                    if (good != null)
                    {
                        MoveClaims(wcid, good.Rooms, parsed);

                        foreach (var oldRoom in good.Rooms)
                            _badLandingWarned.Remove(oldRoom);
                    }
                }

                log.Info($"[RoomAssign] wcid {wcid}: loaded {parsed.Count} room(s).");
                return parsed;
            }
        }

        /// <summary>
        /// Parses "room|0xCELL [x y z] qw qx qy qz|0xCELL,0xCELL;room|...". Every error names the room it is in. All cells
        /// must be on one landblock; landing numbers must be finite and the rotation non-zero.
        /// </summary>
        public static bool TryParseRooms(string raw, out List<Room> rooms, out string error)
        {
            rooms = new List<Room>();
            error = null;

            if (string.IsNullOrWhiteSpace(raw))
            {
                error = "the list is empty";
                return false;
            }

            var numbers = new HashSet<int>();
            var cellOwner = new Dictionary<uint, int>();
            uint? landblock = null;

            foreach (var entryRaw in raw.Split(';'))
            {
                var entry = entryRaw.Trim();
                if (entry.Length == 0)
                    continue;

                var fields = entry.Split('|');
                if (fields.Length != 3)
                {
                    error = $"\"{entry}\" needs 3 fields: room|landing|cells";
                    return false;
                }

                if (!int.TryParse(fields[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) || number <= 0)
                {
                    error = $"\"{fields[0].Trim()}\" is not a room number";
                    return false;
                }

                if (!numbers.Add(number))
                {
                    error = $"room {number} appears twice";
                    return false;
                }

                var m = LandingRx.Match(fields[1].Trim());
                if (!m.Success)
                {
                    error = $"room {number}: the landing must be 0xCELL [x y z] qw qx qy qz";
                    return false;
                }

                var nums = new float[7];
                for (var i = 0; i < 7; i++)
                {
                    if (!float.TryParse(m.Groups[i + 2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out nums[i]) || !float.IsFinite(nums[i]))
                    {
                        error = $"room {number}: \"{m.Groups[i + 2].Value}\" in the landing is not a number";
                        return false;
                    }
                }

                // A landing is a spot in one landblock (review 2026-09-24): a typo like 4e30 parses as a number but is no place
                // anyone can be teleported to.
                for (var i = 0; i < 3; i++)
                    if (Math.Abs(nums[i]) > MaxLandingCoordinate)
                    {
                        error = $"room {number}: the landing position {m.Groups[i + 2].Value} is out of range (at most {MaxLandingCoordinate} either way)";
                        return false;
                    }

                // In double: a huge float squared would overflow to infinity and normalise to an all-zero rotation.
                var rotationLength = Math.Sqrt((double)nums[3] * nums[3] + (double)nums[4] * nums[4] + (double)nums[5] * nums[5] + (double)nums[6] * nums[6]);
                if (!double.IsFinite(rotationLength) || rotationLength < MinRotationLength)
                {
                    error = $"room {number}: the landing rotation is all zero (use 1 0 0 0 for the default facing)";
                    return false;
                }

                // A hand-typed rotation need not be unit length; the client expects one.
                for (var i = 3; i < 7; i++)
                    nums[i] = (float)(nums[i] / rotationLength);

                var room = new Room
                {
                    Number = number,
                    LandingCell = uint.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    X = nums[0], Y = nums[1], Z = nums[2],
                    QW = nums[3], QX = nums[4], QY = nums[5], QZ = nums[6],
                    Cells = new HashSet<uint>(),
                };

                landblock ??= room.LandingCell >> 16;

                foreach (var cellRaw in fields[2].Split(','))
                {
                    var cellText = cellRaw.Trim();
                    if (!CellRx.IsMatch(cellText) ||
                        !uint.TryParse(cellText.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var cell))
                    {
                        error = $"room {number}: \"{cellText}\" is not a cell id";
                        return false;
                    }

                    // Indoor cells only (0x0100 and up): the outdoor cells of a landblock are never part of a dungeon, and the
                    // login rules for a portal-only dungeon ignore them.
                    if ((cell & 0xFFFF) < 0x0100)
                    {
                        error = $"room {number}: cell 0x{cell:X8} is an outdoor cell - a room is made of indoor cells (0x...0100 and up)";
                        return false;
                    }

                    if (cell >> 16 != landblock)
                    {
                        error = $"room {number}: cell 0x{cell:X8} is not on landblock 0x{landblock:X4} - a room list is one dungeon";
                        return false;
                    }

                    if (cellOwner.TryGetValue(cell, out var other) && other != number)
                    {
                        error = $"cell 0x{cell:X8} is in both room {other} and room {number}";
                        return false;
                    }

                    cellOwner[cell] = number;
                    room.Cells.Add(cell);
                }

                if (!room.Cells.Contains(room.LandingCell))
                {
                    error = $"room {number}: the landing cell 0x{room.LandingCell:X8} is not in its own cell list";
                    return false;
                }

                room.Anchor = room.Cells.Min();
                rooms.Add(room);
            }

            if (rooms.Count == 0)
            {
                error = "the list has no rooms";
                return false;
            }

            rooms.Sort((a, b) => a.Number.CompareTo(b.Number));
            return true;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Claim state - all behind _lock
        // ---------------------------------------------------------------------------------------------------------

        private enum HoldKind { Logout, Leave }

        private sealed class Reservation
        {
            public uint Guid;
            public uint Account;
            public Room Room;
            public int? Variation;
            public DateTime Until;
            public double CreatedUnix;   // compared with PropertyFloat.LastTeleportStartTimestamp

            /// <summary>The CHARACTER on their way in. Display only - every decision still compares Guid/Account
            /// (owner 2026-09-23, the same reason holds carry a name).</summary>
            public string Name;

            /// <summary>
            /// Made before the trip is certain (a recall at cast, a portal at its use check): the account's holds and credit
            /// are untouched until the teleport commits it, so a refused or abandoned trip loses nothing.
            /// </summary>
            public bool Pending;
            public bool PendingFresh;
        }

        private sealed class Hold
        {
            public uint Account;
            public DateTime Until;
            public HoldKind Kind;

            /// <summary>
            /// The CHARACTER who made the hold. A hold belongs to the account, but the account is a number nobody
            /// recognises - the tab shows this so "Hold" says who it is waiting for (owner 2026-09-23). Display only:
            /// every decision still compares Account.
            /// </summary>
            public string Name;
        }

        private sealed class RoomRef
        {
            public uint SourceWcid;
            public Room Room;
        }

        private static readonly object _lock = new object();
        private static readonly Dictionary<string, Reservation> _reservations = new Dictionary<string, Reservation>();   // room key -> player on their way in
        private static readonly Dictionary<uint, string> _reservedBy = new Dictionary<uint, string>();                   // player guid -> room key
        private static readonly Dictionary<string, Hold> _holds = new Dictionary<string, Hold>();                        // room key -> account it is held for
        private static readonly Dictionary<string, uint> _roomOwner = new Dictionary<string, uint>();                     // room key -> the account it was last handed to
        private static readonly Dictionary<string, DateTime> _roomOwnerSince = new Dictionary<string, DateTime>();       // room key -> when THAT account's tenure began
        private static readonly Dictionary<uint, string> _announcedRoom = new Dictionary<uint, string>();                // player guid -> the room key their arrival line last named
        private static readonly Dictionary<uint, string> _leftRoom = new Dictionary<uint, string>();                     // player guid -> the room key they last walked out of
        private static readonly Dictionary<uint, DateTime> _lastFullMessage = new Dictionary<uint, DateTime>();
        private static readonly Dictionary<uint, List<RoomRef>> _roomByCell = new Dictionary<uint, List<RoomRef>>();     // cell -> rooms + source WCID
        private static readonly Dictionary<ushort, HashSet<uint>> _sourcesByLandblock = new Dictionary<ushort, HashSet<uint>>();   // landblock -> source WCIDs
        // "wcid|variation" -> the source's rooms exist in that variation: read from its database placements at startup, or a
        // copy was spawned (/createinst, a generator) or used there. Kept until restart - a source removed from a variation
        // only keeps its rooms recognised there (holds, login checks) until then.
        private static readonly HashSet<string> _sourceSeen = new HashSet<string>();

        /// <summary>Rooms already warned about a landing outside their cells (per parsed Room object; cleared when its list is re-parsed).</summary>
        private static readonly HashSet<Room> _badLandingWarned = new HashSet<Room>();

        // Logout holds an ACCOUNT may still take in ONE DUNGEON (CreditKey) before its next fresh hand-out there. Without a limit,
        // relogging - or recalling into the room you stand in - renewed a hold forever; per dungeon (review 2026-09-24), so a
        // new room in another dungeon cannot refill this one. A used-up credit stays as an explicit 0 - "used up" is not "no
        // record": only a record the account has lost (below) is refilled by getting its own room back. Every record of the
        // account is dropped when its character logs out outside a room holding nothing - the visit is over - so the table never
        // outgrows the accounts currently using rooms, and coming back another day refills.
        private static readonly Dictionary<string, int> _logoutHoldCredit = new Dictionary<string, int>();

        /// <summary>The credit key: an account in one dungeon (the rooms' landblock and variation).</summary>
        private static string CreditKey(uint account, Room room, int? variation)
            => $"{account}|{room.LandingCell >> 16:X4}|{VariationManager.NormalizeBase(variation)?.ToString(CultureInfo.InvariantCulture)}";

        /// <summary>
        /// Credit key -> when a USED-UP record (0) of it was dropped. Dropping the record ends the visit, but the used-up state is
        /// remembered for one logout-hold length (review 2026-09-24): otherwise logging out an alt in town would drop the main's
        /// 0 and let its own room refill the credit - a room held offline forever with seconds-long gaps. After that long the
        /// room has been unprotected long enough that getting it back is a genuine return. Pruned by PurgeExpired.
        /// </summary>
        private static readonly Dictionary<string, DateTime> _creditSpentAt = new Dictionary<string, DateTime>();

        /// <summary>Call with _lock held. Drops every logout-hold credit record of the account (a used-up one is remembered, above).</summary>
        private static void DropCredit(uint account, DateTime now)
        {
            var prefix = account.ToString(CultureInfo.InvariantCulture) + "|";
            foreach (var key in _logoutHoldCredit.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            {
                if (_logoutHoldCredit[key] <= 0)
                    _creditSpentAt[key] = now;
                _logoutHoldCredit.Remove(key);
            }
        }

        /// <summary>
        /// Every cell that is part of some room, readable WITHOUT _lock: the trespass check runs on every indoor cell change of
        /// every player at v3+, and most of those cells are corridors - they must not take the global lock (review 2026-09-24).
        /// Kept in step with _roomByCell by RegisterRooms.
        /// </summary>
        private static readonly ConcurrentDictionary<uint, byte> _roomCells = new ConcurrentDictionary<uint, byte>();

        /// <summary>
        /// Call with _lock held. Cells of a re-parsed (or removed) list replace that source's old cells. A cell may belong to
        /// several sources (e.g. a portal and an unplaced plate listing the same rooms); a lookup takes the one valid in the
        /// variation.
        /// </summary>
        private static void RegisterRooms(uint sourceWcid, List<Room> rooms)
        {
            var emptied = new List<uint>();
            foreach (var kv in _roomByCell)
            {
                kv.Value.RemoveAll(r => r.SourceWcid == sourceWcid);
                if (kv.Value.Count == 0)
                    emptied.Add(kv.Key);
            }
            foreach (var cell in emptied)
            {
                _roomByCell.Remove(cell);
                _roomCells.TryRemove(cell, out _);
            }

            foreach (var room in rooms)
            {
                foreach (var cell in room.Cells)
                {
                    if (!_roomByCell.TryGetValue(cell, out var refs))
                        _roomByCell[cell] = refs = new List<RoomRef>();

                    refs.Add(new RoomRef { SourceWcid = sourceWcid, Room = room });
                    _roomCells[cell] = 0;
                }
            }

            var emptiedBlocks = new List<ushort>();
            foreach (var kv in _sourcesByLandblock)
            {
                kv.Value.Remove(sourceWcid);
                if (kv.Value.Count == 0)
                    emptiedBlocks.Add(kv.Key);
            }
            foreach (var block in emptiedBlocks)
                _sourcesByLandblock.Remove(block);

            if (rooms.Count > 0)
            {
                var landblock = (ushort)(rooms[0].LandingCell >> 16);
                if (!_sourcesByLandblock.TryGetValue(landblock, out var sources))
                    _sourcesByLandblock[landblock] = sources = new HashSet<uint>();
                sources.Add(sourceWcid);
            }
        }

        /// <summary>
        /// Call with _lock held, after RegisterRooms, when a list was re-parsed. A room is the same room when it shares a cell
        /// with an old one. Its key is its lowest cell, so an edit that adds or removes that cell changes the key. Every move
        /// is worked out first and applied in TWO passes (every claim taken out under its old key, then put in under its new
        /// one), using the exact old key in the variations this source is used in, so a chain of changed anchors cannot carry a
        /// claim from room to room. A claim is never moved onto a room that already has one, and never away from an anchor
        /// another source still lists (both are logged).
        /// </summary>
        private static void MoveClaims(uint sourceWcid, List<Room> oldRooms, List<Room> newRooms)
        {
            var moved = new Dictionary<uint, Room>();   // old anchor -> new room

            foreach (var oldRoom in oldRooms)
            {
                var newRoom = newRooms.FirstOrDefault(r => r.Cells.Overlaps(oldRoom.Cells));
                if (newRoom == null)
                    continue;

                foreach (var reservation in _reservations.Values)
                    if (reservation.Room == oldRoom)
                        reservation.Room = newRoom;

                if (newRoom.Anchor == oldRoom.Anchor || StillListedElsewhere(sourceWcid, oldRoom.Anchor))
                    continue;

                moved[oldRoom.Anchor] = newRoom;
            }

            if (moved.Count == 0)
                return;

            // The variations this source's rooms are used in - a key of another source's dungeon must not be touched.
            var variations = SourceVariations(sourceWcid);

            // TWO passes (review 2026-09-24): first every moving claim is taken out from under its OLD key, then each is put
            // in under its NEW key. Moving them one pair at a time let a chain of edits (room X re-keyed onto Y's old anchor,
            // Y onto a third) carry X's owner through Y's key to the third room, and overwrite Y's owner on the way.
            var taken = new List<(string OldKey, string NewKey, int Number, Hold Hold, Reservation Reservation, uint? Owner, DateTime? Since)>();

            foreach (var pair in moved)
            {
                foreach (var variation in variations)
                {
                    var oldKey = Room.KeyFor(variation, pair.Key);

                    _holds.TryGetValue(oldKey, out var hold);
                    _reservations.TryGetValue(oldKey, out var reservation);
                    uint? owner = _roomOwner.TryGetValue(oldKey, out var o) ? o : null;
                    DateTime? since = _roomOwnerSince.TryGetValue(oldKey, out var s) ? s : null;

                    if (hold == null && reservation == null && owner == null && since == null)
                        continue;

                    _holds.Remove(oldKey);
                    _reservations.Remove(oldKey);
                    _roomOwner.Remove(oldKey);
                    _roomOwnerSince.Remove(oldKey);

                    taken.Add((oldKey, pair.Value.Key(variation), pair.Value.Number, hold, reservation, owner, since));
                }
            }

            foreach (var t in taken)
            {
                if (t.Hold != null)
                {
                    if (_holds.ContainsKey(t.NewKey))
                        log.Warn($"[RoomAssign] Room list edit (wcid {sourceWcid}): the hold on {t.OldKey} for account {t.Hold.Account} was dropped - room {t.Number} already has one.");
                    else
                        _holds[t.NewKey] = t.Hold;
                }

                if (t.Reservation != null)
                {
                    if (_reservations.ContainsKey(t.NewKey))
                    {
                        _reservedBy.Remove(t.Reservation.Guid);
                        log.Warn($"[RoomAssign] Room list edit (wcid {sourceWcid}): the reservation on {t.OldKey} for 0x{t.Reservation.Guid:X8} was dropped - room {t.Number} is already reserved.");
                    }
                    else
                    {
                        _reservations[t.NewKey] = t.Reservation;
                        _reservedBy[t.Reservation.Guid] = t.NewKey;
                    }
                }

                // Owner and tenure move together, and never over a room that already has an owner: the same collision rule
                // holds use. The tenure clock follows its room - a re-keyed room is the same room, so it must not restart.
                if (t.Owner != null)
                {
                    if (_roomOwner.ContainsKey(t.NewKey))
                        log.Warn($"[RoomAssign] Room list edit (wcid {sourceWcid}): room {t.Number} already has an owner - account {t.Owner} from {t.OldKey} was dropped.");
                    else
                    {
                        _roomOwner[t.NewKey] = t.Owner.Value;
                        if (t.Since != null)
                            _roomOwnerSince[t.NewKey] = t.Since.Value;
                    }
                }
                else if (t.Since != null && !_roomOwnerSince.ContainsKey(t.NewKey))
                    _roomOwnerSince[t.NewKey] = t.Since.Value;
            }
        }

        /// <summary>
        /// Call with _lock held. Every variation this source's rooms are used in (placed or used there). One room list serves
        /// them all, so a change to a room is a change in each of them.
        /// </summary>
        internal static List<int?> SourceVariations(uint sourceWcid)
        {
            var variations = new List<int?>();
            var prefix = sourceWcid.ToString(CultureInfo.InvariantCulture) + "|";
            foreach (var seen in _sourceSeen)
                if (seen.StartsWith(prefix, StringComparison.Ordinal))
                {
                    var text = seen.Substring(prefix.Length);
                    variations.Add(text.Length == 0 ? (int?)null : int.Parse(text, CultureInfo.InvariantCulture));
                }
            return variations;
        }

        /// <summary>Call with _lock held. True when another source still has a room with this anchor - its claims stay put.</summary>
        private static bool StillListedElsewhere(uint sourceWcid, uint anchor)
            => _roomByCell.TryGetValue(anchor, out var refs) && refs.Any(r => r.SourceWcid != sourceWcid && r.Room.Anchor == anchor);

        /// <summary>
        /// The PORTAL-ONLY source whose dungeon this cell is in (its landblock, in the source's variation), or 0. A portal-only
        /// dungeon has no legitimate place outside its rooms - every way in lands in a room and rooms are sealed. A dungeon
        /// that also has a plate source valid there is plate mode (its arrival hall is legitimate), so it returns 0.
        /// </summary>
        private static uint FindPortalOnlyDungeon(uint cell, int? variation)
        {
            if (_roomSourceCount == 0 || !IsRoomVariation(variation))
                return 0;

            // Only indoor cells: the outdoor cells of a landblock (below 0x0100) are never part of a dungeon.
            if ((cell & 0xFFFF) < 0x0100)
                return 0;

            uint[] sources;
            lock (_lock)
            {
                if (!_sourcesByLandblock.TryGetValue((ushort)(cell >> 16), out var set))
                    return 0;
                sources = set.ToArray();
            }

            uint portalSource = 0;
            foreach (var wcid in sources)
            {
                // Placed or spawned there, not merely a weenie whose Destination points at this variation: a portal that is
                // placed nowhere must not make a dungeon eject people (owner 2026-09-17).
                bool placed;
                lock (_lock)
                    placed = _sourceSeen.Contains(SourceKey(wcid, variation));

                if (!placed)
                    continue;

                if (DatabaseManager.World.GetCachedWeenie(wcid)?.WeenieType != WeenieType.Portal)
                    return 0;   // a plate is valid here: plate mode

                portalSource = wcid;
            }

            return portalSource;
        }

        /// <summary>
        /// Zone Share (owner 2026-09-23): the room source whose dungeon this spot is in AND that has Zone Share on
        /// (PropertyBool.RoomAssignZoneShare on its weenie), or 0. The dungeon is the source's landblock in a variation it is
        /// placed at - rooms, corridors and all, indoor cells only. Portal and plate sources alike.
        /// </summary>
        public static uint ZoneShareSourceAt(uint cell, int? variation) => SourceAt(cell, variation, IsZoneShareSource);

        /// <summary>True when this spot is inside a room dungeon (an indoor cell of its landblock, in a variation it is placed at). Door.ActOnUse: its walls never open for players.</summary>
        public static bool IsInRoomDungeon(Position position)
            => position != null && SourceAt(position.Cell, position.Variation, _ => true) != 0;

        /// <summary>Kill Reward (owner 2026-09-23): the room source whose dungeon this spot is in AND that has an active Kill Reward, or 0.</summary>
        public static uint KillRewardSourceAt(uint cell, int? variation) => SourceAt(cell, variation, wcid => KillRewardOf(wcid).Active);

        /// <summary>Kill Reward settings on a room source's weenie (PropertyString.RoomAssignKillReward); off when it has none.</summary>
        public static ACE.Server.Managers.ZoneControl.KillRewardConfig KillRewardOf(uint wcid)
        {
            string raw = null;
            DatabaseManager.World.GetCachedWeenie(wcid)?.PropertiesString?.TryGetValue(PropertyString.RoomAssignKillReward, out raw);
            return ACE.Server.Managers.ZoneControl.KillRewardConfig.Parse(raw);
        }

        /// <summary>
        /// The room source whose dungeon this spot is in - its landblock, in a variation it is placed at, indoor cells only -
        /// and that <paramref name="accept"/> takes, or 0. Portal and plate sources alike. Zone Share and Kill Reward.
        /// </summary>
        private static uint SourceAt(uint cell, int? variation, Func<uint, bool> accept)
        {
            EnsureRoomSourcesLoaded();
            if (_roomSourceCount == 0 || !IsRoomVariation(variation))
                return 0;

            if ((cell & 0xFFFF) < 0x0100)
                return 0;

            uint[] sources;
            lock (_lock)
            {
                if (!_sourcesByLandblock.TryGetValue((ushort)(cell >> 16), out var set))
                    return 0;
                sources = set.Where(wcid => _sourceSeen.Contains(SourceKey(wcid, variation))).ToArray();
            }

            foreach (var wcid in sources)
                if (accept(wcid))
                    return wcid;

            return 0;
        }

        /// <summary>Zone Share (2026-09-23): the source weenie carries RoomAssignZoneShare = true. Read off the weenie cache.</summary>
        public static bool IsZoneShareSource(uint wcid)
            => DatabaseManager.World.GetCachedWeenie(wcid)?.PropertiesBool is { } bools
               && bools.TryGetValue(PropertyBool.RoomAssignZoneShare, out var on) && on;

        // Built for every source on a landblock on each door use, trespass check and Zone Share / Kill Reward lookup: cached
        // per (wcid, variation), like Room.KeyFor, rather than formatted every time (review 2026-09-24).
        private static readonly ConcurrentDictionary<(uint Wcid, int? Variation), string> _sourceKeys = new ConcurrentDictionary<(uint, int?), string>();

        private static string SourceKey(uint wcid, int? variation)
            => _sourceKeys.GetOrAdd((wcid, VariationManager.NormalizeBase(variation)),
                k => $"{k.Wcid}|{k.Variation?.ToString(CultureInfo.InvariantCulture)}");

        /// <summary>Call with _lock held. Remembers that a source exists in a variation.</summary>
        private static void RememberSource(string key)
        {
            _sourceSeen.Add(key);
        }

        /// <summary>
        /// A room portal or plate copy was SPAWNED (/createinst, a generator, a summon - Portal/PressurePlate.EnterWorld) or
        /// a link spot set a portal's destination: its rooms exist in that variation from now on. Database placements do not
        /// come through here (landblock loading skips EnterWorld) - RememberPlacements reads those at startup.
        /// </summary>
        public static void OnSourceEnteredWorld(uint wcid, int? variation)
        {
            try
            {
                if (!IsRoomVariation(variation))
                    return;

                var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
                if (weenie == null)
                    return;

                uint sourceWcid;
                if (weenie.PropertiesString != null && weenie.PropertiesString.ContainsKey(PropertyString.RoomAssignRooms))
                    sourceWcid = wcid;
                else if (weenie.WeenieType != WeenieType.Portal || weenie.PropertiesDID == null || !weenie.PropertiesDID.TryGetValue(PropertyDataId.RoomAssignPlate, out sourceWcid))
                    return;

                lock (_lock)
                    RememberSource(SourceKey(sourceWcid, variation));
            }
            catch (Exception ex)
            {
                log.Error($"[RoomAssign] OnSourceEnteredWorld for wcid {wcid}: {ex}");
            }
        }

        /// <summary>
        /// Whether a source's rooms exist in this variation - from memory and the weenie cache (this runs on teleport and
        /// login; only a cache miss reaches the database). A plate source: where it is placed. A portal source: where its placed copies lead (a link spot or relative
        /// destination can differ from the weenie's own), or its weenie's Destination variation. Never the base world.
        /// </summary>
        private static bool IsSourceVariation(uint sourceWcid, int? variation)
        {
            if (!IsRoomVariation(variation))
                return false;

            lock (_lock)
                if (_sourceSeen.Contains(SourceKey(sourceWcid, variation)))
                    return true;

            var weenie = DatabaseManager.World.GetCachedWeenie(sourceWcid);

            return weenie?.WeenieType == WeenieType.Portal
                && weenie.PropertiesPosition != null && weenie.PropertiesPosition.TryGetValue(PositionType.Destination, out var destination)
                && VariationManager.NormalizeBase(destination.VariationId) == VariationManager.NormalizeBase(variation);
        }

        /// <summary>The room at a cell, in its source's variation; null anywhere else (base world included).</summary>
        private static RoomRef FindRoomAt(uint cell, int? variation)
        {
            EnsureRoomSourcesLoaded();

            if (_roomSourceCount == 0 || !IsRoomVariation(variation))
                return null;

            RoomRef[] candidates;
            lock (_lock)
            {
                if (!_roomByCell.TryGetValue(cell, out var refs))
                    return null;
                candidates = refs.ToArray();
            }

            // Outside _lock: IsSourceVariation takes _lock itself and may read the weenie cache.
            foreach (var candidate in candidates)
                if (IsSourceVariation(candidate.SourceWcid, variation))
                    return candidate;

            return null;
        }

        /// <summary>Call with _lock held.</summary>
        private static void RemoveReservation(string key)
        {
            if (_reservations.TryGetValue(key, out var reservation) && _reservedBy.TryGetValue(reservation.Guid, out var reservedKey) && reservedKey == key)
                _reservedBy.Remove(reservation.Guid);

            _reservations.Remove(key);
        }

        /// <summary>Call with _lock held. Removes this player's own reservation, if any.</summary>
        private static void RemoveOwnReservation(uint guid)
        {
            if (_reservedBy.TryGetValue(guid, out var key))
            {
                if (_reservations.TryGetValue(key, out var reservation) && reservation.Guid == guid)
                    _reservations.Remove(key);
                _reservedBy.Remove(guid);
            }
        }

        /// <summary>Call with _lock held. Spends every hold of the account.</summary>
        private static void SpendHolds(uint account)
        {
            List<string> theirs = null;
            foreach (var kv in _holds)
                if (kv.Value.Account == account)
                    (theirs ??= new List<string>()).Add(kv.Key);

            if (theirs != null)
                foreach (var key in theirs)
                    _holds.Remove(key);
        }

        /// <summary>A hold counts until its time, and not while its setting is 0 - it counts again if the setting comes back in time.</summary>
        private static bool IsHoldActive(Hold hold, DateTime now)
        {
            if (hold.Until <= now)
                return false;

            return hold.Kind == HoldKind.Logout ? LogoutHoldTime > TimeSpan.Zero : LeaveHoldTime > TimeSpan.Zero;
        }

        /// <summary>
        /// Call with _lock held. One hold per account: any other hold of the account is spent. Never overwrites a room held
        /// for another account. False when the room is held for another account.
        /// </summary>
        private static bool SetHold(string key, uint account, TimeSpan time, HoldKind kind, DateTime now, string name)
        {
            if (_holds.TryGetValue(key, out var existing) && existing.Account != account && IsHoldActive(existing, now))
                return false;

            // Only the account the room was handed to may hold it: someone who got inside another player's room (an unsealed
            // room, a cell missing from the list) must not be able to take it over by teleporting out or logging out there.
            if (_roomOwner.TryGetValue(key, out var owner) && owner != account)
                return false;

            SpendHolds(account);
            _holds[key] = new Hold { Account = account, Until = now + time, Kind = kind, Name = name };
            return true;
        }

        /// <summary>
        /// Call with _lock held. Reserves the room for the player's trip, replacing their own reservation. A committed
        /// reservation spends the account's holds (one room per account), and a FRESH hand-out grants logout-hold credit in
        /// this dungeon - unless the room was already the account's and its used-up record still stands (see below). A
        /// pending one (commit false) does neither until it is committed.
        /// </summary>
        private static void Reserve(uint guid, uint account, Room room, int? variation, DateTime now, bool fresh, string name, bool commit = true)
        {
            RemoveOwnReservation(guid);

            var key = room.Key(variation);
            _reservations[key] = new Reservation
            {
                Guid = guid, Account = account, Room = room, Variation = variation, CreatedUnix = Time.GetUnixTime(), Name = name,
                Until = now + (commit ? ReservationTime : PendingReservationTime),
                Pending = !commit, PendingFresh = !commit && fresh,
            };
            _reservedBy[guid] = key;

            if (!commit)
                return;

            // The room is theirs from here: only this account may hold it when it is left or logged out of.
            // The tenure clock starts only when the room changes hands. The same account coming back to the same room -
            // a recall out and back inside its leave hold, a relog inside its logout hold - keeps the clock it already
            // had, so "how long have they had this room" survives the trip out. A DIFFERENT room is a new hand-out and
            // gets its own clock (2026-09-22).
            var alreadyTheirs = _roomOwner.TryGetValue(key, out var previousOwner) && previousOwner == account;
            if (!alreadyTheirs)
                _roomOwnerSince[key] = now;

            _roomOwner[key] = account;

            // IsFreeFor let them have it, so another account's hold here is not counting (its setting is 0): it must not
            // come back over this player if the setting is turned on again.
            if (_holds.TryGetValue(key, out var staleHold) && staleHold.Account != account)
                _holds.Remove(key);

            SpendHolds(account);

            // Logout-hold credit (per account per dungeon) comes back with a fresh hand-out of a room that is new to the account,
            // or when the account has no record for this dungeon at all - it ended its visit (logged out outside every room) or
            // a restart wiped the table. Getting its own room back while a used-up record (0) still stands - a relog after the
            // credit ran out - refills nothing, so relogging forever does not keep a room (review 2026-09-24).
            // A record dropped while used up counts as still used up for one logout-hold length (_creditSpentAt).
            var creditKey = CreditKey(account, room, variation);
            var recentlySpent = _creditSpentAt.TryGetValue(creditKey, out var spentAt) && now - spentAt < LogoutHoldTime;
            if (fresh && (!alreadyTheirs || (!_logoutHoldCredit.ContainsKey(creditKey) && !recentlySpent)))
            {
                _logoutHoldCredit[creditKey] = LogoutHoldCredit;
                _creditSpentAt.Remove(creditKey);
            }
        }

        /// <summary>Call with _lock held.</summary>
        private static void PurgeExpired(DateTime now)
        {
            List<string> expired = null;

            foreach (var kv in _reservations)
                if (kv.Value.Until <= now)
                    (expired ??= new List<string>()).Add(kv.Key);

            if (expired != null)
            {
                foreach (var key in expired)
                    RemoveReservation(key);
                expired.Clear();
            }

            // By time only: a hold whose setting is 0 for a moment is ignored meanwhile (IsHoldActive), not deleted.
            foreach (var kv in _holds)
                if (kv.Value.Until <= now)
                    (expired ??= new List<string>()).Add(kv.Key);

            if (expired != null)
                foreach (var key in expired)
                    _holds.Remove(key);

            if (_lastFullMessage.Count > FullMessagePruneThreshold)
            {
                var stale = new List<uint>();
                foreach (var kv in _lastFullMessage)
                    if (now - kv.Value >= FullMessageInterval)
                        stale.Add(kv.Key);
                foreach (var guid in stale)
                    _lastFullMessage.Remove(guid);
            }

            // A used-up credit is only remembered for one logout-hold length.
            if (_creditSpentAt.Count > 0)
            {
                var holdTime = LogoutHoldTime;
                foreach (var key in _creditSpentAt.Where(kv => now - kv.Value >= holdTime).Select(kv => kv.Key).ToList())
                    _creditSpentAt.Remove(key);
            }
        }

        /// <summary>
        /// A reservation only bridges the gap until the player has landed. It is done once they have finished teleporting
        /// and stand in the room (occupancy protects it from then on), or once a teleport has started since the hand-out,
        /// has finished, and they are not in the room (they landed elsewhere). NOT while they are teleporting: Teleport()
        /// swaps Location to the destination for a moment and moves it for real mid-teleport, and another thread's older
        /// occupancy snapshot would not show them yet. A teleport that never starts is ended by the cap.
        /// </summary>
        private static bool ReservationDone(Reservation reservation, Player player, out bool inRoom)
        {
            inRoom = false;

            // Read the start time BEFORE Teleporting: Teleport() sets Teleporting first and the start time second, so reading
            // them in this order can never pair a new start time with a stale "not teleporting".
            var started = player.GetProperty(PropertyFloat.LastTeleportStartTimestamp);

            if (player.Teleporting)
                return false;

            if (InRoom(player.Location, reservation.Room, reservation.Variation))
            {
                inRoom = true;
                return true;
            }

            return started.HasValue && started.Value >= reservation.CreatedUnix;
        }

        private sealed class Occupancy
        {
            public readonly HashSet<string> Rooms = new HashSet<string>();
            public readonly HashSet<uint> Accounts = new HashSet<uint>();

            /// <summary>Only players holding a reservation - ReleaseDoneReservations needs those, not every online player.</summary>
            public readonly Dictionary<uint, Player> Holders = new Dictionary<uint, Player>();
        }

        /// <summary>
        /// Call with _lock held, with the occupancy built BEFORE the lock. A room released because its player now stands in
        /// it is added to the occupancy (room and account), so this caller cannot hand it out from a snapshot taken before
        /// that player arrived - except for the caller's own player, who is never an occupant against themselves.
        /// </summary>
        private static void ReleaseDoneReservations(Occupancy occupancy, uint excludeGuid)
        {
            if (_reservations.Count == 0)
                return;

            List<string> done = null;

            foreach (var kv in _reservations)
            {
                if (!occupancy.Holders.TryGetValue(kv.Value.Guid, out var player) || !ReservationDone(kv.Value, player, out var inRoom))
                    continue;

                (done ??= new List<string>()).Add(kv.Key);

                if (inRoom && kv.Value.Guid != excludeGuid && !IsStaff(player) && !IsCloaked(player))
                {
                    occupancy.Rooms.Add(kv.Key);
                    occupancy.Accounts.Add(kv.Value.Account);
                }
            }

            if (done != null)
                foreach (var key in done)
                    RemoveReservation(key);
        }

        /// <summary>
        /// Call with _lock held. True when this player is still on the way to a room - a repeat trigger (walking into a
        /// portal fires it several times before the teleport starts) must not reserve or teleport again. Releases their own
        /// finished or orphaned reservation first, so a stale one never blocks them.
        /// </summary>
        private static bool IsOnTheWay(Player player, uint guid)
        {
            if (!_reservedBy.TryGetValue(guid, out var key))
                return false;

            if (!_reservations.TryGetValue(key, out var reservation) || reservation.Guid != guid)
            {
                _reservedBy.Remove(guid);
                return false;
            }

            if (ReservationDone(reservation, player, out _))
            {
                RemoveReservation(key);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Call with _lock held. One room per account: true when another character of the account stands in a room of this
        /// dungeon or is on its way to a room.
        /// </summary>
        private static bool IsAccountBusy(uint guid, uint account, Occupancy occupancy)
        {
            if (occupancy.Accounts.Contains(account))
                return true;

            foreach (var reservation in _reservations.Values)
                if (reservation.Account == account && reservation.Guid != guid)
                    return true;

            return false;
        }

        /// <summary>Admins do not count as occupants, get no holds and are never moved or assigned by portals (owner rulings 2026-09-16).
        /// Internal for Zone Share, which follows the same Count Admin switch (owner 2026-09-23). By the ACCOUNT's access level
        /// only (review 2026-09-24): the character's saved IsAdmin flag survives a demotion, so it must not make anyone staff.</summary>
        internal static bool IsStaff(Player player)
            => IsStaff(player.Session?.AccessLevel ?? AccessLevel.Player);

        private static bool IsStaff(AccessLevel accessLevel)
            => !TestAdminCounts && accessLevel >= AccessLevel.Admin;   // TestAdminCounts: a test tool, false unless room_assign_test_tools is on

        /// <summary>Cloaked (On or Ghost): invisible to everyone else, so never an occupant and never a trespasser.</summary>
        internal static bool IsCloaked(Player player)
            => player.CloakStatus == CloakStatus.On || player.CloakStatus == CloakStatus.Ghost;

        private static uint AccountOf(Player player) => player.Account?.AccountId ?? player.Session?.AccountId ?? 0;

        private static bool InRoom(Position location, Room room, int? variation)
            => location != null && room.Cells.Contains(location.Cell) && VariationManager.SameVariationForVisibility(location.Variation, variation);

        private static Room CurrentRoom(Player player, List<Room> rooms, int? variation)
        {
            var location = player.Location;
            foreach (var room in rooms)
                if (InRoom(location, room, variation))
                    return room;
            return null;
        }

        /// <summary>
        /// Rooms with a non-staff player standing in them, and those players' accounts - dead players count (a room must
        /// not be sniped between a death and the death teleport, which then leaves a hold), and so do teleporting players
        /// (Location is already the destination). A client that never finishes loading is landed by the server's
        /// portal-stuck recovery (portal_stuck_recovery_seconds). Built OUTSIDE _lock: GetAllOnline takes PlayerManager's own
        /// lock. Players on other landblocks are skipped before any room test.
        /// </summary>
        private static Occupancy BuildOccupancy(List<Room> rooms, int? variation, uint excludeGuid)
        {
            var occupancy = new Occupancy();
            var landblock = rooms.Count > 0 ? rooms[0].LandingCell >> 16 : 0;

            // The guids worth keeping a Player for: usually none, never more than one per room.
            HashSet<uint> holders = null;
            lock (_lock)
                foreach (var reservation in _reservations.Values)
                    (holders ??= new HashSet<uint>()).Add(reservation.Guid);

            foreach (var player in PlayerManager.GetAllOnline())
            {
                var guid = player.Guid.Full;

                if (holders != null && holders.Contains(guid))
                    occupancy.Holders[guid] = player;

                if (guid == excludeGuid)
                    continue;

                // On their way out: they are saved where they stand, but the room is not theirs to block once the save lands.
                if (player.IsLoggingOut && player.CurrentLandblock == null)
                    continue;

                var location = player.Location;
                if (location == null || location.Cell >> 16 != landblock)
                    continue;

                foreach (var room in rooms)
                {
                    if (InRoom(location, room, variation))
                    {
                        // Staff and anyone cloaked (a Sentinel drifting through walls, review 2026-09-24) never make a room
                        // look taken. Checked only for players actually in a room.
                        if (!IsStaff(player) && !IsCloaked(player))
                        {
                            occupancy.Rooms.Add(room.Key(variation));
                            occupancy.Accounts.Add(AccountOf(player));
                        }
                        break;
                    }
                }
            }

            TestAddFakes(occupancy);   // test tool: no-op unless room_assign_test_tools is on

            return occupancy;
        }

        /// <summary>Call with _lock held. No other account stands in it, is on the way to it, or holds it.</summary>
        private static bool IsFreeFor(Room room, int? variation, uint account, Occupancy occupancy, DateTime now)
        {
            var key = room.Key(variation);

            if (occupancy.Rooms.Contains(key))
                return false;

            if (_reservations.TryGetValue(key, out var reservation) && reservation.Account != account)
                return false;

            if (_holds.TryGetValue(key, out var hold) && hold.Account != account && IsHoldActive(hold, now))
                return false;

            return true;
        }

        /// <summary>
        /// Call with _lock held. The room this player gets, in order (owner rulings 2026-09-16): the room reserved for them
        /// (a recall reserves at cast), the room they stand in (a recall from inside a room is no way to jump rooms), the
        /// room held for their account (always back to their own room while it is free), then the room their account was
        /// last handed (2026-09-20), then the first free room - the last two are FRESH hand-outs. Null when none is free.
        /// </summary>
        private static Room PickRoom(List<Room> rooms, int? variation, uint guid, uint account, Occupancy occupancy, Room current, DateTime now, out bool fresh, HashSet<Room> skip = null)
        {
            fresh = false;

            if (_reservedBy.TryGetValue(guid, out var reservedKey) && _reservations.TryGetValue(reservedKey, out var reservation) && reservation.Guid == guid)
                foreach (var room in rooms)
                    if (room.Key(variation) == reservedKey && skip?.Contains(room) != true && IsFreeFor(room, variation, account, occupancy, now))
                    {
                        // A pending reservation keeps whether it was a fresh hand-out; a committed one already counted.
                        fresh = reservation.Pending && reservation.PendingFresh;
                        return room;
                    }

            if (current != null && skip?.Contains(current) != true && IsFreeFor(current, variation, account, occupancy, now))
                return current;

            foreach (var room in rooms)
                if (skip?.Contains(room) != true && _holds.TryGetValue(room.Key(variation), out var hold) && hold.Account == account && IsHoldActive(hold, now) && IsFreeFor(room, variation, account, occupancy, now))
                    return room;

            // The room this account was last handed, while nobody else has taken it: a player back after their hold ran
            // out (a long run from the lifestone) returns to their own room and corpse, not to room 1. It does not keep the
            // room from the next arrival - only a hold does that. Fresh like any first-free room.
            foreach (var room in rooms)
                if (skip?.Contains(room) != true && _roomOwner.TryGetValue(room.Key(variation), out var lastOwner) && lastOwner == account
                    && IsFreeFor(room, variation, account, occupancy, now))
                {
                    fresh = true;
                    return room;
                }

            foreach (var room in rooms)
            {
                if (skip?.Contains(room) != true && IsFreeFor(room, variation, account, occupancy, now))
                {
                    fresh = true;
                    return room;
                }
            }

            return null;
        }

        /// <summary>
        /// The landing position for a room, carrying the variation (a Position without one lands in BASE), corrected by
        /// AdjustDungeon. Null when the correction leaves the room's cell list: occupancy would not see a player standing
        /// there, so the room would be handed out twice - it is not used until the room data is fixed (warned once per list).
        /// </summary>
        private static Position Landing(Room room, int? variation, uint sourceWcid)
        {
            var destination = new Position(room.LandingCell, room.X, room.Y, room.Z, room.QX, room.QY, room.QZ, room.QW, false, variation);
            WorldObject.AdjustDungeon(destination);

            if (room.Cells.Contains(destination.Cell))
                return destination;

            bool warn;
            lock (_lock)
                warn = _badLandingWarned.Add(room);

            if (warn)
                log.Warn($"[RoomAssign] wcid {sourceWcid} room {room.Number}: landing cell 0x{room.LandingCell:X8} is adjusted to 0x{destination.Cell:X8}, which is not in the room's cell list - the room is not handed out until the room data is fixed.");

            return null;
        }


        private enum ClaimResult { Claimed, NoRoom, AccountBusy, OnTheWay }

        /// <summary>
        /// Picks and reserves a room in two steps, so nothing is reserved (and no hold or credit changed) until the landing
        /// has been computed: pick under _lock, compute the landing outside it, then reserve under _lock only if the room is
        /// still free for them. player is null for a login (no player object yet): <paramref name="loginName"/> then names the
        /// character on the reservation, for the Rooms tab.
        /// </summary>
        private static ClaimResult ClaimRoom(Player player, uint guid, uint account, List<Room> rooms, int? variation, uint sourceWcid,
            Occupancy occupancy, Room current, DateTime now, out Room room, out Position landing, string loginName = null)
        {
            room = null;
            landing = null;
            bool fresh;
            HashSet<Room> skip = null;

            lock (_lock)
            {
                PurgeExpired(now);
                ReleaseDoneReservations(occupancy, guid);

                // A pending reservation of their own for THIS dungeon is what the pick uses; a trip anywhere else blocks.
                if (player != null && IsOnTheWay(player, guid) && _reservedBy.TryGetValue(guid, out var pendingKey) && !rooms.Any(r => r.Key(variation) == pendingKey))
                    return ClaimResult.OnTheWay;

                if (IsAccountBusy(guid, account, occupancy))
                    return ClaimResult.AccountBusy;

                room = PickRoom(rooms, variation, guid, account, occupancy, current, now, out fresh);
                if (room == null)
                    return ClaimResult.NoRoom;
            }

            // Each pass computes the landing outside the lock, then reserves only if the room is still free for them. A room
            // another thread took meanwhile, or whose landing is bad, is skipped and the next pick tried - at most once per room.
            for (var attempt = 0; attempt <= rooms.Count; attempt++)
            {
                landing = Landing(room, variation, sourceWcid);

                lock (_lock)
                {
                    if (IsAccountBusy(guid, account, occupancy))
                    {
                        room = null;
                        landing = null;
                        return ClaimResult.AccountBusy;
                    }

                    if (landing != null && IsFreeFor(room, variation, account, occupancy, now))
                    {
                        Reserve(guid, account, room, variation, now, fresh, player?.Name ?? loginName);
                        return ClaimResult.Claimed;
                    }

                    (skip ??= new HashSet<Room>()).Add(room);
                    room = PickRoom(rooms, variation, guid, account, occupancy, current, now, out fresh, skip);
                    if (room == null)
                        break;
                }
            }

            room = null;
            landing = null;
            return ClaimResult.NoRoom;
        }

        /// <summary>
        /// Call with _lock held. True within the message interval after this player was refused: a repeat trigger (collisions
        /// fire every physics step) is refused silently BEFORE the occupancy scan, which walks every online player.
        /// </summary>
        private static bool RecentlyRefused(uint guid, DateTime now)
            => _lastFullMessage.TryGetValue(guid, out var last) && now - last < FullMessageInterval;

        /// <summary>Call with _lock held. Say a refusal at most once per interval per player.</summary>
        private static bool ShouldSayFull(uint guid, DateTime now)
        {
            if (_lastFullMessage.TryGetValue(guid, out var last) && now - last < FullMessageInterval)
                return false;

            _lastFullMessage[guid] = now;
            return true;
        }

        private static void Say(Player player, string message)
            => player?.Session?.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.Broadcast));

        private const string MessageNotYourChamber = "That chamber belongs to someone else.";

        /// <summary>
        /// WorldObject.UpdatePosition for a player moving into a new cell (not teleporting): true when that cell is part of a
        /// chamber that is not theirs - so the move is refused like a wall and the client snaps back (review 2026-09-24).
        /// The chamber walls are only models: a jump over a wall lower than the ceiling, a client that sends positions
        /// through one, or any non-chamber portal or tie that lands inside would otherwise put a player in someone else's
        /// chamber, where they count as its occupant. One lookup on a hot path: nothing below v3, nothing outside a room cell.
        ///
        /// Theirs = their account owns it (was handed it), has it reserved (on the way in), or holds it. Staff and cloaked
        /// characters are never stopped. Never throws: a failure lets the move through.
        /// </summary>
        public static bool IsTrespass(Player player, Position to)
        {
            try
            {
                if (player == null || to == null || _roomSourceCount == 0 || !IsRoomVariation(to.Variation) || (to.Cell & 0xFFFF) < 0x0100)
                    return false;

                // A corridor or any other non-room cell: no lock taken.
                if (!_roomCells.ContainsKey(to.Cell))
                    return false;

                var found = FindRoomAt(to.Cell, to.Variation);
                if (found == null || IsStaff(player) || IsCloaked(player))
                    return false;

                var key = found.Room.Key(to.Variation);

                // Moving between cells of the room they already stand in is never stopped (review 2026-09-24): whoever is
                // inside - staff who turned Count Admin on, a Sentinel who de-cloaked, anyone who got in some other way -
                // can always walk about and out, never be frozen in place.
                var from = player.Location;
                if (from != null && _roomCells.ContainsKey(from.Cell))
                {
                    var current = FindRoomAt(from.Cell, from.Variation);
                    if (current != null && current.Room.Key(from.Variation) == key)
                        return false;
                }
                var account = AccountOf(player);
                var now = DateTime.UtcNow;

                bool theirs;
                lock (_lock)
                    theirs = (_roomOwner.TryGetValue(key, out var owner) && owner == account)
                        || (_reservations.TryGetValue(key, out var reservation) && reservation.Account == account)
                        || (_holds.TryGetValue(key, out var hold) && hold.Account == account && IsHoldActive(hold, now));

                if (theirs)
                    return false;

                if (ACE.Server.Diagnostics.LogRateLimiter.ShouldEmit($"roomtrespass:{player.Guid.Full}", TimeSpan.FromSeconds(10), out _))
                {
                    log.Warn($"[RoomAssign] {player.Name} (0x{player.Guid}, acct {account}) tried to enter room {found.Room.Number} of wcid {found.SourceWcid} (cell 0x{to.Cell:X8} v:{to.Variation}) without a claim - refused.");
                    Say(player, MessageNotYourChamber);
                }

                return true;
            }
            catch (Exception ex)
            {
                log.Error($"[RoomAssign] IsTrespass for {player?.Name}: {ex}");
                return false;
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // The plate
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Same as Portal's minTimeSinceLastPortal.</summary>
        private const double PlateMinTimeSinceLastPortal = 3.5;

        /// <summary>Owner 2026-09-17: a room plate's arming window is 1 s unless its weenie sets PressurePlateCooldown (50502).</summary>
        private const double RoomPlateDefaultCooldown = 1.0;

        /// <summary>
        /// A player moved on a room plate. Runs in place of the stock plate activation, which would send "used too
        /// recently" / level messages, start cooldowns and warn about a missing activation target. Admins get a room too
        /// (owner ruling), but never count as occupants.
        /// </summary>
        public static void OnPlateStep(PressurePlate plate, Player player, List<Room> rooms)
        {
            try
            {
                var variation = plate.Location?.Variation;

                if (rooms.Count == 0 || player.Session == null || player.IsLoggingOut || !IsRoomVariation(variation))
                    return;

                // Same rule as Portal.OnCollideObject: a player in another variation never triggers this plate.
                if (!VariationManager.SameVariationForVisibility(player.Location?.Variation, variation))
                    return;

                var guid = player.Guid.Full;
                var account = AccountOf(player);
                var now = DateTime.UtcNow;

                // Cheap checks first - this fires many times a second while someone moves on the plate. The arming window
                // (PressurePlateCooldown, 50502) is plate-wide and silent.
                if (!plate.IsArmed(now, RoomPlateDefaultCooldown))
                    return;

                // The gates every portal applies (Portal.CheckUseRequirements): no escape from a PK fight, no chained teleports.
                if (player.Teleporting)
                    return;

                if (player.LastPortalTeleportTimestamp != null && Time.GetUnixTime() - player.LastPortalTeleportTimestamp.Value < PlateMinTimeSinceLastPortal)
                    return;

                if (player.PKTimerActive)
                {
                    bool sayPk;
                    lock (_lock)
                        sayPk = ShouldSayFull(guid, now);

                    if (sayPk)
                        player.Session.Network.EnqueueSend(new GameEventWeenieError(player.Session, WeenieError.YouHaveBeenInPKBattleTooRecently));
                    return;
                }

                lock (_lock)
                {
                    // Cheapest first: a refused player and a player already on their way cost nothing more.
                    if (RecentlyRefused(guid, now))
                        return;

                    PurgeExpired(now);

                    if (IsOnTheWay(player, guid))
                        return;
                }

                var occupancy = BuildOccupancy(rooms, variation, guid);
                var result = ClaimRoom(player, guid, account, rooms, variation, plate.WeenieClassId, occupancy, null, now, out var room, out var landing);

                if (result == ClaimResult.OnTheWay)
                    return;

                if (result != ClaimResult.Claimed)
                {
                    bool say;
                    lock (_lock)
                        say = ShouldSayFull(guid, now);

                    if (say)
                        Say(player, result == ClaimResult.AccountBusy ? MessageAccountHasRoom : MessagePlateAllTaken);
                    return;
                }

                // Only a hand-out uses the arming window - a step that finds every room taken must not lock others out.
                plate.LastUseTime = now;

                lock (_lock)
                    RememberSource(SourceKey(plate.WeenieClassId, variation));

                player.EnqueueBroadcast(new GameMessageSound(player.Guid, plate.UseSound));

                // fromPortal: this counts as a portal teleport, so the plate's own "no chained teleports" gate is armed by it.
                WorldManager.ThreadSafeTeleport(player, landing, null, true);
                Say(player, MessageSentToRoom(plate.WeenieClassId, room.Number));

                log.Debug($"[RoomAssign] {player.Name} (0x{player.Guid}) sent by plate {plate.WeenieClassId} to room {room.Number}.");
            }
            catch (Exception ex)
            {
                log.Error($"[RoomAssign] OnPlateStep for {player?.Name}, plate {plate?.WeenieClassId}: {ex}");
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Room portals: every way in lands straight in a room (owner 2026-09-16)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The rooms behind a room portal. Null (a normal portal) when the portal carries no room list and points at no
        /// plate, leads below variation 3 (retail), or the player is staff (admins use the portal's normal destination). notReady
        /// when it IS a room portal but its list has never parsed - it must refuse, not act as a normal portal. The source
        /// is the portal itself when it carries the list, else the plate its RoomAssignPlate points at.
        /// </summary>
        private static List<Room> GetPortalRooms(Player player, uint portalWcid, int? variation, out uint sourceWcid, out bool notReady)
        {
            sourceWcid = 0;
            notReady = false;

            var weenie = DatabaseManager.World.GetCachedWeenie(portalWcid);
            if (weenie == null)
                return null;

            if (weenie.PropertiesString != null && weenie.PropertiesString.ContainsKey(PropertyString.RoomAssignRooms))
                sourceWcid = portalWcid;
            else if (weenie.PropertiesDID == null || !weenie.PropertiesDID.TryGetValue(PropertyDataId.RoomAssignPlate, out sourceWcid))
                return null;

            if (player == null || !IsRoomVariation(variation) || IsStaff(player))
                return null;

            var rooms = GetRooms(sourceWcid);
            if (rooms == null || rooms.Count == 0)
            {
                notReady = true;
                return null;
            }

            return rooms;
        }

        /// <summary>
        /// Before a room portal lets a player in: from Portal.CheckUseRequirements for a PLACED portal (use, walk-in,
        /// summoned gateway - before activation emotes and cooldowns run) and from portal recall before its 2 s delay. Not
        /// for Portal Tie / Summon, which must work while the rooms are full. False when every room is taken, the account
        /// already has a room, the player is already on the way to one, or the portal is not ready. Messages are sent at
        /// most once a second when throttled (walk-in re-fires); a recall (not throttled) is always told why. With reserve,
        /// the room is reserved now - a recall does this at cast, so no one takes the last room during its delay.
        /// </summary>
        public static bool CheckPortalHasRoom(Player player, uint portalWcid, Position destination, bool throttle, bool reserve = false)
        {
            var isRoomPortal = false;

            try
            {
                var variation = destination?.Variation;
                var rooms = GetPortalRooms(player, portalWcid, variation, out _, out var notReady);

                if (rooms == null && !notReady)
                    return true;

                isRoomPortal = true;

                var guid = player.Guid.Full;
                var now = DateTime.UtcNow;
                string refusal;

                if (notReady)
                    refusal = MessageNotReady;
                else if (player.IsLoggingOut)
                    return false;
                else
                {
                    bool onTheWay;
                    bool recentlyRefused;
                    lock (_lock)
                    {
                        PurgeExpired(now);
                        onTheWay = IsOnTheWay(player, guid);
                        recentlyRefused = throttle && RecentlyRefused(guid, now);
                    }

                    if (onTheWay)
                    {
                        if (!throttle)
                            Say(player, MessageOnTheWay);
                        return false;
                    }

                    if (recentlyRefused)
                        return false;

                    var account = AccountOf(player);
                    var occupancy = BuildOccupancy(rooms, variation, guid);
                    var current = CurrentRoom(player, rooms, variation);

                    lock (_lock)
                    {
                        ReleaseDoneReservations(occupancy, guid);

                        if (IsAccountBusy(guid, account, occupancy))
                            refusal = MessageAccountHasRoom;
                        else
                        {
                            var room = PickRoom(rooms, variation, guid, account, occupancy, current, now, out var fresh);
                            if (room != null)
                            {
                                // Pending: holds and credit change only when the teleport commits it (AssignPortalRoom).
                                if (reserve)
                                    Reserve(guid, account, room, variation, now, fresh, player?.Name, commit: false);
                                return true;
                            }

                            refusal = MessagePortalAllTaken;
                        }
                    }
                }

                var say = true;
                if (throttle)
                    lock (_lock)
                        say = ShouldSayFull(guid, now);

                if (say)
                    Say(player, refusal);
                return false;
            }
            catch (Exception ex)
            {
                // Same policy as AssignPortalRoom: an ordinary portal is never blocked by this code; a room portal refuses.
                log.Error($"[RoomAssign] CheckPortalHasRoom for {player?.Name}, portal {portalWcid}: {ex}");

                if (isRoomPortal && player != null)
                    lock (_lock)
                        RemoveOwnReservation(player.Guid.Full);

                return !isRoomPortal;
            }
        }

        /// <summary>
        /// This player's PENDING reservation as an opaque token, or null (none, or it is already committed). The token is the
        /// reservation instance itself, compared by reference: every Reserve makes a new one, so a caller can tell "the
        /// reservation this activation made" from an earlier one, even for the same room.
        /// </summary>
        public static object PendingReservationToken(Player player)
        {
            if (player == null || _roomSourceCount == 0)
                return null;

            var guid = player.Guid.Full;
            lock (_lock)
                return _reservedBy.TryGetValue(guid, out var key) && _reservations.TryGetValue(key, out var reservation)
                    && reservation.Guid == guid && reservation.Pending ? reservation : null;
        }

        /// <summary>
        /// Drops a player's PENDING reservation (made by CheckPortalHasRoom with reserve) when the trip it was made for does
        /// not happen - a failed use requirement, a portal out of uses. A committed reservation is left alone.
        /// </summary>
        public static void CancelPendingReservation(Player player)
        {
            if (player == null)
                return;

            try
            {
                var guid = player.Guid.Full;

                lock (_lock)
                    if (_reservedBy.TryGetValue(guid, out var key) && _reservations.TryGetValue(key, out var reservation) && reservation.Guid == guid && reservation.Pending)
                        RemoveOwnReservation(guid);
            }
            catch (Exception ex)
            {
                log.Error($"[RoomAssign] CancelPendingReservation for {player.Name}: {ex}");
            }
        }

        public enum PortalAssign { NotRoomPortal, Assigned, Refused }

        /// <summary>
        /// At the moment a room portal or recall teleports. NotRoomPortal: use the portal's normal destination. Assigned:
        /// teleport to landing. Refused: do NOT teleport - the player stays where they are (owner 2026-09-16); the reason has
        /// been said and their own reservation dropped. Refused covers a player logging out, the last room going to someone
        /// else in the meantime, the account already having a room, and a portal that is not ready.
        /// </summary>
        public static PortalAssign AssignPortalRoom(Player player, uint portalWcid, Position destination, out Position landing, out string arrivalLine)
        {
            landing = null;
            arrivalLine = null;
            var guid = player?.Guid.Full ?? 0;
            var isRoomPortal = false;

            try
            {
                var variation = destination?.Variation;
                var rooms = GetPortalRooms(player, portalWcid, variation, out var sourceWcid, out var notReady);

                if (notReady)
                {
                    Say(player, MessageNotReady);
                    return PortalAssign.Refused;
                }

                if (rooms == null)
                    return PortalAssign.NotRoomPortal;

                isRoomPortal = true;

                // Dead: a recall whose 2 s delay outlived the player must not carry the corpse into a room.
                if (player.IsLoggingOut || player.Session == null || player.CurrentLandblock == null || player.IsDead)
                {
                    lock (_lock)
                        RemoveOwnReservation(guid);
                    return PortalAssign.Refused;
                }

                var account = AccountOf(player);
                var now = DateTime.UtcNow;
                var occupancy = BuildOccupancy(rooms, variation, guid);
                var current = CurrentRoom(player, rooms, variation);

                var result = ClaimRoom(player, guid, account, rooms, variation, sourceWcid, occupancy, current, now, out var room, out landing);

                if (result != ClaimResult.Claimed)
                {
                    // OnTheWay: their reservation belongs to a trip still under way to another dungeon - keep it.
                    if (result != ClaimResult.OnTheWay)
                        lock (_lock)
                            RemoveOwnReservation(guid);

                    Say(player, result == ClaimResult.AccountBusy ? MessageAccountHasRoom : result == ClaimResult.OnTheWay ? MessageOnTheWay : MessagePortalAllTaken);
                    return PortalAssign.Refused;
                }

                // Remember the variation this source was used with: it scopes a plate that is not a database placement, and a
                // portal whose destination comes from a link spot.
                lock (_lock)
                    RememberSource(SourceKey(sourceWcid, variation));

                // arrivalLine stays null when the player is being put back in the room they are still inside, so spamming a
                // gateway into your own chamber does not repeat "opens Chamber 5 to you" on every use (owner 2026-09-22).
                // Walking back into the room they last walked out of says "Welcome back" instead (owner 2026-09-23). A room
                // that is new to them still gets the full line, including a re-entry after their hold lapsed and they were
                // handed another. Both lines are built BEFORE the lock: the arrival line reads the weenie cache, which may go to
                // the database, and nothing inside _lock does database work (review 2026-09-24).
                var stillYoursLine = MessageStillYours(room.Number);
                var sentToRoomLine = MessageSentToRoom(portalWcid, room.Number);

                lock (_lock)
                {
                    var key = room.Key(variation);
                    if (!_announcedRoom.TryGetValue(guid, out var announced) || announced != key)
                    {
                        arrivalLine = _leftRoom.TryGetValue(guid, out var left) && left == key ? stillYoursLine : sentToRoomLine;
                        _announcedRoom[guid] = key;
                    }
                    _leftRoom.Remove(guid);
                }

                log.Debug($"[RoomAssign] {player.Name} (0x{player.Guid}) sent by portal {portalWcid} to room {room.Number}.");
                return PortalAssign.Assigned;
            }
            catch (Exception ex)
            {
                log.Error($"[RoomAssign] AssignPortalRoom for {player?.Name}, portal {portalWcid}: {ex}");

                // An error before the portal is known to be a room portal must not block an ordinary portal.
                if (!isRoomPortal)
                    return PortalAssign.NotRoomPortal;

                lock (_lock)
                    RemoveOwnReservation(guid);

                return PortalAssign.Refused;
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Teleport, logout and login
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// WorldObject.Teleport for a player, after fog deferral and BEFORE Location moves. A player teleporting OUT of a
        /// room - recall, portal, the death teleport to the lifestone - keeps it for the leave hold (owner 2026-09-23: 120 s),
        /// so recalling out and back in returns them to the same room. Not when teleporting back into the same room, and not
        /// when already on the way to a different room (one room per account - that room is theirs now).
        /// </summary>
        public static void OnPlayerTeleportStart(Player player, Position destination)
        {
            try
            {
                // Every teleport on the server comes through here: nothing more unless a room source exists.
                EnsureRoomSourcesLoaded();
                if (_roomSourceCount == 0)
                    return;

                var location = player?.Location;
                if (location == null || !IsRoomVariation(location.Variation))
                    return;

                var holdTime = LeaveHoldTime;
                if (holdTime <= TimeSpan.Zero || IsStaff(player))
                    return;

                var found = FindRoomAt(location.Cell, location.Variation);
                if (found == null)
                    return;

                if (destination != null && found.Room.Cells.Contains(destination.Cell)
                    && VariationManager.NormalizeBase(destination.Variation) == VariationManager.NormalizeBase(location.Variation))
                    return;

                var guid = player.Guid.Full;
                var key = found.Room.Key(location.Variation);

                // Past the guard above, this teleport really does LEAVE the room, so the next arrival is worth announcing
                // again. A trip that lands back in the same room - spamming a gateway from inside your own chamber - takes
                // the early return and keeps the announcement, which is what stops it repeating (owner 2026-09-22).
                // Remember the room they walked out of: coming back to it is a "Welcome back", not a fresh arrival. It is
                // the room they stand in, not the last one announced, so a room they logged in to counts too.
                lock (_lock)
                {
                    _leftRoom[guid] = key;
                    _announcedRoom.Remove(guid);
                }

                var now = DateTime.UtcNow;
                bool held;

                lock (_lock)
                {
                    PurgeExpired(now);

                    // Already travelling to another room (committed): that room is theirs now. A PENDING reservation (a recall
                    // still in its delay) changes nothing until it commits, so this room is still held.
                    if (_reservedBy.TryGetValue(guid, out var reservedKey) && reservedKey != key
                        && _reservations.TryGetValue(reservedKey, out var trip) && trip.Guid == guid && !trip.Pending)
                        return;

                    held = SetHold(key, AccountOf(player), holdTime, HoldKind.Leave, now, player.Name);
                }

                if (held)
                    log.Debug($"[RoomAssign] {player.Name} (0x{player.Guid}) left room {found.Room.Number} - held for {holdTime.TotalSeconds:0} seconds.");
            }
            catch (Exception ex)
            {
                log.Error($"[RoomAssign] OnPlayerTeleportStart for {player?.Name}: {ex}");
            }
        }

        /// <summary>
        /// Top of Player.FinalizeLogout, while Location is still the spot the character is saved at. Every way out of the game
        /// ends there (logout, crash timeout, forced logoff, shutdown). Drops the character's pending reservation. Inside a
        /// room, holds it for the logout hold while the account has logout-hold credit. Anywhere else, the account's credit
        /// ends unless it still holds a room.
        /// </summary>
        public static void OnLogout(Player player)
        {
            try
            {
                EnsureRoomSourcesLoaded();
                if (_roomSourceCount == 0)
                    return;

                var guid = player.Guid.Full;
                var account = AccountOf(player);
                var location = player.Location;
                var now = DateTime.UtcNow;

                var found = location != null && !player.IsDead && !IsStaff(player) ? FindRoomAt(location.Cell, location.Variation) : null;

                var held = false;
                var renewed = false;
                var creditLeft = 0;
                var holdTime = LogoutHoldTime;

                lock (_lock)
                {
                    PurgeExpired(now);
                    RemoveOwnReservation(guid);

                    // Logging out ends the visit: coming back in is a real arrival and says so, whatever room they get.
                    // It also keeps this from holding a guid for a character who is gone.
                    _announcedRoom.Remove(guid);
                    _leftRoom.Remove(guid);

                    if (found == null)
                    {
                        if (!_holds.Values.Any(h => h.Account == account))
                            DropCredit(account, now);
                        return;
                    }

                    if (holdTime <= TimeSpan.Zero)
                        return;

                    var creditKey = CreditKey(account, found.Room, location.Variation);
                    _logoutHoldCredit.TryGetValue(creditKey, out creditLeft);

                    // Their account already has a live logout hold on this room: this is a second FinalizeLogout for the same
                    // logout (a forced logoff after a stuck save). Keep the hold; spend no credit.
                    var roomKey = found.Room.Key(location.Variation);
                    if (_holds.TryGetValue(roomKey, out var existing) && existing.Account == account && existing.Kind == HoldKind.Logout && IsHoldActive(existing, now))
                    {
                        existing.Until = now + holdTime;
                        renewed = true;
                    }
                    else if (creditLeft > 0)
                    {
                        held = SetHold(roomKey, account, holdTime, HoldKind.Logout, now, player.Name);

                        if (held)
                        {
                            // Kept at 0 when used up - "used up", not "no record" - so a relog cannot refill it (see Reserve).
                            creditLeft--;
                            _logoutHoldCredit[creditKey] = creditLeft;
                        }
                    }
                }

                if (renewed)
                    log.Info($"[RoomAssign] {player.Name} (0x{player.Guid}) logged out again in room {found.Room.Number} - its hold kept, no renewal spent.");
                else if (held)
                    log.Info($"[RoomAssign] {player.Name} (0x{player.Guid}) logged out in room {found.Room.Number} - held for {holdTime.TotalMinutes.ToString("0.##", CultureInfo.InvariantCulture)} minutes ({creditLeft} renewal(s) left).");
                else if (creditLeft > 0)
                    log.Info($"[RoomAssign] {player.Name} (0x{player.Guid}) logged out in room {found.Room.Number} - no hold, another account holds or owns it.");
                else
                    log.Info($"[RoomAssign] {player.Name} (0x{player.Guid}) logged out in room {found.Room.Number} - no hold, renewals used up since the last fresh hand-out.");
            }
            catch (Exception ex)
            {
                log.Error($"[RoomAssign] OnLogout for {player?.Name}: {ex}");
            }
        }

        private static void WriteLocation(ACE.Entity.Models.PropertiesPosition location, Position position, int? variation)
        {
            location.ObjCellId = position.Cell;
            location.PositionX = position.PositionX;
            location.PositionY = position.PositionY;
            location.PositionZ = position.PositionZ;
            location.RotationX = position.RotationX;
            location.RotationY = position.RotationY;
            location.RotationZ = position.RotationZ;
            location.RotationW = position.RotationW;
            location.VariationId = variation;
        }

        /// <summary>
        /// Rewrites the saved location to the character's lifestone - where death also sends them (Sanctuary, else
        /// Instantiation). With neither, the same fallback DoPlayerEnterWorld uses for a character with no location: leaving
        /// them in a room they hold no claim on would let two accounts share it.
        /// </summary>
        private static void MoveToLifestone(ACE.Entity.Models.Biota biota, ACE.Entity.Models.PropertiesPosition location)
        {
            if (!biota.PropertiesPosition.TryGetValue(PositionType.Sanctuary, out var lifestone))
                biota.PropertiesPosition.TryGetValue(PositionType.Instantiation, out lifestone);

            if (lifestone != null)
            {
                WriteLocation(location, lifestone);
                return;
            }

            WriteLocation(location, new Position(0xA9B40019, 84, 7.1f, 94, 0, 0, -0.0784591f, 0.996917f), null);
        }

        private static void WriteLocation(ACE.Entity.Models.PropertiesPosition location, ACE.Entity.Models.PropertiesPosition source)
        {
            location.ObjCellId = source.ObjCellId;
            location.PositionX = source.PositionX;
            location.PositionY = source.PositionY;
            location.PositionZ = source.PositionZ;
            location.RotationX = source.RotationX;
            location.RotationY = source.RotationY;
            location.RotationZ = source.RotationZ;
            location.RotationW = source.RotationW;
            location.VariationId = source.VariationId;
        }

        /// <summary>
        /// WorldManager.DoPlayerEnterWorld, before the player object is built. Rewrites the SAVED location in place, like
        /// RiftManager.HandleLoginInRiftInstance, and returns the chat message for a moved character (null when they stay).
        /// - Not in a room (or an Olthoi logging in at the lifestone): the account's holds are spent - they are elsewhere.
        /// - Outside every room of a portal-only dungeon: a room picked like any hand-out (HandleLoginOutsideRooms).
        /// - In a room the account holds (or this character had reserved), free for them: they stay; the room is reserved
        ///   for them while they load in.
        /// - In a room with no claim that nobody else has (a restart wiped the holds, or a hold ran out): they stay, as a
        ///   new hand-out (owner 2026-09-23).
        /// - The room is someone else's now: a room picked like any hand-out - the account's held room, the room it was last
        ///   handed, else the first free one (reserved).
        /// - Lifestone only when every room is taken, or another character of the account already has a room.
        /// </summary>
        public static string HandleLogin(ACE.Entity.Models.Biota biota, AccessLevel accessLevel, uint account)
        {
            try
            {
                EnsureRoomSourcesLoaded();
                if (_roomSourceCount == 0)
                    return null;

                if (biota?.PropertiesPosition == null || !biota.PropertiesPosition.TryGetValue(PositionType.Location, out var location))
                    return null;

                if (IsStaff(accessLevel))
                    return null;

                var guid = biota.Id;
                var now = DateTime.UtcNow;

                // Variation 0 is base: DoPlayerEnterWorld only normalizes a saved 0 later, and room keys treat 0 as null.
                var variation = VariationManager.NormalizeBase(location.VariationId);

                var found = FindRoomAt(location.ObjCellId, variation);
                var rooms = found != null ? GetRooms(found.SourceWcid) : null;

                // GetRooms may have re-parsed an edited list: take the room from the list it returned, not from the cell map,
                // so the key matches the claims MoveClaims has just moved.
                var room = rooms?.FirstOrDefault(r => r.Cells.Contains(location.ObjCellId));
                if (room == null)
                {
                    found = null;
                    rooms = null;
                }

                // An Olthoi logging in at the lifestone is moved there after this - treat them as not in a room.
                var olthoiAtLifestone = biota.PropertiesInt != null && biota.PropertiesInt.TryGetValue(PropertyInt.HeritageGroup, out var heritage)
                    && (heritage == (int)HeritageGroup.Olthoi || heritage == (int)HeritageGroup.OlthoiAcid)
                    && biota.PropertiesBool != null && biota.PropertiesBool.TryGetValue(PropertyBool.LoginAtLifestone, out var atLifestone) && atLifestone;

                // Outside every room, inside a PORTAL-ONLY dungeon: nobody gets there legitimately. Into a room picked like
                // any hand-out (owner 2026-09-20), or to their lifestone when none is free.
                if (found == null && !olthoiAtLifestone)
                {
                    var dungeon = FindPortalOnlyDungeon(location.ObjCellId, variation);
                    var dungeonRooms = dungeon != 0 ? GetRooms(dungeon) : null;

                    if (dungeonRooms != null && dungeonRooms.Count > 0)
                        return HandleLoginOutsideRooms(biota, location, guid, account, variation, dungeon, dungeonRooms, now);
                }

                if (rooms == null || rooms.Count == 0 || olthoiAtLifestone)
                {
                    lock (_lock)
                    {
                        PurgeExpired(now);
                        SpendHolds(account);
                        RemoveOwnReservation(guid);
                    }
                    return null;
                }

                // Never count this character: after a client crash the old session can still be standing in the room.
                var occupancy = BuildOccupancy(rooms, variation, guid);
                var key = room.Key(variation);

                bool hadClaim;
                bool busy;
                var stays = false;
                var keptFree = false;

                // The character is not a Player yet at login - read the name off the biota for the Rooms tab.
                string loginName = null;
                biota.PropertiesString?.TryGetValue(PropertyString.Name, out loginName);

                lock (_lock)
                {
                    PurgeExpired(now);
                    ReleaseDoneReservations(occupancy, guid);

                    hadClaim = (_holds.TryGetValue(key, out var hold) && hold.Account == account && IsHoldActive(hold, now))
                        || (_reservedBy.TryGetValue(guid, out var reservedKey) && reservedKey == key);

                    busy = IsAccountBusy(guid, account, occupancy);

                    var free = IsFreeFor(room, variation, account, occupancy, now);

                    // No claim, but nobody else has the room (a restart wiped the holds, or a hold ran out): they stay in it
                    // rather than being moved to the first free one (owner 2026-09-23). A new hand-out like any other - Reserve
                    // refills logout-hold credit only if the room was not already this account's.
                    keptFree = !hadClaim && !busy && free;

                    if ((hadClaim || keptFree) && !busy && free)
                    {
                        stays = true;
                        Reserve(guid, account, room, variation, now, fresh: keptFree, name: loginName);

                        // The dungeon is known in this variation from now on, even if its entrance is not a database placement
                        // (a spawned one is only remembered once it enters the world): the walls and the trespass check need it.
                        RememberSource(SourceKey(found.SourceWcid, variation));
                    }
                    else if (!hadClaim || busy)
                    {
                        SpendHolds(account);
                        RemoveOwnReservation(guid);
                    }
                }

                if (stays)
                {
                    if (keptFree)
                    {
                        log.Info($"[RoomAssign] Character 0x{guid:X8} logged in to room {room.Number} with no claim - it was free, kept it.");
                        return MessageYoursAgain(room.Number);
                    }

                    return MessageStillYours(room.Number);
                }

                // Here the room is someone else's. No claim (the hold lapsed) gets a room like any hand-out too, the same as a login outside the rooms
                // (owner 2026-09-23, extending the 09-20 ruling; was straight to the lifestone). The lifestone is only for a
                // full dungeon or an account that already has a room.
                if (!busy)
                {
                    var result = ClaimRoom(null, guid, account, rooms, variation, found.SourceWcid, occupancy, null, now, out var newRoom, out var landing, loginName);
                    if (result == ClaimResult.Claimed)
                    {
                        WriteLocation(location, landing, variation);

                        lock (_lock)
                            RememberSource(SourceKey(found.SourceWcid, variation));

                        if (!hadClaim)
                        {
                            log.Info($"[RoomAssign] Character 0x{guid:X8} logged in to room {room.Number} with no claim - handed room {newRoom.Number}.");
                            return MessageSentToRoom(found.SourceWcid, newRoom.Number);
                        }

                        log.Info($"[RoomAssign] Character 0x{guid:X8} logged in to room {room.Number}, which someone else has - moved to room {newRoom.Number}.");
                        return MessageMovedToRoom(newRoom.Number);
                    }

                    lock (_lock)
                    {
                        SpendHolds(account);
                        RemoveOwnReservation(guid);
                    }
                }

                MoveToLifestone(biota, location);

                var reason = busy ? "another character of the account has a room" : hadClaim ? "every room is taken" : "no claim and every room is taken";
                log.Info($"[RoomAssign] Character 0x{guid:X8} logged in to room {room.Number} - {reason}, moved to the lifestone.");
                return busy ? MessageAccountHasRoom : hadClaim ? MessageMovedOut : MessageNoClaim;
            }
            catch (Exception ex)
            {
                log.Error($"[RoomAssign] HandleLogin for 0x{biota?.Id:X8}: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Login outside every room of a portal-only dungeon (owner 2026-09-20, replacing the 2026-09-16 "held room or
        /// lifestone" rule): into a room picked like any hand-out - the room the account holds, the room it was last
        /// handed, else the first free room - reserved while they load in; to the lifestone only when no room is free or
        /// another character of the account has one.
        /// </summary>
        private static string HandleLoginOutsideRooms(ACE.Entity.Models.Biota biota, ACE.Entity.Models.PropertiesPosition location,
            uint guid, uint account, int? variation, uint sourceWcid, List<Room> rooms, DateTime now)
        {
            var occupancy = BuildOccupancy(rooms, variation, guid);
            var fromCell = location.ObjCellId;

            // Which room the account holds, read before ClaimRoom spends the hold: it decides the message.
            string heldKey = null;
            lock (_lock)
                foreach (var room in rooms)
                    if (_holds.TryGetValue(room.Key(variation), out var hold) && hold.Account == account && IsHoldActive(hold, now))
                    {
                        heldKey = room.Key(variation);
                        break;
                    }

            string loginName = null;
            biota.PropertiesString?.TryGetValue(PropertyString.Name, out loginName);

            var result = ClaimRoom(null, guid, account, rooms, variation, sourceWcid, occupancy, null, now, out var claimed, out var landing, loginName);

            if (result == ClaimResult.Claimed)
            {
                WriteLocation(location, landing, variation);

                lock (_lock)
                    RememberSource(SourceKey(sourceWcid, variation));

                var back = claimed.Key(variation) == heldKey;
                log.Info($"[RoomAssign] Character 0x{guid:X8} logged in outside the rooms of portal {sourceWcid} (cell 0x{fromCell:X8}) - {(back ? "returned to its held room" : "moved to room")} {claimed.Number}.");
                return back ? MessageBackToRoom : MessageSentToRoom(sourceWcid, claimed.Number);
            }

            lock (_lock)
            {
                SpendHolds(account);
                RemoveOwnReservation(guid);
            }

            MoveToLifestone(biota, location);
            log.Info($"[RoomAssign] Character 0x{guid:X8} logged in outside the rooms of portal {sourceWcid} (cell 0x{fromCell:X8}) - {(result == ClaimResult.AccountBusy ? "another character of the account has a room" : "every room is taken")}, moved to the lifestone.");
            return result == ClaimResult.AccountBusy ? MessageAccountHasRoom : MessageCannotRemain;
        }
    }
}
