namespace ImageSharp.Textures.Formats.Dds;

/// <summary>
///     Options controlling how a texture is written to a DDS container.
/// </summary>
/// <remarks>
///     A save never recompresses the stored payload unless <see cref="TargetTexelFormat" /> asks for a
///     conversion: the encoder writes each surface exactly as it is held in memory.
/// </remarks>
public sealed class DdsEncodingOptions
{
    /// <summary>
    ///     Gets the texel format to convert every surface to before writing, or <see langword="null" /> to keep
    ///     the texel format each surface already uses.
    /// </summary>
    public TexelFormat? TargetTexelFormat { get; init; }

    /// <summary>
    ///     Gets the quality preset used when a conversion targets BC7.
    /// </summary>
    public Bc7EncodingQuality Bc7Quality { get; init; } = Bc7EncodingQuality.Default;

    /// <summary>
    ///     Gets a value indicating whether BC1, BC4 and BC5 unsigned normalized payloads are written with a
    ///     <c>DDS_HEADER_DXT10</c> extension header instead of the legacy four character code.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The legacy form is used when the file can be expressed with it and the texture is not an array: BC1
    ///         (<c>DXT1</c>), BC4 (<c>BC4U</c>) and BC5 (<c>BC5U</c>) with a single array element. Cube maps and volume
    ///         textures stay legacy because <c>DDSCAPS2</c> describes them, but an array of more than one layer (or a
    ///         cube map array) has no place for its element count in a legacy header, so those textures are written
    ///         with the DX10 header even when this property is <see langword="false" />.
    ///     </para>
    ///     <para>
    ///         BC7, every sRGB format and R8G8B8A8_UNORM always require the DX10 header because they have no legacy
    ///         four character code in the supported mapping.
    ///     </para>
    /// </remarks>
    public bool UseDx10Header { get; init; }

    /// <summary>
    ///     Gets the alpha value, in the range 0 to 1, below which a BC1 conversion switches to the three color
    ///     punch-through mode.
    /// </summary>
    public float Bc1AlphaThreshold { get; init; } = 0.5f;
}
