namespace ImageSharp.Textures.Tests.TestUtilities;

/// <summary>
///     A decoder that produces an empty texture and records how often it was used.
/// </summary>
internal sealed class TestTextureDecoder : ITextureDecoder
{
    public TestTextureDecoder(ITextureFormat format) => this.Format = format;

    public int DecodeCount { get; private set; }

    public int IdentifyCount { get; private set; }

    public ITextureFormat Format { get; }

    public Texture Decode(
        TextureConfiguration configuration,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.DecodeCount++;
        return new Texture(configuration, new TextureMetadata { DecodedTextureFormat = this.Format });
    }

    public async Task<Texture> DecodeAsync(
        TextureConfiguration configuration,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return this.Decode(configuration, stream, cancellationToken);
    }

    public TextureInfo Identify(
        TextureConfiguration configuration,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.IdentifyCount++;
        return new TextureInfo(1, 1, 1, 1, 1, TexelFormat.R8G8B8A8_UNorm);
    }
}
