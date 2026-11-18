using ImageSharp.Textures.Formats.Dds;
using ImageSharp.Textures.Formats.Dds.Enums;
using ImageSharp.Textures.Tests.TestUtilities;
using ImageSharp.Textures.Tests.TestUtilities.Dds;

namespace ImageSharp.Textures.Tests.Formats.Dds;

[Trait("Category", "DdsDecode")]
public class DdsMalformedTests
{
    private const int Rgba8BlockBytes = 0;

    private static readonly TextureConfiguration Configuration = TextureConfiguration.Default;

    [Fact]
    public void MissingMagicWordThrows()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 2, 2, Rgba8BlockBytes);
        builder.IncludeMagic = false;
        builder.AddPatternPayloads();

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Decode(builder));
        Assert.Contains("magic word", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WrongHeaderSizeThrows()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 2, 2, Rgba8BlockBytes);
        builder.HeaderSize = 100;
        builder.AddPatternPayloads();

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Decode(builder));
        Assert.Contains("100", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WrongPixelFormatSizeThrows()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 2, 2, Rgba8BlockBytes);
        builder.PixelFormatSize = 24;
        builder.AddPatternPayloads();

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Decode(builder));
        Assert.Contains("24", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingTextureCapabilityThrows()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 2, 2, Rgba8BlockBytes);
        builder.Caps1 = 0;
        builder.AddPatternPayloads();

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Decode(builder));
        Assert.Contains("texture surface", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PitchAndLinearSizeTogetherThrow()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 2, 2, Rgba8BlockBytes);
        builder.Flags |= 0x8;
        builder.AddPatternPayloads();

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Decode(builder));
        Assert.Contains("pitch", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnknownFourCcThrowsNotSupportedNamingTheCode()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateLegacy(DdsFourCc.DXT1, 4, 4, 8);
        builder.FourCc = 'D' | ('X' << 8) | ('T' << 16) | ('5' << 24);
        builder.AddPatternPayloads();

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => Decode(builder));
        Assert.Contains("DXT5", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownDxgiFormatThrowsNotSupportedNamingTheValue()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10(10, 4, 4, 8);
        builder.AddPatternPayloads();

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => Decode(builder));
        Assert.Contains("10", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UncompressedLegacyPixelFormatThrowsNotSupported()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 2, 2, Rgba8BlockBytes);
        builder.HasDx10Header = false;
        builder.FourCc = 0;
        builder.PixelFormatFlags = 0x41;
        builder.RgbBitCount = 32;
        builder.AddPatternPayloads();

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => Decode(builder));
        Assert.Contains("Uncompressed DDS pixel formats", exception.Message, StringComparison.Ordinal);
        Assert.Contains("32", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(2, 0)]
    public void ZeroDimensionsThrow(int width, int height)
    {
        DdsFileBuilder builder =
            DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, width, height, Rgba8BlockBytes);
        builder.AddPatternPayloads();

        Assert.Throws<InvalidImageContentException>(() => Decode(builder));
    }

    [Fact]
    public void DimensionsAboveTheSaneLimitThrow()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 16385, 1, Rgba8BlockBytes);
        builder.AddPatternPayloads();

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Decode(builder));
        Assert.Contains("16385", exception.Message, StringComparison.Ordinal);
        Assert.Contains("16384", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MipCountFlagWithoutLevelsThrows()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 4, 4, Rgba8BlockBytes);
        builder.MipLevelCount = 0;
        builder.Flags |= 0x20000;
        builder.AddPatternPayloads();

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Decode(builder));
        Assert.Contains("zero mip levels", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MoreMipLevelsThanGeometryAllowsThrows()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 4, 4, Rgba8BlockBytes);
        builder.MipLevelCount = 4;
        builder.WithDerivedHeaderFields();
        builder.AddPatternPayloads();

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Decode(builder));
        Assert.Contains("4 mip levels", exception.Message, StringComparison.Ordinal);
        Assert.Contains("4x4", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TruncatedPayloadThrows()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 4, 4, Rgba8BlockBytes);
        builder.AddPatternPayloads();
        builder.TruncatePayload(10);

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Decode(builder));
        Assert.Contains("truncated", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("64 payload bytes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TruncatedHeaderThrows()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 4, 4, Rgba8BlockBytes);
        byte[] file = builder.Build();

        using MemoryStream stream = new(file[..60]);
        InvalidImageContentException exception =
            Assert.Throws<InvalidImageContentException>(() => new DdsTextureDecoder().Decode(Configuration, stream));
        Assert.Contains("header", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TruncatedDx10HeaderThrows()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 4, 4, Rgba8BlockBytes);
        byte[] file = builder.Build();

        using MemoryStream stream = new(file[..(4 + 124)]);
        InvalidImageContentException exception =
            Assert.Throws<InvalidImageContentException>(() => new DdsTextureDecoder().Decode(Configuration, stream));
        Assert.Contains("DX10 header", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NonSeekableTruncatedPayloadThrows()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 4, 4, Rgba8BlockBytes);
        builder.AddPatternPayloads();
        builder.TruncatePayload(20);

        using MemoryStream content = new(builder.Build());
        using NonSeekableStream stream = new(content);

        InvalidImageContentException exception =
            Assert.Throws<InvalidImageContentException>(() => new DdsTextureDecoder().Decode(Configuration, stream));
        Assert.Contains("ended while reading", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CubeMapWithoutSquareFacesThrows()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 4, 2, Rgba8BlockBytes);
        builder.IsCubeMap = true;
        builder.WithDerivedHeaderFields();
        builder.AddPatternPayloads();

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Decode(builder));
        Assert.Contains("square", exception.Message, StringComparison.Ordinal);
        Assert.Contains("4x2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CubeMapMissingFaceBitsThrows()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateLegacy(DdsFourCc.DXT1, 4, 4, 8);
        builder.Caps2 = 0x200;
        builder.AddPatternPayloads();

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Decode(builder));
        Assert.Contains("six faces", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VolumeWithDepthOneThrows()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 4, 4, Rgba8BlockBytes);
        builder.IsVolume = true;
        builder.Depth = 1;
        builder.WithDerivedHeaderFields();
        builder.AddPatternPayloads();

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Decode(builder));
        Assert.Contains("depth 1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VolumeAndCubeMapTogetherThrow()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 4, 4, Rgba8BlockBytes);
        builder.IsVolume = true;
        builder.Depth = 2;
        builder.IsCubeMap = true;
        builder.WithDerivedHeaderFields();
        builder.AddPatternPayloads();

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Decode(builder));
        Assert.Contains("cube map and a volume", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VolumeCapsDisagreeingWithDx10DimensionThrow()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 4, 4, Rgba8BlockBytes);
        builder.Depth = 2;
        builder.Flags |= 0x800000;
        builder.Caps2 |= 0x200000;
        builder.ResourceDimension = 3;
        builder.AddPatternPayloads();

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Decode(builder));
        Assert.Contains("volume texture", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BufferResourceThrowsNotSupported()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 4, 4, Rgba8BlockBytes);
        builder.ResourceDimension = 1;
        builder.AddPatternPayloads();

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => Decode(builder));
        Assert.Contains("buffer", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ZeroArraySizeThrows()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 4, 4, Rgba8BlockBytes);
        builder.ArraySize = 0;
        builder.AddPatternPayloads();

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Decode(builder));
        Assert.Contains("zero array elements", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VolumeArraySizeAboveOneThrows()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 4, 4, Rgba8BlockBytes);
        builder.IsVolume = true;
        builder.Depth = 2;
        builder.ArraySize = 2;
        builder.WithDerivedHeaderFields();
        builder.AddPatternPayloads();

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Decode(builder));
        Assert.Contains("volume textures must declare exactly one", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IdentifySucceedsOnHeaderOnlyFilesAndDecodeFails()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 4, 4, Rgba8BlockBytes);
        byte[] file = builder.Build();

        using MemoryStream stream = new(file);
        TextureInfo info = new DdsTextureDecoder().Identify(Configuration, stream);
        Assert.Equal(4, info.Width);
        Assert.Equal(1, info.SurfaceCount);

        stream.Position = 0;
        Assert.Throws<InvalidImageContentException>(() => new DdsTextureDecoder().Decode(Configuration, stream));
    }

    private static Texture Decode(DdsFileBuilder builder)
    {
        using MemoryStream stream = new(builder.Build());
        return new DdsTextureDecoder().Decode(Configuration, stream);
    }
}
