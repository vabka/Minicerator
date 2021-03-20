namespace Minicerator.Protocol.Packets
{
    [Packet(0x00)]
    public class Handshake : IPacket
    {
        [PacketField(0, Vary = true)] public int ProtocolVersion { get; init; }

        [PacketField(1, MaxStringLength = 255)]
        public string ServerAddress { get; init; } = "localhost";

        [PacketField(2)] public ushort ServerPort { get; init; } = 25565;

        [PacketField(3, Vary = true)] public State NextState { get; init; } = State.Status;

        public enum State
        {
            Status = 1,
            Login = 2
        }
    }
}
