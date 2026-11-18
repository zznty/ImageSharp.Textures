using ImageSharp.Textures.Formats.Ktx2.Enums;
using ImageSharp.Textures.Formats.Ktx;

namespace ImageSharp.Textures.Formats.Ktx2;

/// <summary>
///     Stores the native metadata of a KTX2 container so that it can be round-tripped and converted to other
///     containers through <see cref="TextureConnectingMetadata" />.
/// </summary>
public sealed class Ktx2TextureMetadata : ITextureFormatMetadata<Ktx2TextureMetadata>, ITextureFaceLayoutMetadata
{
    /// <summary>
    ///     Gets or sets the <c>vkFormat</c> header field.
    /// </summary>
    public VkFormat VkFormat { get; set; } = VkFormat.VK_FORMAT_UNDEFINED;

    /// <summary>
    ///     Gets or sets the <c>typeSize</c> header field.
    /// </summary>
    public uint TypeSize { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the declared <c>layerCount</c> header field; zero for non-array textures.
    /// </summary>
    public uint LayerCount { get; set; }

    /// <summary>
    ///     Gets or sets the declared <c>faceCount</c> header field.
    /// </summary>
    public uint FaceCount { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the declared <c>levelCount</c> header field, preserving a declared zero.
    /// </summary>
    public uint LevelCount { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the declared <c>supercompressionScheme</c> header field.
    /// </summary>
    public uint SupercompressionScheme { get; set; }

    /// <summary>
    ///     Gets or sets the raw key/value data block, including every size field and padding byte. The block is
    ///     re-emitted byte for byte when the texture is saved as KTX2.
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
    ///     Creates KTX2 metadata from container-neutral connecting metadata.
    /// </summary>
    /// <param name="metadata">The connecting texture metadata.</param>
    /// <returns>The KTX2 metadata.</returns>
    /// <exception cref="NotSupportedException">The texel type has no KTX2 representation.</exception>
    public static Ktx2TextureMetadata FromTextureConnectingMetadata(TextureConnectingMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (!Ktx2TexelFormats.TryGetTexelFormat(metadata.TexelTypeInfo, out TexelFormat texelFormat))
        {
            throw new NotSupportedException("The connecting texel type has no supported KTX2 representation.");
        }

        return new Ktx2TextureMetadata { VkFormat = Ktx2TexelFormats.GetVkFormat(texelFormat) };
    }

    /// <summary>
    ///     Converts the metadata to a <see cref="TextureConnectingMetadata" /> instance.
    /// </summary>
    /// <returns>The connecting texture metadata.</returns>
    /// <exception cref="NotSupportedException">The declared Vulkan format has no connecting representation.</exception>
    public TextureConnectingMetadata ToTextureConnectingMetadata()
    {
        if (!Ktx2TexelFormats.TryGetTexelFormat(this.VkFormat, out TexelFormat texelFormat))
        {
            throw new NotSupportedException(
                $"The KTX2 vkFormat {(uint)this.VkFormat} has no supported texel type.");
        }

        return new TextureConnectingMetadata { TexelTypeInfo = Ktx2TexelFormats.GetTexelTypeInfo(texelFormat) };
    }

    /// <summary>
    ///     Creates a deep copy of the metadata.
    /// </summary>
    /// <returns>The cloned metadata.</returns>
    public Ktx2TextureMetadata DeepClone()
    {
        KtxKeyValuePair[] pairs = new KtxKeyValuePair[this.KeyValuePairs.Count];
        for (int i = 0; i < pairs.Length; i++)
        {
            pairs[i] = this.KeyValuePairs[i].Clone();
        }

        return new Ktx2TextureMetadata
        {
            VkFormat = this.VkFormat,
            TypeSize = this.TypeSize,
            LayerCount = this.LayerCount,
            FaceCount = this.FaceCount,
            LevelCount = this.LevelCount,
            SupercompressionScheme = this.SupercompressionScheme,
            KeyValueData = (byte[])this.KeyValueData.Clone(),
            KeyValuePairs = pairs
        };
    }

    /// <inheritdoc />
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();
}
