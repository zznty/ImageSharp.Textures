using ImageSharp.Textures.Texels.Bc1;

namespace ImageSharp.Textures.Tests.Texels.Bc1;

[Trait("Category", "BcDecode")]
public class Bc1DecodeTests
{
    private const string FourColourBlock = "ffff0000e4e4e4e4";
    private const string PunchThroughBlock = "00002108e4e4e4e4";

    // Derivation: color0 = 0xFFFF decodes to (255,255,255) and color1 = 0x0000 to (0,0,0). color0 > color1
    // selects the four colour mode: c2 = (2*255 + 0)/3 = 170 and c3 = (255 + 2*0)/3 = 85, both opaque.
    // The index bytes 0xE4 repeat 0,1,2,3 in every row.
    private const string FourColourExpected =
        "ffffffff000000ffaaaaaaff555555ff" +
        "ffffffff000000ffaaaaaaff555555ff" +
        "ffffffff000000ffaaaaaaff555555ff" +
        "ffffffff000000ffaaaaaaff555555ff";

    // Derivation: color0 = 0x0000 (0,0,0) <= color1 = 0x0821, so the block is in three colour mode. 0x0821
    // expands by bit replication to (8,4,8): r = 1 -> (1<<3)|(1>>2) = 8, g = 1 -> (1<<2)|(1>>4) = 4,
    // b = 1 -> 8. c2 = ((0+8)/2, (0+4)/2, (0+8)/2) = (4,2,4). c3 is the punch-through texel (0,0,0,0).
    private const string PunchThroughExpected =
        "000000ff080408ff040204ff00000000" +
        "000000ff080408ff040204ff00000000" +
        "000000ff080408ff040204ff00000000" +
        "000000ff080408ff040204ff00000000";

    [Fact]
    public void FourColourBlockMatchesTheSpecification()
        => BcDecodeTestHelpers.AssertBlock(Bc1TexelOperations.Instance, FourColourBlock, FourColourExpected);

    [Fact]
    public void ThreeColourBlockProducesPunchThroughAlpha()
        => BcDecodeTestHelpers.AssertBlock(Bc1TexelOperations.Instance, PunchThroughBlock, PunchThroughExpected);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 5)]
    [InlineData(4, 4)]
    [InlineData(8, 8)]
    [InlineData(17, 33)]
    public void PartialEdgeBlocksAreClippedToTheSurface(int width, int height)
    {
        byte[] block = Convert.FromHexString(FourColourBlock);
        byte[] expected = Convert.FromHexString(FourColourExpected);
        byte[] payload = BcDecodeTestHelpers.CreatePayload(width, height, block);

        using Image<Rgba32> image = Bc1TexelOperations.Instance.Decode<Rgba32>(
            payload,
            Configuration.Default,
            width,
            height);

        Assert.Equal(width, image.Width);
        Assert.Equal(height, image.Height);
        BcDecodeTestHelpers.AssertRepeatsBlock(image, expected);
    }

    [Fact]
    public void DecodeIsDeterministic()
    {
        byte[] payload = BcDecodeTestHelpers.CreatePayload(8, 8, Convert.FromHexString(FourColourBlock));

        using Image<Rgba32> first = Bc1TexelOperations.Instance.Decode<Rgba32>(payload, Configuration.Default, 8, 8);
        using Image<Rgba32> second = Bc1TexelOperations.Instance.Decode<Rgba32>(payload, Configuration.Default, 8, 8);

        Assert.Equal(BcDecodeTestHelpers.CopyPixels(first), BcDecodeTestHelpers.CopyPixels(second));
    }

    [Fact]
    public void DescribesPunchThroughRgbTexelType()
    {
        TexelTypeInfo info = Bc1TexelOperations.Instance.GetTexelTypeInfo();

        Assert.Equal(64, info.BitsPerTexel);
        Assert.Equal(TexelColorType.RGB, info.ColorType);
        Assert.Equal(TexelAlphaRepresentation.Unassociated, info.AlphaRepresentation);
        Assert.Equal(1, info.ComponentInfo.ComponentCount);
        Assert.Equal(64, info.ComponentInfo.GetComponentPrecision(0));
        Assert.Equal(TexelComponentInterpretation.UNorm, info.ComponentInfo.GetComponentInterpretation(0));

        TexelTypeInfo srgb = Bc1TexelOperations.SrgbInstance.GetTexelTypeInfo();
        Assert.Equal(TexelComponentInterpretation.SRgbUNorm, srgb.ComponentInfo.GetComponentInterpretation(0));
    }

    [Fact]
    public void EncodedByteCountUsesTheBlockGrid()
    {
        Assert.Equal(8, Bc1TexelOperations.Instance.GetEncodedByteCount(1, 1));
        Assert.Equal(8, Bc1TexelOperations.Instance.GetEncodedByteCount(4, 4));
        Assert.Equal(32, Bc1TexelOperations.Instance.GetEncodedByteCount(5, 5));
        Assert.Equal(360, Bc1TexelOperations.Instance.GetEncodedByteCount(17, 33));
    }

    [Fact]
    public void RegistryResolvesBothBc1Formats()
    {
        Assert.Same(Bc1TexelOperations.Instance, TexelFormatRegistry.GetOperations(TexelFormat.Bc1_UNorm));
        Assert.Same(Bc1TexelOperations.SrgbInstance, TexelFormatRegistry.GetOperations(TexelFormat.Bc1_UnormSrgb));
    }

    [Fact]
    public void TruncatedPayloadIsRejected()
    {
        byte[] payload = new byte[7];

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            Bc1TexelOperations.Instance.Decode<Rgba32>(payload, Configuration.Default, 4, 4));

        Assert.Contains("BC1_UNorm", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(4, 0)]
    public void NonPositiveDimensionsAreRejected(int width, int height)
    {
        byte[] payload = new byte[8];

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Bc1TexelOperations.Instance.Decode<Rgba32>(payload, Configuration.Default, width, height));
    }
}
