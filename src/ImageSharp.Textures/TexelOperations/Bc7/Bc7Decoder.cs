using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using ImageSharp.Textures.Texels.BcCommon;

namespace ImageSharp.Textures.Texels.Bc7;

/// <summary>
///     Decodes BC7_UNorm payloads into RGBA32 texels.
/// </summary>
/// <remarks>
///     <para>
///         A BC7 block is 128 densely packed bits. The mode is a unary prefix (n zero bits then a one bit, n = 0..7);
///         endpoints are stored channel-major, extended by P-bits, unquantised as
///         <c>comp = value &lt;&lt; (8 - precision); comp | (comp &gt;&gt; precision)</c> and interpolated with the
///         integer form <c>(c0 * (64 - w) + c1 * w + 32) &gt;&gt; 6</c>.
///     </para>
///     <para>
///         The first index of every subset (the anchor) is stored one bit shorter, as is alpha index 0 in the two
///         index modes 4 and 5. A two-bit rotation swaps alpha with red, green or blue after interpolation. The
///         reserved mode (eight zero bits) decodes to opaque black, as required by the frozen model contract.
///     </para>
///     <para>
///         The loop is written for throughput: the 16-byte block is loaded once into a <see cref="UInt128" /> register
///         pair, the unary mode prefix is a trailing-zero count, the bit reader shifts the register value directly with
///         no per-field bounds checks, the per-channel endpoint precisions and interpolation weight tables are hoisted
///         out of the texel loop, and the partition and anchor tables are read through their flattened form. The
///         arithmetic itself is unchanged and remains bit-exact against texconv.
///     </para>
/// </remarks>
internal static class Bc7Decoder
{
    private static readonly ModeInfo[] ModeInfos =
    [
        // Mode 0: 3 subsets, 4-bit partition, RGB 444 + unique P (6 P-bits), 3-bit indices.
        new(2, 4, 6, 0, 0, 3, 0, new Channels(4, 4, 4, 0), new Channels(5, 5, 5, 0)),

        // Mode 1: 2 subsets, 6-bit partition, RGB 666 + shared P (2 P-bits), 3-bit indices.
        new(1, 6, 2, 0, 0, 3, 0, new Channels(6, 6, 6, 0), new Channels(7, 7, 7, 0)),

        // Mode 2: 3 subsets, 6-bit partition, RGB 555, 2-bit indices.
        new(2, 6, 0, 0, 0, 2, 0, new Channels(5, 5, 5, 0), new Channels(5, 5, 5, 0)),

        // Mode 3: 2 subsets, 6-bit partition, RGB 777 + unique P (4 P-bits), 2-bit indices.
        new(1, 6, 4, 0, 0, 2, 0, new Channels(7, 7, 7, 0), new Channels(8, 8, 8, 0)),

        // Mode 4: 1 subset, rotation, RGB 555 / A6, colour 2-bit + alpha 3-bit indices.
        new(0, 0, 0, 2, 1, 2, 3, new Channels(5, 5, 5, 6), new Channels(5, 5, 5, 6)),

        // Mode 5: 1 subset, rotation, RGB 777 / A8, colour 2-bit + alpha 2-bit indices.
        new(0, 0, 0, 2, 0, 2, 2, new Channels(7, 7, 7, 8), new Channels(7, 7, 7, 8)),

        // Mode 6: 1 subset, RGBA 7777 + unique P (2 P-bits), 4-bit indices.
        new(0, 0, 2, 0, 0, 4, 0, new Channels(7, 7, 7, 7), new Channels(8, 8, 8, 8)),

        // Mode 7: 2 subsets, 6-bit partition, RGBA 5555 + unique P (4 P-bits), 2-bit indices.
        new(1, 6, 4, 0, 0, 2, 0, new Channels(5, 5, 5, 5), new Channels(6, 6, 6, 6))
    ];

    /// <summary>
    ///     Decodes a BC7_UNorm payload into tightly packed RGBA32 rows.
    /// </summary>
    /// <param name="source">The encoded payload, pitch = ceil(width/4) * 16.</param>
    /// <param name="destination">The destination, at least width * height * 4 bytes.</param>
    /// <param name="width">The surface width, in texels.</param>
    /// <param name="height">The surface height, in texels.</param>
    public static void Decode(ReadOnlySpan<byte> source, Span<byte> destination, int width, int height)
        => BcBlockDecoder.Decode<Block>(source, destination, width, height);

