using System.Buffers.Binary;

namespace ImageSharp.Textures.Formats.Ktx;

/// <summary>
///     Describes the fields of a KTX v1 header.
/// </summary>
internal readonly struct KtxHeader
{
    private KtxHeader(
        KtxEndianness endianness,
        uint glType,
        uint glTypeSize,
        uint glFormat,
        uint glInternalFormat,
        uint glBaseInternalFormat,
        uint width,
        uint height,
        uint depth,
        uint arrayElementCount,
        uint faceCount,
        uint mipLevelCount,
        uint bytesOfKeyValueData)
    {
        this.Endianness = endianness;
        this.GlType = glType;
        this.GlTypeSize = glTypeSize;
        this.GlFormat = glFormat;
        this.GlInternalFormat = glInternalFormat;
        this.GlBaseInternalFormat = glBaseInternalFormat;
        this.Width = width;
        this.Height = height;
        this.Depth = depth;
        this.ArrayElementCount = arrayElementCount;
        this.FaceCount = faceCount;
        this.MipLevelCount = mipLevelCount;
        this.BytesOfKeyValueData = bytesOfKeyValueData;
    }

    /// <summary>
    ///     Gets the byte order declared by the file.
    /// </summary>
    internal KtxEndianness Endianness { get; }

    /// <summary>
    ///     Gets the <c>glType</c> header field.
    /// </summary>
    internal uint GlType { get; }

    /// <summary>
    ///     Gets the <c>glTypeSize</c> header field.
    /// </summary>
    internal uint GlTypeSize { get; }

    /// <summary>
    ///     Gets the <c>glFormat</c> header field.
    /// </summary>
    internal uint GlFormat { get; }

    /// <summary>
    ///     Gets the <c>glInternalFormat</c> header field.
    /// </summary>
    internal uint GlInternalFormat { get; }

    /// <summary>
    ///     Gets the <c>glBaseInternalFormat</c> header field.
    /// </summary>
    internal uint GlBaseInternalFormat { get; }

    /// <summary>
    ///     Gets the base mip level width.
    /// </summary>
    internal uint Width { get; }

    /// <summary>
    ///     Gets the base mip level height; zero for 1D textures.
    /// </summary>
    internal uint Height { get; }

    /// <summary>
    ///     Gets the base mip level depth; zero for 2D, array and cubemap textures.
    /// </summary>
    internal uint Depth { get; }

    /// <summary>
    ///     Gets the declared <c>numberOfArrayElements</c> field.
    /// </summary>
    internal uint ArrayElementCount { get; }

    /// <summary>
    ///     Gets the declared <c>numberOfFaces</c> field.
    /// </summary>
    internal uint FaceCount { get; }

    /// <summary>
    ///     Gets the declared <c>numberOfMipmapLevels</c> field.
    /// </summary>
    internal uint MipLevelCount { get; }

    /// <summary>
    ///     Gets the declared <c>bytesOfKeyValueData</c> field.
    /// </summary>
    internal uint BytesOfKeyValueData { get; }

    /// <summary>
    ///     Gets a value indicating whether the header fields are stored big-endian.
    /// </summary>
    internal bool IsBigEndian => this.Endianness == KtxEndianness.BigEndian;

    /// <summary>
    ///     Parses the 52 header bytes that follow the KTX identifier.
    /// </summary>
    /// <param name="body">The header body.</param>
    /// <param name="endianness">The byte order declared by the file.</param>
    /// <returns>The parsed header.</returns>
    internal static KtxHeader Parse(ReadOnlySpan<byte> body, KtxEndianness endianness)
    {
        bool bigEndian = endianness == KtxEndianness.BigEndian;

        return new KtxHeader(
            endianness,
            ReadUInt32(body, 0, bigEndian),
            ReadUInt32(body, 4, bigEndian),
            ReadUInt32(body, 8, bigEndian),
            ReadUInt32(body, 12, bigEndian),
            ReadUInt32(body, 16, bigEndian),
            ReadUInt32(body, 20, bigEndian),
            ReadUInt32(body, 24, bigEndian),
            ReadUInt32(body, 28, bigEndian),
            ReadUInt32(body, 32, bigEndian),
            ReadUInt32(body, 36, bigEndian),
            ReadUInt32(body, 40, bigEndian),
            ReadUInt32(body, 44, bigEndian));
    }

    /// <summary>
    ///     Reads a 32 bit header field using the file byte order.
    /// </summary>
    /// <param name="source">The buffer that contains the field.</param>
    /// <param name="offset">The field offset.</param>
    /// <param name="bigEndian">Whether the field is stored big-endian.</param>
    /// <returns>The field value.</returns>
    internal static uint ReadUInt32(ReadOnlySpan<byte> source, int offset, bool bigEndian)
        => bigEndian
            ? BinaryPrimitives.ReadUInt32BigEndian(source[offset..])
            : BinaryPrimitives.ReadUInt32LittleEndian(source[offset..]);
}
