using ACE.Server.WorldObjects;

namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessageCreateObject : OutboundGameMessage
    {
        /// <summary>Which object this creates, for SentObjectLog (what a client was last sent before it stopped loading).</summary>
        public uint ObjectGuid { get; }
        public string ObjectName { get; }
        public uint WeenieClassId { get; }

        public GameMessageCreateObject(WorldObject worldObject, bool adminvision = false, bool adminnodraw = false)
            : base(OutboundGameMessageOpcode.ObjectCreate, GameMessageGroup.SmartboxQueue)
        {
            ObjectGuid = worldObject.Guid.Full;
            ObjectName = worldObject.Name;
            WeenieClassId = worldObject.WeenieClassId;

            worldObject.SerializeCreateObject(Writer, adminvision, adminnodraw);
        }
    }
}
