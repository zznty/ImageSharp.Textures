namespace ImageSharp.Textures;

/// <summary>
///     Hardware or accelerated BC7 block-compression backend.
/// </summary>
public interface IBc7HardwareEncoder : IDisposable
{
    /// <summary>
    ///     Attempts to encode a tightly packed RGBA32 surface into BC7 blocks.
    /// </summary>
    /// <param name="rgbaSource">The tightly packed top-left origin RGBA32 pixels (<c>width * height * 4</c> bytes).</param>
    /// <param name="destination">The destination BC7 block buffer (<c>ceil(width / 4) * ceil(height / 4) * 16</c> bytes).</param>
    /// <param name="width">The surface width, in texels.</param>
    /// <param name="height">The surface height, in texels.</param>
    /// <param name="quality">The BC7 quality preset.</param>
    /// <returns>
    ///     <see langword="true" /> when the backend encoded the surface; <see langword="false" /> to fall back to the
    ///     managed CPU encoder.
    /// </returns>
    public bool TryEncode(
        ReadOnlySpan<byte> rgbaSource,
        Span<byte> destination,
        int width,
        int height,
        Bc7EncodingQuality quality);
}
