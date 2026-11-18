using ImageSharp.Textures.Formats.Dds;
using ImageSharp.Textures.Tests.TestUtilities;
using ImageSharp.Textures.Texels.BcCommon;

namespace ImageSharp.Textures.Tests.TexelOperations.Bc1;

[Trait("Category", "BcEncode")]
public class Bc1EncodeTests
{
    /// <summary>
    ///     The reference payloads below were produced by texconv (DirectXTex) from the same inputs; the all
    ///     transparent block is the documented D3DXEncodeBC1 color-key block because a TGA cannot express an
    ///     image whose alpha is zero everywhere (DirectXTex treats those as opaque on load).
    /// </summary>
    [Fact]
    public void FourColourBlockMatchesTheReference()
    {
        using Image<Rgba32> image = CreateFourColourImage();

        byte[] payload = BcEncodeTestHelpers.Encode(TexelFormat.Bc1_UNorm, image);

        BcEncodeTestHelpers.AssertPayload("915D055855FFAA00", payload, "BC1 4x4 four colour block");
    }

    [Fact]
    public void PunchThroughBlockMatchesTheReference()
    {
        using Image<Rgba32> image = CreateFourColourImage(0, 0);

        byte[] payload = BcEncodeTestHelpers.Encode(TexelFormat.Bc1_UNorm, image);

        BcEncodeTestHelpers.AssertPayload("0558915D03AAAA55", payload, "BC1 4x4 punch-through block");
    }

    [Fact]
    public void FullyTransparentBlockWritesTheReferencePunchThroughBlock()
    {
        using Image<Rgba32> image = BcEncodeTestHelpers.CreateImage(4, 4, (x, y) => new Rgba32(0, 0, 0, 0));

        byte[] payload = BcEncodeTestHelpers.Encode(TexelFormat.Bc1_UNorm, image);

        BcEncodeTestHelpers.AssertPayload(
            "0000FFFFFFFFFFFF",
            payload,
            "BC1 4x4 fully transparent block");
    }

