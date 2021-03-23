using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Minicerator.Protocol.Packets;

namespace Minicerator.CLI
{
    public class Reader
    {
        private readonly ChannelWriter<RawPacket> _channelWriter;
        private readonly Socket _socket;

        public Reader(ChannelWriter<RawPacket> channelWriter, Socket socket)
        {
            _channelWriter = channelWriter;
            _socket = socket;
        }

        public async Task StartReading(CancellationToken cancellationToken = default)
        {
            var pipe = new Pipe();
            var fill = StartFillPipe(pipe.Writer, cancellationToken);
            var read = StartReadPipe(pipe.Reader, cancellationToken);
            await fill;
            await read;
        }

        private async ValueTask StartReadPipe(PipeReader pipeReader, CancellationToken cancellationToken)
        {
            var processor = new PacketsProcessor(_channelWriter);
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var result = await pipeReader.ReadAsync(cancellationToken);

                    var length = await processor.ProcessBytesAsync(result.Buffer, cancellationToken);
                    pipeReader.AdvanceTo(result.Buffer.GetPosition(length));
                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
            }
            catch (SocketException e)
            {
                await pipeReader.CompleteAsync(e);
                _channelWriter.Complete(e);
            }
        }


        private async ValueTask StartFillPipe(PipeWriter pipeWriter, CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int readBytes;
                try
                {
                    readBytes = await _socket.ReceiveAsync(pipeWriter.GetMemory(), SocketFlags.None, cancellationToken);
                }
                catch (SocketException e)
                {
                    await pipeWriter.CompleteAsync(e);
                    return;
                }

                if (readBytes == 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
                    continue;
                }

                pipeWriter.Advance(readBytes);

                var result = await pipeWriter.FlushAsync(cancellationToken);

                if (result.IsCompleted)
                    break;
            }

            await pipeWriter.CompleteAsync();
        }
    }

    public class PacketsProcessor
    {
        private enum ReaderState
        {
            ReadingLength,
            ReadingPackageId,
            ReadingContent
        }

        private VarIntReadingContext _length;
        private VarIntReadingContext _packetId;
        private ContentReadingContext _content;
        private ReaderState _readerState = ReaderState.ReadingLength;

        private readonly ChannelWriter<RawPacket> _channelWriter;

        public PacketsProcessor(ChannelWriter<RawPacket> channelWriter) => _channelWriter = channelWriter;

        public async ValueTask<int> ProcessBytesAsync(ReadOnlySequence<byte> bytesSequence,
            CancellationToken cancellationToken)
        {
            var processed = 0;
            while (true)
            {
                switch (_readerState)
                {
                    case ReaderState.ReadingLength:
                    {
                        processed += _length.Accept(bytesSequence.Slice(processed));
                        if (_length.Done)
                            _readerState = ReaderState.ReadingPackageId;
                        else
                            return processed;
                    }
                        break;
                    case ReaderState.ReadingPackageId:
                    {
                        processed += _packetId.Accept(bytesSequence.Slice(processed));
                        if (_packetId.Done)
                        {
                            _readerState = ReaderState.ReadingContent;
                            _content = new ContentReadingContext(_length.Value - _packetId.Length);
                        }
                        else
                            return processed;
                    }
                        break;
                    case ReaderState.ReadingContent:
                    {
                        processed += _content.Accept(bytesSequence.Slice(processed));
                        if (_content.Done)
                        {
                            var rawPacket = new RawPacket
                            {
                                Id = _packetId.Value,
                                Content = _content.Value
                            };
                            await _channelWriter.WriteAsync(rawPacket, cancellationToken);
                            PrepareForNextPacket();
                            _readerState = ReaderState.ReadingLength;
                        }
                        else
                            return processed;
                    }
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        private void PrepareForNextPacket()
        {
            _length = default;
            _packetId = default;
        }

        private struct ContentReadingContext
        {
            private readonly Memory<byte> _value;
            private Memory<byte> _window;

            public ContentReadingContext(int length)
            {
                _value = new Memory<byte>(new byte[length]);
                _window = _value;
            }

            public ReadOnlyMemory<byte> Value => _value;
            public int Length => _value.Length;
            public bool Done => _window.Length == 0;

            public int Accept(ReadOnlySequence<byte> bytesSequence)
            {
                if (Done)
                    return 0;

                var processed = 0;
                foreach (var segment in bytesSequence)
                {
                    var slice = segment.Length <= _window.Length
                        ? segment
                        : segment[.._window.Length];
                    slice.CopyTo(_window);
                    _window = _window[slice.Length..];
                    processed += slice.Length;
                    if (Done)
                        return processed;
                }

                return processed;
            }
        }

        private struct VarIntReadingContext
        {
            private int _shift;
            public int Length { get; private set; }
            public int Value { get; private set; }
            public bool Done { get; private set; }

            public int Accept(ReadOnlySequence<byte> bytesSequence)
            {
                if (Done) return 0;
                var processed = 0;
                foreach (var segment in bytesSequence)
                {
                    foreach (var nextByte in segment.Span)
                    {
                        Length++;
                        var meanBits = nextByte & 0b0111_1111;
                        Value |= meanBits << _shift;
                        processed++;

                        var highBit = nextByte & 0b1000_0000;
                        var noMoreBytes = highBit == 0;
                        if (noMoreBytes)
                        {
                            Done = true;
                            return processed;
                        }

                        _shift += 7;
                    }
                }

                return processed;
            }
        }
    }
}
