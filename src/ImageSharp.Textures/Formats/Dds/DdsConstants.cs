namespace ImageSharp.Textures.Formats.Dds;

/// <summary>
///     Constants describing the DirectDraw Surface container layout.
/// </summary>
internal static class DdsConstants
{
    /// <summary>
    ///     The length of the DDS magic word, in bytes.
    /// </summary>
    internal const int MagicLength = 4;

    /// <summary>
    ///     The DDS magic word: 'D', 'D', 'S', ' ' interpreted as a little endian double word.
    /// </summary>
    internal const uint MagicWord = 'D' | ('D' << 8) | ('S' << 16) | (' ' << 24);

    /// <summary>
    ///     The size of <c>DDS_HEADER</c>, in bytes.
    /// </summary>
    internal const int HeaderSize = 124;

    /// <summary>
    ///     The size of <c>DDS_PIXELFORMAT</c>, in bytes.
    /// </summary>
    internal const int PixelFormatSize = 32;

    /// <summary>
    ///     The size of <c>DDS_HEADER_DXT10</c>, in bytes.
    /// </summary>
    internal const int Dx10HeaderSize = 20;

    /// <summary>
    ///     The size of the magic word plus <c>DDS_HEADER</c>, in bytes.
    /// </summary>
    internal const int HeaderIncludingMagicSize = MagicLength + HeaderSize;

    /// <summary>
    ///     The largest surface dimension accepted from a header. Direct3D 11 caps texture dimensions at 16384.
    /// </summary>
    internal const int MaxDimension = 16384;

    /// <summary>
    ///     The largest mip level count accepted from a header.
    /// </summary>
    internal const int MaxMipLevelCount = 32;

    /// <summary>
    ///     The largest total surface count accepted from a header. Guards against absurd array, face and depth
    ///     declarations that would otherwise turn a tiny file into a huge allocation loop.
    /// </summary>
    internal const int MaxSurfaceCount = 1 << 20;
}
