namespace ImageSharp.Textures.Formats.Ktx.Enums;

/// <summary>
///     The OpenGL internal formats stored by a KTX v1 header for the texel formats supported by this codec.
/// </summary>
internal enum GlInternalFormat : uint
{
    /// <summary>
    ///     <c>GL_RGBA8</c>: four component 8 bit unsigned normalized RGBA.
    /// </summary>
    Rgba8 = 0x8058,

    /// <summary>
    ///     <c>GL_COMPRESSED_RGB_S3TC_DXT1_EXT</c>: BC1 without alpha. Accepted as an alias for
    ///     <see cref="CompressedRgbaS3tcDxt1Ext" />; KTX v1 carries no separate alpha flag.
    /// </summary>
    CompressedRgbS3tcDxt1Ext = 0x83F0,

    /// <summary>
    ///     <c>GL_COMPRESSED_RGBA_S3TC_DXT1_EXT</c>: BC1 with optional punch-through alpha.
    /// </summary>
    CompressedRgbaS3tcDxt1Ext = 0x83F1,

    /// <summary>
    ///     <c>GL_COMPRESSED_SRGB_S3TC_DXT1_EXT</c>: sRGB BC1 without alpha. Accepted as an alias for
    ///     <see cref="CompressedSrgbAlphaS3tcDxt1Ext" />.
    /// </summary>
    CompressedSrgbS3tcDxt1Ext = 0x8C4C,

    /// <summary>
    ///     <c>GL_COMPRESSED_SRGB_ALPHA_S3TC_DXT1_EXT</c>: sRGB BC1 with optional punch-through alpha.
    /// </summary>
    CompressedSrgbAlphaS3tcDxt1Ext = 0x8C4D,

    /// <summary>
    ///     <c>GL_COMPRESSED_RED_RGTC1</c>: BC4.
    /// </summary>
    CompressedRedRgtc1 = 0x8DBB,

    /// <summary>
    ///     <c>GL_COMPRESSED_RG_RGTC2</c>: BC5.
    /// </summary>
    CompressedRgRgtc2 = 0x8DBD,

    /// <summary>
    ///     <c>GL_COMPRESSED_RGBA_BPTC_UNORM</c>: BC7.
    /// </summary>
    CompressedRgbaBptcUnorm = 0x8E8C,

    /// <summary>
    ///     <c>GL_COMPRESSED_SRGB_ALPHA_BPTC_UNORM</c>: sRGB BC7.
    /// </summary>
    CompressedSrgbAlphaBptcUnorm = 0x8E8D
}
