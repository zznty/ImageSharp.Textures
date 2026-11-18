using System.Buffers.Binary;

namespace ImageSharp.Textures.Formats.Ktx2;

/// <summary>
///     Describes one entry of the KTX2 level index.
/// </summary>
internal readonly struct Ktx2LevelIndex
{
    private Ktx2LevelIndex(ulong byteOffset, ulong byteLength, ulong uncompressedByteLength)
    {
        this.ByteOffset = byteOffset;
        this.ByteLength = byteLength;
        this.UncompressedByteLength = uncompressedByteLength;
    }

    /// <summary>
    ///     Gets the absolute offset of the level image data from the start of the file.
    /// </summary>
    internal ulong ByteOffset { get; }

    /// <summary>
    ///     Gets the stored level byte length.
    /// </summary>
    internal ulong ByteLength { get; }

    /// <summary>
    ///     Gets the level byte length after supercompression.
    /// </summary>
    internal ulong UncompressedByteLength { get; }

    /// <summary>
    ///     Parses one 24 byte level index entry.
    /// </summary>
    /// <param name="source">The entry bytes.</param>
    /// <returns>The parsed entry.</returns>
    internal static Ktx2LevelIndex Parse(ReadOnlySpan<byte> source)
        => new(
            BinaryPrimitives.ReadUInt64LittleEndian(source),
            BinaryPrimitives.ReadUInt64LittleEndian(source[8..]),
            BinaryPrimitives.ReadUInt64LittleEndian(source[16..]));
}
