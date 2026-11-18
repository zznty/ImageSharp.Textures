using System.Buffers.Binary;
using ImageSharp.Textures.Texels.BcCommon;

namespace ImageSharp.Textures.Texels.Bc1;

/// <summary>
///     Encodes RGBA32 texels into BC1_UNorm payloads.
/// </summary>
/// <remarks>
///     A direct port of DirectXTex's <c>D3DXEncodeBC1</c> with dithering disabled: the block is quantized to
///     RGB565, the endpoints are refined with the 6D Newton solver on the luminance weighted colour axis,
///     quantized again and finally each texel is assigned the nearest index by projection onto the endpoint
///     axis. Blocks that contain a texel below the alpha threshold use the three colour punch-through mode.
/// </remarks>
internal static class Bc1Encoder
{
    /// <summary>
    ///     The encoded size, in bytes, of one block.
    /// </summary>
    internal const int BlockBytes = 8;

    private const int TexelsPerBlock = 16;

    private const float LuminanceR = 0.2125f / 0.7154f;
    private const float LuminanceG = 1.0f;
    private const float LuminanceB = 0.0721f / 0.7154f;
    private const float LuminanceInvR = 0.7154f / 0.2125f;
    private const float LuminanceInvG = 1.0f;
    private const float LuminanceInvB = 0.7154f / 0.0721f;

    // The smallest normal float, matching the FLT_MIN comparison of the reference implementation.
    private const float SmallestNormal = 1.17549435E-38f;

    private static ReadOnlySpan<float> StepsC3 => [1.0f, 1.0f / 2.0f, 0.0f / 2.0f];

    private static ReadOnlySpan<float> StepsD3 => [0.0f, 1.0f / 2.0f, 2.0f / 2.0f];

    private static ReadOnlySpan<float> StepsC4 => [3.0f / 3.0f, 2.0f / 3.0f, 1.0f / 3.0f, 0.0f / 3.0f];

    private static ReadOnlySpan<float> StepsD4 => [0.0f, 1.0f / 3.0f, 2.0f / 3.0f, 3.0f / 3.0f];

    private static ReadOnlySpan<int> IndexOrder3 => [0, 2, 1];

    private static ReadOnlySpan<int> IndexOrder4 => [0, 2, 3, 1];

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

