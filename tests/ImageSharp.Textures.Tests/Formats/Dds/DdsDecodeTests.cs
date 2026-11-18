using ImageSharp.Textures.Formats.Dds;
using ImageSharp.Textures.Formats.Dds.Enums;
using ImageSharp.Textures.Tests.TestUtilities.Dds;

namespace ImageSharp.Textures.Tests.Formats.Dds;

[Trait("Category", "DdsDecode")]
public class DdsDecodeTests
{
    private const int Rgba8BlockBytes = 0;
    private const int Bc1BlockBytes = 8;
    private const int Bc4BlockBytes = 8;
    private const int Bc5BlockBytes = 16;
    private const int Bc7BlockBytes = 16;

    private static readonly TextureConfiguration Configuration = TextureConfiguration.Default;

    [Fact]
    public void Rgba8PayloadDecodesToImagePixels()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 2, 2, Rgba8BlockBytes)
            .AddPayload(
                1,
                2,
                3,
                4,
                5,
                6,
                7,
                8,
                9,
                10,
                11,
                12,
                13,
                14,
                15,
                16);

        using Texture texture = Decode(builder);

        TextureSurface surface = texture.MipLevels[0].Surfaces[0];
        Assert.Equal(TexelFormat.R8G8B8A8_UNorm, surface.TexelFormat);

        using Image<Rgba32> image = surface.ToImage<Rgba32>();
        Assert.Equal(new Rgba32(1, 2, 3, 4), image[0, 0]);
        Assert.Equal(new Rgba32(5, 6, 7, 8), image[1, 0]);
        Assert.Equal(new Rgba32(9, 10, 11, 12), image[0, 1]);
        Assert.Equal(new Rgba32(13, 14, 15, 16), image[1, 1]);
    }

    [Fact]
    public void Rgba8TextureWithOddDimensionsDecodesEveryTexel()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 3, 3, Rgba8BlockBytes);
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder);

        TextureSurface surface = texture.MipLevels[0].Surfaces[0];
        Assert.Equal(3, surface.Width);
        Assert.Equal(3, surface.Height);
        Assert.Equal(3 * 3 * 4, surface.PayloadLength);

        using Image<Rgba32> image = surface.ToImage<Rgba32>();
        Assert.Equal(new Rgba32(0, 0, 0, 0), image[2, 2]);
    }

    [Theory]
    [InlineData((uint)DxgiFormat.BC1_UNorm, Bc1BlockBytes)]
    [InlineData((uint)DxgiFormat.BC1_UNorm_SRGB, Bc1BlockBytes)]
    [InlineData((uint)DxgiFormat.BC4_UNorm, Bc4BlockBytes)]
    [InlineData((uint)DxgiFormat.BC5_UNorm, Bc5BlockBytes)]
    [InlineData((uint)DxgiFormat.BC7_UNorm, Bc7BlockBytes)]
    [InlineData((uint)DxgiFormat.BC7_UNorm_SRGB, Bc7BlockBytes)]
    public void BlockCompressedPayloadIsCopiedVerbatimPerSurface(uint dxgiFormat, int blockBytes)
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10(dxgiFormat, 8, 8, blockBytes)
            .WithDerivedHeaderFields();
        builder.MipLevelCount = 2;
        builder.WithDerivedHeaderFields();
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder);

        Assert.Equal(2, texture.MipLevels.Count);

        int expectedSurfaceIndex = 0;
        for (int level = 0; level < texture.MipLevels.Count; level++)
        {
            TextureMipLevel mipLevel = texture.MipLevels[level];
            int width = Math.Max(1, 8 >> level);
            Assert.Equal(width, mipLevel.Width);
            Assert.Single(mipLevel.Surfaces);

            TextureSurface surface = mipLevel.Surfaces[0];
            int expectedLength = (width + 3) / 4 * ((Math.Max(1, 8 >> level) + 3) / 4) * blockBytes;
            Assert.Equal(expectedLength, surface.PayloadLength);
            AssertPayloadFilledWith(surface, (byte)expectedSurfaceIndex, level, width);
            expectedSurfaceIndex++;
        }
    }

    [Fact]
    public void MipChainProducesOneSurfacePerLevelWithHalvedDimensions()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 8, 8, Rgba8BlockBytes);
        builder.MipLevelCount = 4;
        builder.WithDerivedHeaderFields();
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder);

        Assert.Equal(4, texture.MipLevels.Count);
        Assert.Equal([8, 4, 2, 1], texture.MipLevels.Select(level => level.Width));
        Assert.Equal([8, 4, 2, 1], texture.MipLevels.Select(level => level.Height));

        for (int level = 0; level < 4; level++)
        {
            Assert.Single(texture.MipLevels[level].Surfaces);
            AssertPayloadFilledWith(texture.MipLevels[level].Surfaces[0], (byte)level, level,
                texture.MipLevels[level].Width);
        }
    }

    [Fact]
    public void ArrayElementsBecomeSurfacesInLayerOrder()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 2, 2, Rgba8BlockBytes);
        builder.ArraySize = 3;
        builder.WithDerivedHeaderFields();
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder);

        TextureMipLevel mipLevel = texture.MipLevels[0];
        Assert.Equal(3, mipLevel.Surfaces.Count);
        Assert.Equal(1, mipLevel.Depth);

        for (int layer = 0; layer < 3; layer++)
        {
            AssertPayloadFilledWith(mipLevel.Surfaces[layer], (byte)layer, 0, 2);
        }
    }

    [Fact]
    public void CubeMapFacesAreOrderedPositiveXToNegativeZ()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 4, 4, Rgba8BlockBytes);
        builder.IsCubeMap = true;
        builder.WithDerivedHeaderFields();
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder);

        TextureMipLevel mipLevel = texture.MipLevels[0];
        Assert.Equal(6, mipLevel.Surfaces.Count);

        for (int face = 0; face < 6; face++)
        {
            AssertPayloadFilledWith(mipLevel.Surfaces[face], (byte)face, 0, 4);
        }

        DdsTextureMetadata metadata = texture.Metadata.GetFormatMetadata(DdsFormat.Instance);
        Assert.Equal(D3d10ResourceMiscFlags.TextureCube, metadata.ResourceMiscFlags);
        Assert.Equal(D3d10ResourceDimension.Texture2D, metadata.ResourceDimension);
    }

    [Fact]
    public void CubeMapArrayOrdersLayerBeforeFace()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 2, 2, Rgba8BlockBytes);
        builder.IsCubeMap = true;
        builder.ArraySize = 2;
        builder.WithDerivedHeaderFields();
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder);

        TextureMipLevel mipLevel = texture.MipLevels[0];
        Assert.Equal(12, mipLevel.Surfaces.Count);

        for (int index = 0; index < 12; index++)
        {
            AssertPayloadFilledWith(mipLevel.Surfaces[index], (byte)index, 0, 2);
        }
    }

    [Fact]
    public void VolumeDepthSlicesBecomeSurfacesPerLevel()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 4, 4, Rgba8BlockBytes);
        builder.IsVolume = true;
        builder.Depth = 4;
        builder.MipLevelCount = 3;
        builder.WithDerivedHeaderFields();
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder);

        Assert.Equal(3, texture.MipLevels.Count);
        Assert.Equal(4, texture.MipLevels[0].Depth);
        Assert.Equal(4, texture.MipLevels[0].Surfaces.Count);
        Assert.Equal(2, texture.MipLevels[1].Depth);
        Assert.Equal(2, texture.MipLevels[1].Surfaces.Count);
        Assert.Equal(1, texture.MipLevels[2].Depth);
        Assert.Single(texture.MipLevels[2].Surfaces);

        int expected = 0;
        foreach (TextureMipLevel level in texture.MipLevels)
        {
            for (int slice = 0; slice < level.Depth; slice++)
            {
                AssertPayloadFilledWith(level.Surfaces[slice], (byte)expected, level.Width, level.Width);
                expected++;
            }
        }

        DdsTextureMetadata metadata = texture.Metadata.GetFormatMetadata(DdsFormat.Instance);
        Assert.Equal(4U, metadata.Depth);
        Assert.Equal(D3d10ResourceDimension.Texture3D, metadata.ResourceDimension);
    }

    [Fact]
    public void LegacyFourCcHeadersDecodeBlockCompressedPayloads()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateLegacy(DdsFourCc.DXT1, 4, 4, Bc1BlockBytes)
            .WithDerivedHeaderFields();
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder);

        TextureSurface surface = texture.MipLevels[0].Surfaces[0];
        Assert.Equal(TexelFormat.Bc1_UNorm, surface.TexelFormat);
        Assert.Equal(Bc1BlockBytes, surface.PayloadLength);

        DdsTextureMetadata metadata = texture.Metadata.GetFormatMetadata(DdsFormat.Instance);
        Assert.False(metadata.HasDx10Header);
        Assert.Equal(DdsFourCc.DXT1, metadata.FourCc);
        Assert.Equal(DxgiFormat.Unknown, metadata.DxgiFormat);
    }

    [Fact]
    public void OneByOneTextureDecodes()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 1, 1, Rgba8BlockBytes)
            .AddPayload(200, 100, 50, 25);

        using Texture texture = Decode(builder);

        using Image<Rgba32> image = texture.MipLevels[0].Surfaces[0].ToImage<Rgba32>();
        Assert.Equal(new Rgba32(200, 100, 50, 25), image[0, 0]);
    }

    [Fact]
    public void IdentifyReportsTheLayoutWithoutReadingPayload()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.BC7_UNorm, 8, 8, Bc7BlockBytes);
        builder.IsCubeMap = true;
        builder.ArraySize = 2;
        builder.MipLevelCount = 3;
        builder.WithDerivedHeaderFields();

        using MemoryStream stream = new(builder.Build());
        TextureInfo info = new DdsTextureDecoder().Identify(Configuration, stream);

        Assert.Equal(8, info.Width);
        Assert.Equal(8, info.Height);
        Assert.Equal(1, info.Depth);
        Assert.Equal(3, info.MipLevelCount);
        Assert.Equal(36, info.SurfaceCount);
        Assert.Equal(TexelFormat.Bc7_UNorm, info.TexelFormat);
        Assert.Equal(4 + 124 + 20, stream.Position);
    }

    [Fact]
    public void DecodedMetadataCarriesTheContainerFields()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 4, 4, Rgba8BlockBytes);
        builder.MipLevelCount = 2;
        builder.Dx10MiscFlags2 = (uint)DdsAlphaMode.Premultiplied;
        builder.WithDerivedHeaderFields();
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder);

        Assert.Same(DdsFormat.Instance, texture.Metadata.DecodedTextureFormat);

        DdsTextureMetadata metadata = texture.Metadata.GetFormatMetadata(DdsFormat.Instance);
        Assert.True(metadata.HasDx10Header);
        Assert.Equal(DxgiFormat.R8G8B8A8_UNorm, metadata.DxgiFormat);
        Assert.Equal(DdsFourCc.None, metadata.FourCc);
        Assert.Equal(D3d10ResourceDimension.Texture2D, metadata.ResourceDimension);
        Assert.Equal(2U, metadata.MipMapCount);
        Assert.Equal(1U, metadata.Depth);
        Assert.Equal(1U, metadata.ArraySize);
        Assert.Equal(DdsAlphaMode.Premultiplied, metadata.AlphaMode);
        Assert.Equal(
            TexelAlphaRepresentation.Associated,
            metadata.ToTextureConnectingMetadata().TexelTypeInfo.AlphaRepresentation);
    }

    private static Texture Decode(DdsFileBuilder builder)
    {
        using MemoryStream stream = new(builder.Build());
        return new DdsTextureDecoder().Decode(Configuration, stream);
    }

    private static void AssertPayloadFilledWith(TextureSurface surface, byte expected, int level, int width)
    {
        ReadOnlySpan<byte> payload = surface.GetPayload();

        for (int i = 0; i < payload.Length; i++)
        {
            Assert.True(
                payload[i] == expected,
                $"Mip level {level} ({width} texels) payload byte {i} of {payload.Length} is 0x{payload[i]:X2}; expected 0x{expected:X2}.");
        }
    }
}
