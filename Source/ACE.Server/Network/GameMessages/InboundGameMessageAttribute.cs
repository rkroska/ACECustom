using System;
using ACE.Server.Network.Enum;

namespace ACE.Server.Network.GameMessages
{
    [AttributeUsage(AttributeTargets.Method)]
    public class InboundGameMessageAttribute : Attribute
    {
        public InboundGameMessageOpcode Opcode { get; }
        public SessionState State { get; }

        public InboundGameMessageAttribute(InboundGameMessageOpcode opcode, SessionState state)
        {
            Opcode = opcode;
            State  = state;
        }
    }
}
