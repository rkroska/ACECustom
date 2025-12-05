using ACE.Entity;

namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessagePlayerKilled : OutboundGameMessage
    {
        public GameMessagePlayerKilled(string deathMessage, ObjectGuid victimId, ObjectGuid killerId)
            : base(OutboundGameMessageOpcode.PlayerKilled, GameMessageGroup.UIQueue)
        {
            // player broadcasts when they die, including to self
            Writer.WriteString16L(deathMessage);
            Writer.WriteGuid(victimId);
            Writer.WriteGuid(killerId);
        }
    }
}
