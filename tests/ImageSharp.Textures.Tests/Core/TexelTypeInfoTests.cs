namespace ImageSharp.Textures.Tests.Core;

[Trait("Category", "Core")]
public class TexelTypeInfoTests
{
    [Fact]
    public void ColorTypeFlagsComposeAsSpecified()
    {
        Assert.Equal(TexelColorType.Red | TexelColorType.Green | TexelColorType.Blue | (TexelColorType)(1 << 10),
            TexelColorType.RGB);
        Assert.Equal(TexelColorType.Blue | TexelColorType.Green | TexelColorType.Red | (TexelColorType)(1 << 11),
            TexelColorType.BGR);
        Assert.Equal(TexelColorType.Depth | TexelColorType.Stencil, TexelColorType.DepthStencil);
        Assert.Equal(1 << 13, (int)TexelColorType.Other);
        Assert.False(TexelColorType.RGB.HasFlag(TexelColorType.Alpha));
        Assert.True((TexelColorType.RGB | TexelColorType.Alpha).HasFlag(TexelColorType.Red));
    }

    [Fact]
    public void ComponentInfoExposesPrecisionsAndInterpretations()
    {
        TexelComponentInfo info = new(
            [8, 8, 8, 8],
            [
                TexelComponentInterpretation.UNorm,
                TexelComponentInterpretation.UNorm,
                TexelComponentInterpretation.SRgbUNorm,
                TexelComponentInterpretation.UNorm
            ],
            0);

        Assert.Equal(4, info.ComponentCount);
        Assert.Equal(0, info.Padding);
        Assert.Equal(8, info.GetComponentPrecision(2));
        Assert.Equal(TexelComponentInterpretation.SRgbUNorm, info.GetComponentInterpretation(2));
    }

    [Fact]
    public void ComponentInfoRejectsMismatchedComponentCounts()
    {
        ArgumentException exception =
            Assert.Throws<ArgumentException>(() =>
                new TexelComponentInfo([8, 8], [TexelComponentInterpretation.UNorm]));
        Assert.Equal("componentInterpretations", exception.ParamName);
    }

    [Fact]
    public void ComponentInfoRejectsInvalidPrecisions()
    {
        Assert.Throws<ArgumentException>(() => new TexelComponentInfo([], []));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TexelComponentInfo([0], [TexelComponentInterpretation.UNorm]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TexelComponentInfo([8], [TexelComponentInterpretation.UNorm], -1));
    }

    [Fact]
    public void ComponentInfoRejectsOutOfRangeIndexes()
    {
        TexelComponentInfo info = new([8], [TexelComponentInterpretation.UNorm]);
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(() => info.GetComponentPrecision(1));
        Assert.Equal("componentIndex", exception.ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => info.GetComponentInterpretation(-1));
    }

    [Fact]
    public void ComponentInfoValueEqualityComparesComponents()
    {
        TexelComponentInfo left = new([8, 8], [TexelComponentInterpretation.UNorm, TexelComponentInterpretation.SNorm]);
        TexelComponentInfo same = new([8, 8], [TexelComponentInterpretation.UNorm, TexelComponentInterpretation.SNorm]);
        TexelComponentInfo different =
            new([8, 8], [TexelComponentInterpretation.UNorm, TexelComponentInterpretation.UNorm]);

        Assert.True(left == same);
        Assert.Equal(left.GetHashCode(), same.GetHashCode());
        Assert.True(left != different);
    }

    [Fact]
    public void TypeInfoValueEqualityComparesEveryField()
    {
        TexelTypeInfo left = CreateTypeInfo(32, TexelColorType.RGB | TexelColorType.Alpha,
            TexelAlphaRepresentation.Unassociated);
        TexelTypeInfo same = CreateTypeInfo(32, TexelColorType.RGB | TexelColorType.Alpha,
            TexelAlphaRepresentation.Unassociated);
        TexelTypeInfo differentAlpha =
            CreateTypeInfo(32, TexelColorType.RGB | TexelColorType.Alpha, TexelAlphaRepresentation.None);
        TexelTypeInfo differentBits = CreateTypeInfo(24, TexelColorType.RGB | TexelColorType.Alpha,
            TexelAlphaRepresentation.Unassociated);

        Assert.Equal(left, same);
        Assert.True(left == same);
        Assert.NotEqual(left, differentAlpha);
        Assert.NotEqual(left, differentBits);
    }

    private static TexelTypeInfo CreateTypeInfo(int bits, TexelColorType colorType, TexelAlphaRepresentation alpha)
        => new()
        {
            BitsPerTexel = bits,
            ColorType = colorType,
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