    /// <summary>
    ///     Reads one channel of every endpoint, channel-major.
    /// </summary>
    /// <param name="source">The 128-bit block.</param>
    /// <param name="bit">The bit position.</param>
    /// <param name="endpoints">The endpoint scratch, four channels per endpoint.</param>
    /// <param name="count">The number of endpoints.</param>
    /// <param name="channel">The channel to read.</param>
    /// <param name="precision">The endpoint precision in bits, 0 when the mode has no such channel.</param>
    /// <returns><see langword="false" /> when the block is truncated.</returns>
    private static bool ReadEndpoints(
        UInt128 source,
        ref uint bit,
        Span<byte> endpoints,
        int count,
        int channel,
        uint precision)
    {
        if (precision == 0)
        {
            for (int i = 0; i < count; i++)
            {
                endpoints[(i * 4) + channel] = 255;
            }

            return true;
        }

        for (int i = 0; i < count; i++)
        {
            if (bit + precision > 128)
            {
                return false;
            }

            endpoints[(i * 4) + channel] = GetBits(source, ref bit, precision);
        }

        return true;
    }

    /// <summary>
    ///     Unquantises one endpoint component to eight bits.
    /// </summary>
    /// <param name="endpoints">The endpoint scratch.</param>
    /// <param name="slot">The component slot.</param>
    /// <param name="bits">The component precision.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Unquantize(Span<byte> endpoints, int slot, uint bits)
    {
        if (bits == 0)
        {
            endpoints[slot] = 255;
            return;
        }

        byte value = (byte)(endpoints[slot] << (int)(8 - bits));
        endpoints[slot] = (byte)(value | (value >> (int)bits));
    }

    /// <summary>
    ///     Interpolates two unquantised components with the BC7 integer weight arithmetic.
    /// </summary>
    /// <param name="c0">The first endpoint component.</param>
    /// <param name="c1">The second endpoint component.</param>
    /// <param name="weight">The interpolation weight, already scaled to 64.</param>
    /// <returns>The interpolated component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte Interpolate(byte c0, byte c1, int weight)
        => (byte)(((c0 * (Bc7Constants.BC67_WEIGHT_MAX - weight)) + (c1 * weight) + Bc7Constants.BC67_WEIGHT_ROUND) >>
                  Bc7Constants.BC67_WEIGHT_SHIFT);

    /// <summary>
    ///     Fills a block with opaque black, the reserved-mode result.
    /// </summary>
    /// <param name="block">The block destination.</param>
    private static void FillOpaqueBlack(Span<byte> block)
    {
        for (int texel = 0; texel < Bc7Constants.NumPixelsPerBlock; texel++)
        {
            int offset = texel * 4;
            block[offset] = 0;
            block[offset + 1] = 0;
            block[offset + 2] = 0;
            block[offset + 3] = 255;
        }
    }

    /// <summary>
    ///     Reads up to eight bits and advances the bit position.
    /// </summary>
    /// <param name="source">The 128-bit block.</param>
    /// <param name="bit">The bit position, strictly less than 128.</param>
    /// <param name="count">The number of bits to read, at most eight.</param>
    /// <returns>The bit field value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte GetBits(UInt128 source, ref uint bit, uint count)
    {
        byte value = (byte)((uint)(source >> (int)bit) & ((1u << (int)count) - 1u));
        bit += count;
        return value;
    }

    /// <summary>
    ///     One BC7 mode's bit-stream layout.
    /// </summary>
    private readonly struct ModeInfo
    {
        internal readonly byte Partitions;
        internal readonly byte PartitionBits;
        internal readonly byte PBits;
        internal readonly byte RotationBits;
        internal readonly byte IndexModeBits;
        internal readonly byte IndexPrec;
        internal readonly byte IndexPrec2;
        internal readonly Channels Prec;
        internal readonly Channels PrecWithP;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ModeInfo" /> struct.
        /// </summary>
        /// <param name="partitions">The number of partitions minus one.</param>
        /// <param name="partitionBits">The width of the partition field.</param>
        /// <param name="pBits">The number of P-bits.</param>
        /// <param name="rotationBits">The width of the rotation field.</param>
        /// <param name="indexModeBits">The width of the index mode field.</param>
        /// <param name="indexPrec">The colour index precision.</param>
        /// <param name="indexPrec2">The alpha index precision, 0 when the mode has one index set.</param>
        /// <param name="prec">The endpoint precision per channel.</param>
        /// <param name="precWithP">The endpoint precision per channel including P-bits.</param>
        internal ModeInfo(
            byte partitions,
            byte partitionBits,
            byte pBits,
            byte rotationBits,
            byte indexModeBits,
            byte indexPrec,
            byte indexPrec2,
            Channels prec,
            Channels precWithP)
        {
            this.Partitions = partitions;
            this.PartitionBits = partitionBits;
            this.PBits = pBits;
            this.RotationBits = rotationBits;
            this.IndexModeBits = indexModeBits;
            this.IndexPrec = indexPrec;
            this.IndexPrec2 = indexPrec2;
            this.Prec = prec;
            this.PrecWithP = precWithP;
        }
    }

