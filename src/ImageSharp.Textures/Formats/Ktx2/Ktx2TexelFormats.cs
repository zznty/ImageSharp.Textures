using ImageSharp.Textures.Formats.Ktx2.Enums;

namespace ImageSharp.Textures.Formats.Ktx2;

/// <summary>
///     Maps Vulkan formats to <see cref="TexelFormat" /> values and to the data format descriptor blocks that
///     describe them, following the authoritative mapping table of the rewrite design.
/// </summary>
internal static class Ktx2TexelFormats
{
    private const int ModelRgbsda = 1;
    private const int ModelBc1a = 128;
    private const int ModelBc4 = 131;
    private const int ModelBc5 = 132;
    private const int ModelBc7 = 134;

    private const byte ChannelRed = 0;
    private const byte ChannelGreen = 1;
    private const byte ChannelBlue = 2;
    private const byte ChannelAlpha = 15;
    private const byte ChannelBc1aColor = 0;
    private const byte ChannelBc1aAlphaPresent = 1;
    private const byte ChannelBc4Data = 0;
    private const byte ChannelBc7Color = 0;

    private const uint UnormUpper = 0xFFFFFFFFU;
    private const uint UnormLower = 0;

    private const TexelColorType ColorChannels = TexelColorType.Red | TexelColorType.Green | TexelColorType.Blue;
    private const TexelColorType RedGreenChannels = TexelColorType.Red | TexelColorType.Green;

    private static readonly Ktx2FormatInfo FormatRgba8 = new(
        VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
        TexelFormat.R8G8B8A8_UNorm,
        1,
        1,
        1,
        4,
        ModelRgbsda,
        false,
        [
            new Ktx2SampleInfo(ChannelRed, 0, 8, UnormLower, 255),
            new Ktx2SampleInfo(ChannelGreen, 8, 8, UnormLower, 255),
            new Ktx2SampleInfo(ChannelBlue, 16, 8, UnormLower, 255),
            new Ktx2SampleInfo(ChannelAlpha, 24, 8, UnormLower, 255)
        ]);

    // The BC1 data format descriptor distinguishes the colour-only variant (vkFormat 131/132) from the
    // alpha-present variant (vkFormat 133/134) through the sample channel id: 0 for opaque BC1 and 1 for
    // BC1A with punch-through alpha. This is the mapping the Khronos reference implementation writes and
    // the one the Khronos Data Format Specification defines for KHR_DF_MODEL_BC1A.
    private static readonly Ktx2FormatInfo FormatBc1Rgb = new(
        VkFormat.VK_FORMAT_BC1_RGB_UNORM_BLOCK,
        TexelFormat.Bc1_UNorm,
        4,
        4,
        1,
        8,
        ModelBc1a,
        false,
        [new Ktx2SampleInfo(ChannelBc1aColor, 0, 64, UnormLower, UnormUpper)]);

    private static readonly Ktx2FormatInfo FormatBc1RgbSrgb = new(
        VkFormat.VK_FORMAT_BC1_RGB_SRGB_BLOCK,
        TexelFormat.Bc1_UnormSrgb,
        4,
        4,
        1,
        8,
        ModelBc1a,
        true,
        [new Ktx2SampleInfo(ChannelBc1aColor, 0, 64, UnormLower, UnormUpper)]);

    private static readonly Ktx2FormatInfo FormatBc1Rgba = new(
        VkFormat.VK_FORMAT_BC1_RGBA_UNORM_BLOCK,
        TexelFormat.Bc1_UNorm,
        4,
        4,
        1,
        8,
        ModelBc1a,
        false,
        [new Ktx2SampleInfo(ChannelBc1aAlphaPresent, 0, 64, UnormLower, UnormUpper)]);

    private static readonly Ktx2FormatInfo FormatBc1RgbaSrgb = new(
        VkFormat.VK_FORMAT_BC1_RGBA_SRGB_BLOCK,
        TexelFormat.Bc1_UnormSrgb,
        4,
        4,
        1,
        8,
        ModelBc1a,
        true,
        [new Ktx2SampleInfo(ChannelBc1aAlphaPresent, 0, 64, UnormLower, UnormUpper)]);

    private static readonly Ktx2FormatInfo FormatBc4 = new(
        VkFormat.VK_FORMAT_BC4_UNORM_BLOCK,
        TexelFormat.Bc4_UNorm,
        4,
        4,
        1,
        8,
        ModelBc4,
        false,
        [new Ktx2SampleInfo(ChannelBc4Data, 0, 64, UnormLower, UnormUpper)]);

    private static readonly Ktx2FormatInfo FormatBc5 = new(
        VkFormat.VK_FORMAT_BC5_UNORM_BLOCK,
        TexelFormat.Bc5_UNorm,
        4,
        4,
        1,
        16,
        ModelBc5,
        false,
        [
            new Ktx2SampleInfo(ChannelRed, 0, 64, UnormLower, UnormUpper),
            new Ktx2SampleInfo(ChannelGreen, 64, 64, UnormLower, UnormUpper)
        ]);

    private static readonly Ktx2FormatInfo FormatBc7 = new(
        VkFormat.VK_FORMAT_BC7_UNORM_BLOCK,
        TexelFormat.Bc7_UNorm,
        4,
        4,
        1,
        16,
        ModelBc7,
        false,
        [new Ktx2SampleInfo(ChannelBc7Color, 0, 128, UnormLower, UnormUpper)]);

