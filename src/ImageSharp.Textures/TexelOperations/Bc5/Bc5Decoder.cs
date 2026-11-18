using System.Buffers.Binary;
using ImageSharp.Textures.Texels.Bc4;
using ImageSharp.Textures.Texels.BcCommon;

namespace ImageSharp.Textures.Texels.Bc5;

/// <summary>
///     Decodes BC5_UNorm payloads into RGBA32 texels.
/// </summary>
/// <remarks>
///     A BC5 block is two BC4-shaped halves: the first carries red, the second green. Each half has its own
///     endpoints and palette mode, so the two channels may interpolate differently. The decoded texel is red and
///     green only: blue is 0 and alpha is 255.
/// </remarks>
internal static class Bc5Decoder
{
    /// <summary>
    ///     Decodes a BC5_UNorm payload into tightly packed RGBA32 rows.
    /// </summary>
    /// <param name="source">The encoded payload, pitch = ceil(width/4) * 16.</param>
    /// <param name="destination">The destination, at least width * height * 4 bytes.</param>
    /// <param name="width">The surface width, in texels.</param>
    /// <param name="height">The surface height, in texels.</param>
    public static void Decode(ReadOnlySpan<byte> source, Span<byte> destination, int width, int height)
        => BcBlockDecoder.Decode<Block>(source, destination, width, height);

    /// <summary>
    ///     Decodes one BC5 block.
    /// </summary>
    internal struct Block : IBcBlockDecoder<Block>
    {
        /// <inheritdoc />
        public static int BlockBytes => 16;

        /// <inheritdoc />
        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<byte> block, Span<byte> scratch)
        {
            ulong redData = BinaryPrimitives.ReadUInt64LittleEndian(source);
            ulong greenData = BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(8));

            Span<byte> red = scratch[..8];
            Span<byte> green = scratch.Slice(8, 8);

            Bc4Decoder.BuildPalette((byte)redData, (byte)(redData >> 8), red);
            Bc4Decoder.BuildPalette((byte)greenData, (byte)(greenData >> 8), green);

            ulong redIndices = redData >> 16;
            ulong greenIndices = greenData >> 16;

            for (int texel = 0; texel < 16; texel++)
            {
                int offset = texel * 4;
                block[offset] = red[(int)((redIndices >> (3 * texel)) & 0x7)];
                block[offset + 1] = green[(int)((greenIndices >> (3 * texel)) & 0x7)];
                block[offset + 2] = 0;
                block[offset + 3] = 255;
            }
        }
    }
}