    /// <summary>
    ///     Four endpoint channels with sequential access (R, G, B, A).
    /// </summary>
    private readonly struct Channels
    {
        private readonly byte r;
        private readonly byte g;
        private readonly byte b;
        private readonly byte a;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Channels" /> struct.
        /// </summary>
        /// <param name="r">The red precision.</param>
        /// <param name="g">The green precision.</param>
        /// <param name="b">The blue precision.</param>
        /// <param name="a">The alpha precision.</param>
        internal Channels(byte r, byte g, byte b, byte a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        /// <summary>
        ///     Gets the precision of the given channel.
        /// </summary>
        /// <param name="channel">The channel index, 0 to 3.</param>
        /// <returns>The channel precision.</returns>
        internal byte this[int channel] => channel switch
        {
            0 => this.r,
            1 => this.g,
            2 => this.b,
            _ => this.a
        };
    }

    /// <summary>
    ///     Decodes one BC7 block.
    /// </summary>
    internal struct Block : IBcBlockDecoder<Block>
    {
        /// <inheritdoc />
        public static int BlockBytes => 16;

        /// <inheritdoc />
        public static void DecodeBlock(ReadOnlySpan<byte> source, Span<byte> block, Span<byte> scratch)
        {
            UInt128 blockBits = BinaryPrimitives.ReadUInt128LittleEndian(source);

            // Scratch layout: endpoints 8 * 4 bytes, the two 16-byte index sets, then 6 P-bits.
            Span<byte> endpoints = scratch[..32];
            Span<byte> w1 = scratch.Slice(32, 16);
            Span<byte> w2 = scratch.Slice(48, 16);
            Span<byte> pBits = scratch.Slice(64, 6);

            // The mode is a unary prefix: the number of zero bits before the first one bit.
            int mode = BitOperations.TrailingZeroCount((uint)blockBits);
            if (mode >= 8)
            {
                FillOpaqueBlack(block);
                return;
            }

            ModeInfo info = ModeInfos[mode];
            uint start = (uint)mode + 1;

            byte shape = GetBits(blockBits, ref start, info.PartitionBits);
            byte rotation = GetBits(blockBits, ref start, info.RotationBits);
            byte indexMode = GetBits(blockBits, ref start, info.IndexModeBits);

            byte partitions = info.Partitions;
            int endpointCount = (partitions + 1) * 2;

            uint precR = info.Prec[0];
            uint precG = info.Prec[1];
            uint precB = info.Prec[2];
            uint precA = info.Prec[3];
            uint precPR = info.PrecWithP[0];
            uint precPG = info.PrecWithP[1];
            uint precPB = info.PrecWithP[2];
            uint precPA = info.PrecWithP[3];

            // Endpoint channels, channel-major: R for every endpoint, then G, B, A.
            if (!ReadEndpoints(blockBits, ref start, endpoints, endpointCount, 0, precR)
                || !ReadEndpoints(blockBits, ref start, endpoints, endpointCount, 1, precG)
                || !ReadEndpoints(blockBits, ref start, endpoints, endpointCount, 2, precB)
                || !ReadEndpoints(blockBits, ref start, endpoints, endpointCount, 3, precA))
            {
                FillOpaqueBlack(block);
                return;
            }

            // P-bits: one per endpoint (unique) or one per subset (shared pair).
            int pCount = info.PBits;
            for (int i = 0; i < pCount; i++)
            {
                if (start >= 128)
                {
                    FillOpaqueBlack(block);
                    return;
                }

                pBits[i] = (byte)((uint)(blockBits >> (int)start) & 1u);
                start++;
            }

            if (pCount != 0)
            {
                for (int i = 0; i < endpointCount; i++)
                {
                    byte p = pBits[i * pCount / endpointCount];
                    int slot = i * 4;

                    if (precR != precPR)
                    {
                        endpoints[slot] = (byte)((endpoints[slot] << 1) | p);
                    }

                    if (precG != precPG)
                    {
                        endpoints[slot + 1] = (byte)((endpoints[slot + 1] << 1) | p);
                    }

                    if (precB != precPB)
                    {
                        endpoints[slot + 2] = (byte)((endpoints[slot + 2] << 1) | p);
                    }

                    if (precA != precPA)
                    {
                        endpoints[slot + 3] = (byte)((endpoints[slot + 3] << 1) | p);
                    }
                }
            }

            // Unquantise to eight bits.
            for (int i = 0; i < endpointCount; i++)
            {
                int slot = i * 4;
                Unquantize(endpoints, slot, precPR);
                Unquantize(endpoints, slot + 1, precPG);
                Unquantize(endpoints, slot + 2, precPB);
                Unquantize(endpoints, slot + 3, precPA);
            }

            int shapeIndex = (partitions * 64) + shape;
            int anchorIndex = shapeIndex * 3;
            ReadOnlySpan<byte> anchors = Bc7Constants.FlatAnchorTable;
            byte anchor0 = anchors[anchorIndex];
            byte anchor1 = anchors[anchorIndex + 1];
            byte anchor2 = anchors[anchorIndex + 2];

            // Colour indices: every texel, except the one-bit-shorter anchors.
            uint indexPrec = info.IndexPrec;
            for (int i = 0; i < 16; i++)
            {
                uint bits = i == anchor0 || i == anchor1 || i == anchor2 ? indexPrec - 1u : indexPrec;
                if (start + bits > 128)
                {
                    FillOpaqueBlack(block);
                    return;
                }

                w1[i] = GetBits(blockBits, ref start, bits);
            }

            // Alpha indices for the two-index modes; texel 0 is one bit shorter.
            uint indexPrec2 = info.IndexPrec2;
            if (indexPrec2 != 0)
            {
                for (int i = 0; i < 16; i++)
                {
                    uint bits = i != 0 ? indexPrec2 : indexPrec2 - 1u;
                    if (start + bits > 128)
                    {
                        FillOpaqueBlack(block);
                        return;
                    }

                    w2[i] = GetBits(blockBits, ref start, bits);
                }
            }

            ReadOnlySpan<int> colourWeights = indexPrec == 2
                ? Bc7Constants.Weights2
                : indexPrec == 3
                    ? Bc7Constants.Weights3
                    : Bc7Constants.Weights4;
            ReadOnlySpan<int> alphaWeights = indexPrec2 == 0
                ? colourWeights
                : indexPrec2 == 2
                    ? Bc7Constants.Weights2
                    : indexPrec2 == 3
                        ? Bc7Constants.Weights3
                        : Bc7Constants.Weights4;

            ReadOnlySpan<byte> partitionTable = Bc7Constants.FlatPartitionTable.Slice(shapeIndex * 16, 16);

            for (int i = 0; i < 16; i++)
            {
                int region = partitionTable[i] << 1;
                int c0 = region * 4;
                int c1 = c0 + 4;

                int colourWeight;
                int alphaWeight;
                int colourIndex = w1[i];

                if (indexPrec2 == 0)
                {
                    colourWeight = colourWeights[colourIndex];
                    alphaWeight = colourWeight;
                }
                else if (indexMode == 0)
                {
                    colourWeight = colourWeights[colourIndex];
                    alphaWeight = alphaWeights[w2[i]];
                }
                else
                {
                    colourWeight = alphaWeights[w2[i]];
                    alphaWeight = colourWeights[colourIndex];
                }

                byte r = Interpolate(endpoints[c0], endpoints[c1], colourWeight);
                byte g = Interpolate(endpoints[c0 + 1], endpoints[c1 + 1], colourWeight);
                byte b = Interpolate(endpoints[c0 + 2], endpoints[c1 + 2], colourWeight);
                byte a = Interpolate(endpoints[c0 + 3], endpoints[c1 + 3], alphaWeight);

                switch (rotation)
                {
                    case 1:
                        (r, a) = (a, r);
                        break;
                    case 2:
                        (g, a) = (a, g);
                        break;
                    case 3:
                        (b, a) = (a, b);
                        break;
                }

                int offset = i * 4;
                block[offset] = r;
                block[offset + 1] = g;
                block[offset + 2] = b;
                block[offset + 3] = a;
            }
        }
    }
}
