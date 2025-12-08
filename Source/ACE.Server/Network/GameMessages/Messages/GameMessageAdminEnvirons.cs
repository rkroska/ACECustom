using ACE.Entity.Enum;

namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessageAdminEnvirons : OutboundGameMessage
    {
        public GameMessageAdminEnvirons(Session session, EnvironChangeType environChange = EnvironChangeType.Clear)
            : base(OutboundGameMessageOpcode.AdminEnvirons, GameMessageGroup.UIQueue)
        {
            Writer.Write((uint)environChange);
        }
    }
}
