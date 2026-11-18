using ImageSharp.Textures.Formats.Dds;
using ImageSharp.Textures.Formats.Ktx2;
using ImageSharp.Textures.Formats.Ktx;
using ImageSharp.Textures.ILGPU;
using ImageSharp.Textures.Texels.Bc7;
using ImageSharp.Textures.Vulkan;

namespace ImageSharp.Textures.Tests.TexelOperations.Bc7;

[Trait("Category", "BcEncode")]
public class Bc7AcceleratedEncodeTests
{
    [Theory]
    [InlineData(64, 64, Bc7EncodingQuality.Quick)]
    [InlineData(64, 64, Bc7EncodingQuality.Default)]
    [InlineData(128, 4, Bc7EncodingQuality.Quick)]
    [InlineData(17, 33, Bc7EncodingQuality.Quick)]
    [InlineData(18, 34, Bc7EncodingQuality.Quick)]
    [InlineData(19, 35, Bc7EncodingQuality.Default)]
    public void EncodeParallelIsByteIdenticalToSingleThreaded(int width, int height, Bc7EncodingQuality quality)
    {
        using Image<Rgba32> image = CreateGradient(width, height);
        byte[] rgba = new byte[width * height * 4];
        image.CopyPixelDataTo(rgba);

        int byteCount = Bc7Encoder.GetEncodedByteCount(width, height);
        byte[] singleThreaded = new byte[byteCount];
        byte[] multiThreaded = new byte[byteCount];

        Bc7Encoder.Encode(rgba, singleThreaded, width, height, quality);
        Bc7Encoder.EncodeParallel(rgba, multiThreaded, width, height, quality, 4);

        Assert.Equal(singleThreaded, multiThreaded);
    }

    [Theory]
    [InlineData(64, 64, Bc7EncodingQuality.Quick)]
    [InlineData(64, 64, Bc7EncodingQuality.Default)]
    [InlineData(17, 33, Bc7EncodingQuality.Quick)]
    [InlineData(18, 34, Bc7EncodingQuality.Quick)]
    [InlineData(19, 35, Bc7EncodingQuality.Default)]
    public void VulkanBc7EncoderRoundTripsGradientWithinErrorBound(int width, int height, Bc7EncodingQuality quality)
    {
        if (!VulkanBc7Encoder.TryCreate(out VulkanBc7Encoder? encoder))
        {
            return;
        }

        using (encoder)
        {
            using Image<Rgba32> source = CreateGradient(width, height);
            byte[] rgba = new byte[width * height * 4];
            source.CopyPixelDataTo(rgba);

            int byteCount = Bc7Encoder.GetEncodedByteCount(width, height);
            byte[] encoded = new byte[byteCount];
            Assert.True(encoder.TryEncode(rgba, encoded, width, height, quality));

            using Image<Rgba32> decoded = BcEncodeTestHelpers.Decode(TexelFormat.Bc7_UNorm, encoded, width, height);
            BcEncodeTestHelpers.AssertMaxChannelError(source, decoded, 16,
                $"Vulkan BC7 ({quality}, {encoder.DeviceName})");
        }
    }

    [Theory]
    [InlineData(64, 64, Bc7EncodingQuality.Quick)]
    [InlineData(64, 64, Bc7EncodingQuality.Default)]
    [InlineData(17, 33, Bc7EncodingQuality.Quick)]
    [InlineData(18, 34, Bc7EncodingQuality.Quick)]
    [InlineData(19, 35, Bc7EncodingQuality.Default)]
    public void IlgpuBc7EncoderRoundTripsAndMatchesBetweenGpuAndCpuAccelerators(int width, int height,
        Bc7EncodingQuality quality)
    {
        using IlgpuBc7Encoder gpuEncoder = new(false);
        using IlgpuBc7Encoder cpuEncoder = new(true);

        using Image<Rgba32> source = CreateGradient(width, height);
        byte[] rgba = new byte[width * height * 4];
        source.CopyPixelDataTo(rgba);

        int byteCount = Bc7Encoder.GetEncodedByteCount(width, height);
        byte[] gpuEncoded = new byte[byteCount];
        byte[] cpuEncoded = new byte[byteCount];

        Assert.True(gpuEncoder.TryEncode(rgba, gpuEncoded, width, height, quality));
        Assert.True(cpuEncoder.TryEncode(rgba, cpuEncoded, width, height, quality));

        Assert.Equal(cpuEncoded, gpuEncoded);

        using Image<Rgba32> decoded = BcEncodeTestHelpers.Decode(TexelFormat.Bc7_UNorm, gpuEncoded, width, height);
        BcEncodeTestHelpers.AssertMaxChannelError(source, decoded, 16,
            $"ILGPU BC7 ({quality}, {gpuEncoder.AcceleratorName})");
    }

