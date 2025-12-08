using ACE.Server.Network.GameMessages;
using System.Collections.Generic;

namespace ACE.Server.Network
{

    internal class NetworkBundle
    {
        private bool propChanged;

        public bool NeedsSending => propChanged || messages.Count > 0;

        public int MessageCount => messages.Count;

        private readonly Queue<OutboundGameMessage> messages = new Queue<OutboundGameMessage>();

        private float clientTime = -1f;
        public float ClientTime
        {
            get => clientTime;
            set
            {
                clientTime = value;
                propChanged = true;
            }
        }

        private bool timeSync;
        public bool TimeSync
        {
            get => timeSync;
            set
            {
                timeSync = value;
                propChanged = true;
            }
        }

        private bool ackSeq;
        public bool SendAck
        {
            get => ackSeq;
            set
            {
                ackSeq = value;
                propChanged = true;
            }
        }

        public bool EncryptedChecksum { get; set; }


        public void Enqueue(OutboundGameMessage message)
        {
            messages.Enqueue(message);
        }

        public OutboundGameMessage Dequeue()
        {
            return messages.Dequeue();
        }
    }
}
