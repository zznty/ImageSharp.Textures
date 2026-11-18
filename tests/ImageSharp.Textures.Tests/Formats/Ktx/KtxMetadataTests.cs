using ImageSharp.Textures.Formats.Ktx;
using ImageSharp.Textures.Formats.Ktx.Enums;
using ImageSharp.Textures.Tests.TestUtilities.Ktx;

namespace ImageSharp.Textures.Tests.Formats.Ktx;

[Trait("Category", "Ktx")]
public class KtxMetadataTests
{
    [Fact]
    public void DecodedMetadataRecordsTheHeaderFields()
    {
        KtxFileBuilder builder = KtxFileBuilder.CreateRgba8(4, 2);
        builder.MipLevelCount = 2;
        builder.ArrayElementCount = 3;
        builder.FaceCount = 6;
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder.Build());
        KtxTextureMetadata metadata = texture.Metadata.GetFormatMetadata(KtxFormat.Instance);

        Assert.Equal(KtxEndianness.LittleEndian, metadata.Endianness);
        Assert.Equal((uint)GlType.UnsignedByte, metadata.GlType);
        Assert.Equal(1u, metadata.GlTypeSize);
        Assert.Equal((uint)GlFormat.Rgba, metadata.GlFormat);
        Assert.Equal((uint)GlInternalFormat.Rgba8, metadata.GlInternalFormat);
        Assert.Equal((uint)GlBaseInternalFormat.Rgba, metadata.GlBaseInternalFormat);
        Assert.Equal(3u, metadata.ArrayElementCount);
        Assert.Equal(6u, metadata.FaceCount);
        Assert.Equal(2u, metadata.MipLevelCount);
    }

    [Fact]
    public void ToTextureConnectingMetadataMapsRgba8()
    {
        KtxFileBuilder builder = KtxFileBuilder.CreateRgba8(2, 2);
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder.Build());
        TextureConnectingMetadata connecting = texture.Metadata
            .GetFormatMetadata(KtxFormat.Instance)
            .ToTextureConnectingMetadata();

        Assert.Equal(32, connecting.TexelTypeInfo.BitsPerTexel);
        Assert.Equal(TexelColorType.RGB | TexelColorType.Alpha, connecting.TexelTypeInfo.ColorType);
        Assert.Equal(4, connecting.TexelTypeInfo.ComponentInfo.ComponentCount);
    }

    [Theory]
    [InlineData(0x83F1u, TexelFormat.Bc1_UNorm, 64)]
    [InlineData(0x8C4Du, TexelFormat.Bc1_UnormSrgb, 64)]
    [InlineData(0x8DBBu, TexelFormat.Bc4_UNorm, 64)]
    [InlineData(0x8DBDu, TexelFormat.Bc5_UNorm, 128)]
    [InlineData(0x8E8Cu, TexelFormat.Bc7_UNorm, 128)]
    [InlineData(0x8E8Du, TexelFormat.Bc7_UnormSrgb, 128)]
    public void ToTextureConnectingMetadataMapsEveryInternalFormat(
        uint glInternalFormat,
        TexelFormat texelFormat,
        int bitsPerTexel)
    {
        KtxTextureMetadata metadata = new() { GlInternalFormat = glInternalFormat };

        TextureConnectingMetadata connecting = metadata.ToTextureConnectingMetadata();

        Assert.Equal(bitsPerTexel, connecting.TexelTypeInfo.BitsPerTexel);
        Assert.True(KtxTexelFormats.TryGetTexelFormat(connecting.TexelTypeInfo, out TexelFormat roundTripped));
        Assert.Equal(texelFormat, roundTripped);
    }

    [Theory]
    [InlineData(TexelFormat.R8G8B8A8_UNorm, 0x8058u, 0x1908u)]
    [InlineData(TexelFormat.Bc1_UNorm, 0x83F1u, 0x1908u)]
    [InlineData(TexelFormat.Bc1_UnormSrgb, 0x8C4Du, 0x1908u)]
    [InlineData(TexelFormat.Bc4_UNorm, 0x8DBBu, 0x1903u)]
    [InlineData(TexelFormat.Bc5_UNorm, 0x8DBDu, 0x8227u)]
    [InlineData(TexelFormat.Bc7_UNorm, 0x8E8Cu, 0x1908u)]
    [InlineData(TexelFormat.Bc7_UnormSrgb, 0x8E8Du, 0x1908u)]
    public void FromTextureConnectingMetadataMapsEveryTexelFormat(
        TexelFormat texelFormat,
        uint expectedInternalFormat,
        uint expectedBaseInternalFormat)
    {
        TextureConnectingMetadata connecting = new() { TexelTypeInfo = KtxTexelFormats.GetTexelTypeInfo(texelFormat) };

        KtxTextureMetadata metadata = KtxTextureMetadata.FromTextureConnectingMetadata(connecting);

        Assert.Equal(expectedInternalFormat, metadata.GlInternalFormat);
        Assert.Equal(expectedBaseInternalFormat, metadata.GlBaseInternalFormat);

        bool compressed = texelFormat != TexelFormat.R8G8B8A8_UNorm;
        Assert.Equal(compressed ? 0u : (uint)GlType.UnsignedByte, metadata.GlType);
        Assert.Equal(compressed ? 0u : (uint)GlFormat.Rgba, metadata.GlFormat);
        Assert.Equal(1u, metadata.GlTypeSize);
        Assert.Empty(metadata.KeyValueData);
    }

    [Fact]
    public void DeepCloneCopiesKeyValueDataAndPairs()
    {
        byte[] keyValueData = KtxFileBuilder.CreateKeyValueBlock(
            false,
            ("KTXwriter", "tests"u8.ToArray()));

        KtxTextureMetadata metadata = new()
        {
            GlInternalFormat = 0x8058,
            KeyValueData = keyValueData,
            KeyValuePairs = [new KtxKeyValuePair("KTXwriter", "tests"u8.ToArray())]
        };

        KtxTextureMetadata clone = metadata.DeepClone();
        clone.KeyValueData[0] ^= 0xFF;
        clone.KeyValuePairs[0].Value[0] ^= 0xFF;

        Assert.NotEqual(metadata.KeyValueData[0], clone.KeyValueData[0]);
        Assert.NotEqual(metadata.KeyValuePairs[0].Value[0], clone.KeyValuePairs[0].Value[0]);
        Assert.Equal(metadata.GlInternalFormat, clone.GlInternalFormat);
    }

    [Fact]
    public void DefaultsAreEmpty()
    {
        KtxTextureMetadata metadata = KtxFormat.Instance.CreateDefaultFormatMetadata();

        Assert.Empty(metadata.KeyValueData);
        Assert.Empty(metadata.KeyValuePairs);
        Assert.Equal(1u, metadata.FaceCount);
        Assert.Equal(1u, metadata.MipLevelCount);
        Assert.Equal(1u, metadata.GlTypeSize);
        Assert.Equal(KtxEndianness.LittleEndian, metadata.Endianness);
    }

    private static Texture Decode(byte[] file)
    {
        using MemoryStream stream = new(file);
        return Texture.Load(stream, new KtxTextureDecoder());
    }
}
