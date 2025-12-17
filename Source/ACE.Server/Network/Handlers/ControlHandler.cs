namespace ACE.Server.Network.Handlers
{
    public static class ControlHandler
    {
        public static void ControlResponse(ClientMessage message, Session session)
        {
            var itemGuid = message.Payload.ReadUInt32();
            session.Player.HandleActionForceObjDescSend(itemGuid);
        }
    }
}
