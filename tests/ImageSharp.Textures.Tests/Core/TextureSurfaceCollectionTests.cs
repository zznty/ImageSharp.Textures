using ImageSharp.Textures.Tests.TestUtilities;

namespace ImageSharp.Textures.Tests.Core;

[Trait("Category", "Core")]
public class TextureSurfaceCollectionTests
{
    [Fact]
    public void AddSurfaceClonesTheSourcePayload()
    {
        TextureConfiguration configuration = TestTextureFactory.CreateConfiguration();
        using Texture texture = TestTextureFactory.CreateTexture(configuration, 4, 4);
        TextureSurfaceCollection surfaces = texture.MipLevels[0].Surfaces;
        TextureSurface source = surfaces[0];

        TextureSurface added = surfaces.AddSurface(source);

        Assert.NotSame(source, added);
        Assert.Equal(2, surfaces.Count);
        source.GetPayload()[0] = 123;
        Assert.Equal(0, added.GetPayload()[0]);
    }

    [Fact]
    public void AddSurfaceRejectsMismatchedDimensions()
    {
        TextureConfiguration configuration = TestTextureFactory.CreateConfiguration();
        using Texture texture = TestTextureFactory.CreateTexture(configuration, 4, 4);
        TextureSurfaceCollection surfaces = texture.MipLevels[0].Surfaces;
        using TextureSurface mismatched = new(configuration, TexelFormat.R8G8B8A8_UNorm, 5, 4);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => surfaces.AddSurface(mismatched));
        Assert.Equal("surface", exception.ParamName);
        Assert.Contains("5x4", exception.Message, StringComparison.Ordinal);
        Assert.Contains("4x4", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InsertSurfacePlacesTheCloneAtTheIndex()
    {
        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            4,
            4,
            2);
        TextureSurfaceCollection surfaces = texture.MipLevels[0].Surfaces;
        TextureSurface clone = surfaces.CloneSurface(1);

        TextureSurface inserted = surfaces.InsertSurface(1, clone);

        Assert.Equal(3, surfaces.Count);
        Assert.Equal(1, surfaces.IndexOf(inserted));
        Assert.Same(inserted, surfaces[1]);
        Assert.NotSame(clone, inserted);
        Assert.Equal(1, surfaces[2].GetPayload()[0]);
    }

    [Fact]
    public void CloneSurfaceIsIndependentOfTheSource()
    {
        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            4,
            4,
            2);
        TextureSurfaceCollection surfaces = texture.MipLevels[0].Surfaces;
        TextureSurface source = surfaces[0];

        TextureSurface clone = surfaces.CloneSurface(0);

        Assert.NotSame(source, clone);
        Assert.Equal(2, surfaces.Count);
        Assert.Equal(source.GetPayload().ToArray(), clone.GetPayload().ToArray());

        clone.GetPayload()[0] ^= 0xFF;
        Assert.NotEqual(source.GetPayload()[0], clone.GetPayload()[0]);
    }

    [Fact]
    public void RemoveSurfaceDisposesTheSurface()
    {
        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            4,
            4,
            2);
        TextureSurfaceCollection surfaces = texture.MipLevels[0].Surfaces;
        TextureSurface removed = surfaces[1];

        surfaces.RemoveSurface(1);

        Assert.Equal(1, surfaces.Count);
        Assert.Throws<ObjectDisposedException>(() => removed.ToImage<Rgba32>());
    }

    [Fact]
    public void ExportSurfaceTransfersOwnership()
    {
        using Texture texture = TestTextureFactory.CreateTexture(TestTextureFactory.CreateConfiguration(), 4, 4);
        TextureSurfaceCollection surfaces = texture.MipLevels[0].Surfaces;
        TextureSurface source = surfaces[0];

        using TextureSurface exported = surfaces.ExportSurface(0);

        Assert.Same(source, exported);
        Assert.Equal(0, surfaces.Count);
        using Image<Rgba32> image = exported.ToImage<Rgba32>();
        Assert.Equal(4, image.Width);
    }

    [Fact]
    public void MoveSurfaceReordersTheCollection()
    {
        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            4,
            4,
            3);
        TextureSurfaceCollection surfaces = texture.MipLevels[0].Surfaces;
        TextureSurface third = surfaces[2];

        surfaces.MoveSurface(2, 0);

        Assert.Same(third, surfaces[0]);
        Assert.Equal(2, surfaces[0].GetPayload()[0]);
        Assert.Equal(0, surfaces[1].GetPayload()[0]);
    }

    [Fact]
    public void ContainsAndIndexOfUseReferenceIdentity()
    {
        using Texture texture = TestTextureFactory.CreateTexture(TestTextureFactory.CreateConfiguration(), 4, 4);
        TextureSurfaceCollection surfaces = texture.MipLevels[0].Surfaces;
        TextureSurface surface = surfaces[0];

        Assert.True(surfaces.Contains(surface));
        Assert.Equal(0, surfaces.IndexOf(surface));
        Assert.False(surfaces.Contains(surfaces.CloneSurface(0)));
    }

    [Fact]
    public void DisposeCascadesToEverySurface()
    {
        Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            4,
            4,
            2);
        TextureSurface first = texture.MipLevels[0].Surfaces[0];
        TextureSurface second = texture.MipLevels[0].Surfaces[1];

        texture.MipLevels[0].Dispose();

        Assert.Throws<ObjectDisposedException>(() => first.ToImage<Rgba32>());
        Assert.Throws<ObjectDisposedException>(() => second.ToImage<Rgba32>());
    }
}
