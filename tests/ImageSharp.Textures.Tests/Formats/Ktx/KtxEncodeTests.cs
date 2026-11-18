using System.Buffers.Binary;
using ImageSharp.Textures.Formats.Ktx;
using ImageSharp.Textures.Tests.TestUtilities;
using ImageSharp.Textures.Tests.TestUtilities.Ktx;

namespace ImageSharp.Textures.Tests.Formats.Ktx;

[Trait("Category", "Ktx")]
public class KtxEncodeTests
{
    private const int KtxHeaderLength = 64;

    [Fact]
    public void EncodeRgba8WritesGoldenFile()
    {
        byte[] payload = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
        using Texture texture = KtxTestTextures.CreateSingleSurface(2, 2, TexelFormat.R8G8B8A8_UNorm, payload);

        using MemoryStream stream = new();
        texture.Save(stream, KtxFormat.Instance);

        byte[] expected = Convert.FromHexString(
            "AB4B5458203131BB0D0A1A0A" // identifier
            + "01020304" // endianness, little-endian
            + "01140000" // glType = GL_UNSIGNED_BYTE
            + "01000000" // glTypeSize = 1
            + "08190000" // glFormat = GL_RGBA
            + "58800000" // glInternalFormat = GL_RGBA8
            + "08190000" // glBaseInternalFormat = GL_RGBA
            + "02000000" // pixelWidth = 2
            + "02000000" // pixelHeight = 2
            + "00000000" // pixelDepth = 0
            + "00000000" // numberOfArrayElements = 0
            + "01000000" // numberOfFaces = 1
            + "01000000" // numberOfMipmapLevels = 1
            + "00000000" // bytesOfKeyValueData = 0
            + "10000000" // imageSize = 16
            + "000102030405060708090A0B0C0D0E0F");

        Assert.Equal(Convert.ToHexString(expected), Convert.ToHexString(stream.ToArray()));
    }

