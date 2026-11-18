using System.Buffers;
using SixLabors.ImageSharp.Advanced;

namespace ImageSharp.Textures.Texels.BcCommon;

/// <summary>
///     Encodes ImageSharp images into block-compressed payloads.
/// </summary>
/// <remarks>
///     Encoding runs one block row at a time through a bounded scratch of four RGBA rows, so a full intermediate
///     RGBA image is never allocated and the only working memory is the rented row scratch.
/// </remarks>
internal static class BcSurfaceEncoder
{
    /// <summary>
    ///     Encodes an image into a block-compressed payload.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type of the source image.</typeparam>
    /// <typeparam name="TEncoder">The block encoder.</typeparam>
    /// <param name="source">The source image.</param>
    /// <param name="destination">The encoded payload.</param>
    /// <param name="options">The encode settings.</param>
    /// <param name="formatName">The texel format name used in error messages.</param>
    internal static void Encode<TPixel, TEncoder>(
        Image<TPixel> source,
        Span<byte> destination,
        BcEncodeOptions options,
        string formatName)
        where TPixel : unmanaged, IPixel<TPixel>
        where TEncoder : struct, IBcBlockEncoder<TEncoder>
    {
        ArgumentNullException.ThrowIfNull(source);

        int width = source.Width;
        int height = source.Height;
        int blockColumns = (width + 3) >> 2;
        int blockRows = (height + 3) >> 2;
        int blockBytes = TEncoder.BlockBytes;
        int blockRowBytes = blockColumns * blockBytes;
        long required = (long)blockRowBytes * blockRows;

        if (destination.Length < required)
        {
            throw new ArgumentException(
                $"The destination payload contains {destination.Length} bytes but {required} bytes are required for a "
                + $"{width}x{height} {formatName} surface.",
                nameof(destination));
        }

        Configuration configuration = source.Configuration;
        int rowBytes = checked(width * 4);
        int scratchByteCount = checked(rowBytes * 4);
        using IMemoryOwner<byte> scratchOwner = configuration.MemoryAllocator.Allocate<byte>(scratchByteCount);
        Span<byte> scratch = scratchOwner.Memory.Span[..scratchByteCount];
        PixelOperations<TPixel> operations = PixelOperations<TPixel>.Instance;

        for (int blockRow = 0; blockRow < blockRows; blockRow++)
        {
            int y = blockRow << 2;
            int visibleRows = Math.Min(4, height - y);

            for (int row = 0; row < visibleRows; row++)
            {
                ReadOnlySpan<TPixel> sourceRow = source.DangerousGetPixelRowMemory(y + row).Span;
                Span<byte> destinationRow = scratch.Slice(row * rowBytes, rowBytes);
                operations.ToRgba32Bytes(configuration, sourceRow, destinationRow, width);
            }

            ReadOnlySpan<byte> blockSource = scratch[..(visibleRows * rowBytes)];
            Span<byte> blockDestination = destination.Slice(
                blockRow * blockRowBytes,
                blockColumns * blockBytes);

            BcBlockEncoder.Encode<TEncoder>(blockSource, blockDestination, width, visibleRows, options);
        }
    }
}