    private static readonly Ktx2FormatInfo FormatBc7Srgb = new(
        VkFormat.VK_FORMAT_BC7_SRGB_BLOCK,
        TexelFormat.Bc7_UnormSrgb,
        4,
        4,
        1,
        16,
        ModelBc7,
        true,
        [new Ktx2SampleInfo(ChannelBc7Color, 0, 128, UnormLower, UnormUpper)]);

    /// <summary>
    ///     Tries to get the descriptor information of a Vulkan format.
    /// </summary>
    /// <param name="vkFormat">The Vulkan format value.</param>
    /// <param name="formatInfo">The format information.</param>
    /// <returns><see langword="true" /> when the format is supported; otherwise, <see langword="false" />.</returns>
    internal static bool TryGetFormatInfo(VkFormat vkFormat, out Ktx2FormatInfo formatInfo)
    {
        switch (vkFormat)
        {
            case VkFormat.VK_FORMAT_R8G8B8A8_UNORM:
                formatInfo = FormatRgba8;
                return true;
            case VkFormat.VK_FORMAT_BC1_RGB_UNORM_BLOCK:
                formatInfo = FormatBc1Rgb;
                return true;
            case VkFormat.VK_FORMAT_BC1_RGB_SRGB_BLOCK:
                formatInfo = FormatBc1RgbSrgb;
                return true;
            case VkFormat.VK_FORMAT_BC1_RGBA_UNORM_BLOCK:
                formatInfo = FormatBc1Rgba;
                return true;
            case VkFormat.VK_FORMAT_BC1_RGBA_SRGB_BLOCK:
                formatInfo = FormatBc1RgbaSrgb;
                return true;
            case VkFormat.VK_FORMAT_BC4_UNORM_BLOCK:
                formatInfo = FormatBc4;
                return true;
            case VkFormat.VK_FORMAT_BC5_UNORM_BLOCK:
                formatInfo = FormatBc5;
                return true;
            case VkFormat.VK_FORMAT_BC7_UNORM_BLOCK:
                formatInfo = FormatBc7;
                return true;
            case VkFormat.VK_FORMAT_BC7_SRGB_BLOCK:
                formatInfo = FormatBc7Srgb;
                return true;
            default:
                formatInfo = null!;
                return false;
        }
    }

    /// <summary>
    ///     Gets the canonical descriptor information of a texel format.
    /// </summary>
    /// <param name="texelFormat">The texel format.</param>
    /// <returns>The format information.</returns>
    /// <exception cref="NotSupportedException">The texel format has no KTX2 representation.</exception>
    internal static Ktx2FormatInfo GetFormatInfo(TexelFormat texelFormat)
        => texelFormat switch
        {
            TexelFormat.R8G8B8A8_UNorm => FormatRgba8,
            TexelFormat.Bc1_UNorm => FormatBc1Rgba,
            TexelFormat.Bc1_UnormSrgb => FormatBc1RgbaSrgb,
            TexelFormat.Bc4_UNorm => FormatBc4,
            TexelFormat.Bc5_UNorm => FormatBc5,
            TexelFormat.Bc7_UNorm => FormatBc7,
            TexelFormat.Bc7_UnormSrgb => FormatBc7Srgb,
            _ => throw new NotSupportedException(
                $"The texel format '{texelFormat}' has no KTX2 vkFormat mapping.")
        };

    /// <summary>
    ///     Gets the canonical Vulkan format of a texel format.
    /// </summary>
    /// <param name="texelFormat">The texel format.</param>
    /// <returns>The Vulkan format.</returns>
    /// <exception cref="NotSupportedException">The texel format has no KTX2 representation.</exception>
    internal static VkFormat GetVkFormat(TexelFormat texelFormat) => GetFormatInfo(texelFormat).VkFormat;

    /// <summary>
    ///     Tries to map a Vulkan format to a texel format.
    /// </summary>
    /// <param name="vkFormat">The Vulkan format.</param>
    /// <param name="texelFormat">The mapped texel format.</param>
    /// <returns><see langword="true" /> when the format is supported; otherwise, <see langword="false" />.</returns>
    internal static bool TryGetTexelFormat(VkFormat vkFormat, out TexelFormat texelFormat)
    {
        if (TryGetFormatInfo(vkFormat, out Ktx2FormatInfo formatInfo))
        {
            texelFormat = formatInfo.TexelFormat;
            return true;
        }

        texelFormat = TexelFormat.Unknown;
        return false;
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
    ///     Describes the stored texel components of the given texel format.
    /// </summary>
    /// <param name="texelFormat">The texel format.</param>
    /// <returns>The texel type description.</returns>
    /// <exception cref="NotSupportedException">The texel format has no KTX2 texel type description.</exception>
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
                $"The texel format '{texelFormat}' has no KTX2 texel type description.")
        };
    }

    private static bool IsSrgb(TexelTypeInfo texelTypeInfo)
        => texelTypeInfo.ComponentInfo.ComponentCount > 0
           && texelTypeInfo.ComponentInfo.GetComponentInterpretation(0) == TexelComponentInterpretation.SRgbUNorm;
}
