using BenchmarkDotNet.Attributes;
using ImageSharp.Textures.Formats.Dds;

namespace ImageSharp.Textures.Benchmarks;

/// <summary>
///     DDS container I/O and surface interop on a BC7 texture with a four level mip chain.
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory("Io")]
public class TextureIoBenchmarks
{
    private const int Size = 1024;
    private const int MipCount = 4;

    private byte[] dds = null!;
    private DdsTextureEncoder encoder = null!;
    private Image<Rgba32> image = null!;
    private byte[] saveBuffer = null!;
    private Texture texture = null!;

    /// <summary>
    ///     Encodes the mip chain into a DDS once and allocates the interop source.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        Texture source = new(TextureConfiguration.Default, new TextureMetadata());

        for (int level = 0; level < MipCount; level++)
        {
            int size = Math.Max(1, Size >> level);
            TextureMipLevel mip = source.AddMipLevel(size, size, 1);
            TextureSurface surface = mip.CreateSurface(TexelFormat.Bc7_UNorm);
            BenchmarkData.GetPayload(TexelFormat.Bc7_UNorm, size).CopyTo(surface.GetPayload());
        }

        this.encoder = new DdsTextureEncoder(new DdsEncodingOptions { TargetTexelFormat = TexelFormat.Bc7_UNorm });

        using MemoryStream stream = new();
        source.Save(stream, this.encoder);
        this.dds = stream.ToArray();
        this.saveBuffer = new byte[this.dds.Length];
        source.Dispose();

        using MemoryStream reload = new(this.dds);
        this.texture = Texture.Load(reload);
        this.image = BenchmarkData.CreateImage(Size);
    }

    /// <summary>
    ///     Disposes the texture and source image.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.texture.Dispose();
        this.image.Dispose();
    }

    /// <summary>
    ///     Loads the container back into a texture and disposes it so pooled surface buffers are returned.
    /// </summary>
    [Benchmark]
    public void Load()
    {
        using MemoryStream stream = new(this.dds, false);
        using Texture loaded = Texture.Load(stream);
    }

    /// <summary>
    ///     Writes the texture back out; the stored BC7 payload is copied, never recompressed.
    /// </summary>
    [Benchmark]
    public void Save()
    {
        using MemoryStream stream = new(this.saveBuffer, true);
        this.texture.Save(stream, this.encoder);
    }

    /// <summary>
    ///     Decodes the base level into an ImageSharp image.
    /// </summary>
    [Benchmark]
    public void ToImage()
    {
        using Image<Rgba32> decoded = this.texture.MipLevels[0].Surfaces[0].ToImage<Rgba32>();
    }

    /// <summary>
    ///     Replaces the base level from an RGBA image, which re-encodes the payload.
    /// </summary>
    [Benchmark]
    public void CopyFrom() => this.texture.MipLevels[0].Surfaces[0].CopyFrom(this.image);
}
