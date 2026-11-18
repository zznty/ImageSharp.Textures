using ILGPU;

namespace ImageSharp.Textures.ILGPU;

/// <summary>
///     Device kernels and the integer helpers they share for the BC7 encoder.
/// </summary>
/// <remarks>
///     <para>
///         The kernels are a direct port of the DirectXTex <c>BC7Encode.hlsl</c> compute pipeline. A group of 64 threads
///         mirrors the shader thread group and the per-block scratch lives in shared memory exactly as the
///         <c>groupshared</c> buffer of the reference does. The lookup tables are read from a flattened device buffer
///         because kernels cannot resolve host-side array lookups.
///     </para>
///     <para>
///         All colour arithmetic is unsigned eight-bit component arithmetic widened to 32 bits, so the modular
///         arithmetic of the shader and the plain integer arithmetic below agree: every intermediate value of a dot
///         product or squared distance is far below <c>2^31</c>.
///     </para>
/// </remarks>
internal static class Bc7Kernels
{
    /// <summary>The number of threads in one kernel group.</summary>
    internal const int GroupSize = 64;

    /// <summary>The number of texels in one BC7 block.</summary>
    internal const int BlockSize = 16;

    /// <summary>The number of blocks one group processes in the single-subset kernels.</summary>
    internal const int BlocksPerGroup = GroupSize / BlockSize;

    /// <summary>
    ///     Tries modes 4, 5, and 6 for four blocks per group and records the best candidate of each block.
    /// </summary>
    /// <param name="source">The tightly packed RGBA32 surface, one word per texel.</param>
    /// <param name="candidates">The candidate buffer, four words per block.</param>
    /// <param name="tables">The flat BC7 lookup tables.</param>
    /// <param name="width">The surface width, in texels.</param>
    /// <param name="height">The surface height, in texels.</param>
    /// <param name="numBlockX">The number of blocks along the x axis.</param>
    /// <param name="numBlocks">The total number of blocks.</param>
    internal static void TryMode456(
        ArrayView<uint> source,
        ArrayView<uint> candidates,
        ArrayView<uint> tables,
        int width,
        int height,
        int numBlockX,
        int numBlocks)
    {
        ArrayView<uint> pixelShared = SharedMemory.Allocate<uint>(GroupSize * 4);
        ArrayView<uint> lowShared = SharedMemory.Allocate<uint>(GroupSize * 4);
        ArrayView<uint> highShared = SharedMemory.Allocate<uint>(GroupSize * 4);
        ArrayView<uint> errorShared = SharedMemory.Allocate<uint>(GroupSize);
        ArrayView<uint> modeShared = SharedMemory.Allocate<uint>(GroupSize);
        ArrayView<uint> selectorShared = SharedMemory.Allocate<uint>(GroupSize);
        ArrayView<uint> rotationShared = SharedMemory.Allocate<uint>(GroupSize);

        int gi = Group.IdxX;
        int blockInGroup = gi / BlockSize;
        int threadInBlock = gi - (blockInGroup * BlockSize);
        int blockID = (Grid.IdxX * BlocksPerGroup) + blockInGroup;
        int threadBase = blockInGroup * BlockSize;
        int slot = gi * 4;

        int blockY = blockID / numBlockX;
        int blockX = blockID - (blockY * numBlockX);

        uint packed = LoadInputPixel(source, width, height, blockX, blockY, threadInBlock);
        uint red = packed & 0xFFu;
        uint green = (packed >> 8) & 0xFFu;
        uint blue = (packed >> 16) & 0xFFu;
        uint alpha = (packed >> 24) & 0xFFu;

        pixelShared[slot] = red;
        pixelShared[slot + 1] = green;
        pixelShared[slot + 2] = blue;
        pixelShared[slot + 3] = alpha;
        lowShared[slot] = red;
        lowShared[slot + 1] = green;
        lowShared[slot + 2] = blue;
        lowShared[slot + 3] = alpha;
        highShared[slot] = red;
        highShared[slot + 1] = green;
        highShared[slot + 2] = blue;
        highShared[slot + 3] = alpha;

        Group.Barrier();

        if (threadInBlock < 8)
        {
            ReduceMinMax(lowShared, highShared, slot, slot + (8 * 4));
        }

        Group.Barrier();

        if (threadInBlock < 4)
        {
            ReduceMinMax(lowShared, highShared, slot, slot + (4 * 4));
        }

        Group.Barrier();

        if (threadInBlock < 2)
        {
            ReduceMinMax(lowShared, highShared, slot, slot + (2 * 4));
        }

        Group.Barrier();

        if (threadInBlock < 1)
        {
            ReduceMinMax(lowShared, highShared, slot, slot + 4);
        }

        Group.Barrier();

        int blockSlot = threadBase * 4;
        EndpointPair endpoints;
        endpoints.R0 = lowShared[blockSlot];
        endpoints.G0 = lowShared[blockSlot + 1];
        endpoints.B0 = lowShared[blockSlot + 2];
        endpoints.A0 = lowShared[blockSlot + 3];
        endpoints.R1 = highShared[blockSlot];
        endpoints.G1 = highShared[blockSlot + 1];
        endpoints.B1 = highShared[blockSlot + 2];
        endpoints.A1 = highShared[blockSlot + 3];

        uint error = 0xFFFFFFFFu;
        uint mode = 0;
        uint indexSelector = 0;
        uint rotation = 0;
        int indexPrecX = 2;
        int indexPrecY = 2;

        if (threadInBlock < 8)
        {
            if ((threadInBlock & 1) == 0)
            {
                indexSelector = 0;
                indexPrecX = 2;
                indexPrecY = 1;
            }
            else
            {
                indexSelector = 1;
                indexPrecX = 1;
                indexPrecY = 2;
            }
        }

        if (threadInBlock < 12)
        {
            if (threadInBlock is < 2 or 8)
            {
                rotation = 0;
            }
            else if (threadInBlock is < 4 or 9)
            {
                rotation = 1;
                (endpoints.R0, endpoints.A0) = (endpoints.A0, endpoints.R0);
                (endpoints.R1, endpoints.A1) = (endpoints.A1, endpoints.R1);
            }
            else if (threadInBlock is < 6 or 10)
            {
                rotation = 2;
                (endpoints.G0, endpoints.A0) = (endpoints.A0, endpoints.G0);
                (endpoints.G1, endpoints.A1) = (endpoints.A1, endpoints.G1);
            }
            else
            {
                rotation = 3;
                (endpoints.B0, endpoints.A0) = (endpoints.A0, endpoints.B0);
                (endpoints.B1, endpoints.A1) = (endpoints.A1, endpoints.B1);
            }

            mode = threadInBlock < 8 ? 4u : 5u;
            EndpointCompression compressed = Compress(mode, endpoints, 0, 0);
            endpoints = compressed.Unquantized;

            uint pixelR = pixelShared[blockSlot];
            uint pixelG = pixelShared[blockSlot + 1];
            uint pixelB = pixelShared[blockSlot + 2];
            uint pixelA = pixelShared[blockSlot + 3];

            if (rotation == 1)
            {
                (pixelR, pixelA) = (pixelA, pixelR);
            }
            else if (rotation == 2)
            {
                (pixelG, pixelA) = (pixelA, pixelG);
            }
            else if (rotation == 3)
            {
                (pixelB, pixelA) = (pixelA, pixelB);
            }

            int spanR = (int)endpoints.R1 - (int)endpoints.R0;
            int spanG = (int)endpoints.G1 - (int)endpoints.G0;
            int spanB = (int)endpoints.B1 - (int)endpoints.B0;
            int spanA = (int)endpoints.A1 - (int)endpoints.A0;
            int normRgb = (spanR * spanR) + (spanG * spanG) + (spanB * spanB);
            int normAlpha = spanA * spanA;

            int distance0 = Squared(pixelR - endpoints.R0) + Squared(pixelG - endpoints.G0) +
                            Squared(pixelB - endpoints.B0);
            int distance1 = Squared(pixelR - endpoints.R1) + Squared(pixelG - endpoints.G1) +
                            Squared(pixelB - endpoints.B1);
            if (distance0 > distance1)
            {
                spanR = -spanR;
                spanG = -spanG;
                spanB = -spanB;
                endpoints = SwapRgb(endpoints);
            }

            distance0 = Squared(pixelA - endpoints.A0);
            distance1 = Squared(pixelA - endpoints.A1);
            if (distance0 > distance1)
            {
                spanA = -spanA;
                (endpoints.A0, endpoints.A1) = (endpoints.A1, endpoints.A0);
            }

            error = 0;

            for (int i = 0; i < BlockSize; i++)
            {
                int pixelSlot = (threadBase + i) * 4;
                uint currentR = pixelShared[pixelSlot];
                uint currentG = pixelShared[pixelSlot + 1];
                uint currentB = pixelShared[pixelSlot + 2];
                uint currentA = pixelShared[pixelSlot + 3];

                if (rotation == 1)
                {
                    (currentR, currentA) = (currentA, currentR);
                }
                else if (rotation == 2)
                {
                    (currentG, currentA) = (currentA, currentG);
                }
                else if (rotation == 3)
                {
                    (currentB, currentA) = (currentA, currentB);
                }

                int colorDot = (spanR * ((int)currentR - (int)endpoints.R0)) +
                               (spanG * ((int)currentG - (int)endpoints.G0)) +
                               (spanB * ((int)currentB - (int)endpoints.B0));
                int alphaDot = spanA * ((int)currentA - (int)endpoints.A0);
                uint colorIndex = StepIndex(colorDot, normRgb, indexPrecX, tables);
                uint alphaIndex = StepIndex(alphaDot, normAlpha, indexPrecY, tables);
                uint colorWeight = GetWeight(indexPrecX, colorIndex, tables);
                uint alphaWeight = GetWeight(indexPrecY, alphaIndex, tables);

                uint resultR = (((64u - colorWeight) * endpoints.R0) + (colorWeight * endpoints.R1) + 32u) >> 6;
                uint resultG = (((64u - colorWeight) * endpoints.G0) + (colorWeight * endpoints.G1) + 32u) >> 6;
                uint resultB = (((64u - colorWeight) * endpoints.B0) + (colorWeight * endpoints.B1) + 32u) >> 6;
                uint resultA = (((64u - alphaWeight) * endpoints.A0) + (alphaWeight * endpoints.A1) + 32u) >> 6;

                error += (uint)(Squared(resultR, currentR) + Squared(resultG, currentG) + Squared(resultB, currentB) +
                                Squared(resultA, currentA));
            }
        }
        else
        {
            uint p = (uint)(threadInBlock - 12);
            EndpointCompression compressed = Compress(6, endpoints, p & 1u, (p >> 1) & 1u);
            endpoints = compressed.Unquantized;

            uint pixelR = pixelShared[blockSlot];
            uint pixelG = pixelShared[blockSlot + 1];
            uint pixelB = pixelShared[blockSlot + 2];
            uint pixelA = pixelShared[blockSlot + 3];

            int spanR = (int)endpoints.R1 - (int)endpoints.R0;
            int spanG = (int)endpoints.G1 - (int)endpoints.G0;
            int spanB = (int)endpoints.B1 - (int)endpoints.B0;
            int spanA = (int)endpoints.A1 - (int)endpoints.A0;
            int norm = (spanR * spanR) + (spanG * spanG) + (spanB * spanB) + (spanA * spanA);
            int dot = (spanR * ((int)pixelR - (int)endpoints.R0)) + (spanG * ((int)pixelG - (int)endpoints.G0)) +
                      (spanB * ((int)pixelB - (int)endpoints.B0)) + (spanA * ((int)pixelA - (int)endpoints.A0));

            if (norm > 0 && dot >= 0 && (uint)(dot * 63.49999f) > (uint)(32 * norm))
            {
                spanR = -spanR;
                spanG = -spanG;
                spanB = -spanB;
                spanA = -spanA;

                (endpoints.R0, endpoints.R1) = (endpoints.R1, endpoints.R0);
                (endpoints.G0, endpoints.G1) = (endpoints.G1, endpoints.G0);
                (endpoints.B0, endpoints.B1) = (endpoints.B1, endpoints.B0);
                (endpoints.A0, endpoints.A1) = (endpoints.A1, endpoints.A0);
            }

            error = 0;

            for (int i = 0; i < BlockSize; i++)
            {
                int pixelSlot = (threadBase + i) * 4;
                uint currentR = pixelShared[pixelSlot];
                uint currentG = pixelShared[pixelSlot + 1];
                uint currentB = pixelShared[pixelSlot + 2];
                uint currentA = pixelShared[pixelSlot + 3];

                int delta = (spanR * ((int)currentR - (int)endpoints.R0)) +
                            (spanG * ((int)currentG - (int)endpoints.G0)) +
                            (spanB * ((int)currentB - (int)endpoints.B0)) +
                            (spanA * ((int)currentA - (int)endpoints.A0));
                uint colorIndex = StepIndex(delta, norm, 0, tables);
                uint weight = GetWeight(0, colorIndex, tables);

                uint resultR = (((64u - weight) * endpoints.R0) + (weight * endpoints.R1) + 32u) >> 6;
                uint resultG = (((64u - weight) * endpoints.G0) + (weight * endpoints.G1) + 32u) >> 6;
                uint resultB = (((64u - weight) * endpoints.B0) + (weight * endpoints.B1) + 32u) >> 6;
                uint resultA = (((64u - weight) * endpoints.A0) + (weight * endpoints.A1) + 32u) >> 6;

                error += (uint)(Squared(resultR, currentR) + Squared(resultG, currentG) + Squared(resultB, currentB) +
                                Squared(resultA, currentA));
            }

            mode = 6;
            rotation = p;
        }

        errorShared[gi] = error;
        modeShared[gi] = mode;
        selectorShared[gi] = indexSelector;
        rotationShared[gi] = rotation;

        Group.Barrier();

        if (threadInBlock < 8)
        {
            KeepBest(errorShared, modeShared, selectorShared, rotationShared, gi, gi + 8);
        }

        Group.Barrier();

        if (threadInBlock < 4)
        {
            KeepBest(errorShared, modeShared, selectorShared, rotationShared, gi, gi + 4);
        }

        Group.Barrier();

        if (threadInBlock < 2)
        {
            KeepBest(errorShared, modeShared, selectorShared, rotationShared, gi, gi + 2);
        }

        Group.Barrier();

        if (threadInBlock < 1)
        {
            KeepBest(errorShared, modeShared, selectorShared, rotationShared, gi, gi + 1);

            if (blockID < numBlocks)
            {
                int candidateSlot = blockID * 4;
                candidates[candidateSlot] = errorShared[gi];
                candidates[candidateSlot + 1] = (selectorShared[gi] << 31) | modeShared[gi];
                candidates[candidateSlot + 2] = 0;
                candidates[candidateSlot + 3] = rotationShared[gi];
            }
        }
    }

