using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

using ACE.Database;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// TEST TOOLS for Room Assign, all OFF unless the server property room_assign_test_tools is true (default false).
    /// They let one admin test Room Assign alone: admins can be made to count as players, and FAKE players can be put
    /// in rooms. A fake is not a world object - occupancy is only ever built by BuildOccupancy, so a fake is merged
    /// into that result and every rule downstream (IsFreeFor, PickRoom, holds, login) sees a taken room. Landing markers
    /// (WCID 1 standing on a landing) are here too. Everything is in memory: a restart clears it.
    ///
    /// Hooks in RoomAssignManager.cs: IsStaff (TestAdminCounts) and BuildOccupancy (TestAddFakes).
    /// </summary>
    public static partial class RoomAssignManager
    {
        /// <summary>True while the server allows the test tools at all.</summary>
        public static bool TestToolsOn => ServerConfig.room_assign_test_tools.Value;

        private const string TestToolsOffMessage = "Test tools are off on this server (room_assign_test_tools).";

        // OFF from the start. A login runs before any command can be typed, so to test an admin
        // relog: turn it on, log out, log in - the switch lives until the server restarts.
        private static volatile bool _testAdminCounts;

        /// <summary>Room key -> the made-up account standing in it. Behind _lock.</summary>
        private static readonly Dictionary<string, uint> _testFakes = new Dictionary<string, uint>();

        /// <summary>Fake accounts count down from here, far above any real account id. Behind _lock.</summary>
        private static uint _testNextAccount = 0xFFFFFF00;

        private static readonly Random _testRandom = new Random();

        public static bool TestAdminCounts
        {
            get => _testAdminCounts && TestToolsOn;
            set => _testAdminCounts = value;
        }

        /// <summary>End of BuildOccupancy (outside _lock). Keys carry the variation, so another dungeon's fakes never match.</summary>
        private static void TestAddFakes(Occupancy occupancy)
        {
            if (!TestToolsOn)
                return;

            lock (_lock)
                foreach (var kv in _testFakes)
                {
                    occupancy.Rooms.Add(kv.Key);
                    occupancy.Accounts.Add(kv.Value);
                }
        }

        /// <summary>where: "here", a room number, or "random" (count rooms that are free right now).</summary>
        public static List<string> TestFill(Player player, string where, int count)
        {
            if (!TestToolsOn)
                return new List<string> { TestToolsOffMessage };

            var lines = new List<string>();

            if (!BuilderResolve(player, out var sourceWcid, out var rooms, out var variation, out var error))
            {
                lines.Add(error);
                return lines;
            }

            var targets = new List<Room>();

            if (where == "here")
            {
                var current = CurrentRoom(player, rooms, variation);
                if (current == null)
                {
                    lines.Add("You are not standing in a room.");
                    return lines;
                }
                targets.Add(current);
            }
            else if (where == "random")
            {
                // Free right now: no real player, no fake. The caller is not excluded - their own room is not free.
                var occupancy = BuildOccupancy(rooms, variation, 0);
                var free = rooms.Where(r => !occupancy.Rooms.Contains(r.Key(variation))).ToList();

                for (var i = 0; i < count && free.Count > 0; i++)
                {
                    int index;
                    lock (_testRandom)
                        index = _testRandom.Next(free.Count);
                    targets.Add(free[index]);
                    free.RemoveAt(index);
                }

                if (targets.Count == 0)
                {
                    lines.Add("No free room to fill.");
                    return lines;
                }
            }
            else if (int.TryParse(where, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                var room = rooms.FirstOrDefault(r => r.Number == number);
                if (room == null)
                {
                    lines.Add($"Source {sourceWcid} has no room {number}.");
                    return lines;
                }
                targets.Add(room);
            }
            else
            {
                lines.Add("Fill what? here, a room number, or random [count].");
                return lines;
            }

            lock (_lock)
                foreach (var room in targets)
                {
                    var key = room.Key(variation);
                    if (_testFakes.ContainsKey(key))
                    {
                        lines.Add($"Room {room.Number} already has a fake player.");
                        continue;
                    }

                    _testFakes[key] = _testNextAccount--;
                    lines.Add($"Fake player put in room {room.Number} (source {sourceWcid}, v:{variation}).");
                }

            return lines;
        }

        /// <summary>number 0 = every fake, everywhere.</summary>
        public static List<string> TestClear(Player player, int number)
        {
            if (!TestToolsOn)
                return new List<string> { TestToolsOffMessage };

            var lines = new List<string>();

            if (number == 0)
            {
                int removed;
                lock (_lock)
                {
                    removed = _testFakes.Count;
                    _testFakes.Clear();
                }
                lines.Add($"Removed {removed} fake player(s).");
                return lines;
            }

            if (!BuilderResolve(player, out var sourceWcid, out var rooms, out var variation, out var error))
            {
                lines.Add(error);
                return lines;
            }

            var room = rooms.FirstOrDefault(r => r.Number == number);
            if (room == null)
            {
                lines.Add($"Source {sourceWcid} has no room {number}.");
                return lines;
            }

            bool had;
            lock (_lock)
                had = _testFakes.Remove(room.Key(variation));

            lines.Add(had ? $"Fake player removed from room {number}." : $"Room {number} has no fake player.");
            return lines;
        }

        /// <summary>Fills every room that is free right now except leaveOpen of them, picked at random.</summary>
        public static List<string> TestFillLeave(Player player, int leaveOpen)
        {
            if (!TestToolsOn)
                return new List<string> { TestToolsOffMessage };

            if (!BuilderResolve(player, out _, out var rooms, out var variation, out var error))
                return new List<string> { error };

            var occupancy = BuildOccupancy(rooms, variation, 0);
            var free = rooms.Count(r => !occupancy.Rooms.Contains(r.Key(variation)));
            var toFill = free - Math.Max(0, leaveOpen);

            if (toFill <= 0)
                return new List<string> { $"{free} room(s) are free - nothing to fill to leave {leaveOpen} open." };

            return TestFill(player, "random", toFill);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Landing markers (owner 2026-09-20): WCID 1 standing where a player would land, so the spot can be SEEN
        // ---------------------------------------------------------------------------------------------------------

        private const uint TestMarkerWcid = 1;

        /// <summary>Room key -> the marker standing on its landing. Temporary objects: never saved, gone on a restart. Behind _lock.</summary>
        private static readonly Dictionary<string, WorldObject> _testMarkers = new Dictionary<string, WorldObject>();

        /// <summary>
        /// Puts a marker on a room's landing - exactly where and how a player arrives (the AdjustDungeon-corrected spot, the
        /// arrival facing) - replacing the room's previous marker. It is a plain spawned object with a dynamic guid: no
        /// landblock_instance row, nothing in the database.
        /// </summary>
        private static void TestMarkLanding(Room room, int? variation, uint sourceWcid, List<string> lines)
        {
            if (!TestToolsOn)
                return;

            var key = room.Key(variation);

            WorldObject old;
            lock (_lock)
            {
                _testMarkers.TryGetValue(key, out old);
                _testMarkers.Remove(key);
            }
            old?.Destroy();

            var landing = Landing(room, variation, sourceWcid);
            if (landing == null)
                return;   // the caller warns about a landing outside its room

            var wo = ACE.Server.Factories.WorldObjectFactory.CreateNewWorldObject(TestMarkerWcid);
            if (wo == null)
            {
                lines.Add($"Marker weenie {TestMarkerWcid} could not be created.");
                return;
            }

            wo.Name = $"Chamber {room.Number} landing";
            wo.Location = new ACE.Entity.Position(landing);

            if (wo is Creature creature)
                creature.Attackable = false;

            // Ethereal, or it cannot stand where someone already stands: spawned on top of the admin (Player Here) the
            // physics engine slid it 1.3 m away to a free spot, so the marker lied about the landing (2026-09-20). The
            // same trick /createinst uses to spawn on the admin's own position.
            wo.Ethereal = true;

            if (!wo.EnterWorld())
            {
                lines.Add($"The marker for room {room.Number} could not be spawned on the landing - the spot may be blocked.");
                return;
            }

            lock (_lock)
                _testMarkers[key] = wo;

            lines.Add($"Marker (WCID {TestMarkerWcid}) stands on room {room.Number}'s landing. It is temporary - nothing is saved.");
        }

        /// <summary>Bumped by every Show and Clear, so a timed clear only removes the markers of the Show that started it. Behind _lock.</summary>
        private static int _testMarkerRun;

        /// <summary>
        /// all: a marker on every room's landing - for minutes, when given (owner 2026-09-20: show them for a walk through
        /// the dungeon, then have them gone again without pressing anything). clear: every marker removed.
        /// </summary>
        public static List<string> TestMarkers(Player player, string what, int minutes = 0)
        {
            if (!TestToolsOn)
                return new List<string> { TestToolsOffMessage };

            var lines = new List<string>();

            if (what == "clear")
            {
                lock (_lock)
                    _testMarkerRun++;

                List<WorldObject> markers;
                lock (_lock)
                {
                    markers = _testMarkers.Values.ToList();
                    _testMarkers.Clear();
                }

                foreach (var wo in markers)
                    wo.Destroy();

                lines.Add($"Removed {markers.Count} landing marker(s).");
                return lines;
            }

            if (!BuilderResolve(player, out var sourceWcid, out var rooms, out var variation, out var error))
            {
                lines.Add(error);
                return lines;
            }

            var quiet = new List<string>();
            foreach (var room in rooms)
                TestMarkLanding(room, variation, sourceWcid, quiet);

            int standing;
            lock (_lock)
                standing = rooms.Count(r => _testMarkers.ContainsKey(r.Key(variation)));

            int run;
            lock (_lock)
                run = ++_testMarkerRun;

            if (minutes > 0)
            {
                // On the world thread when the time is up, and only if no later Show or Clear has taken over.
                System.Threading.Tasks.Task.Delay(TimeSpan.FromMinutes(minutes)).ContinueWith(_ =>
                    WorldManager.EnqueueAction(new ACE.Server.Entity.Actions.ActionEventDelegate(ACE.Server.Entity.Actions.ActionType.EmoteManager_DebugDelay, () =>
                    {
                        List<WorldObject> expired;
                        lock (_lock)
                        {
                            if (run != _testMarkerRun)
                                return;

                            expired = _testMarkers.Values.ToList();
                            _testMarkers.Clear();
                        }

                        foreach (var wo in expired)
                            wo.Destroy();

                        log.Info($"[RoomAssign][DUNGEON] {expired.Count} landing marker(s) removed after {minutes} minute(s).");
                    })));
            }

            lines.Add($"{standing} of {rooms.Count} rooms have a marker on their landing (v:{variation}){(minutes > 0 ? $", for {minutes} minute(s)" : "")}. Temporary - nothing is saved.");
            lines.AddRange(quiet.Where(l => !l.StartsWith("Marker (WCID", StringComparison.Ordinal)));
            return lines;
        }

        /// <summary>Every room of the dungeon with who stands in it, its reservation, hold and last owner.</summary>
        public static List<string> TestStatus(Player player)
        {
            var lines = new List<string>();

            if (!BuilderResolve(player, out var sourceWcid, out var rooms, out var variation, out var error))
            {
                lines.Add(error);
                return lines;
            }

            var now = DateTime.UtcNow;
            var account = AccountOf(player);

            // Real players by room, outside _lock (GetAllOnline takes PlayerManager's lock).
            var standing = new Dictionary<string, List<string>>();
            foreach (var online in PlayerManager.GetAllOnline())
            {
                var room = CurrentRoom(online, rooms, variation);
                if (room == null)
                    continue;

                var key = room.Key(variation);
                if (!standing.TryGetValue(key, out var names))
                    standing[key] = names = new List<string>();

                names.Add(IsStaff(online) ? $"{online.Name} (staff, NOT counted)" : online.Name);
            }

            lines.Add($"Room Assign - source {sourceWcid}, v:{variation}, {rooms.Count} room(s). Admins count: {(_testAdminCounts ? "ON" : "off")}.");

            lock (_lock)
            {
                foreach (var room in rooms)
                {
                    var key = room.Key(variation);
                    var parts = new List<string>();

                    if (standing.TryGetValue(key, out var names))
                        parts.Add("in it: " + string.Join(", ", names));

                    if (_testFakes.ContainsKey(key))
                        parts.Add("FAKE player");

                    if (_reservations.TryGetValue(key, out var reservation))
                        parts.Add($"reserved for acct {reservation.Account} ({(reservation.Pending ? "pending" : "committed")}, {Math.Max(0, (reservation.Until - now).TotalSeconds):0} s)");

                    if (_holds.TryGetValue(key, out var hold))
                        parts.Add($"{hold.Kind} hold for acct {hold.Account} ({Math.Max(0, (hold.Until - now).TotalSeconds):0} s{(IsHoldActive(hold, now) ? "" : ", not active")})");

                    if (_roomOwner.TryGetValue(key, out var owner))
                        parts.Add($"last handed to acct {owner}");

                    lines.Add($"  Room {room.Number} (0x{room.Anchor:X8}): {(parts.Count == 0 ? "free" : string.Join("; ", parts))}");
                }

                _logoutHoldCredit.TryGetValue(account, out var credit);
                lines.Add($"You are acct {account}: {credit} logout hold(s) left.");
            }

            var graceTime = StartupGraceTime;
            var graceLeft = graceTime.Ticks - (now.Ticks - Interlocked.Read(ref _graceStartTicks));
            lines.Add($"Leave hold {LeaveHoldTime.TotalSeconds:0} s, logout hold {LogoutHoldTime.TotalMinutes.ToString("0.##", CultureInfo.InvariantCulture)} min, startup grace {(graceLeft > 0 ? $"{TimeSpan.FromTicks(graceLeft).TotalMinutes.ToString("0.#", CultureInfo.InvariantCulture)} min left" : "over")}.");

            return lines;
        }
    }
}
