using ImageSharp.Textures.Formats.Ktx.Enums;

namespace ImageSharp.Textures.Formats.Ktx;

/// <summary>
///     Maps KTX v1 <c>glInternalFormat</c> values to <see cref="TexelFormat" /> values and back, following the
///     authoritative mapping table of the rewrite design.
/// </summary>
internal static class KtxTexelFormats
{
    private const TexelColorType ColorChannels = TexelColorType.Red | TexelColorType.Green | TexelColorType.Blue;
    private const TexelColorType RedGreenChannels = TexelColorType.Red | TexelColorType.Green;

    /// <summary>
    ///     Tries to map a <c>glInternalFormat</c> value to a texel format.
    /// </summary>
    /// <param name="glInternalFormat">The OpenGL internal format value.</param>
    /// <param name="texelFormat">The mapped texel format.</param>
    /// <returns><see langword="true" /> when the value is supported; otherwise, <see langword="false" />.</returns>
    internal static bool TryGetTexelFormat(uint glInternalFormat, out TexelFormat texelFormat)
    {
        switch ((GlInternalFormat)glInternalFormat)
        {
            case GlInternalFormat.Rgba8:
                texelFormat = TexelFormat.R8G8B8A8_UNorm;
                return true;
            case GlInternalFormat.CompressedRgbS3tcDxt1Ext:
            case GlInternalFormat.CompressedRgbaS3tcDxt1Ext:
                texelFormat = TexelFormat.Bc1_UNorm;
                return true;
            case GlInternalFormat.CompressedSrgbS3tcDxt1Ext:
            case GlInternalFormat.CompressedSrgbAlphaS3tcDxt1Ext:
                texelFormat = TexelFormat.Bc1_UnormSrgb;
                return true;
            case GlInternalFormat.CompressedRedRgtc1:
                texelFormat = TexelFormat.Bc4_UNorm;
                return true;
            case GlInternalFormat.CompressedRgRgtc2:
                texelFormat = TexelFormat.Bc5_UNorm;
                return true;
            case GlInternalFormat.CompressedRgbaBptcUnorm:
                texelFormat = TexelFormat.Bc7_UNorm;
                return true;
            case GlInternalFormat.CompressedSrgbAlphaBptcUnorm:
                texelFormat = TexelFormat.Bc7_UnormSrgb;
                return true;
            default:
                texelFormat = TexelFormat.Unknown;
                return false;
        }
    }

    /// <summary>
    ///     Tries to map a texel type description back to a texel format.
    /// </summary>
    /// <param name="texelTypeInfo">The texel type description.</param>
    /// <param name="texelFormat">The mapped texel format.</param>
    /// <returns><see langword="true" /> when the description matches a supported format; otherwise, <see langword="false" />.</returns>
    internal static bool TryGetTexelFormat(TexelTypeInfo texelTypeInfo, out TexelFormat texelFormat)
    {
        TexelColorType channels = texelTypeInfo.ColorType & ColorChannels;
        bool isSrgb = IsSrgb(texelTypeInfo);
        texelFormat = TexelFormat.Unknown;

        if (channels == ColorChannels)
        {
            texelFormat = texelTypeInfo.BitsPerTexel switch
            {
                32 => TexelFormat.R8G8B8A8_UNorm,
                64 when isSrgb => TexelFormat.Bc1_UnormSrgb,
                64 => TexelFormat.Bc1_UNorm,
                128 when isSrgb => TexelFormat.Bc7_UnormSrgb,
                128 => TexelFormat.Bc7_UNorm,
                _ => TexelFormat.Unknown
            };
        }
        else if (channels == TexelColorType.Red)
        {
            texelFormat = texelTypeInfo.BitsPerTexel == 64 ? TexelFormat.Bc4_UNorm : TexelFormat.Unknown;
        }
        else if (channels == RedGreenChannels)
        {
            texelFormat = texelTypeInfo.BitsPerTexel == 128 ? TexelFormat.Bc5_UNorm : TexelFormat.Unknown;
        }

        return texelFormat != TexelFormat.Unknown;
    }

