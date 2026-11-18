namespace ImageSharp.Textures.Formats.Ktx2;

/// <summary>
///     Encodes <see cref="Texture" /> instances as Khronos Texture 2 (KTX2) containers.
/// </summary>
public sealed class Ktx2TextureEncoder : ITextureEncoder
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="Ktx2TextureEncoder" /> class with default options.
    /// </summary>
    public Ktx2TextureEncoder()
        : this(new Ktx2EncodingOptions())
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Ktx2TextureEncoder" /> class.
    /// </summary>
    /// <param name="options">The encoder options.</param>
    public Ktx2TextureEncoder(Ktx2EncodingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.Options = options;
    }

    /// <summary>
    ///     Gets the encoder options.
    /// </summary>
    public Ktx2EncodingOptions Options { get; }

    /// <inheritdoc />
    public ITextureFormat Format => Ktx2Format.Instance;

    /// <inheritdoc />
    public void Encode(Texture texture, Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(stream);

        new Ktx2EncoderCore(this.Options).Encode(texture, stream, cancellationToken);
    }

    /// <inheritdoc />
    public Task EncodeAsync(Texture texture, Stream stream, CancellationToken cancellationToken = default)
    {
        this.Encode(texture, stream, cancellationToken);
        return Task.CompletedTask;
    }
}