    /// <summary>
    ///     Tries one of the two-subset modes 1, 3, or 7 for a single block per group, one partition per thread.
    /// </summary>
    /// <param name="source">The tightly packed RGBA32 surface, one word per texel.</param>
    /// <param name="input">The candidate buffer produced by the previous pass.</param>
    /// <param name="output">The destination candidate buffer.</param>
    /// <param name="tables">The flat BC7 lookup tables.</param>
    /// <param name="width">The surface width, in texels.</param>
    /// <param name="height">The surface height, in texels.</param>
    /// <param name="numBlockX">The number of blocks along the x axis.</param>
    /// <param name="numBlocks">The total number of blocks.</param>
    /// <param name="modeId">The mode under test, one of 1, 3, or 7.</param>
    internal static void TryMode137(
        ArrayView<uint> source,
        ArrayView<uint> input,
        ArrayView<uint> output,
        ArrayView<uint> tables,
        int width,
        int height,
        int numBlockX,
        int numBlocks,
        uint modeId)
    {
        ArrayView<uint> pixelShared = SharedMemory.Allocate<uint>(GroupSize * 4);
        ArrayView<uint> errorShared = SharedMemory.Allocate<uint>(GroupSize);
        ArrayView<uint> modeShared = SharedMemory.Allocate<uint>(GroupSize);
        ArrayView<uint> partitionShared = SharedMemory.Allocate<uint>(GroupSize);
        ArrayView<uint> rotationShared = SharedMemory.Allocate<uint>(GroupSize);

        int gi = Group.IdxX;
        int blockID = Grid.IdxX;
        int slot = gi * 4;

        int blockY = blockID / numBlockX;
        int blockX = blockID - (blockY * numBlockX);

        if (gi < BlockSize)
        {
            uint packed = LoadInputPixel(source, width, height, blockX, blockY, gi);
            pixelShared[slot] = packed & 0xFFu;
            pixelShared[slot + 1] = (packed >> 8) & 0xFFu;
            pixelShared[slot + 2] = (packed >> 16) & 0xFFu;
            pixelShared[slot + 3] = (packed >> 24) & 0xFFu;
        }

        Group.Barrier();

        uint partition = (uint)gi;
        uint bits = tables[Bc7Tables.SectionBits + (int)partition];

        uint low0R = 0xFFFFFFFFu;
        uint low0G = 0xFFFFFFFFu;
        uint low0B = 0xFFFFFFFFu;
        uint low0A = 0xFFFFFFFFu;
        uint high0R = 0;
        uint high0G = 0;
        uint high0B = 0;
        uint high0A = 0;
        uint low1R = 0xFFFFFFFFu;
        uint low1G = 0xFFFFFFFFu;
        uint low1B = 0xFFFFFFFFu;
        uint low1A = 0xFFFFFFFFu;
        uint high1R = 0;
        uint high1G = 0;
        uint high1B = 0;
        uint high1A = 0;

        for (int i = 0; i < BlockSize; i++)
        {
            int pixelSlot = i * 4;
            uint currentR = pixelShared[pixelSlot];
            uint currentG = pixelShared[pixelSlot + 1];
            uint currentB = pixelShared[pixelSlot + 2];
            uint currentA = pixelShared[pixelSlot + 3];

            if (((bits >> i) & 1u) == 1u)
            {
                low1R = Min(low1R, currentR);
                low1G = Min(low1G, currentG);
                low1B = Min(low1B, currentB);
                low1A = Min(low1A, currentA);
                high1R = Max(high1R, currentR);
                high1G = Max(high1G, currentG);
                high1B = Max(high1B, currentB);
                high1A = Max(high1A, currentA);
            }
            else
            {
                low0R = Min(low0R, currentR);
                low0G = Min(low0G, currentG);
                low0B = Min(low0B, currentB);
                low0A = Min(low0A, currentA);
                high0R = Max(high0R, currentR);
                high0G = Max(high0G, currentG);
                high0B = Max(high0B, currentB);
                high0A = Max(high0A, currentA);
            }
        }

        uint backupLow0R = low0R;
        uint backupLow0G = low0G;
        uint backupLow0B = low0B;
        uint backupLow0A = low0A;
        uint backupHigh0R = high0R;
        uint backupHigh0G = high0G;
        uint backupHigh0B = high0B;
        uint backupHigh0A = high0A;
        uint backupLow1R = low1R;
        uint backupLow1G = low1G;
        uint backupLow1B = low1B;
        uint backupLow1A = low1A;
        uint backupHigh1R = high1R;
        uint backupHigh1G = high1G;
        uint backupHigh1B = high1B;
        uint backupHigh1A = high1A;

        uint maxP = modeId == 1 ? 2u : 4u;
        int stepRow = modeId == 1 ? 1 : 2;
        uint finalP0 = 0;
        uint finalP1 = 0;
        uint bestError0 = 0xFFFFFFFFu;
        uint bestError1 = 0xFFFFFFFFu;
        uint fixUp = GetFixUpIndex1D(partition);

        for (uint p = 0; p < maxP; p++)
        {
            uint endpointP0 = modeId == 1 ? p : p & 1u;
            uint endpointP1 = modeId == 1 ? p : (p >> 1) & 1u;

            EndpointPair subset0;
            subset0.R0 = backupLow0R;
            subset0.G0 = backupLow0G;
            subset0.B0 = backupLow0B;
            subset0.A0 = backupLow0A;
            subset0.R1 = backupHigh0R;
            subset0.G1 = backupHigh0G;
            subset0.B1 = backupHigh0B;
            subset0.A1 = backupHigh0A;

            EndpointPair subset1;
            subset1.R0 = backupLow1R;
            subset1.G0 = backupLow1G;
            subset1.B0 = backupLow1B;
            subset1.A0 = backupLow1A;
            subset1.R1 = backupHigh1R;
            subset1.G1 = backupHigh1G;
            subset1.B1 = backupHigh1B;
            subset1.A1 = backupHigh1A;

            subset0 = Compress(modeId, subset0, endpointP0, endpointP1).Unquantized;
            subset1 = Compress(modeId, subset1, endpointP0, endpointP1).Unquantized;

            int span0R = (int)subset0.R1 - (int)subset0.R0;
            int span0G = (int)subset0.G1 - (int)subset0.G0;
            int span0B = (int)subset0.B1 - (int)subset0.B0;
            int span0A = modeId == 7 ? (int)subset0.A1 - (int)subset0.A0 : 0;
            int span1R = (int)subset1.R1 - (int)subset1.R0;
            int span1G = (int)subset1.G1 - (int)subset1.G0;
            int span1B = (int)subset1.B1 - (int)subset1.B0;
            int span1A = modeId == 7 ? (int)subset1.A1 - (int)subset1.A0 : 0;

            int norm0 = (span0R * span0R) + (span0G * span0G) + (span0B * span0B) + (span0A * span0A);
            int norm1 = (span1R * span1R) + (span1G * span1G) + (span1B * span1B) + (span1A * span1A);

            int pixelSlot = 0;
            int dot0 = (span0R * ((int)pixelShared[pixelSlot] - (int)subset0.R0)) +
                       (span0G * ((int)pixelShared[pixelSlot + 1] - (int)subset0.G0)) +
                       (span0B * ((int)pixelShared[pixelSlot + 2] - (int)subset0.B0)) +
                       (span0A * ((int)pixelShared[pixelSlot + 3] - (int)subset0.A0));
            if (norm0 > 0 && dot0 > 0 && (uint)(dot0 * 63.49999f) > (uint)(32 * norm0))
            {
                span0R = -span0R;
                span0G = -span0G;
                span0B = -span0B;
                span0A = -span0A;
                subset0 = SwapBoth(subset0);
            }

            pixelSlot = (int)fixUp * 4;
            int dot1 = (span1R * ((int)pixelShared[pixelSlot] - (int)subset1.R0)) +
                       (span1G * ((int)pixelShared[pixelSlot + 1] - (int)subset1.G0)) +
                       (span1B * ((int)pixelShared[pixelSlot + 2] - (int)subset1.B0)) +
                       (span1A * ((int)pixelShared[pixelSlot + 3] - (int)subset1.A0));
            if (norm1 > 0 && dot1 > 0 && (uint)(dot1 * 63.49999f) > (uint)(32 * norm1))
            {
                span1R = -span1R;
                span1G = -span1G;
                span1B = -span1B;
                span1A = -span1A;
                subset1 = SwapBoth(subset1);
            }

            uint error0 = 0;
            uint error1 = 0;

            for (int i = 0; i < BlockSize; i++)
            {
                pixelSlot = i * 4;
                uint currentR = pixelShared[pixelSlot];
                uint currentG = pixelShared[pixelSlot + 1];
                uint currentB = pixelShared[pixelSlot + 2];
                uint currentA = pixelShared[pixelSlot + 3];

                int delta;
                uint colorIndex;
                uint resultR;
                uint resultG;
                uint resultB;
                uint resultA;

                if (((bits >> i) & 1u) == 1u)
                {
                    delta = (span1R * ((int)currentR - (int)subset1.R0)) +
                            (span1G * ((int)currentG - (int)subset1.G0)) +
                            (span1B * ((int)currentB - (int)subset1.B0)) + (span1A * ((int)currentA - (int)subset1.A0));
                    colorIndex = StepIndex(delta, norm1, stepRow, tables);
                    uint weight = GetWeight(stepRow, colorIndex, tables);
                    resultR = (((64u - weight) * subset1.R0) + (weight * subset1.R1) + 32u) >> 6;
                    resultG = (((64u - weight) * subset1.G0) + (weight * subset1.G1) + 32u) >> 6;
                    resultB = (((64u - weight) * subset1.B0) + (weight * subset1.B1) + 32u) >> 6;
                    resultA = modeId == 7 ? (((64u - weight) * subset1.A0) + (weight * subset1.A1) + 32u) >> 6 : 255u;
                }
                else
                {
                    delta = (span0R * ((int)currentR - (int)subset0.R0)) +
                            (span0G * ((int)currentG - (int)subset0.G0)) +
                            (span0B * ((int)currentB - (int)subset0.B0)) + (span0A * ((int)currentA - (int)subset0.A0));
                    colorIndex = StepIndex(delta, norm0, stepRow, tables);
                    uint weight = GetWeight(stepRow, colorIndex, tables);
                    resultR = (((64u - weight) * subset0.R0) + (weight * subset0.R1) + 32u) >> 6;
                    resultG = (((64u - weight) * subset0.G0) + (weight * subset0.G1) + 32u) >> 6;
                    resultB = (((64u - weight) * subset0.B0) + (weight * subset0.B1) + 32u) >> 6;
                    resultA = modeId == 7 ? (((64u - weight) * subset0.A0) + (weight * subset0.A1) + 32u) >> 6 : 255u;
                }

                uint pixelError = (uint)(Squared(resultR, currentR) + Squared(resultG, currentG) +
                                         Squared(resultB, currentB) + Squared(resultA, currentA));

                if (((bits >> i) & 1u) == 1u)
                {
                    error1 += pixelError;
                }
                else
                {
                    error0 += pixelError;
                }
            }

            if (error0 < bestError0)
            {
                bestError0 = error0;
                finalP0 = p;
            }

            if (error1 < bestError1)
            {
                bestError1 = error1;
                finalP1 = p;
            }
        }

        errorShared[gi] = bestError0 + bestError1;
        modeShared[gi] = modeId;
        partitionShared[gi] = partition;
        rotationShared[gi] = modeId == 1 ? (finalP1 << 1) | finalP0 : (finalP1 << 2) | finalP0;

        Group.Barrier();

        if (gi < 32)
        {
            KeepBestPartition(errorShared, modeShared, partitionShared, rotationShared, gi, gi + 32);
        }

        Group.Barrier();

        if (gi < 16)
        {
            KeepBestPartition(errorShared, modeShared, partitionShared, rotationShared, gi, gi + 16);
        }

        Group.Barrier();

        if (gi < 8)
        {
            KeepBestPartition(errorShared, modeShared, partitionShared, rotationShared, gi, gi + 8);
        }

        Group.Barrier();

        if (gi < 4)
        {
            KeepBestPartition(errorShared, modeShared, partitionShared, rotationShared, gi, gi + 4);
        }

        Group.Barrier();

        if (gi < 2)
        {
            KeepBestPartition(errorShared, modeShared, partitionShared, rotationShared, gi, gi + 2);
        }

        Group.Barrier();

        if (gi < 1)
        {
            KeepBestPartition(errorShared, modeShared, partitionShared, rotationShared, gi, gi + 1);

            if (blockID < numBlocks)
            {
                int slotIndex = blockID * 4;
                if (input[slotIndex] > errorShared[gi])
                {
                    output[slotIndex] = errorShared[gi];
                    output[slotIndex + 1] = modeShared[gi];
                    output[slotIndex + 2] = partitionShared[gi];
                    output[slotIndex + 3] = rotationShared[gi];
                }
                else
                {
                    output[slotIndex] = input[slotIndex];
                    output[slotIndex + 1] = input[slotIndex + 1];
                    output[slotIndex + 2] = input[slotIndex + 2];
                    output[slotIndex + 3] = input[slotIndex + 3];
                }
            }
        }
    }

