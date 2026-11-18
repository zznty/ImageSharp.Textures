using System.Buffers;
using SixLabors.ImageSharp.Advanced;

namespace ImageSharp.Textures.Texels.Uncompressed;

/// <summary>
///     Converts a one or two channel 8-bit unsigned normalized texel format between payloads and ImageSharp images.
/// </summary>
/// <remarks>
///     The leading channels carry the payload in R, G order; the remaining color channels decode to zero and alpha
///     decodes to 255. These are the payload semantics of BC4 (one channel) and BC5 (two channels), so the block
///     compression lanes can project a single or dual channel payload into RGBA without re-implementing the rules.
/// </remarks>
internal sealed class UnormChannelTexelOperations : TexelOperations
{
    private const int BitsPerTexel = 8;

    private readonly int channelCount;
    private readonly TexelTypeInfo texelTypeInfo;

    private UnormChannelTexelOperations(int channelCount)
    {
        this.channelCount = channelCount;

        TexelColorType colorType = channelCount == 1
            ? TexelColorType.Red
            : TexelColorType.Red | TexelColorType.Green;

        int[] precisions = channelCount == 1 ? [BitsPerTexel] : [BitsPerTexel, BitsPerTexel];
        TexelComponentInterpretation[] interpretations = channelCount == 1
            ? [TexelComponentInterpretation.UNorm]
            : [TexelComponentInterpretation.UNorm, TexelComponentInterpretation.UNorm];

        this.texelTypeInfo = new TexelTypeInfo
        {
            BitsPerTexel = BitsPerTexel * channelCount,
            ColorType = colorType,
            ComponentInfo = new TexelComponentInfo(precisions, interpretations),
            AlphaRepresentation = TexelAlphaRepresentation.None
        };
    }

    /// <summary>
    ///     Gets the operations for an 8-bit single channel payload.
    /// </summary>
    internal static UnormChannelTexelOperations R8 { get; } = new(1);

    /// <summary>
    ///     Gets the operations for an 8-bit dual channel payload.
    /// </summary>
    internal static UnormChannelTexelOperations R8G8 { get; } = new(2);

    /// <inheritdoc />
    public override TexelTypeInfo GetTexelTypeInfo() => this.texelTypeInfo;

    /// <inheritdoc />
    public override int GetEncodedByteCount(int width, int height)
        => checked(width * height * this.channelCount);

    /// <inheritdoc />
    public override Image<TPixel> Decode<TPixel>(
        ReadOnlySpan<byte> source,
        Configuration configuration,
        int width,
        int height)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "The surface width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "The surface height must be positive.");
        }

        int rowBytes = checked(width * this.channelCount);
        int totalBytes = checked(rowBytes * height);

        if (source.Length < totalBytes)
        {
            throw new ArgumentException(
                $"The source payload contains {source.Length} bytes but {totalBytes} bytes are required for a {width}x{height} surface.",
                nameof(source));
        }

        Image<TPixel> image = new(configuration, width, height);
        using IMemoryOwner<Rgba32> scratchOwner = configuration.MemoryAllocator.Allocate<Rgba32>(width);
        Span<Rgba32> scratch = scratchOwner.Memory.Span[..width];
        int channelCount = this.channelCount;
        PixelOperations<TPixel> operations = PixelOperations<TPixel>.Instance;

        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<byte> sourceRow = source.Slice(y * rowBytes, rowBytes);
            Span<TPixel> destinationRow = image.DangerousGetPixelRowMemory(y).Span;

            for (int x = 0; x < width; x++)
            {
                int offset = x * channelCount;
                scratch[x] = channelCount == 1
                    ? new Rgba32(sourceRow[offset], 0, 0, 255)
                    : new Rgba32(sourceRow[offset], sourceRow[offset + 1], 0, 255);
            }

            operations.FromRgba32(configuration, scratch, destinationRow);
        }

        return image;
    }

    /// <inheritdoc />
    public override void Encode<TPixel>(Image<TPixel> source, Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(source);

        int width = source.Width;
        int height = source.Height;
        int rowBytes = checked(width * this.channelCount);
        int totalBytes = checked(rowBytes * height);

        if (destination.Length < totalBytes)
        {
            throw new ArgumentException(
                $"The destination payload contains {destination.Length} bytes but {totalBytes} bytes are required for a {width}x{height} surface.",
                nameof(destination));
        }

        Configuration configuration = source.Configuration;
        using IMemoryOwner<Rgba32> scratchOwner = configuration.MemoryAllocator.Allocate<Rgba32>(width);
        Span<Rgba32> scratch = scratchOwner.Memory.Span[..width];
        int channelCount = this.channelCount;
        PixelOperations<TPixel> operations = PixelOperations<TPixel>.Instance;

        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<TPixel> sourceRow = source.DangerousGetPixelRowMemory(y).Span;
            Span<byte> destinationRow = destination.Slice(y * rowBytes, rowBytes);
            operations.ToRgba32(configuration, sourceRow, scratch);

            for (int x = 0; x < width; x++)
            {
                int offset = x * channelCount;
                Rgba32 pixel = scratch[x];
                destinationRow[offset] = pixel.R;

                if (channelCount == 2)
                {
                    destinationRow[offset + 1] = pixel.G;
                }
            }
        }
    }
}
