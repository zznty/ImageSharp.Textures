namespace ImageSharp.Textures.Texels.BcCommon;

/// <summary>
///     Encodes block-compressed payloads from tightly packed RGBA32 rows.
/// </summary>
/// <remarks>
///     Encoding runs one block row at a time. Partial blocks at the right and bottom edges are completed by
///     replicating the visible texels exactly as DirectXTex does, so a surface whose dimensions are not a
///     multiple of four produces the same blocks as the reference encoder.
/// </remarks>
internal static class BcBlockEncoder
{
    /// <summary>
    ///     The float scratch requirement of the block encoders, in elements.
    /// </summary>
    internal const int ScratchFloats = 48;

    /// <summary>
    ///     Encodes a surface from tightly packed RGBA32 rows.
    /// </summary>
    /// <typeparam name="TEncoder">The block encoder.</typeparam>
    /// <param name="source">The source, at least <paramref name="width" /> * <paramref name="height" /> * 4 bytes.</param>
    /// <param name="destination">The encoded payload.</param>
    /// <param name="width">The surface width, in texels.</param>
    /// <param name="height">The surface height, in texels.</param>
    /// <param name="options">The encode settings.</param>
    internal static void Encode<TEncoder>(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        int width,
        int height,
        BcEncodeOptions options)
        where TEncoder : struct, IBcBlockEncoder<TEncoder>
    {
        int blockColumns = (width + 3) >> 2;
        int blockRows = (height + 3) >> 2;
        int blockBytes = TEncoder.BlockBytes;
        int rowBytes = width * 4;
        int blockRowBytes = blockColumns * blockBytes;

        long required = (long)blockColumns * blockRows * blockBytes;
        if (destination.Length < required)
        {
            throw new ArgumentException(
                $"The destination payload contains {destination.Length} bytes but {required} bytes are required "
                + $"for a {width}x{height} surface.",
                nameof(destination));
        }

        if (source.Length < (long)rowBytes * height)
        {
            throw new ArgumentException(
                $"The source contains {source.Length} bytes but {rowBytes * height} bytes are required for a "
                + $"{width}x{height} RGBA32 surface.",
                nameof(source));
        }

        Span<byte> block = stackalloc byte[64];
        Span<float> scratch = stackalloc float[ScratchFloats];

        for (int blockRow = 0; blockRow < blockRows; blockRow++)
        {
            int y = blockRow << 2;
            int visibleRows = Math.Min(4, height - y);

            for (int blockColumn = 0; blockColumn < blockColumns; blockColumn++)
            {
                int x = blockColumn << 2;
                int visibleColumns = Math.Min(4, width - x);

                GatherBlock(source, block, rowBytes, x, y, visibleColumns, visibleRows);
                int offset = (blockRow * blockRowBytes) + (blockColumn * blockBytes);
                TEncoder.EncodeBlock(block, destination.Slice(offset, blockBytes), scratch, options);
            }
        }
    }

    /// <summary>
    ///     Copies one block out of the source rows and completes partial blocks with the DirectXTex replication
    ///     pattern: missing columns and rows reuse the visible texels through the source index table
    ///     <c>{ 0, 0, 0, 1 }</c>.
    /// </summary>
    /// <param name="source">The source rows.</param>
    /// <param name="block">The 64-byte destination block.</param>
    /// <param name="rowBytes">The source row stride, in bytes.</param>
    /// <param name="x">The first column of the block.</param>
    /// <param name="y">The first row of the block.</param>
    /// <param name="columns">The number of visible columns, 1 to 4.</param>
    /// <param name="rows">The number of visible rows, 1 to 4.</param>
    private static void GatherBlock(
        ReadOnlySpan<byte> source,
        Span<byte> block,
        int rowBytes,
        int x,
        int y,
        int columns,
        int rows)
    {
        for (int row = 0; row < rows; row++)
        {
            ReadOnlySpan<byte> sourceRow = source.Slice(((y + row) * rowBytes) + (x * 4), columns * 4);
            sourceRow.CopyTo(block.Slice(row * 16, columns * 4));
        }

        if (columns == 4 && rows == 4)
        {
            return;
        }

        ReadOnlySpan<int> replicate = [0, 0, 0, 1];

        if (columns < 4)
        {
            for (int row = 0; row < rows; row++)
            {
                for (int column = columns; column < 4; column++)
                {
                    ReadOnlySpan<byte> texel = block.Slice((row * 16) + (replicate[column] * 4), 4);
                    texel.CopyTo(block.Slice((row * 16) + (column * 4), 4));
                }
            }
        }

        if (rows < 4)
        {
            for (int row = rows; row < 4; row++)
            {
                block.Slice(replicate[row] * 16, 16).CopyTo(block.Slice(row * 16, 16));
            }
        }
    }
}
