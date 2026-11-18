using System.Buffers.Binary;
using ImageSharp.Textures.Formats.Ktx;
using ImageSharp.Textures.Tests.TestUtilities;
using ImageSharp.Textures.Tests.TestUtilities.Ktx;

namespace ImageSharp.Textures.Tests.Formats.Ktx;

[Trait("Category", "Ktx")]
public class KtxDecodeTests
{
    [Fact]
    public void DecodesRgba8Pixels()
    {
        byte[] payload = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        KtxFileBuilder builder = KtxFileBuilder.CreateRgba8(2, 2);
        builder.AddPayload(payload);

        using Texture texture = Decode(builder.Build());
        TextureSurface surface = texture.MipLevels[0].Surfaces[0];

        Assert.Equal(TexelFormat.R8G8B8A8_UNorm, surface.TexelFormat);
        Assert.Same(KtxFormat.Instance, texture.Metadata.DecodedTextureFormat);

        using Image<Rgba32> image = surface.ToImage<Rgba32>();
        Assert.Equal(new Rgba32(1, 2, 3, 4), image[0, 0]);
        Assert.Equal(new Rgba32(5, 6, 7, 8), image[1, 0]);
        Assert.Equal(new Rgba32(9, 10, 11, 12), image[0, 1]);
        Assert.Equal(new Rgba32(13, 14, 15, 16), image[1, 1]);
    }

    [Fact]
    public void DecodesMipLevelsFromLargestToSmallest()
    {
        KtxFileBuilder builder = KtxFileBuilder.CreateRgba8(4, 4);
        builder.MipLevelCount = 3;
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder.Build());

        Assert.Equal(3, texture.MipLevels.Count);
        Assert.Equal(4, texture.MipLevels[0].Width);
        Assert.Equal(2, texture.MipLevels[1].Width);
        Assert.Equal(1, texture.MipLevels[2].Width);

