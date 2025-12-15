using ACE.Server.WorldObjects;

namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessagePlayerTeleport : OutboundGameMessage
    {
        public GameMessagePlayerTeleport(Player player)
            : base(OutboundGameMessageOpcode.PlayerTeleport, GameMessageGroup.SmartboxQueue)
        {
            Writer.Write(player.Sequences.GetNextSequence(Sequence.SequenceType.ObjectTeleport));
            Writer.Align();
        }
    }
}
