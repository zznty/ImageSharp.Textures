using System.Buffers.Binary;
using ImageSharp.Textures.Formats.Dds;

namespace ImageSharp.Textures.Tests.TestUtilities.Dds;

/// <summary>
///     Builds DDS files for the container tests. Every header field is settable so malformed files can be
///     produced without a binary fixture.
/// </summary>
internal sealed class DdsFileBuilder
{
    private const uint FlagCaps = 0x1;
    private const uint FlagHeight = 0x2;
    private const uint FlagWidth = 0x4;
    private const uint FlagPixelFormat = 0x1000;
    private const uint FlagMipMapCount = 0x20000;
    private const uint FlagLinearSize = 0x80000;
    private const uint FlagDepth = 0x800000;

    private const uint Caps1Complex = 0x8;
    private const uint Caps1MipMap = 0x400000;
    private const uint Caps1Texture = 0x1000;

    private const uint Caps2CubeMap = 0x200;
    private const uint Caps2CubeMapAllFaces = 0xFE00;
    private const uint Caps2Volume = 0x200000;

    private const uint MiscFlagTextureCube = 0x4;
    private const uint ResourceDimensionTexture2D = 3;
    private const uint ResourceDimensionTexture3D = 4;
    private readonly int blockBytes;

    private readonly List<byte> payload = [];

    private DdsFileBuilder(int blockBytes)
    {
        this.blockBytes = blockBytes;
        this.HeaderSize = 124;
        this.PixelFormatSize = 32;
    }

    /// <summary>Gets or sets the surface width, in texels.</summary>
    internal int Width { get; set; }

    /// <summary>Gets or sets the surface height, in texels.</summary>
    internal int Height { get; set; }

    /// <summary>Gets or sets the volume depth.</summary>
    internal int Depth { get; set; } = 1;

    /// <summary>Gets or sets the mip level count.</summary>
    internal int MipLevelCount { get; set; } = 1;

    /// <summary>Gets or sets the array element count.</summary>
    internal int ArraySize { get; set; } = 1;

    /// <summary>Gets or sets a value indicating whether the texture is a cube map.</summary>
    internal bool IsCubeMap { get; set; }

    /// <summary>Gets or sets a value indicating whether the texture is a volume texture.</summary>
    internal bool IsVolume { get; set; }

    /// <summary>Gets or sets the header flags.</summary>
    internal uint Flags { get; set; }

    /// <summary>Gets or sets the <c>DDS_CAPS</c> value.</summary>
    internal uint Caps1 { get; set; } = Caps1Texture;

    /// <summary>Gets or sets the <c>DDS_CAPS2</c> value.</summary>
    internal uint Caps2 { get; set; }

    /// <summary>Gets or sets the pixel format flags.</summary>
    internal uint PixelFormatFlags { get; set; }

    /// <summary>Gets or sets the pixel format four character code.</summary>
    internal uint FourCc { get; set; }

    /// <summary>Gets or sets the number of bits per pixel of an uncompressed format.</summary>
    internal uint RgbBitCount { get; set; }

    /// <summary>Gets or sets the declared header size.</summary>
    internal uint HeaderSize { get; set; }

    /// <summary>Gets or sets the declared pixel format size.</summary>
    internal uint PixelFormatSize { get; set; }

    /// <summary>Gets or sets a value indicating whether a DX10 header follows the main header.</summary>
    internal bool HasDx10Header { get; set; }

    /// <summary>Gets or sets the DXGI format written to the DX10 header.</summary>
    internal uint DxgiFormat { get; set; }

    /// <summary>Gets or sets the resource dimension written to the DX10 header.</summary>
    internal uint ResourceDimension { get; set; } = ResourceDimensionTexture2D;

    /// <summary>Gets or sets the misc flags written to the DX10 header.</summary>
    internal uint Dx10MiscFlags { get; set; }

    /// <summary>Gets or sets the second misc flags word written to the DX10 header.</summary>
    internal uint Dx10MiscFlags2 { get; set; }

    /// <summary>Gets or sets a value indicating whether the magic word is written.</summary>
    internal bool IncludeMagic { get; set; } = true;

    /// <summary>Gets the number of payload bytes appended so far.</summary>
    internal int PayloadLength => this.payload.Count;

    /// <summary>
    ///     Creates a builder for a file with a legacy pixel format header.
    /// </summary>
    /// <param name="fourCc">The four character code.</param>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    /// <param name="blockBytes">The block size in bytes, or zero for a four byte per texel format.</param>
    /// <returns>The builder.</returns>
    internal static DdsFileBuilder CreateLegacy(DdsFourCc fourCc, int width, int height, int blockBytes)
    {
        DdsFileBuilder builder = new(blockBytes)
        {
            Width = width,
            Height = height,
            FourCc = (uint)fourCc,
            PixelFormatFlags = 0x4,
            Flags = FlagCaps | FlagHeight | FlagWidth | FlagPixelFormat | FlagLinearSize
        };

        return builder;
    }

    /// <summary>
    ///     Creates a builder for a file with a DX10 header.
    /// </summary>
    /// <param name="dxgiFormat">The DXGI format value.</param>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    /// <param name="blockBytes">The block size in bytes, or zero for a four byte per texel format.</param>
    /// <returns>The builder.</returns>
    internal static DdsFileBuilder CreateDx10(uint dxgiFormat, int width, int height, int blockBytes)
    {
        DdsFileBuilder builder = new(blockBytes)
        {
            Width = width,
            Height = height,
            HasDx10Header = true,
            DxgiFormat = dxgiFormat,
            FourCc = (uint)DdsFourCc.DX10,
            PixelFormatFlags = 0x4,
            Flags = FlagCaps | FlagHeight | FlagWidth | FlagPixelFormat | FlagLinearSize
        };

        return builder;
    }

