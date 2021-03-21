using System;
using System.Buffers.Binary;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Unicode;

namespace Minicerator.Protocol.Packets
{
    public struct PacketBufferSize
    {
        public int BufferSize { get; init; }
        public int BodySize { get; init; }
    }

    public class PacketSerializer
    {
        private static readonly UTF8Encoding utf8 = new();

        // codegen pls
        // TODO compression
        public bool Validate<T>(T packet) where T : IPacket =>
            packet switch
            {
                Handshake {ServerAddress: var host} =>
                    host.Length <= 255,
                QueryStatus => true,
                _ => false
            };

        public PacketBufferSize GetBufferSize<T>(T packet) where T : IPacket
        {
            var bodySize = packet switch
            {
                Handshake {ProtocolVersion: var pv, ServerAddress: var host, NextState: var nextState} =>
                    GetVarIntSize(0x0)
                    + GetVarIntSize(pv)
                    + GetStringSize(host)
                    + 2
                    + GetVarIntSize((int) nextState),
                QueryStatus => GetVarIntSize(0x0),
                _ => throw new ArgumentOutOfRangeException(nameof(packet), null, null)
            };
            return new PacketBufferSize
            {
                BodySize = bodySize,
                BufferSize = bodySize + GetVarIntSize(bodySize)
            };
        }

        public void Serialize<T>(Span<byte> buffer, T packet, int bodySize) where T : IPacket
        {
            buffer = buffer[WriteVarInt(buffer, bodySize)..];
            switch (packet)
            {
                case Handshake handshake:
                {
                    buffer = buffer[WriteVarInt(buffer, 0x00)..];
                    buffer = buffer[WriteVarInt(buffer, handshake.ProtocolVersion)..];
                    buffer = buffer[Write(buffer, handshake.ServerAddress)..];
                    buffer = buffer[Write(buffer, handshake.ServerPort)..];
                    buffer = buffer[WriteVarInt(buffer, (int) handshake.NextState)..];
                }
                    break;
                case QueryStatus:
                {
                    buffer = buffer[WriteVarInt(buffer, 0x00)..];
                }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(packet), packet.GetType(), null);
            }

            if (buffer.Length != 0)
                throw new InvalidOperationException("Not all data written, or buffer too large");
        }

        private int Write<T>(Span<byte> span, T value)
        {
            var written = 0;
            switch (value)
            {
                case string str:
                    var size = utf8.GetByteCount(str);
                    written += WriteVarInt(span, size);
                    span = span[written..];
                    written += utf8.GetBytes(str, span);
                    break;
                case ushort n:
                    BinaryPrimitives.WriteUInt16LittleEndian(span, n);
                    written = 2;
                    break;
            }

            return written;
        }

        private int GetStringSize(ReadOnlySpan<char> str)
        {
            var byteCount = utf8.GetByteCount(str);
            var stringSizeSize = GetVarIntSize(byteCount);
            return stringSizeSize + byteCount;
        }

        private static int GetVarIntSize(int value) => VarInt.GetSize(value);

        private static int WriteVarInt(Span<byte> slice, int value) => VarInt.Write(slice, value);
    }
}