    private static void OptimizeRgb(
        ReadOnlySpan<float> pointsR,
        ReadOnlySpan<float> pointsG,
        ReadOnlySpan<float> pointsB,
        int steps,
        out Color3 x,
        out Color3 y)
    {
        const float Epsilon = 0.25f / 64.0f * (0.25f / 64.0f);

        ReadOnlySpan<float> pC = steps == 3 ? StepsC3 : StepsC4;
        ReadOnlySpan<float> pD = steps == 3 ? StepsD3 : StepsD4;

        // Find the min and max points as the starting endpoints, seeded with the luminance axis.
        x = new Color3(LuminanceR, LuminanceG, LuminanceB);
        y = new Color3(0.0f, 0.0f, 0.0f);

        for (int i = 0; i < TexelsPerBlock; i++)
        {
            float pr = pointsR[i];
            float pg = pointsG[i];
            float pb = pointsB[i];

            if (pr < x.R)
            {
                x.R = pr;
            }

            if (pg < x.G)
            {
                x.G = pg;
            }

            if (pb < x.B)
            {
                x.B = pb;
            }

            if (pr > y.R)
            {
                y.R = pr;
            }

            if (pg > y.G)
            {
                y.G = pg;
            }

            if (pb > y.B)
            {
                y.B = pb;
            }
        }

        Color3 ab = new(y.R - x.R, y.G - x.G, y.B - x.B);
        float fab = (ab.R * ab.R) + (ab.G * ab.G) + (ab.B * ab.B);

        if (fab < SmallestNormal)
        {
            return;
        }

        float fabInv = 1.0f / fab;
        Color3 dir = new(ab.R * fabInv, ab.G * fabInv, ab.B * fabInv);
        Color3 mid = new((x.R + y.R) * 0.5f, (x.G + y.G) * 0.5f, (x.B + y.B) * 0.5f);

        float sum0 = 0.0f;
        float sum1 = 0.0f;
        float sum2 = 0.0f;
        float sum3 = 0.0f;

        for (int i = 0; i < TexelsPerBlock; i++)
        {
            float or = (pointsR[i] - mid.R) * dir.R;
            float og = (pointsG[i] - mid.G) * dir.G;
            float ob = (pointsB[i] - mid.B) * dir.B;

            float f = or + og + ob;
            sum0 += f * f;

            f = or + og - ob;
            sum1 += f * f;

            f = or - og + ob;
            sum2 += f * f;

            f = or - og - ob;
            sum3 += f * f;
        }

        float directionMax = sum0;
        int directionIndex = 0;

        if (sum1 > directionMax)
        {
            directionMax = sum1;
            directionIndex = 1;
        }

        if (sum2 > directionMax)
        {
            directionMax = sum2;
            directionIndex = 2;
        }

        if (sum3 > directionMax)
        {
            directionIndex = 3;
        }

        if ((directionIndex & 2) != 0)
        {
            (x.G, y.G) = (y.G, x.G);
        }

        if ((directionIndex & 1) != 0)
        {
            (x.B, y.B) = (y.B, x.B);
        }

        if (fab < 1.0f / 4096.0f)
        {
            return;
        }

        float stepCount = steps - 1;

        for (int iteration = 0; iteration < 8; iteration++)
        {
            dir = new Color3(y.R - x.R, y.G - x.G, y.B - x.B);
            float length = (dir.R * dir.R) + (dir.G * dir.G) + (dir.B * dir.B);

            if (length < 1.0f / 4096.0f)
            {
                break;
            }

            float scale = stepCount / length;
            dir = new Color3(dir.R * scale, dir.G * scale, dir.B * scale);

            float d2X = 0.0f;
            float d2Y = 0.0f;
            Color3 dX = default;
            Color3 dY = default;

            for (int i = 0; i < TexelsPerBlock; i++)
            {
                float pr = pointsR[i];
                float pg = pointsG[i];
                float pb = pointsB[i];
                float dot = ((pr - x.R) * dir.R)
                            + ((pg - x.G) * dir.G)
                            + ((pb - x.B) * dir.B);

                int step;
                if (dot <= 0.0f)
                {
                    step = 0;
                }
                else if (dot >= stepCount)
                {
                    step = steps - 1;
                }
                else
                {
                    step = (int)(dot + 0.5f);
                }

                float c = pC[step];
                float d = pD[step];

                Color3 diff = new(
                    (x.R * c) + (y.R * d) - pr,
                    (x.G * c) + (y.G * d) - pg,
                    (x.B * c) + (y.B * d) - pb);

                float fC = c * (1.0f / 8.0f);
                float fD = d * (1.0f / 8.0f);

                d2X += fC * c;
                dX.R += fC * diff.R;
                dX.G += fC * diff.G;
                dX.B += fC * diff.B;

                d2Y += fD * d;
                dY.R += fD * diff.R;
                dY.G += fD * diff.G;
                dY.B += fD * diff.B;
            }

            if (d2X > 0.0f)
            {
                float f = -1.0f / d2X;
                x.R += dX.R * f;
                x.G += dX.G * f;
                x.B += dX.B * f;
            }

            if (d2Y > 0.0f)
            {
                float f = -1.0f / d2Y;
                y.R += dY.R * f;
                y.G += dY.G * f;
                y.B += dY.B * f;
            }

            if (dX.R * dX.R < Epsilon && dX.G * dX.G < Epsilon && dX.B * dX.B < Epsilon
                && dY.R * dY.R < Epsilon && dY.G * dY.G < Epsilon && dY.B * dY.B < Epsilon)
            {
                break;
            }
        }
    }

    private static ushort Encode565(Color3 color)
    {
        float r = color.R < 0.0f ? 0.0f : color.R > 1.0f ? 1.0f : color.R;
        float g = color.G < 0.0f ? 0.0f : color.G > 1.0f ? 1.0f : color.G;
        float b = color.B < 0.0f ? 0.0f : color.B > 1.0f ? 1.0f : color.B;

        return (ushort)(((int)((r * 31.0f) + 0.5f) << 11)
                        | ((int)((g * 63.0f) + 0.5f) << 5)
                        | (int)((b * 31.0f) + 0.5f));
    }

    private static Color3 Decode565(ushort value)
        => new(
            ((value >> 11) & 31) * (1.0f / 31.0f),
            ((value >> 5) & 63) * (1.0f / 63.0f),
            (value & 31) * (1.0f / 31.0f));

