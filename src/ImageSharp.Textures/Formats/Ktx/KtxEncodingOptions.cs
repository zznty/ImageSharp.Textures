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

    /// <summary>
    ///     Gets the BC7 encoder quality preset used when surfaces are converted to BC7.
    ///     Defaults to <see cref="Bc7EncodingQuality.Default" />.
    /// </summary>
    public Bc7EncodingQuality Bc7Quality { get; init; } = Bc7EncodingQuality.Default;

    /// <summary>
    ///     Gets the normalized alpha cutoff in <c>[0, 1]</c> below which BC1 texels become transparent.
    ///     Defaults to <c>0.5f</c>.
    /// </summary>
    public float Bc1AlphaThreshold { get; init; } = 0.5f;

    /// <summary>
    ///     Gets the maximum number of CPU threads used when encoding BC7 surfaces, or <c>-1</c> to use all available
    ///     processors. Defaults to <c>1</c> (single-threaded).
    /// </summary>
    public int MaxDegreeOfParallelism { get; init; } = 1;

    /// <summary>
    ///     Gets an optional hardware or GPU compute BC7 encoder used when converting surfaces to BC7, or
    ///     <see langword="null" /> to encode on the CPU.
    /// </summary>
    public IBc7HardwareEncoder? Bc7HardwareEncoder { get; init; }
}
