using System;

using ACE.Entity.Enum;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// QB Deeds (owner 2026-10-22): /deeds and /deeds list = the player's QB, ON / OFF and every tier with what it gives;
    /// /deeds on | off = every Deeds bonus on / off (saved, at once); /rebuff = the 10k QB tier's one-command full rebuff
    /// (/buff is the staff command). See Player_Deeds.cs.
    /// </summary>
    public static class DeedsCommands
    {
        [CommandHandler("rebuff", AccessLevel.Player, CommandHandlerFlag.RequiresWorld,
            "Deeds (10,000 QB): casts all your known level 8 buffs at once.", "/rebuff")]
        public static void HandleRebuff(Session session, params string[] parameters)
        {
            session?.Player?.HandleDeedsRebuff();
        }

        [CommandHandler("deeds", AccessLevel.Player, CommandHandlerFlag.RequiresWorld,
            "Shows your QB and every Deeds tier. /deeds on or /deeds off turns the Deeds bonuses on or off.",
            Usage)]
        public static void HandleDeeds(Session session, params string[] parameters)
        {
            var player = session?.Player;
            if (player == null)
                return;

            var verb = parameters == null || parameters.Length == 0 ? "list" : parameters[0].Trim().ToLowerInvariant();

            switch (verb)
            {
                case "list":
                    player.ShowDeedsList();
                    break;
                case "on":
                    player.SetDeedsEnabled(true);
                    break;
                case "off":
                    player.SetDeedsEnabled(false);
                    break;
                case "rebuff":
                    player.HandleDeedsRebuff();
                    break;
                default:
                    CommandHandlerHelper.WriteOutputInfo(session, "Usage: " + Usage);
                    break;
            }
        }

        private const string Usage = "/deeds [list|on|off]  (/deeds or /deeds list: your QB and every tier; on / off: all Deeds bonuses)";
    }
}
