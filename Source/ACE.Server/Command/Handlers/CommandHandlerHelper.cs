
using System;
using System.Globalization;
using System.Linq;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using log4net;

namespace ACE.Server.Command.Handlers
{
    internal static class CommandHandlerHelper
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// This will determine where a command handler should output to, the console or a client session.<para />
        /// If the session is null, the output will be sent to the console. If the session is not null, and the session.Player is in the world, it will be sent to the session.<para />
        /// Messages sent to the console will be sent using log.Info()
        /// </summary>
        public static void WriteOutputInfo(Session session, string output, ChatMessageType chatMessageType = ChatMessageType.Broadcast)
        {
            if (session != null)
            {
                if (session.State == Network.Enum.SessionState.WorldConnected && session.Player != null)
                    ChatPacket.SendServerMessage(session, output, chatMessageType);
            }
            else
                log.Info(output);
        }

        /// <summary>
        /// This will determine where a command handler should output to, the console or a client session.<para />
        /// If the session is null, the output will be sent to the console. If the session is not null, and the session.Player is in the world, it will be sent to the session.<para />
        /// Messages sent to the console will be sent using log.Debug()
        /// </summary>
        public static void WriteOutputDebug(Session session, string output, ChatMessageType chatMessageType = ChatMessageType.Broadcast)
        {
            if (session != null)
            {
                if (session.State == Network.Enum.SessionState.WorldConnected && session.Player != null)
                    ChatPacket.SendServerMessage(session, output, chatMessageType);
            }
            else
                log.Debug(output);
        }

        /// <summary>
        /// This will determine where a command handler should output to, the console or a client session.<para />
        /// If the session is null, the output will be sent to the console. If the session is not null, and the session.Player is in the world, it will be sent to the session.<para />
        /// Messages sent to the console will be sent using log.Debug()
        /// </summary>
        public static void WriteOutputError(Session session, string output, ChatMessageType chatMessageType = ChatMessageType.Broadcast)
        {
            if (session != null)
            {
                if (session.State == Network.Enum.SessionState.WorldConnected && session.Player != null)
                    ChatPacket.SendServerMessage(session, output, chatMessageType);
            }
            else
                log.Error(output);
        }

        /// <summary>
        /// Returns the last appraised WorldObject
        /// </summary>
        public static WorldObject GetLastAppraisedObject(Session session)
        {
            var targetID = session.Player.RequestedAppraisalTarget;
            if (targetID == null)
            {
                WriteOutputInfo(session, "GetLastAppraisedObject() - no appraisal target");
                return null;
            }

            var target = session.Player.FindObject(targetID.Value, Player.SearchLocations.Everywhere, out _, out _, out _);
            if (target == null)
            {
                WriteOutputInfo(session, $"GetLastAppraisedObject() - couldn't find 0x{targetID:X8}");
                return null;
            }
            return target;
        }

        /// <summary>
        /// Returns the currently selected WorldObject, checking health/mana query targets
        /// before falling back to appraisal targets.
        /// </summary>
        public static WorldObject GetSelected(Session session)
        {
            uint? targetID;
            if (session.Player.HealthQueryTarget.HasValue)
                targetID = session.Player.HealthQueryTarget;
            else if (session.Player.ManaQueryTarget.HasValue)
                targetID = session.Player.ManaQueryTarget;
            else if (session.Player.CurrentAppraisalTarget.HasValue)
                targetID = session.Player.CurrentAppraisalTarget;
            else 
                targetID = session.Player.RequestedAppraisalTarget;

            if (targetID == null) return null;
            return session.Player.FindObject(targetID.Value, Player.SearchLocations.Everywhere, out _, out _, out _);
        }

        /// <summary>
        /// Returns the item the player last selected in their own pack or equipment, or null.
        /// Unlike GetSelected, this ignores the health target: it stays on the last creature clicked
        /// (a monster, pet or player) while the player clicks through their pack, so it would hide the item.
        /// </summary>
        public static WorldObject GetSelectedOwnedItem(Session session)
        {
            var player = session.Player;
            var locations = Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems;

            // RequestedAppraisalTarget is set on every click, so it is the most recent selection.
            foreach (var targetID in new[] { player.RequestedAppraisalTarget, player.CurrentAppraisalTarget, player.ManaQueryTarget })
            {
                if (targetID == null) continue;
                var item = player.FindObject(targetID.Value, locations);
                if (item != null) return item;
            }
            return null;
        }

