using ImageSharp.Textures.Texels.BcCommon;

namespace ImageSharp.Textures.Texels.Bc4;

/// <summary>
///     Converts the BC4_UNorm texel format between payloads and ImageSharp images.
/// </summary>
/// <remarks>
///     BC4 stores a single red channel in a 64-bit block. Decoding fills red from the block and sets green and blue
///     to 0 and alpha to 255, so the format has no alpha representation.
/// </remarks>
internal sealed class Bc4TexelOperations : BcEncodingTexelOperations<Bc4Decoder.Block, Bc4Encoder.Block>
{
    private const int BlockBytes = 8;

    private static readonly TexelTypeInfo TexelTypeInfo = new()
    {
        BitsPerTexel = BlockBytes * 8,
        ColorType = TexelColorType.Red,
        ComponentInfo = new TexelComponentInfo([BlockBytes * 8], [TexelComponentInterpretation.UNorm]),
        AlphaRepresentation = TexelAlphaRepresentation.None
    };

    /// <summary>
    ///     Gets the shared operations instance for BC4_UNorm.
    /// </summary>
    internal static readonly Bc4TexelOperations Instance = new();

    private Bc4TexelOperations()
        : base(TexelTypeInfo, "BC4_UNorm")
    {
    }
}
