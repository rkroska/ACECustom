using ACE.Database;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Physics.Common;
using ACE.Server.Entity;
using ACE.Server.WorldObjects;
using ACE.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Position = ACE.Entity.Position;

namespace ACE.Server.Command.Handlers
{
    public class TeleportCommands
    {

        public const string TeleHelpMessage = "Teleports the target to a destination.\n" +
                                              "Usage: /tele [target] [to] [type] <destination>\n" +
                                              "Note: 'to' is optional if target is omitted (defaults to you).\n" +
                                              "Types:\n" +
                                              "  coords <lat/long> (e.g. 10.5N, 40.2E)\n" +
                                              "  loc <hex string> \n" +
                                              "  player <name>\n" +
                                              "  dungeon <name|hex>\n" +
                                              "  poi <name>\n" +
                                              "  variant <retail|default|null|id> (retail/default/null = base; 0 = variation 0)\n" +
                                              "  xyz <cell> <x> <y> <z>...\n" +
                                              "  type <id>\n" +
                                              "  dist <distance>\n" +
                                              "  return (previous location)\n" +
                                              "Examples:\n" +
                                              "  /tele 10.5N, 40.2E\n" +
                                              "  /tele dungeon Mountain Keep\n" +
                                              "  /tele Bob to poi Holtburg\n" +
                                              "  /tele return";

        public const string PortalHelpMessage = "Spawns a portal at the target to a destination.\n" +
                                                "Usage: /portal [target] [to] [type] <destination>\n" +
                                                "Note: 'to' is optional if target is omitted (defaults to you).\n" +
                                                "Types:\n" +
                                                "  coords <lat/long> (e.g. 10.5N, 40.2E)\n" +
                                                "  loc <hex string>\n" +
                                                "  player <name>\n" +
                                                "  dungeon <name|hex>\n" +
                                                "  poi <name>\n" +
                                                "  variant <retail|default|null|id> (retail/default/null = base; 0 = variation 0)\n" +
                                                "  xyz <cell> <x> <y> <z>...\n" +
                                                "  type <id>\n" +
                                                "  dist <distance>\n" +
                                                "  return (previous location)\n" +
                                                "Examples:\n" +
                                                "  /portal 10.5N, 40.2E\n" +
                                                "  /portal dungeon Mountain Keep\n" +
                                                "  /portal Bob to poi Holtburg";


        // ==========================================================================================
        // TELEPORT / PORTAL TO PLAYER
        // ==========================================================================================

        [CommandHandler("tele", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1,
            "Teleport with natural language syntax.",
            TeleHelpMessage)]
        public static void HandleTele(Session session, params string[] parameters)
        {
            if (parameters.Length == 1 && parameters[0].Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("\n" + TeleHelpMessage, ChatMessageType.System));
                return;
            }

            if (!ParseNaturalLanguageArgs(session, parameters, out WorldObject target, out string type, out string[] args)) return;
            if (!ResolveDestination(session, target, type, args, out Position destPos, out string destName)) return;
            if (!ValidateDestination(session, destPos)) return;
            target.SetPosition(PositionType.TeleportedCharacter, target.Location);
            target.Teleport(destPos);
            // A moved mob needs its home cleared or it will immediately run home.
            if (target is not Player && target is Creature) target.Home = destPos;
            target.OnTeleportComplete();

