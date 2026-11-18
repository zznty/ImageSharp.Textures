using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;

namespace ImageSharp.Textures.Benchmarks;

/// <summary>
///     Decode throughput of the pinned DirectXTex CPU codecs, called in process through
///     <c>artifacts/dxtexshim/libdxtexshim.so</c> (built by <c>scripts/build-dxtex-shim.sh</c>).
/// </summary>
/// <remarks>
///     <para>
///         This is the equal-work counterpart of <see cref="TexconvBaselineBenchmarks" />: the shim wraps
///         <c>D3DXDecodeBC1/BC4U/BC5U/BC7</c> over raw buffers, so one iteration is the codec on a preallocated
///         payload and destination and nothing else. The cases mirror <see cref="BcDecodeBenchmarks" /> one to one
///         (same payloads from <see cref="BenchmarkData" />, same sizes, same raw block decode into a preallocated
///         RGBA32 buffer).
///     </para>
///     <para>
///         Every setup proves the shim is the codec this library is tested against: it encodes a 64x64 surface with
///         both implementations (payloads must be byte identical) and decodes it with both (parity thresholds), so a
///         mismatch fails the run instead of silently timing a different codec.
///     </para>
/// </remarks>
[MemoryDiagnoser]
[BenchmarkCategory("DirectXTex")]
public class DirectXTexBaselineBenchmarks
{
    private byte[] destination = null!;
    private byte[] payload = null!;

    /// <summary>
    ///     Gets or sets the texel format under test.
    /// </summary>
    [Params(TexelFormat.Bc1_UNorm, TexelFormat.Bc4_UNorm, TexelFormat.Bc5_UNorm, TexelFormat.Bc7_UNorm)]
    public TexelFormat Format { get; set; }

    /// <summary>
    ///     Gets or sets the surface width and height.
    /// </summary>
    [Params(256, 1024, 4096)]
    public int Size { get; set; }

    /// <summary>
    ///     Encodes the payload once, allocates the raw destination and verifies the shim.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        DirectXTexShim.EnsureVerified();
        this.payload = BenchmarkData.GetPayload(this.Format, this.Size);
        this.destination = new byte[this.Size * this.Size * 4];
    }

    /// <summary>
    ///     Decodes the blocks straight into the preallocated RGBA32 buffer.
    /// </summary>
    [Benchmark]
    public void Decode() => DirectXTexShim.Decode(this.Format, this.payload, this.destination, this.Size);
}

/// <summary>
///     Encode throughput of the pinned DirectXTex BC1/BC4/BC5 codecs, in process, at 1024x1024.
/// </summary>
/// <remarks>
///     Mirrors <see cref="BcEncodeBenchmarks.Encode" />: the source is the same gradient surface and the
///     destination payload is allocated once. Our arm additionally walks the ImageSharp pixel rows into a scratch
///     buffer (a memcpy for <see cref="Rgba32" />); the shim consumes the packed RGBA directly, so our number
///     carries that row copy.
/// </remarks>
[MemoryDiagnoser]
[BenchmarkCategory("DirectXTex", "Encode")]
public class DirectXTexEncodeBaselineBenchmarks
{
    private const int SurfaceSize = 1024;
    private byte[] destination = null!;

    private byte[] source = null!;

    /// <summary>
    ///     Gets or sets the texel format under test.
    /// </summary>
    [Params(TexelFormat.Bc1_UNorm, TexelFormat.Bc4_UNorm, TexelFormat.Bc5_UNorm)]
    public TexelFormat Format { get; set; }

    /// <summary>
    ///     Renders the source once, packs it as RGBA32 and allocates the destination payload.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        DirectXTexShim.EnsureVerified();
        this.source = DirectXTexShim.PackSource(SurfaceSize);
        this.destination = new byte[SurfaceSize / 4 * (SurfaceSize / 4) * BenchmarkData.BlockBytes(this.Format)];
    }

    /// <summary>
    ///     Encodes the source into the destination payload.
    /// </summary>
    [Benchmark]
    public void Encode() =>
        DirectXTexShim.Encode(this.Format, this.source, this.destination, SurfaceSize, Bc7EncodingQuality.Default);
}

/// <summary>
///     BC7 encode throughput of the pinned DirectXTex codec for both quality presets, in process, at 1024x1024.
/// </summary>
/// <remarks>
///     Mirrors <see cref="Bc7EncodeBenchmarks.Encode" />. <see cref="Bc7EncodingQuality.Quick" /> maps to
///     <c>BC_FLAGS_FORCE_BC7_MODE6</c> (the library's mode 6 preset, texconv's <c>-bc q</c>) and
///     <see cref="Bc7EncodingQuality.Default" /> to the full multi-mode search.
/// </remarks>
[MemoryDiagnoser]
[BenchmarkCategory("DirectXTex", "Encode", "Bc7")]
public class DirectXTexBc7EncodeBaselineBenchmarks
{
    private const int SurfaceSize = 1024;
    private byte[] destination = null!;

    private byte[] source = null!;

