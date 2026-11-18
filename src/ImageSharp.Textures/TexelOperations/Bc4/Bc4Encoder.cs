using System.Buffers.Binary;
using System.Numerics;
using ImageSharp.Textures.Texels.BcCommon;

namespace ImageSharp.Textures.Texels.Bc4;

/// <summary>
///     Encodes RGBA32 texels into BC4_UNorm payloads.
/// </summary>
/// <remarks>
///     A direct port of DirectXTex's <c>D3DXEncodeBC4U</c>: the endpoints start at the min and max of the block,
///     the 8 step solver refines them, the endpoints are quantized to 8 bits and every texel takes the nearest of
///     the eight palette entries. Blocks that contain a 0 or a 1 use the four interpolant mode (endpoint0 &lt;=
///     endpoint1), every other block uses the six interpolant mode (endpoint0 &gt; endpoint1).
/// </remarks>
internal static class Bc4Encoder
{
    /// <summary>
    ///     The encoded size, in bytes, of one block.
    /// </summary>
    internal const int BlockBytes = 8;

    private const int TexelsPerBlock = 16;

    private const float ByteToUnit = 1.0f / 255.0f;

    // Positional weights 8^k = 2^(3k) for packing up to eight 3-bit indices via a single dot product.
    // Vector<int> loads the first Vector<int>.Count elements (4 on 128-bit SIMD, 8 on 256-bit SIMD).
    private static ReadOnlySpan<int> PackWeightValues =>
        [1, 1 << 3, 1 << 6, 1 << 9, 1 << 12, 1 << 15, 1 << 18, 1 << 21];

    private static ReadOnlySpan<long> PackWeightValues64 =>
        [1L, 1L << 3, 1L << 6, 1L << 9, 1L << 12, 1L << 15, 1L << 18, 1L << 21];

    private static ReadOnlySpan<float> StepsC6 =>
        [5.0f / 5.0f, 4.0f / 5.0f, 3.0f / 5.0f, 2.0f / 5.0f, 1.0f / 5.0f, 0.0f / 5.0f];

    private static ReadOnlySpan<float> StepsD6 =>
        [0.0f / 5.0f, 1.0f / 5.0f, 2.0f / 5.0f, 3.0f / 5.0f, 4.0f / 5.0f, 5.0f / 5.0f];

    private static ReadOnlySpan<float> StepsC8 =>
    [
        7.0f / 7.0f, 6.0f / 7.0f, 5.0f / 7.0f, 4.0f / 7.0f, 3.0f / 7.0f, 2.0f / 7.0f, 1.0f / 7.0f, 0.0f / 7.0f
    ];

