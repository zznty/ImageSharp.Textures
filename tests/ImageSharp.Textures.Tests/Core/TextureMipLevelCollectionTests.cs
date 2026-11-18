using ImageSharp.Textures.Tests.TestUtilities;

namespace ImageSharp.Textures.Tests.Core;

[Trait("Category", "Core")]
public class TextureMipLevelCollectionTests
{
    [Fact]
    public void AddMipLevelClonesTheSourcePayload()
    {
        using Texture texture = TestTextureFactory.CreateTexture(TestTextureFactory.CreateConfiguration(), 4, 4);
        TextureMipLevel source = texture.MipLevels[0];
        TextureMipLevel added = texture.MipLevels.AddMipLevel(source);

        Assert.NotSame(source, added);
        Assert.Equal(2, texture.MipLevels.Count);
        Assert.NotSame(source.Surfaces[0], added.Surfaces[0]);

        source.Surfaces[0].GetPayload()[0] = 200;
        Assert.Equal(0, added.Surfaces[0].GetPayload()[0]);
    }

    [Fact]
    public void InsertMipLevelPlacesTheCloneAtTheIndex()
    {
        TextureConfiguration configuration = TestTextureFactory.CreateConfiguration();
        using Texture texture = TestTextureFactory.CreateTexture(configuration, 8, 8);
        TextureMipLevel source = texture.MipLevels[0];

        TextureMipLevel inserted = texture.MipLevels.InsertMipLevel(0, source);

        Assert.Equal(0, texture.MipLevels.IndexOf(inserted));
        Assert.Equal(1, texture.MipLevels.IndexOf(source));
    }

    [Fact]
    public void BaseLevelThrowsWhenTheCollectionIsEmpty()
    {
        using Texture texture = new(TestTextureFactory.CreateConfiguration(), new TextureMetadata());

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => texture.MipLevels.BaseLevel);
        Assert.Contains("mip levels", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RemoveMipLevelDisposesTheLevelAndItsSurfaces()
    {
        using Texture texture = TestTextureFactory.CreateTexture(TestTextureFactory.CreateConfiguration(), 4, 4);
        TextureSurface surface = texture.MipLevels[0].Surfaces[0];

        texture.MipLevels.RemoveMipLevel(0);

        Assert.Equal(0, texture.MipLevels.Count);
        Assert.Throws<ObjectDisposedException>(() => surface.ToImage<Rgba32>());
    }

    [Fact]
    public void ExportMipLevelTransfersOwnershipWithoutDisposing()
    {
        using Texture texture = TestTextureFactory.CreateTexture(TestTextureFactory.CreateConfiguration(), 4, 4);
        TextureMipLevel level = texture.MipLevels[0];

        using TextureMipLevel exported = texture.MipLevels.ExportMipLevel(0);

        Assert.Same(level, exported);
        Assert.Equal(0, texture.MipLevels.Count);
        Assert.Equal(4, exported.Surfaces[0].Width);
        using Image<Rgba32> image = exported.Surfaces[0].ToImage<Rgba32>();
        Assert.Equal(4, image.Width);
        Assert.Equal(4, image.Height);
    }

    [Fact]
    public void MoveMipLevelReordersTheCollection()
    {
        TextureConfiguration configuration = TestTextureFactory.CreateConfiguration();
        using Texture texture = TestTextureFactory.CreateTexture(configuration, 8, 8);
        TextureMipLevel baseLevel = texture.MipLevels[0];
        TextureMipLevel other = texture.AddMipLevel(4, 4, 1);

        texture.MipLevels.MoveMipLevel(1, 0);

        Assert.Same(other, texture.MipLevels[0]);
        Assert.Same(baseLevel, texture.MipLevels[1]);
        Assert.Equal(4, texture.MipLevels[0].Width);
    }

    [Fact]
    public void CloneMipLevelIsIndependentOfTheSource()
    {
        using Texture texture = TestTextureFactory.CreateTexture(TestTextureFactory.CreateConfiguration(), 4, 4);
        TextureMipLevel source = texture.MipLevels[0];

        TextureMipLevel clone = texture.MipLevels.CloneMipLevel(0);

        Assert.NotSame(source, clone);
        Assert.Equal(source.Width, clone.Width);
        Assert.Equal(source.Height, clone.Height);
        Assert.Equal(source.Depth, clone.Depth);
        Assert.NotSame(source.Surfaces[0], clone.Surfaces[0]);
    }

    [Fact]
    public void DisposeCascadesToEverySurface()
    {
        Texture texture = TestTextureFactory.CreateTexture(TestTextureFactory.CreateConfiguration(), 4, 4, 2);
        TextureSurface first = texture.MipLevels[0].Surfaces[0];
        TextureSurface second = texture.MipLevels[0].Surfaces[1];

        texture.Dispose();

        Assert.Throws<ObjectDisposedException>(() => first.ToImage<Rgba32>());
        Assert.Throws<ObjectDisposedException>(() => second.ToImage<Rgba32>());
    }

    [Fact]
    public void ContainsAndIndexOfUseReferenceIdentity()
    {
        using Texture texture = TestTextureFactory.CreateTexture(TestTextureFactory.CreateConfiguration(), 4, 4);
        TextureMipLevel level = texture.MipLevels[0];

        Assert.True(texture.MipLevels.Contains(level));
        Assert.Equal(0, texture.MipLevels.IndexOf(level));
        Assert.False(texture.MipLevels.Contains(texture.MipLevels.CloneMipLevel(0)));
    }

    [Fact]
    public void EnumerationFollowsCollectionOrder()
    {
        using Texture texture = TestTextureFactory.CreateTexture(TestTextureFactory.CreateConfiguration(), 8, 8);
        TextureMipLevel second = texture.AddMipLevel(4, 4, 1);
        texture.AddMipLevel(2, 2, 1);

        List<int> widths = [];
        foreach (TextureMipLevel level in texture.MipLevels)
        {
            widths.Add(level.Width);
        }

        Assert.Equal([8, 4, 2], widths);
        Assert.Same(second, texture.MipLevels[1]);
    }
}
