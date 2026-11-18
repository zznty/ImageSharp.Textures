namespace ImageSharp.Textures.Texels.BcCommon;

/// <summary>
///     Entry point for encoding through the texel operations with per-call settings.
/// </summary>
internal static class BcEncode
{
    /// <summary>
    ///     Encodes an image with the given texel operations, honouring the block compression settings when the
    ///     operations support them.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type of the source image.</typeparam>
    /// <param name="operations">The texel operations of the destination format.</param>
    /// <param name="source">The source image.</param>
    /// <param name="destination">The encoded payload.</param>
    /// <param name="options">The encode settings.</param>
    internal static void Encode<TPixel>(
        TexelOperations operations,
        Image<TPixel> source,
        Span<byte> destination,
        BcEncodeOptions options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(source);

        if (operations is IBcEncoder encoder)
        {
            encoder.EncodeWithOptions(source, destination, options);
            return;
        }

        operations.Encode(source, destination);
    }
}
