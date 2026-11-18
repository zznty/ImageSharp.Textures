using ImageSharp.Textures.Formats.Dds;
using ImageSharp.Textures.Formats.Dds.Enums;
using ImageSharp.Textures.Tests.TestUtilities.Dds;

namespace ImageSharp.Textures.Tests.Formats.Dds;

[Trait("Category", "DdsDecode")]
public class DdsRegistrationTests
{
    [Fact]
    public void DefaultConfigurationRegistersExactlyOneDdsEntryWithDecoderEncoderAndDetector()
    {
        TextureFormatManager formats = TextureConfiguration.Default.Formats;

        ITextureFormat[] ddsEntries = formats.Formats.Where(format => format is DdsFormat).ToArray();

        ITextureFormat entry = Assert.Single(ddsEntries);
        Assert.Same(DdsFormat.Instance, entry);

        ITextureDecoder decoder = formats.GetDecoder(DdsFormat.Instance);
        Assert.IsType<DdsTextureDecoder>(decoder);
        Assert.Same(DdsFormat.Instance, decoder.Format);

        Assert.True(formats.TryGetEncoder(DdsFormat.Instance, out ITextureEncoder? encoder));
        Assert.IsType<DdsTextureEncoder>(encoder);
        Assert.Same(DdsFormat.Instance, encoder!.Format);

        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 2, 2, 0);
        Assert.Same(DdsFormat.Instance, formats.DetectFormat(builder.Build().AsSpan(0, 16)));
    }

    [Fact]
    public void DefaultConfigurationDetectsDdsFromTheMagicWord()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 2, 2, 0);
        builder.AddPatternPayloads();
        byte[] file = builder.Build();

        Assert.Same(DdsFormat.Instance, TextureConfiguration.Default.Formats.DetectFormat(file.AsSpan(0, 16)));
        Assert.Null(TextureConfiguration.Default.Formats.DetectFormat("KTX 11"u8.ToArray()));
    }

    [Fact]
    public void LoadStreamDetectsTheDdsDecoder()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 2, 2, 0);
        builder.AddPatternPayloads();

        using MemoryStream stream = new(builder.Build());
        using Texture texture = Texture.Load(stream);

        Assert.Same(DdsFormat.Instance, texture.Metadata.DecodedTextureFormat);
        Assert.Equal(1, texture.MipLevels.Count);
    }

    [Fact]
    public async Task LoadAsyncStreamDetectsTheDdsDecoder()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 2, 2, 0);
        builder.AddPatternPayloads();

        using MemoryStream stream = new(builder.Build());
        using Texture texture = await Texture.LoadAsync(stream);

        Assert.Same(DdsFormat.Instance, texture.Metadata.DecodedTextureFormat);
    }

    [Fact]
    public void LoadPathResolvesTheDdsDecoderByExtension()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 2, 2, 0);
        builder.AddPatternPayloads();

        string directory = Path.Combine(Path.GetTempPath(), $"imagesharp-textures-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "asset.dds");
        File.WriteAllBytes(path, builder.Build());

        try
        {
            using Texture texture = Texture.Load(path);
            Assert.Same(DdsFormat.Instance, texture.Metadata.DecodedTextureFormat);
            Assert.Equal(2, texture.MipLevels[0].Width);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void LoadStreamWithExplicitDecoderDecodesDds()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 2, 2, 0);
        builder.AddPatternPayloads();

        using MemoryStream stream = new(builder.Build());
        using Texture texture = Texture.Load(stream, new DdsTextureDecoder());

        Assert.Same(DdsFormat.Instance, texture.Metadata.DecodedTextureFormat);
    }

    [Fact]
    public void SavingDdsThroughTheStreamOverloadWritesTheRegisteredEncoderOutput()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 2, 2, 0);
        builder.AddPatternPayloads();

        using MemoryStream stream = new(builder.Build());
        using Texture texture = Texture.Load(stream);
        using MemoryStream destination = new();

        texture.Save(destination, DdsFormat.Instance);

        byte[] encoded = destination.ToArray();
        Assert.Equal("DDS "u8.ToArray(), encoded.AsSpan(0, 4).ToArray());

        using Texture reloaded = Texture.Load(new MemoryStream(encoded));
        Assert.Equal(2, reloaded.MipLevels[0].Width);
    }
}
