using BenchmarkDotNet.Attributes;

namespace ImageSharp.Textures.Benchmarks;

/// <summary>
///     BC7 encode throughput for both quality presets.
/// </summary>
/// <remarks>
///     The matrix stops at 1024: the full multi-mode search measured 57 s for a 1024x1024 surface on the reference
///     machine, so a 4096 case would need roughly a quarter hour per iteration and would dominate every run.
/// </remarks>
[MemoryDiagnoser]
[BenchmarkCategory("Bc", "Encode", "Bc7")]
public class Bc7EncodeBenchmarks
{
    private byte[] destination = null!;
    private Image<Rgba32> image = null!;

    /// <summary>
    ///     Gets or sets the BC7 quality preset.
    /// </summary>
    [Params(Bc7EncodingQuality.Quick, Bc7EncodingQuality.Default)]
    public Bc7EncodingQuality Quality { get; set; }

    /// <summary>
    ///     Gets or sets the surface width and height.
    /// </summary>
    [Params(256, 1024)]
    public int Size { get; set; }

    /// <summary>
    ///     Renders the source image once and allocates the destination payload.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.image = BenchmarkData.CreateImage(this.Size);
        int blocks = this.Size / 4 * (this.Size / 4);
        this.destination = new byte[blocks * BenchmarkData.BlockBytes(TexelFormat.Bc7_UNorm)];
    }

    /// <summary>
    ///     Disposes the source image.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => this.image.Dispose();

    /// <summary>
    ///     Encodes the source image with the selected quality preset.
    /// </summary>
    [Benchmark]
    public void Encode()
        => BenchmarkData.Encode(TexelFormat.Bc7_UNorm, this.image, this.destination, this.Quality);
}