    [Fact]
    public void EncodeWritesLittleEndianHeaderAndPayload()
    {
        byte[] payload = [1, 2, 3, 4];
        using Texture texture = KtxTestTextures.CreateSingleSurface(1, 1, TexelFormat.R8G8B8A8_UNorm, payload);

        byte[] file = Encode(texture);

        Assert.Equal(0x01, file[12]);
        Assert.Equal(0x02, file[13]);
        Assert.Equal(0x03, file[14]);
        Assert.Equal(0x04, file[15]);

        // The single surface follows the imageSize field.
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, file[^4..]);
    }

    [Fact]
    public void EncodeWritesMipChainFromLargestToSmallest()
    {
        using Texture texture = KtxTestTextures.Create(4, 4, TexelFormat.R8G8B8A8_UNorm, 3);

        byte[] file = Encode(texture);

        Assert.Equal(3u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(56)));
        Assert.Equal(4u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(36)));
        Assert.Equal(4u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(40)));

        int offset = KtxHeaderLength;
        int[] expectedSizes = [4 * 4 * 4, 2 * 2 * 4, 1 * 1 * 4];

        for (int level = 0; level < expectedSizes.Length; level++)
        {
            int imageSize = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(offset));
            Assert.Equal(expectedSizes[level], imageSize);
            offset += 4;

            byte expected = KtxTestTextures.GetSurfaceValue(0, level);
            for (int i = 0; i < imageSize; i++)
            {
                Assert.Equal(expected, file[offset + i]);
            }

            offset += imageSize;
        }

        Assert.Equal(file.Length, offset);
    }

    [Fact]
    public void EncodeRoundTripIsByteIdentical()
    {
        using Texture texture = KtxTestTextures.Create(
            8,
            8,
            TexelFormat.R8G8B8A8_UNorm,
            2,
            2,
            6);

        byte[] first = Encode(texture);

        using Texture decoded = Decode(first);
        byte[] second = Encode(decoded);

        Assert.Equal(Convert.ToHexString(first), Convert.ToHexString(second));
    }

    [Fact]
    public void EncodeArrayCubeWritesWholeLevelImageSize()
    {
        using Texture texture = KtxTestTextures.Create(
            4,
            4,
            TexelFormat.R8G8B8A8_UNorm,
            layerCount: 2,
            faceCount: 6);

        byte[] file = Encode(texture, new KtxEncodingOptions { LayerCount = 2, FaceCount = 6 });

        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(48)));
        Assert.Equal(6u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(52)));

        int imageSize = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(KtxHeaderLength));
        Assert.Equal(2 * 6 * 4 * 4 * 4, imageSize);

        using Texture decoded = Decode(file);
        Assert.Equal(12, decoded.MipLevels[0].Surfaces.Count);
    }

    [Fact]
    public void EncodeNonArrayCubeWritesPerFaceImageSize()
    {
        using Texture texture = KtxTestTextures.Create(4, 4, TexelFormat.R8G8B8A8_UNorm, faceCount: 6);

        byte[] file = Encode(texture, new KtxEncodingOptions { FaceCount = 6 });

        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(48)));
        Assert.Equal(6u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(52)));

        int imageSize = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(KtxHeaderLength));
        Assert.Equal(4 * 4 * 4, imageSize);

        using Texture decoded = Decode(file);
        Assert.Equal(6, decoded.MipLevels[0].Surfaces.Count);
    }

    [Fact]
    public void EncodeVolumeWritesPixelDepth()
    {
        using Texture texture = KtxTestTextures.Create(2, 2, TexelFormat.R8G8B8A8_UNorm, depth: 3);

        byte[] file = Encode(texture);

        Assert.Equal(3u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(44)));

        using Texture decoded = Decode(file);
        Assert.Equal(3, decoded.MipLevels[0].Depth);
        Assert.Equal(3, decoded.MipLevels[0].Surfaces.Count);
    }

    [Fact]
    public void EncodeHonoursLayerAndFaceCountOptions()
    {
        using Texture texture = KtxTestTextures.Create(2, 2, TexelFormat.R8G8B8A8_UNorm, layerCount: 2, faceCount: 6);
        byte[] file = Encode(texture, new KtxEncodingOptions { LayerCount = 2, FaceCount = 6 });

        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(48)));
        Assert.Equal(6u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(52)));
    }

    [Fact]
    public void EncodeRejectsSurfaceCountThatDoesNotMatchTheLayout()
    {
        using Texture texture = KtxTestTextures.Create(2, 2, TexelFormat.R8G8B8A8_UNorm, layerCount: 3);
        KtxTextureEncoder encoder = new(new KtxEncodingOptions { FaceCount = 2 });

        using MemoryStream stream = new();
        Assert.Throws<InvalidOperationException>(() => texture.Save(stream, encoder));
    }

    [Fact]
    public void EncodeRejectsNonPyramidMipLevels()
    {
        using Texture texture = KtxTestTextures.Create(4, 4, TexelFormat.R8G8B8A8_UNorm);
        TextureMipLevel mipLevel = texture.AddMipLevel(3, 3, 1);
        mipLevel.CreateSurface(TexelFormat.R8G8B8A8_UNorm).GetPayload().Fill(9);

        using MemoryStream stream = new();
        Assert.Throws<InvalidOperationException>(() => texture.Save(stream, KtxFormat.Instance));
    }

    [Fact]
    public async Task EncodeAsyncMatchesEncode()
    {
        using Texture texture = KtxTestTextures.Create(2, 2, TexelFormat.R8G8B8A8_UNorm);

        byte[] synchronous = Encode(texture);

        using MemoryStream stream = new();
        await new KtxTextureEncoder().EncodeAsync(texture, stream);

        Assert.Equal(Convert.ToHexString(synchronous), Convert.ToHexString(stream.ToArray()));
    }

    [Fact]
    public void EncodeDoesNotMutateTheTexture()
    {
        using Texture texture = KtxTestTextures.Create(2, 2, TexelFormat.R8G8B8A8_UNorm);
        byte[] payload = texture.MipLevels[0].Surfaces[0].GetPayload().ToArray();

        _ = Encode(texture);

        Assert.Equal(payload, texture.MipLevels[0].Surfaces[0].GetPayload().ToArray());
    }

    [BcFact(TexelFormat.Bc1_UNorm)]
    public void EncodeWritesZeroGlTypeAndFormatForCompressedFormats()
    {
        using Texture texture = KtxTestTextures.Create(8, 8, TexelFormat.Bc1_UNorm);

        byte[] file = Encode(texture);

        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(16)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(20)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(24)));
        Assert.Equal(0x83F1u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(28)));
        Assert.Equal(0x1908u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(32)));
    }

    [BcTheory(TexelFormat.Bc1_UnormSrgb)]
    [InlineData(TexelFormat.Bc1_UnormSrgb, 0x8C4Du, 0x1908u)]
    [InlineData(TexelFormat.Bc7_UnormSrgb, 0x8E8Du, 0x1908u)]
    [InlineData(TexelFormat.Bc4_UNorm, 0x8DBBu, 0x1903u)]
    [InlineData(TexelFormat.Bc5_UNorm, 0x8DBDu, 0x8227u)]
    [InlineData(TexelFormat.Bc7_UNorm, 0x8E8Cu, 0x1908u)]
    public void EncodeWritesTheInternalFormatOfEveryTexelFormat(
        TexelFormat texelFormat,
        uint expectedInternalFormat,
        uint expectedBaseInternalFormat)
    {
        using Texture texture = KtxTestTextures.Create(8, 8, texelFormat);

        byte[] file = Encode(texture);

        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(16)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(24)));
        Assert.Equal(expectedInternalFormat, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(28)));
        Assert.Equal(expectedBaseInternalFormat, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(32)));
    }

    [BcFact(TexelFormat.Bc1_UNorm)]
    public void EncodeWithTargetTexelFormatConvertsEverySurface()
    {
        using Texture texture = KtxTestTextures.Create(8, 8, TexelFormat.R8G8B8A8_UNorm);
        KtxTextureEncoder encoder = new(new KtxEncodingOptions { TargetTexelFormat = TexelFormat.Bc1_UNorm });

        using MemoryStream stream = new();
        texture.Save(stream, encoder);

        byte[] file = stream.ToArray();
        Assert.Equal(0x83F1u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(28)));

        using Texture decoded = Decode(file);
        Assert.Equal(TexelFormat.Bc1_UNorm, decoded.MipLevels[0].Surfaces[0].TexelFormat);
        Assert.Equal(TexelFormat.R8G8B8A8_UNorm, texture.MipLevels[0].Surfaces[0].TexelFormat);
    }

    [BcFact(TexelFormat.Bc4_UNorm)]
    public void EncodeRejectsMixedTexelFormatsWithoutATarget()
    {
        using Texture texture = KtxTestTextures.Create(4, 4, TexelFormat.R8G8B8A8_UNorm);
        texture.MipLevels[0].CreateSurface(TexelFormat.Bc4_UNorm).GetPayload().Fill(3);

        using MemoryStream stream = new();
        Assert.Throws<InvalidOperationException>(() => texture.Save(stream, KtxFormat.Instance));
    }

    [Fact]
    public void EncodeKeyValueDataCanBeSuppressed()
    {
        byte[] keyValueData = KtxFileBuilder.CreateKeyValueBlock(
            false,
            ("KTXorientation", "S=r,T=d"u8.ToArray()));

        KtxFileBuilder builder = KtxFileBuilder.CreateRgba8(1, 1).AddPatternPayloads();
        builder.KeyValueData = keyValueData;
        using Texture texture = Decode(builder.Build());

        Assert.Equal(keyValueData.Length, texture.Metadata.GetFormatMetadata(KtxFormat.Instance).KeyValueData.Length);

        using MemoryStream stream = new();
        texture.Save(stream, new KtxTextureEncoder(new KtxEncodingOptions { WriteKeyValueData = false }));

        byte[] file = stream.ToArray();
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(60)));
        Assert.Equal(KtxHeaderLength + 4 + 4, file.Length);
    }

    [Fact]
    public void EncoderExposesTheConfiguredOptions()
    {
        KtxTextureEncoder defaultEncoder = new();
        Assert.Null(defaultEncoder.Options.TargetTexelFormat);
        Assert.Null(defaultEncoder.Options.LayerCount);
        Assert.Null(defaultEncoder.Options.FaceCount);
        Assert.True(defaultEncoder.Options.WriteKeyValueData);

        KtxEncodingOptions options = new()
        {
            TargetTexelFormat = TexelFormat.Bc7_UNorm, LayerCount = 2, FaceCount = 6, WriteKeyValueData = false
        };

        KtxTextureEncoder configured = new(options);
        Assert.Same(options, configured.Options);
        Assert.Same(KtxFormat.Instance, configured.Format);
    }

    [BcFact(TexelFormat.Bc7_UNorm)]
    public void EncodePreservesBlockCompressedPayloadsWithoutATarget()
    {
        using Texture texture = KtxTestTextures.Create(8, 8, TexelFormat.Bc7_UNorm);
        TextureSurface surface = texture.MipLevels[0].Surfaces[0];
        FillRamp(surface);
        byte[] payload = surface.GetPayload().ToArray();

        byte[] file = Encode(texture);

        Assert.Equal(0x8E8Cu, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(28)));

        using Texture decoded = Decode(file);
        Assert.Equal(TexelFormat.Bc7_UNorm, decoded.MipLevels[0].Surfaces[0].TexelFormat);
        Assert.Equal(payload, decoded.MipLevels[0].Surfaces[0].GetPayload().ToArray());
    }

    private static void FillRamp(TextureSurface surface)
    {
        Span<byte> payload = surface.GetPayload();
        for (int i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)((i * 7) + 3);
        }
    }

    private static byte[] Encode(Texture texture, KtxEncodingOptions? options = null)
    {
        using MemoryStream stream = new();
        texture.Save(stream, options is null ? new KtxTextureEncoder() : new KtxTextureEncoder(options));
        return stream.ToArray();
    }

    private static Texture Decode(byte[] file)
    {
        using MemoryStream stream = new(file);
        return Texture.Load(stream, new KtxTextureDecoder());
    }
}
