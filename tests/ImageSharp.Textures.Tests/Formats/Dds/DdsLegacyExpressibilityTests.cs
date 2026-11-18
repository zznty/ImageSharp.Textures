using System.Buffers.Binary;
using ImageSharp.Textures.Formats.Dds;
using ImageSharp.Textures.Formats.Dds.Enums;
using ImageSharp.Textures.Tests.TestUtilities;
using ImageSharp.Textures.Tests.TestUtilities.Dds;

namespace ImageSharp.Textures.Tests.Formats.Dds;

[Trait("Category", "DdsEncode")]
public class DdsLegacyExpressibilityTests
{
    private static readonly TextureConfiguration Configuration = TextureConfiguration.Default;

    [Theory]
    [InlineData(TexelFormat.Bc1_UNorm, 0x31545844u)]
    [InlineData(TexelFormat.Bc4_UNorm, 0x55344342u)]
    [InlineData(TexelFormat.Bc5_UNorm, 0x55354342u)]
    public void SingleLayerUnsignedFormatsStayLegacy(TexelFormat texelFormat, uint expectedFourCc)
    {
        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            4,
            4,
            texelFormat: texelFormat);

        byte[] encoded = Encode(texture, new DdsEncodingOptions());

        Assert.Equal(128 + GetPayloadLength(texelFormat), encoded.Length);
        Assert.Equal(expectedFourCc, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(84)));
        Assert.Equal(0x000A1007u, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(8)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(24)));
    }

    [Fact]
    public void ArrayIsUpgradedToTheDx10HeaderWhenTheLegacyFormIsRequested()
    {
        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            4,
            4,
            3,
            TexelFormat.Bc1_UNorm);

        byte[] encoded = Encode(texture, new DdsEncodingOptions { UseDx10Header = false });

        Assert.Equal((uint)DdsFourCc.DX10, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(84)));
        Assert.Equal((uint)DxgiFormat.BC1_UNorm, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(128)));
        Assert.Equal(3u, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(140)));

        using Texture reloaded = Decode(encoded);

        Assert.Equal(3, reloaded.MipLevels[0].Surfaces.Count);

        for (int layer = 0; layer < 3; layer++)
        {
            Assert.Equal(
                texture.MipLevels[0].Surfaces[layer].GetPayload().ToArray(),
                reloaded.MipLevels[0].Surfaces[layer].GetPayload().ToArray());
        }
    }

    [Fact]
    public void CubeMapStaysLegacyBecauseCaps2CanExpressIt()
    {
        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            4,
            4,
            6,
            TexelFormat.Bc1_UNorm);

        TestFaceLayoutMetadata claim = texture.Metadata.GetFormatMetadata(TestFaceLayoutFormat.Instance);
        claim.FaceCount = 6;

        byte[] encoded = Encode(texture, new DdsEncodingOptions { UseDx10Header = false });

        Assert.Equal(128 + (6 * 8), encoded.Length);
        Assert.Equal((uint)DdsFourCc.DXT1, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(84)));
        Assert.Equal(0x200u | 0xFC00u, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(112)));

        using Texture reloaded = Decode(encoded);

        Assert.Equal(6, reloaded.MipLevels[0].Surfaces.Count);
        Assert.True(
            reloaded.Metadata.GetFormatMetadata(DdsFormat.Instance).ResourceMiscFlags
                .HasFlag(D3d10ResourceMiscFlags.TextureCube));
    }

    [Fact]
    public void CubeMapArrayIsUpgradedToTheDx10Header()
    {
        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            4,
            4,
            12,
            TexelFormat.Bc1_UNorm);

        TestFaceLayoutMetadata claim = texture.Metadata.GetFormatMetadata(TestFaceLayoutFormat.Instance);
        claim.FaceCount = 6;

        byte[] encoded = Encode(texture, new DdsEncodingOptions { UseDx10Header = false });

        Assert.Equal((uint)DdsFourCc.DX10, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(84)));
        Assert.Equal(0x200u | 0xFC00u, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(112)));
        Assert.Equal(0x4u, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(136)));
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(140)));

        using Texture reloaded = Decode(encoded);

        Assert.Equal(12, reloaded.MipLevels[0].Surfaces.Count);
        Assert.True(
            reloaded.Metadata.GetFormatMetadata(DdsFormat.Instance).ResourceMiscFlags
                .HasFlag(D3d10ResourceMiscFlags.TextureCube));
    }

    [Fact]
    public void VolumeStaysLegacyBecauseCaps2CanExpressIt()
    {
        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            4,
            4,
            texelFormat: TexelFormat.Bc1_UNorm);
        TextureMipLevel baseLevel = texture.MipLevels[0];

        using Texture volume = new(texture.Configuration, new TextureMetadata());
        TextureMipLevel level = volume.AddMipLevel(4, 4, 2);
        level.CreateSurface(TexelFormat.Bc1_UNorm);
        level.CreateSurface(TexelFormat.Bc1_UNorm);

        Assert.Equal(1, baseLevel.Surfaces.Count);

        byte[] encoded = Encode(volume, new DdsEncodingOptions { UseDx10Header = false });

        Assert.Equal(128 + (2 * 8), encoded.Length);
        Assert.Equal((uint)DdsFourCc.DXT1, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(84)));
        Assert.Equal(0x200000u, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(112)) & 0x200000u);
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(24)));
    }

    [Fact]
    public void TwoDimensionalFileWithZeroDepthDecodes()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 2, 2, 0);
        builder.Depth = 0;
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder.Build());

        Assert.Equal(1, texture.MipLevels[0].Depth);
        Assert.Single(texture.MipLevels[0].Surfaces);

        DdsTextureMetadata metadata = texture.Metadata.GetFormatMetadata(DdsFormat.Instance);
        Assert.Equal(1U, metadata.Depth);
    }

    private static int GetPayloadLength(TexelFormat texelFormat)
        => texelFormat == TexelFormat.Bc5_UNorm ? 16 : 8;

    private static Texture Decode(byte[] file)
    {
        using MemoryStream stream = new(file);
        return new DdsTextureDecoder().Decode(Configuration, stream);
    }

    private static byte[] Encode(Texture texture, DdsEncodingOptions options)
    {
        using MemoryStream stream = new();
        texture.Save(stream, new DdsTextureEncoder(options));
        return stream.ToArray();
    }
}
