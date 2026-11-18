namespace ImageSharp.Textures.Formats.Ktx;

/// <summary>
///     Encodes <see cref="Texture" /> instances as Khronos Texture (KTX v1) containers.
/// </summary>
public sealed class KtxTextureEncoder : ITextureEncoder
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="KtxTextureEncoder" /> class with default options.
    /// </summary>
    public KtxTextureEncoder()
        : this(new KtxEncodingOptions())
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="KtxTextureEncoder" /> class.
    /// </summary>
    /// <param name="options">The encoder options.</param>
    public KtxTextureEncoder(KtxEncodingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.Options = options;
    }

    /// <summary>
    ///     Gets the encoder options.
    /// </summary>
    public KtxEncodingOptions Options { get; }

    /// <inheritdoc />
    public ITextureFormat Format => KtxFormat.Instance;

    /// <inheritdoc />
    public void Encode(Texture texture, Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(stream);

        new KtxEncoderCore(this.Options).Encode(texture, stream, cancellationToken);
    }

    /// <inheritdoc />
    public Task EncodeAsync(Texture texture, Stream stream, CancellationToken cancellationToken = default)
    {
        this.Encode(texture, stream, cancellationToken);
        return Task.CompletedTask;
    }
}
