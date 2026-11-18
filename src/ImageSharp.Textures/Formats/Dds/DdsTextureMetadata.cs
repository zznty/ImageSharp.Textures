using ImageSharp.Textures.Formats.Dds.Enums;

namespace ImageSharp.Textures.Formats.Dds;

/// <summary>
///     Stores the native metadata of a DDS container so that it can be round-tripped and converted to other
///     containers through <see cref="TextureConnectingMetadata" />.
/// </summary>
public sealed class DdsTextureMetadata : ITextureFormatMetadata<DdsTextureMetadata>, ITextureFaceLayoutMetadata
{
    /// <summary>
    ///     Gets or sets the DXGI format read from the DX10 header, or <see cref="Enums.DxgiFormat.Unknown" /> when the
    ///     file used a legacy header.
    /// </summary>
    public DxgiFormat DxgiFormat { get; set; } = DxgiFormat.Unknown;

    /// <summary>
    ///     Gets or sets the legacy four character code, or <see cref="DdsFourCc.None" /> when the file used a
    ///     DX10 header or an uncompressed pixel format.
    /// </summary>
    public DdsFourCc FourCc { get; set; } = DdsFourCc.None;

    /// <summary>
    ///     Gets or sets a value indicating whether the file carried a <c>DDS_HEADER_DXT10</c> extension header.
    /// </summary>
    public bool HasDx10Header { get; set; }

    /// <summary>
    ///     Gets or sets the resource dimension from the DX10 header, or
    ///     <see cref="D3d10ResourceDimension.Unknown" /> when the file used a legacy header.
    /// </summary>
    public D3d10ResourceDimension ResourceDimension { get; set; } = D3d10ResourceDimension.Unknown;

    /// <summary>
    ///     Gets or sets the resource misc flags from the DX10 header.
    /// </summary>
    public D3d10ResourceMiscFlags ResourceMiscFlags { get; set; } = D3d10ResourceMiscFlags.Unknown;

    /// <summary>
    ///     Gets or sets the number of array elements, or the number of cubes for a cube map.
    /// </summary>
    public uint ArraySize { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the number of mip levels declared by the file.
    /// </summary>
    public uint MipMapCount { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the declared volume depth; 1 for flat, array and cube map textures.
    /// </summary>
    public uint Depth { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the alpha mode declared by <c>DDS_HEADER_DXT10.miscFlags2</c>.
    /// </summary>
    public DdsAlphaMode AlphaMode { get; set; } = DdsAlphaMode.Unknown;

    /// <inheritdoc />
    int ITextureFaceLayoutMetadata.FaceCount
        => (this.ResourceMiscFlags & D3d10ResourceMiscFlags.TextureCube) != 0 ? 6 : 1;

    /// <summary>
    ///     Creates DDS metadata from container-neutral connecting metadata.
    /// </summary>
    /// <param name="metadata">The connecting texture metadata.</param>
    /// <returns>The DDS metadata.</returns>
    /// <exception cref="NotSupportedException">The texel type has no DDS representation.</exception>
    public static DdsTextureMetadata FromTextureConnectingMetadata(TextureConnectingMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (!DdsTexelFormats.TryGetTexelFormat(metadata.TexelTypeInfo, out TexelFormat texelFormat))
        {
            throw new NotSupportedException(
                "The connecting texel type has no supported DDS representation.");
        }

        bool hasLegacyFourCc = DdsTexelFormats.TryGetFourCc(texelFormat, out DdsFourCc fourCc);

        return new DdsTextureMetadata
        {
            DxgiFormat = hasLegacyFourCc ? DxgiFormat.Unknown : DdsTexelFormats.GetDxgiFormat(texelFormat),
            FourCc = hasLegacyFourCc ? fourCc : DdsFourCc.None,
            HasDx10Header = !hasLegacyFourCc,
            ResourceDimension = hasLegacyFourCc ? D3d10ResourceDimension.Unknown : D3d10ResourceDimension.Texture2D,
            ResourceMiscFlags = D3d10ResourceMiscFlags.Unknown,
            ArraySize = 1,
            MipMapCount = 1,
            Depth = 1,
            AlphaMode = ToAlphaMode(metadata.TexelTypeInfo.AlphaRepresentation)
        };
    }

    /// <summary>
    ///     Converts the metadata to a <see cref="TextureConnectingMetadata" /> instance.
    /// </summary>
    /// <returns>The connecting texture metadata.</returns>
    /// <exception cref="NotSupportedException">The declared texel format has no connecting representation.</exception>
    public TextureConnectingMetadata ToTextureConnectingMetadata()
    {
        if (!this.TryGetTexelFormat(out TexelFormat texelFormat))
        {
            throw new NotSupportedException(this.HasDx10Header
                ? $"The DXGI format {(uint)this.DxgiFormat} has no supported DDS texel type."
                : $"The DDS four character code '{(uint)this.FourCc:X8}' has no supported DDS texel type.");
        }

        return new TextureConnectingMetadata
        {
            TexelTypeInfo = DdsTexelFormats.GetTexelTypeInfo(texelFormat, this.AlphaMode)
        };
    }

    /// <summary>
    ///     Creates a deep copy of the metadata.
    /// </summary>
    /// <returns>The cloned metadata.</returns>
    public DdsTextureMetadata DeepClone()
        => new()
        {
            DxgiFormat = this.DxgiFormat,
            FourCc = this.FourCc,
            HasDx10Header = this.HasDx10Header,
            ResourceDimension = this.ResourceDimension,
            ResourceMiscFlags = this.ResourceMiscFlags,
            ArraySize = this.ArraySize,
            MipMapCount = this.MipMapCount,
            Depth = this.Depth,
            AlphaMode = this.AlphaMode
        };

    /// <inheritdoc />
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    private static DdsAlphaMode ToAlphaMode(TexelAlphaRepresentation alphaRepresentation)
        => alphaRepresentation switch
        {
            TexelAlphaRepresentation.None => DdsAlphaMode.Opaque,
            TexelAlphaRepresentation.Associated => DdsAlphaMode.Premultiplied,
            TexelAlphaRepresentation.Unassociated => DdsAlphaMode.Straight,
            _ => DdsAlphaMode.Unknown
        };

    private bool TryGetTexelFormat(out TexelFormat texelFormat)
        => this.HasDx10Header
            ? DdsTexelFormats.TryGetTexelFormat(this.DxgiFormat, out texelFormat)
            : DdsTexelFormats.TryGetTexelFormat(this.FourCc, out texelFormat);
}
