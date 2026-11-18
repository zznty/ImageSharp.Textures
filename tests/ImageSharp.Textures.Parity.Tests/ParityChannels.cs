namespace ImageSharp.Textures.Parity.Tests;

/// <summary>
///     The channels of a decoded surface that may be compared against a texconv RGBA dump.
/// </summary>
[Flags]
internal enum ParityChannels
{
    /// <summary>
    ///     No channel.
    /// </summary>
    None = 0,

    /// <summary>
    ///     The red channel.
    /// </summary>
    Red = 1,

    /// <summary>
    ///     The green channel.
    /// </summary>
    Green = 2,

    /// <summary>
    ///     The blue channel.
    /// </summary>
    Blue = 4,

    /// <summary>
    ///     The alpha channel.
    /// </summary>
    Alpha = 8,

    /// <summary>
    ///     BC1 declares RGB with punch-through alpha; every channel is compared.
    /// </summary>
    Bc1 = Red | Green | Blue | Alpha,

    /// <summary>
    ///     BC4 declares red only; green and blue are asserted separately.
    /// </summary>
    Bc4 = Red,

    /// <summary>
    ///     BC5 declares red and green only; blue is asserted separately.
    /// </summary>
    Bc5 = Red | Green,

    /// <summary>
    ///     BC7 declares RGBA.
    /// </summary>
    Bc7 = Red | Green | Blue | Alpha
}
