namespace ImageSharp.Textures.Formats.Dds;

/// <summary>
///     Encodes <see cref="Texture" /> instances into DirectDraw Surface (DDS) containers.
/// </summary>
public sealed class DdsTextureEncoder : ITextureEncoder
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DdsTextureEncoder" /> class with the default options.
    /// </summary>
    public DdsTextureEncoder()
        : this(new DdsEncodingOptions())
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DdsTextureEncoder" /> class.
    /// </summary>
    /// <param name="options">The encoding options.</param>
    public DdsTextureEncoder(DdsEncodingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.Options = options;
    }

    /// <summary>
    ///     Gets the options used by the encoder.
    /// </summary>
    public DdsEncodingOptions Options { get; }

    /// <inheritdoc />
    public ITextureFormat Format => DdsFormat.Instance;

    /// <inheritdoc />
    public void Encode(Texture texture, Stream stream, CancellationToken cancellationToken = default)
        => new DdsEncoderCore(this.Options).Encode(texture, stream, cancellationToken);

    /// <inheritdoc />
    public Task EncodeAsync(Texture texture, Stream stream, CancellationToken cancellationToken = default)
    {
        this.Encode(texture, stream, cancellationToken);
        return Task.CompletedTask;
    }
}
