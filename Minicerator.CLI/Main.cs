using System;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Minicerator.CLI;
using Minicerator.Protocol;
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
var channel = Channel.CreateUnbounded<RawPacket>();
var reader = new Reader(channel.Writer, socket);
var cts = new CancellationTokenSource();
var task = reader.StartReading(cts.Token);
await socket.SendAsync(PrepareBuffer(serializer, handshake), SocketFlags.None);
var poller = Task.Run(async () =>
{
    while (true)
    {
        cts.Token.ThrowIfCancellationRequested();
        await socket.SendAsync(PrepareBuffer(serializer, new QueryStatus()), SocketFlags.None, cts.Token);
        await Task.Delay(1000);
    }
}, cts.Token);
await foreach (var packet in channel.Reader.ReadAllAsync())
{
    if (packet.Id == 0x00)
    {
        var scope = packet.Content;
        if (VarInt.TryRead(scope.Span, out var jsonLength, out var jsonLengthLength))
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

cts.Cancel();
try
{
    await Task.WhenAll(task, poller);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Exited");
}
catch (Exception e)
{
    Console.WriteLine(e);
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
