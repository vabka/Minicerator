using System;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Minicerator.CLI;
using Minicerator.Protocol.Packets;

var serializer = new PacketSerializer();
using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
await socket.ConnectAsync("127.0.0.1", 25565);
var handshake = new Handshake
{
    ProtocolVersion = 754,
    ServerAddress = "localhost",
    ServerPort = 25565,
    NextState = Handshake.State.Status
};
await socket.SendAsync(PrepareBuffer(serializer, handshake), SocketFlags.None);
await socket.SendAsync(PrepareBuffer(serializer, new QueryStatus()), SocketFlags.None);
var buf = new Memory<byte>(new byte[256]);
var readBytes = await socket.ReceiveAsync(buf, SocketFlags.None);
{
    var scope = buf[..readBytes];
    if (TryReadVarInt(scope.Span, out var packetLength, out var packetLengthLength))
    {
        var totalLength = packetLength + packetLengthLength;
        if (scope.Length >= totalLength)
        {
            scope = scope.Slice(packetLengthLength, packetLength);
            if (TryReadVarInt(scope.Span, out var packetId, out var packetIdLength))
            {
                scope = scope[packetIdLength..];
                if (packetId == 0x00)
                {
                    if (TryReadVarInt(scope.Span, out var jsonLength, out var jsonLengthLength))
                    {
                        scope = scope[jsonLengthLength..];
                        if (scope.Length != jsonLength)
                            throw new InvalidOperationException("Invalid json length");
                        var status = JsonSerializer.Deserialize<ServerStatus>(scope.Span);
                        if (status != null)
                        {
                            Console.WriteLine($"Server: {status.Description.Text}");
                            Console.WriteLine($"Version: {status.Version.Name} ({status.Version.Protocol})");
                            Console.WriteLine($"Players: {status.Players.Online}/{status.Players.Max}");
                        }
                    }
                }
            }
        }
    }
}

static bool TryReadVarInt(ReadOnlySpan<byte> slice, out int value, out int readBytes)
{
    readBytes = 0;
    value = 0;
    var shift = 0;
    while (true)
    {
        if (readBytes >= slice.Length)
            return false;

        var nextByte = slice[readBytes++];

        var meanBits = nextByte & 0b0111_1111;
        value |= meanBits << shift;

        var highBit = nextByte & ~0b1000_0000;
        var noMoreBytes = highBit == 0;
        if (noMoreBytes)
            return true;

        shift += 7;
    }
}

static ReadOnlyMemory<byte> PrepareBuffer<T>(PacketSerializer packetSerializer, T packetBody) where T : IPacket
{
    if (!packetSerializer.Validate(packetBody))
        throw new InvalidOperationException("Invalid packet");

    var size = packetSerializer.GetBufferSize(packetBody);
    var buffer = new Memory<byte>(new byte[size.BufferSize]);
    packetSerializer.Serialize(buffer.Span, packetBody, size.BodySize);
    return buffer;
}
