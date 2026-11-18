using ImageSharp.Textures.Texels.BcCommon;

namespace ImageSharp.Textures.Texels.Bc5;

/// <summary>
///     Converts the BC5_UNorm texel format between payloads and ImageSharp images.
/// </summary>
/// <remarks>
///     BC5 stores independent red and green channels in a 128-bit block. Decoding fills red and green from the block
///     and sets blue to 0 and alpha to 255, so the format has no alpha representation.
/// </remarks>
internal sealed class Bc5TexelOperations : BcEncodingTexelOperations<Bc5Decoder.Block, Bc5Encoder.Block>
{
    private const int BlockBytes = 16;

    private static readonly TexelTypeInfo TexelTypeInfo = new()
    {
        BitsPerTexel = BlockBytes * 8,
        ColorType = TexelColorType.Red | TexelColorType.Green,
        ComponentInfo = new TexelComponentInfo(
            [BlockBytes * 4, BlockBytes * 4],
            [TexelComponentInterpretation.UNorm, TexelComponentInterpretation.UNorm]),
        AlphaRepresentation = TexelAlphaRepresentation.None
    };

    /// <summary>
    ///     Gets the shared operations instance for BC5_UNorm.
    /// </summary>
    internal static readonly Bc5TexelOperations Instance = new();

    private Bc5TexelOperations()
        : base(TexelTypeInfo, "BC5_UNorm")
    {
    }
}
