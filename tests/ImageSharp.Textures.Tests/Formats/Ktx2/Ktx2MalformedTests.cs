using System.Buffers.Binary;
using ImageSharp.Textures.Formats.Ktx2;
using ImageSharp.Textures.Tests.TestUtilities;
using ImageSharp.Textures.Tests.TestUtilities.Ktx2;

namespace ImageSharp.Textures.Tests.Formats.Ktx2;

[Trait("Category", "Ktx2")]
public class Ktx2MalformedTests
{
    private const uint VkFormatRgba8 = 37;

    [Theory]
    [InlineData(1u, "BasisLZ")]
    [InlineData(2u, "Zstandard")]
    [InlineData(3u, "ZLIB")]
    public void RejectsSupercompressedFiles(uint scheme, string schemeName)
    {
        byte[] header = CreateHeader(scheme, 0);

        using MemoryStream stream = new(header);
        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            new Ktx2TextureDecoder().Decode(TextureConfiguration.Default, stream));

        Assert.Contains("supercompression", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(schemeName, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsSupercompressionGlobalData()
    {
        byte[] header = CreateHeader(0, 8);

        using MemoryStream stream = new(header);
        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() =>
            new Ktx2TextureDecoder().Decode(TextureConfiguration.Default, stream));

        Assert.Contains("global data", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsLevelOffsetsOutsideTheFile()
    {
        byte[] file = CreateValidFile();
        BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(80), (ulong)file.Length + 8);

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Decode(file));
        Assert.Contains("level index", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsLevelLengthMismatches()
    {
        byte[] file = CreateValidFile();
        BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(88), 12);

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Decode(file));
        Assert.Contains("requires 16 bytes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsUncompressedLengthMismatches()
    {
        byte[] file = CreateValidFile();
        BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(96), 20);

        Assert.Throws<InvalidImageContentException>(() => Decode(file));
    }

    [Fact]
    public void RejectsDfdColorModelMismatch()
    {
        byte[] file = CreateValidFile();
        int dfdOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(48));
        file[dfdOffset + 12] = 128;

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Decode(file));
        Assert.Contains("color model", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsDfdLengthMismatch()
    {
        byte[] file = CreateValidFile();
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(52), 44);

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Decode(file));
        Assert.Contains("total size", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsDfdOutsideTheFile()
    {
        byte[] file = CreateValidFile();
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(48), (uint)file.Length);

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Decode(file));
        Assert.Contains("data format descriptor", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsKeyValueOffsetWithoutData()
    {
        byte[] file = CreateValidFile();
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(56), 104);

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Decode(file));
        Assert.Contains("kvdByteOffset", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsTruncatedFiles()
    {
        byte[] valid = CreateValidFile();
        byte[] truncated = valid[..^4];

        Assert.Throws<InvalidImageContentException>(() => Decode(truncated));
    }

    [Fact]
    public void RejectsMissingIdentifier()
    {
        byte[] file = CreateValidFile();
        file[0] = 0;

        Assert.Throws<InvalidImageContentException>(() => Decode(file));
    }

    [Fact]
    public void RejectsMultiByteTypeSize()
    {
        byte[] file = CreateValidFile();
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(16), 2);

        Assert.Throws<NotSupportedException>(() => Decode(file));
    }

    [Fact]
    public void RejectsVkFormatUndefined()
    {
        byte[] file = CreateValidFile();
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(12), 0);

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => Decode(file));
        Assert.Contains("vkFormat 0", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(135u)]
    [InlineData(143u)]
    [InlineData(147u)]
    [InlineData(157u)]
    public void RejectsUnsupportedVkFormatsNamingTheValue(uint vkFormat)
    {
        byte[] file = CreateValidFile();
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(12), vkFormat);

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => Decode(file));
        Assert.Contains($"vkFormat {vkFormat}", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsOneDimensionalTextures()
    {
        byte[] file = CreateValidFile();
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(24), 0);

        Assert.Throws<NotSupportedException>(() => Decode(file));
    }

    [Fact]
    public void RejectsLevelCountBeyondTheMaximum()
    {
        byte[] file = CreateValidFile();
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(40), 40);

        Assert.Throws<InvalidImageContentException>(() => Decode(file));
    }

    [Fact]
    public void RejectsNonSeekableStreams()
    {
        byte[] file = CreateValidFile();

        using NonSeekableStream stream = new(new MemoryStream(file));
        Assert.Throws<NotSupportedException>(() =>
            new Ktx2TextureDecoder().Decode(TextureConfiguration.Default, stream));
    }

    private static byte[] CreateValidFile()
    {
        byte[] dfd = Ktx2DecodeTests.CreateDfd(VkFormatRgba8);
        Ktx2FileBuilder builder = Ktx2FileBuilder.Create(VkFormatRgba8, 2, 2, 0);
        builder.DataFormatDescriptor = dfd;
        builder.AddPayload(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15);

        return builder.Build();
    }

    private static byte[] CreateHeader(uint supercompressionScheme, ulong sgdByteLength)
    {
        byte[] header = new byte[80];
        Ktx2Constants.Identifier.CopyTo(header);

        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12), VkFormatRgba8);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(20), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(24), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(28), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(32), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(36), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(40), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(44), supercompressionScheme);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(48), 104);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(52), 92);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(56), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(60), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(64), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(72), sgdByteLength);

        return header;
    }

    private static Texture Decode(byte[] file)
    {
        using MemoryStream stream = new(file);
        return Texture.Load(stream, new Ktx2TextureDecoder());
    }
}
