using System.Buffers.Binary;
using ImageSharp.Textures.Formats.Dds;
using ImageSharp.Textures.Formats.Dds.Enums;
using ImageSharp.Textures.Tests.TestUtilities;
using ImageSharp.Textures.Tests.TestUtilities.Dds;

namespace ImageSharp.Textures.Tests.Formats.Dds;

/// <summary>
///     The block compressed encode cases. They need the BC texel operations, which the block compression lane
///     contributes, so they are the integration check for the container/texel boundary.
/// </summary>
[Trait("Category", "DdsEncode")]
public class DdsEncodeBlockCompressedTests
{
    /// <summary>
    ///     The exact 148 header bytes a 4x4 single level BC7_UNORM texture must produce: magic, header
    ///     (CAPS|HEIGHT|WIDTH|LINEARSIZE, linear size 16, one mip), the DX10 pixel format, caps, and the DX10
    ///     header (DXGI 98, TEXTURE2D, one array element, alpha mode STRAIGHT).
    /// </summary>
    private const string Bc7HeaderHex =
        "44445320" + "7C000000" + "07100A00" + "04000000" + "04000000" + "10000000" + "01000000" + "01000000"
        + "0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"
        + "20000000" + "04000000" + "44583130" + "00000000" + "00000000" + "00000000" + "00000000" + "00000000"
        + "00100000" + "00000000" + "00000000" + "00000000" + "00000000"
        + "62000000" + "03000000" + "00000000" + "01000000" + "00000000";

    private static readonly TextureConfiguration Configuration = TextureConfiguration.Default;

    [Fact]
    public void Bc7HeaderMatchesTheGoldenBytes()
    {
        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            4,
            4,
            texelFormat: TexelFormat.Bc7_UNorm);

        byte[] encoded = Encode(texture, new DdsEncodingOptions());