            if (target is Player targetPlayer)
            {
                if (target != session.Player)
                {
                    PlayerManager.BroadcastToAuditChannel(session.Player, $"Admin {session.Player.Name} teleported {target.Name} to {destName}.");
                    targetPlayer.Session?.Network.EnqueueSend(new GameMessageSystemChat($"{session.Player.Name} has teleported you to {destName}.", ChatMessageType.System));
                }
                else
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Teleporting to {destName}.", ChatMessageType.System));
                }
            }
            else
                session.Network.EnqueueSend(new GameMessageSystemChat($"Teleporting {target.Name} to {destName}.", ChatMessageType.System));
        }


        [CommandHandler("portal", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1,
            "Spawn a portal with natural language syntax.",
            PortalHelpMessage)]
        public static void HandlePortal(Session session, params string[] parameters)
        {
            if (parameters.Length == 1 && parameters[0].Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("\n" + PortalHelpMessage, ChatMessageType.System));
                return;
            }

            if (!ParseNaturalLanguageArgs(session, parameters, out WorldObject target, out string type, out var args)) return;
            if (!ResolveDestination(session, target, type, args, out Position destPos, out string destName)) return;
            if (!ValidateDestination(session, destPos)) return;
            SpawnPortal(session, target, destPos, $"Portal to {destName}");
            PlayerManager.BroadcastToAuditChannel(session.Player, $"Admin {session.Player.Name} spawned a portal to {destName} at {target.Location}.");
        }

        // ==========================================================================================
        // DEPRECATED COMMANDS
        // ==========================================================================================

        [CommandHandler("teletome", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1, 
            "Teleports a player to your current location.", 
            "Usage: /teletome <Player Name>\n" +
            "Example: /teletome Bob")]
        public static void HandleTeleToMe(Session session, params string[] parameters) => ForwardCommand(session, parameters, "me", true);

        [CommandHandler("teleto", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1,
            "Teleport yourself to another player.",
            "Usage: /teleto <player>\n" +
            "Example: /teleto Bob")]
        public static void HandleTeleto(Session session, params string[] parameters) => ForwardCommand(session, parameters, "player", false);

        [CommandHandler("teleloc", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1,
             "Teleport yourself to a specific location string.",
             "Usage: /teleloc \"<loc string>\"\n" +
             "Example: /teleloc \"0x12345678 [10.0 20.0 0.0] 1.0 0.0 0.0 0.0\"")]
        public static void HandleTeleLocation(Session session, params string[] parameters) => ForwardCommand(session, parameters, "loc", false);

        [CommandHandler("telepoi", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1,
            "Teleport yourself to a named Point of Interest",
             "Usage: /telepoi \"<poi string>\"\n" +
             "Example: /telepoi \"Arwic\"")]
        public static void HandleTeleportPoi(Session session, params string[] parameters) => ForwardCommand(session, parameters, "poi", false);

        [CommandHandler("teledungeon", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1,
            "Teleport to a dungeon by name or landblock.",
            "Usage: /teledungeon <Name|Landblock>\n" +
            "Example: /teledungeon Mountain Keep\n" +
            "Example: /teledungeon 0x1234")]
        public static void HandleTeleDungeon(Session session, params string[] parameters) => ForwardCommand(session, parameters, "dungeon", false);

        [CommandHandler("telexyz", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 8, 
            "Teleport to a manual coordinate location.", 
            "Usage: /telexyz <cell> <x> <y> <z> <qx> <qy> <qz> <qw>\n" +
            "Note: all parameters must be specified and cell must be in decimal form")]
        public static void HandleDebugTeleportXYZ(Session session, params string[] parameters) => ForwardCommand(session, parameters, "xyz", false);

        [CommandHandler("teletype", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1, 
            "Teleport to a saved character position type.", 
            "Usage: /teletype <uint 0-22>\n" +
            "Example: /teletype 1")]
        public static void HandleTeleType(Session session, params string[] parameters) => ForwardCommand(session, parameters, "type", false);

        [CommandHandler("teledist", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1, 
            "Teleports a some distance ahead of the last object spawned.", 
            "Usage: /teledist <distance>")]
        public static void HandleTeleportDist(Session session, params string[] parameters) => ForwardCommand(session, parameters, "dist", false);

        [CommandHandler("telereturn", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1, 
            "Return a player to their previous location.", 
            "Usage: /telereturn <Player Name>\n" +
            "Example: /telereturn Bob")]
        public static void HandleTeleReturn(Session session, params string[] parameters) => ForwardCommand(session, parameters, "return", true);

        // ==========================================================================================
        // MISC SINGLETON COMMANDS
        // ==========================================================================================

        [CommandHandler("teleallto", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0, 
            "Teleports all players to a player. If no target is specified, all players will be teleported to you.", 
            "Usage: /teleallto <Player Name>\n" +
            "Example: /teleallto Bob")]
        public static void HandleTeleAllTo(Session session, params string[] parameters)
        {
            Player destinationPlayer;

            if (parameters.Length > 0)
            {
                var targetName = string.Join(" ", parameters);
                destinationPlayer = PlayerManager.GetOnlinePlayer(targetName);
                if (destinationPlayer == null)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Player '{targetName}' was not found.", ChatMessageType.Broadcast));
                    return;
                }
            }
            else
            {
                destinationPlayer = session.Player;
            }

            foreach (var player in PlayerManager.GetAllOnline())
            {
                if (player == destinationPlayer)
                    continue;

                player.SetPosition(PositionType.TeleportedCharacter, new Position(player.Location));

                player.Teleport(new Position(destinationPlayer.Location));
            }

            PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} has teleported all online players to {destinationPlayer.Name}'s location.");
        }


        // ==========================================================================================
        // HELPERS
        // ==========================================================================================

        private static void SpawnPortal(Session session, WorldObject origin, Position dest, string name)
        {
            var summonLoc = origin.Location.InFrontOf(3.0);

            if (WorldObjectFactory.CreateNewWorldObject("portalgateway") is not Portal gateway) return;
            gateway.Location = new Position(summonLoc);
            gateway.UpdatePortalDestination(new Position(dest));
            gateway.TimeToRot = 60;
            gateway.EnterWorld();
            session.Network.EnqueueSend(new GameMessageSystemChat($"Spawned portal to {name}.", ChatMessageType.System));
        }

        private static bool ResolvePlayerTarget(Session session, string[] parameters, out Position pos, out string name, bool suppressErrors = false)
        {
            pos = null;
            name = "";

            var playerName = string.Join(" ", parameters);
            var player = PlayerManager.GetOnlinePlayer(playerName);
            if (player == null)
            {
                if (!suppressErrors) session.Network.EnqueueSend(new GameMessageSystemChat($"Player {playerName} was not found.", ChatMessageType.System));
                return false;
            }
            pos = new Position(player.Location);
            name = $"player {player.Name}";
            return true;
        }

        // Shared Logic Helpers

        private static bool ResolveVariant(Session session, WorldObject target, string[] args, out Position pos, out string name)
        {
            pos = new Position(target.Location);
            name = "";

            if (args.Length > 0)
            {
                var a = args[0].Trim();
                // Same semantics as @televariant: retail/default/null => unlayered base; 0 is explicit variation 0
                if (string.Equals(a, "null", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a, "retail", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a, "default", StringComparison.OrdinalIgnoreCase))
                {
                    pos.Variation = null;
                    name = "variant base (retail/default/null)";
                    return true;
                }
                if (int.TryParse(a, out var variant))
                {
                    pos.Variation = variant;
                    name = $"variant {variant}";
                    return true;
                }
            }
            session.Network.EnqueueSend(new GameMessageSystemChat("Usage: ... to variant <retail|default|null|integer id>", ChatMessageType.System));
            return false;
        }

        private static bool ResolveXYZ(Session session, string[] args, out Position pos, out string name)
        {
            pos = null;
            name = "";
            if (args.Length < 8 || !uint.TryParse(args[0], out var cell))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Usage: ... to xyz <cell> <x> <y> <z> <qx> <qy> <qz> <qw>", ChatMessageType.System));
                return false;
            }

            var positionData = new float[7];
            for (uint i = 0u; i < 7u; i++)
            {
                if (!float.TryParse(args[i + 1], out var position))
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid parameter {i+2}: {args[i+1]}", ChatMessageType.System));
                    return false;
                }
                positionData[i] = position;
            }
            pos = new Position(cell, positionData[0], positionData[1], positionData[2], positionData[3], positionData[4], positionData[5], positionData[6]);
            name = $"location {pos}";
            return true;
        }

        private static void ResolveTargetAndArgs(Session session, string[] parameters, out Player target, out string[] args)
        {
            target = session.Player;
            args = parameters;

            for (int i = parameters.Length; i > 0; i--)
            {
                var p = PlayerManager.GetOnlinePlayer(string.Join(" ", parameters.Take(i)));
                if (p != null)
                {
                    target = p;
                    args = [.. parameters.Skip(i)];
                    return;
                }
            }
        }

        private static bool ValidateDestination(Session session, Position dest)
        {
            var lb = LScape.get_landblock(dest.LandblockId.Raw, null);
            if (lb != null && lb.WaterType == LandDefs.WaterType.EntirelyWater && !lb.IsDungeon)
            {
                ChatPacket.SendServerMessage(session, $"Landblock 0x{dest.LandblockId.Landblock:X4} is entirely filled with water, and is impassable", ChatMessageType.System);
                return false;
            }
            return true;
        }

        private static void ForwardCommand(Session session, string[] parameters, string type, bool useResolve)
        {
            Player target = session.Player;
            string[] args = parameters;

            if (useResolve)
            {
                ResolveTargetAndArgs(session, parameters, out target, out args);
            }
            
            var newArgs = new List<string>();
            if (target != session.Player) newArgs.Add(target.Name);
            newArgs.Add("to");
            newArgs.Add(type);
            newArgs.AddRange(args);
            
            var commandString = "/tele " + string.Join(" ", newArgs);

            session.Network.EnqueueSend(new GameMessageSystemChat($"This command is deprecated - use the new /tele command!", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Running: {commandString}", ChatMessageType.System));

            HandleTele(session, newArgs.ToArray());
        }

        private static bool ParseNaturalLanguageArgs(Session session, string[] parameters, out WorldObject target, out string type, out string[] inputArgs)
        {
            target = session.Player;
            type = "";
            inputArgs = [];
            
            var paramsList = parameters.ToList();
            int toIndex = -1;

            // Find "to" keyword
            for (int i = 0; i < paramsList.Count; i++)
            {
                if (paramsList[i].Equals("to", StringComparison.OrdinalIgnoreCase))
                {
                    toIndex = i;
                    break;
                }
            }

            // Parse Target (Before "to")
            if (toIndex > 0)
            {
                var targetName = string.Join(" ", paramsList.Take(toIndex));
                    
                if (targetName.Equals("me", StringComparison.OrdinalIgnoreCase) || targetName.Equals("self", StringComparison.OrdinalIgnoreCase))
                {
                    target = session.Player;
                }
                else if (targetName.Equals("target", StringComparison.OrdinalIgnoreCase) || targetName.Equals("selected", StringComparison.OrdinalIgnoreCase))
                {
                    var wo = CommandHandlerHelper.GetSelected(session);
                    // TODO: could feasibly support teleporting world objects, need to move teleport method up to WorldObject first.
                    if (wo != null && wo.Location != null)
                    {
                        target = wo;
                    }
                    else
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"No valid entity selected.", ChatMessageType.System));
                        return false;
                    }
                }
                else
                {
                    target = PlayerManager.GetOnlinePlayer(targetName);
                    if (target == null)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Player not found: {targetName}", ChatMessageType.System));
                        return false;
                    }
                }
            }

            // Parse Type & Input (After "to")
            // This is a best effort, and may be part of the param if the param type was omitted for brevity. We will correct this later in the default case catch.
            if (toIndex + 1 < paramsList.Count)
            {
                type = paramsList[toIndex + 1].ToLower();
                inputArgs = paramsList.Skip(toIndex + 2).ToArray();
                return true;
            }

            session.Network.EnqueueSend(new GameMessageSystemChat($"Missing destination.", ChatMessageType.System));
            return false;
        }

        private static bool ResolveDestination(Session session, WorldObject target, string type, string[] args, out Position destPos, out string destName)
        {
            destPos = null;
            destName = "Unknown";
            
            switch (type.ToLower())
            {
                case "me":
                    destPos = new Position(session.Player.Location);
                    destName = session.Player.Name;
                    return true;

                case "target":
                case "targeted":
                case "selected":
                    return ResolveSelected(session, args, out destPos, out destName);

                case "loc":
                case "location":
                    return ResolveLocation(session, args, out destPos, out destName);

                case "dungeon":
                    return ResolveDungeon(session, args, out destPos, out destName);

                case "poi":
                    return ResolvePoi(session, args, out destPos, out destName);

                case "v":
                case "variant":
                    return ResolveVariant(session, target, args, out destPos, out destName);

                case "xyz":
                    return ResolveXYZ(session, args, out destPos, out destName);

                case "type":
                    return ResolveType(session, target, args, out destPos, out destName);

                case "dist":
                case "distance":
                    return ResolveDist(session, target, args, out destPos, out destName);

                case "player":
                    return ResolvePlayerTarget(session, args, out destPos, out destName);

                case "return":
                case "previous":
                    return ResolvePreviousLocation(session, target, out destPos, out destName);

                case "coordinates":
                case "coords":
                    return ResolveXYCoordinates(session, args, out destPos, out destName);

                default:
                    args = [type, .. args];
                    if (ResolveXYCoordinates(session, args, out destPos, out destName, /*suppressErrors=*/true)) return true;
                    if (ResolvePlayerTarget(session, args, out destPos, out destName, /*suppressErrors=*/true)) return true;
                    if (ResolveLocation(session, args, out destPos, out destName, /*suppressErrors=*/true)) return true;
                    if (ResolvePoi(session, args, out destPos, out destName, /*suppressErrors=*/true)) return true;
                    if (ResolveDungeon(session, args, out destPos, out destName, /*suppressErrors=*/true)) return true;
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid parameters. See /tele help or /portal help.", ChatMessageType.System));
                    return false;
            }
        }

        private static bool ResolveXYCoordinates(Session session, string[] args, out Position pos, out string name, bool suppressErrors = false)
        {
            pos = null;
            name = "";

            var strParams = string.Join(" ", args);
            Match match = Regex.Match(strParams, @"([\d\.]+[ns])[^\d\.]*([\d\.]+[ew])$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string ns = match.Groups[1].Value;
                string ew = match.Groups[2].Value;
                if (CommandParameterHelpers.TryParsePosition(new string[] { ns, ew }, out string errorMessage, out pos))
                {
                    name = $"{ns}, {ew}";
                    return true;
                }
                else
                {
                    if (!suppressErrors) session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid X/Y coordinates: {errorMessage}", ChatMessageType.System));
                    return false;
                }
            }

            if (!suppressErrors) session.Network.EnqueueSend(new GameMessageSystemChat($"Could not find X/Y coordinates in the form of '23.0N, 45.5W'", ChatMessageType.System));
            return false;
        }

        private static bool ResolveSelected(Session session, string[] args, out Position pos, out string name, bool suppressErrors = false)
        {
            pos = null;
            name = "";

            var wo = CommandHandlerHelper.GetSelected(session);
            if (wo == null)
            {
                if (!suppressErrors) session.Network.EnqueueSend(new GameMessageSystemChat("No previously selected item.", ChatMessageType.System));
                return false;
            }
            if (wo.Location == null)
            {
                if (!suppressErrors) session.Network.EnqueueSend(new GameMessageSystemChat("Selected item must have a valid world location.", ChatMessageType.System));
                return false;
            }
            pos = new Position(wo.Location);
            name = $"{wo.Name}";
            return true;
        }

        private static bool ResolveDist(Session session, WorldObject target, string[] args, out Position pos, out string name, bool suppressErrors = false)
        {
            pos = null;
            name = "";
            if (args.Length == 0 || !float.TryParse(args[0], out var dist))
            {
                if (!suppressErrors) session.Network.EnqueueSend(new GameMessageSystemChat("Usage: ... to dist <distance>", ChatMessageType.System));
                return false;
            }
            pos = target.Location.InFrontOf(dist);
            name = $"{args[0]} units forward";
            return true;
        }

        private static bool ResolvePreviousLocation(Session session, WorldObject target, out Position pos, out string name, bool suppressErrors = false)
        {
            name = "previous location";
            if (target is Player p && p.TeleportedCharacter != null)
            {
                pos = new Position(p.TeleportedCharacter);
                return true;
            }

            pos = null;
            if (!suppressErrors) session.Network.EnqueueSend(new GameMessageSystemChat($"{target.Name} has no saved return position.", ChatMessageType.System));
            return false;
        }

        private static bool ResolveType(Session session, WorldObject target, string[] args, out Position pos, out string destName, bool suppressErrors = false)
        {
            destName = "";
            pos = null;
            
            if (args.Length == 0 || !Enum.TryParse(args[0], true, out PositionType typeId))
            {
                 // Try parsing as int if enum parse fails or if input is int
                 if (args.Length > 0 && int.TryParse(args[0], out int intVal))
                 {
                     typeId = (PositionType)intVal;
                 }
                 else
                 {
                     if (!suppressErrors) session.Network.EnqueueSend(new GameMessageSystemChat("Usage: ... to type <id>", ChatMessageType.System));
                     return false;
                 }
            }

            if (target is Player p)
            {
                pos = p.GetPosition(typeId);
            }
            else
            {
                 if (!suppressErrors) session.Network.EnqueueSend(new GameMessageSystemChat("Only players have saved positions.", ChatMessageType.System));
                 return false;
            }

            if (pos == null)
            {
                if (!suppressErrors) session.Network.EnqueueSend(new GameMessageSystemChat($"Player has no saved position for type {typeId}", ChatMessageType.System));
                return false;
            }

            destName = $"saved position {typeId}: {pos}";
            return true;
        }

        private static bool ResolveLocation(Session session, string[] parameters, out Position pos, out string name, bool suppressErrors = false)
        {
            var locStr = string.Join(" ", parameters).Trim('"'); // Remove surrounding quotes if present

            pos = null;
            name = "";

            if (Position.TryParse(locStr, out pos))
            {
                name = $"location {pos}";
                return true;
            }
            
            if (!suppressErrors) session.Network.EnqueueSend(new GameMessageSystemChat("Invalid location string format.", ChatMessageType.System));
            return false;
        }

        private static bool ResolveDungeon(Session session, string[] parameters, out Position pos, out string name, bool suppressErrors = false)
        {
            pos = null;
            name = "";

            if (parameters.Length == 0)
            {
                if (!suppressErrors) session.Network.EnqueueSend(new GameMessageSystemChat("Usage: ... to dungeon <dungeon> [block] [variation]", ChatMessageType.System));
                return false;
            }
            
            var isBlock = true;
            var param = parameters[0];
            uint? variation = null;
            
            if (parameters.Length > 2)
            {
                isBlock = false;
            }
            else if (parameters.Length == 2)
            {
                if (uint.TryParse(parameters[1], out var tempVal))
                {
                    variation = tempVal;
                }
                else
                {
                    isBlock = false;
                }
            }

            uint landblock = 0;
            if (isBlock)
            {
                try
                {
                    landblock = Convert.ToUInt32(param, 16);
                    if (landblock > 0xFFFF)
                        landblock = landblock >> 16;
                }
                catch
                {
                    isBlock = false;
                }
            }

            if (isBlock)
            {
                 using (var ctx = new WorldDbContext())
                 {
                    var query = from weenie in ctx.Weenie
                                join wpos in ctx.WeeniePropertiesPosition on weenie.ClassId equals wpos.ObjectId
                                where weenie.Type == (int)WeenieType.Portal && wpos.PositionType == (int)PositionType.Destination
                                select new
                                {
                                    Weenie = weenie,
                                    Dest = wpos
                                };


                    var filteredQuery = query.Where(i => (i.Dest.ObjCellId >> 16) == landblock);
                    if (variation != null)
                        filteredQuery = filteredQuery.Where(i => i.Dest.VariationId == variation);
                    var dest = filteredQuery.Select(i => i.Dest).FirstOrDefault();
                    if (dest != null)
                    {
                        pos = new Position(dest.ObjCellId, dest.OriginX, dest.OriginY, dest.OriginZ, dest.AnglesX, dest.AnglesY, dest.AnglesZ, dest.AnglesW, false, dest.VariationId);
                        name = $"dungeon {landblock:X4}";
                        return true;
                    }
                 }
                 if (!suppressErrors) session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find dungeon landblock {landblock:X4}", ChatMessageType.System));
                 return false;
            }
            else
            {
                // Name search
                var searchName = string.Join(" ", parameters);
                using (var ctx = new WorldDbContext())
                {
                    var query = from weenie in ctx.Weenie
                                join wstr in ctx.WeeniePropertiesString on weenie.ClassId equals wstr.ObjectId
                                join wpos in ctx.WeeniePropertiesPosition on weenie.ClassId equals wpos.ObjectId
                                where weenie.Type == (int)WeenieType.Portal
                                    && wstr.Type == (int)PropertyString.Name
                                    && wpos.PositionType == (int)PositionType.Destination
                                    && wstr.Value.Contains(searchName)
                                select new
                                {
                                    Name = wstr.Value,
                                    Dest = wpos
                                };

                    var matches = query.ToList();
                    var dungeon = matches.Where(x => x.Name.Equals(searchName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault()
                        ?? matches.Where(x => x.Name.StartsWith(searchName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault()
                        ?? matches.Where(x => x.Name.Contains(searchName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

                    if (dungeon == null)
                    {
                        if (!suppressErrors) session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find dungeon name {searchName}", ChatMessageType.System));
                        return false;
                    }
                    
                    var dest = dungeon.Dest;
                    pos = new Position(dest.ObjCellId, dest.OriginX, dest.OriginY, dest.OriginZ, dest.AnglesX, dest.AnglesY, dest.AnglesZ, dest.AnglesW, false, dest.VariationId);
                    name = $"{dungeon.Name}{(dungeon.Name.EndsWith("Portal") ? " destination" : "")} ({pos})";
                    return true;
                }
            }
        }

        private static bool ResolvePoi(Session session, string[] parameters, out Position pos, out string name, bool suppressErrors = false)
        {
            pos = null;
            name = string.Join(" ", parameters);
            
            var teleportPOI = DatabaseManager.World.GetCachedPointOfInterest(name);
            if (teleportPOI != null)
            {
                var weenie = DatabaseManager.World.GetCachedWeenie(teleportPOI.WeenieClassId);
                if (weenie != null)
                {
                    pos = weenie.GetPosition(PositionType.Destination);
                    if (pos != null) {
                        name = $"{teleportPOI.Name} ({pos})"; 
                        return true;
                    }
                }
            }
            if (!suppressErrors) session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find POI {name}", ChatMessageType.System));
            return false;
        }


        // ==========================================================================================
        // RANDOM TELEPORT COMMANDS
        // ==========================================================================================

        /// <summary>
        /// Portal names containing these strings are excluded from random dungeon selection
        /// (housing, private instances, dev/test areas).
        /// </summary>
        private static readonly HashSet<string> DungeonNameExclusions = new(StringComparer.OrdinalIgnoreCase)
        {
            "House", "Apartment", "Mansion", "Cottage", "Villa", "Penthouse",
            "Hovel", "Test", "Admin", "Dev", "Instance"
        };

        [CommandHandler("telerandom", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 0,
            "Teleport yourself to a random valid location anywhere in the world (outdoor or dungeon).")]
        public static void HandleTeleRandom(Session session, params string[] parameters)
        {
            if (ThreadSafeRandom.Next(0, 1) == 0)
                TeleportToRandomOutdoor(session);
            else
                TeleportToRandomDungeon(session);
        }

        [CommandHandler("telerandomoutside", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 0,
            "Teleport yourself to a random outdoor location anywhere in Dereth.")]
        public static void HandleTeleRandomOutside(Session session, params string[] parameters)
            => TeleportToRandomOutdoor(session);

        [CommandHandler("telerandomdungeon", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 0,
            "Teleport yourself deep inside a random accessible dungeon.")]
        public static void HandleTeleRandomDungeon(Session session, params string[] parameters)
            => TeleportToRandomDungeon(session);

        // ------------------------------------------------------------------------------------------

        private static void TeleportToRandomOutdoor(Session session)
        {
            const int MaxAttempts = 15;

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                // Full Dereth grid: 0x01-0xFE on both axes
                byte lbX = (byte)ThreadSafeRandom.Next(0x01, 0xFE);
                byte lbY = (byte)ThreadSafeRandom.Next(0x01, 0xFE);
                uint cellId = ((uint)lbX << 24) | ((uint)lbY << 16) | 0x0000;

                var lb = LScape.get_landblock(cellId, null);
                if (lb == null) continue;
                if (lb.WaterType == LandDefs.WaterType.EntirelyWater) continue;
                if (lb.IsDungeon) continue;

                // Sample terrain height at the center of the landblock
                float localX = 96.0f, localY = 96.0f;
                float z = lb.GetZ(new System.Numerics.Vector3(localX, localY, 0f)) + 1.0f;

                var dest = new Position(cellId, localX, localY, z, 0f, 0f, 0f, 1f);
                if (!ValidateDestination(session, dest)) continue;

                session.Player.SetPosition(PositionType.TeleportedCharacter, new Position(session.Player.Location));
                session.Player.Teleport(dest);
                session.Player.OnTeleportComplete();

                var coords = dest.GetMapCoordStr();
                session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"Teleporting to random outdoor location: {coords}", ChatMessageType.System));
                return;
            }

            session.Network.EnqueueSend(new GameMessageSystemChat(
                "Could not find a valid outdoor location after several attempts. Try again.", ChatMessageType.System));
        }

        private static void TeleportToRandomDungeon(Session session)
        {
            const int MaxDungeonAttempts = 5;

            // --- Step 1: Build accessible dungeon list from the world DB ---
            // We pull all portal Destination positions that land inside an EnvCell (dungeon interior),
            // then filter out housing/private portals by name.
            List<(string Name, uint DungeonLandblock, Position Entrance)> accessibleDungeons;
            using (var ctx = new WorldDbContext())
            {
                var query = from weenie in ctx.Weenie
                            join wstr in ctx.WeeniePropertiesString   on weenie.ClassId equals wstr.ObjectId
                            join wpos in ctx.WeeniePropertiesPosition on weenie.ClassId equals wpos.ObjectId
                            where weenie.Type        == (int)WeenieType.Portal
                               && wstr.Type         == (int)PropertyString.Name
                               && wpos.PositionType == (int)PositionType.Destination
                               && (wpos.ObjCellId & 0x0000FFFFu) >= 0x0100u  // EnvCell = dungeon interior
                            select new { wstr.Value, wpos };

                accessibleDungeons = query
                    .AsEnumerable()
                    .Where(r => !DungeonNameExclusions.Any(ex =>
                        r.Value.Contains(ex, StringComparison.OrdinalIgnoreCase)))
                    .Select(r => (
                        Name:             r.Value,
                        DungeonLandblock: r.wpos.ObjCellId >> 16,
                        Entrance:         new Position(
                                              r.wpos.ObjCellId,
                                              r.wpos.OriginX, r.wpos.OriginY, r.wpos.OriginZ,
                                              r.wpos.AnglesX, r.wpos.AnglesY, r.wpos.AnglesZ,
                                              r.wpos.AnglesW, false, r.wpos.VariationId)
                    ))
                    .ToList();
            }

            if (accessibleDungeons.Count == 0)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat(
                    "No accessible dungeons found in the database.", ChatMessageType.System));
                return;
            }

            // --- Steps 2–4: Pick dungeon, find deep creature spawn positions ---
            for (int attempt = 0; attempt < MaxDungeonAttempts; attempt++)
            {
                var chosen = accessibleDungeons[ThreadSafeRandom.Next(0, accessibleDungeons.Count - 1)];
                uint blockStart  = chosen.DungeonLandblock << 16;
                uint blockEnd    = blockStart | 0xFFFFu;
                uint deepCellMin = blockStart | 0x0100u;  // skip outdoor cell, EnvCells only

                // Query creature spawn positions inside this dungeon's cells.
                // Project to anonymous type first (EF-safe), then construct Position in memory.
                List<Position> spawnPositions;
                using (var ctx = new WorldDbContext())
                {
                    var rawRows = (from weenie in ctx.Weenie
                                   join wpos in ctx.WeeniePropertiesPosition on weenie.ClassId equals wpos.ObjectId
                                   where weenie.Type == (int)WeenieType.Creature
                                      && wpos.PositionType == (int)PositionType.Location
                                      && wpos.ObjCellId   >= deepCellMin
                                      && wpos.ObjCellId   <= blockEnd
                                   select new
                                   {
                                       wpos.ObjCellId,
                                       wpos.OriginX, wpos.OriginY, wpos.OriginZ,
                                       wpos.AnglesX, wpos.AnglesY, wpos.AnglesZ,
                                       wpos.AnglesW, wpos.VariationId
                                   }).ToList();

                    spawnPositions = rawRows.Select(r => new Position(
                        r.ObjCellId,
                        r.OriginX, r.OriginY, r.OriginZ,
                        r.AnglesX, r.AnglesY, r.AnglesZ,
                        r.AnglesW, false, r.VariationId)).ToList();
                }


                // No creature data — retry with a different dungeon unless this is the last attempt,
                // in which case use the portal entrance so we still land somewhere valid.
                if (spawnPositions.Count == 0)
                {
                    if (attempt < MaxDungeonAttempts - 1)
                        continue;

                    session.Player.SetPosition(PositionType.TeleportedCharacter, new Position(session.Player.Location));
                    session.Player.Teleport(chosen.Entrance);
                    session.Player.OnTeleportComplete();
                    session.Network.EnqueueSend(new GameMessageSystemChat(
                        $"Teleporting to entrance of: {chosen.Name} (landblock 0x{chosen.DungeonLandblock:X4})",
                        ChatMessageType.System));
                    return;
                }

                // Sort all spawn positions by distance from entrance (descending),
                // take the furthest 33%, and pick one at random — this lands us deep inside.
                var sortedByDepth = spawnPositions
                    .Select(p => (Pos: p, DistSq: DungeonDistanceSq(p, chosen.Entrance)))
                    .OrderByDescending(x => x.DistSq)
                    .ToList();

                int deepCount = Math.Max(1, sortedByDepth.Count / 3);
                var dest = sortedByDepth[ThreadSafeRandom.Next(0, deepCount - 1)].Pos;

                session.Player.SetPosition(PositionType.TeleportedCharacter, new Position(session.Player.Location));
                session.Player.Teleport(dest);
                session.Player.OnTeleportComplete();

                session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"Teleporting deep into: {chosen.Name} (landblock 0x{chosen.DungeonLandblock:X4})",
                    ChatMessageType.System));
                return;
            }


            session.Network.EnqueueSend(new GameMessageSystemChat(
                "Could not find a valid dungeon after several attempts. Try again.", ChatMessageType.System));
        }

        /// <summary>
        /// Squared XYZ distance between two positions within the same dungeon landblock.
        /// Using squared distance avoids sqrt and is sufficient for relative ordering.
        /// </summary>
        private static float DungeonDistanceSq(Position a, Position b)
        {
            float dx = a.Pos.X - b.Pos.X;
            float dy = a.Pos.Y - b.Pos.Y;
            float dz = a.Pos.Z - b.Pos.Z;
            return dx * dx + dy * dy + dz * dz;
        }

    }
}
