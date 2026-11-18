using System.Diagnostics.CodeAnalysis;

namespace ImageSharp.Textures.Formats.Ktx2.Enums;

/// <summary>
///     The Vulkan image formats stored by a KTX2 header for the texel formats supported by this codec.
/// </summary>
/// <remarks>
///     The member names are the <c>VkFormat</c> enumerators from the Vulkan specification, kept verbatim so that
///     the container mapping reads like the specification.
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1707:Identifiers should not contain underscores",
    Justification = "Vulkan format names are the VK_FORMAT identifiers.",
    Scope = "type",
    Target = "~T:ImageSharp.Textures.Formats.Ktx2.Enums.VkFormat")]
public enum VkFormat : uint
{
    /// <summary>
    ///     <c>VK_FORMAT_UNDEFINED</c>: the format must be described by the data format descriptor.
    /// </summary>
    VK_FORMAT_UNDEFINED = 0,

    /// <summary>
    ///     <c>VK_FORMAT_R8G8B8A8_UNORM</c>: four component 8 bit unsigned normalized RGBA.
    /// </summary>
    VK_FORMAT_R8G8B8A8_UNORM = 37,

    /// <summary>
    ///     <c>VK_FORMAT_BC1_RGB_UNORM_BLOCK</c>: BC1 without alpha.
    /// </summary>
    VK_FORMAT_BC1_RGB_UNORM_BLOCK = 131,

    /// <summary>
    ///     <c>VK_FORMAT_BC1_RGB_SRGB_BLOCK</c>: sRGB BC1 without alpha.
    /// </summary>
    VK_FORMAT_BC1_RGB_SRGB_BLOCK = 132,

    /// <summary>
    ///     <c>VK_FORMAT_BC1_RGBA_UNORM_BLOCK</c>: BC1 with optional punch-through alpha.
    /// </summary>
    VK_FORMAT_BC1_RGBA_UNORM_BLOCK = 133,

    /// <summary>
    ///     <c>VK_FORMAT_BC1_RGBA_SRGB_BLOCK</c>: sRGB BC1 with optional punch-through alpha.
    /// </summary>
    VK_FORMAT_BC1_RGBA_SRGB_BLOCK = 134,

    /// <summary>
    ///     <c>VK_FORMAT_BC4_UNORM_BLOCK</c>: BC4.
    /// </summary>
    VK_FORMAT_BC4_UNORM_BLOCK = 139,

    /// <summary>
    ///     <c>VK_FORMAT_BC5_UNORM_BLOCK</c>: BC5.
    /// </summary>
    VK_FORMAT_BC5_UNORM_BLOCK = 141,

    /// <summary>
    ///     <c>VK_FORMAT_BC7_UNORM_BLOCK</c>: BC7.
    /// </summary>
    VK_FORMAT_BC7_UNORM_BLOCK = 145,

    /// <summary>
    ///     <c>VK_FORMAT_BC7_SRGB_BLOCK</c>: sRGB BC7.
    /// </summary>
    VK_FORMAT_BC7_SRGB_BLOCK = 146
}