        for (int level = 0; level < 3; level++)
        {
            KtxTestTextures.AssertSurfacePayload(
                texture.MipLevels[level].Surfaces[0],
                KtxTestTextures.GetSurfaceValue(0, level));
        }
    }

    [Fact]
    public void DecodesArrayElementsAndFacesInFileOrder()
    {
        KtxFileBuilder builder = KtxFileBuilder.CreateRgba8(4, 4);
        builder.ArrayElementCount = 2;
        builder.FaceCount = 6;
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder.Build());
        TextureMipLevel mipLevel = texture.MipLevels[0];

        Assert.Equal(12, mipLevel.Surfaces.Count);
        Assert.Equal(1, mipLevel.Depth);

        for (int surface = 0; surface < 12; surface++)
        {
            KtxTestTextures.AssertSurfacePayload(
                mipLevel.Surfaces[surface],
                KtxTestTextures.GetSurfaceValue(0, surface));
        }
    }

    [Fact]
    public void DecodesNonArrayCubemapFaces()
    {
        KtxFileBuilder builder = KtxFileBuilder.CreateRgba8(4, 4);
        builder.FaceCount = 6;
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder.Build());
        TextureMipLevel mipLevel = texture.MipLevels[0];

        Assert.Equal(6, mipLevel.Surfaces.Count);

        for (int surface = 0; surface < 6; surface++)
        {
            KtxTestTextures.AssertSurfacePayload(
                mipLevel.Surfaces[surface],
                KtxTestTextures.GetSurfaceValue(0, surface));
        }
    }

    [Fact]
    public void DecodesVolumeSlices()
    {
        KtxFileBuilder builder = KtxFileBuilder.CreateRgba8(2, 2);
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
    public void DecodesBigEndianFiles()
    {
        KtxFileBuilder littleEndian = KtxFileBuilder.CreateRgba8(2, 2);
        littleEndian.AddPatternPayloads(7);

        KtxFileBuilder bigEndian = KtxFileBuilder.CreateRgba8(2, 2);
        bigEndian.BigEndian = true;
        bigEndian.AddPatternPayloads(7);

        byte[] littleFile = littleEndian.Build();
        byte[] bigFile = bigEndian.Build();

        Assert.Equal(0x04, bigFile[12]);
        Assert.Equal(0x03, bigFile[13]);
        Assert.Equal(0x02, bigFile[14]);
        Assert.Equal(0x01, bigFile[15]);

        using Texture fromLittle = Decode(littleFile);
        using Texture fromBig = Decode(bigFile);

        Assert.Equal(TexelFormat.R8G8B8A8_UNorm, fromBig.MipLevels[0].Surfaces[0].TexelFormat);
        Assert.Equal(
            fromLittle.MipLevels[0].Surfaces[0].GetPayload().ToArray(),
            fromBig.MipLevels[0].Surfaces[0].GetPayload().ToArray());
        Assert.Equal(KtxEndianness.BigEndian, fromBig.Metadata.GetFormatMetadata(KtxFormat.Instance).Endianness);
    }

    [Fact]
    public void MipLevelCountZeroMeansOneLevel()
    {
        KtxFileBuilder builder = KtxFileBuilder.CreateRgba8(2, 2);
        builder.MipLevelCount = 0;
        builder.AddPatternPayloads();

        byte[] file = builder.Build();
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(56)));

        using Texture texture = Decode(file);
        Assert.Equal(1, texture.MipLevels.Count);
        Assert.Equal(1, texture.MipLevels[0].Surfaces.Count);

        using MemoryStream stream = new(file);
        TextureInfo info = new KtxTextureDecoder().Identify(TextureConfiguration.Default, stream);
        Assert.Equal(1, info.MipLevelCount);
        Assert.Equal(1, info.SurfaceCount);
    }

    [BcTheory(TexelFormat.Bc1_UNorm)]
    [InlineData(0x83F1u, TexelFormat.Bc1_UNorm, 8)]
    [InlineData(0x8C4Du, TexelFormat.Bc1_UnormSrgb, 8)]
    [InlineData(0x8DBBu, TexelFormat.Bc4_UNorm, 8)]
    [InlineData(0x8DBDu, TexelFormat.Bc5_UNorm, 16)]
    [InlineData(0x8E8Cu, TexelFormat.Bc7_UNorm, 16)]
    [InlineData(0x8E8Du, TexelFormat.Bc7_UnormSrgb, 16)]
    public void DecodesBlockCompressedPayloadVerbatim(uint glInternalFormat, TexelFormat texelFormat, int blockBytes)
    {
        KtxFileBuilder builder = KtxFileBuilder.CreateCompressed(glInternalFormat, 8, 8, blockBytes);
        builder.AddPatternPayloads(3);

        using Texture texture = Decode(builder.Build());
        TextureSurface surface = texture.MipLevels[0].Surfaces[0];

        Assert.Equal(texelFormat, surface.TexelFormat);
        uint expectedSize = (uint)((8 + 3) / 4 * ((8 + 3) / 4) * blockBytes);
        Assert.Equal(expectedSize, (uint)surface.PayloadLength);
        KtxTestTextures.AssertSurfacePayload(surface, 3);
    }

    [Fact]
    public void IdentifyReportsTheHeaderLayout()
    {
        KtxFileBuilder builder = KtxFileBuilder.CreateRgba8(8, 4);
        builder.MipLevelCount = 3;
        builder.ArrayElementCount = 2;
        builder.FaceCount = 6;
        builder.AddPatternPayloads();
        byte[] file = builder.Build();

        using MemoryStream stream = new(file);
        TextureInfo info = new KtxTextureDecoder().Identify(TextureConfiguration.Default, stream);

        Assert.Equal(8, info.Width);
        Assert.Equal(4, info.Height);
        Assert.Equal(1, info.Depth);
        Assert.Equal(3, info.MipLevelCount);
        Assert.Equal(36, info.SurfaceCount);
        Assert.Equal(TexelFormat.R8G8B8A8_UNorm, info.TexelFormat);
        Assert.Equal(0, stream.Position - 64);
    }

    [BcFact(TexelFormat.Bc1_UNorm)]
    public void IdentifyReportsBlockCompressedHeaders()
    {
        KtxFileBuilder builder = KtxFileBuilder.CreateCompressed(0x83F1, 64, 64, 8);
        builder.MipLevelCount = 2;
        builder.Depth = 2;
        builder.AddPatternPayloads();
        byte[] file = builder.Build();

        using MemoryStream stream = new(file);
        TextureInfo info = new KtxTextureDecoder().Identify(TextureConfiguration.Default, stream);

        Assert.Equal(64, info.Width);
        Assert.Equal(64, info.Height);
        Assert.Equal(2, info.Depth);
        Assert.Equal(2, info.MipLevelCount);
        Assert.Equal(2 + 1, info.SurfaceCount);
        Assert.Equal(TexelFormat.Bc1_UNorm, info.TexelFormat);
    }

    [Fact]
    public async Task DecodeAsyncMatchesDecode()
    {
        KtxFileBuilder builder = KtxFileBuilder.CreateRgba8(2, 2);
        builder.AddPatternPayloads(5);
        byte[] file = builder.Build();

        using MemoryStream stream = new(file);
        using Texture texture = await new KtxTextureDecoder().DecodeAsync(TextureConfiguration.Default, stream);

        KtxTestTextures.AssertSurfacePayload(texture.MipLevels[0].Surfaces[0], 5);
    }

    [Fact]
    public void DecodesNonSeekableStreamsSequentially()
    {
        KtxFileBuilder builder = KtxFileBuilder.CreateRgba8(2, 2);
        builder.MipLevelCount = 2;
        builder.AddPatternPayloads(6);

        using NonSeekableStream stream = new(new MemoryStream(builder.Build()));
        using Texture texture = new KtxTextureDecoder().Decode(TextureConfiguration.Default, stream);

        Assert.Equal(2, texture.MipLevels.Count);
        KtxTestTextures.AssertSurfacePayload(texture.MipLevels[0].Surfaces[0], 6);
        KtxTestTextures.AssertSurfacePayload(texture.MipLevels[1].Surfaces[0], 7);
    }

    [Fact]
    public void DecodesKeyValueData()
    {
        byte[] keyValueData = KtxFileBuilder.CreateKeyValueBlock(
            false,
            ("KTXorientation", "S=r,T=d"u8.ToArray()),
            ("api", [0x67, 0x6C, 0x65, 0x73, 0x32]));

        KtxFileBuilder builder = KtxFileBuilder.CreateRgba8(1, 1);
        builder.KeyValueData = keyValueData;
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder.Build());
        KtxTextureMetadata metadata = texture.Metadata.GetFormatMetadata(KtxFormat.Instance);

        Assert.Equal(keyValueData, metadata.KeyValueData);
        Assert.Equal(2, metadata.KeyValuePairs.Count);
        Assert.Equal("KTXorientation", metadata.KeyValuePairs[0].Key);
        Assert.Equal("S=r,T=d"u8.ToArray(), metadata.KeyValuePairs[0].Value);
        Assert.Equal("api", metadata.KeyValuePairs[1].Key);
        Assert.Equal(new byte[] { 0x67, 0x6C, 0x65, 0x73, 0x32 }, metadata.KeyValuePairs[1].Value);
    }

    [Fact]
    public void RejectsUnsupportedInternalFormat()
    {
        KtxFileBuilder builder = KtxFileBuilder.CreateCompressed(0x9274, 4, 4, 8);
        builder.AddPatternPayloads();

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => Decode(builder.Build()));
        Assert.Contains("0x9274", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsTruncatedPayload()
    {
        KtxFileBuilder builder = KtxFileBuilder.CreateRgba8(4, 4);
        builder.AddPatternPayloads();
        byte[] valid = builder.Build();

        byte[] truncated = valid[..^8];
        Assert.Throws<InvalidImageContentException>(() => Decode(truncated));
    }

    [Fact]
    public void RejectsMismatchedImageSize()
    {
        KtxFileBuilder builder = KtxFileBuilder.CreateRgba8(2, 2);
        builder.AddPatternPayloads();
        byte[] file = builder.Build();

        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(64), 12);

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Decode(file));
        Assert.Contains("imageSize", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsMissingIdentifier()
    {
        KtxFileBuilder builder = KtxFileBuilder.CreateRgba8(2, 2);
        builder.IncludeIdentifier = false;
        builder.AddPatternPayloads();

        Assert.Throws<InvalidImageContentException>(() => Decode(builder.Build()));
    }

    [Fact]
    public void RejectsInvalidEndiannessWord()
    {
        KtxFileBuilder builder = KtxFileBuilder.CreateRgba8(2, 2);
        builder.EndiannessWord = 0xDEADBEEF;
        builder.AddPatternPayloads();

        InvalidImageContentException exception =
            Assert.Throws<InvalidImageContentException>(() => Decode(builder.Build()));
        Assert.Contains("endianness", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsOneDimensionalTextures()
    {
        KtxFileBuilder builder = KtxFileBuilder.CreateRgba8(4, 1);
        builder.Height = 0;
        builder.AddPatternPayloads();

        Assert.Throws<NotSupportedException>(() => Decode(builder.Build()));
    }

    [Fact]
    public void RejectsCompressedTextureWithNonZeroGlType()
    {
        KtxFileBuilder builder = KtxFileBuilder.CreateCompressed(0x83F1, 4, 4, 8);
        builder.GlType = 0x1401;
        builder.AddPatternPayloads();

        Assert.Throws<InvalidImageContentException>(() => Decode(builder.Build()));
    }

    [Fact]
    public void RejectsUncompressedTextureWithZeroGlType()
    {
        KtxFileBuilder builder = KtxFileBuilder.CreateRgba8(4, 4);
        builder.GlType = 0;
        builder.GlFormat = 0;
        builder.AddPatternPayloads();

        Assert.Throws<NotSupportedException>(() => Decode(builder.Build()));
    }

    private static Texture Decode(byte[] file)
    {
        using MemoryStream stream = new(file);
        return Texture.Load(stream, new KtxTextureDecoder());
    }
}
