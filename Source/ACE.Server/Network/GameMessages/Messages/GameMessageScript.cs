using ACE.Entity;
using ACE.Entity.Enum;

namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessageScript : OutboundGameMessage
    {
        public GameMessageScript(ObjectGuid guid, PlayScript scriptId, float speed = 1.0f)
            : base(OutboundGameMessageOpcode.PlayEffect, GameMessageGroup.SmartboxQueue)
        {
            Writer.WriteGuid(guid);
            Writer.Write((uint)scriptId);
            Writer.Write(speed);
        }
    }
}
