using BenchmarkDotNet.Attributes;
using ImageSharp.Textures.ILGPU;
using ImageSharp.Textures.Texels.Bc7;
using ImageSharp.Textures.Vulkan;

namespace ImageSharp.Textures.Benchmarks;

/// <summary>
///     Compares BC7 encode throughput across single-threaded CPU, multi-threaded CPU, Vulkan compute (Silk.NET),
///     ILGPU GPU (OpenCL/CUDA), and ILGPU CPU accelerator paths on the same RGBA32 buffer.
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory("Bc", "Encode", "Bc7", "Comparison")]
public class Bc7BackendComparisonBenchmarks : IDisposable
{
    private byte[] destination = null!;
    private IlgpuBc7Encoder? ilgpuCpuEncoder;
    private IlgpuBc7Encoder? ilgpuGpuEncoder;
    private byte[] rgba = null!;
    private VulkanBc7Encoder? vulkanEncoder;

    /// <summary>
    ///     Gets or sets the BC7 quality preset.
    /// </summary>
    [Params(Bc7EncodingQuality.Quick, Bc7EncodingQuality.Default)]
    public Bc7EncodingQuality Quality { get; set; }

    /// <summary>
    ///     Gets or sets the surface width and height.
    /// </summary>
    [Params(64, 256, 1024)]
    public int Size { get; set; }

    /// <inheritdoc />
    public void Dispose()
    {
        this.vulkanEncoder?.Dispose();
        this.ilgpuGpuEncoder?.Dispose();
        this.ilgpuCpuEncoder?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Prepares the source RGBA32 buffer, destination BC7 buffer, and pre-warmed GPU/CPU encoders.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        using Image<Rgba32> image = BenchmarkData.CreateImage(this.Size);
        this.rgba = new byte[this.Size * this.Size * 4];
        image.CopyPixelDataTo(this.rgba);
        this.destination = new byte[Bc7Encoder.GetEncodedByteCount(this.Size, this.Size)];

        byte[] warmupRgba = new byte[4 * 4 * 4];
        byte[] warmupDest = new byte[Bc7Encoder.GetEncodedByteCount(4, 4)];

        this.vulkanEncoder = new VulkanBc7Encoder();
        this.vulkanEncoder.TryEncode(warmupRgba, warmupDest, 4, 4, Bc7EncodingQuality.Default);

        this.ilgpuGpuEncoder = new IlgpuBc7Encoder(false);
        this.ilgpuGpuEncoder.TryEncode(warmupRgba, warmupDest, 4, 4, Bc7EncodingQuality.Default);

        this.ilgpuCpuEncoder = new IlgpuBc7Encoder(true);
        this.ilgpuCpuEncoder.TryEncode(warmupRgba, warmupDest, 4, 4, Bc7EncodingQuality.Default);
    }

    /// <summary>
    ///     Disposes hardware encoder contexts and buffers.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => this.Dispose();

    /// <summary>
    ///     Single-threaded CPU BC7 encoder (<see cref="Bc7Encoder.Encode" />).
    /// </summary>
    [Benchmark(Baseline = true)]
    public void CpuSingleThreaded()
        => Bc7Encoder.Encode(this.rgba, this.destination, this.Size, this.Size, this.Quality);

    /// <summary>
    ///     Multi-threaded CPU BC7 encoder (<see cref="Bc7Encoder.EncodeParallel" />).
    /// </summary>
    [Benchmark]
    public void CpuMultiThreaded()
        => Bc7Encoder.EncodeParallel(this.rgba, this.destination, this.Size, this.Size, this.Quality);

    /// <summary>
    ///     Vulkan SPIR-V compute BC7 encoder (<see cref="VulkanBc7Encoder" /> via Silk.NET.Vulkan).
    /// </summary>
    [Benchmark]
    public void VulkanGpu()
    {
        if (this.vulkanEncoder is null ||
            !this.vulkanEncoder.TryEncode(this.rgba, this.destination, this.Size, this.Size, this.Quality))
        {
            throw new InvalidOperationException("VulkanBc7Encoder is unavailable.");
        }
    }

    /// <summary>
    ///     ILGPU GPU (OpenCL/CUDA) BC7 encoder (<see cref="IlgpuBc7Encoder" /> with <c>preferCpu: false</c>).
    /// </summary>
    [Benchmark]
    public void IlgpuGpu()
    {
        if (this.ilgpuGpuEncoder is null ||
            !this.ilgpuGpuEncoder.TryEncode(this.rgba, this.destination, this.Size, this.Size, this.Quality))
        {
            throw new InvalidOperationException("IlgpuBc7Encoder (GPU) failed.");
        }
    }

    /// <summary>
    ///     ILGPU CPU accelerator BC7 encoder (<see cref="IlgpuBc7Encoder" /> with <c>preferCpu: true</c>).
    /// </summary>
    [Benchmark]
    public void IlgpuCpu()
    {
        if (this.ilgpuCpuEncoder is null ||
            !this.ilgpuCpuEncoder.TryEncode(this.rgba, this.destination, this.Size, this.Size, this.Quality))
        {
            throw new InvalidOperationException("IlgpuBc7Encoder (CPU) failed.");
        }
    }
}
