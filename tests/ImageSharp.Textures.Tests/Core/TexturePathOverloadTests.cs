using ImageSharp.Textures.Formats.Ktx;
using ImageSharp.Textures.Tests.TestUtilities;
using ImageSharp.Textures.Tests.TestUtilities.Ktx;

namespace ImageSharp.Textures.Tests.Core;

/// <summary>
///     Covers the <see cref="Texture" /> overloads that take an explicit decoder or encoder, the asynchronous path
///     overloads, dispose cascades and the remaining small public contract members.
/// </summary>
[Trait("Category", "Core")]
public class TexturePathOverloadTests
{
    [Fact]
    public void LoadPathWithExplicitDecoderUsesIt()
    {
        TestTextureFormat format = new("TEST", "image/x-test", ".test");
        TestTextureDecoder decoder = new(format);
        string path = Path.Combine(Path.GetTempPath(), $"imagesharp-textures-{Guid.NewGuid():N}.test");

        try
        {
            File.WriteAllBytes(path, [TestTextureEncoder.Marker]);

            using Texture texture = Texture.Load(path, decoder);

            Assert.Equal(1, decoder.DecodeCount);
            Assert.Same(format, texture.Metadata.DecodedTextureFormat);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsyncPathDetectsTheContainerFromTheExtension()
    {
        using Texture texture = KtxTestTextures.Create(4, 4, TexelFormat.R8G8B8A8_UNorm, 2);
        byte[] payload = texture.MipLevels[0].Surfaces[0].GetPayload().ToArray();
        string path = Path.Combine(Path.GetTempPath(), $"imagesharp-textures-{Guid.NewGuid():N}.ktx");

        try
        {
            texture.Save(path);

            using Texture loaded = await Texture.LoadAsync(path);

            Assert.Same(KtxFormat.Instance, loaded.Metadata.DecodedTextureFormat);
            Assert.Equal(2, loaded.MipLevels.Count);
            Assert.Equal(payload, loaded.MipLevels[0].Surfaces[0].GetPayload().ToArray());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsyncPathWithExplicitDecoderUsesIt()
    {
        TestTextureFormat format = new("TEST", "image/x-test", ".test");
        TestTextureDecoder decoder = new(format);
        string path = Path.Combine(Path.GetTempPath(), $"imagesharp-textures-{Guid.NewGuid():N}.test");

        try
        {
            File.WriteAllBytes(path, [TestTextureEncoder.Marker]);

            using Texture texture = await Texture.LoadAsync(path, decoder);

            Assert.Equal(1, decoder.DecodeCount);
            Assert.Same(format, texture.Metadata.DecodedTextureFormat);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SavePathWithExplicitEncoderWritesItsOutput()
    {
        TestTextureFormat format = new("TEST", "image/x-test", ".test");
        TestTextureEncoder encoder = new(format);
        using Texture texture = TestTextureFactory.CreateTexture(TestTextureFactory.CreateConfiguration(), 2, 2);
        string path = Path.Combine(Path.GetTempPath(), $"imagesharp-textures-{Guid.NewGuid():N}.test");

        try
        {
            texture.Save(path, encoder);

            Assert.Equal(1, encoder.EncodeCount);
            Assert.Equal([TestTextureEncoder.Marker], File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveAsyncPathResolvesTheRegisteredEncoder()
    {
        using Texture texture = KtxTestTextures.Create(4, 4, TexelFormat.R8G8B8A8_UNorm);
        byte[] payload = texture.MipLevels[0].Surfaces[0].GetPayload().ToArray();
        string path = Path.Combine(Path.GetTempPath(), $"imagesharp-textures-{Guid.NewGuid():N}.ktx");

        try
        {
            await texture.SaveAsync(path);

            using Texture loaded = Texture.Load(path);
            Assert.Same(KtxFormat.Instance, loaded.Metadata.DecodedTextureFormat);
            Assert.Equal(payload, loaded.MipLevels[0].Surfaces[0].GetPayload().ToArray());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveAsyncPathWithExplicitEncoderUsesIt()
    {
        TestTextureFormat format = new("TEST", "image/x-test", ".test");
        TestTextureEncoder encoder = new(format);
        using Texture texture = TestTextureFactory.CreateTexture(TestTextureFactory.CreateConfiguration(), 2, 2);
        string path = Path.Combine(Path.GetTempPath(), $"imagesharp-textures-{Guid.NewGuid():N}.test");

        try
        {
            await texture.SaveAsync(path, encoder);

            Assert.Equal(1, encoder.EncodeCount);
            Assert.Equal([TestTextureEncoder.Marker], File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveAsyncStreamWithExplicitEncoderUsesIt()
    {
        TestTextureFormat format = new("TEST", "image/x-test", ".test");
        TestTextureEncoder encoder = new(format);
        using Texture texture = TestTextureFactory.CreateTexture(TestTextureFactory.CreateConfiguration(), 2, 2);
        using MemoryStream stream = new();

        await texture.SaveAsync(stream, encoder);

        Assert.Equal(1, encoder.EncodeCount);
        Assert.Equal([TestTextureEncoder.Marker], stream.ToArray());
    }

    [Fact]
    public void ConfigurationExposesTheSuppliedImageSharpConfiguration()
    {
        Configuration imageSharpConfiguration = new();
        TextureConfiguration configuration = new(imageSharpConfiguration);

        Assert.Same(imageSharpConfiguration, configuration.ImageSharpConfiguration);
        Assert.NotNull(TextureConfiguration.Default.ImageSharpConfiguration);
    }

    [Fact]
    public void UnknownTextureFormatExceptionPreservesTheInnerException()
    {
        InvalidOperationException inner = new("inner");
        UnknownTextureFormatException exception = new("outer", inner);

        Assert.Equal("outer", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void DisposingTheTextureReleasesEverySurface()
    {
        using Texture texture = TestTextureFactory.CreateTexture(TestTextureFactory.CreateConfiguration(), 2, 2, 2);
        TextureSurface surface = texture.MipLevels[0].Surfaces[0];

        texture.Dispose();

        Assert.Throws<ObjectDisposedException>(() => surface.GetPayload());
    }

    [Fact]
    public void DisposingAMipLevelDirectlyReleasesItsSurfaces()
    {
        using Texture texture = TestTextureFactory.CreateTexture(TestTextureFactory.CreateConfiguration(), 2, 2);
        TextureMipLevel mipLevel = texture.MipLevels[0];
        TextureSurface surface = mipLevel.Surfaces[0];

        mipLevel.Dispose();

        Assert.Throws<ObjectDisposedException>(() => surface.GetPayload());
    }

    [Fact]
    public void DisposingASurfaceDirectlyRejectsFurtherUse()
    {
        using Texture texture = TestTextureFactory.CreateTexture(TestTextureFactory.CreateConfiguration(), 2, 2);
        TextureSurface surface = texture.MipLevels[0].Surfaces[0];

        surface.Dispose();

        Assert.Throws<ObjectDisposedException>(() => surface.GetPayload());
        Assert.Throws<ObjectDisposedException>(() => surface.ToImage<Rgba32>());
    }

    [Fact]
    public void TypeInfoHashCodeMatchesValueEquality()
    {
        TexelTypeInfo left = CreateTypeInfo(TexelAlphaRepresentation.Unassociated);
        TexelTypeInfo same = CreateTypeInfo(TexelAlphaRepresentation.Unassociated);
        TexelTypeInfo different = CreateTypeInfo(TexelAlphaRepresentation.None);

        Assert.Equal(left.GetHashCode(), same.GetHashCode());
        Assert.False(left != same);
        Assert.True(left != different);
    }

    private static TexelTypeInfo CreateTypeInfo(TexelAlphaRepresentation alpha)
        => new()
        {
            BitsPerTexel = 32,
            ColorType = TexelColorType.RGB | TexelColorType.Alpha,
            ComponentInfo = new TexelComponentInfo(
                [8, 8, 8, 8],
                [
                    TexelComponentInterpretation.UNorm,
                    TexelComponentInterpretation.UNorm,
                    TexelComponentInterpretation.UNorm,
                    TexelComponentInterpretation.UNorm
                ]),
            AlphaRepresentation = alpha
        };
}
