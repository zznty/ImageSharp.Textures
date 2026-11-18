using System.Buffers;
using System.Buffers.Binary;

namespace ImageSharp.Textures.Formats.Ktx;

/// <summary>
///     Writes the KTX v1 container around a texture's surfaces.
/// </summary>
internal sealed class KtxEncoderCore
{
    private readonly KtxEncodingOptions options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="KtxEncoderCore" /> class.
    /// </summary>
    /// <param name="options">The encoder options.</param>
    internal KtxEncoderCore(KtxEncodingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options;
    }

    /// <summary>
    ///     Encodes a texture as a KTX v1 file.
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

        KtxInternalFormat internalFormat = KtxTexelFormats.GetInternalFormat(layout.TexelFormat);
        byte[] keyValueData = this.GetKeyValueData(texture);

        WriteHeader(stream, layout, internalFormat, keyValueData.Length);
        stream.Write(keyValueData);

        for (int level = 0; level < layout.MipLevelCount; level++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteLevel(stream, texture, layout, level);
        }
    }

    private static void WriteHeader(
        Stream stream,
        TextureEncodingLayout layout,
        KtxInternalFormat internalFormat,
        int keyValueDataLength)
    {
        stream.Write(KtxConstants.Identifier);

        Span<byte> buffer = stackalloc byte[4];
        WriteUInt32(stream, buffer, (uint)KtxEndianness.LittleEndian);
        WriteUInt32(stream, buffer, internalFormat.GlType);
        WriteUInt32(stream, buffer, internalFormat.GlTypeSize);
        WriteUInt32(stream, buffer, internalFormat.GlFormat);
        WriteUInt32(stream, buffer, internalFormat.GlInternalFormat);
        WriteUInt32(stream, buffer, internalFormat.GlBaseInternalFormat);
        WriteUInt32(stream, buffer, (uint)layout.Width);
        WriteUInt32(stream, buffer, (uint)layout.Height);

        // KTX v1 stores zero for a single depth or array element; the reader substitutes one.
        WriteUInt32(stream, buffer, layout.BaseDepth > 1 ? (uint)layout.BaseDepth : 0);
        WriteUInt32(stream, buffer, layout.LayerCount > 1 ? (uint)layout.LayerCount : 0);
        WriteUInt32(stream, buffer, (uint)layout.FaceCount);
        WriteUInt32(stream, buffer, (uint)layout.MipLevelCount);
        WriteUInt32(stream, buffer, (uint)keyValueDataLength);
    }

    private static void WriteLevel(Stream stream, Texture texture, TextureEncodingLayout layout, int level)
    {
        int width = layout.GetLevelWidth(level);
        int height = layout.GetLevelHeight(level);
        int depth = layout.GetLevelDepth(level);
        int surfaceBytes = layout.Operations.GetEncodedByteCount(width, height);

        // A non-array cubemap is the one KTX v1 layout whose imageSize counts a single face; every other
        // texture counts the whole level including every layer, face and depth slice.
        long imageSize = layout.IsNonArrayCubeMap
            ? (long)surfaceBytes * depth
            : (long)surfaceBytes * layout.LayerCount * layout.FaceCount * depth;

        if (imageSize > uint.MaxValue)
        {
            throw new InvalidOperationException(
                $"Mip level {level} requires {imageSize} bytes, which does not fit the KTX v1 imageSize field.");
        }

        Span<byte> buffer = stackalloc byte[4];
        WriteUInt32(stream, buffer, (uint)imageSize);

        TextureMipLevel mipLevel = texture.MipLevels[level];
        IMemoryOwner<byte>? scratch = null;
        int surfaceIndex = 0;

        try
        {
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

                surfaceIndex++;

                if (layout.IsNonArrayCubeMap && surfaceIndex % depth == 0)
                {
                    WriteZeros(stream, Padding(imageSize));
                }
            }
        }
        finally
        {
            scratch?.Dispose();
        }

        long levelBytes = layout.IsNonArrayCubeMap
            ? Align4(imageSize) * layout.FaceCount
            : imageSize;
        WriteZeros(stream, Padding(levelBytes));
    }

    private static void WriteUInt32(Stream stream, Span<byte> buffer, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteZeros(Stream stream, int count)
    {
        if (count == 0)
        {
            return;
        }

        Span<byte> zeros = stackalloc byte[4];
        stream.Write(zeros[..count]);
    }

    private static int Padding(long length) => (int)((4 - (length % 4)) % 4);

    private static long Align4(long length) => length + Padding(length);

    private byte[] GetKeyValueData(Texture texture)
    {
        if (!this.options.WriteKeyValueData)
        {
            return [];
        }

        if (!texture.Metadata.TryGetFormatMetadata(KtxFormat.Instance, out KtxTextureMetadata? metadata))
        {
            return [];
        }

        byte[] keyValueData = metadata!.KeyValueData;
        if (keyValueData.Length % 4 != 0)
        {
            throw new InvalidOperationException(
                $"The KTX key/value data block is {keyValueData.Length} bytes; a KTX block must be a multiple of "
                + "four bytes.");
        }

        return keyValueData;
    }
}
