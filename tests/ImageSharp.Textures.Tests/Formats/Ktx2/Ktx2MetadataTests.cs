using ImageSharp.Textures.Formats.Ktx2;
using ImageSharp.Textures.Formats.Ktx2.Enums;
using ImageSharp.Textures.Formats.Ktx;
using ImageSharp.Textures.Tests.TestUtilities.Ktx2;
using ImageSharp.Textures.Tests.TestUtilities.Ktx;

namespace ImageSharp.Textures.Tests.Formats.Ktx2;

[Trait("Category", "Ktx2")]
public class Ktx2MetadataTests
{
    [Fact]
    public void DecodedMetadataRecordsTheHeaderFields()
    {
        byte[] dfd = Ktx2DecodeTests.CreateDfd(141);
        Ktx2FileBuilder builder = Ktx2FileBuilder.Create(141, 4, 2, 16);
        builder.DataFormatDescriptor = dfd;
        builder.LevelCount = 2;
        builder.LayerCount = 3;
        builder.FaceCount = 6;
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder.Build());
        Ktx2TextureMetadata metadata = texture.Metadata.GetFormatMetadata(Ktx2Format.Instance);

        Assert.Equal(VkFormat.VK_FORMAT_BC5_UNORM_BLOCK, metadata.VkFormat);
        Assert.Equal(1u, metadata.TypeSize);
        Assert.Equal(3u, metadata.LayerCount);
        Assert.Equal(6u, metadata.FaceCount);
        Assert.Equal(2u, metadata.LevelCount);
        Assert.Equal(0u, metadata.SupercompressionScheme);
    }

    [Theory]
    [InlineData(VkFormat.VK_FORMAT_R8G8B8A8_UNORM, 32)]
    [InlineData(VkFormat.VK_FORMAT_BC1_RGBA_UNORM_BLOCK, 64)]
    [InlineData(VkFormat.VK_FORMAT_BC1_RGB_UNORM_BLOCK, 64)]
    [InlineData(VkFormat.VK_FORMAT_BC1_RGBA_SRGB_BLOCK, 64)]
    [InlineData(VkFormat.VK_FORMAT_BC4_UNORM_BLOCK, 64)]
    [InlineData(VkFormat.VK_FORMAT_BC5_UNORM_BLOCK, 128)]
    [InlineData(VkFormat.VK_FORMAT_BC7_UNORM_BLOCK, 128)]
    [InlineData(VkFormat.VK_FORMAT_BC7_SRGB_BLOCK, 128)]
    public void ToTextureConnectingMetadataMapsEveryVkFormat(VkFormat vkFormat, int bitsPerTexel)
    {
        Ktx2TextureMetadata metadata = new() { VkFormat = vkFormat };

        TextureConnectingMetadata connecting = metadata.ToTextureConnectingMetadata();

        Assert.Equal(bitsPerTexel, connecting.TexelTypeInfo.BitsPerTexel);
        Assert.True(Ktx2TexelFormats.TryGetTexelFormat(connecting.TexelTypeInfo, out _));
    }

    [Theory]
    [InlineData(TexelFormat.R8G8B8A8_UNorm, VkFormat.VK_FORMAT_R8G8B8A8_UNORM)]
    [InlineData(TexelFormat.Bc1_UNorm, VkFormat.VK_FORMAT_BC1_RGBA_UNORM_BLOCK)]
    [InlineData(TexelFormat.Bc1_UnormSrgb, VkFormat.VK_FORMAT_BC1_RGBA_SRGB_BLOCK)]
    [InlineData(TexelFormat.Bc4_UNorm, VkFormat.VK_FORMAT_BC4_UNORM_BLOCK)]
    [InlineData(TexelFormat.Bc5_UNorm, VkFormat.VK_FORMAT_BC5_UNORM_BLOCK)]
    [InlineData(TexelFormat.Bc7_UNorm, VkFormat.VK_FORMAT_BC7_UNORM_BLOCK)]
    [InlineData(TexelFormat.Bc7_UnormSrgb, VkFormat.VK_FORMAT_BC7_SRGB_BLOCK)]
    public void FromTextureConnectingMetadataUsesTheCanonicalVkFormat(TexelFormat texelFormat, VkFormat expected)
    {
        TextureConnectingMetadata connecting = new() { TexelTypeInfo = Ktx2TexelFormats.GetTexelTypeInfo(texelFormat) };

        Ktx2TextureMetadata metadata = Ktx2TextureMetadata.FromTextureConnectingMetadata(connecting);

        Assert.Equal(expected, metadata.VkFormat);
        Assert.Empty(metadata.KeyValueData);
        Assert.Empty(metadata.KeyValuePairs);
    }

    [Fact]
    public void ToTextureConnectingMetadataRejectsUnknownFormats()
    {
        Ktx2TextureMetadata metadata = new() { VkFormat = VkFormat.VK_FORMAT_UNDEFINED };

        Assert.Throws<NotSupportedException>(metadata.ToTextureConnectingMetadata);
    }

    [Fact]
    public void DeepCloneCopiesKeyValueDataAndPairs()
    {
        byte[] keyValueData = KtxFileBuilder.CreateKeyValueBlock(
            false,
            ("KTXwriter", "tests"u8.ToArray()));

        Ktx2TextureMetadata metadata = new()
        {
            VkFormat = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
            KeyValueData = keyValueData,
            KeyValuePairs = [new KtxKeyValuePair("KTXwriter", "tests"u8.ToArray())]
        };

        Ktx2TextureMetadata clone = metadata.DeepClone();
        clone.KeyValueData[0] ^= 0xFF;
        clone.KeyValuePairs[0].Value[0] ^= 0xFF;

        Assert.NotEqual(metadata.KeyValueData[0], clone.KeyValueData[0]);
        Assert.NotEqual(metadata.KeyValuePairs[0].Value[0], clone.KeyValuePairs[0].Value[0]);
        Assert.Equal(metadata.VkFormat, clone.VkFormat);
    }

    [Fact]
    public void DefaultsAreEmpty()
    {
        Ktx2TextureMetadata metadata = Ktx2Format.Instance.CreateDefaultFormatMetadata();

        Assert.Equal(VkFormat.VK_FORMAT_UNDEFINED, metadata.VkFormat);
        Assert.Equal(1u, metadata.TypeSize);
        Assert.Equal(1u, metadata.FaceCount);
        Assert.Equal(1u, metadata.LevelCount);
        Assert.Empty(metadata.KeyValueData);
        Assert.Empty(metadata.KeyValuePairs);
    }

    private static Texture Decode(byte[] file)
    {
        using MemoryStream stream = new(file);
        return Texture.Load(stream, new Ktx2TextureDecoder());
    }
}
