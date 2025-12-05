namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessageDDDErrorMessage : OutboundGameMessage
    {
        public GameMessageDDDErrorMessage(uint resourceType, uint dataId, uint errorType)
            : base(OutboundGameMessageOpcode.DDD_ErrorMessage, GameMessageGroup.DatabaseQueue)
        {
            Writer.Write(resourceType);
            Writer.Write(dataId);
            Writer.Write(errorType);
        }
    }
}
