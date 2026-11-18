using System.Buffers.Binary;
using ImageSharp.Textures.Formats.Dds.Enums;

namespace ImageSharp.Textures.Formats.Dds;

/// <summary>
///     The <c>DDS_PIXELFORMAT</c> structure of a DDS header.
/// </summary>
internal readonly struct DdsPixelFormat
{
    internal DdsPixelFormat(uint size, DdsPixelFormatFlags flags, uint fourCc, uint rgbBitCount)
    {
        this.Size = size;
        this.Flags = flags;
        this.FourCC = fourCc;
        this.RGBBitCount = rgbBitCount;
    }

    /// <summary>
    ///     Gets the structure size, which must be 32.
    /// </summary>
    internal uint Size { get; }

    /// <summary>
    ///     Gets the flags indicating what type of data is in the surface.
    /// </summary>
    internal DdsPixelFormatFlags Flags { get; }

    /// <summary>
    ///     Gets the four character code, valid when <see cref="Flags" /> includes
    ///     <see cref="DdsPixelFormatFlags.FourCC" />.
    /// </summary>
    internal uint FourCC { get; }

    /// <summary>
    ///     Gets the number of bits per pixel of an uncompressed format.
    /// </summary>
    internal uint RGBBitCount { get; }

    internal static DdsPixelFormat Parse(ReadOnlySpan<byte> data)
        => new(
            BinaryPrimitives.ReadUInt32LittleEndian(data),
            (DdsPixelFormatFlags)BinaryPrimitives.ReadUInt32LittleEndian(data[4..]),
            BinaryPrimitives.ReadUInt32LittleEndian(data[8..]),
            BinaryPrimitives.ReadUInt32LittleEndian(data[12..]));

    /// <summary>
    ///     Writes the pixel format into the given destination, which must hold at least 32 bytes.
    /// </summary>
    /// <param name="destination">The destination span.</param>
    internal void WriteTo(Span<byte> destination)
    {
        Span<byte> pixelFormat = destination[..DdsConstants.PixelFormatSize];
        pixelFormat.Clear();

        BinaryPrimitives.WriteUInt32LittleEndian(pixelFormat, this.Size);
        BinaryPrimitives.WriteUInt32LittleEndian(pixelFormat[4..], (uint)this.Flags);
        BinaryPrimitives.WriteUInt32LittleEndian(pixelFormat[8..], this.FourCC);
        BinaryPrimitives.WriteUInt32LittleEndian(pixelFormat[12..], this.RGBBitCount);
    }
}
