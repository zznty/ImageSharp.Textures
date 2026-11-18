using ImageSharp.Textures.Tests.TestUtilities;

namespace ImageSharp.Textures.Tests.Core;

[Trait("Category", "Core")]
public class TextureMetadataTests
{
    [Fact]
    public void GetFormatMetadataCreatesAndCachesTheFormatDefault()
    {
        TestTextureFormat format = new("TEST", "image/x-test", ".test");
        TextureMetadata metadata = new();

        TestTextureFormatMetadata first = metadata.GetFormatMetadata(format);
        TestTextureFormatMetadata second = metadata.GetFormatMetadata(format);

        Assert.Same(first, second);
        Assert.Equal(0, first.Value);
    }

    [Fact]
    public void CloneFormatMetadataReturnsAnIndependentCopy()
    {
        TestTextureFormat format = new("TEST", "image/x-test", ".test");
        TextureMetadata metadata = new();
        metadata.GetFormatMetadata(format).Value = 42;

        TestTextureFormatMetadata clone = metadata.CloneFormatMetadata(format);

        Assert.NotSame(metadata.GetFormatMetadata(format), clone);
        Assert.Equal(42, clone.Value);

        clone.Value = 7;
        Assert.Equal(42, metadata.GetFormatMetadata(format).Value);
    }

    [Fact]
    public void DeepCloneCopiesTheDecodedFormatAndClonesFormatMetadata()
    {
        TestTextureFormat format = new("TEST", "image/x-test", ".test");
        TextureMetadata metadata = new() { DecodedTextureFormat = format };
        metadata.GetFormatMetadata(format).Value = 11;

        TextureMetadata clone = metadata.DeepClone();

        Assert.Same(format, clone.DecodedTextureFormat);
        Assert.NotSame(metadata.GetFormatMetadata(format), clone.GetFormatMetadata(format));
        Assert.Equal(11, clone.GetFormatMetadata(format).Value);

        clone.GetFormatMetadata(format).Value = 3;
        Assert.Equal(11, metadata.GetFormatMetadata(format).Value);
    }

    [Fact]
    public void GetFormatMetadataRejectsANullKey()
    {
        TextureMetadata metadata = new();
        Assert.Throws<ArgumentNullException>(() => metadata.GetFormatMetadata<TestTextureFormatMetadata>(null!));
    }
}
