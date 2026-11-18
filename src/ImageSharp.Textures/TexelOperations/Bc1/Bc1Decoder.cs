using System.Buffers.Binary;
using ImageSharp.Textures.Texels.BcCommon;

namespace ImageSharp.Textures.Texels.Bc1;

/// <summary>
///     Decodes BC1_UNorm payloads into RGBA32 texels.
/// </summary>
/// <remarks>
///     A BC1 block is eight bytes: an RGB565 colour0, an RGB565 colour1 and sixteen two-bit palette indices.
///     Colour0 &gt; colour1 selects the four colour mode with c2 = (2*c0 + c1) / 3 and c3 = (c0 + 2*c1) / 3.
///     Otherwise the block is in three colour mode: c2 = (c0 + c1) / 2 and c3 is the punch-through texel with
///     alpha 0. The 5- and 6-bit channels expand to 8 bits by bit replication.
/// </remarks>
internal static class Bc1Decoder
{
    /// <summary>
    ///     Decodes a BC1_UNorm payload into tightly packed RGBA32 rows.
    /// </summary>
    /// <param name="source">The encoded payload, pitch = ceil(width/4) * 8.</param>
    /// <param name="destination">The destination, at least width * height * 4 bytes.</param>
    /// <param name="width">The surface width, in texels.</param>
    /// <param name="height">The surface height, in texels.</param>
    public static void Decode(ReadOnlySpan<byte> source, Span<byte> destination, int width, int height)
        => BcBlockDecoder.Decode<Block>(source, destination, width, height);

    /// <summary>
    ///     Decodes one BC1 block.
    /// </summary>
    internal struct Block : IBcBlockDecoder<Block>
    {
        /// <inheritdoc />
        public static int BlockBytes => 8;

        /// <inheritdoc />
        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<byte> block, Span<byte> scratch)
        {
            Span<byte> palette = scratch[..16];

            ushort color0 = BinaryPrimitives.ReadUInt16LittleEndian(source);
            ushort color1 = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(2));

            Expand(color0, palette, 0);
            Expand(color1, palette, 4);

            if (color0 > color1)
            {
                for (int channel = 0; channel < 3; channel++)
                {
                    byte c0 = palette[channel];
                    byte c1 = palette[4 + channel];
                    palette[8 + channel] = (byte)(((2 * c0) + c1) / 3);
                    palette[12 + channel] = (byte)((c0 + (2 * c1)) / 3);
                }

                palette[11] = 255;
                palette[15] = 255;
            }
            else
            {
                for (int channel = 0; channel < 3; channel++)
                {
                    byte c0 = palette[channel];
                    byte c1 = palette[4 + channel];
                    palette[8 + channel] = (byte)((c0 + c1) / 2);
                    palette[12 + channel] = 0;
                }

                palette[11] = 255;
                palette[15] = 0;
            }

            palette[3] = 255;
            palette[7] = 255;

            uint indices = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(4));
            for (int texel = 0; texel < 16; texel++)
            {
                int index = (int)((indices >> (texel * 2)) & 0x03u);
                int paletteOffset = index * 4;
                int offset = texel * 4;
                block[offset] = palette[paletteOffset];
                block[offset + 1] = palette[paletteOffset + 1];
                block[offset + 2] = palette[paletteOffset + 2];
                block[offset + 3] = palette[paletteOffset + 3];
            }
        }

        /// <summary>
        ///     Expands an RGB565 endpoint into 8-bit R, G, B by bit replication.
        /// </summary>
        /// <param name="color">The packed endpoint.</param>
        /// <param name="destination">The palette buffer.</param>
        /// <param name="offset">The offset of the entry to write.</param>
        private static void Expand(ushort color, Span<byte> destination, int offset)
        {
            int r = (color >> 11) & 0x1F;
            int g = (color >> 5) & 0x3F;
            int b = color & 0x1F;

            destination[offset] = (byte)((r << 3) | (r >> 2));
            destination[offset + 1] = (byte)((g << 2) | (g >> 4));
            destination[offset + 2] = (byte)((b << 3) | (b >> 2));
        }
    }
}
