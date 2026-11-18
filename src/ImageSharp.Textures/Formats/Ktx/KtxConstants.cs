namespace ImageSharp.Textures.Formats.Ktx;

/// <summary>
///     Constants describing the Khronos Texture (KTX v1) container layout.
/// </summary>
internal static class KtxConstants
{
    /// <summary>
    ///     The length of the KTX file identifier, in bytes.
    /// </summary>
    internal const int IdentifierLength = 12;

    /// <summary>
    ///     The length of the KTX header fields that follow the endianness word, in bytes.
    /// </summary>
    internal const int HeaderBodyLength = 48;

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
    ///     Gets the KTX file identifier.
    /// </summary>
    internal static ReadOnlySpan<byte> Identifier =>
    [
        0xAB, 0x4B, 0x54, 0x58, 0x20, 0x31, 0x31, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A
    ];
}
