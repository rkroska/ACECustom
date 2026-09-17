using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

using log4net;

using ACE.Database;
using ACE.Database.Models.World;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity.Actions;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Room Assign plates: step on the plate, get teleported into the first free one-player room
    /// (plan: C:\AI\ZoneControl\RoomAssign_Plan_2026-09-16.md).
    ///
    /// A plate becomes a room plate when its WEENIE carries PropertyString.RoomAssignRooms (9018), one line holding
    /// every room: "room|0xCELL [x y z] qw qx qy qz|0xCELL,0xCELL;room|...". The list is re-read from the weenie cache
    /// on every step (owner ruling), so an /id upload changes rooms live with no landblock reload; a list that does not
    /// parse is logged and the last good one is kept.
    ///
    /// A room is TAKEN when a living, non-staff player stands in one of its cells (in the plate's variation), OR it is
    /// reserved for a player on their way in (30 s), OR it is held for a character who logged out inside it (15 min).
    /// Occupancy is read at the moment of the step, so recall, death and teleporting out free a room with no tracking.
    ///
    /// Threading: a plate step runs on its landblock group's thread, but logout and login reach the same state from
    /// other threads, and "scan the rooms, then reserve one" is two steps. So all reservation / hold state sits behind
    /// one lock, held for whole methods, with no database work, teleport or other lock taken inside it
    /// (the EmoteManager pattern). PlayerManager.GetAllOnline takes its own lock, so occupancy is built BEFORE ours.
    /// </summary>
    public static class RoomAssignManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static readonly TimeSpan ReservationTime = TimeSpan.FromSeconds(30);
        /// <summary>Server setting room_assign_logout_hold_minutes (default 15, saved in the shard DB, /modifylong live). 0 or less = no hold.</summary>
        public static TimeSpan LogoutHoldTime => TimeSpan.FromMinutes(Math.Max(0, ServerConfig.room_assign_logout_hold_minutes.Value));
        public static readonly TimeSpan FullMessageInterval = TimeSpan.FromSeconds(1);   // owner 2026-09-16: was 10 s, too long to wait for the message

        public const string MessageAllTaken = "Every chamber is taken. Move on the plate to try again.";
        public const string MessageMovedOut = "Your chamber was taken while you were away. You have been returned to the entrance.";

        public static string MessageMovedToRoom(int room) => $"Your chamber was taken while you were away. You have been moved to chamber {room}.";

        public sealed class Room
        {
            public int Number;
            public uint LandingCell;
            public float X, Y, Z;
            public float QW, QX, QY, QZ;
            public HashSet<uint> Cells;

            /// <summary>
            /// Base and v2 share every cell id, so a room is only unique together with its variation. The landing cell is
            /// validated to be inside the room, and no cell may belong to two rooms, so this names one physical room.
            /// </summary>
            public string Key(int? variation) => $"{VariationManager.NormalizeBase(variation)}|{LandingCell:X8}";
        }

        // ---------------------------------------------------------------------------------------------------------
        // Room list: parsed from the weenie cache, last good list kept per WCID
        // ---------------------------------------------------------------------------------------------------------

        private sealed class ParsedList
        {
            public string Raw;
            public List<Room> Rooms;
        }

        private static readonly object _parseLock = new object();
        private static readonly Dictionary<uint, ParsedList> _lastGood = new Dictionary<uint, ParsedList>();
        private static readonly HashSet<string> _warnedBad = new HashSet<string>();
        private static readonly List<Room> NoRooms = new List<Room>();

        // Strict on purpose: Position.TryParse silently falls back to a default facing when the rotation does not parse,
        // which is exactly what a pasted /location line does (its ", v:2" leaves a comma on the last number).
        private static readonly Regex LandingRx = new Regex(@"^0x([0-9A-Fa-f]{8}) \[(\S+) (\S+) (\S+)\] (\S+) (\S+) (\S+) (\S+)$", RegexOptions.Compiled);

        /// <summary>
        /// The rooms for a room-assign weenie, or null when the weenie is not a room plate. Re-read from the weenie cache
        /// every call; only a changed string is parsed again.
        /// </summary>
        public static List<Room> GetRooms(uint wcid)
        {
            var weenie = DatabaseManager.World.GetCachedWeenie(wcid);

            string raw = null;
            if (weenie?.PropertiesString == null || !weenie.PropertiesString.TryGetValue(PropertyString.RoomAssignRooms, out raw) || raw == null)
                return null;

            List<Room> parsed = null;

            lock (_parseLock)
            {
                _lastGood.TryGetValue(wcid, out var good);

                if (good != null && good.Raw == raw)
                    return good.Rooms;

                if (TryParseRooms(raw, out var rooms, out var error))
                {
                    _lastGood[wcid] = new ParsedList { Raw = raw, Rooms = rooms };
                    log.Info($"[RoomAssign] wcid {wcid}: loaded {rooms.Count} room(s).");
                    parsed = rooms;
                }
                else
                {
                    if (_warnedBad.Add(wcid + "|" + raw))
                        log.Warn($"[RoomAssign] wcid {wcid}: room list does not parse ({error}) - {(good != null ? "keeping the last good list" : "no good list yet, the plate does nothing")}.");

                    return good?.Rooms ?? NoRooms;
                }
            }

            // A newly loaded list marks its rooms' landblocks for logout / login - outside _parseLock, never nested.
            lock (_lock)
                foreach (var room in parsed)
                    _roomLandblocks.Add((ushort)(room.LandingCell >> 16));

            return parsed;
        }

        /// <summary>
        /// Parses "room|0xCELL [x y z] qw qx qy qz|0xCELL,0xCELL;room|...". Every error names the room it is in.
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
                    if (!float.TryParse(m.Groups[i + 2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out nums[i]))
                    {
                        error = $"room {number}: \"{m.Groups[i + 2].Value}\" in the landing is not a number";
                        return false;
                    }
                }

                var room = new Room
                {
                    Number = number,
                    LandingCell = uint.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    X = nums[0], Y = nums[1], Z = nums[2],
                    QW = nums[3], QX = nums[4], QY = nums[5], QZ = nums[6],
                    Cells = new HashSet<uint>(),
                };

                foreach (var cellRaw in fields[2].Split(','))
                {
                    var cellText = cellRaw.Trim();
                    if (!cellText.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                        !uint.TryParse(cellText.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var cell))
                    {
                        error = $"room {number}: \"{cellText}\" is not a cell id";
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
        // Reservations, holds and the full-rooms message throttle - all behind _lock
        // ---------------------------------------------------------------------------------------------------------

        private sealed class Claim
        {
            public uint Guid;
            public DateTime Until;

            // Reservations only: the room and variation, so a reservation can be released the moment its player has
            // landed (occupancy takes over) or has gone somewhere else - instead of blocking the room for the full 30 s.
            public Room Room;
            public int? Variation;
            public DateTime Created;
        }

        /// <summary>
        /// How long a reservation survives with its player NOT teleporting and NOT in the room. Covers the gap between
        /// the reserve and the teleport actually starting (fog clearing can hold a teleport ~1 s).
        /// </summary>
        private static readonly TimeSpan ReservationGrace = TimeSpan.FromSeconds(5);

        private static Claim NewReservation(uint guid, Room room, int? variation, DateTime now)
            => new Claim { Guid = guid, Until = now + ReservationTime, Room = room, Variation = variation, Created = now };

        private static readonly object _lock = new object();
        private static readonly Dictionary<string, Claim> _reservations = new Dictionary<string, Claim>();   // room key -> player on their way in
        private static readonly Dictionary<uint, string> _reservedBy = new Dictionary<uint, string>();       // player guid -> room key
        private static readonly Dictionary<string, Claim> _holds = new Dictionary<string, Claim>();          // room key -> character who logged out inside
        private static readonly Dictionary<uint, DateTime> _lastFullMessage = new Dictionary<uint, DateTime>();

        // Landblocks holding rooms, marked when a room list is first read - which a room plate does as it loads, so any
        // landblock with a player in it is marked. Login and logout run for every character everywhere, and looking for
        // room plates means reading a landblock's placements (a database read when that landblock is not cached) - so they
        // only look in landblocks listed here. (2026-09-16: marking only on a hand-out left the first logout after a
        // restart with no hold.)
        private static readonly HashSet<ushort> _roomLandblocks = new HashSet<ushort>();

        private static bool IsRoomLandblock(uint cell)
        {
            lock (_lock)
                return _roomLandblocks.Contains((ushort)(cell >> 16));
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
                {
                    if (_reservations.TryGetValue(key, out var claim) && _reservedBy.TryGetValue(claim.Guid, out var reservedKey) && reservedKey == key)
                        _reservedBy.Remove(claim.Guid);
                    _reservations.Remove(key);
                }
                expired.Clear();
            }

            foreach (var kv in _holds)
                if (kv.Value.Until <= now)
                    (expired ??= new List<string>()).Add(kv.Key);

            if (expired != null)
                foreach (var key in expired)
                    _holds.Remove(key);

            if (_lastFullMessage.Count > 64)
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
        /// Call with _lock held, with the online snapshot taken BEFORE the lock. A reservation only bridges the trip: it
        /// is released once its player stands in the room and is done teleporting (occupancy covers them from there), or
        /// once they are not teleporting and not in the room after the grace (they left, or the teleport never happened).
        /// Teleport() does not move Location until the player lands, so this cannot be done at teleport time
        /// (2026-09-16: a player who left a room kept it blocked for the rest of the 30 s).
        /// </summary>
        private static void ReleaseLandedReservations(DateTime now, Dictionary<uint, Player> online)
        {
            if (online == null || _reservations.Count == 0)
                return;

            List<string> done = null;

            foreach (var kv in _reservations)
            {
                var claim = kv.Value;
                if (claim.Room == null || !online.TryGetValue(claim.Guid, out var player) || player.Teleporting)
                    continue;

                if (InRoom(player.Location, claim.Room, claim.Variation) || now - claim.Created >= ReservationGrace)
                    (done ??= new List<string>()).Add(kv.Key);
            }

            if (done == null)
                return;

            foreach (var key in done)
            {
                if (_reservations.TryGetValue(key, out var claim) && _reservedBy.TryGetValue(claim.Guid, out var reservedKey) && reservedKey == key)
                    _reservedBy.Remove(claim.Guid);
                _reservations.Remove(key);
            }
        }

        private static void ClearReservation(uint guid, string key)
        {
            lock (_lock)
            {
                if (_reservations.TryGetValue(key, out var claim) && claim.Guid == guid)
                    _reservations.Remove(key);

                if (_reservedBy.TryGetValue(guid, out var reservedKey) && reservedKey == key)
                    _reservedBy.Remove(guid);
            }
        }

        /// <summary>Admins do not count as occupants and are never moved on login (owner rulings 2026-09-16).</summary>
        private static bool IsStaff(Player player)
            => player.IsAdmin || (player.Session != null && player.Session.AccessLevel >= AccessLevel.Admin);

        private static bool InRoom(Position location, Room room, int? variation)
            => location != null && room.Cells.Contains(location.Cell) && VariationManager.SameVariationForVisibility(location.Variation, variation);

        /// <summary>
        /// Room keys with a living, non-staff player standing in their cells. Built OUTSIDE _lock: GetAllOnline takes
        /// PlayerManager's own lock. Landblock.players is not used - it updates a tick late and drifts on variation
        /// copies (Landblock.cs:1290-1305).
        /// </summary>
        private static HashSet<string> OccupiedRooms(List<Room> rooms, int? variation, uint excludeGuid, out Dictionary<uint, Player> online)
        {
            var occupied = new HashSet<string>();
            online = new Dictionary<uint, Player>();

            foreach (var player in PlayerManager.GetAllOnline())
            {
                online[player.Guid.Full] = player;

                var location = player.Location;
                if (location == null || player.IsDead || IsStaff(player) || player.Guid.Full == excludeGuid)
                    continue;

                foreach (var room in rooms)
                {
                    if (InRoom(location, room, variation))
                    {
                        occupied.Add(room.Key(variation));
                        break;
                    }
                }
            }

            return occupied;
        }

        // ---------------------------------------------------------------------------------------------------------
        // The step
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A player moved on a room plate. Runs in place of the stock plate activation, which would send "used too
        /// recently" / level messages, start cooldowns and warn about a missing activation target.
        /// </summary>
        public static void OnPlateStep(PressurePlate plate, Player player, List<Room> rooms)
        {
            if (rooms.Count == 0 || player.Session == null)
                return;

            var guid = player.Guid.Full;
            var variation = plate.Location?.Variation;
            var now = DateTime.UtcNow;

            // Occupancy + the online snapshot, before our lock (GetAllOnline takes PlayerManager's).
            var occupied = OccupiedRooms(rooms, variation, guid, out var online);

            // 1. Already on the way to a room: ignore, and do not use up the arming window. A delayed teleport (fog
            //    clearing holds it ~1 s) would otherwise let the same player take two rooms.
            lock (_lock)
            {
                PurgeExpired(now);
                ReleaseLandedReservations(now, online);
                if (_reservedBy.ContainsKey(guid))
                    return;
            }

            // 2. Arming window, plate-wide and silent (PressurePlateCooldown, 9056). One player handed a room per window.
            var cooldown = plate.PressurePlateCooldown ?? PressurePlate.DefaultPressurePlateCooldown;
            if (cooldown < 0)
                cooldown = 0;

            if (cooldown > 0 && now < plate.LastUseTime + TimeSpan.FromSeconds(cooldown))
                return;

            plate.LastUseTime = now;

            // 3. Find the first free room and reserve it, as one step.
            Room chosen = null;
            string chosenKey = null;

            lock (_lock)
            {
                PurgeExpired(now);

                if (plate.Location != null)
                    _roomLandblocks.Add((ushort)(plate.Location.Cell >> 16));

                if (_reservedBy.ContainsKey(guid))
                    return;

                foreach (var room in rooms)
                {
                    var key = room.Key(variation);

                    if (occupied.Contains(key) || _reservations.ContainsKey(key))
                        continue;

                    if (_holds.TryGetValue(key, out var hold) && hold.Guid != guid)
                        continue;

                    chosen = room;
                    chosenKey = key;
                    break;
                }

                if (chosen != null)
                {
                    _reservations[chosenKey] = NewReservation(guid, chosen, variation, now);
                    _reservedBy[guid] = chosenKey;

                    // One room per player: any hold they still had is spent.
                    var theirHolds = new List<string>();
                    foreach (var kv in _holds)
                        if (kv.Value.Guid == guid)
                            theirHolds.Add(kv.Key);
                    foreach (var key in theirHolds)
                        _holds.Remove(key);
                }
                else
                {
                    // Walking on the plate re-fires it many times a second - say it once per interval.
                    if (_lastFullMessage.TryGetValue(guid, out var last) && now - last < FullMessageInterval)
                        return;

                    _lastFullMessage[guid] = now;
                }
            }

            if (chosen == null)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(MessageAllTaken, ChatMessageType.Broadcast));
                return;
            }

            // 4. Teleport. The destination MUST carry the variation - a Position without one lands in BASE (variant
            //    review A9). AdjustDungeon may swap the cell for the one that really contains the point.
            var destination = new Position(chosen.LandingCell, chosen.X, chosen.Y, chosen.Z, chosen.QX, chosen.QY, chosen.QZ, chosen.QW, false, variation);
            WorldObject.AdjustDungeon(destination);

            if (!chosen.Cells.Contains(destination.Cell))
                log.Warn($"[RoomAssign] wcid {plate.WeenieClassId} room {chosen.Number}: landing cell 0x{chosen.LandingCell:X8} was adjusted to 0x{destination.Cell:X8}, which is not in the room's cell list - fix the room data.");

            player.EnqueueBroadcast(new GameMessageSound(player.Guid, plate.UseSound));

            var roomNumber = chosen.Number;
            var assignedRoom = chosen;
            var assignedKey = chosenKey;

            // Only clears when Location is already in the room, which Teleport() does NOT do before the player lands - the
            // reservation is normally released by ReleaseLandedReservations on the next step, portal use or login.
            var followUp = new ActionEventDelegate(ActionType.RoomAssign_AfterTeleport, () =>
            {
                if (InRoom(player.Location, assignedRoom, variation))
                    ClearReservation(guid, assignedKey);
            });

            WorldManager.ThreadSafeTeleport(player, destination, followUp);

            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You are sent to chamber {roomNumber}.", ChatMessageType.Broadcast));
        }

        // ---------------------------------------------------------------------------------------------------------
        // Room portals: every way in lands straight in a room (owner 2026-09-16)
        // ---------------------------------------------------------------------------------------------------------

        public const string MessagePortalAllTaken = "Every chamber is taken. Try again later.";

        /// <summary>
        /// The rooms behind a room portal, or null when the portal is not one (or the player is staff - admins use the
        /// portal's normal destination). Read from the WEENIE cache by WCID, so a summoned gateway (pass its
        /// OriginalPortal) and a recall both resolve to the real portal, and an /id change applies live.
        /// </summary>
        private static List<Room> GetPortalRooms(Player player, uint portalWcid)
        {
            if (player == null || IsStaff(player))
                return null;

            var weenie = DatabaseManager.World.GetCachedWeenie(portalWcid);
            if (weenie?.PropertiesDID == null || !weenie.PropertiesDID.TryGetValue(PropertyDataId.RoomAssignPlate, out var plateWcid))
                return null;

            return GetRooms(plateWcid);
        }

        /// <summary>Call with _lock held. True when no one else has the room: not standing in it, not on their way, not holding it.</summary>
        private static bool IsFreeFor(Room room, int? variation, uint guid, HashSet<string> occupied)
        {
            var key = room.Key(variation);

            if (occupied.Contains(key))
                return false;

            if (_reservations.TryGetValue(key, out var r) && r.Guid != guid)
                return false;

            if (_holds.TryGetValue(key, out var h) && h.Guid != guid)
                return false;

            return true;
        }

        /// <summary>
        /// Portal.CheckUseRequirements - runs before using the portal, before a recall's 2 s delay, and before summoning
        /// a gateway. False (and the message sent) when this is a room portal and every room is taken.
        /// </summary>
        public static bool CheckPortalHasRoom(Player player, uint portalWcid, Position destination)
        {
            try
            {
                var rooms = GetPortalRooms(player, portalWcid);
                if (rooms == null || rooms.Count == 0)
                    return true;

                var variation = destination?.Variation;
                var guid = player.Guid.Full;
                var now = DateTime.UtcNow;
                var occupied = OccupiedRooms(rooms, variation, guid, out var online);

                lock (_lock)
                {
                    PurgeExpired(now);
                    ReleaseLandedReservations(now, online);

                    // Already on the way to a room: ignore silently. Walking into a portal fires it several times before
                    // the teleport starts - each one would otherwise reserve again and teleport again (2026-09-16: 8 in 2 s).
                    if (_reservedBy.ContainsKey(guid))
                        return false;

                    foreach (var room in rooms)
                        if (IsFreeFor(room, variation, guid, occupied))
                            return true;

                    // Walking into a portal re-fires the use check - say it once per interval, like the plate.
                    if (_lastFullMessage.TryGetValue(guid, out var last) && now - last < FullMessageInterval)
                        return false;

                    _lastFullMessage[guid] = now;
                }

                player.Session?.Network.EnqueueSend(new GameMessageSystemChat(MessagePortalAllTaken, ChatMessageType.Broadcast));
                return false;
            }
            catch (Exception ex)
            {
                log.Error($"[RoomAssign] CheckPortalHasRoom for {player?.Name}, portal {portalWcid}: {ex}");
                return true;
            }
        }

        /// <summary>
        /// At the moment a portal or recall teleports: picks the first free room, reserves it (30 s, their holds spent) and
        /// returns its landing - carrying the destination's variation - or null to use the normal destination (not a room
        /// portal, staff, or the last room filled since the requirement check, e.g. during a recall's 2 s delay; the
        /// portal destination is the room plate's arrival spot, so they can step on it).
        /// </summary>
        public static Position AssignPortalRoom(Player player, uint portalWcid, Position destination, out int roomNumber)
        {
            roomNumber = 0;

            try
            {
                var rooms = GetPortalRooms(player, portalWcid);
                if (rooms == null || rooms.Count == 0)
                    return null;

                var variation = destination?.Variation;
                var guid = player.Guid.Full;
                var now = DateTime.UtcNow;
                var occupied = OccupiedRooms(rooms, variation, guid, out var online);

                Room chosen = null;
                lock (_lock)
                {
                    PurgeExpired(now);
                    ReleaseLandedReservations(now, online);

                    foreach (var room in rooms)
                    {
                        if (IsFreeFor(room, variation, guid, occupied))
                        {
                            chosen = room;
                            break;
                        }
                    }

                    if (chosen == null)
                        return null;

                    // A reservation they already had elsewhere is replaced - they are going here now.
                    if (_reservedBy.TryGetValue(guid, out var oldKey))
                    {
                        if (_reservations.TryGetValue(oldKey, out var old) && old.Guid == guid)
                            _reservations.Remove(oldKey);
                        _reservedBy.Remove(guid);
                    }

                    var key = chosen.Key(variation);
                    _reservations[key] = NewReservation(guid, chosen, variation, now);
                    _reservedBy[guid] = key;

                    // One room per player: any hold they still had is spent.
                    var theirHolds = new List<string>();
                    foreach (var kv in _holds)
                        if (kv.Value.Guid == guid)
                            theirHolds.Add(kv.Key);
                    foreach (var k in theirHolds)
                        _holds.Remove(k);

                    // Logout and login only look in landblocks where a room was handed out.
                    _roomLandblocks.Add((ushort)(chosen.LandingCell >> 16));
                }

                roomNumber = chosen.Number;
                log.Info($"[RoomAssign] {player.Name} (0x{player.Guid}) sent by portal {portalWcid} to room {chosen.Number}.");

                return new Position(chosen.LandingCell, chosen.X, chosen.Y, chosen.Z, chosen.QX, chosen.QY, chosen.QZ, chosen.QW, false, variation);
            }
            catch (Exception ex)
            {
                log.Error($"[RoomAssign] AssignPortalRoom for {player?.Name}, portal {portalWcid}: {ex}");
                return null;
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Logout and login
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The room (and the room plate's placement) whose cells contain this cell, found from the placed instances of
        /// that landblock + variation - so it works at login, before the landblock is loaded.
        /// </summary>
        private static Room FindRoomAt(uint cell, int? variation, out LandblockInstance plateInstance)
        {
            plateInstance = null;

            if (!IsRoomLandblock(cell))
                return null;

            var instances = DatabaseManager.World.GetCachedInstancesByLandblock((ushort)(cell >> 16), variation);
            if (instances == null)
                return null;

            foreach (var instance in instances)
            {
                var rooms = GetRooms(instance.WeenieClassId);
                if (rooms == null)
                    continue;

                foreach (var room in rooms)
                {
                    if (room.Cells.Contains(cell))
                    {
                        plateInstance = instance;
                        return room;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Top of Player.FinalizeLogout, while Location is still the spot the character is saved at. Every way out of the
        /// game ends there (logout, crash timeout, forced logoff, shutdown). Holds the room for 15 minutes.
        /// </summary>
        public static void OnLogout(Player player)
        {
            try
            {
                var location = player.Location;
                if (location == null || player.IsDead || IsStaff(player))
                    return;

                var holdTime = LogoutHoldTime;   // read once - an admin can change the setting at any moment
                if (holdTime <= TimeSpan.Zero)
                    return;

                var room = FindRoomAt(location.Cell, location.Variation, out _);
                if (room == null || !InRoom(location, room, location.Variation))
                    return;

                var now = DateTime.UtcNow;
                lock (_lock)
                {
                    PurgeExpired(now);
                    _holds[room.Key(location.Variation)] = new Claim { Guid = player.Guid.Full, Until = now + holdTime };
                }

                log.Info($"[RoomAssign] {player.Name} (0x{player.Guid}) logged out in room {room.Number} - held for {holdTime.TotalMinutes:0.##} minutes.");
            }
            catch (Exception ex)
            {
                log.Error($"[RoomAssign] OnLogout for {player?.Name}: {ex}");
            }
        }

        /// <summary>
        /// WorldManager.DoPlayerEnterWorld, before the player object is built. A character logging in inside a room that
        /// someone ELSE now has - standing in it, on their way in, or holding it - is moved before the client ever loads
        /// the room: into the first free room of the same plate (owner 2026-09-16), reserved exactly like a plate step,
        /// or to the room plate when every room is taken. Their own hold, or an empty room, lets them stay. Rewrites the
        /// SAVED location in place, like RiftManager.HandleLoginInRiftInstance. Returns the chat message for a moved
        /// character, or null when they stay.
        /// </summary>
        public static string HandleLogin(ACE.Entity.Models.Biota biota, AccessLevel accessLevel)
        {
            try
            {
                if (biota?.PropertiesPosition == null || !biota.PropertiesPosition.TryGetValue(PositionType.Location, out var location))
                    return null;

                if (accessLevel >= AccessLevel.Admin)
                    return null;

                if (biota.PropertiesBool != null && biota.PropertiesBool.TryGetValue(PropertyBool.IsAdmin, out var isAdmin) && isAdmin)
                    return null;

                var variation = location.VariationId;
                var room = FindRoomAt(location.ObjCellId, variation, out var plate);
                if (room == null || plate == null)
                    return null;

                var rooms = GetRooms(plate.WeenieClassId) ?? new List<Room> { room };

                var key = room.Key(variation);
                var guid = biota.Id;
                var now = DateTime.UtcNow;

                // Every room of the plate, for the fallback. Never count this character: after a client crash the old
                // session can still be standing in the room.
                var occupiedRooms = OccupiedRooms(rooms, variation, guid, out var online);
                var occupied = occupiedRooms.Contains(key);

                bool someoneElse;
                Room newRoom = null;
                lock (_lock)
                {
                    PurgeExpired(now);
                    ReleaseLandedReservations(now, online);

                    var heldByOther = _holds.TryGetValue(key, out var hold) && hold.Guid != guid;
                    var reservedByOther = _reservations.TryGetValue(key, out var reservation) && reservation.Guid != guid;

                    someoneElse = occupied || heldByOther || reservedByOther;

                    // Back in their own room (or an empty one): they are occupancy now, the hold is spent.
                    if (!someoneElse)
                        _holds.Remove(key);
                    else
                    {
                        // The same free-room test as a plate step. Their own room fails it: someone else has it.
                        foreach (var candidate in rooms)
                        {
                            var candidateKey = candidate.Key(variation);

                            if (occupiedRooms.Contains(candidateKey))
                                continue;

                            if (_reservations.TryGetValue(candidateKey, out var r) && r.Guid != guid)
                                continue;

                            if (_holds.TryGetValue(candidateKey, out var h) && h.Guid != guid)
                                continue;

                            newRoom = candidate;
                            break;
                        }

                        if (newRoom != null)
                        {
                            // Reserved while the client loads in; occupancy takes over once they stand there.
                            var newKey = newRoom.Key(variation);
                            _reservations[newKey] = NewReservation(guid, newRoom, variation, now);
                            _reservedBy[guid] = newKey;

                            // One room per player: the hold on the room they lost is spent.
                            var theirHolds = new List<string>();
                            foreach (var kv in _holds)
                                if (kv.Value.Guid == guid)
                                    theirHolds.Add(kv.Key);
                            foreach (var k in theirHolds)
                                _holds.Remove(k);
                        }
                    }
                }

                if (!someoneElse)
                    return null;

                if (newRoom != null)
                {
                    location.ObjCellId = newRoom.LandingCell;
                    location.PositionX = newRoom.X;
                    location.PositionY = newRoom.Y;
                    location.PositionZ = newRoom.Z;
                    location.RotationX = newRoom.QX;
                    location.RotationY = newRoom.QY;
                    location.RotationZ = newRoom.QZ;
                    location.RotationW = newRoom.QW;
                    location.VariationId = variation;

                    log.Info($"[RoomAssign] Character 0x{guid:X8} logged in to room {room.Number}, which someone else has - moved to free room {newRoom.Number}.");
                    return MessageMovedToRoom(newRoom.Number);
                }

                location.ObjCellId = plate.ObjCellId;
                location.PositionX = plate.OriginX;
                location.PositionY = plate.OriginY;
                location.PositionZ = plate.OriginZ;
                location.RotationX = plate.AnglesX;
                location.RotationY = plate.AnglesY;
                location.RotationZ = plate.AnglesZ;
                location.RotationW = plate.AnglesW;
                location.VariationId = plate.VariationId ?? variation;

                log.Info($"[RoomAssign] Character 0x{guid:X8} logged in to room {room.Number}, which someone else has - every room is taken, moved to the room plate.");
                return MessageMovedOut;
            }
            catch (Exception ex)
            {
                log.Error($"[RoomAssign] HandleLogin for 0x{biota?.Id:X8}: {ex}");
                return null;
            }
        }
    }
}
