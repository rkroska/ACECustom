using System;
using System.IO;

using ACE.Server.Network.GameMessages;

using log4net;

namespace ACE.Server.Network
{
    internal class MessageFragment
    {
        private static readonly ILog packetLog = LogManager.GetLogger(System.Reflection.Assembly.GetEntryAssembly(), "Packets");

        public GameMessage Message { get; private set; }

        public uint Sequence { get; set; }

        public ushort Index { get; set; }

        public ushort Count { get; set; }

        public int DataLength => (int)Message.Data.Length;

        public int DataRemaining { get; private set; }

        public int NextSize
        {
            get
            {
                var dataSize = DataRemaining;
                if (dataSize > PacketFragment.MaxFragmentDataSize)
                    dataSize = PacketFragment.MaxFragmentDataSize;
                return PacketFragmentHeader.HeaderSize + dataSize;
            }
        }

        public int TailSize => PacketFragmentHeader.HeaderSize + (DataLength % PacketFragment.MaxFragmentDataSize);

        public bool TailSent { get; private set; }

        public MessageFragment(GameMessage message, uint sequence)
        {
            Message = message;
            DataRemaining = DataLength;
            Sequence = sequence;
            Count = (ushort)(Math.Ceiling((double)DataLength / PacketFragment.MaxFragmentDataSize));
            Index = 0;
            if (Count == 1)
                TailSent = true;
            packetLog.DebugFormat($"Sequence {sequence}, count {Count}, DataRemaining {DataRemaining}");
        }

        public ServerPacketFragment GetTailFragment()
        {
            var index = (ushort)(Count - 1);
            TailSent = true;
            return CreateServerFragment(index);
        }

        public ServerPacketFragment GetNextFragment()
        {
            return CreateServerFragment(Index++);
        }

        private ServerPacketFragment CreateServerFragment(ushort index)
        {
            packetLog.DebugFormat($"Creating ServerFragment for index {index}");
            if (index >= Count)
            {
                packetLog.Error($"Passed index {index} is greater then computed count {Count}");
                return null;
            }
                

            var position = index * PacketFragment.MaxFragmentDataSize;
            if (position > DataLength)
            {
                packetLog.Error($"Passed index {index} computes to invalid position size, datalength: {DataLength}");
                return null;
            }

            if (DataRemaining <= 0)
            {
                packetLog.Error("There is no data remaining");
                return null;
            }

            var dataToSend = DataLength - position;
            if (dataToSend > PacketFragment.MaxFragmentDataSize)
                dataToSend = PacketFragment.MaxFragmentDataSize;

            if (DataRemaining < dataToSend)
            {
                packetLog.Error("More data to send then data remaining!");
                return null;
            }

            // Read data starting at position reading dataToSend bytes
            Message.Data.Seek(position, SeekOrigin.Begin);
            byte[] data = new byte[dataToSend];
            Message.Data.Read(data, 0, dataToSend);

            // Build ServerPacketFragment structure
            ServerPacketFragment fragment = new ServerPacketFragment(data);
            fragment.Header.Sequence = Sequence;
            fragment.Header.Id = 0x80000000;
            fragment.Header.Count = Count;
            fragment.Header.Index = index;
            fragment.Header.Queue = (ushort)Message.Group;

            DataRemaining -= dataToSend;
            packetLog.DebugFormat($"Done creating ServerFragment for index {index}. After reading {dataToSend} DataRemaining {DataRemaining}");
            return fragment;
        }
    }
}
