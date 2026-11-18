using ImageSharp.Textures.Texels.Bc7;

namespace ImageSharp.Textures.Tests.Texels.Bc7;

[Trait("Category", "BcDecode")]
public class Bc7DecodeTests
{
    // Mode 0: 3 subsets, shape 0 maps texels to regions [0,0,1,1,0,0,1,1,0,2,2,1,2,2,2,2] with anchors 0/3/15.
    // RGB precision 4 plus one P-bit per endpoint, all P-bits 0, so the 5-bit values 2 and 4 unquantise to
    // 32|0 = 16 and 32|1 = 33. Region 0 endpoints are (16,0,0)->(33,0,0), region 1 (0,16,0)->(0,33,0) and
    // region 2 (0,0,16)->(0,0,33); alpha precision is 0, so alpha is 255. Texel 5 uses index 3 (weight 27)
    // and decodes to red 23; texel 10 uses index 7 (weight 64) and decodes to blue 33.
    private const string Mode0Block = "21040000200400002004000003802300";

    private const string Mode0Expected =
        "100000ff100000ff001000ff001000ff" +
        "100000ff170000ff001000ff001000ff" +
        "100000ff000010ff000021ff001000ff" +
        "000012ff000010ff000010ff000010ff";

    // Mode 1: 2 subsets, shape 0 (left half / right half) with anchors 0 and 15. RGB precision 6 plus a
    // shared P-bit per subset, all zero, so the doubled values 2,4,6,8 unquantise to 4,8,12,16.
    // Texel 1 uses index 5 (weight 46) -> 7; texel 6 uses index 7 (weight 64) -> 16; anchor texel 15 uses
    // index 3 (weight 27) -> (12*37 + 16*27 + 32) >> 6 = 14.
    private const string Mode1Block = "028130108130108130105001380000c0";

    private const string Mode1Expected =
        "040404ff070707ff0d0d0dff0c0c0cff" +
        "040404ff040404ff101010ff0c0c0cff" +
        "040404ff040404ff0c0c0cff0c0c0cff" +
        "040404ff040404ff0c0c0cff0e0e0eff";

    // Mode 2: 3 subsets, RGB precision 5 (no P-bits), 2-bit indices. Endpoints unquantise to 8, 16, 8, 16,
    // 8, 16 for the three subsets. Anchor texel 0 uses a 1-bit index of 1 (weight 21) -> 11, texel 13 uses
    // index 2 (weight 43) -> 13.
    private const string Mode2Block = "04820000000082000000008218000010";

    private const string Mode2Expected =
        "0b0000ff0b0000ff000800ff000800ff" +
        "080000ff080000ff000800ff000800ff" +
        "080000ff000008ff000008ff000800ff" +
        "000008ff00000dff000008ff000008ff";

    // Mode 3: 2 subsets, RGB precision 7 plus unique P-bits, 2-bit indices. Endpoints (10,20,30,40) double to
    // 20,40,60,80. Texel 3 uses index 3 (weight 64) -> 80; texel 7 uses index 1 (weight 21) ->
    // (60*43 + 80*21 + 32) >> 6 = 67.
    private const string Mode3Block = "0828281e9482e24129281e1480810000";

    private const string Mode3Expected =
        "141414ff141414ff3c3c3cff505050ff" +
        "141414ff141414ff3c3c3cff434343ff" +
        "141414ff141414ff3c3c3cff3c3c3cff" +
        "141414ff141414ff3c3c3cff3c3c3cff";

    // Mode 4: one subset with rotation 1 (red <-> alpha) and index mode 1, so the colour channels are taken
    // from the second index set and alpha from the first. RGB precision 5 unquantises 4,5,6 to 33,41,49 and
    // 8,9,10 to 66,74,82; alpha precision 6 unquantises 10 and 20 to 40 and 80|1 = 81. Texel 1 has colour
    // index 7 -> (66,74,82) and alpha index 0 -> 40, then the rotation swaps red and alpha.
    private const string Mode4Block = "b0049564944221000000380000000000";