        /// <summary>
        /// Gets the player to be used as the command target based on the current selection or a specified player name.
        ///
        /// If no player is found, a system chat message is sent to the session indicating the reason. The caller need not send any additional messages.
        ///
        /// Intended use:
        /// ```
        ///     Player target = CommandHandlerHelper.GetPlayerAsCommandTarget(session, string.Join(" ", parameters));
        ///     if (target == null) return;
        ///     ...
        /// ```
        /// </summary>
        /// <param name="session">The session from which to determine the selected player and to send system chat messages if needed.</param>
        /// <param name="playerName">The name of the player to target. If empty, the currently selected player in the session is used.</param>
        /// <param name="fallbackToSelf">A boolean representing whether the command should fall back to the session player if no valid target is found.</param>
        /// <returns>The player to be used as the command target, or null if no suitable player is found.</returns>
        public static Player GetPlayerAsCommandTarget(Session session, string playerName = "", bool fallbackToSelf = false)
        {
            bool playerWasSpecified = !string.IsNullOrWhiteSpace(playerName);

            Player target = null;
            if (playerWasSpecified)
            {
                target = PlayerManager.GetOnlinePlayer(playerName.Trim());
            }
            else
            {
                if (GetSelected(session) is Player p) target = p;
            }

            if (target == null)
            {
                if (playerWasSpecified)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Player {playerName.Trim()} was not found.", ChatMessageType.System));
                    return null;
                }
                if (!fallbackToSelf)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"You must select a player or pass one to the command.", ChatMessageType.System));
                    return null;
                }

                return session.Player;
            }
            return target;
        }

        /// <summary>
        /// The object an admin command acts on (owner 2026-10-19: optional player name or id on the player admin commands).
        /// The arguments after the command's own ones name the target: a character name (case-insensitive, with or without
        /// the + admin prefix, spaces allowed, quotes stripped) is an online player anywhere; an id (decimal, or 0x hex) is an
        /// online player anywhere or an object near the admin. With no such arguments it is the last appraised object, as
        /// before. A target that is not found is reported and null returned - never the last appraised object instead.
        /// In game only: chat commands run on the world thread between landblock ticks, so acting on a player in another
        /// landblock is safe; the server console runs commands on its own thread, so there it is refused.
        /// </summary>
        /// <param name="session">The admin's session (null from the server console - refused)</param>
        /// <param name="parameters">All the command's arguments (CommandManager.ParseCommand has already grouped quoted text)</param>
        /// <param name="ownParameterCount">How many of them are the command's own (property, value, ...) - the rest name the target</param>
        /// <returns>The target, or null (already reported)</returns>
        public static WorldObject GetCommandTarget(Session session, string[] parameters, int ownParameterCount)
        {
            ResolveCommandTarget(session, parameters, ownParameterCount, false, out var target, out _);
            return target;
        }

        /// <summary>
        /// GetCommandTarget, reaching offline characters too (2026-11-20, /setproperty, /getproperty, /propertydump). The same
        /// rules: with no name or id after the command's own arguments it is the last appraised object; an id is an object
        /// near the admin or an online player anywhere; a name is an online player. When that finds nothing, an offline
        /// character with that name or id is returned in <paramref name="offline"/> - its stored record, which the command
        /// reads or changes and saves. Exactly one of the two outs is set when this returns true; false means nothing was
        /// found, and that has been reported. In game only, as GetCommandTarget.
        /// </summary>
        /// <param name="session">The admin's session (null from the server console - refused)</param>
        /// <param name="parameters">All the command's arguments (CommandManager.ParseCommand has already grouped quoted text)</param>
        /// <param name="ownParameterCount">How many of them are the command's own (property, value, ...) - the rest name the target</param>
        /// <param name="target">The object or online player, when that is what was found</param>
        /// <param name="offline">The offline character, when that is what was found</param>
        /// <returns>True when a target was found</returns>
        public static bool TryGetCommandTargetOrOffline(Session session, string[] parameters, int ownParameterCount, out WorldObject target, out OfflinePlayer offline)
        {
            return ResolveCommandTarget(session, parameters, ownParameterCount, true, out target, out offline);
        }

        /// <summary>
        /// The one set of rules behind GetCommandTarget (<paramref name="allowOffline"/> false: an online player or a nearby
        /// object, reported exactly as before) and TryGetCommandTargetOrOffline (true: then an offline character as well).
        /// Reports what it does not find.
        /// </summary>
        private static bool ResolveCommandTarget(Session session, string[] parameters, int ownParameterCount, bool allowOffline, out WorldObject target, out OfflinePlayer offline)
        {
            target = null;
            offline = null;

            if (session?.Player == null)
            {
                WriteOutputInfo(session, "Run this command in game - from the server console it would change a player off the world thread.");
                return false;
            }

            if (parameters.Length <= ownParameterCount)
            {
                target = GetLastAppraisedObject(session);
                return target != null;
            }

            var text = string.Join(" ", parameters, ownParameterCount, parameters.Length - ownParameterCount).Trim();

            // an unclosed quote ("/setproperty PropertyString.Inscription "Gift 1342184765") makes ParseCommand keep the
            // quoted words AND repeat them as separate words - which would read as a target
            if (ownParameterCount > 0 && parameters[ownParameterCount - 1].EndsWith(" " + text, StringComparison.Ordinal))
            {
                WriteOutputInfo(session, "A quote is not closed - nothing done.");
                return false;
            }

            if (TryParseObjectId(text, out var guid))
            {
                if (!allowOffline)
                {
                    target = GetWorldObjectByGuid(session, guid);   // reports when not found
                    return target != null;
                }

                target = session.Player.FindObject(guid, Player.SearchLocations.Everywhere, out _, out _, out _) ?? PlayerManager.GetOnlinePlayer(guid);
                if (target == null)
                    offline = PlayerManager.GetOfflinePlayer(guid);
                if (target == null && offline == null)
                    WriteOutputInfo(session, $"No object, online player or character with id 0x{guid:X8} - nothing done.");
                return target != null || offline != null;
            }

            if (!allowOffline)
            {
                target = PlayerManager.GetOnlinePlayer(text);   // case-insensitive, with or without the + admin prefix
                if (target == null)
                    WriteOutputInfo(session, $"No online player named {text} - nothing done.");
                return target != null;
            }

            var character = FindCharacterByName(text);
            target = character as Player;
            offline = character as OfflinePlayer;
            if (character == null)
                WriteOutputInfo(session, $"No character named {text} - nothing done.");
            return character != null;
        }

        /// <summary>
        /// A character by name, case-insensitive, with or without the + admin prefix: an online player first, then an offline
        /// character that is not pending deletion (PlayerManager.FindByName), then one that is. A deleted character's name can
        /// be taken again, so the live character comes first - the one pending deletion is still found when it is the only one.
        /// </summary>
        private static IPlayer FindCharacterByName(string name)
            => PlayerManager.FindByName(name) ?? PlayerManager.GetOfflinePlayer(name);

        /// <summary>
        /// "offline", or "offline, pending deletion" - how an offline character is labelled in staff command output.
        /// </summary>
        public static string OfflineLabel(IPlayer character)
            => character.IsPendingDeletion ? "offline, pending deletion" : "offline";

        /// <summary>
        /// The character a staff command names (2026-11-20, /bankwipe, /banklock, /bankunlock): the arguments from
        /// <paramref name="firstParameter"/> on are a character name (case-insensitive, with or without the + admin prefix,
        /// spaces allowed) or an id (decimal, or 0x hex). An online player is found first, then an offline character - by name,
        /// one that is not pending deletion before one that is (FindCharacterByName). False, reported, when there is none.
        /// </summary>
        /// <param name="session">The admin's session</param>
        /// <param name="parameters">All the command's arguments</param>
        /// <param name="firstParameter">Where the name or id starts</param>
        /// <param name="character">The character found</param>
        /// <returns>True when a character was found</returns>
        public static bool TryGetCharacter(Session session, string[] parameters, int firstParameter, out IPlayer character)
        {
            character = null;

            if (parameters == null || parameters.Length <= firstParameter)
            {
                WriteOutputInfo(session, "Name a character, by name or by id.");
                return false;
            }

            var text = string.Join(" ", parameters, firstParameter, parameters.Length - firstParameter).Trim();

            if (TryParseObjectId(text, out var guid))
            {
                character = PlayerManager.FindByGuid(guid);   // online, else offline
                if (character == null)
                    WriteOutputInfo(session, $"No character with id 0x{guid:X8} - nothing done.");
                return character != null;
            }

            character = FindCharacterByName(text);
            if (character == null)
                WriteOutputInfo(session, $"No character named {text} - nothing done.");
            return character != null;
        }

        /// <summary>
        /// An object id as an admin types it: all decimal digits, or 0x followed by hex digits
        /// </summary>
        /// <param name="text">The text typed</param>
        /// <param name="guid">The id, when it is one</param>
        /// <returns>True when the text is an id (a character name is never all digits)</returns>
        internal static bool TryParseObjectId(string text, out uint guid)
        {
            guid = 0;
            var hex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            var digits = hex ? text[2..] : text;
            if (digits.Length == 0 || !digits.All(c => hex ? Uri.IsHexDigit(c) : char.IsAsciiDigit(c)))
                return false;
            return uint.TryParse(digits, hex ? NumberStyles.HexNumber : NumberStyles.None, CultureInfo.InvariantCulture, out guid);
        }

        public static WorldObject GetWorldObjectByGuid(Session session, uint guid)
        {
            var target = session.Player.FindObject(guid, Player.SearchLocations.Everywhere, out _, out _, out _);

            // "Everywhere" is the admin's own landblock (+ adjacent) and inventory - an online player anywhere else by id
            // (owner 2026-10-19: /setproperty <prop> <value> <player id>, the player can be anywhere)
            target ??= PlayerManager.GetOnlinePlayer(guid);

            if (target == null)
            {
                WriteOutputInfo(session, $"GetWorldObjectByGuid() - couldn't find 0x{guid:X8}");
                return null;
            }
            return target;
        }
    }
}
