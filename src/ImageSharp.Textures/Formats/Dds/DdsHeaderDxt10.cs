using System.Buffers.Binary;
using ImageSharp.Textures.Formats.Dds.Enums;

namespace ImageSharp.Textures.Formats.Dds;

/// <summary>
///     The <c>DDS_HEADER_DXT10</c> structure that follows <c>DDS_HEADER</c> when the pixel format four
///     character code is <c>DX10</c>.
/// </summary>
internal readonly struct DdsHeaderDxt10
{
    internal DdsHeaderDxt10(
        DxgiFormat dxgiFormat,
        D3d10ResourceDimension resourceDimension,
        D3d10ResourceMiscFlags miscFlag,
        uint arraySize,
        uint miscFlags2)
    {
        this.DxgiFormat = dxgiFormat;
        this.ResourceDimension = resourceDimension;
        this.MiscFlag = miscFlag;
        this.ArraySize = arraySize;
        this.MiscFlags2 = miscFlags2;
    }

    /// <summary>
    ///     Gets the DXGI surface format.
    /// </summary>
    internal DxgiFormat DxgiFormat { get; }

    /// <summary>
    ///     Gets the type of resource.
    /// </summary>
    internal D3d10ResourceDimension ResourceDimension { get; }

    /// <summary>
    ///     Gets the resource misc flags, including the cube map flag.
    /// </summary>
    internal D3d10ResourceMiscFlags MiscFlag { get; }

    /// <summary>
    ///     Gets the number of array elements, or the number of cubes for a cube map.
    /// </summary>
    internal uint ArraySize { get; }

    /// <summary>
    ///     Gets the second misc flags word, whose low three bits hold the <see cref="DdsAlphaMode" />.
    /// </summary>
    internal uint MiscFlags2 { get; }

    internal static DdsHeaderDxt10 Parse(ReadOnlySpan<byte> data)
        => new(
            (DxgiFormat)BinaryPrimitives.ReadUInt32LittleEndian(data),
            (D3d10ResourceDimension)BinaryPrimitives.ReadUInt32LittleEndian(data[4..]),
            (D3d10ResourceMiscFlags)BinaryPrimitives.ReadUInt32LittleEndian(data[8..]),
            BinaryPrimitives.ReadUInt32LittleEndian(data[12..]),
            BinaryPrimitives.ReadUInt32LittleEndian(data[16..]));

    /// <summary>
    ///     Writes the DX10 header into the given destination, which must hold at least 20 bytes.
    /// </summary>
    /// <param name="destination">The destination span.</param>
    internal void WriteTo(Span<byte> destination)
    {
        Span<byte> header = destination[..DdsConstants.Dx10HeaderSize];
        header.Clear();

        BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)this.DxgiFormat);
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], (uint)this.ResourceDimension);
        BinaryPrimitives.WriteUInt32LittleEndian(header[8..], (uint)this.MiscFlag);
        BinaryPrimitives.WriteUInt32LittleEndian(header[12..], this.ArraySize);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], this.MiscFlags2);
    }
}
