using System.Runtime.CompilerServices;
using ImageSharp.Textures.Tests.TestUtilities;

namespace ImageSharp.Textures.Tests.Interop;

[Trait("Category", "Interop")]
public class Rgba32TexelOperationsTests
{
    [Fact]
    public void TexelTypeInfoDescribesUnassociatedRgba()
    {
        TexelTypeInfo info = TexelFormatRegistry.GetOperations(TexelFormat.R8G8B8A8_UNorm).GetTexelTypeInfo();

        Assert.Equal(32, info.BitsPerTexel);
        Assert.Equal(TexelColorType.RGB | TexelColorType.Alpha, info.ColorType);
        Assert.Equal(4, info.ComponentInfo.ComponentCount);
        Assert.Equal(8, info.ComponentInfo.GetComponentPrecision(3));
        Assert.Equal(TexelComponentInterpretation.UNorm, info.ComponentInfo.GetComponentInterpretation(0));
        Assert.Equal(TexelAlphaRepresentation.Unassociated, info.AlphaRepresentation);
    }

    [Fact]
    public void EncodedByteCountIsFourBytesPerTexel()
    {
        Textures.TexelOperations operations = TexelFormatRegistry.GetOperations(TexelFormat.R8G8B8A8_UNorm);

        Assert.Equal(16, operations.GetEncodedByteCount(2, 2));
        Assert.Equal(4096 * 4, operations.GetEncodedByteCount(64, 64));
    }

    [Fact]
    public void DecodeMapsPayloadBytesToRgba32Pixels()
    {
        const int width = 3;
        const int height = 2;
        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            width,
            height);
        TextureSurface surface = texture.MipLevels[0].Surfaces[0];
        byte[] expected = surface.GetPayload().ToArray();

        using Image<Rgba32> image = surface.ToImage<Rgba32>();

