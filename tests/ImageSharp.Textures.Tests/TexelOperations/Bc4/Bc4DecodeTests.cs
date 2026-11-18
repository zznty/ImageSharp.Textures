using ImageSharp.Textures.Texels.Bc4;

namespace ImageSharp.Textures.Tests.Texels.Bc4;

[Trait("Category", "BcDecode")]
public class Bc4DecodeTests
{
    // Derivation: red0 = 100 > red1 = 0 selects the eight value palette.
    // p2 = (6*100 + 1*0)/7 = 86, p3 = 71, p4 = 57, p5 = 43, p6 = 29, p7 = 14 (rounded to nearest).
    // The index bytes 0x88 0xC6 0xFA repeat 0,1,2,3,4,5,6,7 twice; green and blue are 0 and alpha is 255.
    private const string EightValueBlock = "640088c6fa88c6fa";

    private const string EightValueExpected =
        "640000ff000000ff560000ff470000ff390000ff2b0000ff1d0000ff0e0000ff" +
        "640000ff000000ff560000ff470000ff390000ff2b0000ff1d0000ff0e0000ff";

    // Derivation: red0 = 0 <= red1 = 255 selects the six value palette.
    // p2 = (4*0 + 1*255)/5 = 51, p3 = 102, p4 = 153, p5 = 204, p6 = 0, p7 = 255.
    // Index 6 therefore decodes to 0 and index 7 to 255 exactly as the specification requires.
    private const string SixValueBlock = "00ff88c6fa88c6fa";

    private const string SixValueExpected =
        "000000ffff0000ff330000ff660000ff990000ffcc0000ff000000ffff0000ff" +
        "000000ffff0000ff330000ff660000ff990000ffcc0000ff000000ffff0000ff";

    [Fact]
    public void EightValuePaletteMatchesTheSpecification()
        => BcDecodeTestHelpers.AssertBlock(Bc4TexelOperations.Instance, EightValueBlock, EightValueExpected);

    [Fact]
    public void SixValuePaletteUsesTheZeroAndFullEndpoints()
        => BcDecodeTestHelpers.AssertBlock(Bc4TexelOperations.Instance, SixValueBlock, SixValueExpected);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 5)]
    [InlineData(4, 4)]
    [InlineData(8, 8)]
    [InlineData(17, 33)]
    public void PartialEdgeBlocksAreClippedToTheSurface(int width, int height)
    {
        byte[] payload = BcDecodeTestHelpers.CreatePayload(width, height, Convert.FromHexString(EightValueBlock));

        using Image<Rgba32> image = Bc4TexelOperations.Instance.Decode<Rgba32>(
            payload,
            Configuration.Default,
            width,
            height);

        Assert.Equal(width, image.Width);
        Assert.Equal(height, image.Height);
        BcDecodeTestHelpers.AssertRepeatsBlock(image, Convert.FromHexString(EightValueExpected));
    }

    [Fact]
    public void DecodeIsDeterministic()
    {
        byte[] payload = BcDecodeTestHelpers.CreatePayload(8, 8, Convert.FromHexString(SixValueBlock));

        using Image<Rgba32> first = Bc4TexelOperations.Instance.Decode<Rgba32>(payload, Configuration.Default, 8, 8);
        using Image<Rgba32> second = Bc4TexelOperations.Instance.Decode<Rgba32>(payload, Configuration.Default, 8, 8);

        Assert.Equal(BcDecodeTestHelpers.CopyPixels(first), BcDecodeTestHelpers.CopyPixels(second));
    }

    [Fact]
    public void DescribesRedOnlyTexelType()
    {
        TexelTypeInfo info = Bc4TexelOperations.Instance.GetTexelTypeInfo();

        Assert.Equal(64, info.BitsPerTexel);
        Assert.Equal(TexelColorType.Red, info.ColorType);
        Assert.Equal(TexelAlphaRepresentation.None, info.AlphaRepresentation);
        Assert.Equal(1, info.ComponentInfo.ComponentCount);
        Assert.Equal(TexelComponentInterpretation.UNorm, info.ComponentInfo.GetComponentInterpretation(0));
    }

    [Fact]
    public void EncodedByteCountUsesTheBlockGrid()
    {
        Assert.Equal(8, Bc4TexelOperations.Instance.GetEncodedByteCount(1, 1));
        Assert.Equal(8, Bc4TexelOperations.Instance.GetEncodedByteCount(4, 4));
        Assert.Equal(32, Bc4TexelOperations.Instance.GetEncodedByteCount(5, 5));
        Assert.Equal(360, Bc4TexelOperations.Instance.GetEncodedByteCount(17, 33));
    }

    [Fact]
    public void RegistryResolvesBc4()
        => Assert.Same(Bc4TexelOperations.Instance, TexelFormatRegistry.GetOperations(TexelFormat.Bc4_UNorm));

    [Fact]
    public void TruncatedPayloadIsRejected()
    {
        byte[] payload = new byte[7];

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            Bc4TexelOperations.Instance.Decode<Rgba32>(payload, Configuration.Default, 4, 4));

        Assert.Contains("BC4_UNorm", exception.Message, StringComparison.Ordinal);
    }
}