    /// <summary>
    ///     Packs the winning candidate of four blocks per group into 16-byte BC7 blocks.
    /// </summary>
    /// <param name="source">The tightly packed RGBA32 surface, one word per texel.</param>
    /// <param name="input">The candidate buffer produced by the final search pass.</param>
    /// <param name="blocks">The destination BC7 payload, four words per block.</param>
    /// <param name="tables">The flat BC7 lookup tables.</param>
    /// <param name="width">The surface width, in texels.</param>
    /// <param name="height">The surface height, in texels.</param>
    /// <param name="numBlockX">The number of blocks along the x axis.</param>
    /// <param name="numBlocks">The total number of blocks.</param>
    internal static void EncodeBlock(
        ArrayView<uint> source,
        ArrayView<uint> input,
        ArrayView<uint> blocks,
        ArrayView<uint> tables,
        int width,
        int height,
        int numBlockX,
        int numBlocks)
    {
        ArrayView<uint> pixelShared = SharedMemory.Allocate<uint>(GroupSize * 4);
        ArrayView<uint> lowShared = SharedMemory.Allocate<uint>(GroupSize * 4);
        ArrayView<uint> highShared = SharedMemory.Allocate<uint>(GroupSize * 4);
        ArrayView<uint> lowQuantizedShared = SharedMemory.Allocate<uint>(GroupSize * 4);
        ArrayView<uint> highQuantizedShared = SharedMemory.Allocate<uint>(GroupSize * 4);
        ArrayView<uint> colorIndexShared = SharedMemory.Allocate<uint>(GroupSize);
        ArrayView<uint> alphaIndexShared = SharedMemory.Allocate<uint>(GroupSize);

        int gi = Group.IdxX;
        int blockInGroup = gi / BlockSize;
        int threadInBlock = gi - (blockInGroup * BlockSize);
        int blockID = (Grid.IdxX * BlocksPerGroup) + blockInGroup;
        int threadBase = blockInGroup * BlockSize;
        int slot = gi * 4;
        int safeBlock = blockID < numBlocks ? blockID : 0;
        int candidateSlot = safeBlock * 4;

        uint mode = input[candidateSlot + 1] & 0x7FFFFFFFu;
        uint partition = input[candidateSlot + 2];
        uint indexSelector = (input[candidateSlot + 1] >> 31) & 1u;
        uint rotation = input[candidateSlot + 3];
        uint bits = tables[Bc7Tables.SectionBits + (int)partition];

        int blockY = blockID / numBlockX;
        int blockX = blockID - (blockY * numBlockX);

        uint packed = LoadInputPixel(source, width, height, blockX, blockY, threadInBlock);
        uint red = packed & 0xFFu;
        uint green = (packed >> 8) & 0xFFu;
        uint blue = (packed >> 16) & 0xFFu;
        uint alpha = (packed >> 24) & 0xFFu;

        if (mode is 4 or 5)
        {
            if (rotation == 1)
            {
                (red, alpha) = (alpha, red);
            }
            else if (rotation == 2)
            {
                (green, alpha) = (alpha, green);
            }
            else if (rotation == 3)
            {
                (blue, alpha) = (alpha, blue);
            }
        }

        pixelShared[slot] = red;
        pixelShared[slot + 1] = green;
        pixelShared[slot + 2] = blue;
        pixelShared[slot + 3] = alpha;

        Group.Barrier();

        EndpointPair endpoints;
        endpoints.R0 = 0;
        endpoints.G0 = 0;
        endpoints.B0 = 0;
        endpoints.A0 = 0;
        endpoints.R1 = 0;
        endpoints.G1 = 0;
        endpoints.B1 = 0;
        endpoints.A1 = 0;

        for (int subset = 1; subset >= 0; subset--)
        {
            uint take = 0;
            if (mode is 1 or 3 or 7)
            {
                take = ((bits >> threadInBlock) & 1u) == (uint)subset ? 0xFFFFFFFFu : 0u;
            }
            else if (subset == 0)
            {
                take = 0xFFFFFFFFu;
            }

            uint currentR = pixelShared[slot];
            uint currentG = pixelShared[slot + 1];
            uint currentB = pixelShared[slot + 2];
            uint currentA = pixelShared[slot + 3];

            lowShared[slot] = take != 0 ? currentR : 0xFFFFFFFFu;
            lowShared[slot + 1] = take != 0 ? currentG : 0xFFFFFFFFu;
            lowShared[slot + 2] = take != 0 ? currentB : 0xFFFFFFFFu;
            lowShared[slot + 3] = take != 0 ? currentA : 0xFFFFFFFFu;
            highShared[slot] = take != 0 ? currentR : 0u;
            highShared[slot + 1] = take != 0 ? currentG : 0u;
            highShared[slot + 2] = take != 0 ? currentB : 0u;
            highShared[slot + 3] = take != 0 ? currentA : 0u;

            Group.Barrier();

            if (threadInBlock < 8)
            {
                ReduceMinMax(lowShared, highShared, slot, slot + (8 * 4));
            }

            Group.Barrier();

            if (threadInBlock < 4)
            {
                ReduceMinMax(lowShared, highShared, slot, slot + (4 * 4));
            }

            Group.Barrier();

            if (threadInBlock < 2)
            {
                ReduceMinMax(lowShared, highShared, slot, slot + (2 * 4));
            }

            Group.Barrier();

            if (threadInBlock < 1)
            {
                ReduceMinMax(lowShared, highShared, slot, slot + 4);
            }

            Group.Barrier();

            if (subset == threadInBlock)
            {
                int blockSlot = threadBase * 4;
                endpoints.R0 = lowShared[blockSlot];
                endpoints.G0 = lowShared[blockSlot + 1];
                endpoints.B0 = lowShared[blockSlot + 2];
                endpoints.A0 = lowShared[blockSlot + 3];
                endpoints.R1 = highShared[blockSlot];
                endpoints.G1 = highShared[blockSlot + 1];
                endpoints.B1 = highShared[blockSlot + 2];
                endpoints.A1 = highShared[blockSlot + 3];
            }

            // The shader reads the reduced endpoints back without a following barrier, which is only safe in the
            // lockstep warp it runs in; an explicit barrier keeps every other backend deterministic.
            Group.Barrier();
        }

        if (threadInBlock < 2)
        {
            uint endpointP0;
            uint endpointP1;
            if (mode == 1)
            {
                endpointP0 = (rotation >> threadInBlock) & 1u;
                endpointP1 = endpointP0;
            }
            else
            {
                endpointP0 = (rotation >> (threadInBlock * 2)) & 1u;
                endpointP1 = (rotation >> ((threadInBlock * 2) + 1)) & 1u;
            }

            EndpointCompression compressed = Compress(mode, endpoints, endpointP0, endpointP1);
            endpoints = compressed.Unquantized;
            EndpointPair quantized = compressed.Quantized;

            int spanR = (int)endpoints.R1 - (int)endpoints.R0;
            int spanG = (int)endpoints.G1 - (int)endpoints.G0;
            int spanB = (int)endpoints.B1 - (int)endpoints.B0;
            int spanA = mode < 4 ? 0 : (int)endpoints.A1 - (int)endpoints.A0;

            if (mode is 4 or 5)
            {
                if (threadInBlock == 0)
                {
                    int blockSlot = threadBase * 4;
                    uint pivotR = pixelShared[blockSlot];
                    uint pivotG = pixelShared[blockSlot + 1];
                    uint pivotB = pixelShared[blockSlot + 2];
                    uint pivotA = pixelShared[blockSlot + 3];

                    int normRgb = (spanR * spanR) + (spanG * spanG) + (spanB * spanB);
                    int dotRgb = (spanR * ((int)pivotR - (int)endpoints.R0)) +
                                 (spanG * ((int)pivotG - (int)endpoints.G0)) +
                                 (spanB * ((int)pivotB - (int)endpoints.B0));
                    if (normRgb > 0 && dotRgb > 0 && (uint)(dotRgb * 63.49999f) > (uint)(32 * normRgb))
                    {
                        endpoints = SwapRgb(endpoints);
                        quantized = SwapRgb(quantized);
                    }

                    int normAlpha = spanA * spanA;
                    int dotAlpha = spanA * ((int)pivotA - (int)endpoints.A0);
                    if (normAlpha > 0 && dotAlpha > 0 && (uint)(dotAlpha * 63.49999f) > (uint)(32 * normAlpha))
                    {
                        (endpoints.A0, endpoints.A1) = (endpoints.A1, endpoints.A0);
                        (quantized.A0, quantized.A1) = (quantized.A1, quantized.A0);
                    }
                }
            }
            else
            {
                int fixUp = threadInBlock == 0 ? 0 : (int)GetFixUpIndex1D(partition);

                int pivotSlot = (threadBase + fixUp) * 4;
                uint pivotR = pixelShared[pivotSlot];
                uint pivotG = pixelShared[pivotSlot + 1];
                uint pivotB = pixelShared[pivotSlot + 2];
                uint pivotA = pixelShared[pivotSlot + 3];

                int norm = (spanR * spanR) + (spanG * spanG) + (spanB * spanB) + (spanA * spanA);
                int dot = (spanR * ((int)pivotR - (int)endpoints.R0)) + (spanG * ((int)pivotG - (int)endpoints.G0)) +
                          (spanB * ((int)pivotB - (int)endpoints.B0)) + (spanA * ((int)pivotA - (int)endpoints.A0));
                if (norm > 0 && dot > 0 && (uint)(dot * 63.49999f) > (uint)(32 * norm))
                {
                    endpoints = SwapBoth(endpoints);
                    quantized = SwapBoth(quantized);
                }
            }

            lowShared[slot] = endpoints.R0;
            lowShared[slot + 1] = endpoints.G0;
            lowShared[slot + 2] = endpoints.B0;
            lowShared[slot + 3] = endpoints.A0;
            highShared[slot] = endpoints.R1;
            highShared[slot + 1] = endpoints.G1;
            highShared[slot + 2] = endpoints.B1;
            highShared[slot + 3] = endpoints.A1;
            lowQuantizedShared[slot] = quantized.R0;
            lowQuantizedShared[slot + 1] = quantized.G0;
            lowQuantizedShared[slot + 2] = quantized.B0;
            lowQuantizedShared[slot + 3] = quantized.A0;
            highQuantizedShared[slot] = quantized.R1;
            highQuantizedShared[slot + 1] = quantized.G1;
            highQuantizedShared[slot + 2] = quantized.B1;
            highQuantizedShared[slot + 3] = quantized.A1;
        }

        Group.Barrier();

        int indexPrecX;
        int indexPrecY;
        if (mode is 0 or 1)
        {
            indexPrecX = 1;
            indexPrecY = 1;
        }
        else if (mode == 6)
        {
            indexPrecX = 0;
            indexPrecY = 0;
        }
        else if (mode == 4)
        {
            indexPrecX = indexSelector == 0 ? 2 : 1;
            indexPrecY = indexSelector == 0 ? 1 : 2;
        }
        else
        {
            indexPrecX = 2;
            indexPrecY = 2;
        }

        uint subsetIndex = 0;
        if (mode is 1 or 3 or 7)
        {
            subsetIndex = (bits >> threadInBlock) & 1u;
        }

        int endpointSlot = (threadBase + (int)subsetIndex) * 4;
        uint endpointLowR = lowShared[endpointSlot];
        uint endpointLowG = lowShared[endpointSlot + 1];
        uint endpointLowB = lowShared[endpointSlot + 2];
        uint endpointLowA = lowShared[endpointSlot + 3];
        uint endpointHighR = highShared[endpointSlot];
        uint endpointHighG = highShared[endpointSlot + 1];
        uint endpointHighB = highShared[endpointSlot + 2];
        uint endpointHighA = highShared[endpointSlot + 3];

        int spanBlockR = (int)endpointHighR - (int)endpointLowR;
        int spanBlockG = (int)endpointHighG - (int)endpointLowG;
        int spanBlockB = (int)endpointHighB - (int)endpointLowB;
        int spanBlockA = mode < 4 ? 0 : (int)endpointHighA - (int)endpointLowA;

        uint pixelR = pixelShared[slot];
        uint pixelG = pixelShared[slot + 1];
        uint pixelB = pixelShared[slot + 2];
        uint pixelA = pixelShared[slot + 3];

        uint colorIndex = 0;
        uint alphaIndex = 0;

        if (mode is 4 or 5)
        {
            int normRgb = (spanBlockR * spanBlockR) + (spanBlockG * spanBlockG) + (spanBlockB * spanBlockB);
            int normAlpha = spanBlockA * spanBlockA;
            int colorDot = (spanBlockR * ((int)pixelR - (int)endpointLowR)) +
                           (spanBlockG * ((int)pixelG - (int)endpointLowG)) +
                           (spanBlockB * ((int)pixelB - (int)endpointLowB));
            int alphaDot = spanBlockA * ((int)pixelA - (int)endpointLowA);

            colorIndex = StepIndex(colorDot, normRgb, indexPrecX, tables);
            alphaIndex = StepIndex(alphaDot, normAlpha, indexPrecY, tables);

            if (indexSelector == 1)
            {
                (colorIndex, alphaIndex) = (alphaIndex, colorIndex);
            }
        }
        else
        {
            int norm = (spanBlockR * spanBlockR) + (spanBlockG * spanBlockG) + (spanBlockB * spanBlockB) +
                       (spanBlockA * spanBlockA);
            int colorDot = (spanBlockR * ((int)pixelR - (int)endpointLowR)) +
                           (spanBlockG * ((int)pixelG - (int)endpointLowG)) +
                           (spanBlockB * ((int)pixelB - (int)endpointLowB)) +
                           (spanBlockA * ((int)pixelA - (int)endpointLowA));

            colorIndex = StepIndex(colorDot, norm, indexPrecX, tables);
        }

        colorIndexShared[gi] = colorIndex;
        alphaIndexShared[gi] = alphaIndex;

        Group.Barrier();

        if (threadInBlock == 0 && blockID < numBlocks)
        {
            PackBlock(
                mode,
                partition,
                indexSelector,
                rotation,
                lowQuantizedShared,
                highQuantizedShared,
                colorIndexShared,
                alphaIndexShared,
                threadBase,
                blocks,
                blockID);
        }
    }

