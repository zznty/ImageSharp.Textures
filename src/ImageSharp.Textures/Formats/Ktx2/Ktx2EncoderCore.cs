using System.Buffers;
using System.Buffers.Binary;

namespace ImageSharp.Textures.Formats.Ktx2;

/// <summary>
///     Writes the KTX2 container around a texture's surfaces.
/// </summary>
internal sealed class Ktx2EncoderCore
{
    private readonly Ktx2EncodingOptions options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Ktx2EncoderCore" /> class.
    /// </summary>
    /// <param name="options">The encoder options.</param>
    internal Ktx2EncoderCore(Ktx2EncodingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options;
    }

    /// <summary>
    ///     Encodes a texture as a KTX2 file.
    /// </summary>
    /// <param name="texture">The texture to encode.</param>
    /// <param name="stream">The destination stream.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    internal void Encode(Texture texture, Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(stream);

        TextureEncodingLayout layout = TextureEncodingLayout.Create(
            texture,
            this.options.TargetTexelFormat,
            this.options.LayerCount,
            this.options.FaceCount);

        Ktx2FormatInfo formatInfo = Ktx2TexelFormats.GetFormatInfo(layout.TexelFormat);
        byte[] dfd = Ktx2DataFormatDescriptor.Create(formatInfo);
        byte[] keyValueData = this.GetKeyValueData(texture);

        int levelCount = layout.MipLevelCount;
        int requiredAlignment = GetRequiredAlignment(formatInfo.BlockBytes);

        long dfdOffset = Ktx2Constants.HeaderLength + ((long)levelCount * Ktx2Constants.LevelIndexEntryLength);
        long kvdOffset = keyValueData.Length > 0 ? dfdOffset + dfd.Length : 0;
        long dataOffset = dfdOffset + dfd.Length + keyValueData.Length;
        long dataStart = Align(dataOffset, requiredAlignment);

        long[] levelOffsets = new long[levelCount];
        long[] levelLengths = new long[levelCount];
        long cursor = dataStart;

        // KTX2 writes the mip level images from the smallest level to the base level.
        for (int level = levelCount - 1; level >= 0; level--)
        {
            levelOffsets[level] = cursor;
            levelLengths[level] = GetLevelLength(layout, level);
            cursor += levelLengths[level];

            if (level > 0)
            {
                cursor = Align(cursor, requiredAlignment);
            }
        }

        WriteHeader(stream, layout, dfdOffset, dfd.Length, kvdOffset, keyValueData.Length);
        WriteLevelIndex(stream, levelOffsets, levelLengths);

        stream.Write(dfd);
        stream.Write(keyValueData);
        WriteZeros(stream, (int)(dataStart - dataOffset));

