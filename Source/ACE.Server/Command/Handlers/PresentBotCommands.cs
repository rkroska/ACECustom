using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// /presentbot - the Present Bot (owner 2026-10-01): spam-opens Mine, Mine, Mine! presents on the caller's OWN
    /// character through the real use path and reports uses vs actual prize rolls. Test-shard only (present_bot_enabled).
    /// </summary>
    public static class PresentBotCommands
    {
        private const string Usage = "start [same|nearest] | crowd [stand-ins] [events] | stop | status | math [samples]\n" +
                                     "crowd = hidden stand-ins (default 40) each spamming presents over back-to-back events (default 20), stand in Marketplace v5; " +
                                     "same = hammer one present until it is gone (default); nearest = cycle every present within 6 m; " +
                                     "math = the server's own prize pick N times vs the database odds (no clicks, no rewards)";

        [CommandHandler("presentbot", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0,
            "Present Bot: spam-opens Mine, Mine, Mine! presents on your own character and reports uses vs prize rolls.",
            Usage)]
        public static void HandlePresentBot(Session session, params string[] parameters)
        {
            var player = session.Player;
            var verb = parameters.Length > 0 ? parameters[0].ToLowerInvariant() : "status";

            switch (verb)
            {
                case "status":
                    PresentBot.Status(player);
                    return;

                case "stop":
                    if (PresentBot.IsCrowdRunning(player))
                        PresentBot.StopCrowd(player, "/presentbot stop");
                    else if (PresentBot.IsRunning(player))
                        PresentBot.Stop(player, "/presentbot stop");
                    else
                        Say(session, "not running.");
                    return;

                case "crowd":
                    if (!ServerConfig.present_bot_enabled.Value)
                    {
                        Say(session, "the bot is off on this server (present_bot_enabled). /modifybool present_bot_enabled true - test shard only.");
                        return;
                    }
                    var bots = PresentBot.CrowdDefaultBots;
                    var events = PresentBot.CrowdDefaultEvents;
                    if ((parameters.Length > 1 && !int.TryParse(parameters[1], out bots)) || (parameters.Length > 2 && !int.TryParse(parameters[2], out events)))
                    {
                        Say(session, "crowd: /presentbot crowd [stand-ins 1-100] [events 1-100]");
                        return;
                    }
                    PresentBot.StartCrowd(player, bots, events);
                    return;

                case "math":
                    // no clicks, no rewards, no event needed - works with the bot switched off
                    var samples = 100_000;
                    if (parameters.Length > 1 && !int.TryParse(parameters[1].Replace(",", ""), out samples))
                    {
                        Say(session, $"math: '{parameters[1]}' is not a number. /presentbot math [1000-{PresentBot.MathMaxSamples}]");
                        return;
                    }
                    PresentBot.RunMath(player, samples);
                    return;

                case "start":
                    if (!ServerConfig.present_bot_enabled.Value)
                    {
                        Say(session, "the bot is off on this server (present_bot_enabled). /modifybool present_bot_enabled true - test shard only.");
                        return;
                    }
                    var mode = parameters.Length > 1 ? parameters[1].ToLowerInvariant() : "same";
                    if (mode != "same" && mode != "nearest")
                    {
                        Say(session, $"unknown mode '{mode}'. {Usage}");
                        return;
                    }
                    PresentBot.Start(player, mode);
                    return;

                default:
                    Say(session, Usage);
                    return;
            }
        }

        private static void Say(Session session, string text)
        {
            session?.Network.EnqueueSend(new GameMessageSystemChat("[PresentBot] " + text, ChatMessageType.Broadcast));
        }
    }
}