    /// <summary>
    ///     Gets the OpenGL header values that store the given texel format.
    /// </summary>
    /// <param name="texelFormat">The texel format.</param>
    /// <returns>The OpenGL header values.</returns>
    /// <exception cref="NotSupportedException">The texel format has no KTX v1 representation.</exception>
    internal static KtxInternalFormat GetInternalFormat(TexelFormat texelFormat)
        => texelFormat switch
        {
            TexelFormat.R8G8B8A8_UNorm => new KtxInternalFormat(
                (uint)GlType.UnsignedByte, 1, (uint)GlFormat.Rgba, (uint)GlInternalFormat.Rgba8,
                (uint)GlBaseInternalFormat.Rgba),
            TexelFormat.Bc1_UNorm => Compressed(GlInternalFormat.CompressedRgbaS3tcDxt1Ext, GlBaseInternalFormat.Rgba),
            TexelFormat.Bc1_UnormSrgb => Compressed(GlInternalFormat.CompressedSrgbAlphaS3tcDxt1Ext,
                GlBaseInternalFormat.Rgba),
            TexelFormat.Bc4_UNorm => Compressed(GlInternalFormat.CompressedRedRgtc1, GlBaseInternalFormat.Red),
            TexelFormat.Bc5_UNorm => Compressed(GlInternalFormat.CompressedRgRgtc2, GlBaseInternalFormat.Rg),
            TexelFormat.Bc7_UNorm => Compressed(GlInternalFormat.CompressedRgbaBptcUnorm, GlBaseInternalFormat.Rgba),
            TexelFormat.Bc7_UnormSrgb => Compressed(GlInternalFormat.CompressedSrgbAlphaBptcUnorm,
                GlBaseInternalFormat.Rgba),
            _ => throw new NotSupportedException(
                $"The texel format '{texelFormat}' has no KTX v1 glInternalFormat mapping.")
        };

    /// <summary>
    ///     Describes the stored texel components of the given texel format.
    /// </summary>
    /// <param name="texelFormat">The texel format.</param>
    /// <returns>The texel type description.</returns>
    /// <exception cref="NotSupportedException">The texel format has no KTX v1 texel type description.</exception>
    internal static TexelTypeInfo GetTexelTypeInfo(TexelFormat texelFormat)
    {
        bool isSrgb = texelFormat is TexelFormat.Bc1_UnormSrgb or TexelFormat.Bc7_UnormSrgb;
        TexelComponentInterpretation interpretation = isSrgb
            ? TexelComponentInterpretation.SRgbUNorm
            : TexelComponentInterpretation.UNorm;

        return texelFormat switch
        {
            TexelFormat.R8G8B8A8_UNorm => new TexelTypeInfo
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
                AlphaRepresentation = TexelAlphaRepresentation.Unassociated
            },
            TexelFormat.Bc1_UNorm or TexelFormat.Bc1_UnormSrgb => new TexelTypeInfo
            {
                BitsPerTexel = 64,
                ColorType = TexelColorType.RGB,
                ComponentInfo = new TexelComponentInfo([64], [interpretation]),
                AlphaRepresentation = TexelAlphaRepresentation.Unassociated
            },
            TexelFormat.Bc4_UNorm => new TexelTypeInfo
            {
                BitsPerTexel = 64,
                ColorType = TexelColorType.Red,
                ComponentInfo = new TexelComponentInfo([64], [interpretation]),
                AlphaRepresentation = TexelAlphaRepresentation.None
            },
            TexelFormat.Bc5_UNorm => new TexelTypeInfo
            {
                BitsPerTexel = 128,
                ColorType = RedGreenChannels,
                ComponentInfo = new TexelComponentInfo([64, 64], [interpretation, interpretation]),
                AlphaRepresentation = TexelAlphaRepresentation.None
            },
            TexelFormat.Bc7_UNorm or TexelFormat.Bc7_UnormSrgb => new TexelTypeInfo
            {
                BitsPerTexel = 128,
                ColorType = TexelColorType.RGB | TexelColorType.Alpha,
                ComponentInfo = new TexelComponentInfo([128], [interpretation]),
                AlphaRepresentation = TexelAlphaRepresentation.Unassociated
            },
            _ => throw new NotSupportedException(
                $"The texel format '{texelFormat}' has no KTX v1 texel type description.")
        };
    }

    private static KtxInternalFormat Compressed(GlInternalFormat internalFormat,
        GlBaseInternalFormat baseInternalFormat)
        => new(0, 1, 0, (uint)internalFormat, (uint)baseInternalFormat);

    private static bool IsSrgb(TexelTypeInfo texelTypeInfo)
        => texelTypeInfo.ComponentInfo.ComponentCount > 0
           && texelTypeInfo.ComponentInfo.GetComponentInterpretation(0) == TexelComponentInterpretation.SRgbUNorm;
}
