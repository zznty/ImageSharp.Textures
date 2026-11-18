namespace ImageSharp.Textures.Formats.Ktx;

/// <summary>
///     Configures the KTX v1 encoder.
/// </summary>
public sealed class KtxEncodingOptions
{
    /// <summary>
    ///     Gets the texel format to convert every surface to before writing, or <see langword="null" /> to write the
    ///     surfaces' current texel formats. A KTX file stores one texel format, so conversion is performed per
    ///     surface through RGBA when the texture contains mixed formats.
    /// </summary>
    public TexelFormat? TargetTexelFormat { get; init; }

    /// <summary>
    ///     Gets the number of array layers, or <see langword="null" /> to divide the base mip level's surface
    ///     count by the resolved face count and depth.
    /// </summary>
    public int? LayerCount { get; init; }

    /// <summary>
    ///     Gets the number of cubemap faces. Must be 1 or 6. When <see langword="null" />, the face count is
    ///     resolved from the container-neutral face layout claim in the texture metadata (the container the
    ///     texture was loaded from, DDS, KTX or KTX2); a texture without such a claim has one face. An explicit
    ///     option always wins over the metadata claim.
    /// </summary>
    /// <remarks>
    ///     Layers are never resolved from metadata: they are the surface count divided by faces times depth.
    ///     <see cref="LayerCount" />, when set, overrides that division.
    /// </remarks>
    public int? FaceCount { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the key/value data block of the texture's KTX metadata is written.
    ///     The block is re-emitted byte for byte. Defaults to <see langword="true" />.
    /// </summary>
    public bool WriteKeyValueData { get; init; } = true;
}
