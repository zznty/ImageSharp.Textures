namespace ImageSharp.Textures.Tests.TestUtilities;

/// <summary>
///     Mutable format metadata used to observe cloning behaviour.
/// </summary>
internal sealed class TestTextureFormatMetadata : ITextureFormatMetadata<TestTextureFormatMetadata>
{
    public int Value { get; set; }

    public static TestTextureFormatMetadata FromTextureConnectingMetadata(TextureConnectingMetadata metadata) => new();

    public TextureConnectingMetadata ToTextureConnectingMetadata()
        => new()
        {
            TexelTypeInfo = new TexelTypeInfo
            {
                BitsPerTexel = 8,
                ColorType = TexelColorType.Red,
                ComponentInfo = new TexelComponentInfo([8], [TexelComponentInterpretation.UNorm]),
                AlphaRepresentation = TexelAlphaRepresentation.None
            }
        };

    public TestTextureFormatMetadata DeepClone() => new() { Value = this.Value };

    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();
}
