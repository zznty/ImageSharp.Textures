using System.Buffers.Binary;
using ImageSharp.Textures.Formats.Dds.Enums;

namespace ImageSharp.Textures.Formats.Dds;

/// <summary>
///     Parses DDS headers and materializes the mip level and surface graph.
/// </summary>
internal sealed class DdsDecoderCore
{
    /// <summary>
    ///     The largest total payload size accepted from a header. A 16384 square RGBA878 texture uses 1 GiB, so
    ///     four GiB admits every legal single texture while keeping a lying header from streaming for hours.
    /// </summary>
    private const long MaxTotalPayloadBytes = 4L << 30;

    private const uint AlphaModeMask = 0x7;

    private readonly TextureConfiguration configuration;

    internal DdsDecoderCore(TextureConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        this.configuration = configuration;
    }

    /// <summary>
    ///     Decodes a DDS stream into a texture.
    /// </summary>
    /// <param name="stream">The stream containing the DDS file.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The decoded texture.</returns>
    internal Texture Decode(Stream stream, CancellationToken cancellationToken)
    {
        DdsLayout layout = ReadLayout(stream);
        ValidateStreamLength(stream, layout);

        Texture texture = new(this.configuration, CreateMetadata(layout));

        try
        {
            for (int level = 0; level < layout.MipLevelCount; level++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int width = Math.Max(1, layout.Width >> level);
                int height = Math.Max(1, layout.Height >> level);
                int depth = layout.IsVolume ? Math.Max(1, layout.Depth >> level) : 1;
                int expectedSurfaceBytes = DdsTexelFormats.GetEncodedByteCount(layout.TexelFormat, width, height);
                TextureMipLevel mipLevel = texture.AddMipLevel(width, height, depth);

                // File order is mip-major, then array element, then face, then depth slice, which is the
                // order the surface collection records.
                for (int layer = 0; layer < layout.ArraySize; layer++)
                {
                    for (int face = 0; face < layout.FaceCount; face++)
                    {
                        for (int slice = 0; slice < depth; slice++)
                        {
                            TextureSurface surface = mipLevel.CreateSurface(layout.TexelFormat);

                            if (surface.PayloadLength != expectedSurfaceBytes)
                            {
                                throw new InvalidOperationException(
                                    $"The '{layout.TexelFormat}' texel operations report {surface.PayloadLength} bytes for a "
                                    + $"{width}x{height} surface but the DDS mapping requires {expectedSurfaceBytes} bytes.");
                            }

                            ReadSurfacePayload(
                                stream,
                                surface.GetPayload(),
                                layout.TexelFormat,
                                width,
                                height,
                                layout.MipLevelCount);
                        }
                    }
                }
            }
        }
        catch
        {
            texture.Dispose();
            throw;
        }

        return texture;
    }

    /// <summary>
    ///     Parses the DDS headers without materializing any surface.
    /// </summary>
    /// <param name="stream">The stream containing the DDS file.</param>
    /// <returns>The texture information.</returns>
    internal static TextureInfo Identify(Stream stream)
    {
        DdsLayout layout = ReadLayout(stream);

        return new TextureInfo(
            layout.Width,
            layout.Height,
            layout.BaseDepth,
            layout.MipLevelCount,
            (int)layout.TotalSurfaceCount,
            layout.TexelFormat);
    }

