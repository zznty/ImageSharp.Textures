using ImageSharp.Textures.Texels.BcCommon;

namespace ImageSharp.Textures.Texels.Bc1;

/// <summary>
///     Converts the BC1_UNorm texel formats between payloads and ImageSharp images.
/// </summary>
/// <remarks>
///     BC1 stores RGB colour in a 64-bit block with a one-bit punch-through alpha. Alpha is therefore unassociated:
///     the punch-through texel decodes to alpha 0 and every other texel to alpha 255. The sRGB variant uses the
///     same arithmetic and bytes; only the component interpretation of the texel type differs.
/// </remarks>
internal sealed class Bc1TexelOperations : BcEncodingTexelOperations<Bc1Decoder.Block, Bc1Encoder.Block>
{
    private const int BlockBytes = 8;

    private static readonly TexelTypeInfo LinearTexelTypeInfo = new()
    {
        BitsPerTexel = BlockBytes * 8,
        ColorType = TexelColorType.RGB,
        ComponentInfo = new TexelComponentInfo([BlockBytes * 8], [TexelComponentInterpretation.UNorm]),
        AlphaRepresentation = TexelAlphaRepresentation.Unassociated
    };

    private static readonly TexelTypeInfo SrgbTexelTypeInfo = new()
    {
        BitsPerTexel = BlockBytes * 8,
        ColorType = TexelColorType.RGB,
        ComponentInfo = new TexelComponentInfo([BlockBytes * 8], [TexelComponentInterpretation.SRgbUNorm]),
        AlphaRepresentation = TexelAlphaRepresentation.Unassociated
    };

    /// <summary>
    ///     Gets the shared operations instance for BC1_UNorm.
    /// </summary>
    internal static readonly Bc1TexelOperations Instance = new(LinearTexelTypeInfo, "BC1_UNorm");

    /// <summary>
    ///     Gets the shared operations instance for BC1_UNorm_SRGB.
    /// </summary>
    internal static readonly Bc1TexelOperations SrgbInstance = new(SrgbTexelTypeInfo, "BC1_UNorm_SRGB");

    private Bc1TexelOperations(TexelTypeInfo texelTypeInfo, string formatName)
        : base(texelTypeInfo, formatName)
    {
    }
}
