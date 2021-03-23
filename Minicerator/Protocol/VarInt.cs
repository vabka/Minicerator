using System;

namespace Minicerator.Protocol
{
    public static class VarInt
    {
        public static bool TryRead(ReadOnlySpan<byte> slice, out int value, out int readBytes)
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

                var highBit = nextByte & 0b1000_0000;
                var noMoreBytes = highBit == 0;
                if (noMoreBytes)
                    return true;

                shift += 7;
            }
        }

        public static int GetSize(int value)
        {
            var val = (uint) value;
            var count = 0;
            do
            {
                val >>= 7;
                count++;
            } while (val != 0);

            return count;
        }

        public static int Write(Span<byte> slice, int value)
        {
            var val = (uint) value;
            var written = 0;
            do
            {
                var temp = (byte) (val & 0b01111111);
                val >>= 7;
                if (val != 0)
                {
                    temp |= 0b10000000;
                }

                slice[written++] = temp;
            } while (val != 0);

            return written;
        }
    }

    public static class VarLong
    {
        public static bool TryRead(ReadOnlySpan<byte> slice, out long value, out int readBytes)
        {
            readBytes = 0;
            value = 0;
            var shift = 0;
            while (true)
            {
                if (readBytes >= slice.Length)
                    return false;

                var nextByte = slice[readBytes++];

                long meanBits = nextByte & 0b0111_1111;
                value |= meanBits << shift;

                var highBit = nextByte & 0b1000_0000;
                var noMoreBytes = highBit == 0;
                if (noMoreBytes)
                    return true;

                shift += 7;
            }
        }

        public static int GetSize(long value)
        {
            var val = (uint) value;
            var count = 0;
            do
            {
                val >>= 7;
                count++;
            } while (val != 0);

            return count;
        }

        public static int Write(Span<byte> slice, long value)
        {
            var val = (ulong) value;
            var written = 0;
            do
            {
                var temp = (byte) (val & 0b0111_1111);
                val >>= 7;
                if (val != 0)
                {
                    temp |= 0b1000_0000;
                }

                slice[written++] = temp;
            } while (val != 0);

            return written;
        }
    }
}
