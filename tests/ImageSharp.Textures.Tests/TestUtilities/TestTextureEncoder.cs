namespace ImageSharp.Textures.Tests.TestUtilities;

/// <summary>
///     An encoder that writes a single marker byte and records how often it was used.
/// </summary>
internal sealed class TestTextureEncoder : ITextureEncoder
{
    public const byte Marker = 0x5A;

    public TestTextureEncoder(ITextureFormat format) => this.Format = format;

    public int EncodeCount { get; private set; }

    public ITextureFormat Format { get; }

    public void Encode(Texture texture, Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.EncodeCount++;
        stream.WriteByte(Marker);
    }

    public async Task EncodeAsync(Texture texture, Stream stream, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        this.Encode(texture, stream, cancellationToken);
    }
}
