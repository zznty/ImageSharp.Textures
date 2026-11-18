namespace ImageSharp.Textures;

/// <summary>
///     Converts one texel type between encoded payload bytes and ImageSharp images.
/// </summary>
public abstract class TexelOperations
{
    /// <summary>
    ///     Gets information about the texel type handled by this instance.
    /// </summary>
    /// <returns>The texel type information.</returns>
    public abstract TexelTypeInfo GetTexelTypeInfo();

    /// <summary>
    ///     Gets the encoded payload size for a surface with the specified dimensions.
    /// </summary>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    /// <returns>The encoded payload size, in bytes.</returns>
    public abstract int GetEncodedByteCount(int width, int height);

    /// <summary>
    ///     Decodes a texture payload into an ImageSharp image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type of the decoded image.</typeparam>
    /// <param name="source">The encoded texture payload.</param>
    /// <param name="configuration">The ImageSharp configuration to use when allocating the decoded image.</param>
    /// <param name="width">The decoded image width.</param>
    /// <param name="height">The decoded image height.</param>
    /// <returns>The decoded ImageSharp image.</returns>
    public abstract Image<TPixel> Decode<TPixel>(
        ReadOnlySpan<byte> source,
        Configuration configuration,
        int width,
        int height)
        where TPixel : unmanaged, IPixel<TPixel>;

    /// <summary>
    ///     Encodes an ImageSharp image into a texture payload.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type of the source image.</typeparam>
    /// <param name="source">The source ImageSharp image.</param>
    /// <param name="destination">The encoded texture payload.</param>
    public abstract void Encode<TPixel>(Image<TPixel> source, Span<byte> destination)
        where TPixel : unmanaged, IPixel<TPixel>;
}