    /// <summary>
    ///     Gets or sets the BC7 quality preset.
    /// </summary>
    [Params(Bc7EncodingQuality.Quick, Bc7EncodingQuality.Default)]
    public Bc7EncodingQuality Quality { get; set; }

    /// <summary>
    ///     Renders the source once, packs it as RGBA32 and allocates the destination payload.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        DirectXTexShim.EnsureVerified();
        this.source = DirectXTexShim.PackSource(SurfaceSize);
        this.destination =
            new byte[SurfaceSize / 4 * (SurfaceSize / 4) * BenchmarkData.BlockBytes(TexelFormat.Bc7_UNorm)];
    }

    /// <summary>
    ///     Encodes the source with the selected quality preset.
    /// </summary>
    [Benchmark]
    public void Encode() =>
        DirectXTexShim.Encode(TexelFormat.Bc7_UNorm, this.source, this.destination, SurfaceSize, this.Quality);
}

/// <summary>
///     Binds <c>libdxtexshim.so</c> and proves it is the same codec the library ships against.
/// </summary>
/// <remarks>
///     The library is resolved from <c>artifacts/dxtexshim/libdxtexshim.so</c> and must be produced by
///     <c>scripts/build-dxtex-shim.sh</c>. The first access runs the codec-equivalence check described on
///     <see cref="DirectXTexBaselineBenchmarks" />; it throws rather than letting a mismatched codec be timed.
/// </remarks>
internal static class DirectXTexShim
{
    private const string LibraryFile = "libdxtexshim.so";
    private const int VerifySize = 64;
    private const int Bc7FixedZero = 1;

    private static readonly Lazy<Functions> Resolved = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    ///     Decodes a block payload into an RGBA32 buffer.
    /// </summary>
    /// <param name="format">The texel format.</param>
    /// <param name="source">The encoded payload.</param>
    /// <param name="destination">The RGBA32 destination, <c>size * size * 4</c> bytes.</param>
    /// <param name="size">The surface width and height.</param>
    internal static void Decode(TexelFormat format, byte[] source, byte[] destination, int size)
        => DecodeCore(Resolved.Value, ToShimFormat(format), source, destination, size);

    /// <summary>
    ///     Encodes an RGBA32 buffer into a block payload.
    /// </summary>
    /// <param name="format">The texel format.</param>
    /// <param name="source">The RGBA32 source, <c>size * size * 4</c> bytes.</param>
    /// <param name="destination">The block payload destination.</param>
    /// <param name="size">The surface width and height.</param>
    /// <param name="quality">The BC7 quality preset; ignored by the other formats.</param>
    internal static void Encode(TexelFormat format, byte[] source, byte[] destination, int size,
        Bc7EncodingQuality quality)
        => EncodeCore(Resolved.Value, ToShimFormat(format), source, destination, size, quality);

    private static void DecodeCore(Functions functions, int shimFormat, byte[] source, byte[] destination, int size)
    {
        fixed (byte* sourcePointer = source)
        {
            fixed (byte* destinationPointer = destination)
            {
                functions.Decode(shimFormat, (nint)sourcePointer, (nint)destinationPointer, size, size);
            }
        }
    }

    private static void EncodeCore(Functions functions, int shimFormat, byte[] source, byte[] destination, int size,
        Bc7EncodingQuality quality)
    {
        int numericQuality = quality == Bc7EncodingQuality.Quick ? 0 : 1;

        fixed (byte* sourcePointer = source)
        {
            fixed (byte* destinationPointer = destination)
            {
                functions.Encode(shimFormat, (nint)sourcePointer, (nint)destinationPointer, size, size, numericQuality,
                    0.5f);
            }
        }
    }

    /// <summary>
    ///     Packs a deterministic gradient surface into a tightly packed RGBA32 buffer.
    /// </summary>
    /// <param name="size">The surface width and height.</param>
    /// <returns>The RGBA32 bytes.</returns>
    internal static byte[] PackSource(int size)
    {
        using Image<Rgba32> image = BenchmarkData.CreateImage(size);
        byte[] bytes = new byte[size * size * 4];
        image.CopyPixelDataTo(bytes);
        return bytes;
    }

    /// <summary>
    ///     Loads the shim and verifies it, failing the benchmark run when it is missing or mismatched.
    /// </summary>
    internal static void EnsureVerified() => _ = Resolved.Value;

    private static Functions Load()
    {
        string path = ResolveLibraryPath();
        nint handle = NativeLibrary.Load(path);

        Functions functions;
        functions = new Functions(
            Marshal.GetDelegateForFunctionPointer<BlockBytesFunc>(NativeLibrary.GetExport(handle, "dxtex_block_bytes")),
            Marshal.GetDelegateForFunctionPointer<DecodeFunc>(NativeLibrary.GetExport(handle, "dxtex_decode")),
            Marshal.GetDelegateForFunctionPointer<EncodeFunc>(NativeLibrary.GetExport(handle, "dxtex_encode")));

        Verify(functions, path);
        return functions;
    }

