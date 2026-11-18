using ImageSharp.Textures.Formats.Dds.Enums;

namespace ImageSharp.Textures.Formats.Dds;

/// <summary>
///     Maps DDS container formats (DXGI values and legacy four character codes) to <see cref="TexelFormat" />
///     values and back, following the authoritative mapping table of the rewrite design.
/// </summary>
internal static class DdsTexelFormats
{
    private const TexelColorType ColorChannels = TexelColorType.Red | TexelColorType.Green | TexelColorType.Blue;
    private const TexelColorType RedGreenChannels = TexelColorType.Red | TexelColorType.Green;

    /// <summary>
    ///     Tries to map a DXGI format to a texel format.
    /// </summary>
    /// <param name="format">The DXGI format.</param>
    /// <param name="texelFormat">The mapped texel format.</param>
    /// <returns><see langword="true" /> when the format is supported; otherwise, <see langword="false" />.</returns>
    internal static bool TryGetTexelFormat(DxgiFormat format, out TexelFormat texelFormat)
    {
        switch (format)
        {
            case DxgiFormat.R8G8B8A8_UNorm:
                texelFormat = TexelFormat.R8G8B8A8_UNorm;
                return true;
            case DxgiFormat.BC1_UNorm:
                texelFormat = TexelFormat.Bc1_UNorm;
                return true;
            case DxgiFormat.BC1_UNorm_SRGB:
                texelFormat = TexelFormat.Bc1_UnormSrgb;
                return true;
            case DxgiFormat.BC4_UNorm:
                texelFormat = TexelFormat.Bc4_UNorm;
                return true;
            case DxgiFormat.BC5_UNorm:
                texelFormat = TexelFormat.Bc5_UNorm;
                return true;
            case DxgiFormat.BC7_UNorm:
                texelFormat = TexelFormat.Bc7_UNorm;
                return true;
            case DxgiFormat.BC7_UNorm_SRGB:
                texelFormat = TexelFormat.Bc7_UnormSrgb;
                return true;
            default:
                texelFormat = TexelFormat.Unknown;
                return false;
        }
    }

    /// <summary>
    ///     Tries to map a legacy four character code to a texel format.
    /// </summary>
    /// <param name="fourCc">The four character code.</param>
    /// <param name="texelFormat">The mapped texel format.</param>
    /// <returns><see langword="true" /> when the code is supported; otherwise, <see langword="false" />.</returns>
    internal static bool TryGetTexelFormat(DdsFourCc fourCc, out TexelFormat texelFormat)
    {
        switch (fourCc)
        {
            case DdsFourCc.DXT1:
                texelFormat = TexelFormat.Bc1_UNorm;
                return true;
            case DdsFourCc.BC4U:
                texelFormat = TexelFormat.Bc4_UNorm;
                return true;
            case DdsFourCc.BC5U:
                texelFormat = TexelFormat.Bc5_UNorm;
                return true;
            default:
                texelFormat = TexelFormat.Unknown;
                return false;
        }
    }

    /// <summary>
    ///     Tries to map a texel type description back to a texel format by matching the stored color channels,
    ///     the precision and the sRGB interpretation.
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
    ///     Gets the DXGI format that stores the given texel format.
    /// </summary>
    /// <param name="texelFormat">The texel format.</param>
    /// <returns>The DXGI format, or <see cref="DxgiFormat.Unknown" /> when the texel format has no DXGI mapping.</returns>
    internal static DxgiFormat GetDxgiFormat(TexelFormat texelFormat)
        => texelFormat switch
        {
            TexelFormat.R8G8B8A8_UNorm => DxgiFormat.R8G8B8A8_UNorm,
            TexelFormat.Bc1_UNorm => DxgiFormat.BC1_UNorm,
            TexelFormat.Bc1_UnormSrgb => DxgiFormat.BC1_UNorm_SRGB,
            TexelFormat.Bc4_UNorm => DxgiFormat.BC4_UNorm,
            TexelFormat.Bc5_UNorm => DxgiFormat.BC5_UNorm,
            TexelFormat.Bc7_UNorm => DxgiFormat.BC7_UNorm,
            TexelFormat.Bc7_UnormSrgb => DxgiFormat.BC7_UNorm_SRGB,
            _ => DxgiFormat.Unknown
        };