        Assert.Equal(Bc7HeaderHex, Convert.ToHexString(encoded.AsSpan(0, 148)));
        Assert.Equal(148 + 16, encoded.Length);
    }

    [Theory]
    [InlineData(TexelFormat.Bc1_UNorm, 0x31545844u, 8)]
    [InlineData(TexelFormat.Bc4_UNorm, 0x55344342u, 8)]
    [InlineData(TexelFormat.Bc5_UNorm, 0x55354342u, 16)]
    public void UnsignedFormatsUseTheLegacyFourCharacterCodeByDefault(
        TexelFormat texelFormat,
        uint expectedFourCc,
        int payloadBytes)
    {
        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            4,
            4,
            texelFormat: texelFormat);

        byte[] encoded = Encode(texture, new DdsEncodingOptions());

        Assert.Equal(128 + payloadBytes, encoded.Length);
        Assert.Equal(expectedFourCc, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(84)));
        Assert.Equal(0x000A1007u, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(8)));
        Assert.Equal((uint)payloadBytes, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(20)));
        Assert.Equal(0x1000u, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(108)));
    }

    [Fact]
    public void UseDx10HeaderForcesTheDx10PathForLegacyFormats()
    {
        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            4,
            4,
            texelFormat: TexelFormat.Bc1_UNorm);

        byte[] encoded = Encode(texture, new DdsEncodingOptions { UseDx10Header = true });

        Assert.Equal(148 + 8, encoded.Length);
        Assert.Equal((uint)DdsFourCc.DX10, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(84)));
        Assert.Equal((uint)DxgiFormat.BC1_UNorm, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(128)));
        Assert.Equal(3u, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(132)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(140)));
    }

    [Theory]
    [InlineData(TexelFormat.Bc1_UnormSrgb, DxgiFormat.BC1_UNorm_SRGB)]
    [InlineData(TexelFormat.Bc7_UNorm, DxgiFormat.BC7_UNorm)]
    [InlineData(TexelFormat.Bc7_UnormSrgb, DxgiFormat.BC7_UNorm_SRGB)]
    public void FormatsWithoutALegacyCodeAlwaysUseTheDx10Header(TexelFormat texelFormat, DxgiFormat expectedDxgi)
    {
        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            4,
            4,
            texelFormat: texelFormat);

        byte[] encoded = Encode(texture, new DdsEncodingOptions());

        Assert.Equal((uint)DdsFourCc.DX10, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(84)));
        Assert.Equal((uint)expectedDxgi, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(128)));
    }

    [Fact]
    public void Bc7EncodeTwiceProducesIdenticalBytes()
    {
        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            8,
            8,
            texelFormat: TexelFormat.Bc7_UNorm);

        Assert.Equal(
            Encode(texture, new DdsEncodingOptions()),
            Encode(texture, new DdsEncodingOptions()));
    }

    [Theory]
    [InlineData(TexelFormat.Bc1_UNorm, 0x800u)]
    [InlineData(TexelFormat.Bc4_UNorm, 0x800u)]
    [InlineData(TexelFormat.Bc5_UNorm, 0x1000u)]
    [InlineData(TexelFormat.Bc7_UNorm, 0x1000u)]
    public void LinearSizeMatchesTheTexconvValuesForA64Square(TexelFormat texelFormat, uint expectedLinearSize)
    {
        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            64,
            64,
            texelFormat: texelFormat);

        byte[] encoded = Encode(texture, new DdsEncodingOptions());

        Assert.Equal(expectedLinearSize, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(20)));
        Assert.Equal(0x80000u, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(8)) & 0x80000u);
    }

    [Fact]
    public void MipChainHeaderMatchesTheTexconvShape()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateLegacy(DdsFourCc.DXT1, 64, 64, 8);
        builder.MipLevelCount = 7;
        builder.WithDerivedHeaderFields();
        builder.AddPatternPayloads();

        using Texture decoded = Decode(builder.Build());
        byte[] encoded = Encode(decoded, new DdsEncodingOptions());

        Assert.Equal(7, decoded.MipLevels.Count);

        // texconv writes DDSD_CAPS|HEIGHT|WIDTH|PIXELFORMAT|MIPMAPCOUNT|LINEARSIZE and
        // DDSCAPS_TEXTURE|COMPLEX|MIPMAP for a 64 square mip chain.
        Assert.Equal(0x000A1007u, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(8)));
        Assert.Equal(0x00401008u, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(108)));
        Assert.Equal(7u, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(28)));
        Assert.Equal(0x800u, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(20)));
        Assert.Equal((uint)DdsFourCc.DXT1, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(84)));
    }

    [Fact]
    public void Bc7RoundTripPreservesPayloads()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.BC7_UNorm, 8, 8, 16);
        builder.MipLevelCount = 2;
        builder.WithDerivedHeaderFields();
        builder.AddPatternPayloads();

        using Texture decoded = Decode(builder.Build());
        byte[] encoded = Encode(decoded, new DdsEncodingOptions());
        using Texture roundTripped = Decode(encoded);

        Assert.Equal(decoded.MipLevels.Count, roundTripped.MipLevels.Count);

        for (int level = 0; level < decoded.MipLevels.Count; level++)
        {
            Assert.Equal(
                decoded.MipLevels[level].Surfaces[0].GetPayload().ToArray(),
                roundTripped.MipLevels[level].Surfaces[0].GetPayload().ToArray());
        }
    }

    [Fact]
    public void TargetTexelFormatConvertsBlockCompressedPayloadsThroughRgba()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.BC1_UNorm, 4, 4, 8);
        builder.AddPatternPayloads();

        using Texture decoded = Decode(builder.Build());
        using Image<Rgba32> expectedPixels = decoded.MipLevels[0].Surfaces[0].ToImage<Rgba32>();

        byte[] encoded = Encode(
            decoded,
            new DdsEncodingOptions { TargetTexelFormat = TexelFormat.R8G8B8A8_UNorm });
        using Texture converted = Decode(encoded);

        TextureSurface surface = converted.MipLevels[0].Surfaces[0];
        Assert.Equal(TexelFormat.R8G8B8A8_UNorm, surface.TexelFormat);

        using Image<Rgba32> actualPixels = surface.ToImage<Rgba32>();
        Assert.Equal(expectedPixels.Width, actualPixels.Width);

        for (int y = 0; y < expectedPixels.Height; y++)
        {
            for (int x = 0; x < expectedPixels.Width; x++)
            {
                Assert.Equal(expectedPixels[x, y], actualPixels[x, y]);
            }
        }
    }

    [Fact]
    public void TargetTexelFormatConvertsUncompressedPayloadsToBlocks()
    {
        using Texture texture = TestTextureFactory.CreateTexture(TestTextureFactory.CreateConfiguration(), 4, 4);

        byte[] encoded = Encode(
            texture,
            new DdsEncodingOptions { TargetTexelFormat = TexelFormat.Bc7_UNorm });
        using Texture converted = Decode(encoded);

        TextureSurface surface = converted.MipLevels[0].Surfaces[0];
        Assert.Equal(TexelFormat.Bc7_UNorm, surface.TexelFormat);
        Assert.Equal(16, surface.PayloadLength);
    }

    private static Texture Decode(byte[] file)
    {
        using MemoryStream stream = new(file);
        return new DdsTextureDecoder().Decode(Configuration, stream);
    }

    private static byte[] Encode(Texture texture, DdsEncodingOptions options)
    {
        using MemoryStream stream = new();
        new DdsTextureEncoder(options).Encode(texture, stream);
        return stream.ToArray();
    }
}
