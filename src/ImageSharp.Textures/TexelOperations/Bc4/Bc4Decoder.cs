using System.Buffers.Binary;
using ImageSharp.Textures.Texels.BcCommon;

namespace ImageSharp.Textures.Texels.Bc4;

/// <summary>
///     Decodes BC4_UNorm payloads into RGBA32 texels.
/// </summary>
/// <remarks>
///     A BC4 block is eight bytes: two 8-bit red endpoints and sixteen three-bit palette indices packed into six
///     bytes. red0 &gt; red1 selects the eight value palette (p[k] = ((7 - k) * red0 + k * red1) / 7); otherwise
///     the six value palette is used with p[6] = 0 and p[7] = 255. Divisions round to nearest, matching the
///     DirectXTex float decode to within one least significant bit. The decoded texel is red only: green and blue
///     are 0 and alpha is 255.
/// </remarks>
internal static class Bc4Decoder
{
    /// <summary>
    ///     Decodes a BC4_UNorm payload into tightly packed RGBA32 rows.
    /// </summary>
    /// <param name="source">The encoded payload, pitch = ceil(width/4) * 8.</param>
    /// <param name="destination">The destination, at least width * height * 4 bytes.</param>
    /// <param name="width">The surface width, in texels.</param>
    /// <param name="height">The surface height, in texels.</param>
    public static void Decode(ReadOnlySpan<byte> source, Span<byte> destination, int width, int height)
        => BcBlockDecoder.Decode<Block>(source, destination, width, height);

    /// <summary>
    ///     Builds the eight-entry BC4 palette for the two endpoints.
    /// </summary>
    /// <param name="red0">The first endpoint.</param>
    /// <param name="red1">The second endpoint.</param>
    /// <param name="palette">The destination, eight bytes.</param>
    internal static void BuildPalette(byte red0, byte red1, Span<byte> palette)
    {
        palette[0] = red0;
        palette[1] = red1;

        if (red0 > red1)
        {
            for (int k = 1; k <= 6; k++)
            {
                palette[1 + k] = (byte)((((7 - k) * red0) + (k * red1) + 3) / 7);
            }
        }
        else
        {
            for (int k = 1; k <= 4; k++)
            {
                palette[1 + k] = (byte)((((5 - k) * red0) + (k * red1) + 2) / 5);
            }

            palette[6] = 0;
            palette[7] = 255;
        }
    }

    /// <summary>
    ///     Decodes one BC4 block.
    /// </summary>
    internal struct Block : IBcBlockDecoder<Block>
    {
        /// <inheritdoc />
        public static int BlockBytes => 8;

        /// <inheritdoc />
        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<byte> block, Span<byte> scratch)
        {
            ulong data = BinaryPrimitives.ReadUInt64LittleEndian(source);
            Span<byte> palette = scratch[..8];
            BuildPalette((byte)data, (byte)(data >> 8), palette);

            ulong indices = data >> 16;
            for (int texel = 0; texel < 16; texel++)
            {
                byte value = palette[(int)((indices >> (3 * texel)) & 0x7)];
                int offset = texel * 4;
                block[offset] = value;
                block[offset + 1] = 0;
                block[offset + 2] = 0;
                block[offset + 3] = 255;
            }
        }
    }
}
