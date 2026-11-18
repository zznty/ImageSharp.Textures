using ImageSharp.Textures.Tests.Texels;
using ImageSharp.Textures.Texels.BcCommon;

namespace ImageSharp.Textures.Tests.TexelOperations.Bc7;

[Trait("Category", "BcEncode")]
public class Bc7EncodeTests
{
    /// <summary>
    ///     The reference payloads and decoded bytes below were produced by DirectXTex (Texconv-Custom-DLL v0.6.0,
    ///     DirectXTex @ 232bbf2e) for the same inputs: the block payload is texconv's <c>-bc q</c> (mode 6) output
    ///     and the decoded bytes are its <c>R8G8B8A8_UNORM</c> dump of that payload. They pin the mode 6 search,
    ///     the endpoint quantization and the block bit layout.
    /// </summary>
    [Fact]
    public void Mode6GoldenBlockMatchesTheReference()
    {
        // A horizontal grey ramp: (60x, 60x, 60x, 255).
        using Image<Rgba32> image = BcEncodeTestHelpers.CreateImage(
            4,
            4,
            (x, y) => new Rgba32((byte)(x * 60), (byte)(x * 60), (byte)(x * 60)));

        byte[] payload = BcEncodeTestHelpers.Encode(
            TexelFormat.Bc7_UNorm,
            image,
            new BcEncodeOptions(0.5f, Bc7EncodingQuality.Quick));

        BcEncodeTestHelpers.AssertPayload(
            "408016A00568FF7F50FA50FA50FA50FA",
            payload,
            "BC7 4x4 mode 6 grey ramp block");

        using Image<Rgba32> decoded = BcEncodeTestHelpers.Decode(TexelFormat.Bc7_UNorm, payload, 4, 4);

        Assert.Equal(
            "000000FE3B3B3BFE797979FEB4B4B4FE000000FE3B3B3BFE797979FEB4B4B4FE"
            + "000000FE3B3B3BFE797979FEB4B4B4FE000000FE3B3B3BFE797979FEB4B4B4FE",
            Convert.ToHexString(BcDecodeTestHelpers.CopyPixels(decoded)));
    }

    [Fact]
    public void AlphaBlockMatchesTheReference()
    {
        // A constant colour with a horizontal alpha ramp: (200, 100, 50, 60x).
        using Image<Rgba32> image = BcEncodeTestHelpers.CreateImage(
            4,
            4,
            (x, y) => new Rgba32(200, 100, 50, (byte)(x * 60)));

        byte[] payload = BcEncodeTestHelpers.Encode(
            TexelFormat.Bc7_UNorm,
            image,
            new BcEncodeOptions(0.5f, Bc7EncodingQuality.Quick));

        BcEncodeTestHelpers.AssertPayload(
            "40325926CB64005A50FA50FA50FA50FA",
            payload,
            "BC7 4x4 mode 6 alpha ramp block");

        using Image<Rgba32> decoded = BcEncodeTestHelpers.Decode(TexelFormat.Bc7_UNorm, payload, 4, 4);

        string row = "C8643200C864323BC8643279C86432B4";
        Assert.Equal(
            row + row + row + row,
            Convert.ToHexString(BcDecodeTestHelpers.CopyPixels(decoded)));
    }

    [Fact]
    public void OpaqueBlockDecodesFullyOpaque()
    {
        using Image<Rgba32> image = BcEncodeTestHelpers.CreateImage(8, 8, (x, y) => new Rgba32(30, 160, 220));

        byte[] payload = BcEncodeTestHelpers.Encode(TexelFormat.Bc7_UNorm, image);

        using Image<Rgba32> decoded = BcEncodeTestHelpers.Decode(TexelFormat.Bc7_UNorm, payload, 8, 8);

        for (int y = 0; y < decoded.Height; y++)
        {
            for (int x = 0; x < decoded.Width; x++)
            {
                Assert.Equal(255, decoded[x, y].A);
            }
        }
    }

    [Fact]
    public void AlphaBlockKeepsItsAlphaRamp()
    {
        using Image<Rgba32> image = BcEncodeTestHelpers.CreateImage(
            16,
            16,
            (x, y) => new Rgba32(90, 120, 200, (byte)(x * 16)));

        byte[] payload = BcEncodeTestHelpers.Encode(TexelFormat.Bc7_UNorm, image);

        using Image<Rgba32> decoded = BcEncodeTestHelpers.Decode(TexelFormat.Bc7_UNorm, payload, 16, 16);

        BcEncodeTestHelpers.AssertMaxChannelError(image, decoded, 8, "BC7_UNorm alpha ramp");

        int first = decoded[0, 0].A;
        int last = decoded[15, 0].A;
        Assert.True(last - first > 200, $"the alpha ramp collapsed: {first}..{last}.");
    }

