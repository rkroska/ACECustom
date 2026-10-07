using System;
using System.Globalization;
using System.Linq;

using ACE.Common.Extensions;
using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Command.Handlers
{
    public static class DisciplineCommands
    {
        /// <summary>The longest sentence /jail &lt;time&gt; gives.</summary>
        private static readonly TimeSpan MaxJailTime = TimeSpan.FromDays(7);

        [CommandHandler("jail", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld,
            "Sends a player to jail.",
            "Usage: /jail [playername] [time]\nIf no name is provided, the currently selected player is used.\n"
            + "time (optional, last): 90s, 30m, 2h, 1d or a plain number of minutes - 1 minute to 7 days. Without it, the default sentence.\n"
            + "On a player already in jail: a sentence that ends LATER extends theirs; one that ends sooner changes nothing (/jailbreak first to shorten one).")]
        public static void HandleJail(Session session, params string[] parameters)
        {
            // the time, when given, is the LAST word: names can hold spaces, never digits
            TimeSpan? jailTime = null;
            var nameParts = parameters;
            if (parameters.Length > 0 && TryParseJailTime(parameters[^1], out var parsed, out var timeError))
            {
                if (timeError != null)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, timeError, ChatMessageType.Broadcast);
                    return;
                }
                jailTime = parsed;
                nameParts = parameters.Take(parameters.Length - 1).ToArray();
            }

            Player target = CommandHandlerHelper.GetPlayerAsCommandTarget(session, string.Join(" ", nameParts));
            if (target == null) return;
            target.SendToJail(jailTime);
            var howLong = jailTime.HasValue ? $" for {jailTime.Value.GetFriendlyLongString()}" : "";
            PlayerManager.BroadcastToAuditChannel(session.Player, $"[Jail] Player {target.Name} was sent to jail{howLong} by {session.Player.Name}");
        }

        /// <summary>
        /// "90s" / "30m" / "2h" / "1d" / "45" (minutes) -> a sentence. False when the word is not a time at all (then it is part
        /// of the player name). True with <paramref name="error"/> set when it IS a time but out of range (1 minute - 7 days).
        /// </summary>
        private static bool TryParseJailTime(string word, out TimeSpan time, out string error)
        {
            time = default;
            error = null;
            if (string.IsNullOrEmpty(word))
                return false;

            var unit = char.ToLowerInvariant(word[^1]);
            var digits = char.IsDigit(unit) ? word : word[..^1];
            if (digits.Length == 0 || !digits.All(char.IsDigit))
                return false;
            if (!char.IsDigit(unit) && unit != 's' && unit != 'm' && unit != 'h' && unit != 'd')
                return false;

            if (!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n > 1_000_000)
            {
                error = $"Jail time {word} is too long - the most is {MaxJailTime.TotalDays:0} days.";
                return true;
            }

            time = unit switch
            {
                's' => TimeSpan.FromSeconds(n),
                'h' => TimeSpan.FromHours(n),
                'd' => TimeSpan.FromDays(n),
                _ => TimeSpan.FromMinutes(n),   // 'm' or a plain number
            };
            if (time < TimeSpan.FromMinutes(1) || time > MaxJailTime)
                error = $"Jail time {word} is out of range - 1 minute to {MaxJailTime.TotalDays:0} days (90s, 30m, 2h, 1d or minutes).";
            return true;
        }

        [CommandHandler("jailbreak", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld,
            "Releases a player from jail.",
            "Usage: /jailbreak [playername]\nIf no name is provided, the currently selected player is used.")]
        public static void HandleJailbreak(Session session, params string[] parameters)
        {
            Player target = CommandHandlerHelper.GetPlayerAsCommandTarget(session, string.Join(" ", parameters));
            if (target == null) return;

            if (!target.IsInJail())
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Player {target.Name} is not currently in jail.", ChatMessageType.System));
                return;
            }

            target.ReleaseFromJail();
            PlayerManager.BroadcastToAuditChannel(session.Player, $"[Jail] Player {target.Name} was released from jail by {session.Player.Name}");
        }

        [CommandHandler("ucmcheck", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, "Initiates a UCM check on the selected player.")]
        public static void HandleUCMCheck(Session session, params string[] parameters)
        {
            Player target = CommandHandlerHelper.GetPlayerAsCommandTarget(session, string.Join(" ", parameters));
            if (target == null) return;

            if (target.UCMChecker.IsCheckInProgress())
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"{target.Name} is already undergoing a UCM check.", ChatMessageType.System));
                return;
            }

            bool started = target.UCMChecker.Start();
            if (started)
            {
                PlayerManager.BroadcastToAuditChannel(session.Player, $"Admin {session.Player.Name} initiated a UCM check on {target.Name}.");
            }
            else
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"UCM check on {target.Name} failed to start.", ChatMessageType.System));
            }
        }
    }
}
