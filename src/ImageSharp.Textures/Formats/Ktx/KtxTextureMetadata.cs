namespace ImageSharp.Textures.Formats.Ktx;

/// <summary>
///     Stores the native metadata of a KTX v1 container so that it can be round-tripped and converted to other
///     containers through <see cref="TextureConnectingMetadata" />.
/// </summary>
public sealed class KtxTextureMetadata : ITextureFormatMetadata<KtxTextureMetadata>, ITextureFaceLayoutMetadata
{
    /// <summary>
    ///     Gets or sets the byte order declared by the file.
    /// </summary>
    public KtxEndianness Endianness { get; set; } = KtxEndianness.LittleEndian;

    /// <summary>
    ///     Gets or sets the <c>glType</c> header field; zero for compressed textures.
    /// </summary>
    public uint GlType { get; set; }

    /// <summary>
    ///     Gets or sets the <c>glTypeSize</c> header field.
    /// </summary>
    public uint GlTypeSize { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the <c>glFormat</c> header field; zero for compressed textures.
    /// </summary>
    public uint GlFormat { get; set; }

    /// <summary>
    ///     Gets or sets the <c>glInternalFormat</c> header field.
    /// </summary>
    public uint GlInternalFormat { get; set; }

    /// <summary>
    ///     Gets or sets the <c>glBaseInternalFormat</c> header field.
    /// </summary>
    public uint GlBaseInternalFormat { get; set; }

    /// <summary>
    ///     Gets or sets the declared <c>numberOfArrayElements</c> header field; zero for non-array textures.
    /// </summary>
    public uint ArrayElementCount { get; set; }

    /// <summary>
    ///     Gets or sets the declared <c>numberOfFaces</c> header field.
    /// </summary>
    public uint FaceCount { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the declared <c>numberOfMipmapLevels</c> header field, preserving a declared zero.
    /// </summary>
    public uint MipLevelCount { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the raw key/value data block, including every size field and padding byte. The block is
    ///     re-emitted byte for byte when the texture is saved as KTX.
    /// </summary>
    public byte[] KeyValueData { get; set; } = [];

    /// <summary>
    ///     Gets or sets the parsed key/value pairs. The list is derived from <see cref="KeyValueData" /> when a
    ///     file is decoded and is not used when encoding.
    /// </summary>
    public IReadOnlyList<KtxKeyValuePair> KeyValuePairs { get; set; } = [];

    /// <inheritdoc />
    int ITextureFaceLayoutMetadata.FaceCount => this.FaceCount == 6 ? 6 : 1;

    /// <summary>
    ///     Creates KTX metadata from container-neutral connecting metadata.
    /// </summary>
    /// <param name="metadata">The connecting texture metadata.</param>
    /// <returns>The KTX metadata.</returns>
    /// <exception cref="NotSupportedException">The texel type has no KTX v1 representation.</exception>
    public static KtxTextureMetadata FromTextureConnectingMetadata(TextureConnectingMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (!KtxTexelFormats.TryGetTexelFormat(metadata.TexelTypeInfo, out TexelFormat texelFormat))
        {
            throw new NotSupportedException("The connecting texel type has no supported KTX v1 representation.");
        }

        KtxInternalFormat internalFormat = KtxTexelFormats.GetInternalFormat(texelFormat);

        return new KtxTextureMetadata
        {
            GlType = internalFormat.GlType,
            GlTypeSize = internalFormat.GlTypeSize,
            GlFormat = internalFormat.GlFormat,
            GlInternalFormat = internalFormat.GlInternalFormat,
            GlBaseInternalFormat = internalFormat.GlBaseInternalFormat
        };
    }

    /// <summary>
    ///     Converts the metadata to a <see cref="TextureConnectingMetadata" /> instance.
    /// </summary>
    /// <returns>The connecting texture metadata.</returns>
    /// <exception cref="NotSupportedException">The declared internal format has no connecting representation.</exception>
    public TextureConnectingMetadata ToTextureConnectingMetadata()
    {
        if (!KtxTexelFormats.TryGetTexelFormat(this.GlInternalFormat, out TexelFormat texelFormat))
        {
            throw new NotSupportedException(
                $"The KTX v1 glInternalFormat 0x{this.GlInternalFormat:X} has no supported texel type.");
        }

        return new TextureConnectingMetadata { TexelTypeInfo = KtxTexelFormats.GetTexelTypeInfo(texelFormat) };
    }

    /// <summary>
    ///     Creates a deep copy of the metadata.
    /// </summary>
    /// <returns>The cloned metadata.</returns>
    public KtxTextureMetadata DeepClone()
    {
        KtxKeyValuePair[] pairs = new KtxKeyValuePair[this.KeyValuePairs.Count];
        for (int i = 0; i < pairs.Length; i++)
        {
            pairs[i] = this.KeyValuePairs[i].Clone();
        }

        return new KtxTextureMetadata
        {
            Endianness = this.Endianness,
            GlType = this.GlType,
            GlTypeSize = this.GlTypeSize,
            GlFormat = this.GlFormat,
            GlInternalFormat = this.GlInternalFormat,
            GlBaseInternalFormat = this.GlBaseInternalFormat,
            ArrayElementCount = this.ArrayElementCount,
            FaceCount = this.FaceCount,
            MipLevelCount = this.MipLevelCount,
            KeyValueData = (byte[])this.KeyValueData.Clone(),
            KeyValuePairs = pairs
        };
    }

    /// <inheritdoc />
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();
}
