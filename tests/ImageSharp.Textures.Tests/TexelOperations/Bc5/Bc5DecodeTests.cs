using ImageSharp.Textures.Texels.Bc5;

namespace ImageSharp.Textures.Tests.Texels.Bc5;

[Trait("Category", "BcDecode")]
public class Bc5DecodeTests
{
    // Derivation: the red half is the BC4 eight value case (red0 = 100, red1 = 0) with indices 0..7 twice;
    // the green half is the BC4 six value case (green0 = 0, green1 = 255) with indices 4..7,0..3 so the two
    // channels provably interpolate independently. Blue is 0 and alpha is 255.
    private const string Block = "640088c6fa88c6fa00ffac8f68ac8f68";

    private const string Expected =
        "649900ff00cc00ff560000ff47ff00ff390000ff2bff00ff1d3300ff0e6600ff" +
        "649900ff00cc00ff560000ff47ff00ff390000ff2bff00ff1d3300ff0e6600ff";

    [Fact]
    public void TwoIndependentChannelsMatchTheSpecification()
        => BcDecodeTestHelpers.AssertBlock(Bc5TexelOperations.Instance, Block, Expected);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 5)]
    [InlineData(4, 4)]
    [InlineData(8, 8)]
    [InlineData(17, 33)]
    public void PartialEdgeBlocksAreClippedToTheSurface(int width, int height)
    {
        byte[] payload = BcDecodeTestHelpers.CreatePayload(width, height, Convert.FromHexString(Block));

        using Image<Rgba32> image = Bc5TexelOperations.Instance.Decode<Rgba32>(
            payload,
            Configuration.Default,
            width,
            height);

        Assert.Equal(width, image.Width);
        Assert.Equal(height, image.Height);
        BcDecodeTestHelpers.AssertRepeatsBlock(image, Convert.FromHexString(Expected));
    }

    [Fact]
    public void DecodeIsDeterministic()
    {
        byte[] payload = BcDecodeTestHelpers.CreatePayload(8, 8, Convert.FromHexString(Block));

        using Image<Rgba32> first = Bc5TexelOperations.Instance.Decode<Rgba32>(payload, Configuration.Default, 8, 8);
        using Image<Rgba32> second = Bc5TexelOperations.Instance.Decode<Rgba32>(payload, Configuration.Default, 8, 8);

        Assert.Equal(BcDecodeTestHelpers.CopyPixels(first), BcDecodeTestHelpers.CopyPixels(second));
    }

    [Fact]
    public void DescribesRedGreenTexelType()
    {
        TexelTypeInfo info = Bc5TexelOperations.Instance.GetTexelTypeInfo();

        Assert.Equal(128, info.BitsPerTexel);
        Assert.Equal(TexelColorType.Red | TexelColorType.Green, info.ColorType);
        Assert.Equal(TexelAlphaRepresentation.None, info.AlphaRepresentation);
        Assert.Equal(2, info.ComponentInfo.ComponentCount);
        Assert.Equal(TexelComponentInterpretation.UNorm, info.ComponentInfo.GetComponentInterpretation(0));
        Assert.Equal(TexelComponentInterpretation.UNorm, info.ComponentInfo.GetComponentInterpretation(1));
    }

    [Fact]
    public void EncodedByteCountUsesTheBlockGrid()
    {
        Assert.Equal(16, Bc5TexelOperations.Instance.GetEncodedByteCount(1, 1));
        Assert.Equal(16, Bc5TexelOperations.Instance.GetEncodedByteCount(4, 4));
        Assert.Equal(64, Bc5TexelOperations.Instance.GetEncodedByteCount(5, 5));
        Assert.Equal(720, Bc5TexelOperations.Instance.GetEncodedByteCount(17, 33));
    }

    [Fact]
    public void RegistryResolvesBc5()
        => Assert.Same(Bc5TexelOperations.Instance, TexelFormatRegistry.GetOperations(TexelFormat.Bc5_UNorm));

    [Fact]
    public void TruncatedPayloadIsRejected()
    {
        byte[] payload = new byte[15];

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            Bc5TexelOperations.Instance.Decode<Rgba32>(payload, Configuration.Default, 4, 4));

        Assert.Contains("BC5_UNorm", exception.Message, StringComparison.Ordinal);
    }
}
