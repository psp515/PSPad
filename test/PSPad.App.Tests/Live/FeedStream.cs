using System.Text;
using System.Threading.Channels;

namespace PSPad.App.Tests.Live;

public sealed class FeedStream : Stream
{
    readonly Channel<byte[]> _chunks = Channel.CreateUnbounded<byte[]>();
    byte[] _current = [];
    int _offset;

    public void Send(string text) => _chunks.Writer.TryWrite(Encoding.UTF8.GetBytes(text));

    public void End() => _chunks.Writer.TryComplete();

    public void Fail() => _chunks.Writer.TryComplete(new IOException("connection reset"));

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_offset == _current.Length)
        {
            if (!await _chunks.Reader.WaitToReadAsync(cancellationToken) || !_chunks.Reader.TryRead(out var next))
            {
                return 0;
            }

            _current = next;
            _offset = 0;
        }

        var count = Math.Min(buffer.Length, _current.Length - _offset);
        _current.AsMemory(_offset, count).CopyTo(buffer);
        _offset += count;
        return count;
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
