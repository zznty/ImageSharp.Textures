using ImageSharp.Textures.Formats.Ktx2;
using ImageSharp.Textures.Formats.Ktx;
using ImageSharp.Textures.Tests.TestUtilities.Ktx2;
using ImageSharp.Textures.Tests.TestUtilities.Ktx;

namespace ImageSharp.Textures.Tests.Formats.Ktx2;

[Trait("Category", "Ktx2")]
public class Ktx2RegistrationTests
{
    [Fact]
    public void DefaultConfigurationRegistersKtx2WithDecoderEncoderAndDetector()
    {
        TextureFormatManager formats = TextureConfiguration.Default.Formats;

        ITextureFormat[] entries = formats.Formats.Where(format => format is Ktx2Format).ToArray();
        ITextureFormat entry = Assert.Single(entries);
        Assert.Same(Ktx2Format.Instance, entry);

        ITextureDecoder decoder = formats.GetDecoder(Ktx2Format.Instance);
        Assert.IsType<Ktx2TextureDecoder>(decoder);
        Assert.Same(Ktx2Format.Instance, decoder.Format);

        Assert.True(formats.TryGetEncoder(Ktx2Format.Instance, out ITextureEncoder? encoder));
        Assert.IsType<Ktx2TextureEncoder>(encoder);
        Assert.Same(Ktx2Format.Instance, encoder!.Format);
    }

    [Fact]
    public void FormatDescriptorDescribesTheContainer()
    {
        Assert.Equal("KTX2", Ktx2Format.Instance.Name);
        Assert.Equal("image/ktx2", Ktx2Format.Instance.DefaultMimeType);
        Assert.Equal(["image/ktx2"], Ktx2Format.Instance.MimeTypes);
        Assert.Equal([".ktx2"], Ktx2Format.Instance.FileExtensions);

        Ktx2TextureMetadata metadata = Ktx2Format.Instance.CreateDefaultFormatMetadata();
        Assert.IsType<Ktx2TextureMetadata>(metadata);
        Assert.NotSame(metadata, Ktx2Format.Instance.CreateDefaultFormatMetadata());
    }

    [Fact]
    public void DefaultConfigurationDetectsKtx2FromTheIdentifier()
    {
        Ktx2FileBuilder builder = Ktx2FileBuilder.Create(37, 2, 2, 0);
        builder.DataFormatDescriptor = Ktx2DecodeTests.CreateDfd(37);
        builder.AddPatternPayloads();
        byte[] file = builder.Build();

        Assert.Same(Ktx2Format.Instance, TextureConfiguration.Default.Formats.DetectFormat(file.AsSpan(0, 16)));

        byte[] ktx1Identifier = Convert.FromHexString("AB4B5458203131BB0D0A1A0A");
        Assert.Null(new Ktx2FormatDetector().DetectFormat(ktx1Identifier));
        Assert.Same(KtxFormat.Instance, TextureConfiguration.Default.Formats.DetectFormat(ktx1Identifier));
    }

    [Fact]
    public void LoadStreamDetectsTheKtx2Decoder()
    {
        Ktx2FileBuilder builder = Ktx2FileBuilder.Create(37, 2, 2, 0);
        builder.DataFormatDescriptor = Ktx2DecodeTests.CreateDfd(37);
        builder.AddPatternPayloads(4);

        using MemoryStream stream = new(builder.Build());
        using Texture texture = Texture.Load(stream);

        Assert.Same(Ktx2Format.Instance, texture.Metadata.DecodedTextureFormat);
        KtxTestTextures.AssertSurfacePayload(texture.MipLevels[0].Surfaces[0], 4);
    }

    [Fact]
    public void SaveAndLoadPathRoundTrips()
    {
        using Texture texture = KtxTestTextures.Create(4, 4, TexelFormat.R8G8B8A8_UNorm, 2);

        string directory = Path.Combine(Path.GetTempPath(), "imagesharp-textures-ktx2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "texture.ktx2");
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
        await texture.SaveAsync(stream, Ktx2Format.Instance);

        byte[] file = stream.ToArray();
        Assert.Equal(0xAB, file[0]);
        Assert.Equal(0x32, file[5]);
        Assert.Equal(0x30, file[6]);
    }

    [Fact]
    public void ManagerResolvesTheFormatByExtensionAndMimeType()
    {
        Assert.Same(Ktx2Format.Instance, TextureConfiguration.Default.Formats.FindFormatByFileExtension(".ktx2"));
        Assert.Same(Ktx2Format.Instance, TextureConfiguration.Default.Formats.FindFormatByFileExtension("ktx2"));
        Assert.Same(Ktx2Format.Instance, TextureConfiguration.Default.Formats.FindFormatByMimeType("image/ktx2"));
    }
}
