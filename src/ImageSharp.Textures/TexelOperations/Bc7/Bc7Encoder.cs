using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace ImageSharp.Textures.Texels.Bc7;

internal static class Bc7Encoder
{
    /// <summary>Bytes per BC7 block.</summary>
    public const int BlockBytes = 16;

    /// <summary>Encoded size in bytes for a surface of the given size.</summary>
    public static int GetEncodedByteCount(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                width <= 0 ? nameof(width) : nameof(height), "Dimensions must be positive.");
        }

        return ((width + 3) >> 2) * ((height + 3) >> 2) * BlockBytes;
    }

    /// <summary>Encodes <paramref name="rgbaSource" /> (tightly packed RGBA32) into BC7 blocks.</summary>
    /// <param name="rgbaSource">Source pixels, <c>width * height * 4</c> bytes, top-left origin.</param>
    /// <param name="dest">Destination blocks; at least <see cref="GetEncodedByteCount" /> bytes.</param>
    /// <param name="width">Source width in pixels.</param>
    /// <param name="height">Source height in pixels.</param>
    /// <param name="quality">Mode search to run.</param>
    public static void Encode(
        ReadOnlySpan<byte> rgbaSource,
        Span<byte> dest,
        int width,
        int height,
        Bc7EncodingQuality quality)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
        }

        if (quality is not (Bc7EncodingQuality.Quick or Bc7EncodingQuality.Default))
        {
            throw new ArgumentOutOfRangeException(nameof(quality), quality, "Unknown BC7 encoding quality.");
        }

        long pixelBytes = (long)width * height * 4;
        if (rgbaSource.Length < pixelBytes)
        {
            throw new ArgumentException("Source is smaller than width * height * 4 bytes.", nameof(rgbaSource));
        }

        int blocksX = (width + 3) >> 2;
        int blocksY = (height + 3) >> 2;
        int required = blocksX * blocksY * BlockBytes;
        if (dest.Length < required)
        {
            throw new ArgumentException("Destination is smaller than the BC7 surface.", nameof(dest));
        }

        Span<byte> byteScratch = stackalloc byte[Bc7Scratch.ByteCount];
        Span<int> intScratch = stackalloc int[Bc7Scratch.IntCount];
        Span<float> floatScratch = stackalloc float[Bc7Scratch.FloatCount];
        Span<Vector256<int>> invariantScratch = stackalloc Vector256<int>[16];
        Span<byte> blockPixels = stackalloc byte[16 * 4];
        Span<int> pixelIndexMap = stackalloc int[16];

        Bc7Scratch scratch = new(byteScratch, intScratch, floatScratch, invariantScratch);
        Bc7BlockEncoder encoder = new(scratch, quality == Bc7EncodingQuality.Quick);

        int block = 0;
        for (int by = 0; by < blocksY; by++)
        {
            for (int bx = 0; bx < blocksX; bx++)
            {
                FillBlock(rgbaSource, width, height, bx, by, blockPixels, pixelIndexMap);
                encoder.EncodeBlock(blockPixels, dest.Slice(block * BlockBytes, BlockBytes));
                block++;
            }
        }
    }

    /// <summary>Gathers one 4x4 block, replicating edges exactly as DirectXTex's CompressBC does.</summary>
    private static void FillBlock(
        ReadOnlySpan<byte> source,
        int width,
        int height,
        int bx,
        int by,
        Span<byte> pixels,
        Span<int> indexMap)
    {
        int x0 = bx << 2;
        int y0 = by << 2;
        int pw = Math.Min(4, width - x0);
        int ph = Math.Min(4, height - y0);

        for (int t = 0; t < 4; t++)
        {
            for (int s = 0; s < 4; s++)
            {
                indexMap[(t << 2) | s] = -1;
            }
        }

        for (int t = 0; t < ph; t++)
        {
            for (int s = 0; s < pw; s++)
            {
                indexMap[(t << 2) | s] = ((y0 + t) * width) + x0 + s;
            }
        }

        if (pw != 4 || ph != 4)
        {
            // DirectXTex: static const size_t uSrc[] = { 0, 0, 0, 1 };
            if (pw < 4)
            {
                for (int t = 0; t < ph && t < 4; t++)
                {
                    for (int s = pw; s < 4; s++)
                    {
                        indexMap[(t << 2) | s] = indexMap[(t << 2) | EdgeSource(s)];
                    }
                }
            }

            if (ph < 4)
            {
                for (int t = ph; t < 4; t++)
                {
                    for (int s = 0; s < 4; s++)
                    {
                        indexMap[(t << 2) | s] = indexMap[(EdgeSource(t) << 2) | s];
                    }
                }
            }
        }

        for (int i = 0; i < 16; i++)
        {
            int p = indexMap[i] * 4;
            int o = i << 2;
            pixels[o] = source[p];
            pixels[o + 1] = source[p + 1];
            pixels[o + 2] = source[p + 2];
            pixels[o + 3] = source[p + 3];
        }
    }

    /// <summary>DirectXTex's partial-block source index map: <c>{ 0, 0, 0, 1 }</c>.</summary>
    private static int EdgeSource(int i) => i == 3 ? 1 : 0;

    private readonly struct Bc7ModeInfo
    {
        /// <summary>Number of subsets minus one (0, 1 or 2).</summary>
        public readonly byte Partitions;

        /// <summary>Partition field width in bits.</summary>
        public readonly byte PartitionBits;

        /// <summary>Number of p-bits (0, 2, 4 or 6).</summary>
        public readonly byte PBits;

        /// <summary>Rotation field width in bits.</summary>
        public readonly byte RotationBits;

        /// <summary>Index-mode field width in bits.</summary>
        public readonly byte IndexModeBits;

        /// <summary>Colour index precision.</summary>
        public readonly byte IndexPrec;

        /// <summary>Alpha index precision (0 when colour and alpha share indices).</summary>
        public readonly byte IndexPrec2;

        public Bc7ModeInfo(
            byte partitions,
            byte partitionBits,
            byte pBits,
            byte rotationBits,
            byte indexModeBits,
            byte indexPrec,
            byte indexPrec2,
            uint prec,
            uint precWithP)
        {
            this.Partitions = partitions;
            this.PartitionBits = partitionBits;
            this.PBits = pBits;
            this.RotationBits = rotationBits;
            this.IndexModeBits = indexModeBits;
            this.IndexPrec = indexPrec;
            this.IndexPrec2 = indexPrec2;
            this.PackedPrec = prec;
            this.PackedPrecWithP = precWithP;
        }

        /// <summary>Gets the packed per-channel endpoint precision (r | g &lt;&lt; 8 | b &lt;&lt; 16 | a &lt;&lt; 24).</summary>
        public uint PackedPrec { get; }

        /// <summary>Gets the packed per-channel endpoint precision including the p-bit.</summary>
        public uint PackedPrecWithP { get; }

        /// <summary>Endpoint precision for a channel (0 = r, 1 = g, 2 = b, 3 = a).</summary>
        public byte Prec(int channel) => (byte)(this.PackedPrec >> (channel * 8));

        /// <summary>Endpoint precision including the p-bit for a channel.</summary>
        public byte PrecWithP(int channel) => (byte)(this.PackedPrecWithP >> (channel * 8));
    }

    private struct HdrColor
    {
        public float R;
        public float G;
        public float B;
        public float A;

        public HdrColor(float r, float g, float b, float a)
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = a;
        }

        /// <summary>Dot product (DirectXTex <c>HDRColorA::operator*(const HDRColorA&amp;)</c>).</summary>
        public readonly float Dot(in HdrColor other)
            => (this.R * other.R) + (this.G * other.G) + (this.B * other.B) + (this.A * other.A);

        public readonly HdrColor Add(in HdrColor other)
            => new(this.R + other.R, this.G + other.G, this.B + other.B, this.A + other.A);

        public readonly HdrColor Sub(in HdrColor other)
            => new(this.R - other.R, this.G - other.G, this.B - other.B, this.A - other.A);

        public readonly HdrColor Mul(float f) => new(this.R * f, this.G * f, this.B * f, this.A * f);

        /// <summary>Clamps all four channels into [min, max] in place.</summary>
        public void Clamp(float min, float max)
        {
            this.R = Clamp(this.R, min, max);
            this.G = Clamp(this.G, min, max);
            this.B = Clamp(this.B, min, max);
            this.A = Clamp(this.A, min, max);
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }

    private ref struct Bc7Scratch
    {
        /// <summary>Serialized LDREndPntPair per shape and region: 64 shapes x 3 regions x (A.rgba, B.rgba).</summary>
        public readonly Span<byte> EndPoints;

        /// <summary>Quantized region palettes: 3 regions x 4 channels x 16 entries (channel-planar).</summary>
        public readonly Span<byte> Palettes;

        /// <summary>Palette used by MapColors: 4 channels x 16 entries (channel-planar).</summary>
        public readonly Span<byte> MapPalette;

        /// <summary>Copy of the current region's LDR pixels (16 x rgba).</summary>
        public readonly Span<byte> RegionPixels;

        /// <summary>The current block's 16 RGBA pixels, possibly channel-rotated for the search.</summary>
        public readonly Span<byte> Ldr;

        /// <summary>Region of each pixel for the current shape.</summary>
        public readonly Span<byte> Regions;

        /// <summary>Per-pixel palette errors for MapColors (16 entries).</summary>
        public readonly Span<int> Errors;

        /// <summary>Per-pixel palette error with one channel's contribution omitted (16 pixels x 8 entries).</summary>
        public readonly Span<Vector256<int>> Invariant;

        public readonly Span<byte> IndexOrg;
        public readonly Span<byte> IndexOrg2;
        public readonly Span<byte> IndexOpt;
        public readonly Span<byte> IndexOpt2;

        /// <summary>Quantized candidate endpoints (3 pairs).</summary>
        public readonly Span<byte> EndPointsQuantized;

        /// <summary>Candidate endpoints after the p-bit vote (3 pairs).</summary>
        public readonly Span<byte> EndPointsFixed1;

        /// <summary>Optimized candidate endpoints (3 pairs).</summary>
        public readonly Span<byte> EndPointsOptimized;

        /// <summary>Optimized endpoints after the p-bit vote (3 pairs).</summary>
        public readonly Span<byte> EndPointsFixed2;

        /// <summary>Block currently being emitted.</summary>
        public readonly Span<byte> CurrentBlock;

        /// <summary>Best block emitted so far.</summary>
        public readonly Span<byte> BestBlock;

        public readonly Span<byte> NewA;
        public readonly Span<byte> NewB;
        public readonly Span<byte> NewTmp;
        public readonly Span<byte> PerturbTmp;
        public readonly Span<byte> PVote;
        public readonly Span<byte> PCount;

        public readonly Span<int> PixelIndices;
        public readonly Span<int> RoughErrors;
        public readonly Span<int> RoughShapes;
        public readonly Span<int> ErrorOrg;
        public readonly Span<int> ErrorOpt;
        public readonly Span<int> PBits;

        public readonly Span<float> Hdr;

        /// <summary>Byte scratch required by <see cref="Bc7Scratch" />.</summary>
        public const int ByteCount = (64 * 3 * 8) + (3 * 4 * 16) + (4 * 16) + (16 * 4) + (16 * 4) + 16
                                     + (4 * 16) + (4 * 24) + (2 * 16) + (4 * 8) + 6 + 6;

        /// <summary>Int scratch required by <see cref="Bc7Scratch" />.</summary>
        public const int IntCount = 16 + 64 + 64 + 3 + 3 + 6 + 16;

        /// <summary>Float scratch required by <see cref="Bc7Scratch" />.</summary>
        public const int FloatCount = 16 * 4;

        /// <summary>Initializes a new instance of the <see cref="Bc7Scratch" /> struct.</summary>
        /// <param name="bytes">Byte scratch buffer.</param>
        /// <param name="ints">Int scratch buffer.</param>
        /// <param name="floats">Float scratch buffer.</param>
        /// <param name="invariant">Invariant vector scratch buffer.</param>
        public Bc7Scratch(Span<byte> bytes, Span<int> ints, Span<float> floats, Span<Vector256<int>> invariant)
        {
            int b = 0;
            this.EndPoints = bytes.Slice(b, 64 * 3 * 8);
            b += 64 * 3 * 8;
            this.Palettes = bytes.Slice(b, 3 * 4 * 16);
            b += 3 * 4 * 16;
            this.MapPalette = bytes.Slice(b, 4 * 16);
            b += 4 * 16;
            this.RegionPixels = bytes.Slice(b, 16 * 4);
            b += 16 * 4;
            this.Ldr = bytes.Slice(b, 16 * 4);
            b += 16 * 4;
            this.Regions = bytes.Slice(b, 16);
            b += 16;
            this.IndexOrg = bytes.Slice(b, 16);
            b += 16;
            this.IndexOrg2 = bytes.Slice(b, 16);
            b += 16;
            this.IndexOpt = bytes.Slice(b, 16);
            b += 16;
            this.IndexOpt2 = bytes.Slice(b, 16);
            b += 16;
            this.EndPointsQuantized = bytes.Slice(b, 24);
            b += 24;
            this.EndPointsFixed1 = bytes.Slice(b, 24);
            b += 24;
            this.EndPointsOptimized = bytes.Slice(b, 24);
            b += 24;
            this.EndPointsFixed2 = bytes.Slice(b, 24);
            b += 24;
            this.CurrentBlock = bytes.Slice(b, 16);
            b += 16;
            this.BestBlock = bytes.Slice(b, 16);
            b += 16;
            this.NewA = bytes.Slice(b, 8);
            b += 8;
            this.NewB = bytes.Slice(b, 8);
            b += 8;
            this.NewTmp = bytes.Slice(b, 8);
            b += 8;
            this.PerturbTmp = bytes.Slice(b, 8);
            b += 8;
            this.PVote = bytes.Slice(b, 6);
            b += 6;
            this.PCount = bytes.Slice(b, 6);
            b += 6;

            int i = 0;
            this.PixelIndices = ints.Slice(i, 16);
            i += 16;
            this.RoughErrors = ints.Slice(i, 64);
            i += 64;
            this.RoughShapes = ints.Slice(i, 64);
            i += 64;
            this.ErrorOrg = ints.Slice(i, 3);
            i += 3;
            this.ErrorOpt = ints.Slice(i, 3);
            i += 3;
            this.PBits = ints.Slice(i, 6);
            i += 6;
            this.Errors = ints.Slice(i, 16);
            this.Invariant = invariant;

            this.Hdr = floats.Slice(0, 16 * 4);
        }
    }

    private ref struct Bc7BlockEncoder
    {
        private readonly bool quick;
        private readonly Bc7Scratch state;
        private Bc7ModeInfo modeInfo;
        private int mode;
        private int indexMode;

        // Precision bytes of the current mode, packed as (r | g << 8 | b << 16 | a << 24); read by the
        // hot paths so they do not pay for the per-channel switch on Bc7ModeInfo.
        private uint precPacked;
        private uint precWithPPacked;

        public Bc7BlockEncoder(Bc7Scratch scratch, bool quick)
        {
            this.state = scratch;
            this.quick = quick;
            this.modeInfo = default;
            this.mode = 0;
            this.indexMode = 0;
            this.precPacked = 0;
            this.precWithPPacked = 0;
        }

        // Interpolation weights padded to 16 lanes (entries past the precision are never read).
        private static Vector256<ushort> Weights2Vector => Vector256.Create<ushort>(
            [0, 21, 43, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64]);

        private static Vector256<ushort> Weights3Vector => Vector256.Create<ushort>(
            [0, 9, 18, 27, 37, 46, 55, 64, 64, 64, 64, 64, 64, 64, 64, 64]);

        private static Vector256<ushort> Weights4Vector => Vector256.Create<ushort>(
            [0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64]);

        private static Vector256<ushort> Complement64 => Vector256.Create((ushort)64);

        private static Vector256<ushort> Round32 => Vector256.Create((ushort)32);

        private static Vector256<int> Weights3x8 => Vector256.Create(0, 9, 18, 27, 37, 46, 55, 64);

        private static Vector256<int> Complement3x8 => Vector256.Create(64, 55, 46, 37, 27, 18, 9, 0);

        // 2-bit weights padded to 8 lanes (lanes 4..7 are masked out of every reduction).
        private static Vector256<int> Weights2x8 => Vector256.Create(0, 21, 43, 64, 64, 64, 64, 64);

        private static Vector256<int> Complement2x8 => Vector256.Create(64, 43, 21, 0, 0, 0, 0, 0);

        private static Vector256<int> InvalidLanes4 =>
            Vector256.Create(0, 0, 0, 0, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);

        private static Vector256<int> NoInvalidLanes => Vector256<int>.Zero;

        // Lane j receives lane j-1 (used for the "sequence rose" test); lane 0 is unused.
        private static Vector256<int> ShiftByOneLane => Vector256.Create(7, 0, 1, 2, 3, 4, 5, 6);

        private static ReadOnlySpan<int> Complement2 => [64, 43, 21, 0];

        private static ReadOnlySpan<int> Complement4 => [64, 60, 55, 51, 47, 43, 38, 34, 30, 26, 21, 17, 13, 9, 4, 0];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Prec(int channel) => (int)((this.precPacked >> (channel * 8)) & 0xFF);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int PrecWithP(int channel) => (int)((this.precWithPPacked >> (channel * 8)) & 0xFF);

        /// <summary>Encodes one 4x4 block from tightly packed RGBA32 pixels into <paramref name="dest" />.</summary>
        public void EncodeBlock(ReadOnlySpan<byte> rgba16, Span<byte> dest)
        {
            const float Inv255 = 1.0f / 255.0f;

            int alphaMask = 0xFF;
            for (int i = 0; i < 16; i++)
            {
                int o = i << 2;
                byte r = rgba16[o];
                byte g = rgba16[o + 1];
                byte b = rgba16[o + 2];
                byte a = rgba16[o + 3];
                this.state.Ldr[o] = r;
                this.state.Ldr[o + 1] = g;
                this.state.Ldr[o + 2] = b;
                this.state.Ldr[o + 3] = a;
                this.state.Hdr[o] = r * Inv255;
                this.state.Hdr[o + 1] = g * Inv255;
                this.state.Hdr[o + 2] = b * Inv255;
                this.state.Hdr[o + 3] = a * Inv255;
                alphaMask &= a;
            }

            bool hasAlpha = alphaMask != 0xFF;

            int bestErr = int.MaxValue;

            for (int m = 0; m < 8 && bestErr > 0; m++)
            {
                if (m is 0 or 2)
                {
                    // 3-subset modes: DirectXTex only searches them with -bc x (BC_FLAGS_USE_3SUBSETS).
                    continue;
                }

                if (this.quick && m != 6)
                {
                    continue;
                }

                if (!hasAlpha && m == 7)
                {
                    // Mode 7 buys nothing over the other 2-subset modes for fully opaque blocks.
                    continue;
                }

                this.mode = m;
                this.modeInfo = Bc7Tables.Modes[m];
                this.precPacked = this.modeInfo.PackedPrec;
                this.precWithPPacked = this.modeInfo.PackedPrecWithP;

                int shapes = 1 << this.modeInfo.PartitionBits;
                int rotations = 1 << this.modeInfo.RotationBits;
                int indexModes = 1 << this.modeInfo.IndexModeBits;
                int items = shapes >> 2;
                if (items < 1)
                {
                    items = 1;
                }

                for (int r = 0; r < rotations && bestErr > 0; r++)
                {
                    this.Rotate(r);

                    for (int im = 0; im < indexModes && bestErr > 0; im++)
                    {
                        this.indexMode = im;

                        for (int s = 0; s < shapes; s++)
                        {
                            this.state.RoughErrors[s] = this.RoughMse(s);
                            this.state.RoughShapes[s] = s;
                        }

                        // Bubble the best `items` shapes to the front.
                        for (int i = 0; i < items; i++)
                        {
                            for (int j = i + 1; j < shapes; j++)
                            {
                                if (this.state.RoughErrors[i] > this.state.RoughErrors[j])
                                {
                                    (this.state.RoughErrors[i], this.state.RoughErrors[j]) =
                                        (this.state.RoughErrors[j], this.state.RoughErrors[i]);
                                    (this.state.RoughShapes[i], this.state.RoughShapes[j]) =
                                        (this.state.RoughShapes[j], this.state.RoughShapes[i]);
                                }
                            }
                        }

                        for (int i = 0; i < items && bestErr > 0; i++)
                        {
                            int mse = this.Refine(this.state.RoughShapes[i], r, im);
                            if (mse < bestErr)
                            {
                                this.state.CurrentBlock.CopyTo(this.state.BestBlock);
                                bestErr = mse;
                            }
                        }
                    }

                    this.Rotate(r);
                }
            }

            this.state.BestBlock.CopyTo(dest);
        }

        /// <summary>Swaps a colour channel with alpha for the search (involutive).</summary>
        private void Rotate(int r)
        {
            if (r == 0)
            {
                return;
            }

            int ch = r - 1;
            for (int i = 0; i < 16; i++)
            {
                int o = i << 2;
                (this.state.Ldr[o + ch], this.state.Ldr[o + 3]) = (this.state.Ldr[o + 3], this.state.Ldr[o + ch]);
            }
        }

        /// <summary>RoughMSE: seeds the endpoints per shape/region and returns the resulting error.</summary>
        private int RoughMse(int shape)
        {
            int partitions = this.modeInfo.Partitions;
            int indexPrec = this.indexMode != 0 ? this.modeInfo.IndexPrec2 : this.modeInfo.IndexPrec;
            int indexPrec2 = this.indexMode != 0 ? this.modeInfo.IndexPrec : this.modeInfo.IndexPrec2;
            int numIndices = 1 << indexPrec;
            int numIndices2 = 1 << indexPrec2;

            Span<byte> endPts = this.state.EndPoints.Slice(shape * 24, 24);
            Span<int> pixIdx = this.state.PixelIndices;
            this.SetRegions(shape);

            for (int p = 0; p <= partitions; p++)
            {
                int np = 0;
                for (int i = 0; i < 16; i++)
                {
                    if (this.state.Regions[i] == p)
                    {
                        pixIdx[np++] = i;
                    }
                }

                int aOff = p * 8;
                int bOff = aOff + 4;

                if (np == 1)
                {
                    this.CopyPixel(endPts, aOff, pixIdx[0]);
                    this.CopyPixel(endPts, bOff, pixIdx[0]);
                    continue;
                }

                if (np == 2)
                {
                    this.CopyPixel(endPts, aOff, pixIdx[0]);
                    this.CopyPixel(endPts, bOff, pixIdx[1]);
                    continue;
                }

                if (indexPrec2 == 0)
                {
                    Bc7ColorMath.OptimizeRgba(this.state.Hdr, pixIdx, np, 4, out HdrColor epA, out HdrColor epB);
                    epA.Clamp(0.0f, 1.0f);
                    epB.Clamp(0.0f, 1.0f);
                    StoreEndPoint(endPts, aOff, epA);
                    StoreEndPoint(endPts, bOff, epB);
                }
                else
                {
                    byte minAlpha = 255;
                    byte maxAlpha = 0;
                    for (int i = 0; i < 16; i++)
                    {
                        byte a = this.state.Ldr[(pixIdx[i] << 2) + 3];
                        if (a < minAlpha)
                        {
                            minAlpha = a;
                        }

                        if (a > maxAlpha)
                        {
                            maxAlpha = a;
                        }
                    }

                    Bc7ColorMath.OptimizeRgb(this.state.Hdr, pixIdx, np, 4, out HdrColor epA, out HdrColor epB);
                    epA.Clamp(0.0f, 1.0f);
                    epB.Clamp(0.0f, 1.0f);
                    StoreEndPoint(endPts, aOff, epA);
                    StoreEndPoint(endPts, bOff, epB);
                    endPts[aOff + 3] = minAlpha;
                    endPts[bOff + 3] = maxAlpha;
                }
            }

            // RoughMSE interpolates the raw (already 8-bit) endpoints; no unquantize step.
            for (int p = 0; p <= partitions; p++)
            {
                int e = p * 8;
                InterpolateInto(
                    this.state.Palettes,
                    p * 64,
                    endPts[e],
                    endPts[e + 1],
                    endPts[e + 2],
                    endPts[e + 3],
                    endPts[e + 4],
                    endPts[e + 5],
                    endPts[e + 6],
                    endPts[e + 7],
                    indexPrec,
                    indexPrec2);
            }

            int totalErr = 0;
            for (int i = 0; i < 16; i++)
            {
                int region = this.state.Regions[i];
                totalErr += ComputeError(
                    this.state.Ldr, i, this.state.Palettes, region * 64, numIndices, numIndices2, out _, out _);
            }

            return totalErr;
        }

        /// <summary>Refine: quantizes the rough endpoints, optimizes them, keeps whichever is better.</summary>
        private int Refine(int shape, int rotation, int indexMode)
        {
            this.indexMode = indexMode;

            int partitions = this.modeInfo.Partitions;
            Span<byte> endPts = this.state.EndPoints.Slice(shape * 24, 24);
            Span<byte> quantized = this.state.EndPointsQuantized;
            Span<byte> fixed1 = this.state.EndPointsFixed1;
            Span<byte> optimized = this.state.EndPointsOptimized;
            Span<byte> fixed2 = this.state.EndPointsFixed2;
            this.SetRegions(shape);

            for (int p = 0; p <= partitions; p++)
            {
                this.QuantizeEndPoint(endPts, p * 8, quantized, p * 8);
                this.QuantizeEndPoint(endPts, (p * 8) + 4, quantized, (p * 8) + 4);
            }

            this.FixEndPointPBits(quantized, fixed1);
            this.AssignIndices(shape, fixed1, this.state.IndexOrg, this.state.IndexOrg2, this.state.ErrorOrg);
            this.OptimizeEndPoints(shape, this.state.ErrorOrg, fixed1, optimized);
            this.FixEndPointPBits(optimized, fixed2);
            this.AssignIndices(shape, fixed2, this.state.IndexOpt, this.state.IndexOpt2, this.state.ErrorOpt);

            int orgTotal = 0;
            int optTotal = 0;
            for (int p = 0; p <= partitions; p++)
            {
                orgTotal += this.state.ErrorOrg[p];
                optTotal += this.state.ErrorOpt[p];
            }

            if (optTotal < orgTotal)
            {
                this.EmitBlock(shape, rotation, indexMode, fixed2, this.state.IndexOpt, this.state.IndexOpt2);
                return optTotal;
            }

            this.EmitBlock(shape, rotation, indexMode, fixed1, this.state.IndexOrg, this.state.IndexOrg2);
            return orgTotal;
        }

        private void SetRegions(int shape)
        {
            int partitions = this.modeInfo.Partitions;
            if (partitions == 0)
            {
                this.state.Regions.Clear();
            }
            else
            {
                Bc7Tables.Partition2Subsets.Slice(shape << 4, 16).CopyTo(this.state.Regions);
            }
        }

        /// <summary>Builds the quantized palette of one region from its candidate endpoints.</summary>
        private void GeneratePaletteQuantized(ReadOnlySpan<byte> endPts, int endPointOff, Span<byte> pal, int palBase)
        {
            int indexPrec = this.indexMode != 0 ? this.modeInfo.IndexPrec2 : this.modeInfo.IndexPrec;
            int indexPrec2 = this.indexMode != 0 ? this.modeInfo.IndexPrec : this.modeInfo.IndexPrec2;

            int a0 = UnquantizeChannel(endPts[endPointOff], this.PrecWithP(0));
            int a1 = UnquantizeChannel(endPts[endPointOff + 1], this.PrecWithP(1));
            int a2 = UnquantizeChannel(endPts[endPointOff + 2], this.PrecWithP(2));
            int b0 = UnquantizeChannel(endPts[endPointOff + 4], this.PrecWithP(0));
            int b1 = UnquantizeChannel(endPts[endPointOff + 5], this.PrecWithP(1));
            int b2 = UnquantizeChannel(endPts[endPointOff + 6], this.PrecWithP(2));

            int alphaPrec = this.PrecWithP(3);
            int a3 = alphaPrec != 0 ? UnquantizeChannel(endPts[endPointOff + 3], alphaPrec) : 255;
            int b3 = alphaPrec != 0 ? UnquantizeChannel(endPts[endPointOff + 7], alphaPrec) : 255;

            InterpolateInto(pal, palBase, a0, a1, a2, a3, b0, b1, b2, b3, indexPrec, indexPrec2);
        }

        /// <summary>
        ///     Interpolates a palette of <paramref name="indexPrec" /> colour entries (and, when separate,
        ///     <paramref name="indexPrec2" /> alpha entries) into channel-planar 8-bit storage.
        /// </summary>
        private static void InterpolateInto(
            Span<byte> pal,
            int palBase,
            int a0,
            int a1,
            int a2,
            int a3,
            int b0,
            int b1,
            int b2,
            int b3,
            int indexPrec,
            int indexPrec2)
        {
            int alphaPrec = indexPrec2 == 0 ? indexPrec : indexPrec2;

            if (Vector256.IsHardwareAccelerated)
            {
                Vector256<ushort> w = WeightsVector(indexPrec);
                Vector256<ushort> cw = Complement64 - w;
                InterpolateChannelVector(pal, palBase + 0, a0, b0, w, cw);
                InterpolateChannelVector(pal, palBase + 16, a1, b1, w, cw);
                InterpolateChannelVector(pal, palBase + 32, a2, b2, w, cw);

                if (indexPrec2 == 0 || indexPrec2 == indexPrec)
                {
                    InterpolateChannelVector(pal, palBase + 48, a3, b3, w, cw);
                }
                else
                {
                    Vector256<ushort> wa = WeightsVector(indexPrec2);
                    InterpolateChannelVector(pal, palBase + 48, a3, b3, wa, Complement64 - wa);
                }

                return;
            }

            if (Vector128.IsHardwareAccelerated)
            {
                Vector256<ushort> w = WeightsVector(indexPrec);
                Vector256<ushort> cw = Complement64 - w;
                InterpolateChannelVector128(pal, palBase + 0, a0, b0, w.GetLower(), w.GetUpper(), cw.GetLower(),
                    cw.GetUpper());
                InterpolateChannelVector128(pal, palBase + 16, a1, b1, w.GetLower(), w.GetUpper(), cw.GetLower(),
                    cw.GetUpper());
                InterpolateChannelVector128(pal, palBase + 32, a2, b2, w.GetLower(), w.GetUpper(), cw.GetLower(),
                    cw.GetUpper());

                if (indexPrec2 == 0 || indexPrec2 == indexPrec)
                {
                    InterpolateChannelVector128(pal, palBase + 48, a3, b3, w.GetLower(), w.GetUpper(), cw.GetLower(),
                        cw.GetUpper());
                }
                else
                {
                    Vector256<ushort> wa = WeightsVector(indexPrec2);
                    Vector256<ushort> cwa = Complement64 - wa;
                    InterpolateChannelVector128(pal, palBase + 48, a3, b3, wa.GetLower(), wa.GetUpper(), cwa.GetLower(),
                        cwa.GetUpper());
                }

                return;
            }

            ReadOnlySpan<int> weights = Bc7Tables.Weights(indexPrec);
            for (int i = 0; i < 1 << indexPrec; i++)
            {
                int wi = weights[i];
                pal[palBase + i] = (byte)(((a0 * (64 - wi)) + (b0 * wi) + 32) >> 6);
                pal[palBase + 16 + i] = (byte)(((a1 * (64 - wi)) + (b1 * wi) + 32) >> 6);
                pal[palBase + 32 + i] = (byte)(((a2 * (64 - wi)) + (b2 * wi) + 32) >> 6);
            }

            ReadOnlySpan<int> alphaWeights = Bc7Tables.Weights(alphaPrec);
            for (int i = 0; i < 1 << alphaPrec; i++)
            {
                int wi = alphaWeights[i];
                pal[palBase + 48 + i] = (byte)(((a3 * (64 - wi)) + (b3 * wi) + 32) >> 6);
            }
        }

        private static void InterpolateChannelVector(
            Span<byte> pal, int channelBase, int a, int b, Vector256<ushort> w, Vector256<ushort> complement)
        {
            Vector256<ushort> av = Vector256.Create((ushort)a);
            Vector256<ushort> bv = Vector256.Create((ushort)b);
            Vector256<ushort> v = ((av * complement) + (bv * w) + Round32) >>> 6;
            Vector256.Narrow(v, v).GetLower().CopyTo(pal.Slice(channelBase, 16));
        }

        private static void InterpolateChannelVector128(
            Span<byte> pal,
            int channelBase,
            int a,
            int b,
            Vector128<ushort> wLo,
            Vector128<ushort> wHi,
            Vector128<ushort> cwLo,
            Vector128<ushort> cwHi)
        {
            Vector128<ushort> av = Vector128.Create((ushort)a);
            Vector128<ushort> bv = Vector128.Create((ushort)b);
            Vector128<ushort> round = Vector128.Create((ushort)32);
            Vector128<ushort> lo = ((av * cwLo) + (bv * wLo) + round) >>> 6;
            Vector128<ushort> hi = ((av * cwHi) + (bv * wHi) + round) >>> 6;
            Vector128.Narrow(lo, hi).CopyTo(pal.Slice(channelBase, 16));
        }

        private static Vector256<ushort> WeightsVector(int precision) => precision switch
        {
            2 => Weights2Vector,
            3 => Weights3Vector,
            _ => Weights4Vector
        };

        /// <summary>AssignIndices: per-pixel nearest palette entry, with anchor-endpoint normalisation.</summary>
        private void AssignIndices(int shape, Span<byte> endPts, Span<byte> indices, Span<byte> indices2,
            Span<int> totErr)
        {
            int partitions = this.modeInfo.Partitions;
            int indexPrec = this.indexMode != 0 ? this.modeInfo.IndexPrec2 : this.modeInfo.IndexPrec;
            int indexPrec2 = this.indexMode != 0 ? this.modeInfo.IndexPrec : this.modeInfo.IndexPrec2;
            int numIndices = 1 << indexPrec;
            int numIndices2 = 1 << indexPrec2;

            int highestBit = numIndices >> 1;
            int highestBit2 = numIndices2 >> 1;

            for (int p = 0; p <= partitions; p++)
            {
                this.GeneratePaletteQuantized(endPts, p * 8, this.state.Palettes, p * 64);
                totErr[p] = 0;
            }

            for (int i = 0; i < 16; i++)
            {
                int region = this.state.Regions[i];
                totErr[region] += ComputeError(
                    this.state.Ldr, i, this.state.Palettes, region * 64, numIndices, numIndices2, out byte idx,
                    out byte idx2);
                indices[i] = idx;
                indices2[i] = idx2;
            }

            if (indexPrec2 == 0)
            {
                for (int p = 0; p <= partitions; p++)
                {
                    if ((indices[Bc7Tables.FixUp(shape, p)] & highestBit) != 0)
                    {
                        SwapEndPoints(endPts, p * 8);
                        for (int i = 0; i < 16; i++)
                        {
                            if (this.state.Regions[i] == p)
                            {
                                indices[i] = (byte)(numIndices - 1 - indices[i]);
                            }
                        }
                    }
                }
            }
            else
            {
                for (int p = 0; p <= partitions; p++)
                {
                    if ((indices[Bc7Tables.FixUp(shape, p)] & highestBit) != 0)
                    {
                        SwapRgbChannels(endPts, p * 8);
                        for (int i = 0; i < 16; i++)
                        {
                            if (this.state.Regions[i] == p)
                            {
                                indices[i] = (byte)(numIndices - 1 - indices[i]);
                            }
                        }
                    }

                    if ((indices2[0] & highestBit2) != 0)
                    {
                        SwapAlphaChannel(endPts, p * 8);
                        for (int i = 0; i < 16; i++)
                        {
                            indices2[i] = (byte)(numIndices2 - 1 - indices2[i]);
                        }
                    }
                }
            }
        }

        /// <summary>OptimizeEndPoints: per-region endpoint perturbation, starting from the assigned error.</summary>
        private void OptimizeEndPoints(int shape, ReadOnlySpan<int> orgErr, ReadOnlySpan<byte> orgEndPts,
            Span<byte> optEndPts)
        {
            int partitions = this.modeInfo.Partitions;

            for (int p = 0; p <= partitions; p++)
            {
                int np = 0;
                for (int i = 0; i < 16; i++)
                {
                    if (this.state.Regions[i] == p)
                    {
                        int src = i << 2;
                        int dst = np << 2;
                        this.state.RegionPixels[dst] = this.state.Ldr[src];
                        this.state.RegionPixels[dst + 1] = this.state.Ldr[src + 1];
                        this.state.RegionPixels[dst + 2] = this.state.Ldr[src + 2];
                        this.state.RegionPixels[dst + 3] = this.state.Ldr[src + 3];
                        np++;
                    }
                }

                this.OptimizeOne(np, orgErr[p], orgEndPts, p * 8, optEndPts, p * 8);
            }
        }

        /// <summary>OptimizeOne: per-channel endpoint perturbation and exhaustive touch-up.</summary>
        private void OptimizeOne(int np, int orgErr, ReadOnlySpan<byte> orig, int origOff, Span<byte> opt, int optOff)
        {
            int optErr = orgErr;
            for (int i = 0; i < 8; i++)
            {
                opt[optOff + i] = orig[origOff + i];
            }

            Span<byte> newA = this.state.NewA;
            Span<byte> newB = this.state.NewB;
            Span<byte> newTmp = this.state.NewTmp;

            for (int ch = 0; ch < 4; ch++)
            {
                if (this.PrecWithP(ch) == 0)
                {
                    continue;
                }

                int err0 = this.PerturbOne(np, ch, opt, optOff, newA, 0, optErr, false);
                int err1 = this.PerturbOne(np, ch, opt, optOff, newB, 0, optErr, true);

                // DirectXTex binds both cached channel references to `new_a` (`cnew_b = new_a.B[ch]`),
                // not to `new_b`, and the alternating loop keeps assigning from `new_a` rather than from
                // the pair it just produced in `newEndPts`. That is the reference behaviour (it drops the
                // perturb-B improvement and re-applies the first A candidate), reproduced verbatim.
                ref byte optA = ref opt[optOff + ch];
                ref byte optB = ref opt[optOff + 4 + ch];
                byte newAChannel = newA[ch];
                byte newABChannel = newA[4 + ch];

                int doB;
                if (err0 < err1)
                {
                    if (err0 >= optErr)
                    {
                        continue;
                    }

                    optA = newAChannel;
                    optErr = err0;
                    doB = 1;
                }
                else
                {
                    if (err1 >= optErr)
                    {
                        continue;
                    }

                    optB = newABChannel;
                    optErr = err1;
                    doB = 0;
                }

                while (true)
                {
                    int err = this.PerturbOne(np, ch, opt, optOff, newTmp, 0, optErr, doB != 0);
                    if (err >= optErr)
                    {
                        break;
                    }

                    if (doB == 0)
                    {
                        optA = newAChannel;
                    }
                    else
                    {
                        optB = newABChannel;
                    }

                    optErr = err;
                    doB = 1 - doB;
                }
            }

            for (int ch = 0; ch < 4; ch++)
            {
                this.Exhaustive(np, ch, ref optErr, opt, optOff);
            }
        }

        /// <summary>PerturbOne: logarithmic search for the best value of one endpoint channel.</summary>
        private int PerturbOne(
            int np,
            int ch,
            ReadOnlySpan<byte> oldEndPts,
            int oldOff,
            Span<byte> newEndPts,
            int newOff,
            int oldErr,
            bool doB)
        {
            int prec = this.PrecWithP(ch);
            int limit = 1 << prec;

            Span<byte> tmp = this.state.PerturbTmp;
            for (int i = 0; i < 8; i++)
            {
                newEndPts[newOff + i] = oldEndPts[oldOff + i];
                tmp[i] = oldEndPts[oldOff + i];
            }

            int minErr = oldErr;
            int side = doB ? 4 : 0;
            ref byte newChannel = ref newEndPts[newOff + side + ch];
            ref byte tmpChannel = ref tmp[side + ch];

            // Only the (side, ch) component changes inside this search, so the other channels'
            // contribution to the palette error is computed once.
            bool hoisted = this.CanHoistExhaustive();
            if (hoisted)
            {
                this.PrepareInvariant(np, ch, oldEndPts, oldOff);
            }

            for (int step = 1 << (prec - 1); step != 0; step >>= 1)
            {
                bool improved = false;
                int bestStep = 0;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int value = newChannel + (sign * step);
                    if (value < 0 || value >= limit)
                    {
                        continue;
                    }

                    tmpChannel = (byte)value;

                    int totalErr = hoisted
                        ? this.MapColorsVaryingChannel(np, ch, tmp, 0, minErr)
                        : this.MapColors(np, tmp, 0, minErr);
                    if (totalErr < minErr)
                    {
                        improved = true;
                        minErr = totalErr;
                        bestStep = sign * step;
                    }
                }

                if (improved)
                {
                    newChannel = (byte)(newChannel + bestStep);
                }
            }

            return minErr;
        }

        /// <summary>Exhaustive: exhaustive +-5 search around the perturbed endpoint channel.</summary>
        /// <remarks>
        ///     The inner loop changes exactly one endpoint channel, so the other three channels' palette
        ///     contributions are identical for every candidate in that loop. They are evaluated once per
        ///     outer iteration (<see cref="PrepareInvariant" />) and added to the varying channel's squared
        ///     difference; since all values are exact integers, the regrouped sum is the reference's sum.
        /// </remarks>
        private void Exhaustive(int np, int ch, ref int orgErr, Span<byte> opt, int optOff)
        {
            if (orgErr == 0)
            {
                return;
            }

            int prec = this.PrecWithP(ch);
            const int Delta = 5;

            Span<byte> tmp = this.state.NewTmp;
            for (int i = 0; i < 8; i++)
            {
                tmp[i] = opt[optOff + i];
            }

            int aLow = Math.Max(0, opt[optOff + ch] - Delta);
            int aHigh = Math.Min((1 << prec) - 1, opt[optOff + ch] + Delta);
            int bLow = Math.Max(0, opt[optOff + 4 + ch] - Delta);
            int bHigh = Math.Min((1 << prec) - 1, opt[optOff + 4 + ch] + Delta);
            int aMin = 0;
            int bMin = 0;

            bool hoisted = this.CanHoistExhaustive();
            if (hoisted)
            {
                // Channels other than `ch` never change inside this search, so their contribution is
                // computed once for the whole call.
                this.PrepareInvariant(np, ch, opt, optOff);
            }

            int bestErr = orgErr;
            if (opt[optOff + ch] <= opt[optOff + 4 + ch])
            {
                // Keep a <= b; `b` varies in the inner loop.
                for (int a = aLow; a <= aHigh; a++)
                {
                    tmp[ch] = (byte)a;

                    for (int b = Math.Max(a, bLow); b < bHigh; b++)
                    {
                        tmp[4 + ch] = (byte)b;

                        int err = hoisted
                            ? this.MapColorsVaryingChannel(np, ch, tmp, 0, bestErr)
                            : this.MapColors(np, tmp, 0, bestErr);
                        if (err < bestErr)
                        {
                            aMin = a;
                            bMin = b;
                            bestErr = err;
                        }
                    }
                }
            }
            else
            {
                // Keep b <= a; `a` varies in the inner loop.
                for (int b = bLow; b < bHigh; b++)
                {
                    tmp[4 + ch] = (byte)b;

                    for (int a = Math.Max(b, aLow); a <= aHigh; a++)
                    {
                        tmp[ch] = (byte)a;

                        int err = hoisted
                            ? this.MapColorsVaryingChannel(np, ch, tmp, 0, bestErr)
                            : this.MapColors(np, tmp, 0, bestErr);
                        if (err < bestErr)
                        {
                            aMin = a;
                            bMin = b;
                            bestErr = err;
                        }
                    }
                }
            }

            if (bestErr < orgErr)
            {
                opt[optOff + ch] = (byte)aMin;
                opt[optOff + 4 + ch] = (byte)bMin;
                orgErr = bestErr;
            }
        }

        /// <summary>True when the invariant-channel acceleration applies to the current mode.</summary>
        private bool CanHoistExhaustive()
            => Vector256.IsHardwareAccelerated
               && this.indexMode == 0
               && this.modeInfo.IndexPrec2 == 0
               && 1 << this.modeInfo.IndexPrec <= 8;

        /// <summary>
        ///     Precomputes, for every pixel of the region, the palette-error vector with channel
        ///     <paramref name="ch" /> omitted (the channel the caller is about to vary).
        /// </summary>
        private void PrepareInvariant(int np, int ch, ReadOnlySpan<byte> pair, int pairOff)
        {
            int indexPrec = this.modeInfo.IndexPrec;
            Vector256<int> w = indexPrec == 2 ? Weights2x8 : Weights3x8;
            Vector256<int> cw = indexPrec == 2 ? Complement2x8 : Complement3x8;

            Span<Vector256<int>> inv = this.state.Invariant;
            ReadOnlySpan<byte> colors = this.state.RegionPixels;
            inv.Clear();

            Span<int> channel = stackalloc int[4];
            for (int c = 0; c < 4; c++)
            {
                channel[c] = c == ch ? -1 : this.PrecWithP(c);
            }

            for (int c = 0; c < 4; c++)
            {
                int prec = channel[c];
                if (prec < 0)
                {
                    continue;
                }

                // A channel with no precision (alpha in the colour-only modes) decodes to 255.
                int a = prec != 0 ? UnquantizeChannel(pair[pairOff + c], prec) : 255;
                int b = prec != 0 ? UnquantizeChannel(pair[pairOff + 4 + c], prec) : 255;
                Vector256<int> p = Interpolate8(a, b, w, cw);

                for (int i = 0; i < np; i++)
                {
                    Vector256<int> d = Vector256.Create((int)colors[(i << 2) + c]) - p;
                    inv[i] += d * d;
                }
            }
        }

        /// <summary>
        ///     Error of the region for the current endpoint pair, reusing the invariant vectors from
        ///     <see cref="PrepareInvariant" />; only <paramref name="ch" /> is re-evaluated.
        /// </summary>
        private int MapColorsVaryingChannel(int np, int ch, ReadOnlySpan<byte> pair, int pairOff, int minErr)
        {
            int indexPrec = this.modeInfo.IndexPrec;
            Vector256<int> w = indexPrec == 2 ? Weights2x8 : Weights3x8;
            Vector256<int> cw = indexPrec == 2 ? Complement2x8 : Complement3x8;

            int prec = this.PrecWithP(ch);
            int a = prec != 0 ? UnquantizeChannel(pair[pairOff + ch], prec) : 255;
            int b = prec != 0 ? UnquantizeChannel(pair[pairOff + 4 + ch], prec) : 255;
            Vector256<int> palette = Interpolate8(a, b, w, cw);

            Vector256<int> invalid = indexPrec == 2 ? InvalidLanes4 : NoInvalidLanes;
            int count = 1 << indexPrec;
            ReadOnlySpan<Vector256<int>> inv = this.state.Invariant;
            Span<int> replay = this.state.Errors;
            ReadOnlySpan<byte> colors = this.state.RegionPixels;

            int total = 0;
            int i = 0;
            for (; i + 1 < np; i += 2)
            {
                Vector256<int> d0 = Vector256.Create((int)colors[(i << 2) + ch]) - palette;
                Vector256<int> d1 = Vector256.Create((int)colors[((i + 1) << 2) + ch]) - palette;
                Vector256<int> e0 = inv[i] + (d0 * d0);
                Vector256<int> e1 = inv[i + 1] + (d1 * d1);
                total += PaletteEntryError(replay, e0, invalid, count)
                         + PaletteEntryError(replay, e1, invalid, count);
                if (total > minErr)
                {
                    return int.MaxValue;
                }
            }

            if (i < np)
            {
                Vector256<int> d = Vector256.Create((int)colors[(i << 2) + ch]) - palette;
                total += PaletteEntryError(replay, inv[i] + (d * d), invalid, count);
                if (total > minErr)
                {
                    return int.MaxValue;
                }
            }

            return total;
        }

        /// <summary>MapColors: palette error of the region's pixels for a candidate endpoint pair.</summary>
        /// <remarks>
        ///     The reference walks the palette entries per pixel and stops at the first entry whose error
        ///     exceeds the best so far. That value is *not* always the palette minimum (per-channel rounding
        ///     makes the error sequence non-unimodal), so the value it would produce is computed explicitly:
        ///     the lanes are reduced branchlessly, and only when the sequence rose before reaching its minimum
        ///     (the sole case where the reference stops early) is the sequential scan replayed.
        /// </remarks>
        private int MapColors(int np, ReadOnlySpan<byte> endPts, int endPointOff, int minErr)
        {
            int indexPrec = this.indexMode != 0 ? this.modeInfo.IndexPrec2 : this.modeInfo.IndexPrec;
            int indexPrec2 = this.indexMode != 0 ? this.modeInfo.IndexPrec : this.modeInfo.IndexPrec2;

            int a0 = UnquantizeChannel(endPts[endPointOff], this.PrecWithP(0));
            int a1 = UnquantizeChannel(endPts[endPointOff + 1], this.PrecWithP(1));
            int a2 = UnquantizeChannel(endPts[endPointOff + 2], this.PrecWithP(2));
            int b0 = UnquantizeChannel(endPts[endPointOff + 4], this.PrecWithP(0));
            int b1 = UnquantizeChannel(endPts[endPointOff + 5], this.PrecWithP(1));
            int b2 = UnquantizeChannel(endPts[endPointOff + 6], this.PrecWithP(2));
            int alphaPrec = this.PrecWithP(3);
            int a3 = alphaPrec != 0 ? UnquantizeChannel(endPts[endPointOff + 3], alphaPrec) : 255;
            int b3 = alphaPrec != 0 ? UnquantizeChannel(endPts[endPointOff + 7], alphaPrec) : 255;

            Span<int> errors = this.state.Errors;
            ReadOnlySpan<byte> colors = this.state.RegionPixels;

            if (indexPrec == 4 && Vector.IsHardwareAccelerated)
            {
                // Mode 6: 16 entries, evaluated with width-scaled Vector<int> (1x16 on 512-bit, 2x8 on 256-bit, 4x4 on 128-bit).
                return MapColors16(errors, colors, np, a0, a1, a2, a3, b0, b1, b2, b3, minErr);
            }

            if (Vector256.IsHardwareAccelerated)
            {
                Vector256<int> w = indexPrec == 2 ? Weights2x8 : Weights3x8;
                Vector256<int> cw = indexPrec == 2 ? Complement2x8 : Complement3x8;
                Vector256<int> pr = Interpolate8(a0, b0, w, cw);
                Vector256<int> pg = Interpolate8(a1, b1, w, cw);
                Vector256<int> pb = Interpolate8(a2, b2, w, cw);

                if (indexPrec2 == 0)
                {
                    Vector256<int> pa = Interpolate8(a3, b3, w, cw);
                    Vector256<int> invalid = indexPrec == 2 ? InvalidLanes4 : NoInvalidLanes;
                    return MapColorsUnified(errors, colors, np, pr, pg, pb, pa, invalid, 1 << indexPrec, minErr);
                }

                // Split colour/alpha indices (modes 4 and 5): RGB and alpha are minimised independently.
                Vector256<int> wa = indexPrec2 == 2 ? Weights2x8 : Weights3x8;
                Vector256<int> cwa = indexPrec2 == 2 ? Complement2x8 : Complement3x8;
                Vector256<int> pav = Interpolate8(a3, b3, wa, cwa);
                Vector256<int> invalidRgb = indexPrec == 2 ? InvalidLanes4 : NoInvalidLanes;
                Vector256<int> invalidAlpha = indexPrec2 == 2 ? InvalidLanes4 : NoInvalidLanes;
                return MapColorsSplit(errors, colors, np, pr, pg, pb, pav, invalidRgb, invalidAlpha, 1 << indexPrec,
                    1 << indexPrec2, minErr);
            }

            if (Vector.IsHardwareAccelerated && Vector<int>.Count == 4 && indexPrec == 2 && indexPrec2 == 0)
            {
                Vector<int> w = new(Bc7Constants.Weights2);
                Vector<int> cw = new(Complement2);
                Vector<int> pr = InterpolateVector(a0, b0, w, cw);
                Vector<int> pg = InterpolateVector(a1, b1, w, cw);
                Vector<int> pb = InterpolateVector(a2, b2, w, cw);
                Vector<int> pa = InterpolateVector(a3, b3, w, cw);
                return MapColorsUnifiedVector4(errors, colors, np, pr, pg, pb, pa, minErr);
            }

            return this.MapColorsScalar(colors, np, a0, a1, a2, a3, b0, b1, b2, b3, indexPrec, indexPrec2, minErr);
        }

        private int MapColorsScalar(
            ReadOnlySpan<byte> colors,
            int np,
            int a0,
            int a1,
            int a2,
            int a3,
            int b0,
            int b1,
            int b2,
            int b3,
            int indexPrec,
            int indexPrec2,
            int minErr)
        {
            Span<byte> pal = this.state.MapPalette;
            InterpolateInto(pal, 0, a0, a1, a2, a3, b0, b1, b2, b3, indexPrec, indexPrec2);
            int numIndices = 1 << indexPrec;
            int numIndices2 = 1 << indexPrec2;

            int total = 0;
            for (int i = 0; i < np; i++)
            {
                total += ComputeError(colors, i, pal, 0, numIndices, numIndices2, out _, out _);
                if (total > minErr)
                {
                    return int.MaxValue;
                }
            }

            return total;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<int> Interpolate8(int a, int b, Vector256<int> w, Vector256<int> complement)
            => ((Vector256.Create(a) * complement) + (Vector256.Create(b) * w) + Vector256.Create(32)) >>> 6;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector<int> InterpolateVector(int a, int b, Vector<int> w, Vector<int> complement)
            => ((new Vector<int>(a) * complement) + (new Vector<int>(b) * w) + new Vector<int>(32)) >>> 6;

        private static int MapColorsUnifiedVector4(
            Span<int> errors,
            ReadOnlySpan<byte> colors,
            int np,
            Vector<int> pr,
            Vector<int> pg,
            Vector<int> pb,
            Vector<int> pa,
            int minErr)
        {
            int total = 0;
            for (int i = 0; i < np; i++)
            {
                int o = i << 2;
                Vector<int> d = new Vector<int>(colors[o]) - pr;
                Vector<int> e = d * d;
                d = new Vector<int>(colors[o + 1]) - pg;
                e += d * d;
                d = new Vector<int>(colors[o + 2]) - pb;
                e += d * d;
                d = new Vector<int>(colors[o + 3]) - pa;
                e += d * d;

                e.CopyTo(errors);
                int best = int.MaxValue;
                for (int j = 0; j < 4 && best > 0; j++)
                {
                    int err = errors[j];
                    if (err > best)
                    {
                        break;
                    }

                    if (err < best)
                    {
                        best = err;
                    }
                }

                total += best;
                if (total > minErr)
                {
                    return int.MaxValue;
                }
            }

            return total;
        }

        private static int MapColorsUnified(
            Span<int> errors,
            ReadOnlySpan<byte> colors,
            int np,
            Vector256<int> pr,
            Vector256<int> pg,
            Vector256<int> pb,
            Vector256<int> pa,
            Vector256<int> invalid,
            int count,
            int minErr)
        {
            // Two pixels per iteration: the per-pixel reduction chain is latency bound, and independent
            // chains overlap in the out-of-order window. Bailing out one pixel late returns the same
            // sentinel the reference returns, so the grouping does not change any result.
            int total = 0;
            int i = 0;
            for (; i + 1 < np; i += 2)
            {
                Vector256<int> e0 = PaletteErrors(colors, i << 2, pr, pg, pb, pa);
                Vector256<int> e1 = PaletteErrors(colors, (i << 2) + 4, pr, pg, pb, pa);
                total += PaletteEntryError(errors, e0, invalid, count)
                         + PaletteEntryError(errors, e1, invalid, count);
                if (total > minErr)
                {
                    return int.MaxValue;
                }
            }

            if (i < np)
            {
                total += PaletteEntryError(errors, PaletteErrors(colors, i << 2, pr, pg, pb, pa), invalid, count);
                if (total > minErr)
                {
                    return int.MaxValue;
                }
            }

            return total;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<int> PaletteErrors(
            ReadOnlySpan<byte> colors, int o, Vector256<int> pr, Vector256<int> pg, Vector256<int> pb,
            Vector256<int> pa)
        {
            Vector256<int> d = Vector256.Create((int)colors[o]) - pr;
            Vector256<int> e = d * d;
            d = Vector256.Create((int)colors[o + 1]) - pg;
            e += d * d;
            d = Vector256.Create((int)colors[o + 2]) - pb;
            e += d * d;
            d = Vector256.Create((int)colors[o + 3]) - pa;
            e += d * d;
            return e;
        }

        private static int MapColorsSplit(
            Span<int> errors,
            ReadOnlySpan<byte> colors,
            int np,
            Vector256<int> pr,
            Vector256<int> pg,
            Vector256<int> pb,
            Vector256<int> pa,
            Vector256<int> invalidRgb,
            Vector256<int> invalidAlpha,
            int count,
            int count2,
            int minErr)
        {
            int total = 0;
            for (int i = 0; i < np; i++)
            {
                int o = i << 2;
                Vector256<int> d = Vector256.Create((int)colors[o]) - pr;
                Vector256<int> e = d * d;
                d = Vector256.Create((int)colors[o + 1]) - pg;
                e += d * d;
                d = Vector256.Create((int)colors[o + 2]) - pb;
                e += d * d;
                int rgb = PaletteEntryError(errors, e, invalidRgb, count);

                d = Vector256.Create((int)colors[o + 3]) - pa;
                Vector256<int> ea = d * d;
                int alpha = PaletteEntryError(errors, ea, invalidAlpha, count2);

                total += rgb + alpha;
                if (total > minErr)
                {
                    return int.MaxValue;
                }
            }

            return total;
        }

        /// <summary>
        ///     Reproduces the reference's per-pixel palette-entry scan: the minimum, unless the error sequence
        ///     rose above the running minimum before reaching it (then the scan stops early).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PaletteEntryError(Span<int> errors, Vector256<int> e, Vector256<int> invalid, int count)
        {
            e |= invalid;

            int min = HorizontalMin(e);
            int mask = (int)Vector256.Equals(e, Vector256.Create(min)).ExtractMostSignificantBits();
            int minIndex = BitOperations.TrailingZeroCount(mask);
            if (minIndex == 0)
            {
                return min;
            }

            int rises = (int)Vector256.GreaterThan(e, Vector256.Shuffle(e, ShiftByOneLane))
                .ExtractMostSignificantBits();
            if ((rises & ((1 << minIndex) - 1)) == 0)
            {
                return min;
            }

            // The reference stopped before the minimum: replay its scan exactly.
            e.CopyTo(errors);
            int best = int.MaxValue;
            for (int j = 0; j < count && best > 0; j++)
            {
                int err = errors[j];
                if (err > best)
                {
                    break;
                }

                if (err < best)
                {
                    best = err;
                }
            }

            return best;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int HorizontalMin(Vector256<int> v)
        {
            Vector256<int> t = Vector256.Min(v, Vector256.Shuffle(v, Vector256.Create(4, 5, 6, 7, 0, 1, 2, 3)));
            t = Vector256.Min(t, Vector256.Shuffle(t, Vector256.Create(2, 3, 0, 1, 4, 5, 6, 7)));
            t = Vector256.Min(t, Vector256.Shuffle(t, Vector256.Create(1, 0, 3, 2, 5, 4, 7, 6)));
            return t.ToScalar();
        }

        /// <summary>Mode 6 (16 palette entries): exact prefix scan over the spilled error vector.</summary>
        private static int MapColors16(
            Span<int> errors, ReadOnlySpan<byte> colors, int np, int a0, int a1, int a2, int a3, int b0, int b1, int b2,
            int b3, int minErr)
        {
            ReadOnlySpan<int> weights = Bc7Constants.Weights4;
            ReadOnlySpan<int> complement = Complement4;

            if (Vector<int>.Count == 16)
            {
                Vector<int> w = new(weights);
                Vector<int> cw = new(complement);
                Vector<int> pr = InterpolateVector(a0, b0, w, cw);
                Vector<int> pg = InterpolateVector(a1, b1, w, cw);
                Vector<int> pb = InterpolateVector(a2, b2, w, cw);
                Vector<int> pa = InterpolateVector(a3, b3, w, cw);

                int total = 0;
                for (int i = 0; i < np; i++)
                {
                    int o = i << 2;
                    Vector<int> d = new Vector<int>(colors[o]) - pr;
                    Vector<int> e = d * d;
                    d = new Vector<int>(colors[o + 1]) - pg;
                    e += d * d;
                    d = new Vector<int>(colors[o + 2]) - pb;
                    e += d * d;
                    d = new Vector<int>(colors[o + 3]) - pa;
                    e += d * d;

                    e.CopyTo(errors);
                    total += ScanErrors16(errors);
                    if (total > minErr)
                    {
                        return int.MaxValue;
                    }
                }

                return total;
            }

            if (Vector<int>.Count == 8)
            {
                Vector<int> w0 = new(weights);
                Vector<int> cw0 = new(complement);
                Vector<int> w1 = new(weights.Slice(8));
                Vector<int> cw1 = new(complement.Slice(8));

                Vector<int> pr0 = InterpolateVector(a0, b0, w0, cw0);
                Vector<int> pr1 = InterpolateVector(a0, b0, w1, cw1);
                Vector<int> pg0 = InterpolateVector(a1, b1, w0, cw0);
                Vector<int> pg1 = InterpolateVector(a1, b1, w1, cw1);
                Vector<int> pb0 = InterpolateVector(a2, b2, w0, cw0);
                Vector<int> pb1 = InterpolateVector(a2, b2, w1, cw1);
                Vector<int> pa0 = InterpolateVector(a3, b3, w0, cw0);
                Vector<int> pa1 = InterpolateVector(a3, b3, w1, cw1);

                int total = 0;
                for (int i = 0; i < np; i++)
                {
                    int o = i << 2;
                    int vr = colors[o];
                    int vg = colors[o + 1];
                    int vb = colors[o + 2];
                    int va = colors[o + 3];

                    Vector<int> d = new Vector<int>(vr) - pr0;
                    Vector<int> e0 = d * d;
                    d = new Vector<int>(vr) - pr1;
                    Vector<int> e1 = d * d;
                    d = new Vector<int>(vg) - pg0;
                    e0 += d * d;
                    d = new Vector<int>(vg) - pg1;
                    e1 += d * d;
                    d = new Vector<int>(vb) - pb0;
                    e0 += d * d;
                    d = new Vector<int>(vb) - pb1;
                    e1 += d * d;
                    d = new Vector<int>(va) - pa0;
                    e0 += d * d;
                    d = new Vector<int>(va) - pa1;
                    e1 += d * d;

                    e0.CopyTo(errors);
                    e1.CopyTo(errors.Slice(8, 8));
                    total += ScanErrors16(errors);
                    if (total > minErr)
                    {
                        return int.MaxValue;
                    }
                }

                return total;
            }

            {
                Vector<int> w0 = new(weights);
                Vector<int> cw0 = new(complement);
                Vector<int> w1 = new(weights.Slice(4));
                Vector<int> cw1 = new(complement.Slice(4));
                Vector<int> w2 = new(weights.Slice(8));
                Vector<int> cw2 = new(complement.Slice(8));
                Vector<int> w3 = new(weights.Slice(12));
                Vector<int> cw3 = new(complement.Slice(12));

                Vector<int> pr0 = InterpolateVector(a0, b0, w0, cw0);
                Vector<int> pr1 = InterpolateVector(a0, b0, w1, cw1);
                Vector<int> pr2 = InterpolateVector(a0, b0, w2, cw2);
                Vector<int> pr3 = InterpolateVector(a0, b0, w3, cw3);
                Vector<int> pg0 = InterpolateVector(a1, b1, w0, cw0);
                Vector<int> pg1 = InterpolateVector(a1, b1, w1, cw1);
                Vector<int> pg2 = InterpolateVector(a1, b1, w2, cw2);
                Vector<int> pg3 = InterpolateVector(a1, b1, w3, cw3);
                Vector<int> pb0 = InterpolateVector(a2, b2, w0, cw0);
                Vector<int> pb1 = InterpolateVector(a2, b2, w1, cw1);
                Vector<int> pb2 = InterpolateVector(a2, b2, w2, cw2);
                Vector<int> pb3 = InterpolateVector(a2, b2, w3, cw3);
                Vector<int> pa0 = InterpolateVector(a3, b3, w0, cw0);
                Vector<int> pa1 = InterpolateVector(a3, b3, w1, cw1);
                Vector<int> pa2 = InterpolateVector(a3, b3, w2, cw2);
                Vector<int> pa3 = InterpolateVector(a3, b3, w3, cw3);

                int total = 0;
                for (int i = 0; i < np; i++)
                {
                    int o = i << 2;
                    Vector<int> vr = new(colors[o]);
                    Vector<int> vg = new(colors[o + 1]);
                    Vector<int> vb = new(colors[o + 2]);
                    Vector<int> va = new(colors[o + 3]);

                    Vector<int> d0 = vr - pr0;
                    Vector<int> e0 = d0 * d0;
                    d0 = vg - pg0;
                    e0 += d0 * d0;
                    d0 = vb - pb0;
                    e0 += d0 * d0;
                    d0 = va - pa0;
                    e0 += d0 * d0;
                    e0.CopyTo(errors);

                    Vector<int> d1 = vr - pr1;
                    Vector<int> e1 = d1 * d1;
                    d1 = vg - pg1;
                    e1 += d1 * d1;
                    d1 = vb - pb1;
                    e1 += d1 * d1;
                    d1 = va - pa1;
                    e1 += d1 * d1;
                    e1.CopyTo(errors.Slice(4, 4));

                    Vector<int> d2 = vr - pr2;
                    Vector<int> e2 = d2 * d2;
                    d2 = vg - pg2;
                    e2 += d2 * d2;
                    d2 = vb - pb2;
                    e2 += d2 * d2;
                    d2 = va - pa2;
                    e2 += d2 * d2;
                    e2.CopyTo(errors.Slice(8, 4));

                    Vector<int> d3 = vr - pr3;
                    Vector<int> e3 = d3 * d3;
                    d3 = vg - pg3;
                    e3 += d3 * d3;
                    d3 = vb - pb3;
                    e3 += d3 * d3;
                    d3 = va - pa3;
                    e3 += d3 * d3;
                    e3.CopyTo(errors.Slice(12, 4));

                    total += ScanErrors16(errors);
                    if (total > minErr)
                    {
                        return int.MaxValue;
                    }
                }

                return total;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ScanErrors16(ReadOnlySpan<int> errors)
        {
            int best = int.MaxValue;
            for (int j = 0; j < 16 && best > 0; j++)
            {
                int err = errors[j];
                if (err > best)
                {
                    break;
                }

                if (err < best)
                {
                    best = err;
                }
            }

            return best;
        }

        /// <summary>ComputeError: nearest palette entry for one pixel (RGB and, when separate, alpha).</summary>
        private static int ComputeError(
            ReadOnlySpan<byte> colors,
            int pixel,
            ReadOnlySpan<byte> pal,
            int palBase,
            int numIndices,
            int numIndices2,
            out byte bestIndex,
            out byte bestIndex2)
        {
            int o = pixel << 2;
            int vr = colors[o];
            int vg = colors[o + 1];
            int vb = colors[o + 2];
            int va = colors[o + 3];

            bestIndex = 0;
            bestIndex2 = 0;

            int bestErr = int.MaxValue;
            int rBase = palBase;
            int gBase = palBase + 16;
            int bBase = palBase + 32;
            int aBase = palBase + 48;

            if (numIndices2 == 1)
            {
                for (int i = 0; i < numIndices && bestErr > 0; i++)
                {
                    int dr = vr - pal[rBase + i];
                    int dg = vg - pal[gBase + i];
                    int db = vb - pal[bBase + i];
                    int da = va - pal[aBase + i];
                    int err = (dr * dr) + (dg * dg) + (db * db) + (da * da);
                    if (err > bestErr)
                    {
                        break;
                    }

                    if (err < bestErr)
                    {
                        bestErr = err;
                        bestIndex = (byte)i;
                    }
                }

                return bestErr;
            }

            for (int i = 0; i < numIndices && bestErr > 0; i++)
            {
                int dr = vr - pal[rBase + i];
                int dg = vg - pal[gBase + i];
                int db = vb - pal[bBase + i];
                int err = (dr * dr) + (dg * dg) + (db * db);
                if (err > bestErr)
                {
                    break;
                }

                if (err < bestErr)
                {
                    bestErr = err;
                    bestIndex = (byte)i;
                }
            }

            int total = bestErr;
            bestErr = int.MaxValue;

            for (int i = 0; i < numIndices2 && bestErr > 0; i++)
            {
                int ea = va - pal[aBase + i];
                int err = ea * ea;
                if (err > bestErr)
                {
                    break;
                }

                if (err < bestErr)
                {
                    bestErr = err;
                    bestIndex2 = (byte)i;
                }
            }

            return total + bestErr;
        }

        /// <summary>EmitBlock: writes the candidate block bits.</summary>
        private void EmitBlock(int shape, int rotation, int indexMode, ReadOnlySpan<byte> endPts,
            ReadOnlySpan<byte> indices, ReadOnlySpan<byte> indices2)
        {
            int partitions = this.modeInfo.Partitions;
            int pBits = this.modeInfo.PBits;
            int indexPrec = this.modeInfo.IndexPrec;
            int indexPrec2 = this.modeInfo.IndexPrec2;

            Span<byte> blk = this.state.CurrentBlock;
            blk.Clear();

            int bit = 0;
            SetBits(blk, ref bit, this.mode, 0);
            SetBits(blk, ref bit, 1, 1);
            SetBits(blk, ref bit, this.modeInfo.RotationBits, rotation);
            SetBits(blk, ref bit, this.modeInfo.IndexModeBits, indexMode);
            SetBits(blk, ref bit, this.modeInfo.PartitionBits, shape);

            if (pBits != 0)
            {
                int numEndPoints = (partitions + 1) << 1;
                Span<byte> votes = this.state.PVote;
                Span<byte> counts = this.state.PCount;
                votes.Clear();
                counts.Clear();

                for (int ch = 0; ch < 4; ch++)
                {
                    int prec = this.Prec(ch);
                    int precWithP = this.PrecWithP(ch);
                    int ep = 0;
                    for (int i = 0; i <= partitions; i++)
                    {
                        int e = i * 8;
                        if (prec == precWithP)
                        {
                            SetBits(blk, ref bit, prec, endPts[e + ch]);
                            SetBits(blk, ref bit, prec, endPts[e + 4 + ch]);
                        }
                        else
                        {
                            SetBits(blk, ref bit, prec, endPts[e + ch] >> 1);
                            SetBits(blk, ref bit, prec, endPts[e + 4 + ch] >> 1);

                            int idx = ep++ * pBits / numEndPoints;
                            votes[idx] += (byte)(endPts[e + ch] & 0x01);
                            counts[idx]++;
                            idx = ep++ * pBits / numEndPoints;
                            votes[idx] += (byte)(endPts[e + 4 + ch] & 0x01);
                            counts[idx]++;
                        }
                    }
                }

                for (int i = 0; i < pBits; i++)
                {
                    SetBits(blk, ref bit, 1, votes[i] > counts[i] >> 1 ? 1 : 0);
                }
            }
            else
            {
                for (int ch = 0; ch < 4; ch++)
                {
                    int prec = this.Prec(ch);
                    for (int i = 0; i <= partitions; i++)
                    {
                        int e = i * 8;
                        SetBits(blk, ref bit, prec, endPts[e + ch]);
                        SetBits(blk, ref bit, prec, endPts[e + 4 + ch]);
                    }
                }
            }

            ReadOnlySpan<byte> i1 = indexMode != 0 ? indices2 : indices;
            ReadOnlySpan<byte> i2 = indexMode != 0 ? indices : indices2;

            for (int i = 0; i < 16; i++)
            {
                if (Bc7Tables.IsFixUpOffset(partitions, shape, i))
                {
                    SetBits(blk, ref bit, indexPrec - 1, i1[i]);
                }
                else
                {
                    SetBits(blk, ref bit, indexPrec, i1[i]);
                }
            }

            if (indexPrec2 != 0)
            {
                for (int i = 0; i < 16; i++)
                {
                    SetBits(blk, ref bit, i != 0 ? indexPrec2 : indexPrec2 - 1, i2[i]);
                }
            }
        }

        /// <summary>FixEndpointPBits: derives the p-bits by majority vote and applies them to the endpoints.</summary>
        private void FixEndPointPBits(ReadOnlySpan<byte> orig, Span<byte> fixedEndPts)
        {
            int partitions = this.modeInfo.Partitions;

            for (int i = 0; i < 24; i++)
            {
                fixedEndPts[i] = orig[i];
            }

            int pBits = this.modeInfo.PBits;
            if (pBits == 0)
            {
                return;
            }

            int numEndPoints = (partitions + 1) << 1;
            Span<byte> votes = this.state.PVote;
            Span<byte> counts = this.state.PCount;
            votes.Clear();
            counts.Clear();

            for (int ch = 0; ch < 4; ch++)
            {
                int prec = this.Prec(ch);
                int precWithP = this.PrecWithP(ch);
                int ep = 0;
                for (int i = 0; i <= partitions; i++)
                {
                    int e = i * 8;
                    if (prec == precWithP)
                    {
                        fixedEndPts[e + ch] = orig[e + ch];
                        fixedEndPts[e + 4 + ch] = orig[e + 4 + ch];
                    }
                    else
                    {
                        fixedEndPts[e + ch] = (byte)(orig[e + ch] >> 1);
                        fixedEndPts[e + 4 + ch] = (byte)(orig[e + 4 + ch] >> 1);

                        int idx = ep++ * pBits / numEndPoints;
                        votes[idx] += (byte)(orig[e + ch] & 0x01);
                        counts[idx]++;
                        idx = ep++ * pBits / numEndPoints;
                        votes[idx] += (byte)(orig[e + 4 + ch] & 0x01);
                        counts[idx]++;
                    }
                }
            }

            Span<int> bits = this.state.PBits;
            for (int i = 0; i < pBits; i++)
            {
                bits[i] = votes[i] > counts[i] >> 1 ? 1 : 0;
            }

            // Endpoints are widened to the p-bit precision so error calculations see the real values.
            if (this.mode == 1)
            {
                // Shared p-bits: one bit per region, used by both endpoints.
                for (int ch = 0; ch < 4; ch++)
                {
                    for (int i = 0; i <= partitions; i++)
                    {
                        int e = i * 8;
                        fixedEndPts[e + ch] = (byte)((fixedEndPts[e + ch] << 1) | bits[i]);
                        fixedEndPts[e + 4 + ch] = (byte)((fixedEndPts[e + 4 + ch] << 1) | bits[i]);
                    }
                }
            }
            else
            {
                // Unique p-bits: one bit per endpoint.
                for (int ch = 0; ch < 4; ch++)
                {
                    for (int i = 0; i <= partitions; i++)
                    {
                        int e = i * 8;
                        fixedEndPts[e + ch] = (byte)((fixedEndPts[e + ch] << 1) | bits[(i * 2) + 0]);
                        fixedEndPts[e + 4 + ch] = (byte)((fixedEndPts[e + 4 + ch] << 1) | bits[(i * 2) + 1]);
                    }
                }
            }
        }

        /// <summary>Quantize(c, RGBAPrecWithP).</summary>
        private void QuantizeEndPoint(ReadOnlySpan<byte> src, int srcOff, Span<byte> dst, int dstOff)
        {
            dst[dstOff] = QuantizeChannel(src[srcOff], this.PrecWithP(0));
            dst[dstOff + 1] = QuantizeChannel(src[srcOff + 1], this.PrecWithP(1));
            dst[dstOff + 2] = QuantizeChannel(src[srcOff + 2], this.PrecWithP(2));
            int alphaPrec = this.PrecWithP(3);
            dst[dstOff + 3] = alphaPrec != 0 ? QuantizeChannel(src[srcOff + 3], alphaPrec) : (byte)255;
        }

        /// <summary>
        ///     DirectXTex <c>D3DX_BC7::Quantize(comp, prec)</c>, including its <c>uint8_t</c> truncation
        ///     before the 255 clamp (reproduced for parity: it wraps for components within
        ///     <c>2^(7-prec)</c> of 255).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte QuantizeChannel(byte comp, int prec)
        {
            // prec == 8: the reference computes `1u << -1` (UB); the compiled output is the identity.
            int rnd = prec >= 8 ? comp : unchecked((byte)(comp + (1 << (7 - prec))));
            return (byte)(rnd >> (8 - prec));
        }

        /// <summary>DirectXTex <c>D3DX_BC7::Unquantize(comp, prec)</c>.</summary>
        private static byte UnquantizeChannel(byte comp, int prec)
        {
            int v = comp << (8 - prec);
            return (byte)(v | (v >> prec));
        }

        private void CopyPixel(Span<byte> dst, int dstOff, int pixel)
        {
            int o = pixel << 2;
            dst[dstOff] = this.state.Ldr[o];
            dst[dstOff + 1] = this.state.Ldr[o + 1];
            dst[dstOff + 2] = this.state.Ldr[o + 2];
            dst[dstOff + 3] = this.state.Ldr[o + 3];
        }

        private static void StoreEndPoint(Span<byte> dst, int off, in HdrColor c)
        {
            // DirectXTex HDRColorA::ToLDRColorA: (uint8_t)(v * 255 + 0.01f), truncating.
            dst[off] = (byte)((c.R * 255.0f) + 0.01f);
            dst[off + 1] = (byte)((c.G * 255.0f) + 0.01f);
            dst[off + 2] = (byte)((c.B * 255.0f) + 0.01f);
            dst[off + 3] = (byte)((c.A * 255.0f) + 0.01f);
        }

        private static void SwapEndPoints(Span<byte> endPts, int off)
        {
            for (int ch = 0; ch < 4; ch++)
            {
                (endPts[off + ch], endPts[off + 4 + ch]) = (endPts[off + 4 + ch], endPts[off + ch]);
            }
        }

        private static void SwapRgbChannels(Span<byte> endPts, int off)
        {
            for (int ch = 0; ch < 3; ch++)
            {
                (endPts[off + ch], endPts[off + 4 + ch]) = (endPts[off + 4 + ch], endPts[off + ch]);
            }
        }

        private static void SwapAlphaChannel(Span<byte> endPts, int off) =>
            (endPts[off + 3], endPts[off + 7]) = (endPts[off + 7], endPts[off + 3]);

        private static void SetBits(Span<byte> buf, ref int bitPos, int numBits, int value)
        {
            if (numBits == 0)
            {
                return;
            }

            int index = bitPos >> 3;
            int baseBit = bitPos - (index << 3);
            if (baseBit + numBits > 8)
            {
                int firstBits = 8 - baseBit;
                int nextBits = numBits - firstBits;
                buf[index] = (byte)((buf[index] & ~(((1 << firstBits) - 1) << baseBit)) | ((value << baseBit) & 0xFF));
                buf[index + 1] = (byte)((buf[index + 1] & ~((1 << nextBits) - 1)) | (value >> firstBits));
            }
            else
            {
                buf[index] = (byte)((buf[index] & ~(((1 << numBits) - 1) << baseBit)) | ((value << baseBit) & 0xFF));
            }

            bitPos += numBits;
        }
    }

    private static class Bc7Tables
    {
        /// <summary>Mode 0..7 layout, exactly as DirectXTex <c>D3DX_BC7::ms_aInfo</c>.</summary>
        /// <remarks>
        ///     Modes 0 and 2 (3 subsets) are present for indexing fidelity but are never searched: DirectXTex
        ///     only enables them with <c>-bc x</c> (BC_FLAGS_USE_3SUBSETS), which is out of scope.
        ///     The constructor arguments are: partitions, partitionBits, pBits, rotationBits,
        ///     indexModeBits, indexPrec, indexPrec2, endpoint precision and endpoint precision including
        ///     the p-bit.
        /// </remarks>
        public static readonly Bc7ModeInfo[] Modes =
        [
            new(2, 4, 6, 0, 0, 3, 0, 0x00040404u, 0x00050505u), // 0: RGBP 4441, 3-bit indices, 16 partitions
            new(1, 6, 2, 0, 0, 3, 0, 0x00060606u, 0x00070707u), // 1: RGBP 6661 (shared p-bit), 3-bit indices
            new(2, 6, 0, 0, 0, 2, 0, 0x00050505u, 0x00050505u), // 2: RGB 555, 2-bit indices
            new(1, 6, 4, 0, 0, 2, 0, 0x00070707u, 0x00080808u), // 3: RGBP 7771 (unique p-bit), 2-bit indices
            new(0, 0, 0, 2, 1, 2, 3, 0x06050505u, 0x06050505u), // 4: RGB 555 + A6, split indices, rotation
            new(0, 0, 0, 2, 0, 2, 2, 0x08070707u, 0x08070707u), // 5: RGB 777 + A8, split indices, rotation
            new(0, 0, 2, 0, 0, 4, 0, 0x07070707u, 0x08080808u), // 6: RGBAP 77771 (unique p-bit), 4-bit indices
            new(1, 6, 4, 0, 0, 2, 0, 0x05050505u, 0x06060606u) // 7: RGBAP 55551 (unique p-bit), 2-bit indices
        ];

        /// <summary>Gets the 2-subset partition shapes (64 shapes x 16 pixel regions), from DirectXTex.</summary>
        public static ReadOnlySpan<byte> Partition2Subsets =>
        [
            0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, // shape 0
            0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, // shape 1
            0, 1, 1, 1, 0, 1, 1, 1, 0, 1, 1, 1, 0, 1, 1, 1, // shape 2
            0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 1, 1, 1, // shape 3
            0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1, 1, // shape 4
            0, 0, 1, 1, 0, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, // shape 5
            0, 0, 0, 1, 0, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, // shape 6
            0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 1, 1, 1, // shape 7
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, // shape 8
            0, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // shape 9
            0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1, 1, 1, 1, 1, 1, // shape 10
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1, 1, // shape 11
            0, 0, 0, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // shape 12
            0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, // shape 13
            0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // shape 14
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, // shape 15
            0, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1, 0, 1, 1, 1, 1, // shape 16
            0, 1, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, // shape 17
            0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1, 0, // shape 18
            0, 1, 1, 1, 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0, // shape 19
            0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, // shape 20
            0, 0, 0, 0, 1, 0, 0, 0, 1, 1, 0, 0, 1, 1, 1, 0, // shape 21
            0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 1, 0, 0, // shape 22
            0, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 1, // shape 23
            0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, // shape 24
            0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 1, 0, 0, // shape 25
            0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, // shape 26
            0, 0, 1, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 1, 0, 0, // shape 27
            0, 0, 0, 1, 0, 1, 1, 1, 1, 1, 1, 0, 1, 0, 0, 0, // shape 28
            0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, // shape 29
            0, 1, 1, 1, 0, 0, 0, 1, 1, 0, 0, 0, 1, 1, 1, 0, // shape 30
            0, 0, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 1, 0, 0, // shape 31
            0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, // shape 32
            0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, // shape 33
            0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, // shape 34
            0, 0, 1, 1, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 0, 0, // shape 35
            0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, // shape 36
            0, 1, 0, 1, 0, 1, 0, 1, 1, 0, 1, 0, 1, 0, 1, 0, // shape 37
            0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, // shape 38
            0, 1, 0, 1, 1, 0, 1, 0, 1, 0, 1, 0, 0, 1, 0, 1, // shape 39
            0, 1, 1, 1, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 1, 0, // shape 40
            0, 0, 0, 1, 0, 0, 1, 1, 1, 1, 0, 0, 1, 0, 0, 0, // shape 41
            0, 0, 1, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 1, 0, 0, // shape 42
            0, 0, 1, 1, 1, 0, 1, 1, 1, 1, 0, 1, 1, 1, 0, 0, // shape 43
            0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0, // shape 44
            0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 0, 0, 0, 0, 1, 1, // shape 45
            0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, // shape 46
            0, 0, 0, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 0, 0, // shape 47
            0, 1, 0, 0, 1, 1, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, // shape 48
            0, 0, 1, 0, 0, 1, 1, 1, 0, 0, 1, 0, 0, 0, 0, 0, // shape 49
            0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 0, 0, 1, 0, // shape 50
            0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 0, 0, 1, 0, 0, // shape 51
            0, 1, 1, 0, 1, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 1, // shape 52
            0, 0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 0, 1, 0, 0, 1, // shape 53
            0, 1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 1, 1, 1, 0, 0, // shape 54
            0, 0, 1, 1, 1, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1, 0, // shape 55
            0, 1, 1, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 0, 0, 1, // shape 56
            0, 1, 1, 0, 0, 0, 1, 1, 0, 0, 1, 1, 1, 0, 0, 1, // shape 57
            0, 1, 1, 1, 1, 1, 1, 0, 1, 0, 0, 0, 0, 0, 0, 1, // shape 58
            0, 0, 0, 1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 1, 1, 1, // shape 59
            0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, // shape 60
            0, 0, 1, 1, 0, 0, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, // shape 61
            0, 0, 1, 0, 0, 0, 1, 0, 1, 1, 1, 0, 1, 1, 1, 0, // shape 62
            0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1, 0, 1, 1, 1 // shape 63
        ];

        /// <summary>Gets the fix-up index per region for 2-subset shapes (two entries per shape are used).</summary>
        public static ReadOnlySpan<byte> FixUp2Subsets =>
        [
            0, 15, // shape 0
            0, 15, // shape 1
            0, 15, // shape 2
            0, 15, // shape 3
            0, 15, // shape 4
            0, 15, // shape 5
            0, 15, // shape 6
            0, 15, // shape 7
            0, 15, // shape 8
            0, 15, // shape 9
            0, 15, // shape 10
            0, 15, // shape 11
            0, 15, // shape 12
            0, 15, // shape 13
            0, 15, // shape 14
            0, 15, // shape 15
            0, 15, // shape 16
            0, 2, // shape 17
            0, 8, // shape 18
            0, 2, // shape 19
            0, 2, // shape 20
            0, 8, // shape 21
            0, 8, // shape 22
            0, 15, // shape 23
            0, 2, // shape 24
            0, 8, // shape 25
            0, 2, // shape 26
            0, 2, // shape 27
            0, 8, // shape 28
            0, 8, // shape 29
            0, 2, // shape 30
            0, 2, // shape 31
            0, 15, // shape 32
            0, 15, // shape 33
            0, 6, // shape 34
            0, 8, // shape 35
            0, 2, // shape 36
            0, 8, // shape 37
            0, 15, // shape 38
            0, 15, // shape 39
            0, 2, // shape 40
            0, 8, // shape 41
            0, 2, // shape 42
            0, 2, // shape 43
            0, 2, // shape 44
            0, 15, // shape 45
            0, 15, // shape 46
            0, 6, // shape 47
            0, 6, // shape 48
            0, 2, // shape 49
            0, 6, // shape 50
            0, 8, // shape 51
            0, 15, // shape 52
            0, 15, // shape 53
            0, 2, // shape 54
            0, 2, // shape 55
            0, 15, // shape 56
            0, 15, // shape 57
            0, 15, // shape 58
            0, 15, // shape 59
            0, 15, // shape 60
            0, 2, // shape 61
            0, 2, // shape 62
            0, 15 // shape 63
        ];

        public static ReadOnlySpan<int> Weights(int precision) => precision switch
        {
            2 => Bc7Constants.Weights2,
            3 => Bc7Constants.Weights3,
            _ => Bc7Constants.Weights4
        };

        /// <summary>Region (subset) of a pixel for a 1- or 2-subset shape.</summary>
        public static int Region(int partitions, int shape, int pixel)
            => partitions == 0 ? 0 : Partition2Subsets[(shape << 4) | pixel];

        /// <summary>True when <paramref name="pixel" /> is the anchor (reduced-bit) index of a region.</summary>
        public static bool IsFixUpOffset(int partitions, int shape, int pixel)
        {
            for (int p = 0; p <= partitions; p++)
            {
                if (pixel == FixUp2Subsets[(shape << 1) | p])
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Anchor pixel index of region <paramref name="region" />.</summary>
        public static int FixUp(int shape, int region) => FixUp2Subsets[(shape << 1) | region];
    }

    private static class Bc7ColorMath
    {
        /// <summary><c>FLT_MIN</c>: the smallest positive *normal* float (not <see cref="float.Epsilon" />).</summary>
        public const float FltMin = 1.17549435e-38f;

        /// <summary><c>fEpsilon</c> from DirectXTex (exactly 2^-16).</summary>
        public const float FEpsilon = 0.25f / 64.0f * (0.25f / 64.0f);

        private static ReadOnlySpan<float> PC3 => [2.0f / 2.0f, 1.0f / 2.0f, 0.0f / 2.0f];

        private static ReadOnlySpan<float> PD3 => [0.0f / 2.0f, 1.0f / 2.0f, 2.0f / 2.0f];

        private static ReadOnlySpan<float> PC4 => [3.0f / 3.0f, 2.0f / 3.0f, 1.0f / 3.0f, 0.0f / 3.0f];

        private static ReadOnlySpan<float> PD4 => [0.0f / 3.0f, 1.0f / 3.0f, 2.0f / 3.0f, 3.0f / 3.0f];

        /// <summary>
        ///     <c>OptimizeRGB</c>: least-squares fit of a 4-step RGB line through the selected pixels.
        ///     Alpha of the result is left at 0 (the reference leaves it uninitialised and every caller
        ///     overwrites it).
        /// </summary>
        public static void OptimizeRgb(
            ReadOnlySpan<float> points,
            ReadOnlySpan<int> indices,
            int count,
            int steps,
            out HdrColor x,
            out HdrColor y)
        {
            ReadOnlySpan<float> pC = steps == 3 ? PC3 : PC4;
            ReadOnlySpan<float> pD = steps == 3 ? PD3 : PD4;

            // Find min and max points as the starting point.
            HdrColor px = new(float.MaxValue, float.MaxValue, float.MaxValue, 0f);
            HdrColor py = new(-float.MaxValue, -float.MaxValue, -float.MaxValue, 0f);

            for (int iPoint = 0; iPoint < count; iPoint++)
            {
                int o = indices[iPoint] * 4;
                if (points[o] < px.R)
                {
                    px.R = points[o];
                }

                if (points[o + 1] < px.G)
                {
                    px.G = points[o + 1];
                }

                if (points[o + 2] < px.B)
                {
                    px.B = points[o + 2];
                }

                if (points[o] > py.R)
                {
                    py.R = points[o];
                }

                if (points[o + 1] > py.G)
                {
                    py.G = points[o + 1];
                }

                if (points[o + 2] > py.B)
                {
                    py.B = points[o + 2];
                }
            }

            // Diagonal axis.
            float abR = py.R - px.R;
            float abG = py.G - px.G;
            float abB = py.B - px.B;
            float fAB = (abR * abR) + (abG * abG) + (abB * abB);

            // Single colour block: no need to root-find.
            if (fAB < FltMin)
            {
                x = px;
                y = py;
                return;
            }

            // Try all four axis directions to determine which diagonal best fits the data.
            float fABInv = 1.0f / fAB;
            float dirR = abR * fABInv;
            float dirG = abG * fABInv;
            float dirB = abB * fABInv;

            float midR = (px.R + py.R) * 0.5f;
            float midG = (px.G + py.G) * 0.5f;
            float midB = (px.B + py.B) * 0.5f;

            float dir0 = 0f, dir1 = 0f, dir2 = 0f, dir3 = 0f;
            for (int iPoint = 0; iPoint < count; iPoint++)
            {
                int o = indices[iPoint] * 4;
                float pr = (points[o] - midR) * dirR;
                float pg = (points[o + 1] - midG) * dirG;
                float pb = (points[o + 2] - midB) * dirB;

                float f;
                f = pr + pg + pb;
                dir0 += f * f;
                f = pr + pg - pb;
                dir1 += f * f;
                f = pr - pg + pb;
                dir2 += f * f;
                f = pr - pg - pb;
                dir3 += f * f;
            }

            float fDirMax = dir0;
            int iDirMax = 0;
            if (dir1 > fDirMax)
            {
                fDirMax = dir1;
                iDirMax = 1;
            }

            if (dir2 > fDirMax)
            {
                fDirMax = dir2;
                iDirMax = 2;
            }

            if (dir3 > fDirMax)
            {
                iDirMax = 3;
            }

            if ((iDirMax & 2) != 0)
            {
                (px.G, py.G) = (py.G, px.G);
            }

            if ((iDirMax & 1) != 0)
            {
                (px.B, py.B) = (py.B, px.B);
            }

            // Two colour block: no need to root-find.
            if (fAB < 1.0f / 4096.0f)
            {
                x = px;
                y = py;
                return;
            }

            // Newton's method for a local minimum of the sum-of-squares error.
            float fSteps = steps - 1;

            for (int iIteration = 0; iIteration < 8; iIteration++)
            {
                dirR = py.R - px.R;
                dirG = py.G - px.G;
                dirB = py.B - px.B;

                float fLen = (dirR * dirR) + (dirG * dirG) + (dirB * dirB);
                if (fLen < 1.0f / 4096.0f)
                {
                    break;
                }

                float fScale = fSteps / fLen;
                dirR *= fScale;
                dirG *= fScale;
                dirB *= fScale;

                float d2X = 0f, d2Y = 0f;
                float dXR = 0f, dXG = 0f, dXB = 0f;
                float dYR = 0f, dYG = 0f, dYB = 0f;

                for (int iPoint = 0; iPoint < count; iPoint++)
                {
                    int o = indices[iPoint] * 4;
                    float pr = points[o];
                    float pg = points[o + 1];
                    float pb = points[o + 2];
                    float fDot = ((pr - px.R) * dirR)
                                 + ((pg - px.G) * dirG)
                                 + ((pb - px.B) * dirB);

                    int iStep;
                    if (fDot <= 0.0f)
                    {
                        iStep = 0;
                    }
                    else if (fDot >= fSteps)
                    {
                        iStep = steps - 1;
                    }
                    else
                    {
                        iStep = (int)(fDot + 0.5f);
                    }

                    float c = pC[iStep];
                    float d = pD[iStep];
                    float diffR = (px.R * c) + (py.R * d) - pr;
                    float diffG = (px.G * c) + (py.G * d) - pg;
                    float diffB = (px.B * c) + (py.B * d) - pb;

                    float fC = c * (1.0f / 8.0f);
                    float fD = d * (1.0f / 8.0f);

                    d2X += fC * c;
                    dXR += fC * diffR;
                    dXG += fC * diffG;
                    dXB += fC * diffB;

                    d2Y += fD * d;
                    dYR += fD * diffR;
                    dYG += fD * diffG;
                    dYB += fD * diffB;
                }

                if (d2X > 0.0f)
                {
                    float f = -1.0f / d2X;
                    px.R += dXR * f;
                    px.G += dXG * f;
                    px.B += dXB * f;
                }

                if (d2Y > 0.0f)
                {
                    float f = -1.0f / d2Y;
                    py.R += dYR * f;
                    py.G += dYG * f;
                    py.B += dYB * f;
                }

                if (dXR * dXR < FEpsilon && dXG * dXG < FEpsilon && dXB * dXB < FEpsilon
                    && dYR * dYR < FEpsilon && dYG * dYG < FEpsilon && dYB * dYB < FEpsilon)
                {
                    break;
                }
            }

            x = px;
            y = py;
        }

        /// <summary><c>OptimizeRGBA</c>: least-squares fit of a 4-step RGBA line through the pixels.</summary>
        public static void OptimizeRgba(
            ReadOnlySpan<float> points,
            ReadOnlySpan<int> indices,
            int count,
            int steps,
            out HdrColor x,
            out HdrColor y)
        {
            ReadOnlySpan<float> pC = steps == 3 ? PC3 : PC4;
            ReadOnlySpan<float> pD = steps == 3 ? PD3 : PD4;

            HdrColor px = new(1.0f, 1.0f, 1.0f, 1.0f);
            HdrColor py = new(0.0f, 0.0f, 0.0f, 0.0f);

            for (int iPoint = 0; iPoint < count; iPoint++)
            {
                int o = indices[iPoint] * 4;
                if (points[o] < px.R)
                {
                    px.R = points[o];
                }

                if (points[o + 1] < px.G)
                {
                    px.G = points[o + 1];
                }

                if (points[o + 2] < px.B)
                {
                    px.B = points[o + 2];
                }

                if (points[o + 3] < px.A)
                {
                    px.A = points[o + 3];
                }

                if (points[o] > py.R)
                {
                    py.R = points[o];
                }

                if (points[o + 1] > py.G)
                {
                    py.G = points[o + 1];
                }

                if (points[o + 2] > py.B)
                {
                    py.B = points[o + 2];
                }

                if (points[o + 3] > py.A)
                {
                    py.A = points[o + 3];
                }
            }

            HdrColor ab = py.Sub(px);
            float fAB = ab.Dot(ab);

            if (fAB < FltMin)
            {
                x = px;
                y = py;
                return;
            }

            float fABInv = 1.0f / fAB;
            HdrColor dir = ab.Mul(fABInv);
            HdrColor mid = px.Add(py).Mul(0.5f);

            float dir0 = 0f, dir1 = 0f, dir2 = 0f, dir3 = 0f;
            float dir4 = 0f, dir5 = 0f, dir6 = 0f, dir7 = 0f;

            for (int iPoint = 0; iPoint < count; iPoint++)
            {
                int o = indices[iPoint] * 4;
                float pr = (points[o] - mid.R) * dir.R;
                float pg = (points[o + 1] - mid.G) * dir.G;
                float pb = (points[o + 2] - mid.B) * dir.B;
                float pa = (points[o + 3] - mid.A) * dir.A;

                float f;
                f = pr + pg + pb + pa;
                dir0 += f * f;
                f = pr + pg + pb - pa;
                dir1 += f * f;
                f = pr + pg - pb + pa;
                dir2 += f * f;
                f = pr + pg - pb - pa;
                dir3 += f * f;
                f = pr - pg + pb + pa;
                dir4 += f * f;
                f = pr - pg + pb - pa;
                dir5 += f * f;
                f = pr - pg - pb + pa;
                dir6 += f * f;
                f = pr - pg - pb - pa;
                dir7 += f * f;
            }

            float fDirMax = dir0;
            int iDirMax = 0;
            if (dir1 > fDirMax)
            {
                fDirMax = dir1;
                iDirMax = 1;
            }

            if (dir2 > fDirMax)
            {
                fDirMax = dir2;
                iDirMax = 2;
            }

            if (dir3 > fDirMax)
            {
                fDirMax = dir3;
                iDirMax = 3;
            }

            if (dir4 > fDirMax)
            {
                fDirMax = dir4;
                iDirMax = 4;
            }

            if (dir5 > fDirMax)
            {
                fDirMax = dir5;
                iDirMax = 5;
            }

            if (dir6 > fDirMax)
            {
                fDirMax = dir6;
                iDirMax = 6;
            }

            if (dir7 > fDirMax)
            {
                iDirMax = 7;
            }

            if ((iDirMax & 4) != 0)
            {
                (px.G, py.G) = (py.G, px.G);
            }

            if ((iDirMax & 2) != 0)
            {
                (px.B, py.B) = (py.B, px.B);
            }

            if ((iDirMax & 1) != 0)
            {
                (px.A, py.A) = (py.A, px.A);
            }

            if (fAB < 1.0f / 4096.0f)
            {
                x = px;
                y = py;
                return;
            }

            float fSteps = steps - 1;

            for (int iIteration = 0; iIteration < 8; iIteration++)
            {
                dir = py.Sub(px);
                float fLen = dir.Dot(dir);
                if (fLen < 1.0f / 4096.0f)
                {
                    break;
                }

                float fScale = fSteps / fLen;
                dir = dir.Mul(fScale);

                float d2X = 0f, d2Y = 0f;
                HdrColor dX = default;
                HdrColor dY = default;

                for (int iPoint = 0; iPoint < count; iPoint++)
                {
                    int o = indices[iPoint] * 4;
                    HdrColor p = new(points[o], points[o + 1], points[o + 2], points[o + 3]);
                    float fDot = p.Sub(px).Dot(dir);

                    int iStep;
                    if (fDot <= 0.0f)
                    {
                        iStep = 0;
                    }
                    else if (fDot >= fSteps)
                    {
                        iStep = steps - 1;
                    }
                    else
                    {
                        iStep = (int)(fDot + 0.5f);
                    }

                    float c = pC[iStep];
                    float d = pD[iStep];
                    HdrColor diff = new(
                        (px.R * c) + (py.R * d) - p.R,
                        (px.G * c) + (py.G * d) - p.G,
                        (px.B * c) + (py.B * d) - p.B,
                        (px.A * c) + (py.A * d) - p.A);
                    float fC = c * (1.0f / 8.0f);
                    float fD = d * (1.0f / 8.0f);

                    d2X += fC * c;
                    dX = dX.Add(diff.Mul(fC));

                    d2Y += fD * d;
                    dY = dY.Add(diff.Mul(fD));
                }

                if (d2X > 0.0f)
                {
                    float f = -1.0f / d2X;
                    px = px.Add(dX.Mul(f));
                }

                if (d2Y > 0.0f)
                {
                    float f = -1.0f / d2Y;
                    py = py.Add(dY.Mul(f));
                }

                if (dX.Dot(dX) < FEpsilon && dY.Dot(dY) < FEpsilon)
                {
                    break;
                }
            }

            x = px;
            y = py;
        }
    }
}
