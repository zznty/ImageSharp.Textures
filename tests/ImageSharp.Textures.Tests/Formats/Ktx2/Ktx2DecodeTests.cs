using System.Buffers.Binary;
using ImageSharp.Textures.Formats.Ktx2;
using ImageSharp.Textures.Formats.Ktx2.Enums;
using ImageSharp.Textures.Tests.TestUtilities;
using ImageSharp.Textures.Tests.TestUtilities.Ktx2;
using ImageSharp.Textures.Tests.TestUtilities.Ktx;

namespace ImageSharp.Textures.Tests.Formats.Ktx2;

[Trait("Category", "Ktx2")]
public class Ktx2DecodeTests
{
    [Fact]
    public void DecodesRgba8Pixels()
    {
        byte[] payload = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        Ktx2FileBuilder builder = CreateRgba8Builder(2, 2);
        builder.AddPayload(payload);

        using Texture texture = Decode(builder.Build());
        TextureSurface surface = texture.MipLevels[0].Surfaces[0];

        Assert.Equal(TexelFormat.R8G8B8A8_UNorm, surface.TexelFormat);
        Assert.Same(Ktx2Format.Instance, texture.Metadata.DecodedTextureFormat);

        using Image<Rgba32> image = surface.ToImage<Rgba32>();
        Assert.Equal(new Rgba32(1, 2, 3, 4), image[0, 0]);
        Assert.Equal(new Rgba32(5, 6, 7, 8), image[1, 0]);
        Assert.Equal(new Rgba32(9, 10, 11, 12), image[0, 1]);
        Assert.Equal(new Rgba32(13, 14, 15, 16), image[1, 1]);
    }

    [Fact]
    public void DecodesMipLevelsThatAreStoredSmallestFirst()
    {
        Ktx2FileBuilder builder = CreateRgba8Builder(8, 8);
        builder.LevelCount = 3;
        builder.AddPatternPayloads();

        byte[] file = builder.Build();

        // One surface per level, written from the smallest level to the base level.
        int dataStart = 80 + (24 * 3) + CreateDfd(37).Length;
        Assert.Equal(0, file[dataStart]);
        Assert.Equal(2, file[^1]);

        using Texture texture = Decode(file);

        Assert.Equal(3, texture.MipLevels.Count);
        Assert.Equal(8, texture.MipLevels[0].Width);
        Assert.Equal(4, texture.MipLevels[1].Width);
        Assert.Equal(2, texture.MipLevels[2].Width);

        KtxTestTextures.AssertSurfacePayload(texture.MipLevels[0].Surfaces[0], 2);
        KtxTestTextures.AssertSurfacePayload(texture.MipLevels[1].Surfaces[0], 1);
        KtxTestTextures.AssertSurfacePayload(texture.MipLevels[2].Surfaces[0], 0);
    }

    [Fact]
    public void DecodesArrayLayersAndFacesInLayerFaceOrder()
    {
        Ktx2FileBuilder builder = CreateRgba8Builder(4, 4);
        builder.LayerCount = 2;
        builder.FaceCount = 6;
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder.Build());
        TextureMipLevel mipLevel = texture.MipLevels[0];

        Assert.Equal(12, mipLevel.Surfaces.Count);