    /// <summary>
    ///     Packs one block into the destination buffer.
    /// </summary>
    /// <param name="mode">The winning mode.</param>
    /// <param name="partition">The winning partition.</param>
    /// <param name="indexSelector">The winning index selector of mode 4.</param>
    /// <param name="rotation">The winning rotation of modes 4 and 5.</param>
    /// <param name="lowQuantized">The per-subset low endpoints, quantized and pre-shifted.</param>
    /// <param name="highQuantized">The per-subset high endpoints, quantized and pre-shifted.</param>
    /// <param name="colorIndex">The per-texel color indices.</param>
    /// <param name="alphaIndex">The per-texel alpha indices.</param>
    /// <param name="threadBase">The shared memory slot of the first texel of the block.</param>
    /// <param name="blocks">The destination BC7 payload, four words per block.</param>
    /// <param name="blockID">The destination block index.</param>
    private static void PackBlock(
        uint mode,
        uint partition,
        uint indexSelector,
        uint rotation,
        ArrayView<uint> lowQuantized,
        ArrayView<uint> highQuantized,
        ArrayView<uint> colorIndex,
        ArrayView<uint> alphaIndex,
        int threadBase,
        ArrayView<uint> blocks,
        int blockID)
    {
        if (mode == 1)
        {
            Pack1(lowQuantized, highQuantized, colorIndex, threadBase, blocks, blockID, partition);
        }
        else if (mode == 3)
        {
            Pack3(lowQuantized, highQuantized, colorIndex, threadBase, blocks, blockID, partition);
        }
        else if (mode == 4)
        {
            Pack4(lowQuantized, highQuantized, colorIndex, alphaIndex, threadBase, blocks, blockID, indexSelector,
                rotation);
        }
        else if (mode == 5)
        {
            Pack5(lowQuantized, highQuantized, colorIndex, alphaIndex, threadBase, blocks, blockID, rotation);
        }
        else if (mode == 6)
        {
            Pack6(lowQuantized, highQuantized, colorIndex, threadBase, blocks, blockID);
        }
        else
        {
            Pack7(lowQuantized, highQuantized, colorIndex, threadBase, blocks, blockID, partition);
        }
    }

