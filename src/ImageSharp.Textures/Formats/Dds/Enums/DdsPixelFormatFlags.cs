namespace ImageSharp.Textures.Formats.Dds.Enums;

/// <summary>
///     Values which indicate what type of data is stored in a DDS surface.
/// </summary>
[Flags]
internal enum DdsPixelFormatFlags : uint
{
    /// <summary>
    ///     No flags are set.
    /// </summary>
    Unknown = 0,

    /// <summary>
    ///     The surface contains alpha data; <c>dwABitMask</c> is valid.
    /// </summary>
    AlphaPixels = 0x1,

    /// <summary>
    ///     <c>dwFourCC</c> is valid and holds a compressed or custom format code.
    /// </summary>
    FourCC = 0x4,

    /// <summary>
    ///     The surface contains uncompressed color data; the bit masks are valid.
    /// </summary>
    RGB = 0x40,

    /// <summary>
    ///     The surface contains uncompressed color and alpha data.
    /// </summary>
    RGBA = RGB | AlphaPixels,

    /// <summary>
    ///     The surface contains YUV data.
    /// </summary>
    YUV = 0x200,

    /// <summary>
    ///     The surface contains luminance data.
    /// </summary>
    Luminance = 0x20000
}
