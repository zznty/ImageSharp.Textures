namespace ImageSharp.Textures.Formats.Ktx.Enums;

/// <summary>
///     The OpenGL component types stored by a KTX v1 header for uncompressed textures.
/// </summary>
internal enum GlType : uint
{
    /// <summary>
    ///     <c>GL_UNSIGNED_BYTE</c>: one byte per component. Compressed textures store zero.
    /// </summary>
    UnsignedByte = 0x1401
}
