namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessageServerName : OutboundGameMessage
    {
        public GameMessageServerName(string serverName, int currentConnections = 0, int maxConnections = -1)
            : base(OutboundGameMessageOpcode.ServerName, GameMessageGroup.UIQueue)
        {
            Writer.Write(currentConnections);
            Writer.Write(maxConnections);
            Writer.WriteString16L(serverName);
        }
    }
}
