using System.Buffers.Binary;
using ImageSharp.Textures.Formats.Ktx.Enums;

namespace ImageSharp.Textures.Formats.Ktx;

/// <summary>
///     Parses KTX v1 headers and materializes the mip level and surface graph.
/// </summary>
internal sealed class KtxDecoderCore
{
    private readonly TextureConfiguration configuration;

    /// <summary>
    ///     Initializes a new instance of the <see cref="KtxDecoderCore" /> class.
    /// </summary>
    /// <param name="configuration">The texture configuration used to allocate surface payloads.</param>
    internal KtxDecoderCore(TextureConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        this.configuration = configuration;
    }

    /// <summary>
    ///     Decodes a KTX v1 stream into a texture.
    /// </summary>
    /// <param name="stream">The stream containing the KTX file.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The decoded texture.</returns>
    internal Texture Decode(Stream stream, CancellationToken cancellationToken)
    {
        KtxHeader header = ReadHeader(stream);
        KtxLayout layout = CreateLayout(header);
        ValidateStreamLength(stream, layout);

        byte[] keyValueData = ReadKeyValueData(stream, header);
        Texture texture = new(this.configuration, CreateMetadata(layout, keyValueData));

        try
        {
            TexelOperations operations = TexelFormatRegistry.GetOperations(layout.TexelFormat);

            for (int level = 0; level < layout.MipLevelCount; level++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int width = Math.Max(1, layout.Width >> level);
                int height = Math.Max(1, layout.Height >> level);
                int depth = Math.Max(1, layout.BaseDepth >> level);
                int surfaceBytes = operations.GetEncodedByteCount(width, height);
                int levelSurfaceCount = layout.LayerCount * layout.FaceCount * depth;
                long imageSize = ReadLevelImageSize(stream, header.IsBigEndian);

                long expectedImageSize = layout.IsNonArrayCubeMap
                    ? (long)surfaceBytes * depth
                    : (long)surfaceBytes * levelSurfaceCount;

                if (imageSize != expectedImageSize)
                {
                    throw new InvalidImageContentException(
                        $"The KTX mip level {level} declares an imageSize of {imageSize} bytes for a "
                        + $"{width}x{height}x{depth} '{layout.TexelFormat}' surface layout that requires "
                        + $"{expectedImageSize} bytes.");
                }

                TextureMipLevel mipLevel = texture.AddMipLevel(width, height, depth);

                if (layout.IsNonArrayCubeMap)
                {
                    // Non-array cubemaps store imageSize bytes per face plus cubePadding that aligns the next
                    // face to a four byte boundary; the level as a whole is therefore always four byte aligned.
                    for (int face = 0; face < layout.FaceCount; face++)
                    {
                        for (int slice = 0; slice < depth; slice++)
                        {
                            ReadSurface(stream, mipLevel, layout.TexelFormat, surfaceBytes);
                        }

                        Skip(stream, Padding(imageSize));
                    }
                }
                else
                {
                    // Every other texture stores the whole level contiguously in layer, face, slice order.
                    for (int layer = 0; layer < layout.LayerCount; layer++)
                    {
                        for (int face = 0; face < layout.FaceCount; face++)
                        {
                            for (int slice = 0; slice < depth; slice++)
                            {
                                ReadSurface(stream, mipLevel, layout.TexelFormat, surfaceBytes);
                            }
                        }
                    }
                }

                long levelPaddingBasis = layout.IsNonArrayCubeMap
                    ? Align4(imageSize) * layout.FaceCount
                    : imageSize;
                Skip(stream, Padding(levelPaddingBasis));
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
    ///     Parses a KTX v1 header without materializing any surface.
    /// </summary>
    /// <param name="stream">The stream containing the KTX file.</param>
    /// <returns>The texture information.</returns>
    internal static TextureInfo Identify(Stream stream)
    {
        KtxLayout layout = CreateLayout(ReadHeader(stream));

        return new TextureInfo(
            layout.Width,
            layout.Height,
            layout.BaseDepth,
            layout.MipLevelCount,
            layout.SurfaceCount,
            layout.TexelFormat);
    }

    private static KtxHeader ReadHeader(Stream stream)
    {
        Span<byte> prefix = stackalloc byte[KtxConstants.IdentifierLength + 4];
        ReadExactly(stream, prefix, "the KTX identifier and endianness word");

        if (!prefix[..KtxConstants.IdentifierLength].SequenceEqual(KtxConstants.Identifier))
        {
            throw new InvalidImageContentException("The stream is not a KTX v1 file: the file identifier is missing.");
        }

        uint endianMarker = BinaryPrimitives.ReadUInt32LittleEndian(prefix[KtxConstants.IdentifierLength..]);
        KtxEndianness endianness = endianMarker switch
        {
            0x04030201 => KtxEndianness.LittleEndian,
            0x01020304 => KtxEndianness.BigEndian,
            _ => throw new InvalidImageContentException(
                $"The KTX endianness word 0x{endianMarker:X8} is neither 0x04030201 nor 0x01020304.")
        };

        Span<byte> body = stackalloc byte[KtxConstants.HeaderBodyLength];
        ReadExactly(stream, body, "the KTX header");
        return KtxHeader.Parse(body, endianness);
    }

    private static KtxLayout CreateLayout(KtxHeader header)
    {
        if (header.Width == 0)
        {
            throw new InvalidImageContentException("The KTX pixelWidth field is zero.");
        }

        if (header.Height == 0)
        {
            throw new NotSupportedException(
                "1D KTX textures (pixelHeight = 0) are not supported by the texture model.");
        }

        if (header.Width > KtxConstants.MaxDimension
            || header.Height > KtxConstants.MaxDimension
            || header.Depth > KtxConstants.MaxDimension)
        {
            throw new InvalidImageContentException(
                $"The KTX dimensions {header.Width}x{header.Height}x{header.Depth} exceed the supported maximum "
                + $"of {KtxConstants.MaxDimension} per axis.");
        }

        if (header.FaceCount is not 1 and not 6)
        {
            throw new InvalidImageContentException(
                $"The KTX numberOfFaces field is {header.FaceCount}; only 1 and 6 are valid.");
        }

        if (header.FaceCount == 6 && header.Depth != 0)
        {
            throw new InvalidImageContentException(
                $"The KTX cubemap declares a pixelDepth of {header.Depth}; cubemaps must be two dimensional.");
        }

        if (header.ArrayElementCount > KtxConstants.MaxSurfaceCount)
        {
            throw new InvalidImageContentException(
                $"The KTX numberOfArrayElements field is {header.ArrayElementCount}, which exceeds the supported maximum.");
        }

        int layerCount = (int)Math.Max(1U, header.ArrayElementCount);
        int faceCount = (int)header.FaceCount;
        int mipLevelCount = (int)Math.Max(1U, header.MipLevelCount);

        if (mipLevelCount > KtxConstants.MaxMipLevelCount)
        {
            throw new InvalidImageContentException(
                $"The KTX numberOfMipmapLevels field is {header.MipLevelCount}, which exceeds the supported maximum of "
                + $"{KtxConstants.MaxMipLevelCount}.");
        }

        int baseDepth = (int)Math.Max(1U, header.Depth);
        int maxLevelCount = GetMaximumLevelCount((int)header.Width, (int)header.Height, baseDepth);
        if (mipLevelCount > maxLevelCount)
        {
            throw new InvalidImageContentException(
                $"The KTX numberOfMipmapLevels field is {header.MipLevelCount} but a "
                + $"{header.Width}x{header.Height}x{baseDepth} texture can only have {maxLevelCount} levels.");
        }

        TexelFormat texelFormat = ResolveTexelFormat(header);
        long surfaceCount = 0;
        for (int level = 0; level < mipLevelCount; level++)
        {
            surfaceCount += (long)layerCount * faceCount * Math.Max(1, baseDepth >> level);
        }

        if (surfaceCount > KtxConstants.MaxSurfaceCount)
        {
            throw new InvalidImageContentException(
                $"The KTX layout declares {surfaceCount} surfaces, which exceeds the supported maximum of "
                + $"{KtxConstants.MaxSurfaceCount}.");
        }

        return new KtxLayout(
            header,
            texelFormat,
            (int)header.Width,
            (int)header.Height,
            baseDepth,
            layerCount,
            faceCount,
            mipLevelCount,
            (int)surfaceCount);
    }

    private static TexelFormat ResolveTexelFormat(KtxHeader header)
    {
        if (!KtxTexelFormats.TryGetTexelFormat(header.GlInternalFormat, out TexelFormat texelFormat))
        {
            throw new NotSupportedException(
                $"The KTX glInternalFormat 0x{header.GlInternalFormat:X} has no supported texel format.");
        }

        bool isCompressed = texelFormat != TexelFormat.R8G8B8A8_UNorm;

        if (isCompressed)
        {
            if (header.GlType != 0 || header.GlFormat != 0)
            {
                throw new InvalidImageContentException(
                    $"The KTX compressed texture declares glType 0x{header.GlType:X} and glFormat "
                    + $"0x{header.GlFormat:X}; both must be zero.");
            }

            // Compressed blocks do not depend on the host byte order, so the file endianness only applies to
            // the header fields.
            if (header.GlTypeSize != 1)
            {
                throw new InvalidImageContentException(
                    $"The KTX compressed texture declares glTypeSize {header.GlTypeSize}; it must be 1.");
            }

            return texelFormat;
        }

        if (header.GlType != (uint)GlType.UnsignedByte
            || header.GlFormat != (uint)GlFormat.Rgba
            || header.GlTypeSize != 1)
        {
            throw new NotSupportedException(
                $"The KTX uncompressed texture declares glType 0x{header.GlType:X}, glTypeSize {header.GlTypeSize} "
                + $"and glFormat 0x{header.GlFormat:X}; only GL_UNSIGNED_BYTE RGBA data is supported.");
        }

        return texelFormat;
    }

    private static void ValidateStreamLength(Stream stream, KtxLayout layout)
    {
        if (!stream.CanSeek)
        {
            return;
        }

        TexelOperations operations = TexelFormatRegistry.GetOperations(layout.TexelFormat);
        long required = 0;

        for (int level = 0; level < layout.MipLevelCount; level++)
        {
            int width = Math.Max(1, layout.Width >> level);
            int height = Math.Max(1, layout.Height >> level);
            int depth = Math.Max(1, layout.BaseDepth >> level);
            long levelBytes = (long)operations.GetEncodedByteCount(width, height)
                              * layout.LayerCount * layout.FaceCount * depth;

            if (layout.IsNonArrayCubeMap)
            {
                long faceBytes = levelBytes / layout.FaceCount;
                levelBytes = layout.FaceCount * Align4(faceBytes);
            }

            required += 4 + levelBytes + Padding(levelBytes);

            if (required > KtxConstants.MaxTotalPayloadBytes)
            {
                throw new InvalidImageContentException(
                    $"The KTX layout requires more than {KtxConstants.MaxTotalPayloadBytes} bytes of payload.");
            }
        }

        long remaining = stream.Length - stream.Position;
        if (required > remaining)
        {
            throw new InvalidImageContentException(
                $"The KTX file declares {required} bytes of mip data but only {remaining} bytes remain in the stream.");
        }
    }

    private static byte[] ReadKeyValueData(Stream stream, KtxHeader header)
    {
        uint length = header.BytesOfKeyValueData;
        if (length == 0)
        {
            return [];
        }

        if (length > KtxConstants.MaxKeyValueDataLength)
        {
            throw new InvalidImageContentException(
                $"The KTX bytesOfKeyValueData field is {length}, which exceeds the supported maximum of "
                + $"{KtxConstants.MaxKeyValueDataLength} bytes.");
        }

        if (stream.CanSeek && length > stream.Length - stream.Position)
        {
            throw new InvalidImageContentException(
                $"The KTX bytesOfKeyValueData field is {length} but only {stream.Length - stream.Position} bytes "
                + "remain in the stream.");
        }

        byte[] data = new byte[(int)length];
        ReadExactly(stream, data, "the key/value data block");

        return data;
    }

    private static TextureMetadata CreateMetadata(KtxLayout layout, byte[] keyValueData)
    {
        KtxHeader header = layout.Header;
        TextureMetadata metadata = new() { DecodedTextureFormat = KtxFormat.Instance };
        KtxTextureMetadata formatMetadata = metadata.GetFormatMetadata(KtxFormat.Instance);

        formatMetadata.Endianness = header.Endianness;
        formatMetadata.GlType = header.GlType;
        formatMetadata.GlTypeSize = header.GlTypeSize;
        formatMetadata.GlFormat = header.GlFormat;
        formatMetadata.GlInternalFormat = header.GlInternalFormat;
        formatMetadata.GlBaseInternalFormat = header.GlBaseInternalFormat;
        formatMetadata.ArrayElementCount = header.ArrayElementCount;
        formatMetadata.FaceCount = header.FaceCount;
        formatMetadata.MipLevelCount = header.MipLevelCount;
        formatMetadata.KeyValueData = keyValueData;
        formatMetadata.KeyValuePairs = keyValueData.Length == 0
            ? []
            : KtxKeyValueData.Parse(keyValueData, header.IsBigEndian);

        return metadata;
    }

    private static void ReadSurface(
        Stream stream,
        TextureMipLevel mipLevel,
        TexelFormat texelFormat,
        int surfaceBytes)
    {
        TextureSurface surface = mipLevel.CreateSurface(texelFormat);
        Span<byte> payload = surface.GetPayload();

        if (payload.Length != surfaceBytes)
        {
            throw new InvalidOperationException(
                $"The '{texelFormat}' texel operations report {payload.Length} bytes for the surface but the KTX "
                + $"mapping requires {surfaceBytes} bytes.");
        }

        ReadExactly(stream, payload, "a texture surface");
    }

    private static long ReadLevelImageSize(Stream stream, bool bigEndian)
    {
        Span<byte> buffer = stackalloc byte[4];
        ReadExactly(stream, buffer, "a mip level image size");
        return KtxHeader.ReadUInt32(buffer, 0, bigEndian);
    }

    private static void ReadExactly(Stream stream, Span<byte> destination, string description)
    {
        try
        {
            stream.ReadExactly(destination);
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidImageContentException($"The KTX file ended while reading {description}.", ex);
        }
    }

    private static void Skip(Stream stream, int count)
    {
        if (count == 0)
        {
            return;
        }

        Span<byte> buffer = stackalloc byte[4];
        ReadExactly(stream, buffer[..count], "mip padding");
    }

    private static int Padding(long length) => (int)((4 - (length % 4)) % 4);

    private static long Align4(long length) => length + Padding(length);

    private static int GetMaximumLevelCount(int width, int height, int depth)
    {
        int maximum = Math.Max(width, Math.Max(height, depth));
        int levels = 1;

        while (maximum > 1)
        {
            maximum >>= 1;
            levels++;
        }

        return levels;
    }

    /// <summary>
    ///     The validated layout of a KTX v1 file.
    /// </summary>
    private sealed class KtxLayout
    {
        internal KtxLayout(
            KtxHeader header,
            TexelFormat texelFormat,
            int width,
            int height,
            int baseDepth,
            int layerCount,
            int faceCount,
            int mipLevelCount,
            int surfaceCount)
        {
            this.Header = header;
            this.TexelFormat = texelFormat;
            this.Width = width;
            this.Height = height;
            this.BaseDepth = baseDepth;
            this.LayerCount = layerCount;
            this.FaceCount = faceCount;
            this.MipLevelCount = mipLevelCount;
            this.SurfaceCount = surfaceCount;
        }

        internal KtxHeader Header { get; }

        internal TexelFormat TexelFormat { get; }

        internal int Width { get; }

        internal int Height { get; }

        internal int BaseDepth { get; }

        internal int LayerCount { get; }

        internal int FaceCount { get; }

        internal int MipLevelCount { get; }

        internal int SurfaceCount { get; }

        internal bool IsNonArrayCubeMap => this.FaceCount == 6 && this.LayerCount == 1;
    }
}
