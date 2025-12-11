namespace ACE.Server.Network.GameMessages
{
    public abstract class OutboundGameMessage
    {
        public OutboundGameMessageOpcode Opcode { get; private set; }

        public System.IO.MemoryStream Data { get; private set; }

        public GameMessageGroup Group { get; private set; }

        protected System.IO.BinaryWriter Writer { get; private set; }

        protected OutboundGameMessage(OutboundGameMessageOpcode opCode, GameMessageGroup group)
        {
            Opcode = opCode;

            Group = group;

            Data = new System.IO.MemoryStream();

            Writer = new System.IO.BinaryWriter(Data);

            if (Opcode != OutboundGameMessageOpcode.None)
                Writer.Write((uint)Opcode);
        }
    }
}
