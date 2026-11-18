namespace ImageSharp.Textures;

/// <summary>
///     Represents a texture resource: the mip levels, surfaces and metadata of one texture container.
/// </summary>
public sealed class Texture : IDisposable
{
    private const int HeaderPeekLength = 16;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Texture" /> class without any mip levels.
    ///     Decoders populate the levels through <see cref="AddMipLevel" />.
    /// </summary>
    /// <param name="configuration">The texture configuration used to allocate surface payloads.</param>
    /// <param name="metadata">The texture metadata.</param>
    internal Texture(TextureConfiguration configuration, TextureMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(metadata);

        this.Configuration = configuration;
        this.Metadata = metadata;
        this.MipLevels = new TextureMipLevelCollection();
    }

    /// <summary>
    ///     Gets the texture metadata.
    /// </summary>
    public TextureMetadata Metadata { get; }

    /// <summary>
    ///     Gets the ordered mip levels in the texture.
    /// </summary>
    public TextureMipLevelCollection MipLevels { get; }

    /// <summary>
    ///     Gets the texture configuration used to allocate surface payloads.
    /// </summary>
    internal TextureConfiguration Configuration { get; }

    /// <summary>
    ///     Releases resources owned by the texture.
    /// </summary>
    public void Dispose() => this.MipLevels.Dispose();

    /// <summary>
    ///     Loads a texture from the specified path, resolving the decoder from the file extension or, when the
    ///     extension is unknown, from the container header.
    /// </summary>
    /// <param name="path">The path to the texture file.</param>
    /// <returns>The loaded texture.</returns>
    public static Texture Load(string path) => Load(TextureConfiguration.Default, path);

    /// <summary>
    ///     Loads a texture from the specified path using the given texture configuration.
    /// </summary>
    /// <param name="configuration">The texture configuration to use.</param>
    /// <param name="path">The path to the texture file.</param>
    /// <returns>The loaded texture.</returns>
    internal static Texture Load(TextureConfiguration configuration, string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        ITextureDecoder decoder = ResolveDecoder(configuration, stream, path);
        return decoder.Decode(configuration, stream);
    }

    /// <summary>
    ///     Loads a texture from the specified path using the given decoder.
    /// </summary>
    /// <param name="path">The path to the texture file.</param>
    /// <param name="decoder">The decoder to use.</param>
    /// <returns>The loaded texture.</returns>
    public static Texture Load(string path, ITextureDecoder decoder)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(decoder);

