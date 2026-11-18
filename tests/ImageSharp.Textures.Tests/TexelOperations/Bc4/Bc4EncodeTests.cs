namespace ImageSharp.Textures.Tests.TexelOperations.Bc4;

[Trait("Category", "BcEncode")]
public class Bc4EncodeTests
{
    private static readonly int[] EdgeValues = [0, 255, 128, 64, 32, 16, 200, 100, 10, 250, 90, 170, 20, 240, 60, 130];

    private static readonly int[] InteriorValues =
        [40, 60, 80, 100, 120, 140, 160, 180, 50, 70, 90, 110, 130, 150, 170, 200];

    /// <summary>
    ///     The reference payloads were produced by texconv (DirectXTex) from the same single channel inputs.
    /// </summary>
    [Fact]
    public void ConstantBlockMatchesTheReference()
    {
        using Image<Rgba32> image = BcEncodeTestHelpers.CreateImage(4, 4, (x, y) => new Rgba32(128, 0, 0, 255));

        byte[] payload = BcEncodeTestHelpers.Encode(TexelFormat.Bc4_UNorm, image);

        BcEncodeTestHelpers.AssertPayload("8080000000000000", payload, "BC4 4x4 constant block");
    }

    [Fact]
    public void FourInterpolantBlockMatchesTheReference()
    {
        using Image<Rgba32> image = CreateChannelImage(4, 4, (x, y) => EdgeValues[(y * 4) + x]);

        byte[] payload = BcEncodeTestHelpers.Encode(TexelFormat.Bc4_UNorm, image);

        BcEncodeTestHelpers.AssertPayload("14F9FE0474C88868", payload, "BC4 4x4 four interpolant block");
    }

    [Fact]
    public void SixInterpolantBlockMatchesTheReference()
    {
        using Image<Rgba32> image = CreateChannelImage(4, 4, (x, y) => InteriorValues[(y * 4) + x]);

        byte[] payload = BcEncodeTestHelpers.Encode(TexelFormat.Bc4_UNorm, image);

        BcEncodeTestHelpers.AssertPayload("C82AB95B4EB9CB09", payload, "BC4 4x4 six interpolant block");
    }

    [Fact]
    public void InterpolantModeSetsTheEndpointOrder()
    {
        using Image<Rgba32> boundaryImage = CreateChannelImage(4, 4, (x, y) => EdgeValues[(y * 4) + x]);
        using Image<Rgba32> interiorImage = CreateChannelImage(4, 4, (x, y) => InteriorValues[(y * 4) + x]);

        byte[] boundary = BcEncodeTestHelpers.Encode(TexelFormat.Bc4_UNorm, boundaryImage);
        byte[] interior = BcEncodeTestHelpers.Encode(TexelFormat.Bc4_UNorm, interiorImage);

        string boundaryMessage = "A block containing a boundary value must use the four interpolant mode, but "
                                 + $"endpoint0 was {boundary[0]} and endpoint1 was {boundary[1]}.";
        string interiorMessage = "A block without boundary values must use the six interpolant mode, but "
                                 + $"endpoint0 was {interior[0]} and endpoint1 was {interior[1]}.";

        Assert.True(boundary[0] <= boundary[1], boundaryMessage);
        Assert.True(interior[0] > interior[1], interiorMessage);
    }

    [Fact]
    public void NonMultipleOfFourSurfaceMatchesTheReference()
    {
        using Image<Rgba32> image = CreateChannelImage(6, 6, (x, y) => ((x * 50) + (y * 30)) & 0xFF);

        byte[] payload = BcEncodeTestHelpers.Encode(TexelFormat.Bc4_UNorm, image);

        BcEncodeTestHelpers.AssertPayload(
            "21EBC608AD1A2B36FA0482A0EB797EDFF50D9D42E19D42E1903F615814615814",
            payload,
            "BC4 6x6 surface");
    }

    [Fact]
    public void DecodedChannelsAreRedOnly()
    {
        using Image<Rgba32> image = BcEncodeTestHelpers.CreateImage(
            16,
            16,
            (x, y) => new Rgba32((byte)((x * 16) & 0xFF), (byte)(y * 16), 200, 77));

        byte[] payload = BcEncodeTestHelpers.Encode(TexelFormat.Bc4_UNorm, image);
        using Image<Rgba32> decoded = BcEncodeTestHelpers.Decode(TexelFormat.Bc4_UNorm, payload, 16, 16);

        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                Assert.Equal(0, decoded[x, y].G);
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
            (x, y) => new Rgba32((byte)(x * 4), 0, 0, 255));

        byte[] payload = BcEncodeTestHelpers.Encode(TexelFormat.Bc4_UNorm, image);
        using Image<Rgba32> decoded = BcEncodeTestHelpers.Decode(TexelFormat.Bc4_UNorm, payload, 64, 64);

        BcEncodeTestHelpers.AssertMaxChannelError(
            image,
            decoded,
            8,
            "BC4",
            false,
            false,
            false);
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
            (x, y) => new Rgba32((byte)(x * 7), 0, 0, 255));

        byte[] payload = BcEncodeTestHelpers.Encode(TexelFormat.Bc4_UNorm, image);
        int expected = (width + 3) / 4 * ((height + 3) / 4) * 8;

        Assert.Equal(expected, payload.Length);
        Assert.Equal(expected, BcEncodeTestHelpers.GetEncodedByteCount(TexelFormat.Bc4_UNorm, width, height));
    }

    [Fact]
    public void EncodeIsDeterministic()
    {
        using Image<Rgba32> image = CreateChannelImage(6, 6, (x, y) => ((x * 50) + (y * 30)) & 0xFF);

        byte[] first = BcEncodeTestHelpers.Encode(TexelFormat.Bc4_UNorm, image);
        byte[] second = BcEncodeTestHelpers.Encode(TexelFormat.Bc4_UNorm, image);

        Assert.Equal(first, second);
    }

    private static Image<Rgba32> CreateChannelImage(int width, int height, Func<int, int, int> value)
        => BcEncodeTestHelpers.CreateImage(width, height, (x, y) => new Rgba32((byte)value(x, y), 0, 0, 255));
}
