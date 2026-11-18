namespace ImageSharp.Textures.Tests.TexelOperations.Bc5;

[Trait("Category", "BcEncode")]
public class Bc5EncodeTests
{
    private const string MixedBlockPayload = "14F9FE0474C88868C82AB95B4EB9CB09";

    private const string MixedSurfacePayload = "21EBC608AD1A2B3612A286248D23CB36"
                                               + "FA0482A0EB797EDFCC5079EEBA1C2708"
                                               + "F50D9D42E19D42E1E98D79590A79590A"
                                               + "903F61581461581413E649721C49721C";

    /// <summary>
    ///     The reference payloads were produced by texconv (DirectXTex) from the same inputs.
    /// </summary>
    [Fact]
    public void RedGreenBlockMatchesTheReference()
    {
        using Image<Rgba32> image = CreateMixedChannelImage(4, 4);

        byte[] payload = BcEncodeTestHelpers.Encode(TexelFormat.Bc5_UNorm, image);

        BcEncodeTestHelpers.AssertPayload(MixedBlockPayload, payload, "BC5 4x4 block");
    }

    [Fact]
    public void NonMultipleOfFourSurfaceMatchesTheReference()
    {
        using Image<Rgba32> image = BcEncodeTestHelpers.CreateImage(
            6,
            6,
            (x, y) => new Rgba32(
                (byte)(((x * 50) + (y * 30)) & 0xFF),
                (byte)(((x * 20) + (y * 35)) & 0xFF),
                0,
                255));

        byte[] payload = BcEncodeTestHelpers.Encode(TexelFormat.Bc5_UNorm, image);

        BcEncodeTestHelpers.AssertPayload(MixedSurfacePayload, payload, "BC5 6x6 surface");
    }

    [Fact]
    public void EveryBlockIsTwoIndependentBc4Passes()
    {
        using Image<Rgba32> image = BcEncodeTestHelpers.CreateImage(
            4,
            4,
            (x, y) => new Rgba32((byte)(x * 60), (byte)(y * 60), (byte)((x + y) * 30), 255));
        using Image<Rgba32> redOnly = BcEncodeTestHelpers.CreateImage(
            4,
            4,
            (x, y) => new Rgba32((byte)(x * 60), 0, 0, 255));

        // BC4 encodes the red channel, so the expected green pass is a BC4 encode of an image that carries
        // the green values in red.
        using Image<Rgba32> greenAsRed = BcEncodeTestHelpers.CreateImage(
            4,
            4,
            (x, y) => new Rgba32((byte)(y * 60), 0, 0, 255));

        byte[] payload = BcEncodeTestHelpers.Encode(TexelFormat.Bc5_UNorm, image);
        byte[] red = BcEncodeTestHelpers.Encode(TexelFormat.Bc4_UNorm, redOnly);
        byte[] green = BcEncodeTestHelpers.Encode(TexelFormat.Bc4_UNorm, greenAsRed);

        Assert.Equal(red, payload[..8]);
        Assert.Equal(green, payload[8..]);
    }

    [Fact]
    public void DecodedChannelsAreRedAndGreenOnly()
    {
        using Image<Rgba32> image = BcEncodeTestHelpers.CreateImage(
            16,
            16,
            (x, y) => new Rgba32((byte)(x * 16), (byte)(y * 16), 255, 9));

        byte[] payload = BcEncodeTestHelpers.Encode(TexelFormat.Bc5_UNorm, image);
        using Image<Rgba32> decoded = BcEncodeTestHelpers.Decode(TexelFormat.Bc5_UNorm, payload, 16, 16);

        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                Assert.Equal(0, decoded[x, y].B);
                Assert.Equal(255, decoded[x, y].A);
            }
        }
    }

    [Fact]
    public void SmoothGradientStaysWithinEightLsb()
    {
        using Image<Rgba32> image = BcEncodeTestHelpers.CreateImage(
            64,
            64,
            (x, y) => new Rgba32((byte)(x * 4), (byte)(y * 4), 0, 255));

        byte[] payload = BcEncodeTestHelpers.Encode(TexelFormat.Bc5_UNorm, image);
        using Image<Rgba32> decoded = BcEncodeTestHelpers.Decode(TexelFormat.Bc5_UNorm, payload, 64, 64);

        BcEncodeTestHelpers.AssertMaxChannelError(
            image,
            decoded,
            8,
            "BC5",
            compareBlue: false,
            compareAlpha: false);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 5)]
    [InlineData(4, 4)]
    [InlineData(17, 33)]
    [InlineData(64, 64)]
    public void EncodedByteCountMatchesThePayloadLength(int width, int height)
    {
        using Image<Rgba32> image = BcEncodeTestHelpers.CreateImage(
            width,
            height,
            (x, y) => new Rgba32((byte)(x * 7), (byte)(y * 11), 0, 255));

        byte[] payload = BcEncodeTestHelpers.Encode(TexelFormat.Bc5_UNorm, image);
        int expected = (width + 3) / 4 * ((height + 3) / 4) * 16;

        Assert.Equal(expected, payload.Length);
        Assert.Equal(expected, BcEncodeTestHelpers.GetEncodedByteCount(TexelFormat.Bc5_UNorm, width, height));
    }

    [Fact]
    public void EncodeIsDeterministic()
    {
        using Image<Rgba32> image = BcEncodeTestHelpers.CreateImage(
            6,
            6,
            (x, y) => new Rgba32((byte)(x * 40), (byte)(y * 40), 0, 255));

        byte[] first = BcEncodeTestHelpers.Encode(TexelFormat.Bc5_UNorm, image);
        byte[] second = BcEncodeTestHelpers.Encode(TexelFormat.Bc5_UNorm, image);

        Assert.Equal(first, second);
    }

    /// <summary>
    ///     Builds the 4x4 surface whose red channel is the BC4 boundary set and whose green channel is the BC4
    ///     interior set, so both BC4 interpolant modes are exercised in one block.
    /// </summary>
    private static Image<Rgba32> CreateMixedChannelImage(int width, int height)
    {
        int[] red = [0, 255, 128, 64, 32, 16, 200, 100, 10, 250, 90, 170, 20, 240, 60, 130];
        int[] green = [40, 60, 80, 100, 120, 140, 160, 180, 50, 70, 90, 110, 130, 150, 170, 200];

        return BcEncodeTestHelpers.CreateImage(
            width,
            height,
            (x, y) => new Rgba32((byte)red[(y * 4) + x], (byte)green[(y * 4) + x], 0, 255));
    }
}
