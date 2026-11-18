using BenchmarkDotNet.Attributes;
using ImageSharp.Textures.Texels.Bc1;
using ImageSharp.Textures.Texels.Bc4;
using ImageSharp.Textures.Texels.Bc5;
using ImageSharp.Textures.Texels.Bc7;

namespace ImageSharp.Textures.Benchmarks;

/// <summary>
///     Block compression decode throughput.
/// </summary>
/// <remarks>
///     Two arms: the public <see cref="TexelOperations.Decode{TPixel}" /> call, which allocates the destination
///     image and converts rows, and the raw block decode into a preallocated RGBA32 buffer, which isolates the
///     texel arithmetic.
/// </remarks>
[MemoryDiagnoser]
[BenchmarkCategory("Bc", "Decode")]
public class BcDecodeBenchmarks
{
    private byte[] destination = null!;
    private TexelOperations operations = null!;
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
    ///     Encodes the payload once and allocates the raw destination.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.operations = TexelFormatRegistry.GetOperations(this.Format);
        this.payload = BenchmarkData.GetPayload(this.Format, this.Size);
        this.destination = new byte[this.Size * this.Size * 4];
    }

    /// <summary>
    ///     Decodes through the public texel operations into a new image.
    /// </summary>
    [Benchmark]
    public void TexelOperationsDecode()
    {
        using Image<Rgba32> image =
            this.operations.Decode<Rgba32>(this.payload, Configuration.Default, this.Size, this.Size);
    }

    /// <summary>
    ///     Decodes the blocks straight into a preallocated RGBA32 buffer.
    /// </summary>
    [Benchmark]
    public void BlockDecode()
    {
        switch (this.Format)
        {
            case TexelFormat.Bc1_UNorm:
                Bc1Decoder.Decode(this.payload, this.destination, this.Size, this.Size);
                break;
            case TexelFormat.Bc4_UNorm:
                Bc4Decoder.Decode(this.payload, this.destination, this.Size, this.Size);
                break;
            case TexelFormat.Bc5_UNorm:
                Bc5Decoder.Decode(this.payload, this.destination, this.Size, this.Size);
                break;
            default:
                Bc7Decoder.Decode(this.payload, this.destination, this.Size, this.Size);
                break;
        }
    }
}
