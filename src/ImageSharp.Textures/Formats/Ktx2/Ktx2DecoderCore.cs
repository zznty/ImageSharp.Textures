using ImageSharp.Textures.Formats.Ktx;

namespace ImageSharp.Textures.Formats.Ktx2;

/// <summary>
///     Parses KTX2 headers and materializes the mip level and surface graph.
/// </summary>
internal sealed class Ktx2DecoderCore
{
    private readonly TextureConfiguration configuration;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Ktx2DecoderCore" /> class.
    /// </summary>
    /// <param name="configuration">The texture configuration used to allocate surface payloads.</param>
    internal Ktx2DecoderCore(TextureConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        this.configuration = configuration;
    }

    /// <summary>
    ///     Decodes a KTX2 stream into a texture.
    /// </summary>
    /// <param name="stream">The stream containing the KTX2 file.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The decoded texture.</returns>
    internal Texture Decode(Stream stream, CancellationToken cancellationToken)
    {
        if (!stream.CanSeek)
        {
            throw new NotSupportedException(
                "KTX2 decoding requires a seekable stream because the level index addresses mip data from the "
                + "start of the file.");
        }

        Ktx2Header header = ReadHeader(stream);
        Ktx2Layout layout = CreateLayout(header);
        Ktx2LevelIndex[] levelIndices = ReadLevelIndex(stream, layout.LevelCount);
        ReadDataFormatDescriptor(stream, header, layout.FormatInfo);
        byte[] keyValueData = ReadKeyValueData(stream, header);
        ValidateLevelIndices(stream, layout, levelIndices, TexelFormatRegistry.GetOperations(layout.TexelFormat));

        Texture texture = new(this.configuration, CreateMetadata(layout, keyValueData));
        TexelOperations operations = TexelFormatRegistry.GetOperations(layout.TexelFormat);

        try
        {
            for (int level = 0; level < layout.LevelCount; level++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int width = layout.GetLevelWidth(level);
                int height = layout.GetLevelHeight(level);
                int depth = layout.GetLevelDepth(level);
                int surfaceBytes = operations.GetEncodedByteCount(width, height);

                stream.Position = (long)levelIndices[level].ByteOffset;
                TextureMipLevel mipLevel = texture.AddMipLevel(width, height, depth);

                // KTX2 stores level images in layer, face, slice order; the level index entry addresses the
                // first byte of the level, after any mipPadding.
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
        }
        catch
        {
            texture.Dispose();
            throw;
        }

        return texture;
    }

    /// <summary>
    ///     Parses a KTX2 header without materializing any surface.
    /// </summary>
    /// <param name="stream">The stream containing the KTX2 file.</param>
    /// <returns>The texture information.</returns>
    internal static TextureInfo Identify(Stream stream)
    {
        Ktx2Layout layout = CreateLayout(ReadHeader(stream));

        return new TextureInfo(
            layout.Width,
            layout.Height,
            layout.BaseDepth,
            layout.LevelCount,
            layout.SurfaceCount,
            layout.TexelFormat);
    }

    private static Ktx2Header ReadHeader(Stream stream)
    {
        Span<byte> prefix = stackalloc byte[Ktx2Constants.HeaderLength];
        ReadExactly(stream, prefix, "the KTX2 identifier and header");

        if (!prefix[..Ktx2Constants.IdentifierLength].SequenceEqual(Ktx2Constants.Identifier))
        {
            throw new InvalidImageContentException("The stream is not a KTX2 file: the file identifier is missing.");
        }

        return Ktx2Header.Parse(prefix[Ktx2Constants.IdentifierLength..]);
    }

    private static Ktx2Layout CreateLayout(Ktx2Header header)
    {
        if (header.Width == 0)
        {
            throw new InvalidImageContentException("The KTX2 pixelWidth field is zero.");
        }

        if (header.Height == 0)
        {
            throw new NotSupportedException(
                "1D KTX2 textures (pixelHeight = 0) are not supported by the texture model.");
        }

        if (header.Width > Ktx2Constants.MaxDimension
            || header.Height > Ktx2Constants.MaxDimension
            || header.Depth > Ktx2Constants.MaxDimension)
        {
            throw new InvalidImageContentException(
                $"The KTX2 dimensions {header.Width}x{header.Height}x{header.Depth} exceed the supported maximum "
                + $"of {Ktx2Constants.MaxDimension} per axis.");
        }

        if (header.FaceCount is not 1 and not 6)
        {
            throw new InvalidImageContentException(
                $"The KTX2 faceCount field is {header.FaceCount}; only 1 and 6 are valid.");
        }

        if (header.FaceCount == 6 && header.Depth != 0)
        {
            throw new InvalidImageContentException(
                $"The KTX2 cubemap declares a pixelDepth of {header.Depth}; cubemaps must be two dimensional.");
        }

        if (header.LayerCount > Ktx2Constants.MaxSurfaceCount)
        {
            throw new InvalidImageContentException(
                $"The KTX2 layerCount field is {header.LayerCount}, which exceeds the supported maximum.");
        }

        if (header.SupercompressionScheme != 0)
        {
            throw new NotSupportedException(
                $"The KTX2 file uses supercompression scheme {header.SupercompressionScheme} "
                + $"({GetSupercompressionName(header.SupercompressionScheme)}); supercompressed textures are not "
                + "supported.");
        }

        if (header.SgdByteLength != 0)
        {
            throw new InvalidImageContentException(
                "The KTX2 file declares supercompression global data but does not use supercompression; "
                + "sgdByteLength must be zero.");
        }

        int levelCount = (int)Math.Max(1U, header.LevelCount);
        if (levelCount > Ktx2Constants.MaxMipLevelCount)
        {
            throw new InvalidImageContentException(
                $"The KTX2 levelCount field is {header.LevelCount}, which exceeds the supported maximum of "
                + $"{Ktx2Constants.MaxMipLevelCount}.");
        }

        int baseDepth = (int)Math.Max(1U, header.Depth);
        int maxLevelCount = GetMaximumLevelCount((int)header.Width, (int)header.Height, baseDepth);
        if (levelCount > maxLevelCount)
        {
            throw new InvalidImageContentException(
                $"The KTX2 levelCount field is {header.LevelCount} but a "
                + $"{header.Width}x{header.Height}x{baseDepth} texture can only have {maxLevelCount} levels.");
        }

        if (!Ktx2TexelFormats.TryGetFormatInfo(header.VkFormat, out Ktx2FormatInfo formatInfo))
        {
            throw new NotSupportedException(
                $"The KTX2 vkFormat {(uint)header.VkFormat} has no supported texel format.");
        }

        if (header.TypeSize != 1)
        {
            throw new NotSupportedException(
                $"The KTX2 typeSize field is {header.TypeSize}; endian conversion of multi-byte texel data is not "
                + "supported.");
        }

        int layerCount = (int)Math.Max(1U, header.LayerCount);
        int faceCount = (int)header.FaceCount;
        long surfaceCount = 0;

        for (int level = 0; level < levelCount; level++)
        {
            surfaceCount += (long)layerCount * faceCount * Math.Max(1, baseDepth >> level);
        }

        if (surfaceCount > Ktx2Constants.MaxSurfaceCount)
        {
            throw new InvalidImageContentException(
                $"The KTX2 layout declares {surfaceCount} surfaces, which exceeds the supported maximum of "
                + $"{Ktx2Constants.MaxSurfaceCount}.");
        }

        return new Ktx2Layout(
            header,
            formatInfo,
            (int)header.Width,
            (int)header.Height,
            baseDepth,
            layerCount,
            faceCount,
            levelCount,
            (int)surfaceCount);
    }

    private static Ktx2LevelIndex[] ReadLevelIndex(Stream stream, int levelCount)
    {
        byte[] buffer = new byte[levelCount * Ktx2Constants.LevelIndexEntryLength];
        ReadExactly(stream, buffer, "the KTX2 level index");

        Ktx2LevelIndex[] indices = new Ktx2LevelIndex[levelCount];
        for (int i = 0; i < levelCount; i++)
        {
            indices[i] = Ktx2LevelIndex.Parse(
                buffer.AsSpan(i * Ktx2Constants.LevelIndexEntryLength, Ktx2Constants.LevelIndexEntryLength));
        }

        return indices;
    }

    private static void ReadDataFormatDescriptor(Stream stream, Ktx2Header header, Ktx2FormatInfo formatInfo)
    {
        long fileLength = stream.Length;
        if (header.DfdByteLength < 4 + Ktx2DataFormatDescriptor.BlockHeaderBytes + Ktx2DataFormatDescriptor.SampleBytes
            || header.DfdByteLength > Ktx2Constants.MaxDataFormatDescriptorLength
            || header.DfdByteOffset < Ktx2Constants.HeaderLength
            || (long)header.DfdByteOffset + header.DfdByteLength > fileLength)
        {
            throw new InvalidImageContentException(
                $"The KTX2 data format descriptor at offset {header.DfdByteOffset} with a length of "
                + $"{header.DfdByteLength} bytes does not fit in the {fileLength} byte file.");
        }

        stream.Position = header.DfdByteOffset;
        byte[] dfd = new byte[header.DfdByteLength];
        ReadExactly(stream, dfd, "the data format descriptor");
        Ktx2DataFormatDescriptor.Validate(dfd, formatInfo, header.VkFormat);
    }

    private static byte[] ReadKeyValueData(Stream stream, Ktx2Header header)
    {
        if (header.KvdByteLength == 0)
        {
            if (header.KvdByteOffset != 0)
            {
                throw new InvalidImageContentException(
                    $"The KTX2 file declares kvdByteOffset {header.KvdByteOffset} with a kvdByteLength of zero; the "
                    + "offset must be zero when no key/value data is present.");
            }

            return [];
        }

        long fileLength = stream.Length;
        if (header.KvdByteLength > Ktx2Constants.MaxKeyValueDataLength
            || header.KvdByteOffset < Ktx2Constants.HeaderLength
            || (long)header.KvdByteOffset + header.KvdByteLength > fileLength)
        {
            throw new InvalidImageContentException(
                $"The KTX2 key/value data at offset {header.KvdByteOffset} with a length of {header.KvdByteLength} "
                + $"bytes does not fit in the {fileLength} byte file.");
        }

        stream.Position = header.KvdByteOffset;
        byte[] data = new byte[header.KvdByteLength];
        ReadExactly(stream, data, "the key/value data block");
        return data;
    }

    private static void ValidateLevelIndices(
        Stream stream,
        Ktx2Layout layout,
        Ktx2LevelIndex[] indices,
        TexelOperations operations)
    {
        long fileLength = stream.Length;
        long totalPayload = 0;

        for (int level = 0; level < indices.Length; level++)
        {
            int width = layout.GetLevelWidth(level);
            int height = layout.GetLevelHeight(level);
            int depth = layout.GetLevelDepth(level);
            long expected = (long)operations.GetEncodedByteCount(width, height)
                            * layout.LayerCount * layout.FaceCount * depth;

            Ktx2LevelIndex index = indices[level];
            if (index.ByteLength != (ulong)expected || index.UncompressedByteLength != (ulong)expected)
            {
                throw new InvalidImageContentException(
                    $"The KTX2 level index entry {level} declares {index.ByteLength} stored and "
                    + $"{index.UncompressedByteLength} uncompressed bytes but the "
                    + $"{width}x{height}x{depth} level requires {expected} bytes.");
            }

            if (index.ByteOffset < Ktx2Constants.HeaderLength
                || index.ByteOffset > (ulong)fileLength
                || index.ByteLength > (ulong)fileLength - index.ByteOffset)
            {
                throw new InvalidImageContentException(
                    $"The KTX2 level index entry {level} addresses {index.ByteLength} bytes at offset "
                    + $"{index.ByteOffset} of a {fileLength} byte file.");
            }

            totalPayload += expected;
            if (totalPayload > Ktx2Constants.MaxTotalPayloadBytes)
            {
                throw new InvalidImageContentException(
                    $"The KTX2 layout requires more than {Ktx2Constants.MaxTotalPayloadBytes} bytes of payload.");
            }
        }
    }

    private static TextureMetadata CreateMetadata(Ktx2Layout layout, byte[] keyValueData)
    {
        Ktx2Header header = layout.Header;
        TextureMetadata metadata = new() { DecodedTextureFormat = Ktx2Format.Instance };
        Ktx2TextureMetadata formatMetadata = metadata.GetFormatMetadata(Ktx2Format.Instance);

        formatMetadata.VkFormat = header.VkFormat;
        formatMetadata.TypeSize = header.TypeSize;
        formatMetadata.LayerCount = header.LayerCount;
        formatMetadata.FaceCount = header.FaceCount;
        formatMetadata.LevelCount = header.LevelCount;
        formatMetadata.SupercompressionScheme = header.SupercompressionScheme;
        formatMetadata.KeyValueData = keyValueData;
        formatMetadata.KeyValuePairs = keyValueData.Length == 0
            ? []
            : KtxKeyValueData.Parse(keyValueData, false);

        return metadata;
    }

    private static void ReadSurface(Stream stream, TextureMipLevel mipLevel, TexelFormat texelFormat, int surfaceBytes)
    {
        TextureSurface surface = mipLevel.CreateSurface(texelFormat);
        Span<byte> payload = surface.GetPayload();

        if (payload.Length != surfaceBytes)
        {
            throw new InvalidOperationException(
                $"The '{texelFormat}' texel operations report {payload.Length} bytes for the surface but the KTX2 "
                + $"mapping requires {surfaceBytes} bytes.");
        }

        ReadExactly(stream, payload, "a texture surface");
    }

    private static void ReadExactly(Stream stream, Span<byte> destination, string description)
    {
        try
        {
            stream.ReadExactly(destination);
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidImageContentException($"The KTX2 file ended while reading {description}.", ex);
        }
    }

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

    private static string GetSupercompressionName(uint scheme)
        => scheme switch
        {
            1 => "BasisLZ",
            2 => "Zstandard",
            3 => "ZLIB",
            _ => "unknown"
        };

    /// <summary>
    ///     The validated layout of a KTX2 file.
    /// </summary>
    private sealed class Ktx2Layout
    {
        internal Ktx2Layout(
            Ktx2Header header,
            Ktx2FormatInfo formatInfo,
            int width,
            int height,
            int baseDepth,
            int layerCount,
            int faceCount,
            int levelCount,
            int surfaceCount)
        {
            this.Header = header;
            this.FormatInfo = formatInfo;
            this.Width = width;
            this.Height = height;
            this.BaseDepth = baseDepth;
            this.LayerCount = layerCount;
            this.FaceCount = faceCount;
            this.LevelCount = levelCount;
            this.SurfaceCount = surfaceCount;
        }

        internal Ktx2Header Header { get; }

        internal Ktx2FormatInfo FormatInfo { get; }

        internal TexelFormat TexelFormat => this.FormatInfo.TexelFormat;

        internal int Width { get; }

        internal int Height { get; }

        internal int BaseDepth { get; }

        internal int LayerCount { get; }

        internal int FaceCount { get; }

        internal int LevelCount { get; }

        internal int SurfaceCount { get; }

        internal int GetLevelWidth(int level) => Math.Max(1, this.Width >> level);

        internal int GetLevelHeight(int level) => Math.Max(1, this.Height >> level);

        internal int GetLevelDepth(int level) => Math.Max(1, this.BaseDepth >> level);
    }
}
