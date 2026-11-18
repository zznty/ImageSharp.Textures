namespace ImageSharp.Textures.Formats.Dds.Enums;

/// <summary>
///     Additional detail about the surfaces stored in a DDS file.
/// </summary>
[Flags]
internal enum DdsCaps2 : uint
{
    /// <summary>
    ///     No additional capabilities are declared.
    /// </summary>
    Unknown = 0,

    /// <summary>
    ///     The texture is a cube map; the six face flags must also be set.
    /// </summary>
    Cubemap = 0x200,

    /// <summary>
    ///     The positive X face of the cube map is present.
    /// </summary>
    CubemapPositiveX = 0x400,

    /// <summary>
    ///     The negative X face of the cube map is present.
    /// </summary>
    CubemapNegativeX = 0x800,

    /// <summary>
    ///     The positive Y face of the cube map is present.
    /// </summary>
    CubemapPositiveY = 0x1000,

    /// <summary>
    ///     The negative Y face of the cube map is present.
    /// </summary>
    CubemapNegativeY = 0x2000,

    /// <summary>
    ///     The positive Z face of the cube map is present.
    /// </summary>
    CubemapPositiveZ = 0x4000,

    /// <summary>
    ///     The negative Z face of the cube map is present.
    /// </summary>
    CubemapNegativeZ = 0x8000,

    /// <summary>
    ///     The all-faces mask of a complete cube map.
    /// </summary>
    CubemapAllFaces = CubemapPositiveX | CubemapNegativeX | CubemapPositiveY | CubemapNegativeY | CubemapPositiveZ |
                      CubemapNegativeZ,

    /// <summary>
    ///     The texture is a volume (three-dimensional) texture.
    /// </summary>
    Volume = 0x200000
}
