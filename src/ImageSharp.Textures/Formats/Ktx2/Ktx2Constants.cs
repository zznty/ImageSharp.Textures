namespace ImageSharp.Textures.Formats.Ktx2;

/// <summary>
///     Constants describing the Khronos Texture 2 (KTX2) container layout.
/// </summary>
internal static class Ktx2Constants
{
    /// <summary>
    ///     The length of the KTX2 file identifier, in bytes.
    /// </summary>
    internal const int IdentifierLength = 12;

    /// <summary>
    ///     The length of the KTX2 header that follows the identifier, in bytes.
    /// </summary>
    internal const int HeaderBodyLength = 68;

    /// <summary>
    ///     The length of the KTX2 identifier plus header, in bytes.
    /// </summary>
    internal const int HeaderLength = IdentifierLength + HeaderBodyLength;

    /// <summary>
    ///     The length of one level index entry, in bytes.
    /// </summary>
    internal const int LevelIndexEntryLength = 24;

    /// <summary>
    ///     The largest surface dimension accepted from a header. Direct3D 11 caps texture dimensions at 16384.
    /// </summary>
    internal const int MaxDimension = 16384;

    /// <summary>
    ///     The largest mip level count accepted from a header.
    /// </summary>
    internal const int MaxMipLevelCount = 32;

    /// <summary>
    ///     The largest total surface count accepted from a header.
    /// </summary>
    internal const int MaxSurfaceCount = 1 << 20;

    /// <summary>
    ///     The largest total payload size accepted from a header.
    /// </summary>
    internal const long MaxTotalPayloadBytes = 4L << 30;

    /// <summary>
    ///     The largest key/value data block accepted from a header, in bytes.
    /// </summary>
    internal const int MaxKeyValueDataLength = 64 << 20;

    /// <summary>
    ///     The largest data format descriptor accepted from a header, in bytes.
    /// </summary>
    internal const int MaxDataFormatDescriptorLength = 1 << 16;

    /// <summary>
    ///     Gets the KTX2 file identifier.
    /// </summary>
    internal static ReadOnlySpan<byte> Identifier =>
    [
        0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A
    ];
}