    private static DdsLayout ReadLayout(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Span<byte> headerBytes = stackalloc byte[DdsConstants.HeaderIncludingMagicSize];
        ReadExactly(stream, headerBytes, "the DDS magic word and header");

        if (BinaryPrimitives.ReadUInt32LittleEndian(headerBytes) != DdsConstants.MagicWord)
        {
            throw new InvalidImageContentException(
                "The stream is not a DDS file: the 'DDS ' magic word is missing.");
        }

        DdsHeader header = DdsHeader.Parse(headerBytes[DdsConstants.MagicLength..]);

        if (header.Size != DdsConstants.HeaderSize)
        {
            throw new InvalidImageContentException(
                $"The DDS header declares a size of {header.Size} bytes; {DdsConstants.HeaderSize} bytes are required.");
        }

        if (header.PixelFormat.Size != DdsConstants.PixelFormatSize)
        {
            throw new InvalidImageContentException(
                $"The DDS pixel format declares a size of {header.PixelFormat.Size} bytes; {DdsConstants.PixelFormatSize} bytes are required.");
        }

        bool declaresPitch = (header.Flags & DdsFlags.Pitch) != 0;
        bool declaresLinearSize = (header.Flags & DdsFlags.LinearSize) != 0;
        if (declaresPitch && declaresLinearSize)
        {
            throw new InvalidImageContentException(
                "The DDS header declares both a pitch and a linear size, which is invalid.");
        }

        if ((header.Caps1 & DdsCaps1.Texture) == 0)
        {
            throw new InvalidImageContentException("The DDS header does not declare a texture surface.");
        }

        bool hasDx10Header = (header.PixelFormat.Flags & DdsPixelFormatFlags.FourCC) != 0
                             && header.PixelFormat.FourCC == (uint)DdsFourCc.DX10;

        DdsHeaderDxt10 dx10Header = default;
        if (hasDx10Header)
        {
            Span<byte> dx10Bytes = stackalloc byte[DdsConstants.Dx10HeaderSize];
            ReadExactly(stream, dx10Bytes, "the DDS DX10 header");
            dx10Header = DdsHeaderDxt10.Parse(dx10Bytes);
        }

        TexelFormat texelFormat = ResolveTexelFormat(header, hasDx10Header, dx10Header);
        DdsFourCc fourCc = hasDx10Header ? DdsFourCc.None : (DdsFourCc)header.PixelFormat.FourCC;
        DdsAlphaMode alphaMode = hasDx10Header
            ? (DdsAlphaMode)(dx10Header.MiscFlags2 & AlphaModeMask)
            : DdsAlphaMode.Unknown;

        int width = ValidateDimension(header.Width, "width");
        int height = ValidateDimension(header.Height, "height");
        int mipLevelCount = ValidateMipLevelCount(header, width, height);

        bool isVolume = IsVolume(header, hasDx10Header, dx10Header);
        bool isCubeMap = IsCubeMap(header, hasDx10Header, dx10Header, isVolume, width, height);
        int depth = isVolume ? ValidateDepth(header, hasDx10Header, dx10Header) : 1;
        int arraySize = ValidateArraySize(hasDx10Header, dx10Header, isVolume);
        int faceCount = isCubeMap ? 6 : 1;

        long totalSurfaceCount = 0;
        long totalPayloadBytes = 0;

        for (int level = 0; level < mipLevelCount; level++)
        {
            int levelWidth = Math.Max(1, width >> level);
            int levelHeight = Math.Max(1, height >> level);
            int levelDepth = isVolume ? Math.Max(1, depth >> level) : 1;
            long surfaceCount = (long)arraySize * faceCount * levelDepth;
            totalSurfaceCount += surfaceCount;
            totalPayloadBytes +=
                surfaceCount * DdsTexelFormats.GetEncodedByteCount(texelFormat, levelWidth, levelHeight);
        }

        if (totalSurfaceCount > DdsConstants.MaxSurfaceCount)
        {
            throw new InvalidImageContentException(
                $"The DDS header declares {totalSurfaceCount} surfaces for a {width}x{height} {texelFormat} texture, "
                + $"exceeding the {DdsConstants.MaxSurfaceCount} surface limit.");
        }

        if (totalPayloadBytes > MaxTotalPayloadBytes)
        {
            throw new InvalidImageContentException(
                $"The DDS header declares {totalPayloadBytes} payload bytes for a {width}x{height}x{depth} "
                + $"{texelFormat} texture, exceeding the {MaxTotalPayloadBytes} byte limit.");
        }

        return new DdsLayout(
            texelFormat,
            width,
            height,
            depth,
            mipLevelCount,
            arraySize,
            faceCount,
            isVolume,
            totalSurfaceCount,
            totalPayloadBytes,
            hasDx10Header,
            dx10Header,
            fourCc,
            alphaMode);
    }

    private static TexelFormat ResolveTexelFormat(DdsHeader header, bool hasDx10Header, DdsHeaderDxt10 dx10Header)
    {
        if (hasDx10Header)
        {
            if (!DdsTexelFormats.TryGetTexelFormat(dx10Header.DxgiFormat, out TexelFormat dxgiTexelFormat))
            {
                throw new NotSupportedException(
                    $"The DXGI format {(uint)dx10Header.DxgiFormat} is not supported by the DDS decoder.");
            }

            return dxgiTexelFormat;
        }

        if ((header.PixelFormat.Flags & DdsPixelFormatFlags.FourCC) != 0)
        {
            DdsFourCc fourCc = (DdsFourCc)header.PixelFormat.FourCC;
            if (!DdsTexelFormats.TryGetTexelFormat(fourCc, out TexelFormat fourCcTexelFormat))
            {
                throw new NotSupportedException(
                    $"The DDS four character code '{FormatFourCc(header.PixelFormat.FourCC)}' is not supported by the DDS decoder.");
            }

            return fourCcTexelFormat;
        }

        throw new NotSupportedException(
            $"Uncompressed DDS pixel formats (flags 0x{(uint)header.PixelFormat.Flags:X8}, "
            + $"{header.PixelFormat.RGBBitCount} bits per pixel) are not supported; "
            + "store R8G8B8A8_UNORM in a DX10 header instead.");
    }