        for (int level = levelCount - 1; level >= 0; level--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteLevel(stream, texture, layout, level);

            if (level > 0)
            {
                long levelEnd = levelOffsets[level] + levelLengths[level];
                WriteZeros(stream, (int)(levelOffsets[level - 1] - levelEnd));
            }
        }
    }

    private static void WriteHeader(
        Stream stream,
        TextureEncodingLayout layout,
        long dfdOffset,
        int dfdLength,
        long kvdOffset,
        int kvdLength)
    {
        stream.Write(Ktx2Constants.Identifier);

        Span<byte> buffer = stackalloc byte[8];
        WriteUInt32(stream, buffer, (uint)Ktx2TexelFormats.GetVkFormat(layout.TexelFormat));
        WriteUInt32(stream, buffer, 1);
        WriteUInt32(stream, buffer, (uint)layout.Width);
        WriteUInt32(stream, buffer, (uint)layout.Height);
        WriteUInt32(stream, buffer, layout.BaseDepth > 1 ? (uint)layout.BaseDepth : 0);
        WriteUInt32(stream, buffer, layout.LayerCount > 1 ? (uint)layout.LayerCount : 0);
        WriteUInt32(stream, buffer, (uint)layout.FaceCount);
        WriteUInt32(stream, buffer, (uint)layout.MipLevelCount);
        WriteUInt32(stream, buffer, 0);
        WriteUInt32(stream, buffer, (uint)dfdOffset);
        WriteUInt32(stream, buffer, (uint)dfdLength);
        WriteUInt32(stream, buffer, (uint)kvdOffset);
        WriteUInt32(stream, buffer, (uint)kvdLength);
        WriteUInt64(stream, buffer, 0);
        WriteUInt64(stream, buffer, 0);
    }

    private static void WriteLevelIndex(Stream stream, long[] offsets, long[] lengths)
    {
        Span<byte> buffer = stackalloc byte[8];

        for (int level = 0; level < offsets.Length; level++)
        {
            WriteUInt64(stream, buffer, (ulong)offsets[level]);
            WriteUInt64(stream, buffer, (ulong)lengths[level]);
            WriteUInt64(stream, buffer, (ulong)lengths[level]);
        }
    }

    private static void WriteLevel(Stream stream, Texture texture, TextureEncodingLayout layout, int level)
    {
        int width = layout.GetLevelWidth(level);
        int height = layout.GetLevelHeight(level);
        int surfaceBytes = layout.Operations.GetEncodedByteCount(width, height);

        TextureMipLevel mipLevel = texture.MipLevels[level];
        IMemoryOwner<byte>? scratch = null;

        try
        {
            // KTX2 stores every level contiguously in layer, face, slice order, which is the surface
            // collection order.
            foreach (TextureSurface surface in mipLevel.Surfaces)
            {
                if (surface.TexelFormat == layout.TexelFormat)
                {
                    stream.Write(surface.GetPayload());
                }
                else
                {
                    scratch ??=
                        texture.Configuration.ImageSharpConfiguration.MemoryAllocator.Allocate<byte>(surfaceBytes);
                    Span<byte> destination = scratch.Memory.Span[..surfaceBytes];
                    TextureSurfaceTranscoder.Write(surface, layout.TexelFormat, destination);
                    stream.Write(destination);
                }
            }
        }
        finally
        {
            scratch?.Dispose();
        }
    }

    private static long GetLevelLength(TextureEncodingLayout layout, int level)
    {
        int width = layout.GetLevelWidth(level);
        int height = layout.GetLevelHeight(level);
        int depth = layout.GetLevelDepth(level);

        return (long)layout.Operations.GetEncodedByteCount(width, height)
               * layout.LayerCount * layout.FaceCount * depth;
    }

    private static int GetRequiredAlignment(int blockBytes)
    {
        int gcd = Gcd(blockBytes, 4);
        return blockBytes / gcd * 4;
    }

    private static int Gcd(int left, int right)
    {
        while (right != 0)
        {
            int remainder = left % right;
            left = right;
            right = remainder;
        }

        return left;
    }

    private static void WriteUInt32(Stream stream, Span<byte> buffer, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        stream.Write(buffer[..4]);
    }

    private static void WriteUInt64(Stream stream, Span<byte> buffer, ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        stream.Write(buffer[..8]);
    }

    private static void WriteZeros(Stream stream, int count)
    {
        if (count == 0)
        {
            return;
        }

        Span<byte> zeros = stackalloc byte[32];

        while (count > 0)
        {
            int chunk = Math.Min(count, zeros.Length);
            stream.Write(zeros[..chunk]);
            count -= chunk;
        }
    }

    private static long Align(long value, int alignment) => (value + alignment - 1) / alignment * alignment;

    private byte[] GetKeyValueData(Texture texture)
    {
        if (!this.options.WriteKeyValueData)
        {
            return [];
        }

        if (!texture.Metadata.TryGetFormatMetadata(Ktx2Format.Instance, out Ktx2TextureMetadata? metadata))
        {
            return [];
        }

        byte[] keyValueData = metadata!.KeyValueData;
        if (keyValueData.Length % 4 != 0)
        {
            throw new InvalidOperationException(
                $"The KTX2 key/value data block is {keyValueData.Length} bytes; a KTX2 block must be a multiple of "
                + "four bytes.");
        }

        return keyValueData;
    }
}
