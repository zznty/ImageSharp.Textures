using ImageSharp.Textures.Tests.TestUtilities;

namespace ImageSharp.Textures.Tests.Core;

[Trait("Category", "Core")]
public class TextureLoadSaveTests
{
    private static readonly byte[] Magic = [0x54, 0x46, 0x58, 0x01];

    [Fact]
    public void LoadPathResolvesTheRegisteredDecoderByExtension()
    {
        TextureConfiguration configuration = TestTextureFactory.CreateConfiguration();
        TestTextureFormat format = new("TEST", "image/x-test", ".test");
        TestTextureDecoder decoder = new(format);
        configuration.Formats.AddFormat(format, decoder);

        string path = CreateTempFile("sample.test", Magic);
        try
        {
            using Texture texture = Texture.Load(configuration, path);

            Assert.Equal(1, decoder.DecodeCount);
            Assert.Same(format, texture.Metadata.DecodedTextureFormat);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadPathFallsBackToHeaderDetectionForUnknownExtensions()
    {
        TextureConfiguration configuration = TestTextureFactory.CreateConfiguration();
        TestTextureFormat format = new("TEST", "image/x-test", ".test");
        TestTextureDecoder decoder = new(format);
        configuration.Formats.AddFormat(format, decoder, null, new TestTextureFormatDetector(format, Magic));

        string path = CreateTempFile("sample.bin", Magic);
        try
        {
            using Texture texture = Texture.Load(configuration, path);

            Assert.Equal(1, decoder.DecodeCount);
            Assert.Same(format, texture.Metadata.DecodedTextureFormat);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadPathWithUnknownExtensionThrowsUnknownTextureFormatException()
    {
        string path = CreateTempFile("sample.unknown-container", [0x00, 0x01, 0x02]);
        try
        {
            UnknownTextureFormatException exception =
                Assert.Throws<UnknownTextureFormatException>(() => Texture.Load(path));
            Assert.Contains("sample.unknown-container", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadNonSeekableStreamRequiresAnExplicitDecoder()
    {
        using MemoryStream content = new(Magic);
        using NonSeekableStream stream = new(content);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => Texture.Load(stream));
        Assert.Equal("stream", exception.ParamName);
    }

    [Fact]
    public void LoadSeekableStreamWithoutADetectorThrows()
    {
        using MemoryStream stream = new(Magic);

        Assert.Throws<UnknownTextureFormatException>(() => Texture.Load(stream));
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void LoadStreamUsesTheExplicitDecoder()
    {
        TestTextureFormat format = new("TEST", "image/x-test", ".test");
        TestTextureDecoder decoder = new(format);
        using MemoryStream stream = new(Magic);

        using Texture texture = Texture.Load(stream, decoder);

        Assert.Equal(1, decoder.DecodeCount);
        Assert.Same(format, texture.Metadata.DecodedTextureFormat);
    }

    [Fact]
    public async Task LoadAsyncStreamUsesTheExplicitDecoder()
    {
        TestTextureFormat format = new("TEST", "image/x-test", ".test");
        TestTextureDecoder decoder = new(format);
        using MemoryStream stream = new(Magic);

        using Texture texture = await Texture.LoadAsync(stream, decoder);

        Assert.Equal(1, decoder.DecodeCount);
        Assert.Same(format, texture.Metadata.DecodedTextureFormat);
    }

    [Fact]
    public void SavePathResolvesTheRegisteredEncoderByExtension()
    {
        TextureConfiguration configuration = TestTextureFactory.CreateConfiguration();
        TestTextureFormat format = new("TEST", "image/x-test", ".test");
        TestTextureEncoder encoder = new(format);
        configuration.Formats.AddFormat(format, new TestTextureDecoder(format), encoder);

        using Texture texture = TestTextureFactory.CreateTexture(configuration, 4, 4);
        string path = Path.Combine(Path.GetTempPath(), $"imagesharp-textures-{Guid.NewGuid():N}.test");

        try
        {
            texture.Save(path);

            Assert.Equal(1, encoder.EncodeCount);
            Assert.Equal(new[] { TestTextureEncoder.Marker }, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SavePathWithUnknownExtensionThrowsUnknownTextureFormatException()
    {
        using Texture texture = TestTextureFactory.CreateTexture(TestTextureFactory.CreateConfiguration(), 4, 4);
        string path = Path.Combine(Path.GetTempPath(), $"imagesharp-textures-{Guid.NewGuid():N}.unknown-container");

        UnknownTextureFormatException
            exception = Assert.Throws<UnknownTextureFormatException>(() => texture.Save(path));
        Assert.Contains("unknown-container", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void SaveStreamWithoutARegisteredEncoderThrowsNotSupportedException()
    {
        using Texture texture = TestTextureFactory.CreateTexture(TextureConfiguration.Default, 4, 4);
        TestTextureFormat format = new("NOENCODER", "image/x-no-encoder", ".noencoder");
        using MemoryStream stream = new();

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => texture.Save(stream, format));
        Assert.Contains("NOENCODER", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public async Task SaveAsyncStreamWithoutARegisteredEncoderThrowsNotSupportedException()
    {
        using Texture texture = TestTextureFactory.CreateTexture(TextureConfiguration.Default, 4, 4);
        TestTextureFormat format = new("NOENCODER", "image/x-no-encoder", ".noencoder");
        using MemoryStream stream = new();

        await Assert.ThrowsAsync<NotSupportedException>(() => texture.SaveAsync(stream, format));
    }

    [Fact]
    public void SaveStreamUsesTheRegisteredEncoder()
    {
        TextureConfiguration configuration = TestTextureFactory.CreateConfiguration();
        TestTextureFormat format = new("TEST", "image/x-test", ".test");
        TestTextureEncoder encoder = new(format);
        configuration.Formats.AddFormat(format, new TestTextureDecoder(format), encoder);

        using Texture texture = TestTextureFactory.CreateTexture(configuration, 4, 4);
        using MemoryStream stream = new();

        texture.Save(stream, format);

        Assert.Equal(1, encoder.EncodeCount);
        Assert.Equal(new[] { TestTextureEncoder.Marker }, stream.ToArray());
    }

    private static string CreateTempFile(string fileName, byte[] content)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"imagesharp-textures-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, content);
        return path;
    }
}