        for (int surface = 0; surface < 12; surface++)
        {
            KtxTestTextures.AssertSurfacePayload(
                mipLevel.Surfaces[surface],
                KtxTestTextures.GetSurfaceValue(0, surface));
        }
    }

    [Fact]
    public void DecodesVolumeSlicesInDepthOrder()
    {
        Ktx2FileBuilder builder = CreateRgba8Builder(2, 2);
        builder.Depth = 3;
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder.Build());
        TextureMipLevel mipLevel = texture.MipLevels[0];

        Assert.Equal(3, mipLevel.Depth);
        Assert.Equal(3, mipLevel.Surfaces.Count);

        for (int surface = 0; surface < 3; surface++)
        {
            KtxTestTextures.AssertSurfacePayload(
                mipLevel.Surfaces[surface],
                KtxTestTextures.GetSurfaceValue(0, surface));
        }
    }

    [Fact]
    public void LevelCountZeroMeansOneLevel()
    {
        Ktx2FileBuilder builder = CreateRgba8Builder(2, 2);
        builder.LevelCount = 0;
        builder.AddPatternPayloads();

        byte[] file = builder.Build();
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(40)));

        using Texture texture = Decode(file);
        Assert.Equal(1, texture.MipLevels.Count);

        using MemoryStream stream = new(file);
        TextureInfo info = new Ktx2TextureDecoder().Identify(TextureConfiguration.Default, stream);
        Assert.Equal(1, info.MipLevelCount);
        Assert.Equal(1, info.SurfaceCount);
    }

    [BcTheory(TexelFormat.Bc1_UNorm)]
    [InlineData(133u, TexelFormat.Bc1_UNorm, 8)]
    [InlineData(134u, TexelFormat.Bc1_UnormSrgb, 8)]
    [InlineData(139u, TexelFormat.Bc4_UNorm, 8)]
    [InlineData(141u, TexelFormat.Bc5_UNorm, 16)]
    [InlineData(145u, TexelFormat.Bc7_UNorm, 16)]
    [InlineData(146u, TexelFormat.Bc7_UnormSrgb, 16)]
    public void DecodesBlockCompressedPayloadVerbatim(uint vkFormat, TexelFormat texelFormat, int blockBytes)
    {
        Ktx2FileBuilder builder = Ktx2FileBuilder.Create(vkFormat, 8, 8, blockBytes);
        builder.DataFormatDescriptor = CreateDfd(vkFormat);
        builder.AddPatternPayloads(3);

        using Texture texture = Decode(builder.Build());
        TextureSurface surface = texture.MipLevels[0].Surfaces[0];

        Assert.Equal(texelFormat, surface.TexelFormat);
        Assert.Equal((uint)(2 * 2 * blockBytes), (uint)surface.PayloadLength);
        KtxTestTextures.AssertSurfacePayload(surface, 3);
    }

    [Fact]
    public async Task DecodeAsyncMatchesDecode()
    {
        Ktx2FileBuilder builder = CreateRgba8Builder(2, 2);
        builder.AddPatternPayloads(5);

        using MemoryStream stream = new(builder.Build());
        using Texture texture = await new Ktx2TextureDecoder().DecodeAsync(TextureConfiguration.Default, stream);

        KtxTestTextures.AssertSurfacePayload(texture.MipLevels[0].Surfaces[0], 5);
    }

    [Fact]
    public void DecodesKeyValueData()
    {
        byte[] keyValueData = KtxFileBuilder.CreateKeyValueBlock(
            false,
            ("KTXwriter", "tests"u8.ToArray()),
            ("KTXorientation", "S=r,T=d"u8.ToArray()));

        Ktx2FileBuilder builder = CreateRgba8Builder(1, 1);
        builder.KeyValueData = keyValueData;
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder.Build());
        Ktx2TextureMetadata metadata = texture.Metadata.GetFormatMetadata(Ktx2Format.Instance);

        Assert.Equal(keyValueData, metadata.KeyValueData);
        Assert.Equal(2, metadata.KeyValuePairs.Count);
        Assert.Equal("KTXwriter", metadata.KeyValuePairs[0].Key);
        Assert.Equal("KTXorientation", metadata.KeyValuePairs[1].Key);
    }

    [Fact]
    public void IdentifyReportsTheHeaderLayout()
    {
        Ktx2FileBuilder builder = CreateRgba8Builder(8, 4);
        builder.LevelCount = 3;
        builder.LayerCount = 2;
        builder.FaceCount = 6;
        builder.AddPatternPayloads();
        byte[] file = builder.Build();

        using MemoryStream stream = new(file);
        TextureInfo info = new Ktx2TextureDecoder().Identify(TextureConfiguration.Default, stream);

        Assert.Equal(8, info.Width);
        Assert.Equal(4, info.Height);
        Assert.Equal(1, info.Depth);
        Assert.Equal(3, info.MipLevelCount);
        Assert.Equal(3 * 2 * 6, info.SurfaceCount);
        Assert.Equal(TexelFormat.R8G8B8A8_UNorm, info.TexelFormat);
        Assert.Equal(80, stream.Position);
    }

    [Theory]
    [InlineData(133u, TexelFormat.Bc1_UNorm, 8)]
    [InlineData(141u, TexelFormat.Bc5_UNorm, 16)]
    [InlineData(146u, TexelFormat.Bc7_UnormSrgb, 16)]
    public void IdentifyReportsBlockCompressedHeaders(uint vkFormat, TexelFormat texelFormat, int blockBytes)
    {
        Ktx2FileBuilder builder = Ktx2FileBuilder.Create(vkFormat, 64, 64, blockBytes);
        builder.DataFormatDescriptor = CreateDfd(vkFormat);
        builder.LevelCount = 2;
        builder.Depth = 2;
        builder.AddPatternPayloads();
        byte[] file = builder.Build();

        using MemoryStream stream = new(file);
        TextureInfo info = new Ktx2TextureDecoder().Identify(TextureConfiguration.Default, stream);

        Assert.Equal(64, info.Width);
        Assert.Equal(64, info.Height);
        Assert.Equal(2, info.Depth);
        Assert.Equal(2, info.MipLevelCount);
        Assert.Equal(3, info.SurfaceCount);
        Assert.Equal(texelFormat, info.TexelFormat);
    }

    [BcFact(TexelFormat.Bc1_UNorm)]
    public void DecodesTheVkFormatAliasForOpaqueBc1()
    {
        // vkFormat 131 (BC1 RGB) maps to the same texel format as vkFormat 133 and uses the opaque channel id.
        Ktx2FileBuilder builder = Ktx2FileBuilder.Create(131, 8, 8, 8);
        builder.DataFormatDescriptor = CreateDfd(131);
        builder.AddPatternPayloads();
        byte[] file = builder.Build();

        using Texture texture = Decode(file);
        Assert.Equal(TexelFormat.Bc1_UNorm, texture.MipLevels[0].Surfaces[0].TexelFormat);
    }

    internal static byte[] CreateDfd(uint vkFormat)
    {
        if (!Ktx2TexelFormats.TryGetFormatInfo((VkFormat)vkFormat, out Ktx2FormatInfo formatInfo))
        {
            throw new InvalidOperationException($"The test vkFormat {vkFormat} is not mapped.");
        }

        return Ktx2DataFormatDescriptor.Create(formatInfo);
    }

    private static Ktx2FileBuilder CreateRgba8Builder(int width, int height)
    {
        Ktx2FileBuilder builder = Ktx2FileBuilder.Create(37, width, height, 0);
        builder.DataFormatDescriptor = CreateDfd(37);
        return builder;
    }

    private static Texture Decode(byte[] file)
    {
        using MemoryStream stream = new(file);
        return Texture.Load(stream, new Ktx2TextureDecoder());
    }
}
