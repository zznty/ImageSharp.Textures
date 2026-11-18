namespace ImageSharp.Textures.Formats.Ktx.Enums;

/// <summary>
///     The OpenGL base internal formats stored by a KTX v1 header.
/// </summary>
internal enum GlBaseInternalFormat : uint
{
    /// <summary>
    ///     <c>GL_RED</c>: a single red channel, used by BC4.
    /// </summary>
    Red = 0x1903,

    /// <summary>
    ///     <c>GL_RG</c>: red and green channels, used by BC5.
    /// </summary>
    Rg = 0x8227,

    /// <summary>
    ///     <c>GL_RGBA</c>: four component color, used by RGBA8, BC1 and BC7.
    /// </summary>
    Rgba = 0x1908
}