    /// <summary>
    ///     Packs a mode 1 block.
    /// </summary>
    /// <param name="lowQuantized">The per-subset low endpoints.</param>
    /// <param name="highQuantized">The per-subset high endpoints.</param>
    /// <param name="colorIndex">The per-texel color indices.</param>
    /// <param name="threadBase">The shared memory slot of the first texel of the block.</param>
    /// <param name="blocks">The destination BC7 payload.</param>
    /// <param name="blockID">The destination block index.</param>
    /// <param name="partition">The winning partition.</param>
    private static void Pack1(
        ArrayView<uint> lowQuantized,
        ArrayView<uint> highQuantized,
        ArrayView<uint> colorIndex,
        int threadBase,
        ArrayView<uint> blocks,
        int blockID,
        uint partition)
    {
        int slot0 = threadBase * 4;
        int slot1 = (threadBase + 1) * 4;

        uint low0R = lowQuantized[slot0];
        uint low0G = lowQuantized[slot0 + 1];
        uint low0B = lowQuantized[slot0 + 2];
        uint high0R = highQuantized[slot0];
        uint high0G = highQuantized[slot0 + 1];
        uint high0B = highQuantized[slot0 + 2];
        uint low1R = lowQuantized[slot1];
        uint low1G = lowQuantized[slot1 + 1];
        uint low1B = lowQuantized[slot1 + 2];
        uint high1R = highQuantized[slot1];
        uint high1G = highQuantized[slot1 + 1];
        uint high1B = highQuantized[slot1 + 2];

        uint c0 = colorIndex[threadBase];
        uint c1 = colorIndex[threadBase + 1];
        uint c2 = colorIndex[threadBase + 2];
        uint c3 = colorIndex[threadBase + 3];
        uint c4 = colorIndex[threadBase + 4];
        uint c5 = colorIndex[threadBase + 5];
        uint c6 = colorIndex[threadBase + 6];
        uint c7 = colorIndex[threadBase + 7];
        uint c8 = colorIndex[threadBase + 8];
        uint c9 = colorIndex[threadBase + 9];
        uint c10 = colorIndex[threadBase + 10];
        uint c11 = colorIndex[threadBase + 11];
        uint c12 = colorIndex[threadBase + 12];
        uint c13 = colorIndex[threadBase + 13];
        uint c14 = colorIndex[threadBase + 14];
        uint c15 = colorIndex[threadBase + 15];

        uint fixUp = GetFixUpIndex1D(partition);

        uint x = 0x02u | (partition << 2) | ((low0R & 0xFCu) << 6) | ((high0R & 0xFCu) << 12) |
                 ((low1R & 0xFCu) << 18) | ((high1R & 0xFCu) << 24);
        uint y = ((low0G & 0xFCu) >> 2) | ((high0G & 0xFCu) << 4) | ((low1G & 0xFCu) << 10) | ((high1G & 0xFCu) << 16) |
                 ((low0B & 0xFCu) << 22) | ((high0B & 0xFCu) << 28);
        uint z = ((high0B & 0xFCu) >> 4) | ((low1B & 0xFCu) << 2) | ((high1B & 0xFCu) << 8) |
                 ((low0R & 0x02u) << 15) | ((low1R & 0x02u) << 16) | (c0 << 18);
        uint w = 0;

        if (fixUp == 15)
        {
            w = (c15 << 30) | (c14 << 27) | (c13 << 24) | (c12 << 21) | (c11 << 18) | (c10 << 15) | (c9 << 12) |
                (c8 << 9) |
                (c7 << 6) | (c6 << 3) | c5;
            z |= (c4 << 29) | (c3 << 26) | (c2 << 23) | (c1 << 20);
        }
        else if (fixUp == 2)
        {
            w = (c15 << 29) | (c14 << 26) | (c13 << 23) | (c12 << 20) | (c11 << 17) | (c10 << 14) | (c9 << 11) |
                (c8 << 8) |
                (c7 << 5) | (c6 << 2) | (c5 >> 1);
            z |= (c5 << 31) | (c4 << 28) | (c3 << 25) | (c2 << 23) | (c1 << 20);
        }
        else if (fixUp == 8)
        {
            w = (c15 << 29) | (c14 << 26) | (c13 << 23) | (c12 << 20) | (c11 << 17) | (c10 << 14) | (c9 << 11) |
                (c8 << 9) |
                (c7 << 6) | (c6 << 3) | c5;
            z |= (c4 << 29) | (c3 << 26) | (c2 << 23) | (c1 << 20);
        }
        else
        {
            w = (c15 << 29) | (c14 << 26) | (c13 << 23) | (c12 << 20) | (c11 << 17) | (c10 << 14) | (c9 << 11) |
                (c8 << 8) |
                (c7 << 5) | (c6 << 3) | c5;
            z |= (c4 << 29) | (c3 << 26) | (c2 << 23) | (c1 << 20);
        }

        int destination = blockID * 4;
        blocks[destination] = x;
        blocks[destination + 1] = y;
        blocks[destination + 2] = z;
        blocks[destination + 3] = w;
    }

    /// <summary>
    ///     Packs a mode 3 block.
    /// </summary>
    /// <param name="lowQuantized">The per-subset low endpoints.</param>
    /// <param name="highQuantized">The per-subset high endpoints.</param>
    /// <param name="colorIndex">The per-texel color indices.</param>
    /// <param name="threadBase">The shared memory slot of the first texel of the block.</param>
    /// <param name="blocks">The destination BC7 payload.</param>
    /// <param name="blockID">The destination block index.</param>
    /// <param name="partition">The winning partition.</param>
    private static void Pack3(
        ArrayView<uint> lowQuantized,
        ArrayView<uint> highQuantized,
        ArrayView<uint> colorIndex,
        int threadBase,
        ArrayView<uint> blocks,
        int blockID,
        uint partition)
    {
        int slot0 = threadBase * 4;
        int slot1 = (threadBase + 1) * 4;

        uint low0R = lowQuantized[slot0];
        uint low0G = lowQuantized[slot0 + 1];
        uint low0B = lowQuantized[slot0 + 2];
        uint high0R = highQuantized[slot0];
        uint high0G = highQuantized[slot0 + 1];
        uint high0B = highQuantized[slot0 + 2];
        uint low1R = lowQuantized[slot1];
        uint low1G = lowQuantized[slot1 + 1];
        uint low1B = lowQuantized[slot1 + 2];
        uint high1R = highQuantized[slot1];
        uint high1G = highQuantized[slot1 + 1];
        uint high1B = highQuantized[slot1 + 2];

        uint c0 = colorIndex[threadBase];
        uint fixUp = GetFixUpIndex1D(partition);

        uint x = 0x08u | (partition << 4) | ((low0R & 0xFEu) << 9) | ((high0R & 0xFEu) << 16) |
                 ((low1R & 0xFEu) << 23) |
                 ((high1R & 0xFEu) << 30);
        uint y = ((high1R & 0xFEu) >> 2) | ((low0G & 0xFEu) << 5) | ((high0G & 0xFEu) << 12) | ((low1G & 0xFEu) << 19) |
                 ((high1G & 0xFEu) << 26);
        uint z = ((high1G & 0xFEu) >> 6) | ((low0B & 0xFEu) << 1) | ((high0B & 0xFEu) << 8) | ((low1B & 0xFEu) << 15) |
                 ((high1B & 0xFEu) << 22) | ((low0R & 0x01u) << 30) | ((high0R & 0x01u) << 31);
        uint w = (low1R & 0x01u) | ((high1R & 0x01u) << 1) | (c0 << 2);

        uint index = 1;
        for (; index <= fixUp; index++)
        {
            w |= GetIndex(colorIndex, threadBase, (int)index) << (int)((index * 2) + 1);
        }

        for (; index < BlockSize; index++)
        {
            w |= GetIndex(colorIndex, threadBase, (int)index) << (int)(index * 2);
        }

        int destination = blockID * 4;
        blocks[destination] = x;
        blocks[destination + 1] = y;
        blocks[destination + 2] = z;
        blocks[destination + 3] = w;
    }

    /// <summary>
    ///     Packs a mode 4 block.
    /// </summary>
    /// <param name="lowQuantized">The per-subset low endpoints.</param>
    /// <param name="highQuantized">The per-subset high endpoints.</param>
    /// <param name="colorIndex">The per-texel color indices.</param>
    /// <param name="alphaIndex">The per-texel alpha indices.</param>
    /// <param name="threadBase">The shared memory slot of the first texel of the block.</param>
    /// <param name="blocks">The destination BC7 payload.</param>
    /// <param name="blockID">The destination block index.</param>
    /// <param name="indexSelector">The winning index selector.</param>
    /// <param name="rotation">The winning rotation.</param>
    private static void Pack4(
        ArrayView<uint> lowQuantized,
        ArrayView<uint> highQuantized,
        ArrayView<uint> colorIndex,
        ArrayView<uint> alphaIndex,
        int threadBase,
        ArrayView<uint> blocks,
        int blockID,
        uint indexSelector,
        uint rotation)
    {
        int slot0 = threadBase * 4;

        uint lowR = lowQuantized[slot0];
        uint lowG = lowQuantized[slot0 + 1];
        uint lowB = lowQuantized[slot0 + 2];
        uint lowA = lowQuantized[slot0 + 3];
        uint highR = highQuantized[slot0];
        uint highG = highQuantized[slot0 + 1];
        uint highB = highQuantized[slot0 + 2];
        uint highA = highQuantized[slot0 + 3];

        uint c0 = colorIndex[threadBase];
        uint c1 = colorIndex[threadBase + 1];
        uint c2 = colorIndex[threadBase + 2];
        uint c3 = colorIndex[threadBase + 3];
        uint c4 = colorIndex[threadBase + 4];
        uint c5 = colorIndex[threadBase + 5];
        uint c6 = colorIndex[threadBase + 6];
        uint c7 = colorIndex[threadBase + 7];
        uint c8 = colorIndex[threadBase + 8];
        uint c9 = colorIndex[threadBase + 9];
        uint c10 = colorIndex[threadBase + 10];
        uint c11 = colorIndex[threadBase + 11];
        uint c12 = colorIndex[threadBase + 12];
        uint c13 = colorIndex[threadBase + 13];
        uint c14 = colorIndex[threadBase + 14];
        uint c15 = colorIndex[threadBase + 15];

        uint a0 = alphaIndex[threadBase];
        uint a1 = alphaIndex[threadBase + 1];
        uint a2 = alphaIndex[threadBase + 2];
        uint a3 = alphaIndex[threadBase + 3];
        uint a4 = alphaIndex[threadBase + 4];
        uint a5 = alphaIndex[threadBase + 5];
        uint a6 = alphaIndex[threadBase + 6];
        uint a7 = alphaIndex[threadBase + 7];
        uint a8 = alphaIndex[threadBase + 8];
        uint a9 = alphaIndex[threadBase + 9];
        uint a10 = alphaIndex[threadBase + 10];
        uint a11 = alphaIndex[threadBase + 11];
        uint a12 = alphaIndex[threadBase + 12];
        uint a13 = alphaIndex[threadBase + 13];
        uint a14 = alphaIndex[threadBase + 14];
        uint a15 = alphaIndex[threadBase + 15];

        uint x = 0x10u | ((rotation & 3u) << 5) | ((indexSelector & 1u) << 7) | ((lowR & 0xF8u) << 5) |
                 ((highR & 0xF8u) << 10) | ((lowG & 0xF8u) << 15) | ((highG & 0xF8u) << 20) | ((lowB & 0xF8u) << 25);
        uint y = ((lowB & 0xF8u) >> 7) | ((highB & 0xF8u) >> 2) | ((lowA & 0xFCu) << 4) | ((highA & 0xFCu) << 10) |
                 ((c0 & 1u) << 18) | (c1 << 19) | (c2 << 21) | (c3 << 23) | (c4 << 25) | (c5 << 27) | (c6 << 29) |
                 (c7 << 31);
        uint z = (c7 >> 1) | (c8 << 1) | (c9 << 3) | (c10 << 5) | (c11 << 7) | (c12 << 9) | (c13 << 11) | (c14 << 13) |
                 (c15 << 15) | ((a0 & 3u) << 17) | (a1 << 19) | (a2 << 22) | (a3 << 25) | (a4 << 28) | (a5 << 31);
        uint w = (a5 >> 1) | (a6 << 2) | (a7 << 5) | (a8 << 8) | (a9 << 11) | (a10 << 14) | (a11 << 17) | (a12 << 20) |
                 (a13 << 23) | (a14 << 26) | (a15 << 29);

        int destination = blockID * 4;
        blocks[destination] = x;
        blocks[destination + 1] = y;
        blocks[destination + 2] = z;
        blocks[destination + 3] = w;
    }

