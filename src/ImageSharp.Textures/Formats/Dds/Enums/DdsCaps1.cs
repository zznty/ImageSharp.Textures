namespace ImageSharp.Textures.Formats.Dds.Enums;

/// <summary>
///     Specifies the complexity of the surfaces stored in a DDS file.
/// </summary>
[Flags]
internal enum DdsCaps1 : uint
{
    /// <summary>
    ///     No capabilities are declared.
    /// </summary>
    Unknown = 0,

    /// <summary>
    ///     The file contains more than one surface: a mip chain, a cube map or a volume texture.
    /// </summary>
    Complex = 0x8,

    /// <summary>
    ///     The file contains at least one texture surface. Required for textures.
    /// </summary>
    Texture = 0x1000,

    /// <summary>
    ///     The surface has an attached mip chain.
    /// </summary>
    MipMap = 0x400000
}