    private const string Mode4Expected =
        "28293121284a52423529312128293121" +
        "28293121282931212829312128293121" +
        "28293121282931212829312128293121" +
        "28293121282931212829312128293121";

    // Mode 5: one subset with rotation 2 (green <-> alpha) and two independent index sets. RGB precision 7
    // doubles 10,22,30 to 20,44,60 and 50,60,70 to 100,120,140; alpha precision 8 keeps 40 and 80.
    // Texel 1 uses colour index 2 (weight 43) and alpha index 3 -> (74,95,114) with alpha 80, then the
    // rotation swaps green and alpha.
    private const string Mode5Block = "a00a9985e731a240110000001c000000";

    private const string Mode5Expected =
        "14283c2c4a50725f14353c2c14283c2c" +
        "14283c2c14283c2c14283c2c14283c2c" +
        "14283c2c14283c2c14283c2c14283c2c" +
        "14283c2c14283c2c14283c2c14283c2c";

    // Mode 6: one subset, RGBA precision 7 plus P-bits, 4-bit indices. Endpoints (10,20,30,40) and
    // (50,60,70,120) double to (20,40,60,80) and (100,120,140,240). Texel 5 uses index 15 -> the second
    // endpoint; texel 6 uses index 8 -> (63,83,103,165); texel 7 uses index 1 -> (25,45,65,90).
    private const string Mode6Block = "40858cc2f31851780000f01800000000";

    private const string Mode6Expected =
        "14283c5014283c5014283c5014283c50" +
        "14283c5064788cf03f5367a5192d415a" +
        "14283c5014283c5014283c5014283c50" +
        "14283c5014283c5014283c5014283c50";

    // Mode 7: 2 subsets, RGBA precision 5 plus P-bits, 2-bit indices. Endpoints 1..4 double to 8,16,24,32.
    // Anchor texel 0 uses a 1-bit index of 1 (weight 21) -> 11; texel 1 uses index 3 -> 16; texel 2 uses
    // index 1 (weight 21) -> (24*43 + 32*21 + 32) >> 6 = 27.
    private const string Mode7Block = "8040108304314810830431083c000000";

    private const string Mode7Expected =
        "0b0b0b0b101010101b1b1b1b18181818" +
        "08080808080808081818181818181818" +
        "08080808080808081818181818181818" +
        "08080808080808081818181818181818";

    // Reserved mode: sixteen zero bytes are eight zero mode bits, which is mode 8. The frozen model contract
    // decodes the reserved mode to opaque black.
    private const string ReservedBlock = "00000000000000000000000000000000";

    private const string ReservedExpected =
        "000000ff000000ff000000ff000000ff" +
        "000000ff000000ff000000ff000000ff" +
        "000000ff000000ff000000ff000000ff" +
        "000000ff000000ff000000ff000000ff";

    [Fact]
    public void Mode0ThreeSubsetsMatchTheSpecification()
        => BcDecodeTestHelpers.AssertBlock(Bc7TexelOperations.Instance, Mode0Block, Mode0Expected);

    [Fact]
    public void Mode1TwoSubsetsMatchTheSpecification()
        => BcDecodeTestHelpers.AssertBlock(Bc7TexelOperations.Instance, Mode1Block, Mode1Expected);

    [Fact]
    public void Mode2ThreeSubsetsMatchTheSpecification()
        => BcDecodeTestHelpers.AssertBlock(Bc7TexelOperations.Instance, Mode2Block, Mode2Expected);

    [Fact]
    public void Mode3TwoSubsetsMatchTheSpecification()
        => BcDecodeTestHelpers.AssertBlock(Bc7TexelOperations.Instance, Mode3Block, Mode3Expected);

    [Fact]
    public void Mode4RotationAndIndexModeMatchTheSpecification()
        => BcDecodeTestHelpers.AssertBlock(Bc7TexelOperations.Instance, Mode4Block, Mode4Expected);

    [Fact]
    public void Mode5RotationAndAlphaIndicesMatchTheSpecification()
        => BcDecodeTestHelpers.AssertBlock(Bc7TexelOperations.Instance, Mode5Block, Mode5Expected);