    private static ReadOnlySpan<float> StepsD8 =>
    [
        0.0f / 7.0f, 1.0f / 7.0f, 2.0f / 7.0f, 3.0f / 7.0f, 4.0f / 7.0f, 5.0f / 7.0f, 6.0f / 7.0f, 7.0f / 7.0f
    ];

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
    ///     Encodes one channel of four by four float texels into an eight byte BC4 block.
    /// </summary>
    /// <param name="texels">The sixteen channel values, in the range 0 to 1.</param>
    /// <param name="gradient">An eight-float scratch buffer for the reconstructed palette.</param>
    /// <param name="destination">The destination, eight bytes.</param>
    internal static void EncodeChannel(ReadOnlySpan<float> texels, Span<float> gradient, Span<byte> destination)
    {
        FindEndpoints(texels, out byte endpoint0, out byte endpoint1);
        BuildGradient(endpoint0, endpoint1, gradient);

        ulong data = endpoint0 | ((ulong)endpoint1 << 8);

        if (Vector.IsHardwareAccelerated && Vector<float>.Count <= 16)
        {
            // The palette is scanned over Vector<float>.Count texels at a time (4 on 128-bit, 8 on 256-bit, 16 on 512-bit):
            // each of the eight entries contributes a vector of per-texel absolute differences, the running minimum
            // keeps the closest entry per lane, and a strictly smaller delta replaces the recorded index.
            Vector<float> entry0 = new(gradient[0]);
            Vector<float> entry1 = new(gradient[1]);
            Vector<float> entry2 = new(gradient[2]);
            Vector<float> entry3 = new(gradient[3]);
            Vector<float> entry4 = new(gradient[4]);
            Vector<float> entry5 = new(gradient[5]);
            Vector<float> entry6 = new(gradient[6]);
            Vector<float> entry7 = new(gradient[7]);

            if (Vector<float>.Count == 16)
            {
                Vector<float> values = new(texels);
                Vector<float> best = Vector.Abs(entry0 - values);
                Vector<int> indices = Vector<int>.Zero;

                Accumulate(values, entry1, 1, ref best, ref indices);
                Accumulate(values, entry2, 2, ref best, ref indices);
                Accumulate(values, entry3, 3, ref best, ref indices);
                Accumulate(values, entry4, 4, ref best, ref indices);
                Accumulate(values, entry5, 5, ref best, ref indices);
                Accumulate(values, entry6, 6, ref best, ref indices);
                Accumulate(values, entry7, 7, ref best, ref indices);

                Vector.Widen(indices, out Vector<long> lo, out Vector<long> hi);
                Vector<long> packWeights64 = new(PackWeightValues64);
                ulong packedLo = (ulong)Vector.Dot(lo, packWeights64);
                ulong packedHi = (ulong)Vector.Dot(hi, packWeights64);
                data |= (packedLo << 16) | (packedHi << 40);
            }
            else
            {
                Vector<int> packWeights = new(PackWeightValues);
                int shift = 16;

                while (texels.Length >= Vector<float>.Count)
                {
                    Vector<float> values = new(texels);
                    Vector<float> best = Vector.Abs(entry0 - values);
                    Vector<int> indices = Vector<int>.Zero;

                    Accumulate(values, entry1, 1, ref best, ref indices);
                    Accumulate(values, entry2, 2, ref best, ref indices);
                    Accumulate(values, entry3, 3, ref best, ref indices);
                    Accumulate(values, entry4, 4, ref best, ref indices);
                    Accumulate(values, entry5, 5, ref best, ref indices);
                    Accumulate(values, entry6, 6, ref best, ref indices);
                    Accumulate(values, entry7, 7, ref best, ref indices);

                    uint packed = (uint)Vector.Dot(indices, packWeights);
                    data |= (ulong)packed << shift;
                    shift += Vector<float>.Count * 3;
                    texels = texels.Slice(Vector<float>.Count);
                }
            }
        }
        else
        {
            for (int i = 0; i < TexelsPerBlock; i++)
            {
                float value = texels[i];
                float best = 100000.0f;
                uint bestIndex = 0;

                for (uint j = 0; j < 8; j++)
                {
                    float delta = MathF.Abs(gradient[(int)j] - value);
                    if (delta < best)
                    {
                        best = delta;
                        bestIndex = j;
                    }
                }

                data |= (ulong)bestIndex << ((3 * i) + 16);
            }
        }

        BinaryPrimitives.WriteUInt64LittleEndian(destination, data);
    }

    private static void Accumulate(
        Vector<float> values,
        Vector<float> entry,
        int index,
        ref Vector<float> best,
        ref Vector<int> indices)
    {
        Vector<float> delta = Vector.Abs(entry - values);
        Vector<int> better = Vector.LessThan(delta, best);
        best = Vector.ConditionalSelect(better, delta, best);
        indices = Vector.ConditionalSelect(better, new Vector<int>(index), indices);
    }

    private static void FindEndpoints(ReadOnlySpan<float> texels, out byte endpoint0, out byte endpoint1)
    {
        const float MinNorm = 0.0f;
        const float MaxNorm = 1.0f;

        float blockMax = texels[0];
        float blockMin = texels[0];

        for (int i = 0; i < TexelsPerBlock; i++)
        {
            if (texels[i] < blockMin)
            {
                blockMin = texels[i];
            }
            else if (texels[i] > blockMax)
            {
                blockMax = texels[i];
            }
        }

        // A block that already contains a boundary value uses the four interpolant mode so the boundary is
        // reproduced exactly.
        bool useFourInterpolants = blockMin == MinNorm || blockMax == MaxNorm;

        OptimizeAlpha(texels, useFourInterpolants ? 6 : 8, out float start, out float end);

        byte startValue = (byte)(start * 255.0f);
        byte endValue = (byte)(end * 255.0f);

        if (useFourInterpolants)
        {
            endpoint0 = startValue;
            endpoint1 = endValue;
        }
        else
        {
            endpoint0 = endValue;
            endpoint1 = startValue;
        }
    }

