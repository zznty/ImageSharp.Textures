using ImageSharp.Textures.Texels.Bc4;
using ImageSharp.Textures.Texels.BcCommon;

namespace ImageSharp.Textures.Texels.Bc5;

/// <summary>
///     Encodes RGBA32 texels into BC5_UNorm payloads.
/// </summary>
/// <remarks>
///     BC5 stores independent red and green channels, so the block is written with two complete BC4 passes: the
///     red channel fills the first eight bytes and the green channel the second eight.
/// </remarks>
internal static class Bc5Encoder
{
    /// <summary>
    ///     The encoded size, in bytes, of one block.
    /// </summary>
    internal const int BlockBytes = 16;

    private const int TexelsPerBlock = 16;

    private const float ByteToUnit = 1.0f / 255.0f;

    /// <summary>
    ///     Encodes a surface from tightly packed RGBA32 rows.
    /// </summary>
    /// <param name="source">The source, at least width * height * 4 bytes.</param>
    /// <param name="destination">The encoded payload.</param>
    /// <param name="width">The surface width, in texels.</param>
    /// <param name="height">The surface height, in texels.</param>
    /// <param name="options">The encode settings.</param>
    internal static void Encode(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        int width,
        int height,
        BcEncodeOptions options)
        => BcBlockEncoder.Encode<Block>(source, destination, width, height, options);

    /// <summary>
    ///     Encodes one BC5 block.
    /// </summary>
    internal struct Block : IBcBlockEncoder<Block>
    {
        /// <inheritdoc />
        public static int BlockBytes => Bc5Encoder.BlockBytes;

        /// <inheritdoc />
        public static void EncodeBlock(
            ReadOnlySpan<byte> block,
            Span<byte> destination,
            Span<float> scratch,
            BcEncodeOptions options)
        {
            Span<float> red = scratch[..TexelsPerBlock];
            Span<float> green = scratch.Slice(TexelsPerBlock, TexelsPerBlock);
            Span<float> gradient = scratch.Slice(TexelsPerBlock * 2, 8);

            for (int i = 0; i < TexelsPerBlock; i++)
            {
                // DirectXTex loads the source byte as `value * (1.0f / 255.0f)` (XMLoadUByteN4), not as a
                // division: the two differ by one ulp for 126 of the 256 byte values, which flips the tie
                // breaks in the endpoint search and stops the payload matching texconv's.
                red[i] = block[i * 4] * ByteToUnit;
                green[i] = block[(i * 4) + 1] * ByteToUnit;
            }

            Bc4Encoder.EncodeChannel(red, gradient, destination[..Bc4Encoder.BlockBytes]);
            Bc4Encoder.EncodeChannel(green, gradient, destination[Bc4Encoder.BlockBytes..]);
        }
    }
}
