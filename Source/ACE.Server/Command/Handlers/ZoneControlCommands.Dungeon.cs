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

            // A verb that can change the map (rooms, cells, landings, placed objects, or which dungeon) answers with the
            // fresh map lines after its reply (owner 2026-09-21: the plugin used to wait 0.7 s and ask - the delay you
            // felt on every click). The plugin reads whatever map lines arrive, asked for or not.
            var mapChanges = verb == "add" || verb == "remove" || verb == "land" || verb == "cell" || verb == "place"
                || verb == "monster" || verb == "select" || verb == "nudge";

            try
            {
                if (verb != "state" && verb != "map" && verb != "list")
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

                    case "list":
                        // Only the [[ZCDGL]] lines: the tab's Dungeon dropdown.
                        All(RoomAssignManager.BuilderList());
                        return;

                    case "select":
                        All(RoomAssignManager.BuilderSelect(player, Arg(2), Arg(3)));
                        break;

                    case "entrance":
                        All(RoomAssignManager.BuilderEntrance(player));
                        break;

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

                        // Monster pin: "cell" = the centre of the pinned cell, else the middle of the room. Player pin: "middle" =
                        // the middle of the room instead of the nearest corner.
                        var pinMode = args.Count > 6 ? args[6].ToLowerInvariant() : "room";
                        uint pinDoor = 0;
                        if (pinMode != "cell" && pinMode != "middle")
                        {
                            // A door pick rides in the same slot as the mode - "place door <cell> <x> <y> <wcid>".
                            if (args.Count > 6)
                                uint.TryParse(args[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out pinDoor);
                            pinMode = "room";
                        }
                        All(RoomAssignManager.BuilderPlace(player, arg, args[3], pinX, pinY, pinMode, null, pinDoor));
                        break;

                    case "nudge":
                        // nudge <guid hex> <dx> <dy> [turn degrees] - the tab's Nudge pop-out, on a wall or generator picked on the map.
                        if (args.Count < 5
                            || !uint.TryParse((Arg(2) ?? "").Replace("0x", "").Replace("0X", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var nudgeGuid)
                            || !float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var nudgeX)
                            || !float.TryParse(args[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var nudgeY))
                        {
                            Msg("/zonecontrol dungeon nudge <guid> <east> <north> [turn degrees] - move a placed wall or generator a little.");
                            break;
                        }

                        var nudgeTurn = 0f;
                        if (args.Count > 5 && !float.TryParse(args[5], NumberStyles.Float, CultureInfo.InvariantCulture, out nudgeTurn))
                            nudgeTurn = 0f;

                        All(RoomAssignManager.BuilderNudge(player, nudgeGuid, nudgeX, nudgeY, nudgeTurn));
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

                    case "zoneshare":
                        // Zone Share (owner 2026-09-23): everyone in the selected dungeon shares kill XP, luminance and kill
                        // tasks as one fellowship. Stored on the room source's weenie, like its room list.
                        if (arg != "on" && arg != "off")
                        {
                            Msg("/zonecontrol dungeon zoneshare on|off - everyone in this dungeon shares kill XP, luminance and kill tasks as one fellowship.");
                            break;
                        }

                        All(RoomAssignManager.BuilderSetZoneShare(player, arg == "on"));
                        break;

                    case "killreward":
                        // Kill Reward (owner 2026-09-23): items every N kills per player, at most once per cooldown - several per
                        // dungeon. Stored on the room source's weenie, like its room list. The edit itself is shared with zones.
                        if (arg == null)
                        {
                            Msg("/zonecontrol dungeon killreward " + KillRewardManager.EditUsage + " - items every N kills per player, at most once per cooldown.");
                            break;
                        }

                        All(RoomAssignManager.BuilderSetKillReward(player, c => KillRewardManager.Edit(c, arg, args, 3)));
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

                    case "doors":
                        if (arg == "show" || arg == "clear")
                        {
                            var doorSeconds = 5;
                            if (args.Count > 3 && (!int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out doorSeconds) || doorSeconds < 1 || doorSeconds > 120))
                            {
                                Msg("The seconds must be a number from 1 to 120.");
                                break;
                            }

                            All(RoomAssignManager.TestDoorShow(player, arg, doorSeconds));
                            break;
                        }

                        All(RoomAssignManager.BuilderDoorList(player));
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
                        Msg("  /zonecontrol dungeon list                  - every room dungeon the server knows");
                        Msg("  /zonecontrol dungeon select here|<wcid> <v> - work on that dungeon from anywhere; here = follow me again");
                        Msg("  /zonecontrol dungeon entrance              - teleport to the dungeon's entrance portal or plate");
                        Msg("  /zonecontrol dungeon goto <room>           - teleport to a room's landing (no claim)");
                        Msg("  /zonecontrol dungeon doors                 - every door weenie the Door pin can use");
                        Msg("  /zonecontrol dungeon doors show [seconds]  - one of each in a row in front of you, 5s by default");
                        Msg("  /zonecontrol dungeon add [room]            - new room here; lowest free number, or that one");
                        Msg("  /zonecontrol dungeon remove <room>         - take a room out of the list");
                        Msg("  /zonecontrol dungeon land                  - landing of this room = here, facing its generator");
                        Msg("  /zonecontrol dungeon cell <room> <cell>    - add a cell to a room, or take it out");
                        Msg("  /zonecontrol dungeon place <what> <cell> <x> <y> [cell|middle] - player | monster | door at a map pin");
                        Msg("  /zonecontrol dungeon monster here          - the room's generator exactly where you stand");
                        Msg("  /zonecontrol dungeon nudge <guid> <east> <north> [turn] - move a placed wall or generator a little");
                        Msg("  /zonecontrol dungeon zoneshare on|off      - everyone in the dungeon shares kills as one fellowship");
                        Msg("  /zonecontrol dungeon killreward on|off|add|set <n>|remove <n> - items every N kills per player");
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

                if (mapChanges)
                    All(RoomAssignManager.BuilderMap(player));
            }
            catch (Exception ex)
            {
                Msg($"/zonecontrol dungeon failed: {ex.Message}");
                dungeonLog.Error($"[RoomAssign][DUNGEON] /zonecontrol {string.Join(" ", args)} failed: {ex}");
            }
        }
    }
}