    [Fact]
    public void EncodingIsDeterministic()
    {
        using Image<Rgba32> image = BcEncodeTestHelpers.CreateImage(
            32,
            32,
            (x, y) => new Rgba32((byte)(x * 8), (byte)(y * 8), (byte)(255 - (x * 8)), (byte)(y * 8)));

        byte[] first = BcEncodeTestHelpers.Encode(TexelFormat.Bc7_UNorm, image);
        byte[] second = BcEncodeTestHelpers.Encode(TexelFormat.Bc7_UNorm, image);

        Assert.Equal(first, second);
    }

    [Fact]
    public void EncodedByteCountMatchesTheTexelOperations()
    {
        foreach ((int width, int height) in new[] { (1, 1), (4, 4), (17, 33), (64, 64) })
        {
            int expected = ((width + 3) >> 2) * ((height + 3) >> 2) * 16;

            Assert.Equal(expected, BcEncodeTestHelpers.GetEncodedByteCount(TexelFormat.Bc7_UNorm, width, height));

            using Image<Rgba32> image =
                BcEncodeTestHelpers.CreateImage(width, height, (x, y) => new Rgba32(10, 20, 30));
            byte[] payload = BcEncodeTestHelpers.Encode(TexelFormat.Bc7_UNorm, image);
            Assert.Equal(expected, payload.Length);
        }
    }

    [Fact]
    public void SmoothGradientRoundTripsWithinFourLsb()
    {
        using Image<Rgba32> image = BcEncodeTestHelpers.CreateImage(
            64,
            64,
            (x, y) => new Rgba32((byte)(x * 4), (byte)(x * 4), (byte)(x * 4)));

        byte[] payload = BcEncodeTestHelpers.Encode(TexelFormat.Bc7_UNorm, image);

        using Image<Rgba32> decoded = BcEncodeTestHelpers.Decode(TexelFormat.Bc7_UNorm, payload, 64, 64);

        BcEncodeTestHelpers.AssertMaxChannelError(image, decoded, 4, "BC7_UNorm smooth gradient");
    }

    [Fact]
    public void DefaultQualitySearchesBeyondMode6()
    {
        // Two flat colours split down the middle of every block: mode 6 (one subset, one line) cannot
        // represent it, while the two subset modes can.
        using Image<Rgba32> image = BcEncodeTestHelpers.CreateImage(
            32,
            32,
            (x, y) => (x & 2) == 0 ? new Rgba32(240, 20, 20) : new Rgba32(20, 20, 240));

        byte[] quick = BcEncodeTestHelpers.Encode(
            TexelFormat.Bc7_UNorm,
            image,
            new BcEncodeOptions(0.5f, Bc7EncodingQuality.Quick));
        byte[] quality = BcEncodeTestHelpers.Encode(
            TexelFormat.Bc7_UNorm,
            image,
            new BcEncodeOptions(0.5f, Bc7EncodingQuality.Default));

        Assert.NotEqual(quick, quality);

        using Image<Rgba32> quickDecoded = BcEncodeTestHelpers.Decode(TexelFormat.Bc7_UNorm, quick, 32, 32);
        using Image<Rgba32> qualityDecoded = BcEncodeTestHelpers.Decode(TexelFormat.Bc7_UNorm, quality, 32, 32);

        BcEncodeTestHelpers.AssertMaxChannelError(image, qualityDecoded, 2, "BC7_UNorm two tone (default)");
        BcEncodeTestHelpers.AssertMaxChannelError(quickDecoded, qualityDecoded, 40, "BC7_UNorm two tone (quick)");
    }

    [Fact]
    public void TexelOperationsEncodeUsesTheDefaultSettings()
    {
        using Image<Rgba32> image = BcEncodeTestHelpers.CreateImage(
            24,
            12,
            (x, y) => new Rgba32((byte)(x * 10), 128, (byte)(x * 10), (byte)(x * 10)));

        Textures.TexelOperations operations =
            TexelFormatRegistry.GetOperations(TexelFormat.Bc7_UNorm);

        byte[] viaTexelOperations = new byte[operations.GetEncodedByteCount(image.Width, image.Height)];
        operations.Encode(image, viaTexelOperations);

        byte[] viaOptions = BcEncodeTestHelpers.Encode(TexelFormat.Bc7_UNorm, image);

        Assert.Equal(viaOptions, viaTexelOperations);

        using Image<Rgba32> decoded = operations.Decode<Rgba32>(
            viaTexelOperations,
            Configuration.Default,
            image.Width,
            image.Height);

        BcEncodeTestHelpers.AssertMaxChannelError(image, decoded, 4, "BC7_UNorm through TexelOperations.Encode");
    }
}
