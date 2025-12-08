using ACE.Entity;
using ACE.Entity.Enum;

namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessageSound : OutboundGameMessage
    {
        public GameMessageSound(ObjectGuid guid, Sound soundId, float volume = 1.0f)
            : base(OutboundGameMessageOpcode.Sound, GameMessageGroup.SmartboxQueue)
        {
            Writer.WriteGuid(guid);
            Writer.Write((uint)soundId);
            Writer.Write(volume);
        }
    }
}