    private static int ValidateDimension(uint value, string name)
    {
        if (value == 0)
        {
            throw new InvalidImageContentException($"The DDS header declares a zero {name}.");
        }

        if (value > DdsConstants.MaxDimension)
        {
            throw new InvalidImageContentException(
                $"The DDS header declares a {name} of {value}, exceeding the {DdsConstants.MaxDimension} texel limit.");
        }

        return (int)value;
    }

    private static int ValidateMipLevelCount(DdsHeader header, int width, int height)
    {
        if ((header.Flags & DdsFlags.MipMapCount) == 0)
        {
            return 1;
        }

        if (header.MipMapCount == 0)
        {
            throw new InvalidImageContentException(
                "The DDS header declares the mip map count flag but reports zero mip levels.");
        }

        if (header.MipMapCount > DdsConstants.MaxMipLevelCount)
        {
            throw new InvalidImageContentException(
                $"The DDS header declares {header.MipMapCount} mip levels, exceeding the "
                + $"{DdsConstants.MaxMipLevelCount} level limit.");
        }

        int mipLevelCount = (int)header.MipMapCount;
        int largestDimension = Math.Max(width, height);

        if (mipLevelCount > 1 && 1L << (mipLevelCount - 1) > largestDimension)
        {
            throw new InvalidImageContentException(
                $"The DDS header declares {mipLevelCount} mip levels for a {width}x{height} texture, "
                + $"which cannot contain that many levels.");
        }

        return mipLevelCount;
    }

    private static bool IsVolume(DdsHeader header, bool hasDx10Header, DdsHeaderDxt10 dx10Header)
    {
        bool declaresVolume = (header.Caps2 & DdsCaps2.Volume) != 0;

        if (!hasDx10Header)
        {
            return declaresVolume;
        }

        bool dimensionVolume = dx10Header.ResourceDimension == D3d10ResourceDimension.Texture3D;

        if (dimensionVolume != declaresVolume && (declaresVolume || dimensionVolume))
        {
            throw new InvalidImageContentException(
                "The DDS header disagrees about the resource being a volume texture: "
                + $"the caps declare volume = {declaresVolume}, the DX10 header resource dimension is "
                + $"{dx10Header.ResourceDimension}.");
        }

        return declaresVolume;
    }

    private static bool IsCubeMap(
        DdsHeader header,
        bool hasDx10Header,
        DdsHeaderDxt10 dx10Header,
        bool isVolume,
        int width,
        int height)
    {
        bool declaresCube = (header.Caps2 & DdsCaps2.Cubemap) != 0;
        bool dimensionCube = hasDx10Header && (dx10Header.MiscFlag & D3d10ResourceMiscFlags.TextureCube) != 0;
        bool isCubeMap = declaresCube || dimensionCube;

        if (!isCubeMap)
        {
            return false;
        }

        if (isVolume)
        {
            throw new InvalidImageContentException("The DDS header declares both a cube map and a volume texture.");
        }

        if (declaresCube && (header.Caps2 & DdsCaps2.CubemapAllFaces) != DdsCaps2.CubemapAllFaces)
        {
            throw new InvalidImageContentException("The DDS cube map does not declare all six faces.");
        }

        if (width != height)
        {
            throw new InvalidImageContentException(
                $"The DDS cube map declares {width}x{height} faces; cube map faces must be square.");
        }

        return true;
    }

    private static int ValidateDepth(DdsHeader header, bool hasDx10Header, DdsHeaderDxt10 dx10Header)
    {
        if ((header.Flags & DdsFlags.Depth) == 0 && header.Depth == 0)
        {
            throw new InvalidImageContentException(
                "The DDS header declares a volume texture but carries no depth.");
        }

        if (header.Depth <= 1)
        {
            throw new InvalidImageContentException(
                $"The DDS header declares a volume texture with depth {header.Depth}; at least two slices are required.");
        }

        if (header.Depth > DdsConstants.MaxDimension)
        {
            throw new InvalidImageContentException(
                $"The DDS header declares a depth of {header.Depth}, exceeding the {DdsConstants.MaxDimension} slice limit.");
        }

        if (hasDx10Header
            && dx10Header.ResourceDimension != D3d10ResourceDimension.Texture3D
            && dx10Header.ResourceDimension != D3d10ResourceDimension.Unknown)
        {
            throw new InvalidImageContentException(
                $"The DDS header declares a volume texture but the DX10 resource dimension is {dx10Header.ResourceDimension}.");
        }

        return (int)header.Depth;
    }