        TextureConfiguration configuration = TextureConfiguration.Default;
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return decoder.Decode(configuration, stream);
    }

    /// <summary>
    ///     Loads a texture from the specified stream, detecting the decoder from the container header.
    /// </summary>
    /// <param name="stream">The stream containing the encoded texture.</param>
    /// <returns>The loaded texture.</returns>
    public static Texture Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        TextureConfiguration configuration = TextureConfiguration.Default;
        ITextureDecoder decoder = ResolveDecoder(configuration, stream, null);
        return decoder.Decode(configuration, stream);
    }

    /// <summary>
    ///     Loads a texture from the specified stream using the given decoder.
    /// </summary>
    /// <param name="stream">The stream containing the encoded texture.</param>
    /// <param name="decoder">The decoder to use.</param>
    /// <returns>The loaded texture.</returns>
    public static Texture Load(Stream stream, ITextureDecoder decoder)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(decoder);

        return decoder.Decode(TextureConfiguration.Default, stream);
    }

    /// <summary>
    ///     Asynchronously loads a texture from the specified path, resolving the decoder from the file extension
    ///     or, when the extension is unknown, from the container header.
    /// </summary>
    /// <param name="path">The path to the texture file.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The loaded texture.</returns>
    public static async Task<Texture> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        TextureConfiguration configuration = TextureConfiguration.Default;
        using FileStream stream = OpenAsyncReadStream(path);
        ITextureDecoder decoder = ResolveDecoder(configuration, stream, path);
        return await decoder.DecodeAsync(configuration, stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Asynchronously loads a texture from the specified path using the given decoder.
    /// </summary>
    /// <param name="path">The path to the texture file.</param>
    /// <param name="decoder">The decoder to use.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The loaded texture.</returns>
    public static async Task<Texture> LoadAsync(
        string path,
        ITextureDecoder decoder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(decoder);

        TextureConfiguration configuration = TextureConfiguration.Default;
        using FileStream stream = OpenAsyncReadStream(path);
        return await decoder.DecodeAsync(configuration, stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Asynchronously loads a texture from the specified stream, detecting the decoder from the container header.
    /// </summary>
    /// <param name="stream">The stream containing the encoded texture.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The loaded texture.</returns>
    public static async Task<Texture> LoadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        TextureConfiguration configuration = TextureConfiguration.Default;
        ITextureDecoder decoder = ResolveDecoder(configuration, stream, null);
        return await decoder.DecodeAsync(configuration, stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Asynchronously loads a texture from the specified stream using the given decoder.
    /// </summary>
    /// <param name="stream">The stream containing the encoded texture.</param>
    /// <param name="decoder">The decoder to use.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The loaded texture.</returns>
    public static Task<Texture> LoadAsync(
        Stream stream,
        ITextureDecoder decoder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(decoder);

        return decoder.DecodeAsync(TextureConfiguration.Default, stream, cancellationToken);
    }

    /// <summary>
    ///     Saves the texture to the specified path, resolving the encoder from the file extension.
    /// </summary>
    /// <param name="path">The destination path.</param>
    public void Save(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        ITextureEncoder encoder = this.ResolveEncoderForPath(path);
        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Encode(this, stream);
    }

    /// <summary>
    ///     Saves the texture to the specified path using the given encoder.
    /// </summary>
    /// <param name="path">The destination path.</param>
    /// <param name="encoder">The encoder to use.</param>
    public void Save(string path, ITextureEncoder encoder)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(encoder);

        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Encode(this, stream);
    }

    /// <summary>
    ///     Saves the texture to the specified stream using the encoder registered for the given format.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="format">The texture format to use.</param>
    public void Save(Stream stream, ITextureFormat format)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(format);

        this.GetEncoder(format).Encode(this, stream);
    }

    /// <summary>
    ///     Saves the texture to the specified stream using the given encoder.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="encoder">The encoder to use.</param>
    public void Save(Stream stream, ITextureEncoder encoder)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(encoder);

        encoder.Encode(this, stream);
    }

    /// <summary>
    ///     Asynchronously saves the texture to the specified path, resolving the encoder from the file extension.
    /// </summary>
    /// <param name="path">The destination path.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous save operation.</returns>
    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        ITextureEncoder encoder = this.ResolveEncoderForPath(path);
        using FileStream stream = OpenAsyncWriteStream(path);
        await encoder.EncodeAsync(this, stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Asynchronously saves the texture to the specified path using the given encoder.
    /// </summary>
    /// <param name="path">The destination path.</param>
    /// <param name="encoder">The encoder to use.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous save operation.</returns>
    public async Task SaveAsync(
        string path,
        ITextureEncoder encoder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(encoder);

        using FileStream stream = OpenAsyncWriteStream(path);
        await encoder.EncodeAsync(this, stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Asynchronously saves the texture to the specified stream using the encoder registered for the given format.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="format">The texture format to use.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous save operation.</returns>
    public Task SaveAsync(Stream stream, ITextureFormat format, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(format);

        return this.GetEncoder(format).EncodeAsync(this, stream, cancellationToken);
    }

    /// <summary>
    ///     Asynchronously saves the texture to the specified stream using the given encoder.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="encoder">The encoder to use.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous save operation.</returns>
    public Task SaveAsync(Stream stream, ITextureEncoder encoder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(encoder);

        return encoder.EncodeAsync(this, stream, cancellationToken);
    }

    /// <summary>
    ///     Appends a new, empty mip level with the given dimensions. Container decoders call this once per mip level
    ///     and then append one surface per layer, face or depth slice through
    ///     <see cref="TextureMipLevel.CreateSurface" />.
    /// </summary>
    /// <param name="width">The width of surfaces at this mip level.</param>
    /// <param name="height">The height of surfaces at this mip level.</param>
    /// <param name="depth">The depth of this mip level; 1 for flat, array and cubemap textures.</param>
    /// <returns>The appended mip level.</returns>
    internal TextureMipLevel AddMipLevel(int width, int height, int depth)
    {
        TextureMipLevel mipLevel = new(this.Configuration, width, height, depth);
        this.MipLevels.AppendMipLevel(mipLevel);
        return mipLevel;
    }

    private static FileStream OpenAsyncReadStream(string path)
        => new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

    private static FileStream OpenAsyncWriteStream(string path)
        => new(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);

    private static ITextureDecoder ResolveDecoder(TextureConfiguration configuration, Stream stream, string? path)
    {
        if (path is not null)
        {
            ITextureFormat? extensionFormat = configuration.Formats.FindFormatByFileExtension(Path.GetExtension(path));
            if (extensionFormat is not null)
            {
                return configuration.Formats.GetDecoder(extensionFormat);
            }
        }

        if (!stream.CanSeek)
        {
            throw new ArgumentException(
                "A non-seekable stream requires an explicit texture decoder because the container format cannot be detected from the header.",
                nameof(stream));
        }

        long position = stream.Position;
        Span<byte> header = stackalloc byte[HeaderPeekLength];
        int read = stream.Read(header);
        stream.Position = position;

        ITextureFormat? detectedFormat = configuration.Formats.DetectFormat(header[..read]);
        if (detectedFormat is null)
        {
            string origin = path is null ? "the stream" : $"'{path}'";
            throw new UnknownTextureFormatException(
                $"The decoder for {origin} could not be determined from the file extension or the container header.");
        }

        return configuration.Formats.GetDecoder(detectedFormat);
    }

    private ITextureEncoder GetEncoder(ITextureFormat format)
    {
        this.Configuration.Formats.TryGetEncoder(format, out ITextureEncoder? encoder);
        return encoder ?? throw new NotSupportedException(
            $"No encoder is registered for the texture format '{format.Name}'.");
    }

    private ITextureEncoder ResolveEncoderForPath(string path)
    {
        string extension = Path.GetExtension(path);
        string normalized = extension.StartsWith('.') ? extension[1..] : extension;
        List<ITextureFormat> matchingFormats = [];

        foreach (ITextureFormat format in this.Configuration.Formats.Formats)
        {
            if (MatchesExtension(format, normalized))
            {
                matchingFormats.Add(format);
            }
        }

        if (matchingFormats.Count == 0)
        {
            throw new UnknownTextureFormatException(
                $"The encoder for '{path}' could not be determined from the file extension.");
        }

        List<ITextureEncoder> encoders = [];

        foreach (ITextureFormat format in matchingFormats)
        {
            if (this.Configuration.Formats.TryGetEncoder(format, out ITextureEncoder? encoder) && encoder is not null)
            {
                encoders.Add(encoder);
            }
        }

        return encoders.Count switch
        {
            0 => throw new NotSupportedException(
                $"No encoder is registered for the '{extension}' file extension."),
            > 1 => throw new InvalidOperationException(
                $"More than one texture encoder is registered for the '{extension}' file extension; pass an explicit encoder."),
            _ => encoders[0]
        };
    }

    private static bool MatchesExtension(ITextureFormat format, string normalizedExtension)
    {
        foreach (string candidate in format.FileExtensions)
        {
            string value = candidate.StartsWith('.') ? candidate[1..] : candidate;

            if (value.Equals(normalizedExtension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
