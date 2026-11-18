using SixLabors.ImageSharp.Advanced;

namespace ImageSharp.Textures.Texels.Uncompressed;

/// <summary>
///     Converts the R8G8B8A8_UNorm texel format between payloads and ImageSharp images.
/// </summary>
/// <remarks>
///     The byte order is R, G, B, A. Alpha is treated as unassociated because the payload is not
///     premultiplied by any container that uses this format.
/// </remarks>
internal sealed class Rgba32TexelOperations : TexelOperations
{
    private const int BytesPerTexel = 4;

    private static readonly TexelTypeInfo TexelTypeInfo = new()
    {
        BitsPerTexel = 32,
        ColorType = TexelColorType.RGB | TexelColorType.Alpha,
        ComponentInfo = new TexelComponentInfo(
            [8, 8, 8, 8],
            [
                TexelComponentInterpretation.UNorm,
                TexelComponentInterpretation.UNorm,
                TexelComponentInterpretation.UNorm,
                TexelComponentInterpretation.UNorm
            ]),
        AlphaRepresentation = TexelAlphaRepresentation.Unassociated
    };

    private Rgba32TexelOperations()
    {
    }

    /// <summary>
    ///     Gets the shared operations instance. The operations are stateless.
    /// </summary>
    internal static Rgba32TexelOperations Instance { get; } = new();

    /// <inheritdoc />
    public override TexelTypeInfo GetTexelTypeInfo() => TexelTypeInfo;

    /// <inheritdoc />
    public override int GetEncodedByteCount(int width, int height) => checked(width * height * BytesPerTexel);

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

        int rowBytes = checked(width * BytesPerTexel);
        int totalBytes = checked(rowBytes * height);

        if (source.Length < totalBytes)
        {
            throw new ArgumentException(
                $"The source payload contains {source.Length} bytes but {totalBytes} bytes are required for a {width}x{height} R8G8B8A8_UNorm surface.",
                nameof(source));
        }

        Image<TPixel> image = new(configuration, width, height);
        ConvertRows(image, source, configuration, width, height);
        return image;
    }

    /// <inheritdoc />
    public override void Encode<TPixel>(Image<TPixel> source, Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(source);

        int width = source.Width;
        int height = source.Height;
        int rowBytes = checked(width * BytesPerTexel);
        int totalBytes = checked(rowBytes * height);

        if (destination.Length < totalBytes)
        {
            throw new ArgumentException(
                $"The destination payload contains {destination.Length} bytes but {totalBytes} bytes are required for a {width}x{height} R8G8B8A8_UNorm surface.",
                nameof(destination));
        }

        CopyRows(source, destination);
    }

    private static void ConvertRows<TPixel>(
        Image<TPixel> image,
        ReadOnlySpan<byte> source,
        Configuration configuration,
        int width,
        int height)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int rowBytes = width * BytesPerTexel;
        PixelOperations<TPixel> operations = PixelOperations<TPixel>.Instance;

        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<byte> sourceRow = source.Slice(y * rowBytes, rowBytes);
            Span<TPixel> destinationRow = image.DangerousGetPixelRowMemory(y).Span;
            operations.FromRgba32Bytes(configuration, sourceRow, destinationRow, width);
        }
    }

    private static void CopyRows<TPixel>(Image<TPixel> image, Span<byte> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = image.Width;
        int height = image.Height;
        int rowBytes = width * BytesPerTexel;
        Configuration configuration = image.Configuration;
        PixelOperations<TPixel> operations = PixelOperations<TPixel>.Instance;

        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<TPixel> sourceRow = image.DangerousGetPixelRowMemory(y).Span;
            Span<byte> destinationRow = destination.Slice(y * rowBytes, rowBytes);
            operations.ToRgba32Bytes(configuration, sourceRow, destinationRow, width);
        }
    }
}
