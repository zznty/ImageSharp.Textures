using System.Security.Cryptography;
using SixLabors.ImageSharp.Formats.Tga;

namespace ImageSharp.Textures.Parity.Tests;

/// <summary>
///     The deterministic parity corpus.
/// </summary>
/// <remarks>
///     Every image is produced from code, encoded through ImageSharp's TGA encoder with 32 bits per pixel and no
///     run-length compression, and cached under <c>artifacts/parity/corpus</c> keyed by name, size and the
///     SHA-256 of the encoded bytes. Regenerating an image therefore either hits the cache exactly or writes a
///     new, content-addressed file; the bytes never depend on the run.
/// </remarks>
internal static class ParityCorpus
{
    private const int AlphaGradientRgb = 0xC8;

    private static readonly Dictionary<string, Func<int, int, int, int, Rgba32>> Paints = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, ParityImage> Images = Build();

    /// <summary>
    ///     Gets every corpus image, ordered by name.
    /// </summary>
    internal static IReadOnlyList<ParityImage> All { get; } =
        Images.Values.OrderBy(image => image.Name, StringComparer.Ordinal).ToArray();

    /// <summary>
    ///     Gets a corpus image by name.
    /// </summary>
    /// <param name="name">The corpus name.</param>
    /// <returns>The image.</returns>
    internal static ParityImage Get(string name)
        => Images.TryGetValue(name, out ParityImage? image)
            ? image
            : throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown corpus image.");

    /// <summary>
    ///     Re-encodes a corpus image from its definition, bypassing the cache.
    /// </summary>
    /// <param name="name">The corpus name.</param>
    /// <returns>The encoded TGA bytes.</returns>
    internal static byte[] Encode(string name)
    {
        ParityImage image = Get(name);
        return Encode(image.Width, image.Height, Paints[name]);
    }

    private static Dictionary<string, ParityImage> Build()
    {
        Dictionary<string, ParityImage> images = new(StringComparer.Ordinal);

        Add(images, "gradient-256", 256, 256, (x, y, w, h) => new Rgba32(
            Scale(x, w),
            Scale(y, h),
            128,
            255));

        Add(images, "gradient-1024", 1024, 1024, (x, y, w, h) => new Rgba32(
            Scale(x, w),
            Scale(y, h),
            128,
            255));

        Add(images, "checker-256", 256, 256, (x, y, _, _) =>
            (((x >> 5) + (y >> 5)) & 1) == 0 ? new Rgba32(255, 255, 255, 255) : new Rgba32(0, 0, 0, 255));

        Add(images, "noise-256", 256, 256, (x, y, _, _) => Noise(x, y, 0x5EED));

        Add(images, "alpha-gradient-256", 256, 256, (x, y, w, h) => new Rgba32(
            AlphaGradientRgb,
            AlphaGradientRgb / 2,
            AlphaGradientRgb / 4,
            Scale(y, h)));

        Add(images, "normal-map-256", 256, 256, (x, y, w, h) => new Rgba32(
            Scale(x, w),
            Scale(y, h),
            0,
            255));

        Add(images, "flat-4x4", 4, 4, (_, _, _, _) => new Rgba32(10, 20, 30, 255));

        Add(images, "odd-17x33", 17, 33, (x, y, w, h) => new Rgba32(
            Scale(x, w),
            Scale(y, h),
            255 - Scale(x, w),
            255));

        Add(images, "odd-1x1", 1, 1, (_, _, _, _) => new Rgba32(7, 8, 9, 10));

        return images;
    }

    private static void Add(
        Dictionary<string, ParityImage> images,
        string name,
        int width,
        int height,
        Func<int, int, int, int, Rgba32> paint)
    {
        string directory = Path.Combine(TexconvLocator.RepoRoot, "artifacts", "parity", "corpus");
        Directory.CreateDirectory(directory);

        Paints.Add(name, paint);

        byte[] bytes = Encode(width, height, paint);
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        string file = Path.Combine(directory, $"{name}-{width}x{height}-{hash[..16]}.tga");

        if (!File.Exists(file))
        {
            File.WriteAllBytes(file, bytes);
        }

        images.Add(name, new ParityImage(name, width, height, file, hash));
    }

    /// <summary>
    ///     A pure per-pixel hash, so the noise image is reproducible from its coordinates alone.
    /// </summary>
    /// <param name="x">The pixel column.</param>
    /// <param name="y">The pixel row.</param>
    /// <param name="seed">The corpus seed.</param>
    /// <returns>The pixel.</returns>
    private static Rgba32 Noise(int x, int y, uint seed)
    {
        uint hash = (seed * 0x9E3779B9u) ^ ((uint)x * 0x85EBCA6Bu) ^ ((uint)y * 0xC2B2AE35u);
        hash ^= hash >> 16;
        hash *= 0x7FEB352Du;
        hash ^= hash >> 15;
        hash *= 0x846CA68Bu;
        hash ^= hash >> 16;

        return new Rgba32((byte)hash, (byte)(hash >> 8), (byte)(hash >> 16), 255);
    }

    private static byte[] Encode(int width, int height, Func<int, int, int, int, Rgba32> paint)
    {
        using Image<Rgba32> image = new(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                image[x, y] = paint(x, y, width, height);
            }
        }

        using MemoryStream stream = new();
        image.Save(stream, new TgaEncoder { BitsPerPixel = TgaBitsPerPixel.Bit32, Compression = TgaCompression.None });
        return stream.ToArray();
    }

    private static byte Scale(int value, int maximum)
        => maximum <= 1 ? (byte)0 : (byte)(value * 255 / (maximum - 1));
}
