namespace ImageSharp.Textures.Formats.Ktx;

/// <summary>
///     Identifies the byte order of the header fields and texture data in a KTX v1 file.
/// </summary>
public enum KtxEndianness : uint
{
    /// <summary>
    ///     The file stores multi-byte values in little-endian order; the endianness word reads as
    ///     <c>0x04030201</c> on a little-endian host.
    /// </summary>
    LittleEndian = 0x04030201,

    /// <summary>
    ///     The file stores multi-byte values in big-endian order; the endianness word reads as
    ///     <c>0x01020304</c> on a little-endian host.
    /// </summary>
    BigEndian = 0x01020304
}
