using System.Diagnostics.CodeAnalysis;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

namespace ImageSharp.Textures.ILGPU;

/// <summary>
///     BC7 block encoder that runs the DirectXTex <c>BC7Encode.hlsl</c> compute pipeline on an ILGPU GPU accelerator
///     (CUDA or OpenCL).
/// </summary>
/// <remarks>
///     <para>
///         The encoder issues three passes over the surface, mirroring the DirectX compute shader pipeline: modes 4,
///         5, and 6 are evaluated first, the two-subset modes 1, 3, and 7 follow for the default quality preset, and a
///         final pass packs the winning candidate of every block into 16 bytes. The three-subset modes 0 and 2 are
///         not evaluated, matching the reference pipeline's default flags.
///     </para>
///     <para>
///         Alpha contributes with unit weight and no separate alpha weighting is applied, matching the reference
///         pipeline defaults. Partial edge blocks are completed using DirectXTex's <c>{ 0, 0, 0, 1 }</c> edge
///         replication table.
///     </para>
///     <para>
///         Only hardware GPU accelerators (<see cref="AcceleratorType.Cuda" /> and <see cref="AcceleratorType.OpenCL" />)
///         are selected by the public API; ILGPU's CPU workgroup emulator is intentionally excluded because multi-threaded
///         CPU encoding via <see cref="Formats.Dds.DdsEncodingOptions.MaxDegreeOfParallelism" /> is orders of magnitude
///         faster on CPU.
///         All <see cref="TryEncode" /> and <see cref="Dispose" /> calls are synchronized and thread-safe.
///     </para>
/// </remarks>
public sealed class IlgpuBc7Encoder : IBc7HardwareEncoder
{
    private const int BlockBytes = 16;

    private const int BlockEdge = 4;

    private readonly Accelerator accelerator;

    private readonly Context context;

    private readonly
        Action<KernelConfig, ArrayView<uint>, ArrayView<uint>, ArrayView<uint>, ArrayView<uint>, int, int, int, int>
        encodeBlockKernel;

    private readonly Lock syncLock = new();

    private readonly MemoryBuffer1D<uint, Stride1D.Dense> tableBuffer;

    private readonly
        Action<KernelConfig, ArrayView<uint>, ArrayView<uint>, ArrayView<uint>, ArrayView<uint>, int, int, int, int,
            uint> tryMode137Kernel;

    private readonly Action<KernelConfig, ArrayView<uint>, ArrayView<uint>, ArrayView<uint>, int, int, int, int>
        tryMode456Kernel;

    private MemoryBuffer1D<byte, Stride1D.Dense>? blockBuffer;

    private int blockCapacity;

    private bool disposed;

    private MemoryBuffer1D<uint, Stride1D.Dense>? errorBufferA;

    private MemoryBuffer1D<uint, Stride1D.Dense>? errorBufferB;

    private MemoryBuffer1D<byte, Stride1D.Dense>? sourceBuffer;

    private long sourceByteCapacity;

