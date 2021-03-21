using System;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Minicerator.CLI
{
    public struct RawPacket
    {
        public int Id { get; init; }
        public ReadOnlyMemory<byte> Content { get; init; }
    }

    public class Reader
    {
        private enum ReaderState
        {
            ReadingLength,
            ReadingPackageId,
            ReadingContent
        }

        private readonly ChannelWriter<RawPacket> _channel;
        private readonly Socket _socket;

        public Reader(ChannelWriter<RawPacket> channel, Socket socket)
        {
            _channel = channel;
            _socket = socket;
        }

        public async Task StartReading(CancellationToken cancellationToken = default)
        {
            var pipe = new Pipe();
            var fill = StartFillPipe(pipe.Writer, cancellationToken);
            var read = StartReadPipe(pipe.Reader, cancellationToken);
            await Task.WhenAll(fill, read);
        }

        private async Task StartReadPipe(PipeReader pipeReader, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private async Task StartFillPipe(PipeWriter pipeWriter, CancellationToken cancellationToken)
        {
            var state = ReaderState.ReadingLength;
            
        }
    }
}
