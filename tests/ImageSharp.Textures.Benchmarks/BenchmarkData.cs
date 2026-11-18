using ImageSharp.Textures.Texels.BcCommon;

namespace ImageSharp.Textures.Benchmarks;

/// <summary>
///     Builds the inputs shared by the block compression benchmarks.
/// </summary>
internal static class BenchmarkData
{
    private const int PayloadSourceSize = 256;

    private static readonly Dictionary<TexelFormat, byte[]> PayloadCache = new();

    /// <summary>
    ///     Renders a deterministic RGBA gradient of the requested size.
    /// </summary>
    /// <param name="size">The image width and height.</param>
    /// <returns>The image.</returns>
    internal static Image<Rgba32> CreateImage(int size)
    {
        Image<Rgba32> image = new(size, size);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                image[x, y] = new Rgba32(
                    (byte)(x * 255 / Math.Max(1, size - 1)),
                    (byte)(y * 255 / Math.Max(1, size - 1)),
                    (byte)((x + y) * 255 / Math.Max(1, (2 * size) - 2)),
                    255);
            }
        }

        return image;
    }

    /// <summary>
    ///     Gets a payload of the requested size for the format.
    /// </summary>
    /// <remarks>
    ///     The block data is encoded once at 256x256 through the library's own encoders and then tiled to the
    ///     requested size. Decode throughput depends on the number of blocks, not on their contents, so tiling
    ///     keeps the setup cost flat: encoding a 4096x4096 BC7 surface with the full mode search would otherwise
    ///     dominate the whole run.
    /// </remarks>
    /// <param name="format">The texel format.</param>
    /// <param name="size">The surface width and height.</param>
    /// <returns>The encoded payload.</returns>
    internal static byte[] GetPayload(TexelFormat format, int size)
    {
        byte[] source = GetSourcePayload(format);
        int blocks = size / 4 * (size / 4);
        int sourceBlocks = source.Length / BlockBytes(format);
        byte[] payload = new byte[blocks * BlockBytes(format)];

        for (int block = 0; block < blocks; block++)
        {
            int offset = block % sourceBlocks * BlockBytes(format);
            Buffer.BlockCopy(source, offset, payload, block * BlockBytes(format), BlockBytes(format));
        }

        return payload;
    }

    /// <summary>
    ///     Encodes an image into the destination payload with the given settings.
    /// </summary>
    /// <param name="format">The texel format.</param>
    /// <param name="source">The source image.</param>
    /// <param name="destination">The destination payload.</param>
    /// <param name="quality">The BC7 quality preset.</param>
    internal static void Encode(TexelFormat format, Image<Rgba32> source, Span<byte> destination,
        Bc7EncodingQuality quality = Bc7EncodingQuality.Default)
        => BcEncode.Encode(
            TexelFormatRegistry.GetOperations(format),
            source,
            destination,
            new BcEncodeOptions(0.5f, quality));

    /// <summary>
    ///     Gets the block size of the format, in bytes.
    /// </summary>
    /// <param name="format">The texel format.</param>
    /// <returns>The block size.</returns>
    internal static int BlockBytes(TexelFormat format)
        => format is TexelFormat.Bc1_UNorm or TexelFormat.Bc1_UnormSrgb or TexelFormat.Bc4_UNorm ? 8 : 16;

    private static byte[] GetSourcePayload(TexelFormat format)
    {
        if (PayloadCache.TryGetValue(format, out byte[]? cached))
        {
            return cached;
        }

        using Image<Rgba32> image = CreateImage(PayloadSourceSize);
        int blocks = PayloadSourceSize / 4 * (PayloadSourceSize / 4);
        byte[] payload = new byte[blocks * BlockBytes(format)];
        Encode(format, image, payload);
        PayloadCache[format] = payload;
        return payload;
    }
}
