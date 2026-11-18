namespace ImageSharp.Textures;

/// <summary>
///     Represents how component values are interpreted in a texel type.
/// </summary>
public enum TexelComponentInterpretation
{
    /// <summary>
    ///     Unsigned normalized integer.
    /// </summary>
    UNorm,

    /// <summary>
    ///     Signed normalized integer.
    /// </summary>
    SNorm,

    /// <summary>
    ///     Unsigned integer.
    /// </summary>
    UInt,

    /// <summary>
    ///     Signed integer.
    /// </summary>
    SInt,

    /// <summary>
    ///     Floating point.
    /// </summary>
    Float,

    /// <summary>
    ///     sRGB-encoded unsigned normalized integer.
    /// </summary>
    SRgbUNorm,

    /// <summary>
    ///     Shared exponent floating-point representation.
    /// </summary>
    SharedExponent,

    /// <summary>
    ///     Type is intentionally unspecified by the container format.
    /// </summary>
    Typeless
}
