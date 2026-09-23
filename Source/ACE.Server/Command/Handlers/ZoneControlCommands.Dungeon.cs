using System;
using System.Collections.Generic;
using System.Globalization;

using ACE.Server.Managers;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// /zonecontrol dungeon ... - the dungeon builder (RoomAssignManager.Builder.cs) and the Room Assign test tools
    /// (RoomAssignManager.Testing.cs), behind the Zone Control plugin's Dungeons tab. Made permanent 2026-09-21 (owner);
    /// it replaces the test-only /roomrig.
    ///
    /// Every command and every reply is also written to the server log as [RoomAssign][DUNGEON]: a pin that did nothing
    /// must leave a trace. The two machine-readable line kinds ([[ZCDG]] state, [[ZCDGM]] map) are left out of the log.
    /// </summary>
    public static partial class ZoneControlCommands
    {
        private static readonly log4net.ILog dungeonLog = log4net.LogManager.GetLogger(typeof(ZoneControlCommands));

        /// <param name="args">The re-tokenized /zonecontrol arguments: args[0] is "dungeon", args[1] the verb.</param>
        private static void HandleDungeon(Session session, List<string> args)
        {
            void Msg(string s)
            {
                if (s == null)
                    return;

                // Wire lines can grow with the dungeon (a state line carries every room), so they go out chunked.
                if (s.StartsWith("[[ZC", StringComparison.Ordinal))
                {
                    SendChunked(session, s);
                    return;
                }

                ChatPacket.SendServerMessage(session, s, ACE.Entity.Enum.ChatMessageType.Broadcast);
                dungeonLog.Info($"[RoomAssign][DUNGEON] > {s}");
            }
            void All(List<string> lines) { foreach (var line in lines) Msg(line); }

            string Arg(int index) => args.Count > index ? args[index] : null;

            var player = session?.Player;
            if (player == null)
            {
                Msg("/zonecontrol dungeon needs a character in the world.");
                return;
            }

            var verb = (Arg(1) ?? "help").ToLowerInvariant();
            var arg = Arg(2)?.ToLowerInvariant();

            try
            {
                if (verb != "state" && verb != "map")
                    dungeonLog.Info($"[RoomAssign][DUNGEON] {player.Name}: /zonecontrol {string.Join(" ", args)}  (at 0x{player.Location?.Cell:X8} v:{player.Location?.Variation})");

                switch (verb)
                {
                    case "status":
                        All(RoomAssignManager.TestStatus(player));
                        break;

                    case "state":
                        break;   // only the state line below

                    case "map":
                        // Only the map lines: no state line after them, the plugin asks for that separately.
                        All(RoomAssignManager.BuilderMap(player));
                        return;

                    case "goto":
                        if (arg == null || !int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var gotoNumber) || gotoNumber < 1)
                        {
                            Msg("Go to which room? /zonecontrol dungeon goto <room>.");
                            break;
                        }

                        All(RoomAssignManager.BuilderGoto(player, gotoNumber));
                        break;

                    // ---- building: every one of these is refused below variation 3 (RoomAssignManager.BuilderMayWrite)

                    case "add":
                        var addNumber = 0;
                        if (arg != null && (!int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out addNumber) || addNumber < 1))
                        {
                            Msg("Add which number? A room number, or nothing for the lowest one free.");
                            break;
                        }

                        All(RoomAssignManager.BuilderAddRoom(player, addNumber));
                        break;

                    case "remove":
                        if (arg == null || !int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var removeNumber) || removeNumber < 1)
                        {
                            Msg("Remove which room? /zonecontrol dungeon remove <room>.");
                            break;
                        }

                        All(RoomAssignManager.BuilderRemoveRoom(player, removeNumber));
                        break;

                    case "land":
                        All(RoomAssignManager.BuilderSetLanding(player));
                        break;

                    case "cell":
                        if (args.Count < 4 || !int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cellRoom) || cellRoom < 1)
                        {
                            Msg("/zonecontrol dungeon cell <room> <cell> - adds the cell to the room, or takes it out.");
                            break;
                        }

                        All(RoomAssignManager.BuilderToggleCell(player, cellRoom, args[3]));
                        break;

                    case "place":
                        if (args.Count < 6
                            || !float.TryParse(args[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var pinX)
                            || !float.TryParse(args[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var pinY))
                        {
                            Msg("/zonecontrol dungeon place player|monster|door <cell> <x> <y> [cell] - the map sends this when a pin is dropped.");
                            break;
                        }

                        // Monster pin only: "cell" = the centre of the pinned cell, anything else = the middle of the room.
                        var pinMode = args.Count > 6 && args[6].Equals("cell", StringComparison.OrdinalIgnoreCase) ? "cell" : "room";
                        All(RoomAssignManager.BuilderPlace(player, arg, args[3], pinX, pinY, pinMode));
                        break;

                    case "monster":
                        // Monster Here: the generator goes EXACTLY where the player stands, the twin of "land".
                        if (arg != "here" || player.Location == null)
                        {
                            Msg("/zonecontrol dungeon monster here - the room's generator is placed (or moved) exactly where you stand.");
                            break;
                        }

                        All(RoomAssignManager.BuilderPlace(player, "monster", player.Location.Cell.ToString("X8"), player.Location.PositionX, player.Location.PositionY, "here", player.Location.PositionZ));
                        break;

                    // ---- test tools: every one of these answers "off" unless room_assign_test_tools is true

                    case "admin":
                        if (!RoomAssignManager.TestToolsOn)
                        {
                            Msg("Test tools are off on this server (room_assign_test_tools).");
                            break;
                        }

                        if (arg != "on" && arg != "off")
                        {
                            Msg($"Admins count as players: {(RoomAssignManager.TestAdminCounts ? "ON" : "off")}. Use /zonecontrol dungeon admin on|off.");
                            break;
                        }

                        RoomAssignManager.TestAdminCounts = arg == "on";
                        Msg(RoomAssignManager.TestAdminCounts
                            ? "Admins now COUNT as players: rooms, holds, occupancy and login rules all apply to you."
                            : "Admins no longer count: portals use their normal destination for you.");
                        break;

                    case "fill" when arg == "random" && args.Count > 3 && args[3].Equals("leave", StringComparison.OrdinalIgnoreCase):
                        var leaveOpen = 0;
                        if (args.Count > 4 && (!int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out leaveOpen) || leaveOpen < 0))
                        {
                            Msg("Leave how many open? A number, 0 or more.");
                            break;
                        }

                        All(RoomAssignManager.TestFillLeave(player, leaveOpen));
                        break;

                    case "fill":
                        var count = 1;
                        if (args.Count > 3 && (!int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count < 1))
                        {
                            Msg("The count must be a number, 1 or more.");
                            break;
                        }

                        All(RoomAssignManager.TestFill(player, arg ?? "here", count));
                        break;

                    case "clear":
                        var number = 0;
                        if (arg != null && (!int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) || number < 1))
                        {
                            Msg("Clear which room? A room number, or nothing for every fake player.");
                            break;
                        }

                        All(RoomAssignManager.TestClear(player, number));
                        break;

                    case "markers":
                        if (arg != "all" && arg != "clear")
                        {
                            Msg("/zonecontrol dungeon markers all [minutes] - WCID 1 on every landing, gone again after that long. /zonecontrol dungeon markers clear - remove them.");
                            break;
                        }

                        var markerMinutes = 0;
                        if (args.Count > 3 && (!int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out markerMinutes) || markerMinutes < 1 || markerMinutes > 120))
                        {
                            Msg("The minutes must be a number from 1 to 120.");
                            break;
                        }

                        All(RoomAssignManager.TestMarkers(player, arg, markerMinutes));
                        break;

                    default:
                        Msg("Dungeon builder - one-player room dungeons (writes only to variation 3 and up)");
                        Msg("  /zonecontrol dungeon status                - every room: who, reservation, hold");
                        Msg("  /zonecontrol dungeon goto <room>           - teleport to a room's landing (no claim)");
                        Msg("  /zonecontrol dungeon add [room]            - new room here; lowest free number, or that one");
                        Msg("  /zonecontrol dungeon remove <room>         - take a room out of the list");
                        Msg("  /zonecontrol dungeon land                  - landing of this room = here, facing its generator");
                        Msg("  /zonecontrol dungeon cell <room> <cell>    - add a cell to a room, or take it out");
                        Msg("  /zonecontrol dungeon place <what> <cell> <x> <y> [cell] - player | monster | door at a map pin");
                        Msg("  /zonecontrol dungeon monster here          - the room's generator exactly where you stand");
                        Msg("  /zonecontrol dungeon state | map           - data lines for the plugin's Dungeons tab");
                        Msg("Test tools (need server property room_assign_test_tools = true; memory only):");
                        Msg("  /zonecontrol dungeon admin on|off          - admins count as players");
                        Msg("  /zonecontrol dungeon fill here|<room>|random [count]|random leave <n> - fake players");
                        Msg("  /zonecontrol dungeon clear [room]          - remove one, or every fake player");
                        Msg("  /zonecontrol dungeon markers all [min]|clear - WCID 1 on every landing");
                        return;
                }

                // One machine-readable line after every command: the plugin tab colours its rooms from it.
                Msg(RoomAssignManager.BuilderState(player));
            }
            catch (Exception ex)
            {
                Msg($"/zonecontrol dungeon failed: {ex.Message}");
                dungeonLog.Error($"[RoomAssign][DUNGEON] /zonecontrol {string.Join(" ", args)} failed: {ex}");
            }
        }
    }
}