    [Theory]
    [InlineData(64, 64, Bc7EncodingQuality.Quick)]
    [InlineData(64, 64, Bc7EncodingQuality.Default)]
    [InlineData(17, 33, Bc7EncodingQuality.Quick)]
    [InlineData(18, 34, Bc7EncodingQuality.Quick)]
    [InlineData(19, 35, Bc7EncodingQuality.Default)]
    public void VulkanAndIlgpuMatchDirectXTexShaderOutput(int width, int height, Bc7EncodingQuality quality)
    {
        if (!VulkanBc7Encoder.TryCreate(out VulkanBc7Encoder? vulkanEncoder))
        {
            return;
        }

        using (vulkanEncoder)
        using (IlgpuBc7Encoder ilgpuEncoder = new(false))
        {
            using Image<Rgba32> source = CreateGradient(width, height);
            byte[] rgba = new byte[width * height * 4];
            source.CopyPixelDataTo(rgba);

            int byteCount = Bc7Encoder.GetEncodedByteCount(width, height);
            byte[] vulkanEncoded = new byte[byteCount];
            byte[] ilgpuEncoded = new byte[byteCount];

            Assert.True(vulkanEncoder.TryEncode(rgba, vulkanEncoded, width, height, quality));
            Assert.True(ilgpuEncoder.TryEncode(rgba, ilgpuEncoded, width, height, quality));

            Assert.Equal(vulkanEncoded, ilgpuEncoded);
        }
    }

    [Fact]
    public void IlgpuBc7EncoderIsThreadSafeUnderConcurrentCalls()
    {
        using IlgpuBc7Encoder encoder = new(false);
        using Image<Rgba32> source = CreateGradient(32, 32);
        byte[] rgba = new byte[32 * 32 * 4];
        source.CopyPixelDataTo(rgba);

        int byteCount = Bc7Encoder.GetEncodedByteCount(32, 32);
        byte[] expected = new byte[byteCount];
        Assert.True(encoder.TryEncode(rgba, expected, 32, 32, Bc7EncodingQuality.Quick));

        Parallel.For(
            0,
            8,
            _ =>
            {
                byte[] actual = new byte[byteCount];
                Assert.True(encoder.TryEncode(rgba, actual, 32, 32, Bc7EncodingQuality.Quick));
                Assert.Equal(expected, actual);
            });
    }

    [Fact]
    public void ContainerEncodersUseConfiguredHardwareAndParallelOptions()
    {
        using IlgpuBc7Encoder encoder = new(false);
        using Image<Rgba32> sourceImage = CreateGradient(32, 32);
        using Texture texture = new(TextureConfiguration.Default, new TextureMetadata());
        TextureMipLevel mip = texture.AddMipLevel(32, 32, 1);
        TextureSurface surface = mip.CreateSurface(TexelFormat.R8G8B8A8_UNorm);
        surface.CopyFrom(sourceImage);

        using MemoryStream ddsStream = new();
        texture.Save(
            ddsStream,
            new DdsTextureEncoder(new DdsEncodingOptions
            {
                TargetTexelFormat = TexelFormat.Bc7_UNorm,
                Bc7Quality = Bc7EncodingQuality.Quick,
                Bc7HardwareEncoder = encoder
            }));

        ddsStream.Position = 0;
        using Texture ddsLoaded = Texture.Load(ddsStream);
        Assert.Equal(TexelFormat.Bc7_UNorm, ddsLoaded.MipLevels[0].Surfaces[0].TexelFormat);

        using MemoryStream ktx2Stream = new();
        texture.Save(
            ktx2Stream,
            new Ktx2TextureEncoder(new Ktx2EncodingOptions
            {
                TargetTexelFormat = TexelFormat.Bc7_UNorm,
                Bc7Quality = Bc7EncodingQuality.Quick,
                Bc7HardwareEncoder = encoder
            }));

        ktx2Stream.Position = 0;
        using Texture ktx2Loaded = Texture.Load(ktx2Stream);
        Assert.Equal(TexelFormat.Bc7_UNorm, ktx2Loaded.MipLevels[0].Surfaces[0].TexelFormat);

        using MemoryStream ktxStream = new();
        texture.Save(
            ktxStream,
            new KtxTextureEncoder(new KtxEncodingOptions
            {
                TargetTexelFormat = TexelFormat.Bc7_UNorm,
                Bc7Quality = Bc7EncodingQuality.Quick,
                MaxDegreeOfParallelism = 4
            }));

        ktxStream.Position = 0;
        using Texture ktxLoaded = Texture.Load(ktxStream);
        Assert.Equal(TexelFormat.Bc7_UNorm, ktxLoaded.MipLevels[0].Surfaces[0].TexelFormat);
    }

    private static Image<Rgba32> CreateGradient(int width, int height)
        => BcEncodeTestHelpers.CreateImage(
            width,
            height,
            (x, y) => new Rgba32(
                (byte)(x * 255 / Math.Max(1, width - 1)),
                (byte)(y * 255 / Math.Max(1, height - 1)),
                (byte)((x + y) * 255 / Math.Max(1, width + height - 2)),
                255));
}
