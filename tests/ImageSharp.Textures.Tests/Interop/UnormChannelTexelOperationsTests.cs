using ImageSharp.Textures.Texels.Uncompressed;

namespace ImageSharp.Textures.Tests.Interop;

[Trait("Category", "Interop")]
public class UnormChannelTexelOperationsTests
{
    [Fact]
    public void R8DescribesSingleChannelUnorm()
    {
        TexelTypeInfo info = UnormChannelTexelOperations.R8.GetTexelTypeInfo();

        Assert.Equal(8, info.BitsPerTexel);
        Assert.Equal(TexelColorType.Red, info.ColorType);
        Assert.Equal(1, info.ComponentInfo.ComponentCount);
        Assert.Equal(TexelAlphaRepresentation.None, info.AlphaRepresentation);
        Assert.Equal(4, UnormChannelTexelOperations.R8.GetEncodedByteCount(2, 2));
    }

    [Fact]
    public void R8DecodeSetsRedAndOpaqueAlpha()
    {
        byte[] payload = [0, 64, 128, 255];
        using Image<Rgba32> image = UnormChannelTexelOperations.R8.Decode<Rgba32>(
            payload,
            Configuration.Default,
            2,
            2);

        Assert.Equal(new Rgba32(0, 0, 0, 255), image[0, 0]);
        Assert.Equal(new Rgba32(64, 0, 0, 255), image[1, 0]);
        Assert.Equal(new Rgba32(128, 0, 0, 255), image[0, 1]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), image[1, 1]);
    }

    [Fact]
    public void R8EncodeReadsTheRedChannel()
    {
        using Image<Rgba32> image = new(2, 1);
        image[0, 0] = new Rgba32(12, 200, 40, 255);
        image[1, 0] = new Rgba32(250, 1, 2, 0);

        byte[] destination = new byte[2];
        UnormChannelTexelOperations.R8.Encode(image, destination);

        Assert.Equal([12, 250], destination);
    }

    [Fact]
    public void R8G8DescribesDualChannelUnorm()
    {
        TexelTypeInfo info = UnormChannelTexelOperations.R8G8.GetTexelTypeInfo();

        Assert.Equal(16, info.BitsPerTexel);
        Assert.Equal(TexelColorType.Red | TexelColorType.Green, info.ColorType);
        Assert.Equal(2, info.ComponentInfo.ComponentCount);
        Assert.Equal(8, info.ComponentInfo.GetComponentPrecision(1));
        Assert.Equal(8, UnormChannelTexelOperations.R8G8.GetEncodedByteCount(2, 2));
    }

    [Fact]
    public void R8G8DecodeSetsRedAndGreenWithZeroBlueAndOpaqueAlpha()
    {
        byte[] payload = [0, 255, 64, 128];
        using Image<Rgba32> image = UnormChannelTexelOperations.R8G8.Decode<Rgba32>(
            payload,
            Configuration.Default,
            2,
            1);

        Assert.Equal(new Rgba32(0, 255, 0, 255), image[0, 0]);
        Assert.Equal(new Rgba32(64, 128, 0, 255), image[1, 0]);
    }

    [Fact]
    public void R8G8EncodeReadsTheRedAndGreenChannels()
    {
        using Image<Rgba32> image = new(2, 1);
        image[0, 0] = new Rgba32(1, 2, 3, 4);
        image[1, 0] = new Rgba32(250, 251, 252, 253);

        byte[] destination = new byte[4];
        UnormChannelTexelOperations.R8G8.Encode(image, destination);

        Assert.Equal([1, 2, 250, 251], destination);
    }

    [Fact]
    public void DecodeRejectsShortPayloads()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            UnormChannelTexelOperations.R8G8.Decode<Rgba32>([0, 1, 2], Configuration.Default, 2, 2));
        Assert.Equal("source", exception.ParamName);
        Assert.Contains("8 bytes are required for a 2x2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EncodeRejectsShortDestinations()
    {
        using Image<Rgba32> image = new(4, 4);

        ArgumentException exception =
            Assert.Throws<ArgumentException>(() => UnormChannelTexelOperations.R8G8.Encode(image, new byte[16]));
        Assert.Equal("destination", exception.ParamName);
        Assert.Contains("32 bytes are required for a 4x4", exception.Message, StringComparison.Ordinal);
    }
}
