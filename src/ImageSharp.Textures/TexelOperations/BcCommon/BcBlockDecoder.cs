using System.Runtime.CompilerServices;

namespace ImageSharp.Textures.Texels.BcCommon;

/// <summary>
///     Iterates the blocks of a block-compressed payload, writing decoded rows straight into the destination.
/// </summary>
/// <remarks>
///     Decoding runs one block row at a time. Each block is decoded into a 64-byte stack scratch and its visible
///     rows are copied into the destination, so no intermediate image is ever materialised and the only working
///     memory is a single block plus the per-decoder scratch (96 bytes, all on the stack).
/// </remarks>
internal static class BcBlockDecoder
{
    /// <summary>
    ///     The scratch requirement of the block decoders, in bytes.
    /// </summary>
    internal const int ScratchBytes = 96;

    /// <summary>
    ///     Decodes a full surface into tightly packed RGBA32 rows.
    /// </summary>
    /// <typeparam name="TBlock">The block decoder.</typeparam>
    /// <param name="source">The encoded payload.</param>
    /// <param name="destination">The destination, at least <paramref name="width" /> * <paramref name="height" /> * 4 bytes.</param>
    /// <param name="width">The surface width, in texels.</param>
    /// <param name="height">The surface height, in texels.</param>
    internal static void Decode<TBlock>(ReadOnlySpan<byte> source, Span<byte> destination, int width, int height)
        where TBlock : struct, IBcBlockDecoder<TBlock>
    {
        int blockColumns = (width + 3) >> 2;
        int blockRows = (height + 3) >> 2;
        int rowBytes = width * 4;
        int blockRowBytes = blockColumns * TBlock.BlockBytes;

        Span<byte> block = stackalloc byte[64];
        Span<byte> scratch = stackalloc byte[ScratchBytes];

        for (int blockRow = 0; blockRow < blockRows; blockRow++)
        {
            int y = blockRow << 2;
            int rows = Math.Min(4, height - y);
            ReadOnlySpan<byte> sourceRow = source.Slice(blockRow * blockRowBytes, blockRowBytes);
            DecodeBlockRow<TBlock>(sourceRow, destination.Slice(y * rowBytes), width, rows, block, scratch);
        }
    }

    /// <summary>
    ///     Decodes one block row into a destination that starts at the first row of the block row.
    /// </summary>
    /// <typeparam name="TBlock">The block decoder.</typeparam>
    /// <param name="source">The encoded blocks of one block row.</param>
    /// <param name="destination">The destination; rows are <paramref name="width" /> texels apart.</param>
    /// <param name="width">The surface width, in texels.</param>
    /// <param name="rows">The number of visible rows in this block row, 1 to 4.</param>
    /// <param name="block">The 64-byte block scratch.</param>
    /// <param name="scratch">The per-decoder scratch.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void DecodeBlockRow<TBlock>(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        int width,
        int rows,
        Span<byte> block,
        Span<byte> scratch)
        where TBlock : struct, IBcBlockDecoder<TBlock>
    {
        int blockColumns = (width + 3) >> 2;
        int rowBytes = width * 4;
        int blockBytes = TBlock.BlockBytes;

        for (int column = 0; column < blockColumns; column++)
        {
            TBlock.DecodeBlock(source.Slice(column * blockBytes, blockBytes), block, scratch);

            int x = column << 2;
            int columns = Math.Min(4, width - x);
            for (int row = 0; row < rows; row++)
            {
                block.Slice(row * 16, columns * 4).CopyTo(destination.Slice((row * rowBytes) + (x * 4)));
            }
        }
    }
}
