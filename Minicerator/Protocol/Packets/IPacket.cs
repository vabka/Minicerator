using System;

namespace Minicerator.Protocol.Packets
{
    public interface IPacket
    {
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public class PacketAttribute : Attribute
    {
        public PacketAttribute(int id)
        {
            Id = id;
        }

        public int Id { get; }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public class PacketFieldAttribute : Attribute
    {
        public PacketFieldAttribute(int order)
        {
            Order = order;
        }

        public int Order { get; }
        public int MaxStringLength { get; init; }
        public bool Vary { get; init; } = false;
    }
}
