using System.Buffers.Binary;
using ImageSharp.Textures.Formats.Ktx2.Enums;

namespace ImageSharp.Textures.Formats.Ktx2;

/// <summary>
///     Describes the fields of a KTX2 header.
/// </summary>
internal readonly struct Ktx2Header
{
    private Ktx2Header(
        VkFormat vkFormat,
        uint typeSize,
        uint width,
        uint height,
        uint depth,
        uint layerCount,
        uint faceCount,
        uint levelCount,
        uint supercompressionScheme,
        uint dfdByteOffset,
        uint dfdByteLength,
        uint kvdByteOffset,
        uint kvdByteLength,
        ulong sgdByteOffset,
        ulong sgdByteLength)
    {
        this.VkFormat = vkFormat;
        this.TypeSize = typeSize;
        this.Width = width;
        this.Height = height;
        this.Depth = depth;
        this.LayerCount = layerCount;
        this.FaceCount = faceCount;
        this.LevelCount = levelCount;
        this.SupercompressionScheme = supercompressionScheme;
        this.DfdByteOffset = dfdByteOffset;
        this.DfdByteLength = dfdByteLength;
        this.KvdByteOffset = kvdByteOffset;
        this.KvdByteLength = kvdByteLength;
        this.SgdByteOffset = sgdByteOffset;
        this.SgdByteLength = sgdByteLength;
    }

    /// <summary>
    ///     Gets the <c>vkFormat</c> field.
    /// </summary>
    internal VkFormat VkFormat { get; }

    /// <summary>
    ///     Gets the <c>typeSize</c> field.
    /// </summary>
    internal uint TypeSize { get; }

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
    ///     Gets the declared <c>layerCount</c> field.
    /// </summary>
    internal uint LayerCount { get; }

    /// <summary>
    ///     Gets the declared <c>faceCount</c> field.
    /// </summary>
    internal uint FaceCount { get; }

    /// <summary>
    ///     Gets the declared <c>levelCount</c> field.
    /// </summary>
    internal uint LevelCount { get; }

    /// <summary>
    ///     Gets the declared <c>supercompressionScheme</c> field.
    /// </summary>
    internal uint SupercompressionScheme { get; }

    /// <summary>
    ///     Gets the <c>dfdByteOffset</c> field.
    /// </summary>
    internal uint DfdByteOffset { get; }

    /// <summary>
    ///     Gets the <c>dfdByteLength</c> field.
    /// </summary>
    internal uint DfdByteLength { get; }

    /// <summary>
    ///     Gets the <c>kvdByteOffset</c> field.
    /// </summary>
    internal uint KvdByteOffset { get; }

    /// <summary>
    ///     Gets the <c>kvdByteLength</c> field.
    /// </summary>
    internal uint KvdByteLength { get; }

    /// <summary>
    ///     Gets the <c>sgdByteOffset</c> field.
    /// </summary>
    internal ulong SgdByteOffset { get; }

    /// <summary>
    ///     Gets the <c>sgdByteLength</c> field.
    /// </summary>
    internal ulong SgdByteLength { get; }

    /// <summary>
    ///     Parses the 68 header bytes that follow the KTX2 identifier.
    /// </summary>
    /// <param name="body">The header body.</param>
    /// <returns>The parsed header.</returns>
    internal static Ktx2Header Parse(ReadOnlySpan<byte> body) =>
        new(
            (VkFormat)BinaryPrimitives.ReadUInt32LittleEndian(body),
            BinaryPrimitives.ReadUInt32LittleEndian(body[4..]),
            BinaryPrimitives.ReadUInt32LittleEndian(body[8..]),
            BinaryPrimitives.ReadUInt32LittleEndian(body[12..]),
            BinaryPrimitives.ReadUInt32LittleEndian(body[16..]),
            BinaryPrimitives.ReadUInt32LittleEndian(body[20..]),
            BinaryPrimitives.ReadUInt32LittleEndian(body[24..]),
            BinaryPrimitives.ReadUInt32LittleEndian(body[28..]),
            BinaryPrimitives.ReadUInt32LittleEndian(body[32..]),
            BinaryPrimitives.ReadUInt32LittleEndian(body[36..]),
            BinaryPrimitives.ReadUInt32LittleEndian(body[40..]),
            BinaryPrimitives.ReadUInt32LittleEndian(body[44..]),
            BinaryPrimitives.ReadUInt32LittleEndian(body[48..]),
            BinaryPrimitives.ReadUInt64LittleEndian(body[52..]),
            BinaryPrimitives.ReadUInt64LittleEndian(body[60..]));
}
