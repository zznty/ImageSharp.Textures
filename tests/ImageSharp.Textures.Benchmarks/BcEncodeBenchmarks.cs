using BenchmarkDotNet.Attributes;
using ImageSharp.Textures.Texels.Bc1;
using ImageSharp.Textures.Texels.Bc4;
using ImageSharp.Textures.Texels.Bc5;
using ImageSharp.Textures.Texels.BcCommon;

namespace ImageSharp.Textures.Benchmarks;

/// <summary>
///     Block compression encode throughput through the texel operations.
/// </summary>
/// <remarks>
///     <para>
///         The destination payload is allocated once and reused, so the measurement is the block search itself. BC7 is
///         measured separately in <see cref="Bc7EncodeBenchmarks" /> because its multi-mode search is orders of
///         magnitude slower and would otherwise dominate the whole run.
///     </para>
///     <para>
///         Two arms: <see cref="Encode" /> goes through the public texel operations (which walk the ImageSharp pixel rows
///         into a scratch buffer) and <see cref="EncodePacked" /> calls the block encoder on a tightly packed RGBA32
///         buffer, which is exactly what <see cref="DirectXTexEncodeBaselineBenchmarks" /> feeds its codec. The
///         difference between the two arms is the interop cost, not codec time.
///     </para>
/// </remarks>
[MemoryDiagnoser]
[BenchmarkCategory("Bc", "Encode")]
public class BcEncodeBenchmarks
{
    private byte[] destination = null!;
    private Image<Rgba32> image = null!;
    private byte[] packed = null!;

    /// <summary>
    ///     Gets or sets the texel format under test.
    /// </summary>
    [Params(TexelFormat.Bc1_UNorm, TexelFormat.Bc4_UNorm, TexelFormat.Bc5_UNorm)]
    public TexelFormat Format { get; set; }

    /// <summary>
    ///     Gets or sets the surface width and height.
    /// </summary>
    [Params(256, 1024, 4096)]
    public int Size { get; set; }

    /// <summary>
    ///     Renders the source image once and allocates the destination payload.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.image = BenchmarkData.CreateImage(this.Size);
        int blocks = this.Size / 4 * (this.Size / 4);
        this.destination = new byte[blocks * BenchmarkData.BlockBytes(this.Format)];
        this.packed = new byte[this.Size * this.Size * 4];
        this.image.CopyPixelDataTo(this.packed);
    }

    /// <summary>
    ///     Disposes the source image.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => this.image.Dispose();

    /// <summary>
    ///     Encodes the source image into the destination payload through the texel operations.
    /// </summary>
    [Benchmark]
    public void Encode()
        => BenchmarkData.Encode(this.Format, this.image, this.destination);

    /// <summary>
    ///     Encodes a tightly packed RGBA32 buffer directly with the block encoder, matching the DirectXTex shim's
    ///     input so the two codecs can be compared without the interop row walk.
    /// </summary>
    [Benchmark]
    public void EncodePacked()
    {
        switch (this.Format)
        {
            case TexelFormat.Bc1_UNorm:
                Bc1Encoder.Encode(this.packed, this.destination, this.Size, this.Size, BcEncodeOptions.Default);
                break;
            case TexelFormat.Bc4_UNorm:
                Bc4Encoder.Encode(this.packed, this.destination, this.Size, this.Size, BcEncodeOptions.Default);
                break;
            default:
                Bc5Encoder.Encode(this.packed, this.destination, this.Size, this.Size, BcEncodeOptions.Default);
                break;
        }
    }
}