    /// <summary>
    ///     Initializes a new instance of the <see cref="IlgpuBc7Encoder" /> class bound to the best available CUDA or
    ///     OpenCL GPU accelerator.
    /// </summary>
    /// <exception cref="NotSupportedException">
    ///     No compatible CUDA or OpenCL GPU accelerator with at least 64 threads per group was found.
    /// </exception>
    public IlgpuBc7Encoder()
        : this(false, false)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="IlgpuBc7Encoder" /> class for internal testing and benchmarking.
    /// </summary>
    /// <param name="preferCpu">
    ///     <see langword="true" /> to force the ILGPU CPU accelerator for parity verification; <see langword="false" />
    ///     to prefer a CUDA or OpenCL GPU accelerator.
    /// </param>
    /// <param name="allowCpuFallback">
    ///     <see langword="true" /> to allow falling back to the ILGPU CPU accelerator when no GPU is present.
    /// </param>
    internal IlgpuBc7Encoder(bool preferCpu, bool allowCpuFallback = true)
    {
        Context context = Context.Create(builder => builder.Default().Optimize(OptimizationLevel.O1));
        Device? device = null;

        if (!preferCpu)
        {
            foreach (Device candidate in context.Devices)
            {
                if (candidate.AcceleratorType == AcceleratorType.Cuda &&
                    candidate.MaxNumThreadsPerGroup >= Bc7Kernels.GroupSize)
                {
                    device = candidate;
                    break;
                }
            }

            if (device is null)
            {
                foreach (Device candidate in context.Devices)
                {
                    if (candidate.AcceleratorType == AcceleratorType.OpenCL &&
                        candidate.MaxNumThreadsPerGroup >= Bc7Kernels.GroupSize)
                    {
                        device = candidate;
                        break;
                    }
                }
            }
        }

        if (device is null)
        {
            context.Dispose();

            if (!preferCpu && !allowCpuFallback)
            {
                throw new NotSupportedException(
                    "No compatible CUDA or OpenCL GPU accelerator with at least 64 threads per workgroup was found.");
            }

            // The default CPU device is capped at 16 threads per group, which is below the 64-thread groups the
            // kernels are written for, so the CPU device is re-created with a wider group limit.
            context = Context.Create(builder => builder.CPU(CPUDeviceKind.Intel).Optimize(OptimizationLevel.O1));
            device = context.GetPreferredDevice(true);
        }

        this.context = context;
        Accelerator accelerator = device.CreateAccelerator(context);
        this.accelerator = accelerator;

        try
        {
            uint[] tableData = Bc7Tables.Create();
            this.tableBuffer = accelerator.Allocate1D<uint>(tableData.Length);
            this.tableBuffer.View.CopyFromCPU(accelerator.DefaultStream, tableData);

            this.tryMode456Kernel =
                accelerator.LoadStreamKernel<ArrayView<uint>, ArrayView<uint>, ArrayView<uint>, int, int, int, int>(
                    Bc7Kernels.TryMode456);
            this.tryMode137Kernel =
                accelerator
                    .LoadStreamKernel<ArrayView<uint>, ArrayView<uint>, ArrayView<uint>, ArrayView<uint>, int, int, int,
                        int, uint>(
                        Bc7Kernels.TryMode137);
            this.encodeBlockKernel =
                accelerator
                    .LoadStreamKernel<ArrayView<uint>, ArrayView<uint>, ArrayView<uint>, ArrayView<uint>, int, int, int,
                        int>(
                        Bc7Kernels.EncodeBlock);
        }
        catch
        {
            this.tableBuffer?.Dispose();
            accelerator.Dispose();
            context.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Gets the name of the accelerator device backing this encoder.
    /// </summary>
    public string AcceleratorName => this.accelerator.Name;

    /// <summary>
    ///     Gets the accelerator type backing this encoder.
    /// </summary>
    public AcceleratorType AcceleratorType => this.accelerator.AcceleratorType;

    /// <inheritdoc />
    public bool TryEncode(
        ReadOnlySpan<byte> rgbaSource,
        Span<byte> destination,
        int width,
        int height,
        Bc7EncodingQuality quality)
    {
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        long pixelCount = (long)width * height;
        int blockColumns = (width + (BlockEdge - 1)) / BlockEdge;
        int blockRows = (height + (BlockEdge - 1)) / BlockEdge;
        int blockCount = blockColumns * blockRows;

        long sourceByteCount = pixelCount * 4;
        long destinationByteCount = (long)blockCount * BlockBytes;

        if (rgbaSource.Length < sourceByteCount || destination.Length < destinationByteCount)
        {
            return false;
        }

        if (sourceByteCount > int.MaxValue || (long)blockCount * 4 > int.MaxValue)
        {
            return false;
        }

        lock (this.syncLock)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);

            this.EnsureCapacity(sourceByteCount, blockCount);

            MemoryBuffer1D<byte, Stride1D.Dense> source = this.sourceBuffer!;
            MemoryBuffer1D<uint, Stride1D.Dense> errors = this.errorBufferA!;

            KernelConfig blockConfig = new(
                new Index1D((blockCount + Bc7Kernels.BlocksPerGroup - 1) / Bc7Kernels.BlocksPerGroup),
                new Index1D(Bc7Kernels.GroupSize));

            ArrayView1D<byte, Stride1D.Dense> sourceBytesView =
                source.View.SubView(new Index1D(0), new Index1D((int)sourceByteCount));
            sourceBytesView.CopyFromCPU(this.accelerator.DefaultStream, rgbaSource[..(int)sourceByteCount]);
            ArrayView<uint> sourceWordsView = source.View.BaseView.Cast<uint>();

            this.tryMode456Kernel(blockConfig, sourceWordsView, errors.View, this.tableBuffer.View, width, height,
                blockColumns, blockCount);

            if (quality != Bc7EncodingQuality.Quick)
            {
                KernelConfig partitionConfig = new(new Index1D(blockCount), new Index1D(Bc7Kernels.GroupSize));
                MemoryBuffer1D<uint, Stride1D.Dense> second = this.errorBufferB!;

                this.tryMode137Kernel(partitionConfig, sourceWordsView, errors.View, second.View, this.tableBuffer.View,
                    width, height, blockColumns, blockCount, 1u);
                this.tryMode137Kernel(partitionConfig, sourceWordsView, second.View, errors.View, this.tableBuffer.View,
                    width, height, blockColumns, blockCount, 3u);
                this.tryMode137Kernel(partitionConfig, sourceWordsView, errors.View, second.View, this.tableBuffer.View,
                    width, height, blockColumns, blockCount, 7u);
                errors = second;
            }

            MemoryBuffer1D<byte, Stride1D.Dense> blocks = this.blockBuffer!;
            ArrayView<uint> blockWordsView = blocks.View.BaseView.Cast<uint>();
            this.encodeBlockKernel(blockConfig, sourceWordsView, errors.View, blockWordsView, this.tableBuffer.View,
                width, height, blockColumns, blockCount);

            ArrayView1D<byte, Stride1D.Dense> blockBytesView =
                blocks.View.SubView(new Index1D(0), new Index1D((int)destinationByteCount));
            blockBytesView.CopyToCPU(this.accelerator.DefaultStream, destination[..(int)destinationByteCount]);

            return true;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (this.syncLock)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.sourceBuffer?.Dispose();
            this.errorBufferA?.Dispose();
            this.errorBufferB?.Dispose();
            this.blockBuffer?.Dispose();
            this.tableBuffer.Dispose();
            this.accelerator.Dispose();
            this.context.Dispose();
        }
    }

