using System.Buffers.Binary;
using ImageSharp.Textures.Formats.Ktx2;
using ImageSharp.Textures.Tests.TestUtilities;
using ImageSharp.Textures.Tests.TestUtilities.Ktx2;
using ImageSharp.Textures.Tests.TestUtilities.Ktx;

namespace ImageSharp.Textures.Tests.Formats.Ktx2;

[Trait("Category", "Ktx2")]
public class Ktx2EncodeTests
{
    private const uint VkFormatRgba8 = 37;

    private const string Rgba8DfdHex =
        "5C000000" + "00000000" + "02005800" + "01010100" + "00000000" + "04000000" + "00000000"
        + "000007000000000000000000FF000000"
        + "080007010000000000000000FF000000"
        + "100007020000000000000000FF000000"
        + "1800070F0000000000000000FF000000";

    [Fact]
    public void EncodeRgba8WritesGoldenFile()
    {
        byte[] payload = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
        using Texture texture = KtxTestTextures.CreateSingleSurface(2, 2, TexelFormat.R8G8B8A8_UNorm, payload);

        byte[] file = Encode(texture);

        string expected =
            "AB4B5458203230BB0D0A1A0A" // identifier
            + "25000000" // vkFormat = 37
            + "01000000" // typeSize = 1
            + "02000000" // pixelWidth = 2
            + "02000000" // pixelHeight = 2
            + "00000000" // pixelDepth = 0
            + "00000000" // layerCount = 0
            + "01000000" // faceCount = 1
            + "01000000" // levelCount = 1
            + "00000000" // supercompressionScheme = 0
            + "68000000" // dfdByteOffset = 104
            + "5C000000" // dfdByteLength = 92
            + "00000000" // kvdByteOffset = 0
            + "00000000" // kvdByteLength = 0
            + "0000000000000000" // sgdByteOffset = 0
            + "0000000000000000" // sgdByteLength = 0
            + "C400000000000000" // levels[0].byteOffset = 196
            + "1000000000000000" // levels[0].byteLength = 16
            + "1000000000000000" // levels[0].uncompressedByteLength = 16
            + Rgba8DfdHex
            + "000102030405060708090A0B0C0D0E0F";

        Assert.Equal(expected, Convert.ToHexString(file));
    }

    [Fact]
    public void EncodeWritesTheLevelIndexFromBaseToSmallest()
    {
        using Texture texture = KtxTestTextures.Create(8, 8, TexelFormat.R8G8B8A8_UNorm, 3);

        byte[] file = Encode(texture);

        Assert.Equal(3u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(40)));

        long baseOffset = (long)BinaryPrimitives.ReadUInt64LittleEndian(file.AsSpan(80));
        long middleOffset = (long)BinaryPrimitives.ReadUInt64LittleEndian(file.AsSpan(104));
        long smallestOffset = (long)BinaryPrimitives.ReadUInt64LittleEndian(file.AsSpan(128));

        Assert.True(baseOffset > middleOffset);
        Assert.True(middleOffset > smallestOffset);
        Assert.Equal(file.Length, baseOffset + (8 * 8 * 4));
        Assert.Equal(smallestOffset + (2 * 2 * 4), middleOffset);

        using Texture decoded = Decode(file);
        Assert.Equal(3, decoded.MipLevels.Count);
        Assert.Equal(8, decoded.MipLevels[0].Width);
        Assert.Equal(2, decoded.MipLevels[2].Width);

        for (int level = 0; level < 3; level++)
        {
            KtxTestTextures.AssertSurfacePayload(
                decoded.MipLevels[level].Surfaces[0],
                KtxTestTextures.GetSurfaceValue(0, level));
        }
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

        byte[] first = Encode(texture, new Ktx2EncodingOptions { LayerCount = 2, FaceCount = 6 });

        using Texture decoded = Decode(first);
        byte[] second = Encode(decoded);