    private static void WriteBlock(Span<byte> destination, ushort color0, ushort color1, uint bits)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(destination, color0);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], color1);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], bits);
    }

    /// <summary>
    ///     Encodes one BC1 block.
    /// </summary>
    internal struct Block : IBcBlockEncoder<Block>
    {
        /// <inheritdoc />
        public static int BlockBytes => Bc1Encoder.BlockBytes;

        /// <inheritdoc />
        public static void EncodeBlock(
            ReadOnlySpan<byte> block,
            Span<byte> destination,
            Span<float> scratch,
            BcEncodeOptions options)
        {
            float threshold = options.Bc1AlphaThreshold;
            ReadOnlySpan<float> div255 = BcUnits.Div255;

            int colorKeyCount = 0;

            for (int i = 0; i < TexelsPerBlock; i++)
            {
                if (div255[block[(i * 4) + 3]] < threshold)
                {
                    colorKeyCount++;
                }
            }

            if (colorKeyCount == TexelsPerBlock)
            {
                // A fully transparent block is encoded as black to white with every texel punched through.
                WriteBlock(destination, 0x0000, 0xFFFF, 0xFFFFFFFF);
                return;
            }

            int steps = colorKeyCount > 0 ? 3 : 4;

            // Quantize the block to RGB565 and weight it by the luminance axis.
            Span<float> pointsR = scratch[..TexelsPerBlock];
            Span<float> pointsG = scratch.Slice(TexelsPerBlock, TexelsPerBlock);
            Span<float> pointsB = scratch.Slice(TexelsPerBlock * 2, TexelsPerBlock);
            ReadOnlySpan<float> quantR = Bc1Tables.QuantizedLuminanceR;
            ReadOnlySpan<float> quantG = Bc1Tables.QuantizedLuminanceG;
            ReadOnlySpan<float> quantB = Bc1Tables.QuantizedLuminanceB;

            for (int i = 0; i < TexelsPerBlock; i++)
            {
                pointsR[i] = quantR[block[i * 4]];
                pointsG[i] = quantG[block[(i * 4) + 1]];
                pointsB[i] = quantB[block[(i * 4) + 2]];
            }

            OptimizeRgb(pointsR, pointsG, pointsB, steps, out Color3 colorA, out Color3 colorB);

            ushort colorWordA = Encode565(new Color3(
                colorA.R * LuminanceInvR,
                colorA.G * LuminanceInvG,
                colorA.B * LuminanceInvB));
            ushort colorWordB = Encode565(new Color3(
                colorB.R * LuminanceInvR,
                colorB.G * LuminanceInvG,
                colorB.B * LuminanceInvB));

            if (steps == 4 && colorWordA == colorWordB)
            {
                WriteBlock(destination, colorWordA, colorWordB, 0);
                return;
            }

            Color3 decoded0 = Decode565(colorWordA);
            Color3 decoded1 = Decode565(colorWordB);
            colorA = new Color3(
                decoded0.R * LuminanceR,
                decoded0.G * LuminanceG,
                decoded0.B * LuminanceB);
            colorB = new Color3(
                decoded1.R * LuminanceR,
                decoded1.G * LuminanceG,
                decoded1.B * LuminanceB);

            ushort color0;
            ushort color1;
            Color3 step0;
            Color3 step1;

            if (steps == 3 == (colorWordA <= colorWordB))
            {
                color0 = colorWordA;
                color1 = colorWordB;
                step0 = colorA;
                step1 = colorB;
            }
            else
            {
                color0 = colorWordB;
                color1 = colorWordA;
                step0 = colorB;
                step1 = colorA;
            }

            // The index order differs between the three and four colour modes: the punch-through index is
            // always the last one.
            ReadOnlySpan<int> indexOrder = steps == 3 ? IndexOrder3 : IndexOrder4;

            Color3 dir = new(step1.R - step0.R, step1.G - step0.G, step1.B - step0.B);
            float stepCount = steps - 1;
            float dirLengthSquared = (dir.R * dir.R) + (dir.G * dir.G) + (dir.B * dir.B);
            float scale = colorWordA != colorWordB ? stepCount / dirLengthSquared : 0.0f;
            dir = new Color3(dir.R * scale, dir.G * scale, dir.B * scale);

            uint bits = 0;
            ReadOnlySpan<float> lumR = Bc1Tables.Div255LuminanceR;
            ReadOnlySpan<float> lumG = Bc1Tables.Div255LuminanceG;
            ReadOnlySpan<float> lumB = Bc1Tables.Div255LuminanceB;

            for (int i = 0; i < TexelsPerBlock; i++)
            {
                if (steps == 3 && div255[block[(i * 4) + 3]] < threshold)
                {
                    bits = (3u << 30) | (bits >> 2);
                    continue;
                }

                Color3 color = new(
                    lumR[block[i * 4]],
                    lumG[block[(i * 4) + 1]],
                    lumB[block[(i * 4) + 2]]);

                float dot = ((color.R - step0.R) * dir.R)
                            + ((color.G - step0.G) * dir.G)
                            + ((color.B - step0.B) * dir.B);

                int index;
                if (dot <= 0.0f)
                {
                    index = 0;
                }
                else if (dot >= stepCount)
                {
                    index = 1;
                }
                else
                {
                    index = indexOrder[(int)(dot + 0.5f)];
                }

                bits = ((uint)index << 30) | (bits >> 2);
            }

            WriteBlock(destination, color0, color1, bits);
        }
    }

    /// <summary>
    ///     A three component float colour used by the endpoint solver.
    /// </summary>
    private struct Color3
    {
        internal float R;
        internal float G;
        internal float B;

        internal Color3(float r, float g, float b)
        {
            this.R = r;
            this.G = g;
            this.B = b;
        }
    }
}
