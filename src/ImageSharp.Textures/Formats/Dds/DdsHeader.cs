using System.Buffers.Binary;
using ImageSharp.Textures.Formats.Dds.Enums;

namespace ImageSharp.Textures.Formats.Dds;

/// <summary>
///     The <c>DDS_HEADER</c> structure of a DDS file, excluding the magic word.
/// </summary>
internal readonly struct DdsHeader
{
    internal DdsHeader(
        uint size,
        DdsFlags flags,
        uint height,
        uint width,
        uint pitchOrLinearSize,
        uint depth,
        uint mipMapCount,
        DdsPixelFormat pixelFormat,
        DdsCaps1 caps1,
        DdsCaps2 caps2)
    {
        this.Size = size;
        this.Flags = flags;
        this.Height = height;
        this.Width = width;
        this.PitchOrLinearSize = pitchOrLinearSize;
        this.Depth = depth;
        this.MipMapCount = mipMapCount;
        this.PixelFormat = pixelFormat;
        this.Caps1 = caps1;
        this.Caps2 = caps2;
    }

    /// <summary>
    ///     Gets the structure size, which must be 124.
    /// </summary>
    internal uint Size { get; }

    /// <summary>
    ///     Gets the flags indicating which members contain valid data.
    /// </summary>
    internal DdsFlags Flags { get; }

    /// <summary>
    ///     Gets the surface height, in pixels.
    /// </summary>
    internal uint Height { get; }

    /// <summary>
    ///     Gets the surface width, in pixels.
    /// </summary>
    internal uint Width { get; }

    /// <summary>
    ///     Gets the pitch of an uncompressed texture or the total size of a compressed top level.
    /// </summary>
    internal uint PitchOrLinearSize { get; }

    /// <summary>
    ///     Gets the depth of a volume texture; zero or one for other textures.
    /// </summary>
    internal uint Depth { get; }

    /// <summary>
    ///     Gets the declared mip map count, valid when <see cref="Flags" /> includes
    ///     <see cref="DdsFlags.MipMapCount" />.
    /// </summary>
    internal uint MipMapCount { get; }

    /// <summary>
    ///     Gets the pixel format.
    /// </summary>
    internal DdsPixelFormat PixelFormat { get; }

    /// <summary>
    ///     Gets the surface complexity capabilities.
    /// </summary>
    internal DdsCaps1 Caps1 { get; }

    /// <summary>
    ///     Gets the additional surface capabilities.
    /// </summary>
    internal DdsCaps2 Caps2 { get; }

    internal static DdsHeader Parse(ReadOnlySpan<byte> data)
        => new(
            BinaryPrimitives.ReadUInt32LittleEndian(data),
            (DdsFlags)BinaryPrimitives.ReadUInt32LittleEndian(data[4..]),
            BinaryPrimitives.ReadUInt32LittleEndian(data[8..]),
            BinaryPrimitives.ReadUInt32LittleEndian(data[12..]),
            BinaryPrimitives.ReadUInt32LittleEndian(data[16..]),
            BinaryPrimitives.ReadUInt32LittleEndian(data[20..]),
            BinaryPrimitives.ReadUInt32LittleEndian(data[24..]),
            DdsPixelFormat.Parse(data[72..]),
            (DdsCaps1)BinaryPrimitives.ReadUInt32LittleEndian(data[104..]),
            (DdsCaps2)BinaryPrimitives.ReadUInt32LittleEndian(data[108..]));

    /// <summary>
    ///     Writes the header into the given destination, which must hold at least 124 bytes.
    /// </summary>
    /// <param name="destination">The destination span.</param>
    internal void WriteTo(Span<byte> destination)
    {
        Span<byte> header = destination[..DdsConstants.HeaderSize];
        header.Clear();

        BinaryPrimitives.WriteUInt32LittleEndian(header, this.Size);
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], (uint)this.Flags);
        BinaryPrimitives.WriteUInt32LittleEndian(header[8..], this.Height);
        BinaryPrimitives.WriteUInt32LittleEndian(header[12..], this.Width);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], this.PitchOrLinearSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header[20..], this.Depth);
        BinaryPrimitives.WriteUInt32LittleEndian(header[24..], this.MipMapCount);
        this.PixelFormat.WriteTo(header[72..]);
        BinaryPrimitives.WriteUInt32LittleEndian(header[104..], (uint)this.Caps1);
        BinaryPrimitives.WriteUInt32LittleEndian(header[108..], (uint)this.Caps2);
    }
}
