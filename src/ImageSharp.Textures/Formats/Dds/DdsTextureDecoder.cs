namespace ImageSharp.Textures.Formats.Dds;

/// <summary>
///     Decodes DirectDraw Surface (DDS) containers into <see cref="Texture" /> instances.
/// </summary>
public sealed class DdsTextureDecoder : ITextureDecoder
{
    /// <inheritdoc />
    public ITextureFormat Format => DdsFormat.Instance;

    /// <inheritdoc />
    public Texture Decode(
        TextureConfiguration configuration,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(stream);

        return new DdsDecoderCore(configuration).Decode(stream, cancellationToken);
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

        return DdsDecoderCore.Identify(stream);
    }
}
