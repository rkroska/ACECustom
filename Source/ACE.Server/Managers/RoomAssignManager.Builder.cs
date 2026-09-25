using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using ACE.Database;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// DUNGEON BUILDER - the tools behind /zonecontrol dungeon and the Zone Control plugin's Dungeons tab: lay out a Room
    /// Assign dungeon in game. Rooms and their cells, landings, generators and doors are worked out from a map pin or
    /// from where the admin stands, checked against the DAT, and written to the world database (room list on the
    /// entrance weenie, landblock_instance rows), with the running landblock updated in place - no restart.
    ///
    /// EVERY write goes through BuilderMayWrite first: nothing is written below variation 3, so retail layers
    /// (NULL/0, 1, 2) can be looked at but never changed.
    ///
    /// Began as a test rig on 2026-09-20; made permanent 2026-09-21 (owner). The test-only half - fake players, admins
    /// counting as players, landing markers - is RoomAssignManager.Testing.cs, off unless room_assign_test_tools is set.
    /// </summary>
    public static partial class RoomAssignManager
    {
        /// <summary>The lowest variation the builder writes to. Owner 2026-09-21: every auto-assigned dungeon is v3 or up.</summary>
        public const int BuilderMinVariation = 3;

        /// <summary>
        /// The one gate in front of every builder WRITE. False, with the reason added to lines, when the rooms are in a
        /// retail layer. Keyed on the ROOMS' variation, not on where the admin stands.
        /// </summary>
        private static bool BuilderMayWrite(int? variation, List<string> lines)
        {
            if ((variation ?? 0) >= BuilderMinVariation)
                return true;

            lines.Add($"This dungeon is in layer v:{variation ?? 0}. Dungeon tools only write to v:{BuilderMinVariation} and up. Nothing changed.");
            return false;
        }


        // ---------------------------------------------------------------------------------------------------------
        // Which dungeon (owner 2026-09-21): the tab has a Dungeon dropdown, so a builder can be pointed at a dungeon the
        // admin is NOT standing in. The pick is per character, in memory, gone on a restart or with "select here".
        // ---------------------------------------------------------------------------------------------------------

        public const string BuilderListTag = "[[ZCDGL]]";
        public const string BuilderDoorsTag = "[[ZCDGD]]";

        /// <summary>The owner's custom item range (7777xxxxx) - where every barrier weenie lives.</summary>
        private const uint BuilderCustomLow = 777700000;
        private const uint BuilderCustomHigh = 777799999;

        /// <summary>
        /// Every door this shard can put in a doorway: the Door weenies in the owner's custom range, by name, so the
        /// tab can offer a choice instead of always taking the dungeon's most-placed one (owner 2026-09-22).
        /// Discovered, not hardcoded - a barrier added to the DB later shows up with no code change.
        ///   [[ZCDGD]]i=3|n=12|wcid=777704004|name=Legion Door|auto=1
        /// auto=1 marks the one the Door pin would pick by itself in the dungeon being worked on.
        /// </summary>
        public static List<string> BuilderDoorList(Player player)
        {
            var lines = new List<string>();

            List<uint> wcids;
            using (var context = new WorldDbContext())
                wcids = context.Weenie
                    .Where(w => w.Type == (int)WeenieType.Door && w.ClassId >= BuilderCustomLow && w.ClassId <= BuilderCustomHigh)
                    .Select(w => w.ClassId)
                    .ToList();

            uint auto = 0;
            if (BuilderResolve(player, out _, out var rooms, out var variation, out _) && rooms.Count > 0)
                auto = BuilderMostPlaced(rooms[0].LandingCell >> 16, variation, w => w.WeenieType == WeenieType.Door);
            if (auto == 0)
                auto = BuilderDefaultWallWcid;

            var found = wcids.OrderBy(w => w).Select(w => new { Wcid = w, Weenie = DatabaseManager.World.GetCachedWeenie(w) })
                             .Where(x => x.Weenie != null).ToList();

            for (var i = 0; i < found.Count; i++)
            {
                // The wire has its own separators: the same cleaning as every other name on it.
                var name = BuilderWireName(ACE.Entity.Models.WeenieExtensions.GetName(found[i].Weenie) ?? ("Door " + found[i].Wcid));
                lines.Add($"{BuilderDoorsTag}i={i}|n={found.Count}|wcid={found[i].Wcid}|name={name}|auto={(found[i].Wcid == auto ? 1 : 0)}");
            }

            if (lines.Count == 0)
                lines.Add($"No door weenie found in {BuilderCustomLow}-{BuilderCustomHigh}. The Door pin will use {BuilderDefaultWallWcid}.");

            return lines;
        }

        /// <summary>Character guid -> the dungeon picked in the tab's dropdown. Behind its own lock.</summary>
        private static readonly Dictionary<uint, (uint Wcid, int? Variation)> _builderSelected = new Dictionary<uint, (uint, int?)>();

        /// <summary>Every room dungeon the server knows: a source with a parsed room list, in a variation it is placed in.</summary>
        private static List<(uint Wcid, int? Variation)> BuilderKnownDungeons()
        {
            var seen = new List<(uint Wcid, int? Variation)>();
            lock (_lock)
                foreach (var key in _sourceSeen)
                {
                    var bar = key.IndexOf('|');
                    var text = key.Substring(bar + 1);
                    seen.Add((uint.Parse(key.Substring(0, bar), CultureInfo.InvariantCulture),
                        text.Length == 0 ? (int?)null : int.Parse(text, CultureInfo.InvariantCulture)));
                }

            return seen.Where(s => (GetRooms(s.Wcid)?.Count ?? 0) > 0).OrderBy(s => s.Variation ?? 0).ThenBy(s => s.Wcid).ToList();
        }

        private static bool BuilderHasPick(Player player)
        {
            if (player == null) return false;
            lock (_builderSelected) return _builderSelected.ContainsKey(player.Guid.Full);
        }

        /// <summary>True when the player stands in this dungeon: its landblock, its variation.</summary>
        private static bool BuilderStandsIn(Player player, List<Room> rooms, int? variation)
        {
            var location = player?.Location;
            return location != null && rooms != null && rooms.Count > 0
                && location.Cell >> 16 == rooms[0].LandingCell >> 16
                && VariationManager.NormalizeBase(location.Variation) == VariationManager.NormalizeBase(variation);
        }

        /// <summary>Added to lines, and false, when a command that acts WHERE THE ADMIN STANDS is used from outside the dungeon.</summary>
        private static bool BuilderMustStandIn(Player player, List<Room> rooms, int? variation, List<string> lines)
        {
            if (BuilderStandsIn(player, rooms, variation))
                return true;

            lines.Add($"You are not standing in that dungeon (0x{rooms[0].LandingCell >> 16:X4} v:{variation ?? 0}). This command acts where you stand - go there first, or use a map pin. Nothing changed.");
            return false;
        }

        /// <summary>
        /// One [[ZCDGL]] line per known dungeon, for the tab's dropdown:
        ///   [[ZCDGL]]i=0|n=3|src=777704023|v=3|kind=Portal|name=The Tyrant's Quarry|lb=01F7|rooms=29
        /// i = 0 starts a new list on the plugin side. n = 0 (one line) when there are none.
        /// </summary>
        public static List<string> BuilderList()
        {
            var known = BuilderKnownDungeons();
            if (known.Count == 0)
                return new List<string> { $"{BuilderListTag}i=0|n=0" };

            var lines = new List<string>();
            for (var i = 0; i < known.Count; i++)
            {
                string name = null;
                DatabaseManager.World.GetCachedWeenie(known[i].Wcid)?.PropertiesString?.TryGetValue(PropertyString.Name, out name);
                var rooms = GetRooms(known[i].Wcid);

                lines.Add($"{BuilderListTag}i={i}|n={known.Count}|src={known[i].Wcid}|v={known[i].Variation ?? 0}|kind={BuilderWeenieType(known[i].Wcid)}"
                    + $"|name={BuilderWireName(name)}|lb={rooms[0].LandingCell >> 16:X4}|rooms={rooms.Count}");
            }
            return lines;
        }

        /// <summary>"here" = back to the dungeon the admin stands in; else a source wcid and its variation.</summary>
        public static List<string> BuilderSelect(Player player, string wcidText, string variationText)
        {
            var lines = new List<string>();
            if (player == null)
                return lines;

            if (wcidText == null || wcidText.Equals("here", StringComparison.OrdinalIgnoreCase))
            {
                lock (_builderSelected)
                    _builderSelected.Remove(player.Guid.Full);
                lines.Add("Dungeon tools follow the dungeon you stand in.");
                return lines;
            }

            if (!uint.TryParse(wcidText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var wcid)
                || !int.TryParse(variationText ?? "", NumberStyles.Integer, CultureInfo.InvariantCulture, out var variation))
            {
                lines.Add("/zonecontrol dungeon select here | <source wcid> <variation>   (see /zonecontrol dungeon list)");
                return lines;
            }

            var pick = BuilderKnownDungeons().FirstOrDefault(d => d.Wcid == wcid && (d.Variation ?? 0) == variation);
            if (pick.Wcid == 0)
            {
                lines.Add($"No room dungeon {wcid} is known in v:{variation}. See /zonecontrol dungeon list.");
                return lines;
            }

            lock (_builderSelected)
                _builderSelected[player.Guid.Full] = pick;

            string name = null;
            DatabaseManager.World.GetCachedWeenie(wcid)?.PropertiesString?.TryGetValue(PropertyString.Name, out name);
            lines.Add($"Dungeon tools now work on {name ?? wcid.ToString(CultureInfo.InvariantCulture)} ({wcid}, v:{variation}), wherever you stand. Commands that act where you stand still need you inside it.");
            return lines;
        }

        /// <summary>
        /// Teleports the admin to the dungeon's ENTRANCE: where the source weenie is placed in the world - or, for a plate
        /// that a portal points at, that portal - a few steps in front of it so the arrival does not set it off.
        /// </summary>
        public static List<string> BuilderEntrance(Player player)
        {
            var lines = new List<string>();

            if (!BuilderResolve(player, out var sourceWcid, out _, out var variation, out var error))
            {
                lines.Add(error);
                return lines;
            }

            List<LandblockInstance> placed;
            using (var context = new WorldDbContext())
            {
                var pointing = context.WeeniePropertiesDID
                    .Where(d => d.Type == (ushort)PropertyDataId.RoomAssignPlate && d.Value == sourceWcid)
                    .Select(d => d.ObjectId).ToList();

                placed = context.LandblockInstance
                    .Where(i => i.WeenieClassId == sourceWcid || pointing.Contains(i.WeenieClassId)).ToList();
            }

            // One placed in the picked dungeon's own variation first (a v3 pick never lands you at a v5 copy), then a portal
            // before a plate: the portal is what a player walks into.
            var wanted = VariationManager.NormalizeBase(variation);
            var entrance = placed
                .OrderBy(i => VariationManager.NormalizeBase(i.VariationId) == wanted ? 0 : 1)
                .ThenBy(i => BuilderWeenieType(i.WeenieClassId) == WeenieType.Portal ? 0 : 1)
                .ThenBy(i => i.Guid)
                .FirstOrDefault();

            if (entrance == null)
            {
                lines.Add($"Source {sourceWcid} is not placed anywhere in the world database - it has no entrance to go to.");
                return lines;
            }

            // 3 units along the way the entrance faces, so the admin lands in front of it rather than inside it.
            BuilderDirection(entrance.AnglesW, entrance.AnglesZ, out var dx, out var dy);
            var at = new ACE.Entity.Position(entrance.ObjCellId, entrance.OriginX + dx * 3f, entrance.OriginY + dy * 3f, entrance.OriginZ + 0.05f,
                entrance.AnglesX, entrance.AnglesY, entrance.AnglesZ, entrance.AnglesW, false, entrance.VariationId);

            WorldManager.ThreadSafeTeleport(player, at);

            lines.Add($"Entrance of {sourceWcid}: 0x{entrance.Guid:X8} at 0x{entrance.ObjCellId:X8} v:{entrance.VariationId ?? 0}"
                + (placed.Count > 1 ? $" (the first of {placed.Count} placements)." : "."));
            return lines;
        }

        /// <summary>
        /// The dungeon a builder command is about: the one picked in the tab's dropdown (BuilderSelect), else the one the
        /// player stands in a room of, else the room source placed in the player's variation and landblock (a portal before
        /// a plate), else - for looking only - any placed source. A WRITE (<paramref name="forWrite"/>) never acts on a guess
        /// (review 2026-09-24): with nothing picked it needs the dungeon around the player, so a command typed in town cannot
        /// edit whichever dungeon happens to sort first.
        /// </summary>
        private static bool BuilderResolve(Player player, out uint sourceWcid, out List<Room> rooms, out int? variation, out string error, bool forWrite = false)
        {
            sourceWcid = 0;
            rooms = null;
            variation = null;
            error = null;

            var location = player?.Location;
            var found = location != null ? FindRoomAt(location.Cell, location.Variation) : null;

            (uint Wcid, int? Variation) picked = default;
            if (player != null)
                lock (_builderSelected)
                    _builderSelected.TryGetValue(player.Guid.Full, out picked);

            if (picked.Wcid != 0)
            {
                sourceWcid = picked.Wcid;
                variation = picked.Variation;
            }
            else if (found != null)
            {
                sourceWcid = found.SourceWcid;
                variation = location.Variation;
            }
            else
            {
                var seen = BuilderKnownDungeons();

                if (seen.Count == 0)
                {
                    error = "No room source is placed in a room variation.";
                    return false;
                }

                // Outside every room (always the case for Add: a new room's cell is in no list yet) the dungeon is the one
                // around the player: same variation first, then same landblock, then a portal before a plate.
                var here = VariationManager.NormalizeBase(location?.Variation);
                var block = location != null ? location.Cell >> 16 : 0;

                var pick = seen
                    .OrderBy(s => location != null && VariationManager.NormalizeBase(s.Variation) == here ? 0 : 1)
                    .ThenBy(s => BuilderLandblock(s.Wcid) == block ? 0 : 1)
                    .ThenBy(s => BuilderWeenieType(s.Wcid) == WeenieType.Portal ? 0 : 1)
                    .ThenBy(s => s.Wcid)
                    .First();

                if (forWrite && (location == null || VariationManager.NormalizeBase(pick.Variation) != here || BuilderLandblock(pick.Wcid) != block))
                {
                    error = "You are not in a dungeon and none is picked. Pick one in the Dungeon dropdown (/zonecontrol dungeon select <wcid> <variation>), or go there. Nothing changed.";
                    return false;
                }

                sourceWcid = pick.Wcid;
                variation = pick.Variation;
            }

            rooms = GetRooms(sourceWcid);
            if (rooms == null || rooms.Count == 0)
            {
                error = $"Source {sourceWcid} has no parsed room list.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Nudge (owner 2026-09-21): a placed wall or generator moved by a small step along an axis, or turned, after a
        /// pin put it in place. dx/dy in game units (north = +y), turn in degrees (clockwise seen from above). The move is
        /// refused when the spot would leave every indoor cell of the dungeon - a wall cannot be nudged into rock.
        /// </summary>
        public static List<string> BuilderNudge(Player player, uint guid, float dx, float dy, float turn)
        {
            var lines = new List<string>();

            if (!BuilderResolve(player, out _, out var rooms, out var variation, out var error, forWrite: true))
            {
                lines.Add(error);
                return lines;
            }

            if (!BuilderMayWrite(variation, lines))
                return lines;

            LandblockInstance row;
            using (var context = new WorldDbContext())
                row = context.LandblockInstance.FirstOrDefault(i => i.Guid == guid);

            var landblock = rooms[0].LandingCell >> 16;
            if (row == null || row.ObjCellId >> 16 != landblock || VariationManager.NormalizeBase(row.VariationId) != VariationManager.NormalizeBase(variation))
            {
                lines.Add($"0x{guid:X8} is not a placed object of this dungeon (0x{landblock:X4} v:{variation ?? 0}). Nothing moved.");
                return lines;
            }

            var x = row.OriginX + dx;
            var y = row.OriginY + dy;
            var z = row.OriginZ;

            // Which indoor cell the new spot is in: the same one first, then any cell of the dungeon at this height.
            var dat = BuilderDatCells(landblock);
            uint cell = 0;
            if (BuilderInsideCell(row.ObjCellId, variation, x, y, z))
                cell = row.ObjCellId;
            else
                foreach (var candidate in dat.Where(kv => Math.Abs(kv.Value.Centre.Z - z) < 4f).OrderBy(kv => (kv.Value.Centre.X - x) * (kv.Value.Centre.X - x) + (kv.Value.Centre.Y - y) * (kv.Value.Centre.Y - y)).Take(6))
                    if (BuilderInsideCell(candidate.Key, variation, x, y, z)) { cell = candidate.Key; break; }

            if (cell == 0)
            {
                lines.Add($"That step would put 0x{guid:X8} outside every cell of the dungeon ({x:0.##}, {y:0.##}). Nothing moved.");
                return lines;
            }

            var rotation = new System.Numerics.Quaternion(row.AnglesX, row.AnglesY, row.AnglesZ, row.AnglesW);
            if (Math.Abs(turn) > 0.01f)
                rotation = System.Numerics.Quaternion.Normalize(rotation * System.Numerics.Quaternion.CreateFromYawPitchRoll(0, 0, (float)(-turn * Math.PI / 180.0)));

            var to = new ACE.Entity.Position(cell, x, y, z, rotation.X, rotation.Y, rotation.Z, rotation.W, false, variation);
            if (!BuilderMoveInstance(player, guid, to, lines))
                return lines;

            string name = null;
            DatabaseManager.World.GetCachedWeenie(row.WeenieClassId)?.PropertiesString?.TryGetValue(PropertyString.Name, out name);
            lines.Add($"{name ?? row.WeenieClassId.ToString(CultureInfo.InvariantCulture)} 0x{guid:X8} nudged"
                + (Math.Abs(dx) > 0.001f || Math.Abs(dy) > 0.001f ? $" by {dx:0.##} east, {dy:0.##} north" : "")
                + (Math.Abs(turn) > 0.01f ? $" and turned {turn:0.#} degrees" : "") + $" - now at [{x:0.##} {y:0.##}] in cell 0x{cell & 0xFFFF:X4}.");
            return lines;
        }

        /// <summary>The landblock a source's rooms are on, 0 when it has no parsed list.</summary>
        private static uint BuilderLandblock(uint wcid)
        {
            var rooms = GetRooms(wcid);
            return rooms != null && rooms.Count > 0 ? rooms[0].LandingCell >> 16 : 0;
        }

        private static WeenieType BuilderWeenieType(uint wcid)
            => DatabaseManager.World.GetCachedWeenie(wcid)?.WeenieType ?? WeenieType.Undef;

        public const string BuilderStateTag = "[[ZCDG]]";
        public const string BuilderMapTag = "[[ZCDGM]]";

        /// <summary>Zone Control's wire uses | , = ~ as separators, and this payload also : and + - none may ride in a name.
        /// The one name cleaner for the wire: Kill Reward's reward lines use it too.</summary>
        internal static string BuilderWireName(string name)
        {
            var text = new System.Text.StringBuilder(name ?? "");
            foreach (var c in new[] { '|', ',', '=', '~', ':', '+', ';' })
                text.Replace(c, ' ');
            return text.ToString().Trim();
        }

        /// <summary>
        /// One machine-readable line for the Zone Control plugin's Dungeons tab, in the [[ZC*]] wire shape:
        ///   [[ZCDG]]src=777704023|kind=Portal|name=The Tyrant's Quarry|v=3|lb=01F7|write=1|tools=0|admin=0|rooms=1:-,2:F,3:PH|who=3:Some Player|markers=0|holds=15,60,1,15|expires=3:43
        /// write = the builder may write here (variation 3 or up); tools = the test tools are allowed on this server;
        /// admin = admins currently count as players. Per room: P a counted player stands in it, F fake player,
        /// R reserved, H actively held, - free. who = the names behind P. src=0 when no room dungeon is found.
        /// APPEND-ONLY: new fields go on the end, the plugin skips keys it does not know.
        /// </summary>
        public static string BuilderState(Player player)
        {
            var tools = TestToolsOn ? 1 : 0;
            var admin = TestAdminCounts ? 1 : 0;
            var holds = BuilderHolds();

            if (!BuilderResolve(player, out var sourceWcid, out var rooms, out var variation, out _))
                return $"{BuilderStateTag}src=0|kind=|name=|v=|lb=0000|write=0|tools={tools}|admin={admin}|rooms=|who=|markers={TestMarkerCount}|sel={(BuilderHasPick(player) ? 1 : 0)}|in=0|holds={holds}";

            var now = DateTime.UtcNow;

            // Fakes are left out here so P means a real player; F comes from the test tools' own table below.
            var real = new HashSet<string>();
            var who = new Dictionary<int, List<string>>();
            foreach (var online in PlayerManager.GetAllOnline())
            {
                var room = CurrentRoom(online, rooms, variation);
                if (room != null && !IsStaff(online))
                {
                    real.Add(room.Key(variation));
                    if (!who.TryGetValue(room.Number, out var names))
                        who[room.Number] = names = new List<string>();
                    names.Add(BuilderWireName(online.Name ?? "?"));
                }
            }

            var parts = new List<string>();
            var expires = new List<string>();   // room:seconds left, for the claims that run out on their own
            var held = new List<string>();      // room:seconds the owning account has had it (the tenure clock)
            var heldBy = new List<string>();    // room:name - the character a hold is waiting for
            lock (_lock)
                foreach (var room in rooms)
                {
                    var key = room.Key(variation);
                    var flags = "";
                    if (real.Contains(key)) flags += "P";
                    if (_testFakes.ContainsKey(key)) flags += "F";
                    // Only a LIVE reservation: ContainsKey alone kept showing "Reserved" on a room whose reservation
                    // had already run out but had not been purged yet - with no time beside it, because the time is only
                    // sent while it is still in the future. The row then sat there until something else purged it
                    // (owner 2026-09-23).
                    var reservedNow = _reservations.TryGetValue(key, out var resv) && resv.Until > now;
                    if (reservedNow) flags += "R";
                    if (_holds.TryGetValue(key, out var hold) && IsHoldActive(hold, now)) flags += "H";
                    parts.Add($"{room.Number}:{(flags.Length == 0 ? "-" : flags)}");

                    // A hold and a reservation both end by themselves, and how long is left is the thing you watch while
                    // testing. The reservation is the shorter of the two, so it wins when a room somehow carries both.
                    var left = TimeSpan.MinValue;
                    if (hold != null && IsHoldActive(hold, now))
                        left = hold.Until - now;
                    if (reservedNow && (left == TimeSpan.MinValue || resv.Until - now < left))
                        left = resv.Until - now;
                    if (left > TimeSpan.Zero)
                        expires.Add($"{room.Number}:{(int)Math.Ceiling(left.TotalSeconds)}");

                    // How long the account that owns this room has had it. It survives a trip out and back, so a player
                    // standing in a room shows a tenure, not a stopwatch restarted by every teleport.
                    if (_roomOwnerSince.TryGetValue(key, out var ownedSince) && ownedSince <= now)
                        held.Add($"{room.Number}:{(int)(now - ownedSince).TotalSeconds}");

                    // Who a HOLD is waiting for. "Hold" alone says nothing about whose it is, and a hold for your own
                    // account behaves the opposite way to someone else's - you walk straight back in (owner 2026-09-23).
                    if (hold != null && IsHoldActive(hold, now) && !string.IsNullOrWhiteSpace(hold.Name))
                        heldBy.Add($"{room.Number}:{BuilderWireName(hold.Name)}");
                    else if (reservedNow && !string.IsNullOrWhiteSpace(resv.Name))
                        heldBy.Add($"{room.Number}:{BuilderWireName(resv.Name)}");
                }

            string sourceName = null;
            DatabaseManager.World.GetCachedWeenie(sourceWcid)?.PropertiesString?.TryGetValue(PropertyString.Name, out sourceName);
            var zoneShare = IsZoneShareSource(sourceWcid);
            var kr = KillRewardOf(sourceWcid);

            return $"{BuilderStateTag}src={sourceWcid}|kind={BuilderWeenieType(sourceWcid)}|name={BuilderWireName(sourceName)}|v={variation}"
                + $"|lb={rooms[0].LandingCell >> 16:X4}|write={((variation ?? 0) >= BuilderMinVariation ? 1 : 0)}|tools={tools}|admin={admin}"
                + $"|rooms={string.Join(",", parts)}|who={string.Join(",", who.OrderBy(w => w.Key).Select(w => w.Key + ":" + string.Join("+", w.Value)))}"
                + $"|markers={TestMarkerCount}|sel={(BuilderHasPick(player) ? 1 : 0)}|in={(BuilderStandsIn(player, rooms, variation) ? 1 : 0)}"   // appended 2026-09-21: how many landing markers stand, so the tab's switch shows the truth
                + $"|holds={holds}"   // appended 2026-09-22: the four hold settings, so the tab can show what a hold test is actually running against
                + $"|expires={string.Join(",", expires)}"   // appended 2026-09-22: seconds left on each timed claim, so a hold can be watched running out
                + $"|since={string.Join(",", held)}"   // appended 2026-09-22: how long the owning account has had each room
                + $"|heldby={string.Join(",", heldBy)}"   // appended 2026-09-23: the character each hold is waiting for
                + $"|zshare={(zoneShare ? 1 : 0)}|zsharen={(zoneShare ? ZoneShareManager.CountInDungeon(sourceWcid, variation) : 0)}"   // appended 2026-09-23: Zone Share switch + players sharing now
                + $"|kr={(kr.Enabled ? 1 : 0)}|krlist={KillRewardManager.Wire(kr)}";   // appended 2026-09-23: Kill Reward - on, and every reward
        }

        /// <summary>
        /// The room_assign_* hold settings for the state line: logout minutes, leave seconds, renewals, and a fourth field that
        /// is always 0 - it was the startup grace (retired 2026-09-24) and stays so the field order never moves (the wire is
        /// append-only). These are the CLAMPED values the manager really uses, not the raw properties, so what the plugin shows
        /// is what a hold will actually do.
        /// </summary>
        private static string BuilderHolds()
        {
            var logout = (long)LogoutHoldTime.TotalMinutes;
            var leave = (long)LeaveHoldTime.TotalSeconds;
            var renewals = Math.Clamp(ServerConfig.room_assign_logout_hold_renewals.Value, 0, MaxLogoutHoldRenewals);
            return $"{logout},{leave},{renewals},0";
        }

        /// <summary>
        /// Zone Share on/off for the selected dungeon (owner 2026-09-23): PropertyBool.RoomAssignZoneShare on the room
        /// source's weenie in the world database - beside its room list, so it ships with the source's SQL - then the weenie
        /// cache is dropped so the kill hooks read the new value. The SQL is logged, as every builder write is.
        /// </summary>
        public static List<string> BuilderSetZoneShare(Player player, bool on)
        {
            var lines = new List<string>();

            if (!BuilderResolve(player, out var sourceWcid, out _, out var variation, out var error, forWrite: true))
            {
                lines.Add(error);
                return lines;
            }

            if (!BuilderMayWrite(variation, lines))
                return lines;

            using (var context = new WorldDbContext())
            {
                var row = context.WeeniePropertiesBool.FirstOrDefault(r => r.ObjectId == sourceWcid && r.Type == (ushort)PropertyBool.RoomAssignZoneShare);
                if (row == null)
                    context.WeeniePropertiesBool.Add(new WeeniePropertiesBool { ObjectId = sourceWcid, Type = (ushort)PropertyBool.RoomAssignZoneShare, Value = on });
                else
                    row.Value = on;
                context.SaveChanges();
            }

            DatabaseManager.World.ClearCachedWeenie(sourceWcid);
            GetRooms(sourceWcid);   // re-warm the cache the room code reads

            log.Info($"[RoomAssign][DUNGEON] {player.Name} set Zone Share {(on ? "ON" : "off")} on wcid {sourceWcid}. SQL: INSERT INTO weenie_properties_bool (object_Id, type, value) VALUES ({sourceWcid}, {(ushort)PropertyBool.RoomAssignZoneShare}, {(on ? 1 : 0)}) ON DUPLICATE KEY UPDATE value = {(on ? 1 : 0)};");

            string sourceName = null;
            DatabaseManager.World.GetCachedWeenie(sourceWcid)?.PropertiesString?.TryGetValue(PropertyString.Name, out sourceName);
            lines.Add(on
                ? $"Zone Share ON for {sourceName ?? sourceWcid.ToString()}: everyone in the dungeon shares kill XP, luminance and kill tasks as one fellowship."
                : $"Zone Share off for {sourceName ?? sourceWcid.ToString()}: normal fellowship rules.");

            return lines;
        }

        /// <summary>
        /// Kill Reward for the selected dungeon (owner 2026-09-23): edits its settings - PropertyString.RoomAssignKillReward on
        /// the room source's weenie in the world database, beside its room list, so it ships with the source's SQL - then drops
        /// the weenie cache so the kill hook reads the new value. The edit (KillRewardManager.Edit, shared with zones) refuses
        /// what it cannot do - turning on with no reward, an unknown WCID - and then nothing is written. The SQL is logged, as
        /// every builder write is.
        /// </summary>
        public static List<string> BuilderSetKillReward(Player player, Func<ACE.Server.Managers.ZoneControl.KillRewardConfig, string> edit)
        {
            var lines = new List<string>();

            if (!BuilderResolve(player, out var sourceWcid, out _, out var variation, out var error, forWrite: true))
            {
                lines.Add(error);
                return lines;
            }

            if (!BuilderMayWrite(variation, lines))
                return lines;

            // One locked read-change-write (owner 2026-09-23: two admins at once): read the CURRENT settings from the database,
            // apply this one edit, write - so each admin's edit touches only its own reward, and the last save to it wins.
            ACE.Server.Managers.ZoneControl.KillRewardConfig cfg;
            string raw;
            lock (_killRewardWriteLock)
            {
                using (var context = new WorldDbContext())
                {
                    var row = context.WeeniePropertiesString.FirstOrDefault(r => r.ObjectId == sourceWcid && r.Type == (ushort)PropertyString.RoomAssignKillReward);
                    cfg = ACE.Server.Managers.ZoneControl.KillRewardConfig.Parse(row?.Value);

                    var refused = edit(cfg);
                    if (refused != null)
                    {
                        lines.Add(refused + " Nothing changed.");
                        return lines;
                    }

                    raw = cfg.Format();
                    if (row == null)
                        context.WeeniePropertiesString.Add(new WeeniePropertiesString { ObjectId = sourceWcid, Type = (ushort)PropertyString.RoomAssignKillReward, Value = raw });
                    else
                        row.Value = raw;
                    context.SaveChanges();
                }

                DatabaseManager.World.ClearCachedWeenie(sourceWcid);
            }
            GetRooms(sourceWcid);   // re-warm the cache the room code reads

            log.Info($"[RoomAssign][DUNGEON] {player.Name} set Kill Reward '{raw}' on wcid {sourceWcid}. SQL: INSERT INTO weenie_properties_string (object_Id, type, value) VALUES ({sourceWcid}, {(ushort)PropertyString.RoomAssignKillReward}, '{raw}') ON DUPLICATE KEY UPDATE value = '{raw}';");

            lines.Add("Kill Reward " + KillRewardManager.Describe(cfg) + ".");
            return lines;
        }

        private static readonly object _killRewardWriteLock = new object();

        /// <summary>An item's name for messages and the wire, or "WCID n" when it has none.</summary>
        internal static string ItemName(uint wcid)
        {
            if (wcid == 0) return "no item";
            string name = null;
            DatabaseManager.World.GetCachedWeenie(wcid)?.PropertiesString?.TryGetValue(PropertyString.Name, out name);
            return string.IsNullOrWhiteSpace(name) ? "WCID " + wcid : name.Trim();
        }

        private static readonly object _roomListWriteLock = new object();

        /// <summary>A float as the room list writes it: invariant, up to 6 decimals.</summary>
        private static string BuilderF(float value) => value.ToString("0.######", CultureInfo.InvariantCulture);

        /// <summary>One room-list entry: "number|0xLANDING [x y z] qw qx qy qz|0xCELL,0xCELL".</summary>
        private static string BuilderRoomEntry(int number, uint landingCell, float x, float y, float z, float qw, float qx, float qy, float qz, IEnumerable<uint> cells)
            => $"{number}|0x{landingCell:X8} [{BuilderF(x)} {BuilderF(y)} {BuilderF(z)}] {BuilderF(qw)} {BuilderF(qx)} {BuilderF(qy)} {BuilderF(qz)}|"
                + string.Join(",", cells.OrderBy(c => c).Select(c => $"0x{c:X8}"));

        /// <summary>The number an entry starts with, or null.</summary>
        private static int? BuilderEntryNumber(string entry)
            => int.TryParse(entry.Split('|')[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : (int?)null;

        /// <summary>
        /// The ONE way the builder changes a source's room list (review 2026-09-24): under one lock, read the list from the
        /// world DATABASE row (never the weenie cache, which may be older than a hand edit), apply <paramref name="change"/>,
        /// check the result parses, write it, drop the cache. Two admins editing at once each apply their change to the
        /// other's result instead of overwriting it. <paramref name="change"/> returns the new list, or null after adding
        /// its own reason to <paramref name="lines"/>. The SQL is logged, so any edit can be replayed to live by hand.
        /// </summary>
        private static bool BuilderSaveRoomList(Player player, uint sourceWcid, string what, Func<string, string> change, List<string> lines, out string newRaw)
        {
            newRaw = null;

            lock (_roomListWriteLock)
            {
                using (var context = new WorldDbContext())
                {
                    var row = context.WeeniePropertiesString.FirstOrDefault(r => r.ObjectId == sourceWcid && r.Type == (ushort)PropertyString.RoomAssignRooms);
                    if (row == null)
                    {
                        lines.Add($"Source {sourceWcid} has no RoomAssignRooms row in the world database.");
                        return false;
                    }

                    var changed = change(row.Value ?? "");
                    if (changed == null)
                        return false;

                    // Same parser the live list goes through: wrong landblock, outdoor cell, zero rotation, a cell in two
                    // rooms are all refused here, before anything is written.
                    if (!TryParseRooms(changed, out _, out var parseError))
                    {
                        lines.Add($"Nothing changed - the new list does not parse: {parseError}.");
                        return false;
                    }

                    log.Info($"[RoomAssign][DUNGEON] {player.Name}: {what} on wcid {sourceWcid}. List BEFORE: {row.Value}");

                    row.Value = changed;
                    context.SaveChanges();
                    newRaw = changed;
                }

                DatabaseManager.World.ClearCachedWeenie(sourceWcid);
            }

            log.Info($"[RoomAssign][DUNGEON] {player.Name}: {what} on wcid {sourceWcid}. SQL: UPDATE weenie_properties_string SET value = '{newRaw}' WHERE object_Id = {sourceWcid} AND type = {(ushort)PropertyString.RoomAssignRooms};");
            return true;
        }


        /// <summary>
        /// Adds a room to the source's REAL list: the weenie's RoomAssignRooms string in the world database, then the weenie
        /// cache is dropped so the next use re-parses it. The landing is where the player stands, facing as they face; the
        /// room is the one cell they stand in. Its number is the lowest one free, or the one asked for. The new list must
        /// parse, or nothing is written.
        /// </summary>
        public static List<string> BuilderAddRoom(Player player, int wanted = 0)
        {
            var lines = new List<string>();

            if (!BuilderResolve(player, out var sourceWcid, out var rooms, out var variation, out var error, forWrite: true))
            {
                lines.Add(error);
                return lines;
            }

            if (!BuilderMayWrite(variation, lines))
                return lines;

            // Standing in the dungeon also means standing in its variation - BuilderStandsIn checks both.
            if (!BuilderMustStandIn(player, rooms, variation, lines))
                return lines;

            var location = player.Location;
            var cell = location.Cell;
            var owner = rooms.FirstOrDefault(r => r.Cells.Contains(cell));
            if (owner != null)
            {
                lines.Add($"Cell 0x{cell:X8} is already part of room {owner.Number}. Nothing added.");
                return lines;
            }

            // The lowest number not in use, so a removed room 1 comes back as room 1 - or the number asked for.
            var number = wanted;
            if (number <= 0)
                for (number = 1; rooms.Any(r => r.Number == number); number++) { }
            else if (rooms.Any(r => r.Number == number))
            {
                lines.Add($"Source {sourceWcid} (v:{variation}) already has a room {number}. Nothing added.");
                return lines;
            }

            var entry = BuilderRoomEntry(number, cell, location.PositionX, location.PositionY, location.PositionZ,
                location.RotationW, location.RotationX, location.RotationY, location.RotationZ, new[] { cell });

            var saved = BuilderSaveRoomList(player, sourceWcid, $"add room {number}", raw =>
            {
                var entries = raw.Split(';').Select(e => e.Trim()).Where(e => e.Length > 0).ToList();
                if (entries.Any(e => BuilderEntryNumber(e) == number))
                {
                    lines.Add($"Room {number} was just added by someone else. Nothing added.");
                    return null;
                }

                // Kept in room-number order, so the stored list reads the way the rooms are numbered.
                entries.Add(entry);
                return string.Join(";", entries.OrderBy(e => BuilderEntryNumber(e) ?? int.MaxValue));
            }, lines, out _);

            if (!saved)
                return lines;

            var reparsed = GetRooms(sourceWcid);

            lines.Add($"Room {number} added to source {sourceWcid} (v:{variation}): {entry}");

            var added = reparsed?.FirstOrDefault(r => r.Number == number);
            if (added == null)
                lines.Add("WARNING: written to the database, but the re-read list does not show it yet.");
            else if (Landing(added, variation, sourceWcid) == null)
                lines.Add("WARNING: the landing is adjusted into a cell outside the room - it will not be handed out. Stand nearer the middle and edit the list.");

            return lines;
        }

        /// <summary>
        /// Removes a room from the source's REAL list in the world database - the counterpart of BuilderAddRoom. The other rooms
        /// keep their numbers. Refused for the last room, and while another counted player stands in it. The list as it was is
        /// logged first, so a removal can be put back by hand.
        /// </summary>
        public static List<string> BuilderRemoveRoom(Player player, int number)
        {
            var lines = new List<string>();

            if (!BuilderResolve(player, out var sourceWcid, out var rooms, out var variation, out var error, forWrite: true))
            {
                lines.Add(error);
                return lines;
            }

            if (!BuilderMayWrite(variation, lines))
                return lines;

            var room = rooms.FirstOrDefault(r => r.Number == number);
            if (room == null)
            {
                lines.Add($"Source {sourceWcid} has no room {number}.");
                return lines;
            }

            if (rooms.Count == 1)
            {
                lines.Add("That is the last room - a room source must keep at least one. Nothing removed.");
                return lines;
            }

            // One room list serves every variation the source is placed in, so the room goes from all of them: every copy is
            // checked and cleaned, not only the one picked (review 2026-09-24).
            List<int?> variations;
            lock (_lock)
                variations = SourceVariations(sourceWcid);
            if (!variations.Any(v => VariationManager.NormalizeBase(v) == VariationManager.NormalizeBase(variation)))
                variations.Add(variation);
            var keys = variations.Select(v => room.Key(v)).ToList();

            // Anyone else standing in it blocks the removal. Not the caller: a lone tester is nearly always in the room they
            // want gone, and is simply outside every room afterwards, until they portal or relog.
            foreach (var online in PlayerManager.GetAllOnline())
                if (online.Guid != player.Guid && !IsStaff(online) && variations.Any(v => InRoom(online.Location, room, v)))
                {
                    lines.Add($"{online.Name} is standing in room {number}. Nothing removed.");
                    return lines;
                }

            // A hold or a reservation on it blocks the removal too (review 2026-09-24): its owner is at their lifestone, or on
            // the way in. Removing it anyway would leave the claim behind for a new room in the same spot to inherit, and send
            // a player mid-portal into a cell that is no longer a room.
            var now = DateTime.UtcNow;
            lock (_lock)
            {
                PurgeExpired(now);

                foreach (var key in keys)
                {
                    if (_reservations.TryGetValue(key, out var reservation))
                    {
                        lines.Add($"Room {number} is reserved for {reservation.Name ?? "a player"} on their way in. Nothing removed - try again in a few seconds.");
                        return lines;
                    }

                    if (_holds.TryGetValue(key, out var hold) && IsHoldActive(hold, now))
                    {
                        lines.Add($"Room {number} is held for {hold.Name ?? "a player"} for {Math.Ceiling((hold.Until - now).TotalSeconds):0} more second(s). Nothing removed.");
                        return lines;
                    }
                }
            }

            var saved = BuilderSaveRoomList(player, sourceWcid, $"remove room {number}", raw =>
            {
                // Drop the one entry whose first field is this number; every other entry is kept exactly as authored.
                var entries = raw.Split(';').Select(e => e.Trim()).Where(e => e.Length > 0).ToList();
                var kept = entries.Where(e => BuilderEntryNumber(e) != number).ToList();

                if (entries.Count - kept.Count != 1)
                {
                    lines.Add($"Not removed - room {number} is not in the stored list exactly once.");
                    return null;
                }

                if (kept.Count == 0)
                {
                    lines.Add("That is the last room - a room source must keep at least one. Nothing removed.");
                    return null;
                }

                return string.Join(";", kept);
            }, lines, out _);

            if (!saved)
                return lines;

            // The room is gone: whatever still names it goes with it, in every copy, so a new room in the same spot starts clean.
            var markers = new List<WorldObject>();
            lock (_lock)
            {
                foreach (var key in keys)
                {
                    // A hand-out that slipped in between the check above and the write: its reservation goes too (the player
                    // lands in a cell that is no longer a room and is outside every room, as any login there would be).
                    RemoveReservation(key);

                    _testFakes.Remove(key);
                    _holds.Remove(key);
                    _roomOwner.Remove(key);
                    _roomOwnerSince.Remove(key);

                    if (_testMarkers.Remove(key, out var marker))
                        markers.Add(marker);

                    foreach (var guid in _leftRoom.Where(kv => kv.Value == key).Select(kv => kv.Key).ToList())
                        _leftRoom.Remove(guid);
                    foreach (var guid in _announcedRoom.Where(kv => kv.Value == key).Select(kv => kv.Key).ToList())
                        _announcedRoom.Remove(guid);
                }
            }

            // Its landing marker (a test tool) would otherwise stand on a spot that is no longer a room. Outside _lock.
            foreach (var marker in markers)
                marker.Destroy();

            var reparsed = GetRooms(sourceWcid);

            lines.Add($"Room {number} removed from source {sourceWcid} (v:{variation}). {reparsed?.Count ?? 0} room(s) left. The old list is in the server log.");
            return lines;
        }

        /// <summary>A placed generator of a room: its landblock_instance guid and where it stands.</summary>
        private sealed class BuilderGenerator
        {
            public uint Guid;
            public float X, Y, Z;
        }

        /// <summary>
        /// The placed generator nearest to the player inside these cells, in this variation: a landblock_instance whose
        /// weenie has generator profiles. Null when the room has none.
        /// </summary>
        private static BuilderGenerator BuilderFindGenerator(HashSet<uint> cells, int? variation, ACE.Entity.Position from)
        {
            List<LandblockInstance> placed;
            var here = VariationManager.NormalizeBase(variation);

            using (var context = new WorldDbContext())
                placed = context.LandblockInstance.Where(i => cells.Contains(i.ObjCellId)).ToList();

            BuilderGenerator best = null;
            var bestDistance = float.MaxValue;

            foreach (var instance in placed)
            {
                if (VariationManager.NormalizeBase(instance.VariationId) != here)
                    continue;

                var weenie = DatabaseManager.World.GetCachedWeenie(instance.WeenieClassId);
                if (weenie?.PropertiesGenerator == null || weenie.PropertiesGenerator.Count == 0)
                    continue;

                var dx = instance.OriginX - from.PositionX;
                var dy = instance.OriginY - from.PositionY;
                var distance = dx * dx + dy * dy;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = new BuilderGenerator { Guid = instance.Guid, X = instance.OriginX, Y = instance.OriginY, Z = instance.OriginZ };
                }
            }

            return best;
        }

        /// <summary>
        /// The room's generator; when its own cells hold none, the nearest one in a cell directly connected to them that
        /// belongs to no other room - that cell (with its dead-end cell) then JOINS the room. Owner 2026-09-21: the
        /// monster may stand in the cell next to the landing's.
        /// </summary>
        private static BuilderGenerator BuilderFindOrJoinGenerator(Dictionary<uint, BuilderCellInfo> dat, HashSet<uint> cells, List<Room> rooms, Room room,
            int? variation, ACE.Entity.Position from, List<string> lines)
        {
            var generator = BuilderFindGenerator(cells, variation, from);
            if (generator != null)
                return generator;

            var taken = new HashSet<uint>(rooms.Where(r => r != room).SelectMany(r => r.Cells));
            var next = new HashSet<uint>();

            foreach (var cell in cells)
                if (dat.TryGetValue(cell, out var info))
                    foreach (var opening in info.Openings)
                        if (!cells.Contains(opening.Other) && !taken.Contains(opening.Other) && dat.ContainsKey(opening.Other))
                            next.Add(opening.Other);

            if (next.Count == 0)
                return null;

            generator = BuilderFindGenerator(next, variation, from);
            if (generator == null)
                return null;

            // Which of the connected cells holds it.
            uint joined;
            using (var context = new WorldDbContext())
                joined = context.LandblockInstance.Where(i => i.Guid == generator.Guid).Select(i => i.ObjCellId).FirstOrDefault();

            if (!next.Contains(joined))
                return null;

            cells.Add(joined);
            foreach (var extra in BuilderDeadEndCells(dat, cells, rooms, room).ToList())
                cells.Add(extra);

            lines.Add($"Cell 0x{joined & 0xFFFF:X4} joined the room: it is connected to it and holds generator 0x{generator.Guid:X8}.");
            return generator;
        }

        /// <summary>The rotation that looks along (dx, dy) on the floor - the same formula as ACE.Entity.Position.Rotate.</summary>
        private static System.Numerics.Quaternion BuilderFacing(float dx, float dy)
            => System.Numerics.Quaternion.CreateFromYawPitchRoll(0, 0, (float)Math.Atan2(-dx, dy));

        /// <summary>
        /// Makes a live object's new Location real for everyone looking at it. Setting Location alone changed what the
        /// server spawns from, but the physics body and every client kept the old spot and facing - the owner saw a
        /// generator that "did not turn" (2026-09-20). Same two steps /nudge ends with: the physics body, then a position
        /// update to the clients.
        /// </summary>
        private static void BuilderShowLive(WorldObject live)
        {
            try
            {
                if (live.PhysicsObj != null)
                    live.PhysicsObj.Position = new ACE.Server.Physics.Common.Position(live.Location);

                live.SendUpdatePosition(true);
            }
            catch (Exception ex)
            {
                log.Warn($"[RoomAssign][DUNGEON] could not show 0x{live.Guid} at its new position: {ex.Message}");
            }
        }

        /// <summary>
        /// Turns a placed generator to face a spot: its landblock_instance row, the cached instances of the landblock, and
        /// the live generator object, so its next spawn already faces that way (a Scatter spawn copies the generator's
        /// rotation). The monster standing there now keeps its facing until it respawns.
        /// </summary>
        private static void BuilderTurnGenerator(Player player, BuilderGenerator generator, int? variation, System.Numerics.Quaternion facing, List<string> lines)
        {
            uint cell;
            using (var context = new WorldDbContext())
            {
                var row = context.LandblockInstance.FirstOrDefault(i => i.Guid == generator.Guid);
                if (row == null)
                    return;

                cell = row.ObjCellId;
                row.AnglesW = facing.W;
                row.AnglesX = facing.X;
                row.AnglesY = facing.Y;
                row.AnglesZ = facing.Z;
                row.LastModified = DateTime.UtcNow;
                context.SaveChanges();
            }

            // The GENERATOR's landblock, not the admin's: with a dungeon picked in the dropdown they may be anywhere.
            DatabaseManager.World.ClearCachedInstancesByLandblock((ushort)(cell >> 16), variation);

            var live = BuilderLiveObject(generator.Guid, cell, variation);
            if (live != null)
                LandblockManager.RunOnThreadFor(live, ACE.Server.Entity.Actions.ActionType.DungeonBuilder_LiveEdit, () =>
                {
                    if (live.Location == null)
                        return;
                    live.Location.Rotation = facing;
                    BuilderShowLive(live);
                });

            log.Info($"[RoomAssign][DUNGEON] {player.Name} turned generator 0x{generator.Guid:X8} to face the landing. SQL: UPDATE landblock_instance SET angles_W = {BuilderF(facing.W)}, angles_X = {BuilderF(facing.X)}, angles_Y = {BuilderF(facing.Y)}, angles_Z = {BuilderF(facing.Z)} WHERE guid = 0x{generator.Guid:X8};");
            lines.Add($"Generator 0x{generator.Guid:X8} now faces the landing{(live != null ? "" : " (not loaded right now - applies when the landblock loads)")}. The monster standing there turns when it next respawns.");
        }

        /// <summary>
        /// "Player Here" / Set Landing Here: the landing goes EXACTLY where the admin stands - the counterpart of the Player
        /// pin, which snaps to a padded corner. The room the admin stands in keeps its number; a cell that is no room yet
        /// becomes one. The player will face the room's generator (with none yet: the middle of the cell, where the Monster
        /// pin puts it), the generator is turned to face the landing, the room's dead-end cell above or below joins it, and
        /// a WCID 1 marker is put on the spot.
        /// </summary>
        public static List<string> BuilderSetLanding(Player player)
        {
            var lines = new List<string>();

            if (!BuilderResolve(player, out var sourceWcid, out var rooms, out var variation, out var error, forWrite: true))
            {
                lines.Add(error);
                return lines;
            }

            if (!BuilderMayWrite(variation, lines))
                return lines;

            if (!BuilderMustStandIn(player, rooms, variation, lines))
                return lines;

            // BuilderMustStandIn has checked the landblock and the variation.
            var location = player.Location;
            var landblock = rooms[0].LandingCell >> 16;
            var cell = location.Cell;
            var dat = BuilderDatCells(landblock);

            if (cell >> 16 != landblock || !dat.TryGetValue(cell, out var here))
            {
                lines.Add($"Cell 0x{cell:X8} is not an indoor cell of this dungeon (0x{landblock:X4}). Nothing changed.");
                return lines;
            }

            var room = CurrentRoom(player, rooms, variation);

            var cells = new HashSet<uint>(room != null ? room.Cells : new HashSet<uint>());
            cells.Add(cell);
            foreach (var extra in BuilderDeadEndCells(dat, cells, rooms, room).ToList())
                cells.Add(extra);

            var generator = BuilderFindOrJoinGenerator(dat, cells, rooms, room, variation, location, lines);
            var targetX = generator != null ? generator.X : here.Centre.X;
            var targetY = generator != null ? generator.Y : here.Centre.Y;
            var dx = targetX - location.PositionX;
            var dy = targetY - location.PositionY;

            // Standing on the very spot the landing would face: no direction can be worked out from there. It used to be
            // refused; the owner (2026-09-20) stands wherever he likes - the monster is ethereal - so the landing keeps the
            // way he is FACING instead, and the generator is left as it is. A later Monster pin or Here re-faces both.
            var onTheSpot = dx * dx + dy * dy < 0.25f;
            var rotation = onTheSpot
                ? new System.Numerics.Quaternion(location.RotationX, location.RotationY, location.RotationZ, location.RotationW)
                : BuilderFacing(dx, dy);

            var number = room?.Number ?? 0;
            if (number == 0)
                for (number = 1; rooms.Any(r => r.Number == number); number++) { }

            if (!BuilderSaveRoom(player, sourceWcid, number, cell, location.PositionX, location.PositionY, location.PositionZ, rotation, cells, lines, isNew: room == null))
                return lines;

            lines.Add($"Room {number} (v:{variation}): {(room != null ? "landing moved" : "new room, landing set")} to exactly where you stand, facing {(onTheSpot ? "the way you face now (you stand on the spot it would look at)" : generator != null ? "the generator" : "the middle of the cell")}. {cells.Count} cell(s).");

            if (generator != null && !onTheSpot)
                BuilderTurnGenerator(player, generator, variation, BuilderFacing(-dx, -dy), lines);

            var saved = GetRooms(sourceWcid)?.FirstOrDefault(r => r.Number == number);
            if (saved != null && Landing(saved, variation, sourceWcid) == null)
                lines.Add("WARNING: this landing is adjusted into a cell outside the room - it will not be handed out. Stand nearer the middle and set it again.");
            else if (saved != null)
                TestMarkLanding(saved, variation, sourceWcid, lines);   // no-op while the test tools are off

            return lines;
        }

        // ---------------------------------------------------------------------------------------------------------
        // The plugin's map: data the client cannot see for itself
        // ---------------------------------------------------------------------------------------------------------

        // Every map line stays well under 600 characters: a longer chat line freezes the client before any plugin hook
        // runs (measured 2026-09-03, see ZoneControlCommands.SendChunked). Records are packed, never split.
        private const int BuilderMapLineLength = 540;

        /// <summary>The floor direction a rotation looks along - the inverse of BuilderFacing / ACE.Entity.Position.Rotate.</summary>
        private static void BuilderDirection(float qw, float qz, out float dx, out float dy)
        {
            var angle = 2.0 * Math.Atan2(qz, qw);
            dx = (float)-Math.Sin(angle);
            dy = (float)Math.Cos(angle);
        }

        /// <summary>
        /// Everything the plugin's dungeon map draws on top of the DAT floor plan, as [[ZCDGM]] lines it reads and hides:
        ///   [[ZCDGM]]begin=src,variation,landblock
        ///   [[ZCDGM]]rooms=number,landingCell,x,y,z,dx,dy,cell+cell+...;...
        ///   [[ZCDGM]]objs=kind,guid,cell,x,y,z,dx,dy,name,wcid;...      kind: g generator, d door, p portal, o other
        ///   [[ZCDGM]]end=1
        /// Cells are 4 hex digits (the landblock is in begin), positions are landblock-local like the room list, and dx,dy is
        /// the unit direction the landing or object faces. Records are packed several to a line, each line its own short
        /// chat message - so nothing here needs SendChunked. Who is in a room is not here: that is the [[ZCDG]] state
        /// line, matched by room number.
        /// </summary>
        public static List<string> BuilderMap(Player player)
        {
            var lines = new List<string>();

            if (!BuilderResolve(player, out var sourceWcid, out var rooms, out var variation, out _))
            {
                lines.Add(BuilderMapTag + "begin=0,,0000");
                lines.Add(BuilderMapTag + "end=1");
                return lines;
            }

            string F(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

            var landblock = rooms[0].LandingCell >> 16;
            lines.Add($"{BuilderMapTag}begin={sourceWcid},{variation},{landblock:X4}");

            void Pack(string kind, IEnumerable<string> records)
            {
                var line = "";
                foreach (var record in records)
                {
                    if (line.Length > 0 && line.Length + record.Length + 1 > BuilderMapLineLength)
                    {
                        lines.Add($"{BuilderMapTag}{kind}={line}");
                        line = "";
                    }
                    line += (line.Length > 0 ? ";" : "") + record;
                }
                if (line.Length > 0)
                    lines.Add($"{BuilderMapTag}{kind}={line}");
            }

            Pack("rooms", rooms.Select(r =>
            {
                BuilderDirection(r.QW, r.QZ, out var dx, out var dy);
                return $"{r.Number},{r.LandingCell & 0xFFFF:X4},{F(r.X)},{F(r.Y)},{F(r.Z)},{F(dx)},{F(dy)},{string.Join("+", r.Cells.OrderBy(c => c).Select(c => $"{c & 0xFFFF:X4}"))}";
            }));

            List<LandblockInstance> placed;
            var low = landblock << 16;
            var high = low | 0xFFFF;
            using (var context = new WorldDbContext())
                placed = context.LandblockInstance.Where(i => i.ObjCellId >= low && i.ObjCellId <= high).ToList();

            var here = VariationManager.NormalizeBase(variation);

            Pack("objs", placed.Where(i => VariationManager.NormalizeBase(i.VariationId) == here).Select(i =>
            {
                var weenie = DatabaseManager.World.GetCachedWeenie(i.WeenieClassId);
                var kind = weenie?.PropertiesGenerator != null && weenie.PropertiesGenerator.Count > 0 ? "g"
                    : weenie?.WeenieType == WeenieType.Door ? "d"
                    : weenie?.WeenieType == WeenieType.Portal ? "p" : "o";

                string name = null;
                weenie?.PropertiesString?.TryGetValue(PropertyString.Name, out name);
                name = BuilderWireName(name ?? i.WeenieClassId.ToString(CultureInfo.InvariantCulture));

                BuilderDirection(i.AnglesW, i.AnglesZ, out var dx, out var dy);
                // wcid appended 2026-09-21: the Nudge window's Remove sends /removeinst <wcid> (nearest instance of that weenie).
                return $"{kind},{i.Guid:X8},{i.ObjCellId & 0xFFFF:X4},{F(i.OriginX)},{F(i.OriginY)},{F(i.OriginZ)},{F(dx)},{F(dy)},{name},{i.WeenieClassId}";
            }));

            lines.Add(BuilderMapTag + "end=1");
            return lines;
        }

        /// <summary>Teleports the caller to a room's landing - a plain admin teleport, no claim is made or checked.</summary>
        public static List<string> BuilderGoto(Player player, int number)
        {
            var lines = new List<string>();

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

            var landing = Landing(room, variation, sourceWcid);
            if (landing == null)
            {
                lines.Add($"Room {number}'s landing is adjusted into a cell outside the room - fix the room first.");
                return lines;
            }

            WorldManager.ThreadSafeTeleport(player, landing);
            lines.Add($"Going to room {number} (v:{variation}). This is a plain teleport: no room is claimed for you.");
            return lines;
        }

        /// <summary>
        /// Adds a cell to a room, or takes it out if it is already in it - how a room of several cells is built from the
        /// map. The landing's own cell cannot be taken out. The list must still parse (a cell in two rooms is refused).
        /// </summary>
        public static List<string> BuilderToggleCell(Player player, int number, string cellText)
        {
            var lines = new List<string>();

            if (!BuilderResolve(player, out var sourceWcid, out var rooms, out var variation, out var error, forWrite: true))
            {
                lines.Add(error);
                return lines;
            }

            if (!BuilderMayWrite(variation, lines))
                return lines;

            var room = rooms.FirstOrDefault(r => r.Number == number);
            if (room == null)
            {
                lines.Add($"Source {sourceWcid} has no room {number}.");
                return lines;
            }

            cellText = (cellText ?? "").Trim();
            if (cellText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                cellText = cellText.Substring(2);

            if (!uint.TryParse(cellText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var cell))
            {
                lines.Add($"\"{cellText}\" is not a cell id.");
                return lines;
            }

            var landblock = room.LandingCell >> 16;
            if (cell <= 0xFFFF)
                cell |= landblock << 16;

            // The cell must really exist (review 2026-09-24): the parser only checks "indoor, same landblock", so a typo
            // like FFFF would otherwise be stored as part of the room.
            if (cell >> 16 != landblock || !BuilderDatCells(landblock).ContainsKey(cell))
            {
                lines.Add($"Cell 0x{cell:X8} is not an indoor cell of this dungeon (0x{landblock:X4}). Nothing changed.");
                return lines;
            }

            // Taking out a cell someone else stands in would make them stop counting, and the room could look free to the next
            // arrival (review 2026-09-24). Refused, like removing a room someone stands in.
            if (room.Cells.Contains(cell))
            {
                List<int?> variations;
                lock (_lock)
                    variations = SourceVariations(sourceWcid);

                foreach (var online in PlayerManager.GetAllOnline())
                {
                    var at = online.Location;
                    if (online.Guid != player.Guid && !IsStaff(online) && at != null && at.Cell == cell
                        && (variations.Count == 0 || variations.Any(v => VariationManager.NormalizeBase(v) == VariationManager.NormalizeBase(at.Variation))))
                    {
                        lines.Add($"{online.Name} is standing in cell 0x{cell:X8}. Nothing changed.");
                        return lines;
                    }
                }
            }

            // The new entry is built from the DATABASE row, inside the save's lock (review 2026-09-24) - never from the cached
            // room, which a hand edit not yet reloaded would make older than what it overwrites.
            var added = false;
            var cellCount = 0;
            var saved = BuilderSaveRoomList(player, sourceWcid, $"toggle cell 0x{cell:X8} in room {number}", raw =>
            {
                if (!TryParseRooms(raw, out var stored, out var storedError))
                {
                    lines.Add($"Nothing changed - the stored list does not parse: {storedError}.");
                    return null;
                }

                var current = stored.FirstOrDefault(r => r.Number == number);
                if (current == null)
                {
                    lines.Add($"Nothing changed - room {number} is no longer in the stored list.");
                    return null;
                }

                var cells = new HashSet<uint>(current.Cells);
                if (cells.Contains(cell))
                {
                    if (cell == current.LandingCell)
                    {
                        lines.Add($"Cell 0x{cell:X8} holds room {number}'s landing - move the landing first.");
                        return null;
                    }

                    cells.Remove(cell);
                    added = false;
                }
                else
                {
                    cells.Add(cell);
                    added = true;
                }

                cellCount = cells.Count;
                var entry = BuilderRoomEntry(current.Number, current.LandingCell, current.X, current.Y, current.Z, current.QW, current.QX, current.QY, current.QZ, cells);

                var entries = raw.Split(';').Select(e => e.Trim()).Where(e => e.Length > 0).ToList();
                var index = entries.FindIndex(e => BuilderEntryNumber(e) == number);
                if (index < 0 || entries.FindLastIndex(e => BuilderEntryNumber(e) == number) != index)
                {
                    lines.Add($"Nothing changed - room {number} is not in the stored list exactly once.");
                    return null;
                }

                entries[index] = entry;
                return string.Join(";", entries);
            }, lines, out _);

            if (!saved)
                return lines;

            GetRooms(sourceWcid);

            lines.Add($"Room {number} (v:{variation}): cell 0x{cell:X8} {(added ? "added" : "removed")}. It now has {cellCount} cell(s).");
            return lines;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Place by pin (owner 2026-09-20): the map drops a pin, the server works out where the thing really goes
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>How far in from the cell's edge a landing goes, tried in this order until the spot is inside the cell.</summary>
        private static readonly float[] BuilderCornerOffsets = { 3.0f, 2.5f, 2.0f, 1.5f };

        private const uint BuilderDefaultWallWcid = 777704001;   // grumpy_barrier_magicwall

        /// <summary>One way out of a cell, from the DAT: the polygon the cell's portal stands in.</summary>
        private sealed class BuilderOpening
        {
            public uint Cell, Other;
            public System.Numerics.Vector3 Centre;
            public System.Numerics.Vector3 Normal;   // horizontal unit vector; zero for an opening in a floor or ceiling
            public float FloorZ;
            public bool Vertical;
        }

        private sealed class BuilderCellInfo
        {
            public System.Numerics.Vector3 Centre;
            public List<BuilderOpening> Openings = new List<BuilderOpening>();
        }

        /// <summary>Landblock (high word) -> every indoor cell, read from the DAT once. Behind its own lock.</summary>
        private static readonly Dictionary<uint, Dictionary<uint, BuilderCellInfo>> _builderDatCache = new Dictionary<uint, Dictionary<uint, BuilderCellInfo>>();

        /// <summary>
        /// Every indoor cell of a landblock with its centre and openings, exactly as measured offline against the 42
        /// hand-placed walls of 0x01F7 (2026-09-20): 39 within 1.0 of an opening's centre, 41 facing along it.
        /// </summary>
        private static Dictionary<uint, BuilderCellInfo> BuilderDatCells(uint landblock)
        {
            lock (_builderDatCache)
            {
                if (_builderDatCache.TryGetValue(landblock, out var cached))
                    return cached;

                var cells = new Dictionary<uint, BuilderCellInfo>();
                var info = ACE.DatLoader.DatManager.CellDat.ReadFromDat<ACE.DatLoader.FileTypes.LandblockInfo>((landblock << 16) | 0xFFFE);

                for (uint i = 0; i < info.NumCells; i++)
                {
                    var id = (landblock << 16) | (0x100 + i);
                    var cell = ACE.DatLoader.DatManager.CellDat.ReadFromDat<ACE.DatLoader.FileTypes.EnvCell>(id);
                    var environment = ACE.DatLoader.DatManager.PortalDat.ReadFromDat<ACE.DatLoader.FileTypes.Environment>(cell.EnvironmentId);

                    var entry = new BuilderCellInfo { Centre = cell.Position.Origin };
                    cells[id] = entry;

                    if (!environment.Cells.TryGetValue(cell.CellStructure, out var structure))
                        continue;

                    foreach (var portal in cell.CellPortals)
                    {
                        if (!structure.Polygons.TryGetValue(portal.PolygonId, out var polygon) || polygon.VertexIds.Count < 3)
                            continue;

                        var points = polygon.VertexIds
                            .Select(v => cell.Position.Origin + System.Numerics.Vector3.Transform(structure.VertexArray.Vertices[(ushort)v].Origin, cell.Position.Orientation))
                            .ToList();

                        var centre = points.Aggregate(System.Numerics.Vector3.Zero, (a, b) => a + b) / points.Count;
                        var normal = System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(points[1] - points[0], points[2] - points[0]));
                        var flat = new System.Numerics.Vector3(normal.X, normal.Y, 0);

                        entry.Openings.Add(new BuilderOpening
                        {
                            Cell = id,
                            Other = (landblock << 16) | portal.OtherCellId,
                            Centre = centre,
                            Vertical = Math.Abs(normal.Z) > 0.7f,
                            Normal = flat.Length() > 0.01f ? System.Numerics.Vector3.Normalize(flat) : System.Numerics.Vector3.Zero,
                            FloorZ = points.Min(q => q.Z),
                        });
                    }
                }

                _builderDatCache[landblock] = cells;
                return cells;
            }
        }

        /// <summary>
        /// The cells above or below these that lead nowhere else: reached through an opening in a floor or ceiling, with
        /// every opening of their own coming straight back. 23 of the 25 rooms of 0x01F7 have one; it belongs to its room.
        /// A cell that ANOTHER room already holds is never taken (2026-09-21: a new room next to room 16 picked up 16's
        /// cell 01F8 this way, and the save was refused for the overlap).
        /// </summary>
        private static List<uint> BuilderDeadEndCells(Dictionary<uint, BuilderCellInfo> dat, HashSet<uint> cells, List<Room> rooms, Room mine)
        {
            var taken = new HashSet<uint>(rooms.Where(r => r != mine).SelectMany(r => r.Cells));
            var found = new List<uint>();

            foreach (var cell in cells)
                if (dat.TryGetValue(cell, out var info))
                    foreach (var opening in info.Openings.Where(o => o.Vertical))
                        if (!cells.Contains(opening.Other) && !found.Contains(opening.Other) && !taken.Contains(opening.Other)
                            && dat.TryGetValue(opening.Other, out var other) && other.Openings.All(o => cells.Contains(o.Other)))
                            found.Add(opening.Other);

            return found;
        }

        /// <summary>True when the spot is inside the cell as the physics engine sees it (tested at body height).</summary>
        private static bool BuilderInsideCell(uint cell, int? variation, float x, float y, float floorZ)
        {
            try
            {
                var physicsCell = ACE.Server.Physics.Common.LScape.get_landcell(cell, variation);
                return physicsCell != null && physicsCell.point_in_cell(new System.Numerics.Vector3(x, y, floorZ + 1.0f));
            }
            catch (Exception ex)
            {
                log.Warn($"[RoomAssign][DUNGEON] point_in_cell 0x{cell:X8}: {ex.Message}");
                return false;
            }
        }

        /// <summary>The wcid placed most often in this dungeon among weenies passing the test, or 0.</summary>
        private static uint BuilderMostPlaced(uint landblock, int? variation, Func<ACE.Entity.Models.Weenie, bool> test)
        {
            var low = landblock << 16;
            var high = low | 0xFFFF;
            var here = VariationManager.NormalizeBase(variation);

            List<LandblockInstance> placed;
            using (var context = new WorldDbContext())
                placed = context.LandblockInstance.Where(i => i.ObjCellId >= low && i.ObjCellId <= high).ToList();

            return placed
                .Where(i => VariationManager.NormalizeBase(i.VariationId) == here)
                .GroupBy(i => i.WeenieClassId)
                .Where(g => { var w = DatabaseManager.World.GetCachedWeenie(g.Key); return w != null && test(w); })
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();
        }

        /// <summary>
        /// Places a weenie as a saved landblock instance: the core of /createinst (next static guid, spawn, row), at a
        /// position of our choosing instead of the admin's feet. Returns the new guid, 0 on failure (said in lines).
        /// </summary>
        private static uint BuilderPlaceInstance(Player player, uint wcid, ACE.Entity.Position at, List<string> lines)
        {
            var landblock = (ushort)(at.Cell >> 16);
            var variation = at.Variation;

            var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
            if (weenie == null)
            {
                lines.Add($"Weenie {wcid} does not exist.");
                return 0;
            }

            DatabaseManager.World.ClearCachedInstancesByLandblock(landblock, variation);
            var instances = DatabaseManager.World.GetLandblockInstancesByLandblockBypassCache(landblock);
            var guid = ACE.Server.Command.Handlers.Processors.DeveloperContentCommands.GetNextStaticGuid(landblock, instances);

            if (guid > ((ACE.Entity.ObjectGuid.LandblockInstanceGuidBase | ((uint)landblock << 12)) | 0xFFF))
            {
                lines.Add($"Landblock {landblock:X4} has no static guid left.");
                return 0;
            }

            var wo = ACE.Server.Factories.WorldObjectFactory.CreateWorldObject(weenie, new ACE.Entity.ObjectGuid(guid));
            if (wo == null)
            {
                lines.Add($"Could not create an object for weenie {wcid}.");
                return 0;
            }

            if (!wo.Stuck)
            {
                lines.Add($"Weenie {wcid} is missing PropertyBool.Stuck - it cannot be a landblock instance.");
                wo.Destroy();
                return 0;
            }

            if (wo.WeenieType != WeenieType.Creature)
                wo.CreatedByAccountId = player.Account.AccountId;

            wo.Location = new ACE.Entity.Position(at);

            if (!wo.EnterWorld())
            {
                lines.Add($"{wo.Name} could not be spawned at that spot.");
                return 0;
            }

            var instance = ACE.Server.Command.Handlers.Processors.DeveloperContentCommands.CreateLandblockInstance(wo, false, variation);
            if (!ACE.Server.Command.Handlers.Processors.DeveloperContentCommands.SaveInstanceToWorldDatabase(instance))
            {
                wo.Destroy();
                lines.Add("The landblock instance could not be saved to the database - nothing was placed.");
                return 0;
            }

            DatabaseManager.World.ClearCachedInstancesByLandblock(landblock, variation);

            // The row as SQL, like every other builder write, so a placement can be replayed to live from the log.
            log.Info($"[RoomAssign][DUNGEON] {player.Name} placed {wo.Name} ({wcid}) as 0x{guid:X8} at 0x{at.Cell:X8} v:{variation}. SQL: "
                + "INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, angles_W, angles_X, angles_Y, angles_Z, is_Link_Child, variation_Id) "
                + $"VALUES (0x{instance.Guid:X8}, {instance.WeenieClassId}, 0x{instance.ObjCellId:X8}, {BuilderF(instance.OriginX)}, {BuilderF(instance.OriginY)}, {BuilderF(instance.OriginZ)}, "
                + $"{BuilderF(instance.AnglesW)}, {BuilderF(instance.AnglesX)}, {BuilderF(instance.AnglesY)}, {BuilderF(instance.AnglesZ)}, {(instance.IsLinkChild ? 1 : 0)}, {(instance.VariationId.HasValue ? instance.VariationId.Value.ToString(CultureInfo.InvariantCulture) : "NULL")});");
            return guid;
        }

        /// <summary>
        /// Moves a placed SOLID object (a wall): its landblock_instance row gets the exact spot and facing; the live object
        /// is walked there by the physics engine as an admin move - the way /nudge does it, so it changes cell properly -
        /// then turned, and every client is told. When the physics move is refused the body is set directly. The guid is
        /// kept. False (said in lines) when the row is gone.
        /// </summary>
        private static bool BuilderMoveInstance(Player player, uint guid, ACE.Entity.Position to, List<string> lines)
        {
            using (var context = new WorldDbContext())
            {
                var row = context.LandblockInstance.FirstOrDefault(i => i.Guid == guid);
                if (row == null)
                {
                    lines.Add($"0x{guid:X8} has no landblock_instance row any more. Nothing moved.");
                    return false;
                }

                row.ObjCellId = to.Cell;
                row.OriginX = to.PositionX;
                row.OriginY = to.PositionY;
                row.OriginZ = to.PositionZ;
                row.AnglesW = to.RotationW;
                row.AnglesX = to.RotationX;
                row.AnglesY = to.RotationY;
                row.AnglesZ = to.RotationZ;
                row.LastModified = DateTime.UtcNow;
                context.SaveChanges();
            }

            DatabaseManager.World.ClearCachedInstancesByLandblock((ushort)(to.Cell >> 16), to.Variation);

            log.Info($"[RoomAssign][DUNGEON] {player.Name} moved 0x{guid:X8} to 0x{to.Cell:X8} [{BuilderF(to.PositionX)} {BuilderF(to.PositionY)} {BuilderF(to.PositionZ)}]. SQL: UPDATE landblock_instance SET obj_Cell_Id = 0x{to.Cell:X8}, origin_X = {BuilderF(to.PositionX)}, origin_Y = {BuilderF(to.PositionY)}, origin_Z = {BuilderF(to.PositionZ)}, angles_W = {BuilderF(to.RotationW)}, angles_X = {BuilderF(to.RotationX)}, angles_Y = {BuilderF(to.RotationY)}, angles_Z = {BuilderF(to.RotationZ)} WHERE guid = 0x{guid:X8};");

            // The loaded landblock the object is in, whichever it is - not the admin's own (they may have the dungeon picked
            // from elsewhere). Its physics are touched on that landblock's own thread.
            var live = BuilderLiveObject(guid, to.Cell, to.Variation);
            if (live == null)
            {
                lines.Add("It is not loaded right now - the move applies when the landblock loads.");
                return true;
            }

            LandblockManager.RunOnThreadFor(live, ACE.Server.Entity.Actions.ActionType.DungeonBuilder_LiveEdit, () =>
            {
                try
                {
                    var walked = false;

                    if (live.PhysicsObj != null)
                    {
                        var target = new ACE.Server.Physics.Common.Position(to);
                        var transit = live.PhysicsObj.transition(live.PhysicsObj.Position, target, true);

                        if (transit != null && (transit.SpherePath.CurPos.ObjCellID >> 16) == (to.Cell >> 16))
                        {
                            live.PhysicsObj.SetPositionInternal(transit);
                            live.PhysicsObj.Position.Frame.Orientation = to.Rotation;
                            walked = true;
                        }
                        else
                            live.PhysicsObj.Position = target;
                    }

                    live.Location = walked ? ACE.Server.Entity.PositionExtensions.ACEPosition(live.PhysicsObj.Position) : new ACE.Entity.Position(to);
                    live.Location.Rotation = to.Rotation;
                    if (live.Location.Variation == null)
                        live.Location.Variation = to.Variation;

                    live.SendUpdatePosition(true);
                }
                catch (Exception ex)
                {
                    // The row is already moved: the object is right after the landblock reloads.
                    log.Warn($"[RoomAssign][DUNGEON] moving live 0x{guid:X8}: {ex.Message}");
                }
            });

            return true;
        }

        /// <summary>The live object of a placed instance, in the LOADED landblock that holds this cell and variation - or null.</summary>
        private static WorldObject BuilderLiveObject(uint guid, uint cell, int? variation)
            => LandblockManager.GetLoadedLandblock(new ACE.Entity.LandblockId(cell), variation)?.GetObject(guid);

        /// <summary>
        /// Moves a placed generator: its landblock_instance row (cell, position, facing), the cached instances, and the live
        /// generator object - which is then reset, so what it had spawned is removed and it spawns again at the new spot,
        /// facing the new way. The guid is kept. False (said in lines) when the row is gone.
        /// </summary>
        private static bool BuilderMoveGenerator(Player player, BuilderGenerator generator, ACE.Entity.Position to, List<string> lines)
        {
            using (var context = new WorldDbContext())
            {
                var row = context.LandblockInstance.FirstOrDefault(i => i.Guid == generator.Guid);
                if (row == null)
                {
                    lines.Add($"Generator 0x{generator.Guid:X8} has no landblock_instance row any more. Nothing moved.");
                    return false;
                }

                row.ObjCellId = to.Cell;
                row.OriginX = to.PositionX;
                row.OriginY = to.PositionY;
                row.OriginZ = to.PositionZ;
                row.AnglesW = to.RotationW;
                row.AnglesX = to.RotationX;
                row.AnglesY = to.RotationY;
                row.AnglesZ = to.RotationZ;
                row.LastModified = DateTime.UtcNow;
                context.SaveChanges();
            }

            DatabaseManager.World.ClearCachedInstancesByLandblock((ushort)(to.Cell >> 16), to.Variation);

            // The generator's own loaded landblock, on its own thread - not the admin's (see BuilderMoveInstance).
            var live = BuilderLiveObject(generator.Guid, to.Cell, to.Variation);
            if (live != null)
                LandblockManager.RunOnThreadFor(live, ACE.Server.Entity.Actions.ActionType.DungeonBuilder_LiveEdit, () =>
                {
                    live.Location = new ACE.Entity.Position(to);
                    BuilderShowLive(live);
                    live.ResetGenerator();
                });

            log.Info($"[RoomAssign][DUNGEON] {player.Name} moved generator 0x{generator.Guid:X8} to 0x{to.Cell:X8} [{BuilderF(to.PositionX)} {BuilderF(to.PositionY)} {BuilderF(to.PositionZ)}]. SQL: UPDATE landblock_instance SET obj_Cell_Id = 0x{to.Cell:X8}, origin_X = {BuilderF(to.PositionX)}, origin_Y = {BuilderF(to.PositionY)}, origin_Z = {BuilderF(to.PositionZ)}, angles_W = {BuilderF(to.RotationW)}, angles_X = {BuilderF(to.RotationX)}, angles_Y = {BuilderF(to.RotationY)}, angles_Z = {BuilderF(to.RotationZ)} WHERE guid = 0x{generator.Guid:X8};");

            if (live == null)
                lines.Add("The generator is not loaded right now - the move applies when the landblock loads.");
            else
                lines.Add("Its monster was removed and respawns at the new spot.");

            return true;
        }

        /// <summary>
        /// Writes one room into the source's stored list - replacing the entry with its number, or adding it - after the
        /// whole list has parsed. False (said in lines) when nothing was written. <paramref name="isNew"/>: the caller picked
        /// this number as a free one from the cached list - if the stored list already has it (added by hand, not reloaded
        /// yet), nothing is written rather than overwriting that room (review 2026-09-24).
        /// </summary>
        private static bool BuilderSaveRoom(Player player, uint sourceWcid, int number, uint landingCell, float x, float y, float z,
            System.Numerics.Quaternion rotation, IEnumerable<uint> cells, List<string> lines, bool isNew = false)
        {
            var saved = BuilderSaveRoomList(player, sourceWcid, $"set room {number}", raw =>
            {
                if (isNew && raw.Split(';').Any(e => BuilderEntryNumber(e.Trim()) == number))
                {
                    lines.Add($"Room {number} was just added to the stored list by someone else. Nothing changed - try again.");
                    return null;
                }

                // An existing room keeps every cell the DATABASE row has for it (review 2026-09-24): the caller worked from the
                // cached room, and a cell added to the row by hand but not reloaded yet must not be dropped by this write.
                var allCells = new HashSet<uint>(cells);
                if (!isNew && TryParseRooms(raw, out var stored, out _))
                {
                    var current = stored.FirstOrDefault(r => r.Number == number);
                    if (current != null)
                        allCells.UnionWith(current.Cells);
                }

                var entry = BuilderRoomEntry(number, landingCell, x, y, z, rotation.W, rotation.X, rotation.Y, rotation.Z, allCells);

                // Replace the entry with this number (the first, dropping any duplicate), or add it; keep number order.
                var entries = new List<string>();
                var swapped = false;
                foreach (var old in raw.Split(';').Select(e => e.Trim()).Where(e => e.Length > 0))
                {
                    if (BuilderEntryNumber(old) == number)
                    {
                        if (!swapped)
                            entries.Add(entry);
                        swapped = true;
                    }
                    else
                        entries.Add(old);
                }

                if (!swapped)
                    entries.Add(entry);

                return string.Join(";", entries.OrderBy(e => BuilderEntryNumber(e) ?? int.MaxValue));
            }, lines, out _);

            if (saved)
                GetRooms(sourceWcid);

            return saved;
        }

        /// <summary>
        /// what = player | monster | door; the pin is a cell of the dungeon and a spot in it (landblock-local x, y).
        ///  - monster: this dungeon's own generator at the centre of the pinned room, unless the room has one.
        ///  - player: the room's landing in the corner of the pinned cell nearest the pin, padded in from the walls and
        ///    checked against the physics cell; a cell that is no room yet becomes one.
        ///  - door: a wall in the opening nearest the pin, at its centre, facing along it, unless one stands there.
        /// Afterwards the player faces the generator and the generator faces the landing, and the room's dead-end cell
        /// above or below is part of the room.
        /// </summary>
        public static List<string> BuilderPlace(Player player, string what, string cellText, float pinX, float pinY, string mode = "room", float? hereZ = null, uint doorWcid = 0)
        {
            var lines = new List<string>();

            if (!BuilderResolve(player, out var sourceWcid, out var rooms, out var variation, out var error, forWrite: true))
            {
                lines.Add(error);
                return lines;
            }

            if (!BuilderMayWrite(variation, lines))
                return lines;

            // "Monster Here" uses where the admin stands, so they must stand in this dungeon - its landblock AND variation
            // (review 2026-09-24: from the same landblock at another variation it placed at their coordinates anyway).
            if (mode == "here" && !BuilderMustStandIn(player, rooms, variation, lines))
                return lines;

            var landblock = rooms[0].LandingCell >> 16;

            cellText = (cellText ?? "").Trim();
            if (cellText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                cellText = cellText.Substring(2);

            if (!uint.TryParse(cellText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var cell))
            {
                lines.Add($"\"{cellText}\" is not a cell id.");
                return lines;
            }

            if (cell <= 0xFFFF)
                cell |= landblock << 16;

            var dat = BuilderDatCells(landblock);
            if (cell >> 16 != landblock || !dat.TryGetValue(cell, out var pinned))
            {
                lines.Add($"Cell 0x{cell:X8} is not an indoor cell of this dungeon (0x{landblock:X4}).");
                return lines;
            }

            var room = rooms.FirstOrDefault(r => r.Cells.Contains(cell));
            var floorZ = pinned.Centre.Z;

            switch (what)
            {
                case "door":
                {
                    BuilderOpening best = null;
                    var bestDistance = float.MaxValue;

                    foreach (var info in dat.Values)
                        foreach (var opening in info.Openings)
                        {
                            if (opening.Vertical || Math.Abs(opening.FloorZ - floorZ) > 3f)
                                continue;

                            var dx = opening.Centre.X - pinX;
                            var dy = opening.Centre.Y - pinY;
                            var distance = dx * dx + dy * dy;
                            if (distance < bestDistance) { bestDistance = distance; best = opening; }
                        }

                    if (best == null || bestDistance > 6f * 6f)
                    {
                        lines.Add("No doorway within 6 of the pin on this level. Drop the pin nearer the opening.");
                        return lines;
                    }

                    var low = landblock << 16;
                    var high = low | 0xFFFF;
                    var here = VariationManager.NormalizeBase(variation);

                    List<LandblockInstance> placed;
                    using (var context = new WorldDbContext())
                        placed = context.LandblockInstance.Where(i => i.ObjCellId >= low && i.ObjCellId <= high).ToList();

                    var standing = placed.FirstOrDefault(i => VariationManager.NormalizeBase(i.VariationId) == here
                        && Math.Abs(i.OriginZ - best.FloorZ) <= 4f
                        && (i.OriginX - best.Centre.X) * (i.OriginX - best.Centre.X) + (i.OriginY - best.Centre.Y) * (i.OriginY - best.Centre.Y) <= 3f * 3f
                        && DatabaseManager.World.GetCachedWeenie(i.WeenieClassId)?.WeenieType == WeenieType.Door);
                    // 3.0: the hand-placed walls that missed their doorway did so by 2.6 and 2.7 (measured 2026-09-20)

                    // The wall goes on the side away from the room (where most hand-placed ones are), a touch off the
                    // opening's plane so its cell is never in doubt.
                    var wallCell = best.Cell;
                    var outward = best.Normal;
                    var cellRoom = rooms.FirstOrDefault(r => r.Cells.Contains(best.Cell));
                    var otherRoom = rooms.FirstOrDefault(r => r.Cells.Contains(best.Other));

                    if (dat.TryGetValue(best.Other, out var otherInfo))
                    {
                        var toOther = new System.Numerics.Vector3(otherInfo.Centre.X - dat[best.Cell].Centre.X, otherInfo.Centre.Y - dat[best.Cell].Centre.Y, 0);
                        if (toOther.Length() > 0.01f)
                            outward = System.Numerics.Vector3.Normalize(toOther);

                        if (cellRoom != null && otherRoom == null)
                            wallCell = best.Other;                       // outward already points out of the room
                        else if (otherRoom != null && cellRoom == null)
                            outward = -outward;                          // the room is on the far side: stay in best.Cell
                    }

                    var spot = best.Centre + outward * 0.1f;

                    // Owner 2026-09-20: a door always runs exactly north-south or east-west. The opening's own direction is
                    // snapped to the nearer axis, so a slightly skewed doorway polygon can never leave a door a few degrees off.
                    var axis = Math.Abs(best.Normal.X) >= Math.Abs(best.Normal.Y)
                        ? new System.Numerics.Vector3(Math.Sign(best.Normal.X), 0, 0)
                        : new System.Numerics.Vector3(0, Math.Sign(best.Normal.Y), 0);
                    var facing = BuilderFacing(axis.X, axis.Y);
                    var at = new ACE.Entity.Position(wallCell, spot.X, spot.Y, best.FloorZ + 0.03f, facing.X, facing.Y, facing.Z, facing.W, false, variation);
                    var runs = axis.X != 0 ? "east-west" : "north-south";

                    if (standing != null)
                    {
                        // Owner 2026-09-20: a door that is already there is REPLACED - put exactly in the doorway, squared up.
                        var off = Math.Sqrt((standing.OriginX - spot.X) * (standing.OriginX - spot.X) + (standing.OriginY - spot.Y) * (standing.OriginY - spot.Y));
                        if (BuilderMoveInstance(player, standing.Guid, at, lines))
                            lines.Add($"Wall 0x{standing.Guid:X8} moved into the doorway 0x{best.Cell & 0xFFFF:X4} > 0x{best.Other & 0xFFFF:X4} (v:{variation}), from {off:0.00} away, facing {runs}.");

                        return lines;
                    }

                    // The tab's door picker wins; with no pick, the dungeon's own most-placed door; with neither, the
                    // Magic Wall (owner 2026-09-22).
                    var wallWcid = doorWcid;
                    if (wallWcid != 0)
                    {
                        var picked = DatabaseManager.World.GetCachedWeenie(wallWcid);
                        if (picked == null || picked.WeenieType != WeenieType.Door)
                        {
                            lines.Add($"{wallWcid} is not a door weenie - placing this dungeon's usual door instead.");
                            wallWcid = 0;
                        }
                    }
                    if (wallWcid == 0)
                        wallWcid = BuilderMostPlaced(landblock, variation, w => w.WeenieType == WeenieType.Door);
                    if (wallWcid == 0)
                        wallWcid = BuilderDefaultWallWcid;

                    var wallGuid = BuilderPlaceInstance(player, wallWcid, at, lines);
                    if (wallGuid != 0)
                        lines.Add($"Wall 0x{wallGuid:X8} ({wallWcid}) closes the doorway 0x{best.Cell & 0xFFFF:X4} > 0x{best.Other & 0xFFFF:X4} (v:{variation}), facing {runs}.");

                    return lines;
                }

                case "monster":
                {
                    var cells = room != null ? room.Cells : new HashSet<uint> { cell };
                    var existing = BuilderFindGenerator(cells, variation, new ACE.Entity.Position(cell, pinX, pinY, floorZ, 0, 0, 0, 1, false, variation));

                    uint generatorWcid = 0;
                    if (existing == null)
                    {
                        generatorWcid = BuilderMostPlaced(landblock, variation, w => w.PropertiesGenerator != null && w.PropertiesGenerator.Count > 0);
                        if (generatorWcid == 0)
                        {
                            lines.Add("This dungeon has no generator placed yet to copy - place the first one with Add Generator.");
                            return lines;
                        }
                    }

                    // Where it goes (owner 2026-09-20): "room" = the middle of the room's cells on this level, for the usual
                    // one-cell room the cell's own centre; "cell" = the centre of the PINNED cell, how a room of several
                    // cells picks one; "here" = exactly the given spot (Monster Here: where the player stands).
                    var middle = pinned.Centre;
                    var middleCell = cell;
                    var spot = "cell 0x" + (cell & 0xFFFF).ToString("X4") + (room != null ? $" in room {room.Number}" : "");

                    if (mode == "here")
                    {
                        middle = new System.Numerics.Vector3(pinX, pinY, hereZ ?? floorZ);
                        floorZ = middle.Z;
                        spot = "where you stand, in " + spot;
                    }
                    else if (mode != "cell")
                    {
                        var level = cells.Where(c => dat.TryGetValue(c, out var i) && Math.Abs(i.Centre.Z - floorZ) < 0.5f).Select(c => dat[c].Centre).ToList();
                        var average = level.Count > 0 ? level.Aggregate(System.Numerics.Vector3.Zero, (a, b) => a + b) / level.Count : pinned.Centre;
                        var averageCell = cells.FirstOrDefault(c => BuilderInsideCell(c, variation, average.X, average.Y, floorZ));
                        if (averageCell != 0)
                        {
                            middle = average;
                            middleCell = averageCell;
                            spot = room != null ? $"room {room.Number}" : spot;
                        }
                    }

                    // Face the landing when the room has one that is not on this very spot.
                    var rotation = System.Numerics.Quaternion.Identity;
                    if (room != null && ((room.X - middle.X) * (room.X - middle.X) + (room.Y - middle.Y) * (room.Y - middle.Y)) > 0.25f)
                        rotation = BuilderFacing(room.X - middle.X, room.Y - middle.Y);

                    var at = new ACE.Entity.Position(middleCell, middle.X, middle.Y, floorZ + 0.05f, rotation.X, rotation.Y, rotation.Z, rotation.W, false, variation);

                    if (existing != null)
                    {
                        // Owner 2026-09-20: a room's generator is MOVED to the centre, not left where it was placed by hand.
                        if (!BuilderMoveGenerator(player, existing, at, lines))
                            return lines;

                        lines.Add($"Generator 0x{existing.Guid:X8} moved to {(mode == "here" ? "" : "the centre of ")}{spot} (v:{variation}), from {Math.Sqrt((existing.X - middle.X) * (existing.X - middle.X) + (existing.Y - middle.Y) * (existing.Y - middle.Y)):0.0} away.");
                    }
                    else
                    {
                        var generatorGuid = BuilderPlaceInstance(player, generatorWcid, at, lines);
                        if (generatorGuid == 0)
                            return lines;

                        lines.Add($"Generator 0x{generatorGuid:X8} ({generatorWcid}) placed at {(mode == "here" ? "" : "the centre of ")}{spot} (v:{variation}).");
                    }

                    // The landing now faces the new generator.
                    if (room != null && ((room.X - middle.X) * (room.X - middle.X) + (room.Y - middle.Y) * (room.Y - middle.Y)) > 0.25f)
                    {
                        var withDeadEnds = new HashSet<uint>(room.Cells);
                        foreach (var extra in BuilderDeadEndCells(dat, withDeadEnds, rooms, room).ToList())
                            withDeadEnds.Add(extra);

                        if (BuilderSaveRoom(player, sourceWcid, room.Number, room.LandingCell, room.X, room.Y, room.Z, BuilderFacing(middle.X - room.X, middle.Y - room.Y), withDeadEnds, lines))
                            lines.Add($"Room {room.Number}'s landing now faces it.");
                    }
                    else if (room == null)
                        lines.Add("This cell is no room yet - drop the Player pin in it to make one.");

                    return lines;
                }

                case "player":
                {
                    float landX = 0, landY = 0;
                    var signX = pinX >= pinned.Centre.X ? 1f : -1f;
                    var signY = pinY >= pinned.Centre.Y ? 1f : -1f;
                    var where = "";

                    if (mode == "middle")
                    {
                        // Player Middle (owner 2026-09-21): the landing at the middle of the room's cells on this level - for
                        // the usual one-cell room, the cell's own centre - like Monster Middle.
                        var cellsHere = room != null ? room.Cells : new HashSet<uint> { cell };
                        var level = cellsHere.Where(c => dat.TryGetValue(c, out var i) && Math.Abs(i.Centre.Z - floorZ) < 0.5f).Select(c => dat[c].Centre).ToList();
                        var average = level.Count > 0 ? level.Aggregate(System.Numerics.Vector3.Zero, (a, b) => a + b) / level.Count : pinned.Centre;
                        var averageCell = cellsHere.FirstOrDefault(c => BuilderInsideCell(c, variation, average.X, average.Y, floorZ));
                        if (averageCell != 0)
                        {
                            cell = averageCell;
                            pinned = dat[cell];
                        }
                        else
                            average = pinned.Centre;

                        landX = average.X;
                        landY = average.Y;
                        where = "at the middle of " + (room != null ? $"room {room.Number}" : $"cell 0x{cell & 0xFFFF:X4}");
                    }
                    else
                    {
                        // The corner of the pinned cell nearest the pin, in from the walls.
                        var found = false;
                        foreach (var offset in BuilderCornerOffsets)
                        {
                            landX = pinned.Centre.X + signX * offset;
                            landY = pinned.Centre.Y + signY * offset;
                            if (BuilderInsideCell(cell, variation, landX, landY, floorZ))
                            {
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {
                            lines.Add($"No spot in that corner of cell 0x{cell:X8} is inside the cell. Drop the pin toward another corner.");
                            return lines;
                        }

                        where = $"in the {(signY > 0 ? "north" : "south")}-{(signX > 0 ? "east" : "west")} corner of cell 0x{cell & 0xFFFF:X4}";
                    }

                    var cells = new HashSet<uint>(room != null ? room.Cells : new HashSet<uint>());
                    cells.Add(cell);
                    foreach (var extra in BuilderDeadEndCells(dat, cells, rooms, room).ToList())
                        cells.Add(extra);

                    // Face the room's generator; with none yet, the middle of the cell, where the monster will stand.
                    var landing = new ACE.Entity.Position(cell, landX, landY, floorZ + 0.005f, 0, 0, 0, 1, false, variation);
                    var generator = BuilderFindOrJoinGenerator(dat, cells, rooms, room, variation, landing, lines);
                    var targetX = generator != null ? generator.X : pinned.Centre.X;
                    var targetY = generator != null ? generator.Y : pinned.Centre.Y;
                    // On the very spot it would face (Player Middle over Monster Middle): keep north rather than a random turn.
                    var onSpot = (targetX - landX) * (targetX - landX) + (targetY - landY) * (targetY - landY) < 0.25f;
                    var rotation = onSpot ? System.Numerics.Quaternion.Identity : BuilderFacing(targetX - landX, targetY - landY);

                    var number = room?.Number ?? 0;
                    if (number == 0)
                        for (number = 1; rooms.Any(r => r.Number == number); number++) { }

                    if (!BuilderSaveRoom(player, sourceWcid, number, cell, landX, landY, floorZ + 0.005f, rotation, cells, lines, isNew: room == null))
                        return lines;

                    lines.Add($"Room {number} (v:{variation}): {(room != null ? "landing moved" : "new room, landing set")} {where}, facing {(onSpot ? "north (it stands on the spot it would face)" : generator != null ? "the generator" : "the middle of the room")}. {cells.Count} cell(s).");

                    if (generator != null && !onSpot)
                        BuilderTurnGenerator(player, generator, variation, BuilderFacing(landX - generator.X, landY - generator.Y), lines);

                    var saved = GetRooms(sourceWcid)?.FirstOrDefault(r => r.Number == number);
                    if (saved != null && Landing(saved, variation, sourceWcid) == null)
                        lines.Add("WARNING: this landing is adjusted into a cell outside the room - it will not be handed out. Drop the pin elsewhere.");
                    else if (saved != null)
                        TestMarkLanding(saved, variation, sourceWcid, lines);   // no-op while the test tools are off

                    return lines;
                }

                default:
                    lines.Add("Place what? player, monster or door.");
                    return lines;
            }
        }
    }
}
