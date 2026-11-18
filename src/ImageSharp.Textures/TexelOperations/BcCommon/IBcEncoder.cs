namespace ImageSharp.Textures.Texels.BcCommon;

/// <summary>
///     Implemented by texel operations that support block compression encoding with per-call settings.
/// </summary>
/// <remarks>
///     <see cref="TexelOperations.Encode{TPixel}" /> is part of the frozen public API and carries no options, so
///     container encoders reach the block encoders through this internal entry point.
/// </remarks>
internal interface IBcEncoder
{
    /// <summary>
    ///     Encodes an image into a block-compressed payload with the given settings.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type of the source image.</typeparam>
    /// <param name="source">The source image.</param>
    /// <param name="destination">The encoded payload.</param>
    /// <param name="options">The encode settings.</param>
    public void EncodeWithOptions<TPixel>(Image<TPixel> source, Span<byte> destination, BcEncodeOptions options)
        where TPixel : unmanaged, IPixel<TPixel>;
}