        Assert.Equal(width, image.Width);
        Assert.Equal(height, image.Height);
        Assert.Equal(expected, ReadRgbaBytes(image));
    }

    [Fact]
    public void Rgba32RoundTripIsByteExact()
    {
        using Image<Rgba32> source = CreatePattern<Rgba32>(5, 3);
        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            5,
            3);
        TextureSurface surface = texture.MipLevels[0].Surfaces[0];

        surface.CopyFrom(source);
        using Image<Rgba32> roundTripped = surface.ToImage<Rgba32>();

        Assert.Equal(ReadRgbaBytes(source), ReadRgbaBytes(roundTripped));
    }

    [Fact]
    public void Bgra32RoundTripIsByteExact()
    {
        using Image<Bgra32> source = CreatePattern<Bgra32>(4, 4);
        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            4,
            4);
        TextureSurface surface = texture.MipLevels[0].Surfaces[0];

        surface.CopyFrom(source);
        using Image<Bgra32> roundTripped = surface.ToImage<Bgra32>();

        Assert.Equal(ReadBytes(source), ReadBytes(roundTripped));
    }

    [Fact]
    public void Rgb24RoundTripKeepsColorAndAddsOpaqueAlpha()
    {
        using Image<Rgb24> source = CreatePattern<Rgb24>(4, 2);
        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            4,
            2);
        TextureSurface surface = texture.MipLevels[0].Surfaces[0];

        surface.CopyFrom(source);

        using Image<Rgb24> roundTripped = surface.ToImage<Rgb24>();
        Assert.Equal(ReadBytes(source), ReadBytes(roundTripped));

        using Image<Rgba32> asRgba = surface.ToImage<Rgba32>();
        for (int y = 0; y < asRgba.Height; y++)
        {
            for (int x = 0; x < asRgba.Width; x++)
            {
                Assert.Equal(255, asRgba[x, y].A);
            }
        }
    }

    [Fact]
    public void AlphaIsPreservedThroughAnRgba32RoundTrip()
    {
        using Image<Rgba32> source = new(4, 1);
        for (int x = 0; x < 4; x++)
        {
            source[x, 0] = new Rgba32(10, 20, 30, (byte)(x * 60));
        }

        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            4,
            1);
        TextureSurface surface = texture.MipLevels[0].Surfaces[0];

        surface.CopyFrom(source);
        using Image<Rgba32> roundTripped = surface.ToImage<Rgba32>();

        for (int x = 0; x < 4; x++)
        {
            Assert.Equal((byte)(x * 60), roundTripped[x, 0].A);
            Assert.Equal(new Rgba32(10, 20, 30, (byte)(x * 60)), roundTripped[x, 0]);
        }
    }

    [Fact]
    public void CopyFromImportsEditedPixels()
    {
        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            2,
            2);
        TextureSurface surface = texture.MipLevels[0].Surfaces[0];

        using (Image<Rgba32> image = surface.ToImage<Rgba32>())
        {
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    Span<Rgba32> row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        row[x] = new Rgba32(1, 2, 3, 4);
                    }
                }
            });

            surface.CopyFrom(image);
        }

        using Image<Rgba32> result = surface.ToImage<Rgba32>();
        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 2; x++)
            {
                Assert.Equal(new Rgba32(1, 2, 3, 4), result[x, y]);
            }
        }
    }

    [Fact]
    public void CopyFromRejectsDimensionMismatch()
    {
        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            4,
            4);
        TextureSurface surface = texture.MipLevels[0].Surfaces[0];
        using Image<Rgba32> image = new(5, 4);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => surface.CopyFrom(image));
        Assert.Equal("image", exception.ParamName);
        Assert.Contains("5x4", exception.Message, StringComparison.Ordinal);
        Assert.Contains("4x4", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToImageReturnsADetachedCopy()
    {
        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            2,
            2);
        TextureSurface surface = texture.MipLevels[0].Surfaces[0];
        byte[] payloadBefore = surface.GetPayload().ToArray();

        using (Image<Rgba32> image = surface.ToImage<Rgba32>())
        {
            image.ProcessPixelRows(accessor =>
            {
                Span<Rgba32> row = accessor.GetRowSpan(0);
                row.Fill(new Rgba32(255, 0, 255, 255));
            });
        }

        Assert.Equal(payloadBefore, surface.GetPayload().ToArray());
    }

    [Fact]
    public void LuminanceRoundTripKeepsAlphaOpaque()
    {
        using Image<Gray8> source = new(4, 4);
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                source[x, y] = new Gray8((byte)((x * 16) + (y * 4)));
            }
        }

        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            4,
            4);
        TextureSurface surface = texture.MipLevels[0].Surfaces[0];

        surface.CopyFrom(source);

        using Image<Gray8> roundTripped = surface.ToImage<Gray8>();
        Assert.Equal(ReadBytes(source), ReadBytes(roundTripped));

        using Image<Rgba32> asRgba = surface.ToImage<Rgba32>();
        Assert.Equal(255, asRgba[0, 0].A);
    }

    [Fact]
    public void DecodeOfA1024SquareStaysWithinTheAllocationBound()
    {
        const int size = 1024;
        using Texture texture = TestTextureFactory.CreateTexture(
            TestTextureFactory.CreateConfiguration(),
            size,
            size);
        TextureSurface surface = texture.MipLevels[0].Surfaces[0];

        using (Image<Rgba32> warmup = surface.ToImage<Rgba32>())
        {
            Assert.Equal(size, warmup.Width);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        using Image<Rgba32> image = surface.ToImage<Rgba32>();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        const long Limit = (4L * 1024 * 1024) + (128L * 1024);
        Assert.True(
            allocated <= Limit,
            $"Decoding a {size}x{size} R8G8B8A8_UNorm surface allocated {allocated} bytes, exceeding the {Limit} byte limit.");
    }

    private static Image<TPixel> CreatePattern<TPixel>(int width, int height)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Image<TPixel> image = new(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                image[x, y] = TPixel.FromRgba32(new Rgba32(
                    (byte)(x * 13),
                    (byte)(y * 29),
                    (byte)((x * 7) + (y * 3)),
                    (byte)(255 - (x * 5))));
            }
        }

        return image;
    }

    private static byte[] ReadRgbaBytes(Image<Rgba32> image)
    {
        List<byte> bytes = [];
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                foreach (Rgba32 pixel in accessor.GetRowSpan(y))
                {
                    bytes.Add(pixel.R);
                    bytes.Add(pixel.G);
                    bytes.Add(pixel.B);
                    bytes.Add(pixel.A);
                }
            }
        });

        return [.. bytes];
    }

    private static byte[] ReadBytes<TPixel>(Image<TPixel> image)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        byte[] bytes = new byte[image.Width * image.Height * Unsafe.SizeOf<TPixel>()];
        image.CopyPixelDataTo(bytes);
        return bytes;
    }
}
