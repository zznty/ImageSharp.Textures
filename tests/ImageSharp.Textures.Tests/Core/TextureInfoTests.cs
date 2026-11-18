namespace ImageSharp.Textures.Tests.Core;

[Trait("Category", "Core")]
public class TextureInfoTests
{
    [Fact]
    public void ExposesTheDescribedLayout()
    {
        TextureInfo info = new(64, 32, 4, 3, 12, TexelFormat.Bc7_UNorm);

        Assert.Equal(64, info.Width);
        Assert.Equal(32, info.Height);
        Assert.Equal(4, info.Depth);
        Assert.Equal(3, info.MipLevelCount);
        Assert.Equal(12, info.SurfaceCount);
        Assert.Equal(TexelFormat.Bc7_UNorm, info.TexelFormat);
    }

    [Fact]
    public void RejectsNonPositiveDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextureInfo(0, 1, 1, 1, 1, TexelFormat.Bc1_UNorm));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextureInfo(1, 0, 1, 1, 1, TexelFormat.Bc1_UNorm));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextureInfo(1, 1, 0, 1, 1, TexelFormat.Bc1_UNorm));
    }

    [Fact]
    public void RejectsEmptyMipOrSurfaceCounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextureInfo(1, 1, 1, 0, 1, TexelFormat.Bc1_UNorm));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextureInfo(1, 1, 1, 1, 0, TexelFormat.Bc1_UNorm));
    }
}
