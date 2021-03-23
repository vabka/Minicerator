using System;

namespace Minicerator.Protocol.Packets
{
    public struct RawPacket
    {
        public int Id { get; init; }
        public ReadOnlyMemory<byte> Content { get; init; }
    }
}