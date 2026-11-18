using ImageSharp.Textures.Formats.Ktx;
using ImageSharp.Textures.Tests.TestUtilities.Ktx;

namespace ImageSharp.Textures.Tests.Formats.Ktx;

[Trait("Category", "Ktx")]
public class KtxRegistrationTests
{
    [Fact]
    public void DefaultConfigurationRegistersKtxWithDecoderEncoderAndDetector()
    {
        TextureFormatManager formats = TextureConfiguration.Default.Formats;

        ITextureFormat[] entries = formats.Formats.Where(format => format is KtxFormat).ToArray();
        ITextureFormat entry = Assert.Single(entries);
        Assert.Same(KtxFormat.Instance, entry);

        ITextureDecoder decoder = formats.GetDecoder(KtxFormat.Instance);
        Assert.IsType<KtxTextureDecoder>(decoder);
        Assert.Same(KtxFormat.Instance, decoder.Format);

        Assert.True(formats.TryGetEncoder(KtxFormat.Instance, out ITextureEncoder? encoder));
        Assert.IsType<KtxTextureEncoder>(encoder);
        Assert.Same(KtxFormat.Instance, encoder!.Format);
    }

    [Fact]
    public void FormatDescriptorDescribesTheContainer()
    {
        Assert.Equal("KTX", KtxFormat.Instance.Name);
        Assert.Equal("image/ktx", KtxFormat.Instance.DefaultMimeType);
        Assert.Equal(["image/ktx"], KtxFormat.Instance.MimeTypes);
        Assert.Equal([".ktx"], KtxFormat.Instance.FileExtensions);

        KtxTextureMetadata metadata = KtxFormat.Instance.CreateDefaultFormatMetadata();
        Assert.IsType<KtxTextureMetadata>(metadata);
        Assert.NotSame(metadata, KtxFormat.Instance.CreateDefaultFormatMetadata());
    }

    [Fact]
    public void DefaultConfigurationDetectsKtxFromTheIdentifier()
    {
        KtxFileBuilder builder = KtxFileBuilder.CreateRgba8(2, 2);
        builder.AddPatternPayloads();
        byte[] file = builder.Build();

        Assert.Same(KtxFormat.Instance, TextureConfiguration.Default.Formats.DetectFormat(file.AsSpan(0, 16)));

        byte[] ktx2Identifier = Convert.FromHexString("AB4B5458203230BB0D0A1A0A");
        Assert.Null(new KtxFormatDetector().DetectFormat(ktx2Identifier));
    }

    [Fact]
    public void LoadStreamDetectsTheKtxDecoder()
    {
        KtxFileBuilder builder = KtxFileBuilder.CreateRgba8(2, 2);
        builder.AddPatternPayloads(4);

        using MemoryStream stream = new(builder.Build());
        using Texture texture = Texture.Load(stream);

        Assert.Same(KtxFormat.Instance, texture.Metadata.DecodedTextureFormat);
        KtxTestTextures.AssertSurfacePayload(texture.MipLevels[0].Surfaces[0], 4);
    }

    [Fact]
    public void SaveAndLoadPathRoundTrips()
    {
        using Texture texture = KtxTestTextures.Create(4, 4, TexelFormat.R8G8B8A8_UNorm, 2);

        string directory = Path.Combine(Path.GetTempPath(), "imagesharp-textures-ktx", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "texture.ktx");
            texture.Save(path);
            Assert.True(File.Exists(path));

            using Texture reloaded = Texture.Load(path);
            Assert.Equal(2, reloaded.MipLevels.Count);
            Assert.Equal(texture.MipLevels[1].Width, reloaded.MipLevels[1].Width);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task SaveAsyncStreamUsesTheRegisteredEncoder()
    {
        using Texture texture = KtxTestTextures.Create(2, 2, TexelFormat.R8G8B8A8_UNorm);

        using MemoryStream stream = new();
        await texture.SaveAsync(stream, KtxFormat.Instance);

        byte[] file = stream.ToArray();
        Assert.Equal(0xAB, file[0]);
        Assert.Equal(0x31, file[6]);
    }

    [Fact]
    public void ManagerResolvesTheFormatByExtensionAndMimeType()
    {
        Assert.Same(KtxFormat.Instance, TextureConfiguration.Default.Formats.FindFormatByFileExtension(".ktx"));
        Assert.Same(KtxFormat.Instance, TextureConfiguration.Default.Formats.FindFormatByFileExtension("ktx"));
        Assert.Same(KtxFormat.Instance, TextureConfiguration.Default.Formats.FindFormatByMimeType("image/ktx"));
    }
}