    /// <summary>
    ///     Tries to get the legacy four character code that stores the given texel format.
    /// </summary>
    /// <param name="texelFormat">The texel format.</param>
    /// <param name="fourCc">The four character code.</param>
    /// <returns>
    ///     <see langword="true" /> when a legacy code exists for the format; otherwise, <see langword="false" />
    ///     because the format requires a DX10 header.
    /// </returns>
    internal static bool TryGetFourCc(TexelFormat texelFormat, out DdsFourCc fourCc)
    {
        switch (texelFormat)
        {
            case TexelFormat.Bc1_UNorm:
                fourCc = DdsFourCc.DXT1;
                return true;
            case TexelFormat.Bc4_UNorm:
                fourCc = DdsFourCc.BC4U;
                return true;
            case TexelFormat.Bc5_UNorm:
                fourCc = DdsFourCc.BC5U;
                return true;
            default:
                fourCc = DdsFourCc.None;
                return false;
        }
    }

    /// <summary>
    ///     Describes the stored texel components of the given texel format.
    /// </summary>
    /// <param name="texelFormat">The texel format.</param>
    /// <param name="alphaMode">The alpha mode declared by the container.</param>
    /// <returns>The texel type description.</returns>
    internal static TexelTypeInfo GetTexelTypeInfo(TexelFormat texelFormat, DdsAlphaMode alphaMode)
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
                AlphaRepresentation = GetAlphaRepresentation(alphaMode, true)
            },
            TexelFormat.Bc1_UNorm or TexelFormat.Bc1_UnormSrgb => new TexelTypeInfo
            {
                BitsPerTexel = 64,
                ColorType = TexelColorType.RGB,
                ComponentInfo = new TexelComponentInfo([64], [interpretation]),
                AlphaRepresentation = GetAlphaRepresentation(alphaMode, true)
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
                AlphaRepresentation = GetAlphaRepresentation(alphaMode, true)
            },
            _ => throw new NotSupportedException($"The texel format '{texelFormat}' has no DDS texel type description.")
        };
    }

    /// <summary>
    ///     Determines whether the texel format stores 4x4 blocks rather than texels.
    /// </summary>
    /// <param name="texelFormat">The texel format.</param>
    /// <returns><see langword="true" /> when the format is block compressed; otherwise, <see langword="false" />.</returns>
    internal static bool IsBlockCompressed(TexelFormat texelFormat)
        => texelFormat is TexelFormat.Bc1_UNorm
            or TexelFormat.Bc1_UnormSrgb
            or TexelFormat.Bc4_UNorm
            or TexelFormat.Bc5_UNorm
            or TexelFormat.Bc7_UNorm
            or TexelFormat.Bc7_UnormSrgb;

    /// <summary>
    ///     Gets the encoded payload size of one surface in the given texel format.
    /// </summary>
    /// <param name="texelFormat">The texel format.</param>
    /// <param name="width">The surface width, in texels.</param>
    /// <param name="height">The surface height, in texels.</param>
    /// <returns>The encoded payload size, in bytes.</returns>
    internal static int GetEncodedByteCount(TexelFormat texelFormat, int width, int height)
    {
        int blockBytes = texelFormat switch
        {
            TexelFormat.Bc1_UNorm or TexelFormat.Bc1_UnormSrgb => 8,
            TexelFormat.Bc4_UNorm => 8,
            TexelFormat.Bc5_UNorm => 16,
            TexelFormat.Bc7_UNorm or TexelFormat.Bc7_UnormSrgb => 16,
            TexelFormat.R8G8B8A8_UNorm => 0,
            _ => throw new NotSupportedException($"The texel format '{texelFormat}' has no DDS payload size.")
        };

        if (blockBytes == 0)
        {
            return checked(width * height * 4);
        }

        int blocksX = checked((width + 3) / 4);
        int blocksY = checked((height + 3) / 4);
        return checked(blocksX * blocksY * blockBytes);
    }

    private static bool IsSrgb(TexelTypeInfo texelTypeInfo)
        => texelTypeInfo.ComponentInfo.ComponentCount > 0
           && texelTypeInfo.ComponentInfo.GetComponentInterpretation(0) == TexelComponentInterpretation.SRgbUNorm;

    private static TexelAlphaRepresentation GetAlphaRepresentation(DdsAlphaMode alphaMode, bool hasAlpha)
    {
        if (!hasAlpha)
        {
            return TexelAlphaRepresentation.None;
        }

        return alphaMode switch
        {
            DdsAlphaMode.Opaque => TexelAlphaRepresentation.None,
            DdsAlphaMode.Premultiplied => TexelAlphaRepresentation.Associated,
            DdsAlphaMode.Straight or DdsAlphaMode.Custom => TexelAlphaRepresentation.Unassociated,

            // Legacy headers (and DX10 headers that declare no alpha mode) cannot promise the texture is
            // opaque: BC1 punch-through and BC7 index alpha are only visible in the payload.
            _ => TexelAlphaRepresentation.Unassociated
        };
    }
}