    private static string ResolveLibraryPath()
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable("DXTEXSHIM_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnvironment) && File.Exists(fromEnvironment))
        {
            return fromEnvironment;
        }

        string candidate = Path.Combine(FindRepositoryRoot(), "artifacts", "dxtexshim", LibraryFile);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        throw new InvalidOperationException(
            $"{LibraryFile} not found. Run scripts/build-dxtex-shim.sh (looked at '{fromEnvironment}' and '{candidate}').");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ImageSharp.Textures.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root (no ImageSharp.Textures.sln) above '{AppContext.BaseDirectory}'.");
    }

    /// <summary>
    ///     Encodes and decodes a small surface with both codecs and fails on any mismatch.
    /// </summary>
    private static void Verify(Functions functions, string path)
    {
        byte[] source = PackSource(VerifySize);
        TexelFormat[] formats =
            [TexelFormat.Bc1_UNorm, TexelFormat.Bc4_UNorm, TexelFormat.Bc5_UNorm, TexelFormat.Bc7_UNorm];

        foreach (TexelFormat format in formats)
        {
            Bc7EncodingQuality[] qualities = format == TexelFormat.Bc7_UNorm
                ? [Bc7EncodingQuality.Quick, Bc7EncodingQuality.Default]
                : [Bc7EncodingQuality.Default];

            int blockBytes = BenchmarkData.BlockBytes(format);
            int blocks = VerifySize / 4 * (VerifySize / 4);

            foreach (Bc7EncodingQuality quality in qualities)
            {
                byte[] ours = new byte[blocks * blockBytes];
                BenchmarkData.Encode(format, CreateVerifyImage(), ours, quality);

                byte[] theirs = new byte[blocks * blockBytes];
                EncodeCore(functions, ToShimFormat(format), source, theirs, VerifySize, quality);

                if (!ours.AsSpan().SequenceEqual(theirs))
                {
                    int differing = 0;
                    for (int i = 0; i < ours.Length; i++)
                    {
                        if (ours[i] != theirs[i])
                        {
                            differing++;
                        }
                    }

                    throw new InvalidOperationException(
                        $"{path} is not the codec this library is tested against: {format} ({quality}) payloads differ in "
                        + $"{differing} of {ours.Length} bytes on a {VerifySize}x{VerifySize} surface.");
                }

                byte[] ourDecode = DecodeWithLibrary(format, ours, VerifySize);
                byte[] theirDecode = new byte[VerifySize * VerifySize * 4];
                DecodeCore(functions, ToShimFormat(format), ours, theirDecode, VerifySize);

                int limit = format == TexelFormat.Bc7_UNorm ? 0 : 1;
                int maxDiff = MaxDeclaredChannelDiff(format, ourDecode, theirDecode);

                if (maxDiff > limit)
                {
                    throw new InvalidOperationException(
                        $"{path} is not the codec this library is tested against: {format} ({quality}) decode differs by "
                        + $"{maxDiff} LSB (limit {limit}) on a {VerifySize}x{VerifySize} surface.");
                }
            }
        }
    }

    private static Image<Rgba32> CreateVerifyImage() => BenchmarkData.CreateImage(VerifySize);

    private static byte[] DecodeWithLibrary(TexelFormat format, byte[] payload, int size)
    {
        TexelOperations operations = TexelFormatRegistry.GetOperations(format);
        using Image<Rgba32> decoded = operations.Decode<Rgba32>(payload, Configuration.Default, size, size);

        byte[] bytes = new byte[size * size * 4];
        decoded.CopyPixelDataTo(bytes);
        return bytes;
    }

    private static int MaxDeclaredChannelDiff(TexelFormat format, byte[] expected, byte[] actual)
    {
        // BC4 declares red only and BC5 red and green; texconv's single channel dumps splat red across RGB.
        (int First, int Last) range = format switch
        {
            TexelFormat.Bc4_UNorm => (0, 1),
            TexelFormat.Bc5_UNorm => (0, 2),
            _ => (0, 4)
        };

        int maxDiff = 0;
        for (int i = 0; i < expected.Length; i += 4)
        {
            for (int channel = range.First; channel < range.Last; channel++)
            {
                int diff = Math.Abs(expected[i + channel] - actual[i + channel]);
                if (diff > maxDiff)
                {
                    maxDiff = diff;
                }
            }
        }

        return maxDiff;
    }

    private static int ToShimFormat(TexelFormat format) => format switch
    {
        TexelFormat.Bc1_UNorm => 0,
        TexelFormat.Bc4_UNorm => 1,
        TexelFormat.Bc5_UNorm => 2,
        _ => 3
    };

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int BlockBytesFunc(int format);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DecodeFunc(int format, nint source, nint rgba, int width, int height);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void EncodeFunc(int format, nint rgba, nint destination, int width, int height, int quality,
        float bc1AlphaThreshold);

    private sealed class Functions
    {
        internal Functions(BlockBytesFunc blockBytes, DecodeFunc decode, EncodeFunc encode)
        {
            this.BlockBytes = blockBytes;
            this.Decode = decode;
            this.Encode = encode;
        }

        internal BlockBytesFunc BlockBytes { get; }

        internal DecodeFunc Decode { get; }

        internal EncodeFunc Encode { get; }
    }
}
