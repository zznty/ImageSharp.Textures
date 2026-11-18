namespace ImageSharp.Textures.Tests.TestUtilities;

/// <summary>
///     A read-only stream that reports itself as non-seekable.
/// </summary>
internal sealed class NonSeekableStream : Stream
{
    private readonly Stream inner;

    public NonSeekableStream(Stream inner) => this.inner = inner;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => this.inner.Read(buffer, offset, count);

    public override int Read(Span<byte> buffer) => this.inner.Read(buffer);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
