namespace ImageSharp.Textures.Formats.Ktx.Enums;

/// <summary>
///     The OpenGL pixel formats stored by a KTX v1 header for uncompressed textures.
/// </summary>
internal enum GlFormat : uint
{
    /// <summary>
    ///     <c>GL_RGBA</c>: four component pixels. Compressed textures store zero.
    /// </summary>
    Rgba = 0x1908
}
