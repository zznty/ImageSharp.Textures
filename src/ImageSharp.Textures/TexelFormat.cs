namespace ImageSharp.Textures;

/// <summary>
///     Identifies the texel format used by a texture surface.
/// </summary>
public enum TexelFormat
{
    /// <summary>
    ///     The texel format is unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    ///     Four component, 8 bits per component, unsigned normalized RGBA.
    /// </summary>
    R8G8B8A8_UNorm,

    /// <summary>
    ///     BC1 block compression, unsigned normalized.
    /// </summary>
    Bc1_UNorm,

    /// <summary>
    ///     BC1 block compression, unsigned normalized, sRGB encoded.
    /// </summary>
    Bc1_UnormSrgb,

    /// <summary>
    ///     BC4 single channel block compression, unsigned normalized.
    /// </summary>
    Bc4_UNorm,

    /// <summary>
    ///     BC5 dual channel block compression, unsigned normalized.
    /// </summary>
    Bc5_UNorm,

    /// <summary>
    ///     BC7 block compression, unsigned normalized.
    /// </summary>
    Bc7_UNorm,

    /// <summary>
    ///     BC7 block compression, unsigned normalized, sRGB encoded.
    /// </summary>
    Bc7_UnormSrgb
}
