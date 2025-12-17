using ACE.Entity.Enum;

namespace ACE.Server.Network.Handlers
{
    public static class FriendsOldHandler
    {
        public static void FriendsOld(ClientMessage message, Session session)
        {
            ChatPacket.SendServerMessage(session, "That command is not used in the emulator.", ChatMessageType.Broadcast);
        }
    }
}
