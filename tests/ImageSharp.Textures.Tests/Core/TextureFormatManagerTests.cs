using ImageSharp.Textures.Formats.Dds;
using ImageSharp.Textures.Formats.Ktx2;
using ImageSharp.Textures.Formats.Ktx;
using ImageSharp.Textures.Tests.TestUtilities;

namespace ImageSharp.Textures.Tests.Core;

[Trait("Category", "Core")]
public class TextureFormatManagerTests
{
    [Fact]
    public void AddFormatRegistersDecoderAndOptionalEncoder()
    {
        TestTextureFormat format = new("TEST", "image/x-test", ".test");
        TestTextureDecoder decoder = new(format);
        TestTextureEncoder encoder = new(format);
        TextureFormatManager manager = new();

        manager.AddFormat(format, decoder, encoder);

        Assert.Same(decoder, manager.GetDecoder(format));
        Assert.True(manager.TryGetEncoder(format, out ITextureEncoder? resolved));
        Assert.Same(encoder, resolved);
        Assert.Equal([format], manager.Formats);
    }

    [Fact]
    public void TryGetEncoderReturnsFalseWhenNoEncoderIsRegistered()
    {
        TestTextureFormat format = new("TEST", "image/x-test", ".test");
        TextureFormatManager manager = new();
        manager.AddFormat(format, new TestTextureDecoder(format));

        Assert.False(manager.TryGetEncoder(format, out ITextureEncoder? encoder));
        Assert.Null(encoder);
    }

    [Fact]
    public void GetDecoderThrowsForUnregisteredFormats()
    {
        TestTextureFormat format = new("MISSING", "image/x-missing", ".missing");
        TextureFormatManager manager = new();

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => manager.GetDecoder(format));
        Assert.Contains("MISSING", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetDecoderRejectsANullFormat()
    {
        TextureFormatManager manager = new();
        Assert.Throws<ArgumentNullException>(() => manager.GetDecoder(null!));
    }

    [Fact]
    public void AddFormatReplacesAnExistingRegistrationOfTheSameType()
    {
        TestTextureFormat format = new("TEST", "image/x-test", ".test");
        TestTextureDecoder first = new(format);
        TestTextureDecoder second = new(format);
        TextureFormatManager manager = new();

        manager.AddFormat(format, first);
        manager.AddFormat(format, second);

        Assert.Single(manager.Formats);
        Assert.Same(second, manager.GetDecoder(format));
    }

    [Fact]
    public void AddFormatRejectsADecoderForAnotherFormat()
    {
        TestTextureFormat format = new("TEST", "image/x-test", ".test");
        TestTextureFormat other = new("OTHER", "image/x-other", ".other");
        TextureFormatManager manager = new();

        ArgumentException exception =
            Assert.Throws<ArgumentException>(() => manager.AddFormat(format, new TestTextureDecoder(other)));
        Assert.Equal("decoder", exception.ParamName);
    }

    [Fact]
    public void RemoveFormatDropsEveryLookup()
    {
        TestTextureFormat format = new("TEST", "image/x-test", ".test");
        TextureFormatManager manager = new();
        manager.AddFormat(format, new TestTextureDecoder(format), new TestTextureEncoder(format));

        Assert.True(manager.RemoveFormat(format));
        Assert.False(manager.RemoveFormat(format));
        Assert.Empty(manager.Formats);
        Assert.Null(manager.FindFormatByFileExtension(".test"));
        Assert.Null(manager.FindFormatByMimeType("image/x-test"));
        Assert.Throws<NotSupportedException>(() => manager.GetDecoder(format));
    }

    [Fact]
    public void FindFormatByFileExtensionIgnoresTheLeadingPeriodAndCase()
    {
        TestTextureFormat format = new("TEST", "image/x-test", ".test");
        TextureFormatManager manager = new();
        manager.AddFormat(format, new TestTextureDecoder(format));

        Assert.Same(format, manager.FindFormatByFileExtension(".test"));
        Assert.Same(format, manager.FindFormatByFileExtension("test"));
        Assert.Same(format, manager.FindFormatByFileExtension(".TEST"));
        Assert.Null(manager.FindFormatByFileExtension(".unknown"));
        Assert.Null(manager.FindFormatByFileExtension(string.Empty));
    }

    [Fact]
    public void FindFormatByMimeTypeMatchesTheDefaultAndDeclaredTypes()
    {
        TestTextureFormat format = new("TEST", "image/x-test", ".test");
        TextureFormatManager manager = new();
        manager.AddFormat(format, new TestTextureDecoder(format));

        Assert.Same(format, manager.FindFormatByMimeType("image/x-test"));
        Assert.Same(format, manager.FindFormatByMimeType("IMAGE/X-TEST"));
        Assert.Null(manager.FindFormatByMimeType("image/x-unknown"));
        Assert.Null(manager.FindFormatByMimeType(string.Empty));
        Assert.Throws<ArgumentNullException>(() => manager.FindFormatByMimeType(null!));
    }

    [Fact]
    public void DetectFormatUsesTheFirstMatchingDetector()
    {
        TextureFormatManager manager = new();
        manager.AddFormat(
            DdsFormat.Instance,
            new TestTextureDecoder(DdsFormat.Instance),
            null,
            new TestTextureFormatDetector(DdsFormat.Instance, 0xAB, 0xCD));
        manager.AddFormat(
            KtxFormat.Instance,
            new TestTextureDecoder(KtxFormat.Instance),
            null,
            new TestTextureFormatDetector(KtxFormat.Instance, 0xAB, 0xCD));

        ReadOnlySpan<byte> header = [0xAB, 0xCD, 0x00];

        Assert.Same(DdsFormat.Instance, manager.DetectFormat(header));
        Assert.Equal(new ITextureFormat[] { DdsFormat.Instance, KtxFormat.Instance }, manager.Formats);
    }

    [Fact]
    public void DetectFormatReturnsNullWhenNothingMatches()
    {
        TestTextureFormat format = new("TEST", "image/x-test", ".test");
        TextureFormatManager manager = new();
        manager.AddFormat(format, new TestTextureDecoder(format), null, new TestTextureFormatDetector(format, 0x7F));

        Assert.Null(manager.DetectFormat([0x00, 0x01]));
        Assert.Null(new TextureFormatManager().DetectFormat([0x00]));
    }

    [Fact]
    public void DefaultConfigurationDescribesTheContainerFormats()
    {
        TextureFormatManager formats = TextureConfiguration.Default.Formats;

        Assert.Same(DdsFormat.Instance, formats.FindFormatByFileExtension(".dds"));
        Assert.Same(KtxFormat.Instance, formats.FindFormatByFileExtension(".ktx"));
        Assert.Same(Ktx2Format.Instance, formats.FindFormatByFileExtension(".ktx2"));
        Assert.Same(DdsFormat.Instance, formats.FindFormatByMimeType("image/vnd.ms-dds"));
        Assert.Same(KtxFormat.Instance, formats.FindFormatByMimeType("image/ktx"));
        Assert.Same(Ktx2Format.Instance, formats.FindFormatByMimeType("image/ktx2"));
        Assert.Equal(["DDS", "KTX", "KTX2"], formats.Formats.Select(format => format.Name));
    }
}
