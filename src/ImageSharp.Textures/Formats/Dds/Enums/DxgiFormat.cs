namespace ImageSharp.Textures.Formats.Dds.Enums;

/// <summary>
///     Identifies a DXGI surface format stored in a DDS DX10 header.
/// </summary>
/// <remarks>
///     Only the formats this library supports are declared. A DDS file declaring any other value is rejected
///     with a <see cref="System.NotSupportedException" /> naming the numeric value.
/// </remarks>
public enum DxgiFormat : uint
{
    /// <summary>
    ///     The format is unknown or the file used a legacy FourCC header instead.
    /// </summary>
    Unknown = 0,

    /// <summary>
    ///     Four component, 8 bits per component, unsigned normalized RGBA.
    /// </summary>
    R8G8B8A8_UNorm = 28,

    /// <summary>
    ///     BC1 block compression, unsigned normalized.
    /// </summary>
    BC1_UNorm = 71,

    /// <summary>
    ///     BC1 block compression, unsigned normalized, sRGB encoded.
    /// </summary>
    BC1_UNorm_SRGB = 72,

    /// <summary>
    ///     BC4 single channel block compression, unsigned normalized.
    /// </summary>
    BC4_UNorm = 80,

    /// <summary>
    ///     BC5 dual channel block compression, unsigned normalized.
    /// </summary>
    BC5_UNorm = 83,

    /// <summary>
    ///     BC7 block compression, unsigned normalized.
    /// </summary>
    BC7_UNorm = 98,

    /// <summary>
    ///     BC7 block compression, unsigned normalized, sRGB encoded.
    /// </summary>
    BC7_UNorm_SRGB = 99
}