    [Fact]
    public void OpaqueBlocksNeverPunchThrough()
    {
        using Image<Rgba32> image = BcEncodeTestHelpers.CreateImage(
            8,
            8,
            (x, y) => new Rgba32((byte)(x * 30), (byte)(y * 30), 90, 255));

        byte[] payload = BcEncodeTestHelpers.Encode(TexelFormat.Bc1_UNorm, image);
        using Image<Rgba32> decoded = BcEncodeTestHelpers.Decode(TexelFormat.Bc1_UNorm, payload, 8, 8);

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                Assert.Equal(255, decoded[x, y].A);
            }
        }
    }

    [Fact]
    public void AlphaThresholdSelectsThePunchThroughMode()
    {
        using Image<Rgba32> image = CreateImageWithSemiTransparentTexel();

        byte[] punched = BcEncodeTestHelpers.Encode(
            TexelFormat.Bc1_UNorm,
            image,
            new BcEncodeOptions(0.5f, Bc7EncodingQuality.Default));
        byte[] opaque = BcEncodeTestHelpers.Encode(
            TexelFormat.Bc1_UNorm,
            image,
            new BcEncodeOptions(0.1f, Bc7EncodingQuality.Default));

        using Image<Rgba32> punchedImage = BcEncodeTestHelpers.Decode(TexelFormat.Bc1_UNorm, punched, 4, 4);
        using Image<Rgba32> opaqueImage = BcEncodeTestHelpers.Decode(TexelFormat.Bc1_UNorm, opaque, 4, 4);

        Assert.Equal(0, punchedImage[1, 1].A);
        Assert.Equal(255, opaqueImage[1, 1].A);
        Assert.NotEqual(punched, opaque);
    }

    [Fact]
    public void NonMultipleOfFourBlocksMatchTheReference()
    {
        using Image<Rgba32> image = CreateSixBySixImage(-1, -1);
        using Image<Rgba32> punchedImage = CreateSixBySixImage(5, 5);

        byte[] payload = BcEncodeTestHelpers.Encode(TexelFormat.Bc1_UNorm, image);
        byte[] punchedPayload = BcEncodeTestHelpers.Encode(TexelFormat.Bc1_UNorm, punchedImage);

        BcEncodeTestHelpers.AssertPayload(
            "C243023855FFAA00C2B302A855FFAA00227E0205F50AF50A22B602B555005500",
            payload,
            "BC1 6x6 surface");
        BcEncodeTestHelpers.AssertPayload(
            "C243023855FFAA00C2B302A855FFAA00227E0205F50AF50A02B522B600DD00DD",
            punchedPayload,
            "BC1 6x6 surface with a punch-through texel");
    }

    [Fact]
    public void PartialBlocksReplicateTexelsLikeTheReference()
    {
        using Image<Rgba32> surface = CreateSixBySixImage(-1, -1);

        // The 8x8 image is the 6x6 surface with the edges completed using the DirectXTex replication table,
        // so a correct encoder must produce exactly the same blocks for both.
        using Image<Rgba32> padded = BcEncodeTestHelpers.CreateImage(
            8,
            8,
            (x, y) => surface[Replicate(x, 6), Replicate(y, 6)]);

        byte[] surfacePayload = BcEncodeTestHelpers.Encode(TexelFormat.Bc1_UNorm, surface);
        byte[] paddedPayload = BcEncodeTestHelpers.Encode(TexelFormat.Bc1_UNorm, padded);

        Assert.Equal(paddedPayload, surfacePayload);
    }

    [Fact]
    public void SmoothGradientStaysWithinEightLsb()
    {
        // A gray ramp: BC1's luminance weighted axis reproduces it within four LSB (texconv measures the same).
        using Image<Rgba32> image = BcEncodeTestHelpers.CreateImage(
            64,
            64,
            (x, y) => new Rgba32((byte)(x * 4), (byte)(x * 4), (byte)(x * 4), 255));

        byte[] payload = BcEncodeTestHelpers.Encode(TexelFormat.Bc1_UNorm, image);
        using Image<Rgba32> decoded = BcEncodeTestHelpers.Decode(TexelFormat.Bc1_UNorm, payload, 64, 64);

        BcEncodeTestHelpers.AssertMaxChannelError(image, decoded, 8, "BC1");
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
            (x, y) => new Rgba32((byte)(x * 7), (byte)(y * 11), 40, 255));

        byte[] payload = BcEncodeTestHelpers.Encode(TexelFormat.Bc1_UNorm, image);
        int expected = (width + 3) / 4 * ((height + 3) / 4) * 8;

        Assert.Equal(expected, payload.Length);
        Assert.Equal(expected, BcEncodeTestHelpers.GetEncodedByteCount(TexelFormat.Bc1_UNorm, width, height));
    }

    [Fact]
    public void EncodeIsDeterministic()
    {
        using Image<Rgba32> image = CreateSixBySixImage(-1, -1);

        byte[] first = BcEncodeTestHelpers.Encode(TexelFormat.Bc1_UNorm, image);
        byte[] second = BcEncodeTestHelpers.Encode(TexelFormat.Bc1_UNorm, image);

        Assert.Equal(first, second);
    }

    [Fact]
    public void CopyFromEditSurvivesTheDdsRoundTrip()
    {
        using Texture texture = TestTextureFactory.CreateTexture(TextureConfiguration.Default, 8, 8);
        TextureSurface surface = texture.MipLevels[0].Surfaces[0];

        // A gray ramp: BC1 reproduces it within four LSB (texconv measures the same on this input).
        using Image<Rgba32> edited = BcEncodeTestHelpers.CreateImage(
            8,
            8,
            (x, y) => new Rgba32((byte)(x * 32), (byte)(x * 32), (byte)(x * 32), 255));
        surface.CopyFrom(edited);

        byte[] direct = BcEncodeTestHelpers.Encode(TexelFormat.Bc1_UNorm, edited);

        using MemoryStream stream = new();
        texture.Save(
            stream,
            new DdsTextureEncoder(new DdsEncodingOptions { TargetTexelFormat = TexelFormat.Bc1_UNorm }));

        using Texture reloaded = Texture.Load(new MemoryStream(stream.ToArray()));
        TextureSurface reloadedSurface = reloaded.MipLevels[0].Surfaces[0];

        Assert.Equal(TexelFormat.Bc1_UNorm, reloadedSurface.TexelFormat);
        Assert.Equal(direct, reloadedSurface.GetPayload().ToArray());

        using Image<Rgba32> decoded = reloadedSurface.ToImage<Rgba32>();
        BcEncodeTestHelpers.AssertMaxChannelError(edited, decoded, 8, "BC1 via DDS");
    }

    [Fact]
    public void DdsOptionsReachTheBc1Encoder()
    {
        using Texture texture = TestTextureFactory.CreateTexture(TextureConfiguration.Default, 4, 4);
        TextureSurface surface = texture.MipLevels[0].Surfaces[0];

        using (Image<Rgba32> edited = CreateImageWithSemiTransparentTexel())
        {
            surface.CopyFrom(edited);
        }

        byte[] punched = EncodeDds(
            texture,
            new DdsEncodingOptions { TargetTexelFormat = TexelFormat.Bc1_UNorm, Bc1AlphaThreshold = 0.5f });
        byte[] opaque = EncodeDds(
            texture,
            new DdsEncodingOptions { TargetTexelFormat = TexelFormat.Bc1_UNorm, Bc1AlphaThreshold = 0.1f });

        using Texture punchedTexture = Texture.Load(new MemoryStream(punched));
        using Texture opaqueTexture = Texture.Load(new MemoryStream(opaque));
        using Image<Rgba32> punchedImage = punchedTexture.MipLevels[0].Surfaces[0].ToImage<Rgba32>();
        using Image<Rgba32> opaqueImage = opaqueTexture.MipLevels[0].Surfaces[0].ToImage<Rgba32>();

        Assert.Equal(0, punchedImage[1, 1].A);
        Assert.Equal(255, opaqueImage[1, 1].A);
    }

    private static byte[] EncodeDds(Texture texture, DdsEncodingOptions options)
    {
        using MemoryStream stream = new();
        texture.Save(stream, new DdsTextureEncoder(options));
        return stream.ToArray();
    }

    private static Image<Rgba32> CreateFourColourImage(int punchThroughX = -1, int punchThroughY = -1)
        => BcEncodeTestHelpers.CreateImage(
            4,
            4,
            (x, y) => new Rgba32(
                (byte)(x * 60),
                (byte)(y * 60),
                (byte)((x + y) * 30),
                (byte)(x == punchThroughX && y == punchThroughY ? 0 : 255)));

    private static Image<Rgba32> CreateImageWithSemiTransparentTexel()
        => BcEncodeTestHelpers.CreateImage(
            4,
            4,
            (x, y) => new Rgba32(
                (byte)(x * 60),
                (byte)(y * 60),
                (byte)((x + y) * 30),
                (byte)(x == 1 && y == 1 ? 100 : 255)));

    private static Image<Rgba32> CreateSixBySixImage(int punchThroughX, int punchThroughY)
        => BcEncodeTestHelpers.CreateImage(
            6,
            6,
            (x, y) => new Rgba32(
                (byte)(x * 40),
                (byte)(y * 40),
                20,
                (byte)(x == punchThroughX && y == punchThroughY ? 0 : 255)));

    /// <summary>
    ///     Resolves a padded coordinate through DirectXTex's sequential replication table <c>{ 0, 0, 0, 1 }</c>.
    /// </summary>
    private static int Replicate(int coordinate, int size)
    {
        int blockStart = coordinate & ~3;
        int relative = coordinate & 3;
        int visible = Math.Min(4, size - blockStart);

        while (relative >= visible)
        {
            relative = relative switch { 1 => 0, 2 => 0, 3 => 1, _ => 0 };
        }

        return blockStart + relative;
    }
}