    private static void OptimizeAlpha(ReadOnlySpan<float> points, int steps, out float x, out float y)
    {
        ReadOnlySpan<float> pC = steps == 6 ? StepsC6 : StepsC8;
        ReadOnlySpan<float> pD = steps == 6 ? StepsD6 : StepsD8;

        const float MinValue = 0.0f;
        const float MaxValue = 1.0f;

        float fX = MaxValue;
        float fY = MinValue;

        if (steps == 8)
        {
            for (int i = 0; i < TexelsPerBlock; i++)
            {
                if (points[i] < fX)
                {
                    fX = points[i];
                }

                if (points[i] > fY)
                {
                    fY = points[i];
                }
            }
        }
        else
        {
            for (int i = 0; i < TexelsPerBlock; i++)
            {
                if (points[i] < fX && points[i] > MinValue)
                {
                    fX = points[i];
                }

                if (points[i] > fY && points[i] < MaxValue)
                {
                    fY = points[i];
                }
            }

            if (fX == fY)
            {
                fY = MaxValue;
            }
        }

        float stepCount = steps - 1;

        for (int iteration = 0; iteration < 8; iteration++)
        {
            if (fY - fX < 1.0f / 256.0f)
            {
                break;
            }

            float scale = stepCount / (fY - fX);

            float dX = 0.0f;
            float dY = 0.0f;
            float d2X = 0.0f;
            float d2Y = 0.0f;

            for (int i = 0; i < TexelsPerBlock; i++)
            {
                float point = points[i];
                float dot = (point - fX) * scale;

                int step;
                if (dot <= 0.0f)
                {
                    step = steps == 6 && point <= (fX + MinValue) * 0.5f ? 6 : 0;
                }
                else if (dot >= stepCount)
                {
                    step = steps == 6 && point >= (fY + MaxValue) * 0.5f ? 7 : steps - 1;
                }
                else
                {
                    step = (int)(dot + 0.5f);
                }

                if ((uint)step < (uint)steps)
                {
                    float c = pC[step];
                    float d = pD[step];
                    float diff = (c * fX) + (d * fY) - point;

                    dX += c * diff;
                    d2X += c * c;

                    dY += d * diff;
                    d2Y += d * d;
                }
            }

            if (d2X > 0.0f)
            {
                fX -= dX / d2X;
            }

            if (d2Y > 0.0f)
            {
                fY -= dY / d2Y;
            }

            if (fX > fY)
            {
                (fX, fY) = (fY, fX);
            }

            if (dX * dX < 1.0f / 64.0f && dY * dY < 1.0f / 64.0f)
            {
                break;
            }
        }

        x = fX < MinValue ? MinValue : fX > MaxValue ? MaxValue : fX;
        y = fY < MinValue ? MinValue : fY > MaxValue ? MaxValue : fY;
    }

    private static void BuildGradient(byte endpoint0, byte endpoint1, Span<float> gradient)
    {
        ReadOnlySpan<float> div255 = BcUnits.Div255;
        float first = div255[endpoint0];
        float second = div255[endpoint1];
        gradient[0] = first;
        gradient[1] = second;

        if (endpoint0 > endpoint1)
        {
            for (int i = 1; i <= 6; i++)
            {
                gradient[i + 1] = ((first * (7 - i)) + (second * i)) / 7.0f;
            }
        }
        else
        {
            for (int i = 1; i <= 4; i++)
            {
                gradient[i + 1] = ((first * (5 - i)) + (second * i)) / 5.0f;
            }

            gradient[6] = 0.0f;
            gradient[7] = 1.0f;
        }
    }

    /// <summary>
    ///     Encodes one BC4 block.
    /// </summary>
    internal struct Block : IBcBlockEncoder<Block>
    {
        /// <inheritdoc />
        public static int BlockBytes => Bc4Encoder.BlockBytes;

        /// <inheritdoc />
        public static void EncodeBlock(
            ReadOnlySpan<byte> block,
            Span<byte> destination,
            Span<float> scratch,
            BcEncodeOptions options)
        {
            Span<float> texels = scratch[..TexelsPerBlock];
            Span<float> gradient = scratch.Slice(TexelsPerBlock, 8);

            for (int i = 0; i < TexelsPerBlock; i++)
            {
                // DirectXTex loads the source byte as `value * (1.0f / 255.0f)` (XMLoadUByteN4), not as a
                // division: the two differ by one ulp for 126 of the 256 byte values, which flips the tie
                // breaks in the endpoint search and stops the payload matching texconv's.
                texels[i] = block[i * 4] * ByteToUnit;
            }

            EncodeChannel(texels, gradient, destination);
        }
    }
}
