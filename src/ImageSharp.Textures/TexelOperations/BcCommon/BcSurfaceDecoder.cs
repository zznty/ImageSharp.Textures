using System.Buffers;
using SixLabors.ImageSharp.Advanced;

namespace ImageSharp.Textures.Texels.BcCommon;

/// <summary>
///     Decodes block-compressed payloads into ImageSharp images.
/// </summary>
/// <remarks>
///     Decoding is done one block row at a time into a bounded row scratch of four RGBA rows. Each visible row is
///     then converted to the requested pixel type, so a full intermediate RGBA image is never allocated.
/// </remarks>
internal static class BcSurfaceDecoder
{
    /// <summary>
    ///     Decodes a block-compressed payload into an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type of the decoded image.</typeparam>
    /// <typeparam name="TBlock">The block decoder.</typeparam>
    /// <param name="source">The encoded payload.</param>
    /// <param name="configuration">The configuration used to allocate the image and the row scratch.</param>
    /// <param name="width">The surface width, in texels.</param>
    /// <param name="height">The surface height, in texels.</param>
    /// <param name="formatName">The texel format name used in error messages.</param>
    /// <returns>The decoded image.</returns>
    internal static Image<TPixel> Decode<TPixel, TBlock>(
        ReadOnlySpan<byte> source,
        Configuration configuration,
        int width,
        int height,
        string formatName)
        where TPixel : unmanaged, IPixel<TPixel>
        where TBlock : struct, IBcBlockDecoder<TBlock>
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "The surface width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "The surface height must be positive.");
        }

        int blockColumns = (width + 3) >> 2;
        int blockRows = (height + 3) >> 2;
        int blockBytes = TBlock.BlockBytes;
        int blockRowBytes = blockColumns * blockBytes;
        long required = (long)blockRowBytes * blockRows;

        if (source.Length < required)
        {
            throw new ArgumentException(
                $"The source payload contains {source.Length} bytes but {required} bytes are required for a {width}x{height} {formatName} surface.",
                nameof(source));
        }

        Image<TPixel> image = new(configuration, width, height);

        int rowBytes = checked(width * 4);
        int scratchByteCount = checked(rowBytes * 4);
        using IMemoryOwner<byte> scratchOwner = configuration.MemoryAllocator.Allocate<byte>(scratchByteCount);
        Span<byte> scratch = scratchOwner.Memory.Span[..scratchByteCount];
        Span<byte> block = stackalloc byte[64];
        Span<byte> decoderScratch = stackalloc byte[BcBlockDecoder.ScratchBytes];
        PixelOperations<TPixel> operations = PixelOperations<TPixel>.Instance;

        for (int blockRow = 0; blockRow < blockRows; blockRow++)
        {
            int y = blockRow << 2;
            int visibleRows = Math.Min(4, height - y);

            BcBlockDecoder.DecodeBlockRow<TBlock>(
                source.Slice(blockRow * blockRowBytes, blockRowBytes),
                scratch,
                width,
                visibleRows,
                block,
                decoderScratch);

            for (int row = 0; row < visibleRows; row++)
            {
                ReadOnlySpan<byte> rgbaRow = scratch.Slice(row * rowBytes, rowBytes);
                Span<TPixel> destinationRow = image.DangerousGetPixelRowMemory(y + row).Span;
                operations.FromRgba32Bytes(configuration, rgbaRow, destinationRow, width);
            }
        }

        return image;
    }
}
