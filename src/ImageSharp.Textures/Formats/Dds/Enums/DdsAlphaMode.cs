namespace ImageSharp.Textures.Formats.Dds.Enums;

/// <summary>
///     Describes how alpha is stored, as reported by <c>DDS_HEADER_DXT10.miscFlags2</c>.
/// </summary>
public enum DdsAlphaMode : uint
{
    /// <summary>
    ///     The alpha mode is not specified by the file.
    /// </summary>
    Unknown = 0,

    /// <summary>
    ///     Alpha is stored independently of the color components.
    /// </summary>
    Straight = 1,

    /// <summary>
    ///     Color components are already multiplied by alpha.
    /// </summary>
    Premultiplied = 2,

    /// <summary>
    ///     The texture is opaque; alpha is not used.
    /// </summary>
    Opaque = 3,

    /// <summary>
    ///     The alpha mode is defined by the payload and not by the header.
    /// </summary>
    Custom = 4
}
