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
    /// placed. Never in the base world (variation null/0): retail is never touched.
    ///
    /// Claims belong to the ACCOUNT (owner 2026-09-16): one room per account at a time, across all its characters.
    /// A room is TAKEN for a player when:
    ///  - a non-staff player of another account stands in it (dead and teleporting players count),
    ///  - another account holds a reservation on it (from the hand-out until that player has landed; 30 s cap), or
    ///  - another account holds it: a LEAVE hold (room_assign_leave_hold_seconds, made when teleporting out of a room -
    ///    recall, portal, death) or a LOGOUT hold (room_assign_logout_hold_minutes, made at logout inside a room while the
    ///    account has logout-hold credit). One hold per account.
    /// An account whose other character is standing in or travelling to a room gets no room at all.
    /// A player gets, in order: the room reserved for them, the room they stand in, the room held for their account, the
    /// first free room. Only the last is a FRESH hand-out; only a fresh hand-out grants logout-hold credit
    /// (1 + room_assign_logout_hold_renewals).
    ///
    /// Threading: a plate step, portal or recall runs on its landblock group's thread, teleport on whatever thread
    /// teleports, login on the world thread, logout on whichever thread finalizes it. All claim state sits behind _lock.
    /// Nothing inside _lock does database work, teleports or takes a manager lock; the only locks taken inside it are a
    /// player's own property read locks. Lock order _scanLock -> _parseLock -> _lock, never reversed.
    /// PlayerManager.GetAllOnline takes its own lock, so occupancy is built BEFORE _lock.
    /// </summary>
    public static class RoomAssignManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>Hard cap on a reservation that never saw its player land (normally released on landing).</summary>
        private static readonly TimeSpan ReservationTime = TimeSpan.FromSeconds(30);

        /// <summary>Owner 2026-09-16: was 10 s, too long to wait for the message.</summary>
        private static readonly TimeSpan FullMessageInterval = TimeSpan.FromSeconds(1);

        private const int FullMessagePruneThreshold = 64;

        // Setting caps: a huge /modifylong value must not overflow DateTime arithmetic in every check.
        private const long MaxLogoutHoldMinutes = 7 * 24 * 60;
        private const long MaxLeaveHoldSeconds = 24 * 60 * 60;
        private const long MaxLogoutHoldRenewals = 100;
        private const long MaxStartupGraceMinutes = 24 * 60;

        /// <summary>Server setting room_assign_logout_hold_minutes (0 - 10080). 0 = no logout holds.</summary>
        private static TimeSpan LogoutHoldTime => TimeSpan.FromMinutes(Math.Clamp(ServerConfig.room_assign_logout_hold_minutes.Value, 0, MaxLogoutHoldMinutes));

        /// <summary>Server setting room_assign_leave_hold_seconds (0 - 86400; owner 2026-09-16: 60, respawns are 15-45 s). 0 = off.</summary>
        private static TimeSpan LeaveHoldTime => TimeSpan.FromSeconds(Math.Clamp(ServerConfig.room_assign_leave_hold_seconds.Value, 0, MaxLeaveHoldSeconds));

        /// <summary>
        /// 1 + server setting room_assign_logout_hold_renewals (0 - 100; owner 2026-09-16: 1). A fresh hand-out allows one
        /// logout hold plus this many more - a crash, and a second crash, keep the room; relogging forever does not.
        /// </summary>
        private static int LogoutHoldCredit => 1 + (int)Math.Clamp(ServerConfig.room_assign_logout_hold_renewals.Value, 0, MaxLogoutHoldRenewals);

        /// <summary>
        /// Server setting room_assign_startup_grace_minutes (0 - 1440; owner 2026-09-16: 15). Holds live in memory, so a
        /// restart wipes them - and a restart logs everyone out at once. For this long after the server started, a character
        /// logging in inside a free room keeps it as if it had a hold (first to log in wins). 0 = off.
        /// </summary>
        private static TimeSpan StartupGraceTime => TimeSpan.FromMinutes(Math.Clamp(ServerConfig.room_assign_startup_grace_minutes.Value, 0, MaxStartupGraceMinutes));

        /// <summary>
        /// When the startup grace began (UTC): set by Initialize as the world opens, so a slow boot does not use it up.
        /// Falls back to the first use of this class if Initialize never ran.
        /// </summary>
        private static DateTime _graceStartUtc = DateTime.UtcNow;

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

        public static string MessageSentToRoom(int room) => $"You are sent to chamber {room}.";

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

            internal static string KeyFor(int? variation, uint anchor)
                => $"{VariationManager.NormalizeBase(variation)?.ToString(CultureInfo.InvariantCulture)}|{anchor:X8}";
        }

        /// <summary>Rooms never exist in the base world (variation null/0): retail content is never a room.</summary>
        public static bool IsRoomVariation(int? variation) => VariationManager.NormalizeBase(variation) != null;

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
        private static readonly Regex LandingRx = new Regex(@"^0x([0-9A-Fa-f]{8})\s+\[\s*(\S+)\s+(\S+)\s+(\S+)\s*\]\s+(\S+)\s+(\S+)\s+(\S+)\s+(\S+)$", RegexOptions.Compiled);

        /// <summary>
        /// Program startup, after WorldManager.Initialize and before the world accepts logins: loads every room source (a
        /// database query that must not run on a game thread) and starts the startup grace.
        /// </summary>
        public static void Initialize()
        {
            _graceStartUtc = DateTime.UtcNow;
            ScanRoomSources();
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
                    List<uint> ids;
                    using (var context = new WorldDbContext())
                        ids = context.WeeniePropertiesString
                            .Where(r => r.Type == (ushort)PropertyString.RoomAssignRooms)
                            .Select(r => r.ObjectId)
                            .Distinct()
                            .ToList();

                    foreach (var id in ids)
                    {
                        GetRooms(id);

                        var weenie = DatabaseManager.World.GetCachedWeenie(id);
                        if (weenie?.WeenieType == WeenieType.Portal && (weenie.PropertiesPosition == null || !weenie.PropertiesPosition.ContainsKey(PositionType.Destination)))
                            log.Warn($"[RoomAssign] wcid {id}: room portal has no Destination on its weenie - its rooms are only known once a placed copy has loaded or been used.");
                    }

                    _sourcesScanned = 1;
                    log.Info($"[RoomAssign] {ids.Count} room source(s) in the world database.");
                }
                catch (Exception ex)
                {
                    Interlocked.Exchange(ref _scanRetryAtTicks, (DateTime.UtcNow + ScanRetry).Ticks);
                    log.Error($"[RoomAssign] Loading room sources failed, retrying in {ScanRetry.TotalSeconds:0} s: {ex}");
                }
            }
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
                        MoveClaims(good.Rooms, parsed);
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

                var rotationLength = MathF.Sqrt(nums[3] * nums[3] + nums[4] * nums[4] + nums[5] * nums[5] + nums[6] * nums[6]);
                if (rotationLength < 0.01f)
                {
                    error = $"room {number}: the landing rotation is all zero (use 1 0 0 0 for the default facing)";
                    return false;
                }

                // A hand-typed rotation need not be unit length; the client expects one.
                for (var i = 3; i < 7; i++)
                    nums[i] /= rotationLength;

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
                    if (!cellText.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                        !uint.TryParse(cellText.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var cell))
                    {
                        error = $"room {number}: \"{cellText}\" is not a cell id";
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
        }

        private sealed class RoomRef
        {
            public uint SourceWcid;
            public Room Room;
        }

        /// <summary>A "no plate in this variation" answer is re-checked after this.</summary>
        private static readonly TimeSpan NoPlateRecheck = TimeSpan.FromSeconds(60);

        private static readonly object _lock = new object();
        private static readonly Dictionary<string, Reservation> _reservations = new Dictionary<string, Reservation>();   // room key -> player on their way in
        private static readonly Dictionary<uint, string> _reservedBy = new Dictionary<uint, string>();                   // player guid -> room key
        private static readonly Dictionary<string, Hold> _holds = new Dictionary<string, Hold>();                        // room key -> account it is held for
        private static readonly Dictionary<uint, DateTime> _lastFullMessage = new Dictionary<uint, DateTime>();
        private static readonly Dictionary<uint, List<RoomRef>> _roomByCell = new Dictionary<uint, List<RoomRef>>();     // cell -> rooms + source WCID
        private static readonly Dictionary<ushort, HashSet<uint>> _sourcesByLandblock = new Dictionary<ushort, HashSet<uint>>();   // landblock -> source WCIDs
        // "wcid|variation" -> the source was seen in that variation: a placed copy entered the world, or it was used. Kept
        // until restart - a portal's destination variation and a plate's placement do not change, and a source removed
        // from a variation only keeps its rooms recognised there (holds, login checks) until then.
        private static readonly HashSet<string> _sourceSeen = new HashSet<string>();
        private static readonly Dictionary<string, DateTime> _noPlateUntil = new Dictionary<string, DateTime>();         // "wcid|variation" -> no placement, re-checked after

        // Logout holds an ACCOUNT may still take before its next FRESH hand-out. Without a limit, relogging - or recalling
        // into the room you stand in - renewed a hold forever. Cleared when the account's character logs out outside a room
        // holding nothing, so it never outgrows the accounts currently using rooms.
        private static readonly Dictionary<uint, int> _logoutHoldCredit = new Dictionary<uint, int>();

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
                _roomByCell.Remove(cell);

            foreach (var room in rooms)
            {
                foreach (var cell in room.Cells)
                {
                    if (!_roomByCell.TryGetValue(cell, out var refs))
                        _roomByCell[cell] = refs = new List<RoomRef>();

                    refs.Add(new RoomRef { SourceWcid = sourceWcid, Room = room });
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
        /// Call with _lock held, after a list was re-parsed. A room is the same room when it shares a cell with an old one.
        /// Its key is its lowest cell, so an edit that adds or removes that cell changes the key: holds and reservations
        /// move to the new key (never over a claim already there), and reservations point at the new room's cells.
        /// </summary>
        private static void MoveClaims(List<Room> oldRooms, List<Room> newRooms)
        {
            foreach (var oldRoom in oldRooms)
            {
                var newRoom = newRooms.FirstOrDefault(r => r.Cells.Overlaps(oldRoom.Cells));
                if (newRoom == null)
                    continue;

                foreach (var reservation in _reservations.Values)
                    if (reservation.Room == oldRoom)
                        reservation.Room = newRoom;

                if (newRoom.Anchor == oldRoom.Anchor)
                    continue;

                var oldSuffix = $"|{oldRoom.Anchor:X8}";

                foreach (var key in _holds.Keys.Where(k => k.EndsWith(oldSuffix, StringComparison.Ordinal)).ToList())
                {
                    var newKey = key.Substring(0, key.Length - oldSuffix.Length) + $"|{newRoom.Anchor:X8}";
                    if (!_holds.ContainsKey(newKey))
                        _holds[newKey] = _holds[key];
                    _holds.Remove(key);
                }

                foreach (var key in _reservations.Keys.Where(k => k.EndsWith(oldSuffix, StringComparison.Ordinal)).ToList())
                {
                    var reservation = _reservations[key];
                    _reservations.Remove(key);

                    var newKey = key.Substring(0, key.Length - oldSuffix.Length) + $"|{newRoom.Anchor:X8}";
                    if (_reservations.ContainsKey(newKey))
                    {
                        _reservedBy.Remove(reservation.Guid);
                        continue;
                    }

                    _reservations[newKey] = reservation;
                    _reservedBy[reservation.Guid] = newKey;
                }
            }
        }

        /// <summary>
        /// The PORTAL-ONLY source whose dungeon this cell is in (its landblock, in the source's variation), or 0. A portal-only
        /// dungeon has no legitimate place outside its rooms - every way in lands in a room and rooms are sealed. A dungeon
        /// that also has a plate source valid there is plate mode (its arrival hall is legitimate), so it returns 0.
        /// </summary>
        private static uint FindPortalOnlyDungeon(uint cell, int? variation)
        {
            if (_roomSourceCount == 0 || !IsRoomVariation(variation))
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
                if (!IsSourceVariation(wcid, variation, cell))
                    continue;

                if (DatabaseManager.World.GetCachedWeenie(wcid)?.WeenieType != WeenieType.Portal)
                    return 0;   // a plate is valid here: plate mode

                portalSource = wcid;
            }

            return portalSource;
        }

        private static string SourceKey(uint wcid, int? variation) => $"{wcid}|{VariationManager.NormalizeBase(variation)}";

        /// <summary>Call with _lock held. Remembers that a source exists in a variation.</summary>
        private static void RememberSource(string key)
        {
            _sourceSeen.Add(key);
            _noPlateUntil.Remove(key);
        }

        /// <summary>
        /// A placed room portal's destination is known (it entered the world, or a link spot set it), or a room plate entered
        /// the world: its rooms exist in that variation from now on, before anyone uses it. This is what scopes a portal
        /// whose destination is not on its weenie (link spot, relative destination) and a plate that is not a database
        /// placement (spawned by a generator).
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
        /// Whether a plate source is placed in a variation: seen in the world or used there, else found in the landblock's
        /// database placements.
        /// </summary>
        private static bool IsPlatePlaced(uint plateWcid, int? variation, uint roomCell)
        {
            if (!IsRoomVariation(variation))
                return false;

            var key = SourceKey(plateWcid, variation);
            var now = DateTime.UtcNow;

            lock (_lock)
            {
                if (_sourceSeen.Contains(key))
                    return true;

                // Remembered "not here": a player teleporting inside a room landblock must not open a database context on
                // every teleport. A plate placed later is seen within NoPlateRecheck, or at once when it loads or is used.
                if (_noPlateUntil.TryGetValue(key, out var until) && now < until)
                    return false;
            }

            var instances = DatabaseManager.World.GetCachedInstancesByLandblock((ushort)(roomCell >> 16), VariationManager.NormalizeBase(variation));

            // A copy: the cached list is the shared one that /createinst and /removeinst edit in place.
            foreach (var instance in instances?.ToArray() ?? Array.Empty<LandblockInstance>())
            {
                if (instance?.WeenieClassId != plateWcid)
                    continue;

                lock (_lock)
                    RememberSource(key);

                return true;
            }

            lock (_lock)
            {
                if (_sourceSeen.Contains(key))
                    return true;

                _noPlateUntil[key] = now + NoPlateRecheck;
            }

            return false;
        }

        /// <summary>
        /// Whether a source's rooms exist in this variation. A portal source: its weenie's Destination variation, or - for a
        /// portal whose destination comes from a link spot or is relative - a variation a placed copy was seen in. A plate
        /// source: where the plate is placed. Never the base world.
        /// </summary>
        private static bool IsSourceVariation(uint sourceWcid, int? variation, uint cell)
        {
            if (!IsRoomVariation(variation))
                return false;

            var weenie = DatabaseManager.World.GetCachedWeenie(sourceWcid);
            if (weenie == null)
                return false;

            if (weenie.WeenieType == WeenieType.Portal)
            {
                if (weenie.PropertiesPosition != null && weenie.PropertiesPosition.TryGetValue(PositionType.Destination, out var destination))
                    return VariationManager.NormalizeBase(destination.VariationId) == VariationManager.NormalizeBase(variation);

                lock (_lock)
                    return _sourceSeen.Contains(SourceKey(sourceWcid, variation));
            }

            return IsPlatePlaced(sourceWcid, variation, cell);
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

            // Outside _lock: IsSourceVariation may read the world database (plate placements).
            foreach (var candidate in candidates)
                if (IsSourceVariation(candidate.SourceWcid, variation, cell))
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
        private static bool SetHold(string key, uint account, TimeSpan time, HoldKind kind, DateTime now)
        {
            if (_holds.TryGetValue(key, out var existing) && existing.Account != account && IsHoldActive(existing, now))
                return false;

            SpendHolds(account);
            _holds[key] = new Hold { Account = account, Until = now + time, Kind = kind };
            return true;
        }

        /// <summary>
        /// Call with _lock held. Reserves the room for the player's trip, replacing their own reservation. A committed
        /// reservation spends the account's holds (one room per account), and a FRESH hand-out (a first-free room) also
        /// grants logout-hold credit. A pending one (commit false) does neither until it is committed.
        /// </summary>
        private static void Reserve(uint guid, uint account, Room room, int? variation, DateTime now, bool fresh, bool commit = true)
        {
            RemoveOwnReservation(guid);

            var key = room.Key(variation);
            _reservations[key] = new Reservation
            {
                Guid = guid, Account = account, Room = room, Variation = variation, Until = now + ReservationTime, CreatedUnix = Time.GetUnixTime(),
                Pending = !commit, PendingFresh = !commit && fresh,
            };
            _reservedBy[guid] = key;

            if (!commit)
                return;

            SpendHolds(account);

            if (fresh)
                _logoutHoldCredit[account] = LogoutHoldCredit;
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
            public readonly Dictionary<uint, Player> Online = new Dictionary<uint, Player>();
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
                if (!occupancy.Online.TryGetValue(kv.Value.Guid, out var player) || !ReservationDone(kv.Value, player, out var inRoom))
                    continue;

                (done ??= new List<string>()).Add(kv.Key);

                if (inRoom && kv.Value.Guid != excludeGuid && !IsStaff(player))
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

        /// <summary>Admins do not count as occupants, get no holds and are never moved or assigned by portals (owner rulings 2026-09-16).</summary>
        private static bool IsStaff(Player player)
            => player.IsAdmin || (player.Session != null && player.Session.AccessLevel >= AccessLevel.Admin);

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

            foreach (var player in PlayerManager.GetAllOnline())
            {
                var guid = player.Guid.Full;
                occupancy.Online[guid] = player;

                if (guid == excludeGuid)
                    continue;

                var location = player.Location;
                if (location == null || location.Cell >> 16 != landblock)
                    continue;

                foreach (var room in rooms)
                {
                    if (InRoom(location, room, variation))
                    {
                        // The staff check reads a property: only for players actually in a room.
                        if (!IsStaff(player))
                        {
                            occupancy.Rooms.Add(room.Key(variation));
                            occupancy.Accounts.Add(AccountOf(player));
                        }
                        break;
                    }
                }
            }

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
        /// room held for their account (always back to their own room while it is free), then the first free room - the only
        /// FRESH hand-out. Null when none is free.
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

        /// <summary>Rooms already warned about a landing outside their cells (per parsed Room object, so a fixed list warns again if still bad).</summary>
        private static readonly HashSet<Room> _badLandingWarned = new HashSet<Room>();

        private enum ClaimResult { Claimed, NoRoom, AccountBusy, OnTheWay }

        /// <summary>
        /// Picks and reserves a room in two steps, so nothing is reserved (and no hold or credit changed) until the landing
        /// has been computed: pick under _lock, compute the landing outside it, then reserve under _lock only if the room is
        /// still free for them. player is null for a login (no player object yet).
        /// </summary>
        private static ClaimResult ClaimRoom(Player player, uint guid, uint account, List<Room> rooms, int? variation, uint sourceWcid,
            Occupancy occupancy, Room current, DateTime now, out Room room, out Position landing)
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
                        Reserve(guid, account, room, variation, now, fresh);
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

        // ---------------------------------------------------------------------------------------------------------
        // The plate
        // ---------------------------------------------------------------------------------------------------------

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
                var cooldown = plate.EffectivePressurePlateCooldown;
                if (cooldown > 0 && now < plate.LastUseTime + TimeSpan.FromSeconds(cooldown))
                    return;

                lock (_lock)
                {
                    PurgeExpired(now);
                    RememberSource(SourceKey(plate.WeenieClassId, variation));

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

                player.EnqueueBroadcast(new GameMessageSound(player.Guid, plate.UseSound));
                WorldManager.ThreadSafeTeleport(player, landing);
                Say(player, MessageSentToRoom(room.Number));

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
        /// plate, leads to the base world, or the player is staff (admins use the portal's normal destination). notReady
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
                    lock (_lock)
                    {
                        PurgeExpired(now);
                        onTheWay = IsOnTheWay(player, guid);
                    }

                    if (onTheWay)
                    {
                        if (!throttle)
                            Say(player, MessageOnTheWay);
                        return false;
                    }

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
                                    Reserve(guid, account, room, variation, now, fresh, commit: false);
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
        public static PortalAssign AssignPortalRoom(Player player, uint portalWcid, Position destination, out Position landing, out int roomNumber)
        {
            landing = null;
            roomNumber = 0;
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

                if (player.IsLoggingOut || player.Session == null || player.CurrentLandblock == null)
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
                    lock (_lock)
                        RemoveOwnReservation(guid);

                    Say(player, result == ClaimResult.AccountBusy ? MessageAccountHasRoom : result == ClaimResult.OnTheWay ? MessageOnTheWay : MessagePortalAllTaken);
                    return PortalAssign.Refused;
                }

                // Remember the variation this source was used with: it scopes a plate that is not a database placement, and a
                // portal whose destination comes from a link spot.
                lock (_lock)
                    RememberSource(SourceKey(sourceWcid, variation));

                roomNumber = room.Number;
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
        /// room - recall, portal, the death teleport to the lifestone - keeps it for the leave hold (owner 2026-09-16: 60 s),
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
                var now = DateTime.UtcNow;
                bool held;

                lock (_lock)
                {
                    PurgeExpired(now);

                    if (_reservedBy.TryGetValue(guid, out var reservedKey) && reservedKey != key)
                        return;

                    held = SetHold(key, AccountOf(player), holdTime, HoldKind.Leave, now);
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
                var creditLeft = 0;
                var holdTime = LogoutHoldTime;

                lock (_lock)
                {
                    PurgeExpired(now);
                    RemoveOwnReservation(guid);

                    if (found == null)
                    {
                        if (!_holds.Values.Any(h => h.Account == account))
                            _logoutHoldCredit.Remove(account);
                        return;
                    }

                    if (holdTime <= TimeSpan.Zero)
                        return;

                    _logoutHoldCredit.TryGetValue(account, out creditLeft);

                    if (creditLeft > 0)
                    {
                        held = SetHold(found.Room.Key(location.Variation), account, holdTime, HoldKind.Logout, now);

                        if (held)
                        {
                            creditLeft--;
                            if (creditLeft > 0)
                                _logoutHoldCredit[account] = creditLeft;
                            else
                                _logoutHoldCredit.Remove(account);
                        }
                    }
                }

                if (held)
                    log.Info($"[RoomAssign] {player.Name} (0x{player.Guid}) logged out in room {found.Room.Number} - held for {holdTime.TotalMinutes:0.##} minutes ({creditLeft} renewal(s) left).");
                else if (creditLeft <= 0)
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
        /// - In a room the account holds (or this character had reserved), free for them: they stay; the room is reserved
        ///   for them while they load in.
        /// - Within the startup grace (holds were wiped by the restart), a free room counts as claimed - first in wins.
        /// - They held it but another account has it now: moved to the account's held room or the first free room
        ///   (reserved), or to their lifestone when every room is taken.
        /// - No claim on it, or another character of the account already has a room: to their lifestone (owner
        ///   2026-09-16) - they come back through the portal like anyone else.
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

                if (accessLevel >= AccessLevel.Admin)
                    return null;

                if (biota.PropertiesBool != null && biota.PropertiesBool.TryGetValue(PropertyBool.IsAdmin, out var isAdmin) && isAdmin)
                    return null;

                var guid = biota.Id;
                var now = DateTime.UtcNow;

                // Variation 0 is base: DoPlayerEnterWorld only normalizes a saved 0 later, and room keys treat 0 as null.
                var variation = VariationManager.NormalizeBase(location.VariationId);

                var found = FindRoomAt(location.ObjCellId, variation);
                var rooms = found != null ? GetRooms(found.SourceWcid) : null;

                // An Olthoi logging in at the lifestone is moved there after this - treat them as not in a room.
                var olthoiAtLifestone = biota.PropertiesInt != null && biota.PropertiesInt.TryGetValue(PropertyInt.HeritageGroup, out var heritage)
                    && (heritage == (int)HeritageGroup.Olthoi || heritage == (int)HeritageGroup.OlthoiAcid)
                    && biota.PropertiesBool != null && biota.PropertiesBool.TryGetValue(PropertyBool.LoginAtLifestone, out var atLifestone) && atLifestone;

                // Outside every room, inside a PORTAL-ONLY dungeon (owner 2026-09-16): nobody gets there legitimately. Back to
                // the room their account holds, if it is free; otherwise to their lifestone. Never a new room - rooms are
                // only handed out through the portal, where its requirements are checked.
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
                var key = found.Room.Key(variation);

                bool hadClaim;
                bool busy;
                var stays = false;
                var byGrace = false;
                var graceTime = StartupGraceTime;
                var inGrace = graceTime > TimeSpan.Zero && now - _graceStartUtc < graceTime;

                lock (_lock)
                {
                    PurgeExpired(now);
                    ReleaseDoneReservations(occupancy, guid);

                    hadClaim = (_holds.TryGetValue(key, out var hold) && hold.Account == account && IsHoldActive(hold, now))
                        || (_reservedBy.TryGetValue(guid, out var reservedKey) && reservedKey == key);

                    busy = IsAccountBusy(guid, account, occupancy);

                    var free = IsFreeFor(found.Room, variation, account, occupancy, now);

                    // Startup grace: the restart wiped every hold - a free room is theirs again, first in wins. It counts
                    // as a fresh hand-out, so they get logout-hold credit back too.
                    if (!hadClaim && inGrace && free && !busy)
                    {
                        hadClaim = true;
                        byGrace = true;
                    }

                    if (hadClaim && !busy && free)
                    {
                        stays = true;
                        Reserve(guid, account, found.Room, variation, now, fresh: byGrace);
                    }
                    else if (!hadClaim || busy)
                    {
                        SpendHolds(account);
                        RemoveOwnReservation(guid);
                    }
                }

                if (stays)
                {
                    if (byGrace)
                        log.Info($"[RoomAssign] Character 0x{guid:X8} logged in to room {found.Room.Number} within the startup grace - kept it.");
                    return null;
                }

                if (hadClaim && !busy)
                {
                    var result = ClaimRoom(null, guid, account, rooms, variation, found.SourceWcid, occupancy, null, now, out var newRoom, out var landing);
                    if (result == ClaimResult.Claimed)
                    {
                        WriteLocation(location, landing, variation);
                        log.Info($"[RoomAssign] Character 0x{guid:X8} logged in to room {found.Room.Number}, which someone else has - moved to room {newRoom.Number}.");
                        return MessageMovedToRoom(newRoom.Number);
                    }

                    lock (_lock)
                    {
                        SpendHolds(account);
                        RemoveOwnReservation(guid);
                    }
                }

                // To their lifestone - where death also sends them (Sanctuary, else Instantiation).
                if (!biota.PropertiesPosition.TryGetValue(PositionType.Sanctuary, out var lifestone))
                    biota.PropertiesPosition.TryGetValue(PositionType.Instantiation, out lifestone);

                if (lifestone == null)
                {
                    log.Warn($"[RoomAssign] Character 0x{guid:X8} logged in to room {found.Room.Number} without a claim it can keep, and it has no lifestone - left in place.");
                    return null;
                }

                WriteLocation(location, lifestone);

                var reason = busy ? "another character of the account has a room" : hadClaim ? "every room is taken" : "it holds no claim on the room";
                log.Info($"[RoomAssign] Character 0x{guid:X8} logged in to room {found.Room.Number} - {reason}, moved to the lifestone.");
                return busy ? MessageAccountHasRoom : hadClaim ? MessageMovedOut : MessageNoClaim;
            }
            catch (Exception ex)
            {
                log.Error($"[RoomAssign] HandleLogin for 0x{biota?.Id:X8}: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Login outside every room of a portal-only dungeon: into the room the account holds (if free for them), else to
        /// the lifestone. The held room is reserved while they load in.
        /// </summary>
        private static string HandleLoginOutsideRooms(ACE.Entity.Models.Biota biota, ACE.Entity.Models.PropertiesPosition location,
            uint guid, uint account, int? variation, uint sourceWcid, List<Room> rooms, DateTime now)
        {
            var occupancy = BuildOccupancy(rooms, variation, guid);
            Room held = null;

            lock (_lock)
            {
                PurgeExpired(now);
                ReleaseDoneReservations(occupancy, guid);

                if (!IsAccountBusy(guid, account, occupancy))
                    foreach (var room in rooms)
                        if (_holds.TryGetValue(room.Key(variation), out var hold) && hold.Account == account && IsHoldActive(hold, now)
                            && IsFreeFor(room, variation, account, occupancy, now))
                        {
                            held = room;
                            break;
                        }
            }

            if (held != null)
            {
                var landing = Landing(held, variation, sourceWcid);

                lock (_lock)
                {
                    if (landing != null && !IsAccountBusy(guid, account, occupancy) && IsFreeFor(held, variation, account, occupancy, now))
                        Reserve(guid, account, held, variation, now, fresh: false);
                    else
                        held = null;
                }

                if (held != null)
                {
                    WriteLocation(location, landing, variation);
                    log.Info($"[RoomAssign] Character 0x{guid:X8} logged in outside the rooms of portal {sourceWcid} - returned to its held room {held.Number}.");
                    return MessageBackToRoom;
                }
            }

            lock (_lock)
            {
                SpendHolds(account);
                RemoveOwnReservation(guid);
            }

            if (!biota.PropertiesPosition.TryGetValue(PositionType.Sanctuary, out var lifestone))
                biota.PropertiesPosition.TryGetValue(PositionType.Instantiation, out lifestone);

            if (lifestone == null)
            {
                log.Warn($"[RoomAssign] Character 0x{guid:X8} logged in outside the rooms of portal {sourceWcid} and has no lifestone - left in place.");
                return null;
            }

            var fromCell = location.ObjCellId;
            WriteLocation(location, lifestone);
            log.Info($"[RoomAssign] Character 0x{guid:X8} logged in outside the rooms of portal {sourceWcid} (cell 0x{fromCell:X8}) - moved to the lifestone.");
            return MessageCannotRemain;
        }
    }
}