    [Fact]
    public void Mode6RgbaMatchesTheSpecification()
        => BcDecodeTestHelpers.AssertBlock(Bc7TexelOperations.Instance, Mode6Block, Mode6Expected);

    [Fact]
    public void Mode7TwoSubsetsMatchTheSpecification()
        => BcDecodeTestHelpers.AssertBlock(Bc7TexelOperations.Instance, Mode7Block, Mode7Expected);

    [Fact]
    public void ReservedModeDecodesToOpaqueBlack()
        => BcDecodeTestHelpers.AssertBlock(Bc7TexelOperations.Instance, ReservedBlock, ReservedExpected);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 5)]
    [InlineData(4, 4)]
    [InlineData(8, 8)]
    [InlineData(17, 33)]
    public void PartialEdgeBlocksAreClippedToTheSurface(int width, int height)
    {
        byte[] payload = BcDecodeTestHelpers.CreatePayload(width, height, Convert.FromHexString(Mode6Block));

        using Image<Rgba32> image = Bc7TexelOperations.Instance.Decode<Rgba32>(
            payload,
            Configuration.Default,
            width,
            height);

        Assert.Equal(width, image.Width);
        Assert.Equal(height, image.Height);
        BcDecodeTestHelpers.AssertRepeatsBlock(image, Convert.FromHexString(Mode6Expected));
    }

    [Fact]
    public void DecodeIsDeterministic()
    {
        byte[] payload = BcDecodeTestHelpers.CreatePayload(8, 8, Convert.FromHexString(Mode6Block));

        using Image<Rgba32> first = Bc7TexelOperations.Instance.Decode<Rgba32>(payload, Configuration.Default, 8, 8);
        using Image<Rgba32> second = Bc7TexelOperations.Instance.Decode<Rgba32>(payload, Configuration.Default, 8, 8);

        Assert.Equal(BcDecodeTestHelpers.CopyPixels(first), BcDecodeTestHelpers.CopyPixels(second));
    }

    [Fact]
    public void DescribesRgbaTexelType()
    {
        TexelTypeInfo info = Bc7TexelOperations.Instance.GetTexelTypeInfo();

        Assert.Equal(128, info.BitsPerTexel);
        Assert.Equal(TexelColorType.RGB | TexelColorType.Alpha, info.ColorType);
        Assert.Equal(TexelAlphaRepresentation.Unassociated, info.AlphaRepresentation);
        Assert.Equal(1, info.ComponentInfo.ComponentCount);
        Assert.Equal(TexelComponentInterpretation.UNorm, info.ComponentInfo.GetComponentInterpretation(0));

        TexelTypeInfo srgb = Bc7TexelOperations.SrgbInstance.GetTexelTypeInfo();
        Assert.Equal(TexelComponentInterpretation.SRgbUNorm, srgb.ComponentInfo.GetComponentInterpretation(0));
    }

    [Fact]
    public void EncodedByteCountUsesTheBlockGrid()
    {
        Assert.Equal(16, Bc7TexelOperations.Instance.GetEncodedByteCount(1, 1));
        Assert.Equal(16, Bc7TexelOperations.Instance.GetEncodedByteCount(4, 4));
        Assert.Equal(64, Bc7TexelOperations.Instance.GetEncodedByteCount(5, 5));
        Assert.Equal(720, Bc7TexelOperations.Instance.GetEncodedByteCount(17, 33));
    }

    [Fact]
    public void RegistryResolvesBothBc7Formats()
    {
        Assert.Same(Bc7TexelOperations.Instance, TexelFormatRegistry.GetOperations(TexelFormat.Bc7_UNorm));
        Assert.Same(Bc7TexelOperations.SrgbInstance, TexelFormatRegistry.GetOperations(TexelFormat.Bc7_UnormSrgb));
    }

    [Fact]
    public void TruncatedPayloadIsRejected()
    {
        byte[] payload = new byte[15];

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            Bc7TexelOperations.Instance.Decode<Rgba32>(payload, Configuration.Default, 4, 4));

        Assert.Contains("BC7_UNorm", exception.Message, StringComparison.Ordinal);
    }
}