    /// <summary>
    ///     Packs a mode 5 block.
    /// </summary>
    /// <param name="lowQuantized">The per-subset low endpoints.</param>
    /// <param name="highQuantized">The per-subset high endpoints.</param>
    /// <param name="colorIndex">The per-texel color indices.</param>
    /// <param name="alphaIndex">The per-texel alpha indices.</param>
    /// <param name="threadBase">The shared memory slot of the first texel of the block.</param>
    /// <param name="blocks">The destination BC7 payload.</param>
    /// <param name="blockID">The destination block index.</param>
    /// <param name="rotation">The winning rotation.</param>
    private static void Pack5(
        ArrayView<uint> lowQuantized,
        ArrayView<uint> highQuantized,
        ArrayView<uint> colorIndex,
        ArrayView<uint> alphaIndex,
        int threadBase,
        ArrayView<uint> blocks,
        int blockID,
        uint rotation)
    {
        int slot0 = threadBase * 4;

        uint lowR = lowQuantized[slot0];
        uint lowG = lowQuantized[slot0 + 1];
        uint lowB = lowQuantized[slot0 + 2];
        uint lowA = lowQuantized[slot0 + 3];
        uint highR = highQuantized[slot0];
        uint highG = highQuantized[slot0 + 1];
        uint highB = highQuantized[slot0 + 2];
        uint highA = highQuantized[slot0 + 3];

        uint c0 = colorIndex[threadBase];
        uint c1 = colorIndex[threadBase + 1];
        uint c2 = colorIndex[threadBase + 2];
        uint c3 = colorIndex[threadBase + 3];
        uint c4 = colorIndex[threadBase + 4];
        uint c5 = colorIndex[threadBase + 5];
        uint c6 = colorIndex[threadBase + 6];
        uint c7 = colorIndex[threadBase + 7];
        uint c8 = colorIndex[threadBase + 8];
        uint c9 = colorIndex[threadBase + 9];
        uint c10 = colorIndex[threadBase + 10];
        uint c11 = colorIndex[threadBase + 11];
        uint c12 = colorIndex[threadBase + 12];
        uint c13 = colorIndex[threadBase + 13];
        uint c14 = colorIndex[threadBase + 14];
        uint c15 = colorIndex[threadBase + 15];

        uint a0 = alphaIndex[threadBase];
        uint a1 = alphaIndex[threadBase + 1];
        uint a2 = alphaIndex[threadBase + 2];
        uint a3 = alphaIndex[threadBase + 3];
        uint a4 = alphaIndex[threadBase + 4];
        uint a5 = alphaIndex[threadBase + 5];
        uint a6 = alphaIndex[threadBase + 6];
        uint a7 = alphaIndex[threadBase + 7];
        uint a8 = alphaIndex[threadBase + 8];
        uint a9 = alphaIndex[threadBase + 9];
        uint a10 = alphaIndex[threadBase + 10];
        uint a11 = alphaIndex[threadBase + 11];
        uint a12 = alphaIndex[threadBase + 12];
        uint a13 = alphaIndex[threadBase + 13];
        uint a14 = alphaIndex[threadBase + 14];
        uint a15 = alphaIndex[threadBase + 15];

        uint x = 0x20u | ((rotation & 3u) << 6) | ((lowR & 0xFEu) << 7) | ((highR & 0xFEu) << 14) |
                 ((lowG & 0xFEu) << 21) | ((highG & 0xFEu) << 28);
        uint y = ((highG & 0xFEu) >> 4) | ((lowB & 0xFEu) << 3) | ((highB & 0xFEu) << 10) | (lowA << 18) |
                 (highA << 26);
        uint z = (highA >> 6) | (c0 << 2) | (c1 << 3) | (c2 << 5) | (c3 << 7) | (c4 << 9) | (c5 << 11) | (c6 << 13) |
                 (c7 << 15) | (c8 << 17) | (c9 << 19) | (c10 << 21) | (c11 << 23) | (c12 << 25) | (c13 << 27) |
                 (c14 << 29) |
                 (c15 << 31);
        uint w = (c15 >> 1) | (a0 << 1) | (a1 << 2) | (a2 << 4) | (a3 << 6) | (a4 << 8) | (a5 << 10) | (a6 << 12) |
                 (a7 << 14) | (a8 << 16) | (a9 << 18) | (a10 << 20) | (a11 << 22) | (a12 << 24) | (a13 << 26) |
                 (a14 << 28) |
                 (a15 << 30);

        int destination = blockID * 4;
        blocks[destination] = x;
        blocks[destination + 1] = y;
        blocks[destination + 2] = z;
        blocks[destination + 3] = w;
    }

    /// <summary>
    ///     Packs a mode 6 block.
    /// </summary>
    /// <param name="lowQuantized">The low endpoint.</param>
    /// <param name="highQuantized">The high endpoint.</param>
    /// <param name="colorIndex">The per-texel color indices.</param>
    /// <param name="threadBase">The shared memory slot of the first texel of the block.</param>
    /// <param name="blocks">The destination BC7 payload.</param>
    /// <param name="blockID">The destination block index.</param>
    private static void Pack6(
        ArrayView<uint> lowQuantized,
        ArrayView<uint> highQuantized,
        ArrayView<uint> colorIndex,
        int threadBase,
        ArrayView<uint> blocks,
        int blockID)
    {
        int slot0 = threadBase * 4;

        uint lowR = lowQuantized[slot0];
        uint lowG = lowQuantized[slot0 + 1];
        uint lowB = lowQuantized[slot0 + 2];
        uint lowA = lowQuantized[slot0 + 3];
        uint highR = highQuantized[slot0];
        uint highG = highQuantized[slot0 + 1];
        uint highB = highQuantized[slot0 + 2];
        uint highA = highQuantized[slot0 + 3];

        uint c0 = colorIndex[threadBase];
        uint c1 = colorIndex[threadBase + 1];
        uint c2 = colorIndex[threadBase + 2];
        uint c3 = colorIndex[threadBase + 3];
        uint c4 = colorIndex[threadBase + 4];
        uint c5 = colorIndex[threadBase + 5];
        uint c6 = colorIndex[threadBase + 6];
        uint c7 = colorIndex[threadBase + 7];
        uint c8 = colorIndex[threadBase + 8];
        uint c9 = colorIndex[threadBase + 9];
        uint c10 = colorIndex[threadBase + 10];
        uint c11 = colorIndex[threadBase + 11];
        uint c12 = colorIndex[threadBase + 12];
        uint c13 = colorIndex[threadBase + 13];
        uint c14 = colorIndex[threadBase + 14];
        uint c15 = colorIndex[threadBase + 15];

        uint x = 0x40u | ((lowR & 0xFEu) << 6) | ((highR & 0xFEu) << 13) | ((lowG & 0xFEu) << 20) |
                 ((highG & 0xFEu) << 27);
        uint y = ((highG & 0xFEu) >> 5) | ((lowB & 0xFEu) << 2) | ((highB & 0xFEu) << 9) | ((lowA & 0xFEu) << 16) |
                 ((highA & 0xFEu) << 23) | ((lowR & 0x01u) << 31);
        uint z = (highR & 0x01u) | (c0 << 1) | (c1 << 4) | (c2 << 8) | (c3 << 12) | (c4 << 16) | (c5 << 20) |
                 (c6 << 24) |
                 (c7 << 28);
        uint w = c8 | (c9 << 4) | (c10 << 8) | (c11 << 12) | (c12 << 16) | (c13 << 20) | (c14 << 24) | (c15 << 28);

        int destination = blockID * 4;
        blocks[destination] = x;
        blocks[destination + 1] = y;
        blocks[destination + 2] = z;
        blocks[destination + 3] = w;
    }

    /// <summary>
    ///     Packs a mode 7 block.
    /// </summary>
    /// <param name="lowQuantized">The per-subset low endpoints.</param>
    /// <param name="highQuantized">The per-subset high endpoints.</param>
    /// <param name="colorIndex">The per-texel color indices.</param>
    /// <param name="threadBase">The shared memory slot of the first texel of the block.</param>
    /// <param name="blocks">The destination BC7 payload.</param>
    /// <param name="blockID">The destination block index.</param>
    /// <param name="partition">The winning partition.</param>
    private static void Pack7(
        ArrayView<uint> lowQuantized,
        ArrayView<uint> highQuantized,
        ArrayView<uint> colorIndex,
        int threadBase,
        ArrayView<uint> blocks,
        int blockID,
        uint partition)
    {
        int slot0 = threadBase * 4;
        int slot1 = (threadBase + 1) * 4;

        uint low0R = lowQuantized[slot0];
        uint low0G = lowQuantized[slot0 + 1];
        uint low0B = lowQuantized[slot0 + 2];
        uint low0A = lowQuantized[slot0 + 3];
        uint high0R = highQuantized[slot0];
        uint high0G = highQuantized[slot0 + 1];
        uint high0B = highQuantized[slot0 + 2];
        uint high0A = highQuantized[slot0 + 3];
        uint low1R = lowQuantized[slot1];
        uint low1G = lowQuantized[slot1 + 1];
        uint low1B = lowQuantized[slot1 + 2];
        uint low1A = lowQuantized[slot1 + 3];
        uint high1R = highQuantized[slot1];
        uint high1G = highQuantized[slot1 + 1];
        uint high1B = highQuantized[slot1 + 2];
        uint high1A = highQuantized[slot1 + 3];

        uint c0 = colorIndex[threadBase];
        uint fixUp = GetFixUpIndex1D(partition);

        uint x = 0x80u | (partition << 8) | ((low0R & 0xF8u) << 11) | ((high0R & 0xF8u) << 16) |
                 ((low1R & 0xF8u) << 21) |
                 ((high1R & 0xF8u) << 26);
        uint y = ((high1R & 0xF8u) >> 6) | ((low0G & 0xF8u) >> 1) | ((high0G & 0xF8u) << 4) | ((low1G & 0xF8u) << 9) |
                 ((high1G & 0xF8u) << 14) | ((low0B & 0xF8u) << 19) | ((high0B & 0xF8u) << 24);
        uint z = ((low1B & 0xF8u) >> 3) | ((high1B & 0xF8u) << 2) | ((low0A & 0xF8u) << 7) | ((high0A & 0xF8u) << 12) |
                 ((low1A & 0xF8u) << 17) | ((high1A & 0xF8u) << 22) | ((low0R & 0x04u) << 28) |
                 ((high0R & 0x04u) << 29);
        uint w = ((low1R & 0x04u) >> 2) | ((high1R & 0x04u) >> 1) | (c0 << 2);

        uint index = 1;
        for (; index <= fixUp; index++)
        {
            w |= GetIndex(colorIndex, threadBase, (int)index) << (int)((index * 2) + 1);
        }

        for (; index < BlockSize; index++)
        {
            w |= GetIndex(colorIndex, threadBase, (int)index) << (int)(index * 2);
        }

        int destination = blockID * 4;
        blocks[destination] = x;
        blocks[destination + 1] = y;
        blocks[destination + 2] = z;
        blocks[destination + 3] = w;
    }