    /// <summary>
    ///     Gets the payload size of one surface for the given dimensions.
    /// </summary>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    /// <returns>The payload size, in bytes.</returns>
    internal int GetSurfaceSize(int width, int height)
        => this.blockBytes == 0
            ? width * height * 4
            : (width + 3) / 4 * ((height + 3) / 4) * this.blockBytes;

    /// <summary>
    ///     Applies the caps, flags and DX10 fields implied by the current layout.
    /// </summary>
    /// <returns>The builder.</returns>
    internal DdsFileBuilder WithDerivedHeaderFields()
    {
        if (this.MipLevelCount > 1)
        {
            this.Flags |= FlagMipMapCount;
            this.Caps1 |= Caps1Complex | Caps1MipMap;
        }

        if (this.IsVolume)
        {
            this.Flags |= FlagDepth;
            this.Caps1 |= Caps1Complex;
            this.Caps2 |= Caps2Volume;
            this.ResourceDimension = ResourceDimensionTexture3D;
        }

        if (this.IsCubeMap)
        {
            this.Caps1 |= Caps1Complex;
            this.Caps2 |= Caps2CubeMap | Caps2CubeMapAllFaces;
            this.Dx10MiscFlags |= MiscFlagTextureCube;
        }

        return this;
    }

    /// <summary>
    ///     Appends one payload per surface, filled with a deterministic value derived from the surface index so
    ///     tests can verify the file order.
    /// </summary>
    /// <param name="seed">The seed added to every surface index.</param>
    /// <returns>The builder.</returns>
    internal DdsFileBuilder AddPatternPayloads(byte seed = 0)
    {
        int surfaceIndex = 0;

        for (int level = 0; level < this.MipLevelCount; level++)
        {
            int width = Math.Max(1, this.Width >> level);
            int height = Math.Max(1, this.Height >> level);
            int depth = this.IsVolume ? Math.Max(1, this.Depth >> level) : 1;
            int size = this.GetSurfaceSize(width, height);

            for (int layer = 0; layer < this.ArraySize; layer++)
            {
                for (int face = 0; face < (this.IsCubeMap ? 6 : 1); face++)
                {
                    for (int slice = 0; slice < depth; slice++)
                    {
                        byte value = (byte)(seed + surfaceIndex);
                        this.payload.AddRange(Enumerable.Repeat(value, size));
                        surfaceIndex++;
                    }
                }
            }
        }

        return this;
    }

    /// <summary>
    ///     Appends raw payload bytes.
    /// </summary>
    /// <param name="bytes">The bytes to append.</param>
    /// <returns>The builder.</returns>
    internal DdsFileBuilder AddPayload(params byte[] bytes)
    {
        this.payload.AddRange(bytes);
        return this;
    }

    /// <summary>
    ///     Removes payload bytes from the end of the file.
    /// </summary>
    /// <param name="count">The number of bytes to remove.</param>
    /// <returns>The builder.</returns>
    internal DdsFileBuilder TruncatePayload(int count)
    {
        this.payload.RemoveRange(this.payload.Count - count, count);
        return this;
    }

    /// <summary>
    ///     Serializes the file.
    /// </summary>
    /// <returns>The encoded DDS file.</returns>
    internal byte[] Build()
    {
        byte[] buffer =
            new byte[(this.IncludeMagic ? 4 : 0) + 124 + (this.HasDx10Header ? 20 : 0) + this.payload.Count];
        Span<byte> span = buffer;
        int offset = 0;

        if (this.IncludeMagic)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(span, DdsConstants.MagicWord);
            offset += 4;
        }

        WriteHeader(span[offset..], this);
        offset += 124;

        if (this.HasDx10Header)
        {
            WriteDx10Header(span[offset..], this);
            offset += 20;
        }

        this.payload.CopyTo(buffer, offset);
        return buffer;
    }

    private static void WriteHeader(Span<byte> header, DdsFileBuilder builder)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(header, builder.HeaderSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], builder.Flags);
        BinaryPrimitives.WriteUInt32LittleEndian(header[8..], (uint)builder.Height);
        BinaryPrimitives.WriteUInt32LittleEndian(header[12..], (uint)builder.Width);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], 0);
        BinaryPrimitives.WriteUInt32LittleEndian(header[20..], (uint)builder.Depth);
        BinaryPrimitives.WriteUInt32LittleEndian(header[24..], (uint)builder.MipLevelCount);

        BinaryPrimitives.WriteUInt32LittleEndian(header[72..], builder.PixelFormatSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header[76..], builder.PixelFormatFlags);
        BinaryPrimitives.WriteUInt32LittleEndian(header[80..], builder.FourCc);
        BinaryPrimitives.WriteUInt32LittleEndian(header[84..], builder.RgbBitCount);

        BinaryPrimitives.WriteUInt32LittleEndian(header[104..], builder.Caps1);
        BinaryPrimitives.WriteUInt32LittleEndian(header[108..], builder.Caps2);
    }

    private static void WriteDx10Header(Span<byte> dx10, DdsFileBuilder builder)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(dx10, builder.DxgiFormat);
        BinaryPrimitives.WriteUInt32LittleEndian(dx10[4..], builder.ResourceDimension);
        BinaryPrimitives.WriteUInt32LittleEndian(dx10[8..], builder.Dx10MiscFlags);
        BinaryPrimitives.WriteUInt32LittleEndian(dx10[12..], (uint)builder.ArraySize);
        BinaryPrimitives.WriteUInt32LittleEndian(dx10[16..], builder.Dx10MiscFlags2);
    }
}
