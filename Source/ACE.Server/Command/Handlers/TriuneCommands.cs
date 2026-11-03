using ACE.Entity.Enum;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>/triune - the player's Triune Weave, this week's kills and pace, and when the week turns (TriuneEarning.cs).</summary>
    public static class TriuneCommands
    {
        [CommandHandler("triune", AccessLevel.Player, CommandHandlerFlag.RequiresWorld,
            "Shows your Triune Weave, this week's kills toward it and when the week turns.", "/triune")]
        public static void HandleTriune(Session session, params string[] parameters)
        {
            var player = session?.Player;
            if (player == null)
                return;

            // one message per line: the client is not relied on to break a line inside one message
            foreach (var line in TriuneEarning.StatusLines(player))
                player.SendMessage(line, ChatMessageType.Broadcast);
        }
    }
}
