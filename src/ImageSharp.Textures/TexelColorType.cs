namespace ImageSharp.Textures;

/// <summary>
///     Represents the color type and component order of a texel type.
/// </summary>
[Flags]
public enum TexelColorType
{
    /// <summary>
    ///     No color type.
    /// </summary>
    None = 0,

    /// <summary>
    ///     Represents the red component.
    /// </summary>
    Red = 1 << 0,

    /// <summary>
    ///     Represents the green component.
    /// </summary>
    Green = 1 << 1,

    /// <summary>
    ///     Represents the blue component.
    /// </summary>
    Blue = 1 << 2,

    /// <summary>
    ///     Represents the alpha component.
    /// </summary>
    Alpha = 1 << 3,

    /// <summary>
    ///     Represents the exponent component used by shared-exponent formats.
    /// </summary>
    Exponent = 1 << 4,

    /// <summary>
    ///     Represents luminance data.
    /// </summary>
    Luminance = 1 << 5,

    /// <summary>
    ///     Represents the chrominance blue component.
    /// </summary>
    ChrominanceBlue = 1 << 6,

    /// <summary>
    ///     Represents the chrominance red component.
    /// </summary>
    ChrominanceRed = 1 << 7,

    /// <summary>
    ///     Represents depth data.
    /// </summary>
    Depth = 1 << 8,

    /// <summary>
    ///     Represents stencil data.
    /// </summary>
    Stencil = 1 << 9,

    /// <summary>
    ///     Indicates that the color type is RGB.
    /// </summary>
    RGB = Red | Green | Blue | (1 << 10),

    /// <summary>
    ///     Indicates that the color type is BGR.
    /// </summary>
    BGR = Blue | Green | Red | (1 << 11),

    /// <summary>
    ///     Indicates that the color type is YCbCr.
    /// </summary>
    YCbCr = Luminance | ChrominanceBlue | ChrominanceRed | (1 << 12),

    /// <summary>
    ///     Indicates that the color type is depth-stencil.
    /// </summary>
    DepthStencil = Depth | Stencil,

    /// <summary>
    ///     Indicates that the color type is not represented by another value.
    /// </summary>
    Other = 1 << 13
}
