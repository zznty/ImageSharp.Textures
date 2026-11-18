using ImageSharp.Textures.Formats.Dds;
using ImageSharp.Textures.Formats.Dds.Enums;

namespace ImageSharp.Textures.Tests.Formats.Dds;

[Trait("Category", "DdsDecode")]
public class DdsMetadataTests
{
    private static readonly string[] ExpectedMimeTypes = ["image/vnd.ms-dds"];
    private static readonly string[] ExpectedFileExtensions = [".dds"];

    [Theory]
    [InlineData(DxgiFormat.R8G8B8A8_UNorm, 32, TexelColorType.RGB | TexelColorType.Alpha,
        TexelAlphaRepresentation.None)]
    [InlineData(DxgiFormat.BC1_UNorm, 64, TexelColorType.RGB, TexelAlphaRepresentation.None)]
    [InlineData(DxgiFormat.BC4_UNorm, 64, TexelColorType.Red, TexelAlphaRepresentation.None)]
    [InlineData(DxgiFormat.BC5_UNorm, 128, TexelColorType.Red | TexelColorType.Green, TexelAlphaRepresentation.None)]
    [InlineData(DxgiFormat.BC7_UNorm, 128, TexelColorType.RGB | TexelColorType.Alpha, TexelAlphaRepresentation.None)]
    public void ToTextureConnectingMetadataMapsSupportedFormats(
        DxgiFormat dxgiFormat,
        int expectedBitsPerTexel,
        TexelColorType expectedColorType,
        TexelAlphaRepresentation expectedAlpha)
    {
        DdsTextureMetadata metadata = new()
        {
            HasDx10Header = true, DxgiFormat = dxgiFormat, AlphaMode = DdsAlphaMode.Opaque
        };

        TexelTypeInfo info = metadata.ToTextureConnectingMetadata().TexelTypeInfo;

        Assert.Equal(expectedBitsPerTexel, info.BitsPerTexel);
        Assert.Equal(expectedColorType, info.ColorType);
        Assert.Equal(expectedAlpha, info.AlphaRepresentation);
        Assert.Equal(TexelComponentInterpretation.UNorm, info.ComponentInfo.GetComponentInterpretation(0));
    }

    [Theory]
    [InlineData(DxgiFormat.BC1_UNorm_SRGB)]
    [InlineData(DxgiFormat.BC7_UNorm_SRGB)]
    public void SrgbFormatsUseSrgbComponentInterpretation(DxgiFormat dxgiFormat)
    {
        DdsTextureMetadata metadata = new() { HasDx10Header = true, DxgiFormat = dxgiFormat };

        TexelTypeInfo info = metadata.ToTextureConnectingMetadata().TexelTypeInfo;

        Assert.Equal(TexelComponentInterpretation.SRgbUNorm, info.ComponentInfo.GetComponentInterpretation(0));
    }

    [Theory]
    [InlineData(DdsAlphaMode.Opaque, TexelAlphaRepresentation.None)]
    [InlineData(DdsAlphaMode.Straight, TexelAlphaRepresentation.Unassociated)]
    [InlineData(DdsAlphaMode.Premultiplied, TexelAlphaRepresentation.Associated)]
    [InlineData(DdsAlphaMode.Custom, TexelAlphaRepresentation.Unassociated)]
    [InlineData(DdsAlphaMode.Unknown, TexelAlphaRepresentation.Unassociated)]
    public void AlphaModeMapsToAlphaRepresentation(DdsAlphaMode alphaMode, TexelAlphaRepresentation expected)
    {
        DdsTextureMetadata metadata = new()
        {
            HasDx10Header = true, DxgiFormat = DxgiFormat.BC7_UNorm, AlphaMode = alphaMode
        };

        Assert.Equal(expected, metadata.ToTextureConnectingMetadata().TexelTypeInfo.AlphaRepresentation);
    }

    [Theory]
    [InlineData(DdsFourCc.DXT1, 64, TexelColorType.RGB)]
    [InlineData(DdsFourCc.BC4U, 64, TexelColorType.Red)]
    [InlineData(DdsFourCc.BC5U, 128, TexelColorType.Red | TexelColorType.Green)]
    public void LegacyFourCcMetadataMapsToTheSameTexelType(DdsFourCc fourCc, int bitsPerTexel, TexelColorType colorType)
    {
        DdsTextureMetadata metadata = new() { HasDx10Header = false, FourCc = fourCc, AlphaMode = DdsAlphaMode.Opaque };

        TexelTypeInfo info = metadata.ToTextureConnectingMetadata().TexelTypeInfo;

        Assert.Equal(bitsPerTexel, info.BitsPerTexel);
        Assert.Equal(colorType, info.ColorType);
    }

    [Theory]
    [InlineData(64, TexelColorType.RGB, TexelAlphaRepresentation.None, DdsFourCc.DXT1)]
    [InlineData(64, TexelColorType.Red, TexelAlphaRepresentation.None, DdsFourCc.BC4U)]
    [InlineData(128, TexelColorType.Red | TexelColorType.Green, TexelAlphaRepresentation.None, DdsFourCc.BC5U)]
    public void FromTextureConnectingMetadataPrefersLegacyFourCodes(
        int bitsPerTexel,
        TexelColorType colorType,
        TexelAlphaRepresentation alpha,
        DdsFourCc expectedFourCc)
    {
        TextureConnectingMetadata connecting = new()
        {
            TexelTypeInfo = CreateTypeInfo(bitsPerTexel, colorType, alpha, TexelComponentInterpretation.UNorm)
        };

        DdsTextureMetadata metadata = DdsTextureMetadata.FromTextureConnectingMetadata(connecting);

        Assert.False(metadata.HasDx10Header);
        Assert.Equal(expectedFourCc, metadata.FourCc);
        Assert.Equal(DxgiFormat.Unknown, metadata.DxgiFormat);
        Assert.Equal(DdsAlphaMode.Opaque, metadata.AlphaMode);
        Assert.Equal(connecting.TexelTypeInfo, metadata.ToTextureConnectingMetadata().TexelTypeInfo);
    }