    /// <summary>
    ///     Attempts to create a GPU-backed <see cref="IlgpuBc7Encoder" /> using a CUDA or OpenCL accelerator.
    /// </summary>
    /// <param name="encoder">
    ///     When this method returns <see langword="true" />, contains the initialized <see cref="IlgpuBc7Encoder" />;
    ///     otherwise, <see langword="null" />.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> if a compatible CUDA or OpenCL GPU accelerator was initialized; otherwise,
    ///     <see langword="false" />.
    /// </returns>
    public static bool TryCreate([NotNullWhen(true)] out IlgpuBc7Encoder? encoder)
    {
        try
        {
            encoder = new IlgpuBc7Encoder();
            return true;
        }
        catch
        {
            encoder = null;
            return false;
        }
    }

    /// <summary>
    ///     Grows the reusable device buffers to hold the requested surface.
    /// </summary>
    /// <param name="sourceByteCount">The number of source RGBA32 bytes to store.</param>
    /// <param name="blockCount">The number of 16-byte BC7 blocks to store.</param>
    private void EnsureCapacity(long sourceByteCount, int blockCount)
    {
        if (this.sourceBuffer is null || this.sourceByteCapacity < sourceByteCount)
        {
            this.sourceBuffer?.Dispose();
            this.sourceBuffer = null;
            this.sourceByteCapacity = 0;
            this.sourceBuffer = this.accelerator.Allocate1D<byte>(sourceByteCount);
            this.sourceByteCapacity = sourceByteCount;
        }

        if (this.errorBufferA is null || this.blockCapacity < blockCount)
        {
            this.errorBufferA?.Dispose();
            this.errorBufferB?.Dispose();
            this.blockBuffer?.Dispose();
            this.errorBufferA = null;
            this.errorBufferB = null;
            this.blockBuffer = null;
            this.blockCapacity = 0;
            this.errorBufferA = this.accelerator.Allocate1D<uint>((long)blockCount * 4);
            this.errorBufferB = this.accelerator.Allocate1D<uint>((long)blockCount * 4);
            this.blockBuffer = this.accelerator.Allocate1D<byte>((long)blockCount * BlockBytes);
            this.blockCapacity = blockCount;
        }
    }
}