    /// <summary>
    ///     Loads one texel from the source surface, replicating out-of-bounds partial block edges via DirectXTex's
    ///     <c>{ 0, 0, 0, 1 }</c> table.
    /// </summary>
    private static uint LoadInputPixel(
        ArrayView<uint> source,
        int width,
        int height,
        int blockX,
        int blockY,
        int threadInBlock)
    {
        int baseX = blockX * 4;
        int baseY = blockY * 4;
        int px = threadInBlock & 3;
        int py = threadInBlock >> 2;
        int remW = width - baseX;
        int remH = height - baseY;
        int validW = remW < 4 ? remW > 0 ? remW : 0 : 4;
        int validH = remH < 4 ? remH > 0 ? remH : 0 : 4;

        if (px >= validW)
        {
            px = px == 3 && validW > 1 ? 1 : 0;
        }

        if (py >= validH)
        {
            py = py == 3 && validH > 1 ? 1 : 0;
        }

        int x = baseX + px;
        int y = baseY + py;
        x = x < width ? x : width - 1;
        y = y < height ? y : height - 1;
        return source[(y * width) + x];
    }

    /// <summary>
    ///     Reads the BC7 interpolation weight for the given precision row and index.
    /// </summary>
    private static uint GetWeight(int precisionRow, uint index, ArrayView<uint> tables)
        => tables[Bc7Tables.Weights + (precisionRow * Bc7Tables.WeightRowSize) + (int)index];

    /// <summary>
    ///     Computes the two-subset anchor fix-up texel index for a partition in <c>[0, 63]</c>.
    /// </summary>
    private static uint GetFixUpIndex1D(uint partition)
    {
        uint word = partition < 16u ? 0x00000000u
            : partition < 32u ? 0x5A592964u
            : partition < 48u ? 0xC15909B0u : 0x140050B7u;
        uint code = (word >> (int)((partition & 15u) * 2u)) & 3u;
        return (0x0608020Fu >> (int)(code * 8u)) & 0xFFu;
    }

    /// <summary>
    ///     Reads one color index from the shared index array.
    /// </summary>
    /// <param name="colorIndex">The per-texel color indices.</param>
    /// <param name="threadBase">The shared memory slot of the first texel of the block.</param>
    /// <param name="index">The texel index within the block.</param>
    /// <returns>The index value.</returns>
    private static uint GetIndex(ArrayView<uint> colorIndex, int threadBase, int index)
        => colorIndex[threadBase + index];

    /// <summary>
    ///     Converts a projection onto an endpoint span into one index of the requested precision.
    /// </summary>
    /// <param name="dotProduct">The projection of the texel onto the span.</param>
    /// <param name="normSqr">The squared length of the span, which always fits a signed 32-bit value.</param>
    /// <param name="precisionRow">The table row of the index precision, zero for four-bit indices.</param>
    /// <param name="tables">The flat BC7 lookup tables.</param>
    /// <returns>The index value of the requested precision.</returns>
    private static uint StepIndex(int dotProduct, int normSqr, int precisionRow, ArrayView<uint> tables)
    {
        int rowOffset = Bc7Tables.Steps + (precisionRow * Bc7Tables.StepRowSize);
        if (normSqr <= 0 || dotProduct <= 0)
        {
            return tables[rowOffset];
        }

        if (dotProduct < normSqr)
        {
            // The shader scales the projection in single precision; the operands are signed so that no unsigned
            // conversion is required, which ILGPU would otherwise lower through a 64-bit float.
            float scaled = dotProduct * 63.49999f;
            int step = (int)(uint)(scaled / normSqr);
            return tables[rowOffset + step];
        }

        return tables[rowOffset + (Bc7Tables.StepRowSize - 1)];
    }

    /// <summary>
    ///     Returns the squared difference of two eight-bit components.
    /// </summary>
    /// <remarks>
    ///     The shader orders the reconstructed and source components with <c>Ensure_A_Is_Larger</c> and then
    ///     subtracts them, which is the absolute difference; squaring the wrapping difference is the same value.
    /// </remarks>
    /// <param name="result">The reconstructed component.</param>
    /// <param name="source">The source component.</param>
    /// <returns>The squared difference.</returns>
    private static int Squared(uint result, uint source)
        => Squared((int)(result - source));

    /// <summary>
    ///     Returns the squared difference of one eight-bit component.
    /// </summary>
    /// <param name="difference">The wrapping difference of two components.</param>
    /// <returns>The squared difference.</returns>
    private static int Squared(uint difference) => Squared((int)difference);

    /// <summary>
    ///     Returns the square of a signed difference.
    /// </summary>
    /// <param name="difference">The difference to square.</param>
    /// <returns>The squared difference.</returns>
    private static int Squared(int difference) => difference * difference;

    /// <summary>
    ///     Merges the minimum and maximum endpoints of two shared memory slots.
    /// </summary>
    /// <param name="low">The shared low endpoint components.</param>
    /// <param name="high">The shared high endpoint components.</param>
    /// <param name="target">The destination slot.</param>
    /// <param name="other">The source slot.</param>
    private static void ReduceMinMax(ArrayView<uint> low, ArrayView<uint> high, int target, int other)
    {
        low[target] = Min(low[target], low[other]);
        low[target + 1] = Min(low[target + 1], low[other + 1]);
        low[target + 2] = Min(low[target + 2], low[other + 2]);
        low[target + 3] = Min(low[target + 3], low[other + 3]);
        high[target] = Max(high[target], high[other]);
        high[target + 1] = Max(high[target + 1], high[other + 1]);
        high[target + 2] = Max(high[target + 2], high[other + 2]);
        high[target + 3] = Max(high[target + 3], high[other + 3]);
    }

    /// <summary>
    ///     Keeps the better of two single-subset candidates.
    /// </summary>
    /// <param name="errors">The shared candidate errors.</param>
    /// <param name="modes">The shared candidate modes.</param>
    /// <param name="selectors">The shared candidate index selectors.</param>
    /// <param name="rotations">The shared candidate rotations.</param>
    /// <param name="target">The destination slot.</param>
    /// <param name="other">The source slot.</param>
    private static void KeepBest(
        ArrayView<uint> errors,
        ArrayView<uint> modes,
        ArrayView<uint> selectors,
        ArrayView<uint> rotations,
        int target,
        int other)
    {
        if (errors[target] > errors[other])
        {
            errors[target] = errors[other];
            modes[target] = modes[other];
            selectors[target] = selectors[other];
            rotations[target] = rotations[other];
        }
    }

    /// <summary>
    ///     Keeps the better of two two-subset candidates.
    /// </summary>
    /// <param name="errors">The shared candidate errors.</param>
    /// <param name="modes">The shared candidate modes.</param>
    /// <param name="partitions">The shared candidate partitions.</param>
    /// <param name="rotations">The shared candidate rotations.</param>
    /// <param name="target">The destination slot.</param>
    /// <param name="other">The source slot.</param>
    private static void KeepBestPartition(
        ArrayView<uint> errors,
        ArrayView<uint> modes,
        ArrayView<uint> partitions,
        ArrayView<uint> rotations,
        int target,
        int other)
    {
        if (errors[target] > errors[other])
        {
            errors[target] = errors[other];
            modes[target] = modes[other];
            partitions[target] = partitions[other];
            rotations[target] = rotations[other];
        }
    }

    /// <summary>
    ///     Swaps the colour components of both endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint pair to swap.</param>
    /// <returns>The swapped endpoint pair.</returns>
    private static EndpointPair SwapRgb(EndpointPair endpoints)
    {
        EndpointPair result;
        result.R0 = endpoints.R1;
        result.G0 = endpoints.G1;
        result.B0 = endpoints.B1;
        result.A0 = endpoints.A0;
        result.R1 = endpoints.R0;
        result.G1 = endpoints.G0;
        result.B1 = endpoints.B0;
        result.A1 = endpoints.A1;
        return result;
    }

    /// <summary>
    ///     Swaps both endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint pair to swap.</param>
    /// <returns>The swapped endpoint pair.</returns>
    private static EndpointPair SwapBoth(EndpointPair endpoints)
    {
        EndpointPair result;
        result.R0 = endpoints.R1;
        result.G0 = endpoints.G1;
        result.B0 = endpoints.B1;
        result.A0 = endpoints.A1;
        result.R1 = endpoints.R0;
        result.G1 = endpoints.G0;
        result.B1 = endpoints.B0;
        result.A1 = endpoints.A0;
        return result;
    }

