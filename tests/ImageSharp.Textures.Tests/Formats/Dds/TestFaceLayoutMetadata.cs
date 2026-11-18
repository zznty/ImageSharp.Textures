namespace ImageSharp.Textures.Tests.Formats.Dds;

/// <summary>
///     Mutable face layout metadata used to prove that the DDS encoder honours a layout declared by another
///     container.
/// </summary>
internal sealed class TestFaceLayoutMetadata : ITextureFormatMetadata<TestFaceLayoutMetadata>,
    ITextureFaceLayoutMetadata
{
    /// <inheritdoc />
    public int FaceCount { get; set; } = 1;

    /// <inheritdoc />
    public static TestFaceLayoutMetadata FromTextureConnectingMetadata(TextureConnectingMetadata metadata) => new();

    /// <inheritdoc />
    public TextureConnectingMetadata ToTextureConnectingMetadata()
        => new()
        {
            TexelTypeInfo = new TexelTypeInfo
            {
                BitsPerTexel = 128,
                ColorType = TexelColorType.RGB | TexelColorType.Alpha,
                ComponentInfo = new TexelComponentInfo([128], [TexelComponentInterpretation.UNorm]),
                AlphaRepresentation = TexelAlphaRepresentation.Unassociated
            }
        };

    /// <inheritdoc />
    public TestFaceLayoutMetadata DeepClone() => new() { FaceCount = this.FaceCount };

    /// <inheritdoc />
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();
}
