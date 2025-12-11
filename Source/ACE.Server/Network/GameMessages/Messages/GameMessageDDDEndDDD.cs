namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessageDDDEndDDD : OutboundGameMessage
    {
        public GameMessageDDDEndDDD()
            : base(OutboundGameMessageOpcode.DDD_EndDDD, GameMessageGroup.DatabaseQueue)
        {
        }
    }
}