    [Theory]
    [InlineData(DxgiFormat.BC1_UNorm_SRGB)]
    [InlineData(DxgiFormat.BC7_UNorm)]
    [InlineData(DxgiFormat.BC7_UNorm_SRGB)]
    [InlineData(DxgiFormat.R8G8B8A8_UNorm)]
    public void FromTextureConnectingMetadataUsesDx10ForNonLegacyFormats(DxgiFormat dxgiFormat)
    {
        DdsTextureMetadata source = new() { HasDx10Header = true, DxgiFormat = dxgiFormat };
        TextureConnectingMetadata connecting = source.ToTextureConnectingMetadata();

        DdsTextureMetadata metadata = DdsTextureMetadata.FromTextureConnectingMetadata(connecting);

        Assert.True(metadata.HasDx10Header);
        Assert.Equal(dxgiFormat, metadata.DxgiFormat);
        Assert.Equal(DdsFourCc.None, metadata.FourCc);
        Assert.Equal(D3d10ResourceDimension.Texture2D, metadata.ResourceDimension);
        Assert.Equal(1U, metadata.ArraySize);
        Assert.Equal(connecting.TexelTypeInfo, metadata.ToTextureConnectingMetadata().TexelTypeInfo);
    }

    [Fact]
    public void FromTextureConnectingMetadataRejectsUnknownTexelTypes()
    {
        TextureConnectingMetadata connecting = new()
        {
            TexelTypeInfo = CreateTypeInfo(
                24,
                TexelColorType.RGB,
                TexelAlphaRepresentation.None,
                TexelComponentInterpretation.UNorm)
        };

        NotSupportedException exception =
            Assert.Throws<NotSupportedException>(() => DdsTextureMetadata.FromTextureConnectingMetadata(connecting));
        Assert.Contains("DDS", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToTextureConnectingMetadataRejectsUnsupportedDxgiValues()
    {
        DdsTextureMetadata metadata = new() { HasDx10Header = true, DxgiFormat = (DxgiFormat)10 };

        NotSupportedException exception =
            Assert.Throws<NotSupportedException>(() => metadata.ToTextureConnectingMetadata());
        Assert.Contains("10", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DeepCloneCopiesEveryField()
    {
        DdsTextureMetadata metadata = new()
        {
            HasDx10Header = true,
            DxgiFormat = DxgiFormat.BC7_UNorm,
            FourCc = DdsFourCc.None,
            ResourceDimension = D3d10ResourceDimension.Texture3D,
            ResourceMiscFlags = D3d10ResourceMiscFlags.TextureCube,
            ArraySize = 3,
            MipMapCount = 4,
            Depth = 5,
            AlphaMode = DdsAlphaMode.Premultiplied
        };

        DdsTextureMetadata clone = metadata.DeepClone();
        clone.ArraySize = 9;

        Assert.NotSame(metadata, clone);
        Assert.Equal(DxgiFormat.BC7_UNorm, clone.DxgiFormat);
        Assert.Equal(D3d10ResourceDimension.Texture3D, clone.ResourceDimension);
        Assert.Equal(D3d10ResourceMiscFlags.TextureCube, clone.ResourceMiscFlags);
        Assert.Equal(4U, clone.MipMapCount);
        Assert.Equal(5U, clone.Depth);
        Assert.Equal(DdsAlphaMode.Premultiplied, clone.AlphaMode);
        Assert.Equal(3U, metadata.ArraySize);
    }

    [Fact]
    public void FormatDescriptorDescribesTheContainer()
    {
        DdsFormat format = DdsFormat.Instance;

        Assert.Equal("DDS", format.Name);
        Assert.Equal("image/vnd.ms-dds", format.DefaultMimeType);
        Assert.Equal(ExpectedMimeTypes, format.MimeTypes);
        Assert.Equal(ExpectedFileExtensions, format.FileExtensions);
        Assert.NotNull(format.CreateDefaultFormatMetadata());
        Assert.Equal(DxgiFormat.Unknown, format.CreateDefaultFormatMetadata().DxgiFormat);
    }

    private static TexelTypeInfo CreateTypeInfo(
        int bitsPerTexel,
        TexelColorType colorType,
        TexelAlphaRepresentation alpha,
        TexelComponentInterpretation interpretation)
    {
        int componentCount = bitsPerTexel == 32 ? 4 :
            bitsPerTexel == 128 && colorType.HasFlag(TexelColorType.Green) ? 2 : 1;
        int precision = bitsPerTexel / componentCount;
        int[] precisions = new int[componentCount];
        TexelComponentInterpretation[] interpretations = new TexelComponentInterpretation[componentCount];

        for (int i = 0; i < componentCount; i++)
        {
            precisions[i] = precision;
            interpretations[i] = interpretation;
        }

        return new TexelTypeInfo
        {
            BitsPerTexel = bitsPerTexel,
            ColorType = colorType,
            ComponentInfo = new TexelComponentInfo(precisions, interpretations),
            AlphaRepresentation = alpha
        };
    }
}
