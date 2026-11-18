using System.Buffers;
using ImageSharp.Textures.Texels.BcCommon;
using SixLabors.ImageSharp.Advanced;

namespace ImageSharp.Textures.Texels.Bc7;

/// <summary>
///     Converts the BC7_UNorm texel formats between payloads and ImageSharp images.
/// </summary>
/// <remarks>
///     <para>
///         BC7 stores full RGBA colour in a 128-bit block and uses straight (unassociated) alpha. The sRGB variant uses
///         the same arithmetic and bytes; only the component interpretation of the texel type differs.
///     </para>
///     <para>
///         Encoding runs the multi-mode search in <see cref="Bc7Encoder" />, which consumes a whole surface rather than
///         one block at a time, so a tightly packed RGBA32 surface is materialised in one rented buffer and handed to
///         it. That is the only working memory the encode allocates; the destination is supplied by the caller.
///     </para>
/// </remarks>
internal sealed class Bc7TexelOperations : BcTexelOperations<Bc7Decoder.Block>, IBcEncoder
{
    private const int BlockBytes = 16;

    private static readonly TexelTypeInfo LinearTexelTypeInfo = new()
    {
        BitsPerTexel = BlockBytes * 8,
        ColorType = TexelColorType.RGB | TexelColorType.Alpha,
        ComponentInfo = new TexelComponentInfo([BlockBytes * 8], [TexelComponentInterpretation.UNorm]),
        AlphaRepresentation = TexelAlphaRepresentation.Unassociated
    };

    private static readonly TexelTypeInfo SrgbTexelTypeInfo = new()
    {
        BitsPerTexel = BlockBytes * 8,
        ColorType = TexelColorType.RGB | TexelColorType.Alpha,
        ComponentInfo = new TexelComponentInfo([BlockBytes * 8], [TexelComponentInterpretation.SRgbUNorm]),
        AlphaRepresentation = TexelAlphaRepresentation.Unassociated
    };

    /// <summary>
    ///     Gets the shared operations instance for BC7_UNorm.
    /// </summary>
    internal static readonly Bc7TexelOperations Instance = new(LinearTexelTypeInfo, "BC7_UNorm");

    /// <summary>
    ///     Gets the shared operations instance for BC7_UNorm_SRGB.
    /// </summary>
    internal static readonly Bc7TexelOperations SrgbInstance = new(SrgbTexelTypeInfo, "BC7_UNorm_SRGB");

    private Bc7TexelOperations(TexelTypeInfo texelTypeInfo, string formatName)
        : base(texelTypeInfo, formatName)
    {
    }

    /// <inheritdoc />
    public void EncodeWithOptions<TPixel>(Image<TPixel> source, Span<byte> destination, BcEncodeOptions options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(source);

        int width = source.Width;
        int height = source.Height;
        long required = Bc7Encoder.GetEncodedByteCount(width, height);

        if (destination.Length < required)
        {
            throw new ArgumentException(
                $"The destination payload contains {destination.Length} bytes but {required} bytes are required for "
                + $"a {width}x{height} {this.FormatName} surface.",
                nameof(destination));
        }

        int length = checked(width * height * 4);
        Configuration configuration = source.Configuration;
        using IMemoryOwner<byte> owner = configuration.MemoryAllocator.Allocate<byte>(length);
        Memory<byte> rgbaMemory = owner.Memory[..length];
        Span<byte> rgba = rgbaMemory.Span;

        PixelOperations<TPixel> pixelOperations = PixelOperations<TPixel>.Instance;
        int rowBytes = width * 4;

        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<TPixel> sourceRow = source.DangerousGetPixelRowMemory(y).Span;
            Span<byte> destinationRow = rgba.Slice(y * rowBytes, rowBytes);
            pixelOperations.ToRgba32Bytes(configuration, sourceRow, destinationRow, width);
        }

        if (options.Bc7HardwareEncoder is { } hardwareEncoder
            && hardwareEncoder.TryEncode(rgba, destination, width, height, options.Bc7Quality))
        {
            return;
        }

        if (options.MaxDegreeOfParallelism != 1)
        {
            int encodedBytes = (int)required;
            using IMemoryOwner<byte> destOwner = configuration.MemoryAllocator.Allocate<byte>(encodedBytes);
            Memory<byte> destMemory = destOwner.Memory[..encodedBytes];
            Bc7Encoder.EncodeParallel(rgbaMemory, destMemory, width, height, options.Bc7Quality,
                options.MaxDegreeOfParallelism);
            destMemory.Span.CopyTo(destination);
            return;
        }

        Bc7Encoder.Encode(rgba, destination, width, height, options.Bc7Quality);
    }

    /// <inheritdoc />
    public override void Encode<TPixel>(Image<TPixel> source, Span<byte> destination)
        => this.EncodeWithOptions(source, destination, BcEncodeOptions.Default);
}