    private static int ValidateArraySize(bool hasDx10Header, DdsHeaderDxt10 dx10Header, bool isVolume)
    {
        if (!hasDx10Header)
        {
            return 1;
        }

        if (dx10Header.ResourceDimension is D3d10ResourceDimension.Buffer)
        {
            throw new NotSupportedException(
                "The DDS DX10 header declares a buffer resource, which is not a texture.");
        }

        if (dx10Header.ArraySize == 0)
        {
            throw new InvalidImageContentException("The DDS DX10 header declares zero array elements.");
        }

        if (isVolume && dx10Header.ArraySize != 1)
        {
            throw new InvalidImageContentException(
                $"The DDS DX10 header declares {dx10Header.ArraySize} array elements for a volume texture; "
                + "volume textures must declare exactly one.");
        }

        if (dx10Header.ArraySize > DdsConstants.MaxSurfaceCount)
        {
            throw new InvalidImageContentException(
                $"The DDS DX10 header declares {dx10Header.ArraySize} array elements, exceeding the "
                + $"{DdsConstants.MaxSurfaceCount} element limit.");
        }

        return (int)dx10Header.ArraySize;
    }

    private static void ValidateStreamLength(Stream stream, DdsLayout layout)
    {
        if (!stream.CanSeek)
        {
            return;
        }

        long remaining = stream.Length - stream.Position;
        if (remaining < layout.PayloadBytes)
        {
            throw new InvalidImageContentException(
                $"The DDS payload is truncated: the header declares {layout.PayloadBytes} payload bytes for a "
                + $"{layout.Width}x{layout.Height}x{layout.Depth} {layout.TexelFormat} texture but only {remaining} bytes remain.");
        }
    }

    private static void ReadSurfacePayload(
        Stream stream,
        Span<byte> destination,
        TexelFormat texelFormat,
        int width,
        int height,
        int mipLevelCount)
    {
        try
        {
            stream.ReadExactly(destination);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidImageContentException(
                $"The DDS stream ended while reading a {width}x{height} {texelFormat} surface of a "
                + $"{mipLevelCount} level texture.",
                exception);
        }
    }

    private static TextureMetadata CreateMetadata(DdsLayout layout)
    {
        TextureMetadata metadata = new() { DecodedTextureFormat = DdsFormat.Instance };
        DdsTextureMetadata dds = metadata.GetFormatMetadata(DdsFormat.Instance);

        dds.DxgiFormat = layout.HasDx10Header ? layout.Dx10Header.DxgiFormat : DxgiFormat.Unknown;
        dds.FourCc = layout.FourCc;
        dds.HasDx10Header = layout.HasDx10Header;
        dds.ResourceDimension = layout.HasDx10Header
            ? layout.Dx10Header.ResourceDimension
            : D3d10ResourceDimension.Unknown;

        // Legacy files carry the cube map in caps2 and have no DX10 header; the flags must still describe
        // the texture as a cube map so that the face layout survives a load/save cycle.
        D3d10ResourceMiscFlags miscFlags = layout.HasDx10Header
            ? layout.Dx10Header.MiscFlag
            : D3d10ResourceMiscFlags.Unknown;

        if (layout.FaceCount == 6)
        {
            miscFlags |= D3d10ResourceMiscFlags.TextureCube;
        }

        dds.ResourceMiscFlags = miscFlags;
        dds.ArraySize = (uint)layout.ArraySize;
        dds.MipMapCount = (uint)layout.MipLevelCount;
        dds.Depth = (uint)layout.Depth;
        dds.AlphaMode = layout.AlphaMode;

        return metadata;
    }

    private static string FormatFourCc(uint fourCc)
    {
        Span<char> characters = stackalloc char[4];

        for (int i = 0; i < characters.Length; i++)
        {
            byte value = (byte)(fourCc >> (i * 8));
            characters[i] = value is >= 32 and < 127 ? (char)value : '?';
        }

        return new string(characters);
    }

    private static void ReadExactly(Stream stream, Span<byte> destination, string description)
    {
        try
        {
            stream.ReadExactly(destination);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidImageContentException(
                $"The DDS stream ended while reading {description}.",
                exception);
        }
    }

    private sealed record DdsLayout(
        TexelFormat TexelFormat,
        int Width,
        int Height,
        int Depth,
        int MipLevelCount,
        int ArraySize,
        int FaceCount,
        bool IsVolume,
        long TotalSurfaceCount,
        long PayloadBytes,
        bool HasDx10Header,
        DdsHeaderDxt10 Dx10Header,
        DdsFourCc FourCc,
        DdsAlphaMode AlphaMode)
    {
        internal int BaseDepth => this.IsVolume ? this.Depth : 1;
    }
}