        Assert.Equal(Convert.ToHexString(first), Convert.ToHexString(second));
    }

    [Fact]
    public void EncodeArrayCubeWritesLayerAndFaceCounts()
    {
        using Texture texture = KtxTestTextures.Create(4, 4, TexelFormat.R8G8B8A8_UNorm, layerCount: 2, faceCount: 6);

        byte[] file = Encode(texture, new Ktx2EncodingOptions { LayerCount = 2, FaceCount = 6 });

        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(32)));
        Assert.Equal(6u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(36)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(28)));

        using Texture decoded = Decode(file);
        Assert.Equal(12, decoded.MipLevels[0].Surfaces.Count);
    }

    [Fact]
    public void EncodeNonArrayCubeWritesZeroLayerCount()
    {
        using Texture texture = KtxTestTextures.Create(4, 4, TexelFormat.R8G8B8A8_UNorm, faceCount: 6);

        byte[] file = Encode(texture, new Ktx2EncodingOptions { FaceCount = 6 });

        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(32)));
        Assert.Equal(6u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(36)));

        using Texture decoded = Decode(file);
        Assert.Equal(6, decoded.MipLevels[0].Surfaces.Count);
    }

    [Fact]
    public void EncodeVolumeWritesPixelDepth()
    {
        using Texture texture = KtxTestTextures.Create(2, 2, TexelFormat.R8G8B8A8_UNorm, depth: 3);

        byte[] file = Encode(texture);

        Assert.Equal(3u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(28)));

        using Texture decoded = Decode(file);
        Assert.Equal(3, decoded.MipLevels[0].Depth);
        Assert.Equal(3, decoded.MipLevels[0].Surfaces.Count);
    }

    [Fact]
    public void EncodeRejectsNonPyramidMipLevels()
    {
        using Texture texture = KtxTestTextures.Create(4, 4, TexelFormat.R8G8B8A8_UNorm);
        texture.AddMipLevel(3, 3, 1).CreateSurface(TexelFormat.R8G8B8A8_UNorm).GetPayload().Fill(9);

        using MemoryStream stream = new();
        Assert.Throws<InvalidOperationException>(() => texture.Save(stream, Ktx2Format.Instance));
    }

    [Fact]
    public void EncodeRejectsSurfaceCountThatDoesNotMatchTheLayout()
    {
        using Texture texture = KtxTestTextures.Create(2, 2, TexelFormat.R8G8B8A8_UNorm, layerCount: 3);
        Ktx2TextureEncoder encoder = new(new Ktx2EncodingOptions { FaceCount = 2 });

        using MemoryStream stream = new();
        Assert.Throws<InvalidOperationException>(() => texture.Save(stream, encoder));
    }

    [Fact]
    public void EncodeDoesNotMutateTheTexture()
    {
        using Texture texture = KtxTestTextures.Create(2, 2, TexelFormat.R8G8B8A8_UNorm);
        byte[] payload = texture.MipLevels[0].Surfaces[0].GetPayload().ToArray();

        _ = Encode(texture);

        Assert.Equal(payload, texture.MipLevels[0].Surfaces[0].GetPayload().ToArray());
    }

    [Fact]
    public async Task EncodeAsyncMatchesEncode()
    {
        using Texture texture = KtxTestTextures.Create(2, 2, TexelFormat.R8G8B8A8_UNorm);

        byte[] synchronous = Encode(texture);

        using MemoryStream stream = new();
        await new Ktx2TextureEncoder().EncodeAsync(texture, stream);

        Assert.Equal(Convert.ToHexString(synchronous), Convert.ToHexString(stream.ToArray()));
    }

    [Fact]
    public void EncodeKeyValueDataCanBeSuppressed()
    {
        byte[] keyValueData = KtxFileBuilder.CreateKeyValueBlock(
            false,
            ("KTXwriter", "tests"u8.ToArray()));

        Ktx2FileBuilder builder = Ktx2FileBuilder.Create(VkFormatRgba8, 1, 1, 0);
        builder.DataFormatDescriptor = Ktx2DataFormatDescriptor.Create(
            Ktx2TexelFormats.GetFormatInfo(TexelFormat.R8G8B8A8_UNorm));
        builder.KeyValueData = keyValueData;
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder.Build());
        Assert.Equal(keyValueData.Length, texture.Metadata.GetFormatMetadata(Ktx2Format.Instance).KeyValueData.Length);

        using MemoryStream stream = new();
        texture.Save(stream, new Ktx2TextureEncoder(new Ktx2EncodingOptions { WriteKeyValueData = false }));

        byte[] file = stream.ToArray();
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(56)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(60)));
    }

    [BcFact(TexelFormat.Bc1_UNorm)]
    public void EncodeWithTargetTexelFormatConvertsEverySurface()
    {
        using Texture texture = KtxTestTextures.Create(8, 8, TexelFormat.R8G8B8A8_UNorm);
        Ktx2TextureEncoder encoder = new(new Ktx2EncodingOptions { TargetTexelFormat = TexelFormat.Bc1_UNorm });

        using MemoryStream stream = new();
        texture.Save(stream, encoder);

        byte[] file = stream.ToArray();
        Assert.Equal(133u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(12)));

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
        Assert.Throws<InvalidOperationException>(() => texture.Save(stream, Ktx2Format.Instance));
    }

    [Fact]
    public void EncoderExposesTheConfiguredOptions()
    {
        Ktx2TextureEncoder defaultEncoder = new();
        Assert.Null(defaultEncoder.Options.TargetTexelFormat);
        Assert.Null(defaultEncoder.Options.LayerCount);
        Assert.Null(defaultEncoder.Options.FaceCount);
        Assert.True(defaultEncoder.Options.WriteKeyValueData);

        Ktx2EncodingOptions options = new()
        {
            TargetTexelFormat = TexelFormat.Bc7_UNorm, LayerCount = 2, FaceCount = 6, WriteKeyValueData = false
        };

        Ktx2TextureEncoder configured = new(options);
        Assert.Same(options, configured.Options);
        Assert.Same(Ktx2Format.Instance, configured.Format);
    }

    [BcFact(TexelFormat.Bc7_UNorm)]
    public void EncodePreservesBlockCompressedPayloadsWithoutATarget()
    {
        using Texture texture = KtxTestTextures.Create(8, 8, TexelFormat.Bc7_UNorm);
        TextureSurface surface = texture.MipLevels[0].Surfaces[0];
        Span<byte> source = surface.GetPayload();
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = (byte)((i * 7) + 3);
        }

        byte[] payload = surface.GetPayload().ToArray();

        byte[] file = Encode(texture);

        Assert.Equal(145u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(12)));

        using Texture decoded = Decode(file);
        Assert.Equal(TexelFormat.Bc7_UNorm, decoded.MipLevels[0].Surfaces[0].TexelFormat);
        Assert.Equal(payload, decoded.MipLevels[0].Surfaces[0].GetPayload().ToArray());
    }

    private static byte[] Encode(Texture texture, Ktx2EncodingOptions? options = null)
    {
        using MemoryStream stream = new();
        texture.Save(stream, options is null ? new Ktx2TextureEncoder() : new Ktx2TextureEncoder(options));
        return stream.ToArray();
    }

    private static Texture Decode(byte[] file)
    {
        using MemoryStream stream = new(file);
        return Texture.Load(stream, new Ktx2TextureDecoder());
    }
}
