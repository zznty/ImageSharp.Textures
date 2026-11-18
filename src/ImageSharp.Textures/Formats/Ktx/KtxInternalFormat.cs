namespace ImageSharp.Textures.Formats.Ktx;

/// <summary>
///     The OpenGL header values that describe a texel format in a KTX v1 file.
/// </summary>
/// <param name="GlType">The <c>glType</c> value; zero for compressed formats.</param>
/// <param name="GlTypeSize">The <c>glTypeSize</c> value; always one for the supported formats.</param>
/// <param name="GlFormat">The <c>glFormat</c> value; zero for compressed formats.</param>
/// <param name="GlInternalFormat">The <c>glInternalFormat</c> value.</param>
/// <param name="GlBaseInternalFormat">The <c>glBaseInternalFormat</c> value.</param>
internal readonly record struct KtxInternalFormat(
    uint GlType,
    uint GlTypeSize,
    uint GlFormat,
    uint GlInternalFormat,
    uint GlBaseInternalFormat);