    /// <summary>
    ///     Compresses both endpoints of a subset to the precision of a mode.
    /// </summary>
    /// <param name="mode">The mode whose endpoint precision applies.</param>
    /// <param name="endpoints">The endpoints to compress.</param>
    /// <param name="p0">The p-bit of the first endpoint.</param>
    /// <param name="p1">The p-bit of the second endpoint.</param>
    /// <returns>The unquantized reconstruction and the packed endpoint values.</returns>
    private static EndpointCompression Compress(uint mode, EndpointPair endpoints, uint p0, uint p1)
    {
        uint unquantized0R = endpoints.R0;
        uint unquantized0G = endpoints.G0;
        uint unquantized0B = endpoints.B0;
        uint unquantized0A = endpoints.A0;
        uint unquantized1R = endpoints.R1;
        uint unquantized1G = endpoints.G1;
        uint unquantized1B = endpoints.B1;
        uint unquantized1A = endpoints.A1;
        uint quantized0R = endpoints.R0;
        uint quantized0G = endpoints.G0;
        uint quantized0B = endpoints.B0;
        uint quantized0A = endpoints.A0;
        uint quantized1R = endpoints.R1;
        uint quantized1G = endpoints.G1;
        uint quantized1B = endpoints.B1;
        uint quantized1A = endpoints.A1;

        if (mode == 0)
        {
            quantized0R = (Quantize(quantized0R, 5) & 0xFFFFFFFEu) | p0;
            quantized0G = (Quantize(quantized0G, 5) & 0xFFFFFFFEu) | p0;
            quantized0B = (Quantize(quantized0B, 5) & 0xFFFFFFFEu) | p0;
            quantized1R = (Quantize(quantized1R, 5) & 0xFFFFFFFEu) | p1;
            quantized1G = (Quantize(quantized1G, 5) & 0xFFFFFFFEu) | p1;
            quantized1B = (Quantize(quantized1B, 5) & 0xFFFFFFFEu) | p1;
            quantized0A = 0xFFu;
            quantized1A = 0xFFu;
            unquantized0R = Unquantize(quantized0R, 5);
            unquantized0G = Unquantize(quantized0G, 5);
            unquantized0B = Unquantize(quantized0B, 5);
            unquantized1R = Unquantize(quantized1R, 5);
            unquantized1G = Unquantize(quantized1G, 5);
            unquantized1B = Unquantize(quantized1B, 5);
            unquantized0A = 0xFFu;
            unquantized1A = 0xFFu;
            quantized0R <<= 3;
            quantized0G <<= 3;
            quantized0B <<= 3;
            quantized0A <<= 3;
            quantized1R <<= 3;
            quantized1G <<= 3;
            quantized1B <<= 3;
            quantized1A <<= 3;
        }
        else if (mode == 1)
        {
            quantized0R = (Quantize(quantized0R, 7) & 0xFFFFFFFEu) | p0;
            quantized0G = (Quantize(quantized0G, 7) & 0xFFFFFFFEu) | p0;
            quantized0B = (Quantize(quantized0B, 7) & 0xFFFFFFFEu) | p0;
            quantized1R = (Quantize(quantized1R, 7) & 0xFFFFFFFEu) | p1;
            quantized1G = (Quantize(quantized1G, 7) & 0xFFFFFFFEu) | p1;
            quantized1B = (Quantize(quantized1B, 7) & 0xFFFFFFFEu) | p1;
            quantized0A = 0xFFu;
            quantized1A = 0xFFu;
            unquantized0R = Unquantize(quantized0R, 7);
            unquantized0G = Unquantize(quantized0G, 7);
            unquantized0B = Unquantize(quantized0B, 7);
            unquantized1R = Unquantize(quantized1R, 7);
            unquantized1G = Unquantize(quantized1G, 7);
            unquantized1B = Unquantize(quantized1B, 7);
            unquantized0A = 0xFFu;
            unquantized1A = 0xFFu;
            quantized0R <<= 1;
            quantized0G <<= 1;
            quantized0B <<= 1;
            quantized0A <<= 1;
            quantized1R <<= 1;
            quantized1G <<= 1;
            quantized1B <<= 1;
            quantized1A <<= 1;
        }
        else if (mode == 2)
        {
            quantized0R = Quantize(quantized0R, 5);
            quantized0G = Quantize(quantized0G, 5);
            quantized0B = Quantize(quantized0B, 5);
            quantized1R = Quantize(quantized1R, 5);
            quantized1G = Quantize(quantized1G, 5);
            quantized1B = Quantize(quantized1B, 5);
            quantized0A = 0xFFu;
            quantized1A = 0xFFu;
            unquantized0R = Unquantize(quantized0R, 5);
            unquantized0G = Unquantize(quantized0G, 5);
            unquantized0B = Unquantize(quantized0B, 5);
            unquantized1R = Unquantize(quantized1R, 5);
            unquantized1G = Unquantize(quantized1G, 5);
            unquantized1B = Unquantize(quantized1B, 5);
            unquantized0A = 0xFFu;
            unquantized1A = 0xFFu;
            quantized0R <<= 3;
            quantized0G <<= 3;
            quantized0B <<= 3;
            quantized0A <<= 3;
            quantized1R <<= 3;
            quantized1G <<= 3;
            quantized1B <<= 3;
            quantized1A <<= 3;
        }
        else if (mode == 3)
        {
            quantized0R = (quantized0R & 0xFFFFFFFEu) | p0;
            quantized0G = (quantized0G & 0xFFFFFFFEu) | p0;
            quantized0B = (quantized0B & 0xFFFFFFFEu) | p0;
            quantized1R = (quantized1R & 0xFFFFFFFEu) | p1;
            quantized1G = (quantized1G & 0xFFFFFFFEu) | p1;
            quantized1B = (quantized1B & 0xFFFFFFFEu) | p1;
            quantized0A = 0xFFu;
            quantized1A = 0xFFu;
            unquantized0R = quantized0R;
            unquantized0G = quantized0G;
            unquantized0B = quantized0B;
            unquantized1R = quantized1R;
            unquantized1G = quantized1G;
            unquantized1B = quantized1B;
            unquantized0A = 0xFFu;
            unquantized1A = 0xFFu;
        }
        else if (mode == 4)
        {
            quantized0R = Quantize(quantized0R, 5);
            quantized0G = Quantize(quantized0G, 5);
            quantized0B = Quantize(quantized0B, 5);
            quantized0A = Quantize(quantized0A, 6);
            quantized1R = Quantize(quantized1R, 5);
            quantized1G = Quantize(quantized1G, 5);
            quantized1B = Quantize(quantized1B, 5);
            quantized1A = Quantize(quantized1A, 6);
            unquantized0R = Unquantize(quantized0R, 5);
            unquantized0G = Unquantize(quantized0G, 5);
            unquantized0B = Unquantize(quantized0B, 5);
            unquantized0A = Unquantize(quantized0A, 6);
            unquantized1R = Unquantize(quantized1R, 5);
            unquantized1G = Unquantize(quantized1G, 5);
            unquantized1B = Unquantize(quantized1B, 5);
            unquantized1A = Unquantize(quantized1A, 6);
            quantized0R <<= 3;
            quantized0G <<= 3;
            quantized0B <<= 3;
            quantized0A <<= 2;
            quantized1R <<= 3;
            quantized1G <<= 3;
            quantized1B <<= 3;
            quantized1A <<= 2;
        }
        else if (mode == 5)
        {
            quantized0R = Quantize(quantized0R, 7);
            quantized0G = Quantize(quantized0G, 7);
            quantized0B = Quantize(quantized0B, 7);
            quantized1R = Quantize(quantized1R, 7);
            quantized1G = Quantize(quantized1G, 7);
            quantized1B = Quantize(quantized1B, 7);
            unquantized0R = Unquantize(quantized0R, 7);
            unquantized0G = Unquantize(quantized0G, 7);
            unquantized0B = Unquantize(quantized0B, 7);
            unquantized1R = Unquantize(quantized1R, 7);
            unquantized1G = Unquantize(quantized1G, 7);
            unquantized1B = Unquantize(quantized1B, 7);
            quantized0R <<= 1;
            quantized0G <<= 1;
            quantized0B <<= 1;
            quantized1R <<= 1;
            quantized1G <<= 1;
            quantized1B <<= 1;
        }
        else if (mode == 6)
        {
            quantized0R = (quantized0R & 0xFFFFFFFEu) | p0;
            quantized0G = (quantized0G & 0xFFFFFFFEu) | p0;
            quantized0B = (quantized0B & 0xFFFFFFFEu) | p0;
            quantized0A = (quantized0A & 0xFFFFFFFEu) | p0;
            quantized1R = (quantized1R & 0xFFFFFFFEu) | p1;
            quantized1G = (quantized1G & 0xFFFFFFFEu) | p1;
            quantized1B = (quantized1B & 0xFFFFFFFEu) | p1;
            quantized1A = (quantized1A & 0xFFFFFFFEu) | p1;
            unquantized0R = quantized0R;
            unquantized0G = quantized0G;
            unquantized0B = quantized0B;
            unquantized0A = quantized0A;
            unquantized1R = quantized1R;
            unquantized1G = quantized1G;
            unquantized1B = quantized1B;
            unquantized1A = quantized1A;
        }
        else
        {
            quantized0R = (Quantize(quantized0R, 6) & 0xFFFFFFFEu) | p0;
            quantized0G = (Quantize(quantized0G, 6) & 0xFFFFFFFEu) | p0;
            quantized0B = (Quantize(quantized0B, 6) & 0xFFFFFFFEu) | p0;
            quantized0A = (Quantize(quantized0A, 6) & 0xFFFFFFFEu) | p0;
            quantized1R = (Quantize(quantized1R, 6) & 0xFFFFFFFEu) | p1;
            quantized1G = (Quantize(quantized1G, 6) & 0xFFFFFFFEu) | p1;
            quantized1B = (Quantize(quantized1B, 6) & 0xFFFFFFFEu) | p1;
            quantized1A = (Quantize(quantized1A, 6) & 0xFFFFFFFEu) | p1;
            unquantized0R = Unquantize(quantized0R, 6);
            unquantized0G = Unquantize(quantized0G, 6);
            unquantized0B = Unquantize(quantized0B, 6);
            unquantized0A = Unquantize(quantized0A, 6);
            unquantized1R = Unquantize(quantized1R, 6);
            unquantized1G = Unquantize(quantized1G, 6);
            unquantized1B = Unquantize(quantized1B, 6);
            unquantized1A = Unquantize(quantized1A, 6);
            quantized0R <<= 2;
            quantized0G <<= 2;
            quantized0B <<= 2;
            quantized0A <<= 2;
            quantized1R <<= 2;
            quantized1G <<= 2;
            quantized1B <<= 2;
            quantized1A <<= 2;
        }

        EndpointCompression result;
        result.Unquantized.R0 = unquantized0R;
        result.Unquantized.G0 = unquantized0G;
        result.Unquantized.B0 = unquantized0B;
        result.Unquantized.A0 = unquantized0A;
        result.Unquantized.R1 = unquantized1R;
        result.Unquantized.G1 = unquantized1G;
        result.Unquantized.B1 = unquantized1B;
        result.Unquantized.A1 = unquantized1A;
        result.Quantized.R0 = quantized0R;
        result.Quantized.G0 = quantized0G;
        result.Quantized.B0 = quantized0B;
        result.Quantized.A0 = quantized0A;
        result.Quantized.R1 = quantized1R;
        result.Quantized.G1 = quantized1G;
        result.Quantized.B1 = quantized1B;
        result.Quantized.A1 = quantized1A;
        return result;
    }

    /// <summary>
    ///     Returns the smaller of two unsigned values.
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>The smaller value.</returns>
    private static uint Min(uint left, uint right) => left < right ? left : right;

    /// <summary>
    ///     Returns the larger of two unsigned values.
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>The larger value.</returns>
    private static uint Max(uint left, uint right) => left > right ? left : right;

    /// <summary>
    ///     Rounds an eight-bit component to the requested precision.
    /// </summary>
    /// <param name="color">The eight-bit component.</param>
    /// <param name="precision">The target precision, in bits.</param>
    /// <returns>The quantized component.</returns>
    private static uint Quantize(uint color, uint precision)
        => ((((color << 8) + color) * ((1u << (int)precision) - 1u)) + 32768u) >> 16;

    /// <summary>
    ///     Expands a quantized component back to eight bits.
    /// </summary>
    /// <param name="color">The quantized component.</param>
    /// <param name="precision">The precision of the component, in bits.</param>
    /// <returns>The expanded component.</returns>
    private static uint Unquantize(uint color, uint precision)
    {
        uint shifted = color << (int)(8 - precision);
        return shifted | (shifted >> (int)precision);
    }
}

/// <summary>
///     The two endpoints of one BC7 subset.
/// </summary>
internal struct EndpointPair
{
    /// <summary>The red component of the first endpoint.</summary>
    public uint R0;

    /// <summary>The green component of the first endpoint.</summary>
    public uint G0;

    /// <summary>The blue component of the first endpoint.</summary>
    public uint B0;

    /// <summary>The alpha component of the first endpoint.</summary>
    public uint A0;

    /// <summary>The red component of the second endpoint.</summary>
    public uint R1;

    /// <summary>The green component of the second endpoint.</summary>
    public uint G1;

    /// <summary>The blue component of the second endpoint.</summary>
    public uint B1;

    /// <summary>The alpha component of the second endpoint.</summary>
    public uint A1;
}

/// <summary>
///     The result of compressing one BC7 subset: the reconstructed endpoints and the values to pack.
/// </summary>
internal struct EndpointCompression
{
    /// <summary>The endpoints expanded back to eight bits.</summary>
    public EndpointPair Unquantized;

    /// <summary>The endpoints reduced to the mode precision and shifted into their packed position.</summary>
    public EndpointPair Quantized;
}
