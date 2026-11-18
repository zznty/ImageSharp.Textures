namespace ImageSharp.Textures.Formats.Ktx2;

/// <summary>
///     Decodes Khronos Texture 2 (KTX2) containers into <see cref="Texture" /> instances.
/// </summary>
public sealed class Ktx2TextureDecoder : ITextureDecoder
{
    /// <inheritdoc />
    public ITextureFormat Format => Ktx2Format.Instance;

    /// <inheritdoc />
    public Texture Decode(
        TextureConfiguration configuration,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(stream);

        return new Ktx2DecoderCore(configuration).Decode(stream, cancellationToken);
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

        return Ktx2DecoderCore.Identify(stream);
    }
}
