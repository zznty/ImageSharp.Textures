namespace ImageSharp.Textures.Formats.Dds.Enums;

/// <summary>
///     Flags indicating which members of <c>DDS_HEADER</c> contain valid data.
/// </summary>
[Flags]
internal enum DdsFlags : uint
{
    /// <summary>
    ///     No flags are set.
    /// </summary>
    Unknown = 0,

    /// <summary>
    ///     <c>dwCaps</c>, <c>dwHeight</c>, <c>dwWidth</c> and <c>ddspf</c> are valid. Required in every file.
    /// </summary>
    Caps = 0x1,

    /// <summary>
    ///     <c>dwHeight</c> is valid.
    /// </summary>
    Height = 0x2,

    /// <summary>
    ///     <c>dwWidth</c> is valid.
    /// </summary>
    Width = 0x4,

    /// <summary>
    ///     <c>dwPitchOrLinearSize</c> holds the pitch of an uncompressed texture.
    /// </summary>
    Pitch = 0x8,

    /// <summary>
    ///     <c>ddspf</c> is valid. Required in every file.
    /// </summary>
    PixelFormat = 0x1000,

    /// <summary>
    ///     <c>dwMipMapCount</c> is valid.
    /// </summary>
    MipMapCount = 0x20000,

    /// <summary>
    ///     <c>dwPitchOrLinearSize</c> holds the total size of the top level of a compressed texture.
    /// </summary>
    LinearSize = 0x80000,

    /// <summary>
    ///     <c>dwDepth</c> is valid.
    /// </summary>
    Depth = 0x800000
}
