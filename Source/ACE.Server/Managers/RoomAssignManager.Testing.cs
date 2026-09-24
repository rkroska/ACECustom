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

            if (!BuilderResolve(player, out var sourceWcid, out var rooms, out var variation, out var error, forWrite: true))
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

            if (!BuilderResolve(player, out var sourceWcid, out var rooms, out var variation, out var error, forWrite: true))
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

            if (!BuilderResolve(player, out _, out var rooms, out var variation, out var error, forWrite: true))
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

        /// <summary>How many landing markers stand right now, for the state line.</summary>
        public static int TestMarkerCount { get { lock (_lock) return _testMarkers.Count; } }

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

            // Never saved to the shard. Without this a marker standing at shutdown was written as a biota and came
            // back with the landblock after the restart, no longer tracked by anything here (found 2026-09-21).
            wo.SuppressShardPersistence = true;

            // A spawned object with no rot time is decayed by its landblock after 5 minutes (WorldObject_Decay,
            // DefaultTimeToRot) - every marker vanished 5 minutes after Show, whatever the switch said (found 2026-09-21).
            // -1 = never rot, the same value the boundary lanterns use.
            wo.TimeToRot = -1;
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

        // -----------------------------------------------------------------------------------------------------
        // Door preview (owner 2026-09-22: "a button to display the doors for 5 seconds so i can see what they
        // look like"). Every door in the owner's range, side by side in front of the player, facing them, then
        // gone. Nothing is saved and nothing is placed in a doorway - this only shows the models.
        // -----------------------------------------------------------------------------------------------------

        private static readonly List<WorldObject> _testDoors = new List<WorldObject>();

        /// <summary>Bumped by every Show and Clear, so a timed clear only removes the doors of the Show that started it. Behind _lock.</summary>
        private static int _testDoorRun;

        private const float TestDoorSpacing = 3.5f;
        private const float TestDoorAhead = 6f;

        /// <summary>
        /// Spawns one of every door weenie in a row in front of the player for <paramref name="seconds"/>, each named,
        /// each turned to face them. "clear" takes them away early.
        /// </summary>
        public static List<string> TestDoorShow(Player player, string what, int seconds)
        {
            // A test tool like the rest of this file (review 2026-09-24): it spawns objects wherever the admin stands.
            // "clear" still works with the tools off.
            if (!TestToolsOn && what != "clear")
                return new List<string> { TestToolsOffMessage };

            var lines = new List<string>();

            List<WorldObject> standing;
            lock (_lock)
            {
                _testDoorRun++;
                standing = _testDoors.ToList();
                _testDoors.Clear();
            }
            foreach (var wo in standing)
                wo.Destroy();

            if (what == "clear")
            {
                lines.Add($"Removed {standing.Count} preview door(s).");
                return lines;
            }

            if (player?.Location == null)
            {
                lines.Add("You have to be in the world to see the doors.");
                return lines;
            }

            var wcids = new List<uint>();
            using (var context = new WorldDbContext())
                wcids = context.Weenie
                    .Where(w => w.Type == (int)WeenieType.Door && w.ClassId >= 777700000 && w.ClassId <= 777799999)
                    .Select(w => w.ClassId)
                    .OrderBy(w => w)
                    .ToList();

            if (wcids.Count == 0)
            {
                lines.Add("No door weenie found in 777700000-777799999.");
                return lines;
            }

            // The player's own heading: forward is where they look, right is ninety degrees off it, so the row runs
            // across their view with the middle door straight ahead.
            var at = player.Location;
            var heading = Math.Atan2(2.0 * at.RotationW * at.RotationZ, 1.0 - 2.0 * at.RotationZ * at.RotationZ);
            var fx = (float)(-Math.Sin(heading));
            var fy = (float)Math.Cos(heading);
            var rx = fy;
            var ry = -fx;
            var facing = BuilderFacing(-fx, -fy);          // the doors look back at the player

            var spawned = 0;
            for (var i = 0; i < wcids.Count; i++)
            {
                var offset = (i - (wcids.Count - 1) / 2f) * TestDoorSpacing;
                var x = at.PositionX + fx * TestDoorAhead + rx * offset;
                var y = at.PositionY + fy * TestDoorAhead + ry * offset;

                var wo = ACE.Server.Factories.WorldObjectFactory.CreateNewWorldObject(wcids[i]);
                if (wo == null)
                    continue;

                // Same three rules the landing markers learned the hard way: never written to the shard, never
                // decayed by the landblock, and ethereal so physics cannot slide it off the spot it is meant to show.
                wo.SuppressShardPersistence = true;
                wo.TimeToRot = -1;
                wo.Ethereal = true;
                wo.Name = (wo.Name ?? "Door") + " (" + wcids[i] + ")";
                wo.Location = new ACE.Entity.Position(at.Cell, x, y, at.PositionZ + 0.05f,
                                                      facing.X, facing.Y, facing.Z, facing.W, false, at.Variation);

                if (!wo.EnterWorld())
                    continue;

                lock (_lock)
                    _testDoors.Add(wo);
                spawned++;
            }

            if (spawned == 0)
            {
                lines.Add("None of the doors could be spawned here - stand in an open space and try again.");
                return lines;
            }

            int run;
            lock (_lock)
                run = _testDoorRun;

            // On the world thread when the time is up, and only if no later Show or Clear has taken over.
            System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(seconds)).ContinueWith(_ =>
                WorldManager.EnqueueAction(new ACE.Server.Entity.Actions.ActionEventDelegate(ACE.Server.Entity.Actions.ActionType.EmoteManager_DebugDelay, () =>
                {
                    List<WorldObject> expired;
                    lock (_lock)
                    {
                        if (run != _testDoorRun)
                            return;

                        expired = _testDoors.ToList();
                        _testDoors.Clear();
                    }

                    foreach (var wo in expired)
                        wo.Destroy();

                    log.Info($"[RoomAssign][DUNGEON] {expired.Count} preview door(s) removed after {seconds} second(s).");
                })));

            lines.Add($"{spawned} door(s) in front of you for {seconds} second(s), named, facing you. Nothing is saved.");
            return lines;
        }

        /// <summary>
        /// all: a marker on every room's landing - for minutes, when given (owner 2026-09-20: show them for a walk through
        /// the dungeon, then have them gone again without pressing anything). clear: every marker removed.
        /// </summary>
        public static List<string> TestMarkers(Player player, string what, int minutes = 0)
        {
            // Clearing always works: markers never rot, so ones placed before the tools were turned off must still go.
            if (!TestToolsOn && what != "clear")
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

                // Strays: markers a build before 2026-09-21 let the shard save, reloaded with the landblock and tracked by
                // nothing. Only this tool makes a WCID 1 object named "Chamber N landing", so the name is the test.
                // Destroy also removes a saved one from the shard.
                var strays = player?.CurrentLandblock?.GetAllWorldObjectsForDiagnostics()
                    .Where(o => o.WeenieClassId == TestMarkerWcid && !markers.Contains(o) && o.Name != null
                        && o.Name.StartsWith("Chamber ", StringComparison.Ordinal) && o.Name.EndsWith(" landing", StringComparison.Ordinal))
                    .ToList() ?? new List<WorldObject>();

                foreach (var wo in strays)
                    wo.Destroy();

                lines.Add($"Removed {markers.Count} landing marker(s)" + (strays.Count > 0 ? $", and {strays.Count} left over from before a restart." : "."));
                return lines;
            }

            if (!BuilderResolve(player, out var sourceWcid, out var rooms, out var variation, out var error, forWrite: true))
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

            lines.Add($"Room Assign - source {sourceWcid}, v:{variation}, {rooms.Count} room(s). Admins count: {(TestAdminCounts ? "ON" : "off")}.");

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

                var hasCredit = _logoutHoldCredit.TryGetValue(CreditKey(account, rooms[0], variation), out var credit);
                lines.Add($"You are acct {account}: {(hasCredit ? $"{credit} logout hold(s) left in this dungeon" : "no logout-hold record in this dungeon (the next hand-out refills it)")}.");
            }

            lines.Add($"Leave hold {LeaveHoldTime.TotalSeconds:0} s, logout hold {LogoutHoldTime.TotalMinutes.ToString("0.##", CultureInfo.InvariantCulture)} min.");

            return lines;
        }
    }
}
