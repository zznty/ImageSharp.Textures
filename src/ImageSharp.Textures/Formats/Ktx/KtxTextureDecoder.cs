namespace ImageSharp.Textures.Formats.Ktx;

/// <summary>
///     Decodes Khronos Texture (KTX v1) containers into <see cref="Texture" /> instances.
/// </summary>
public sealed class KtxTextureDecoder : ITextureDecoder
{
    /// <inheritdoc />
    public ITextureFormat Format => KtxFormat.Instance;

    /// <inheritdoc />
    public Texture Decode(
        TextureConfiguration configuration,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(stream);

        return new KtxDecoderCore(configuration).Decode(stream, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Texture> DecodeAsync(
        TextureConfiguration configuration,
        Stream stream,
        CancellationToken cancellationToken = default)
        => Task.FromResult(this.Decode(configuration, stream, cancellationToken));

    /// <inheritdoc />
    public TextureInfo Identify(
        TextureConfiguration configuration,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();

        return KtxDecoderCore.Identify(stream);
    }
}
