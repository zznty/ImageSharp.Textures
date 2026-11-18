using System.Buffers.Binary;
using System.Text;
using ImageSharp.Textures.Formats.Ktx;

namespace ImageSharp.Textures.Tests.TestUtilities.Ktx;

/// <summary>
///     Builds KTX v1 files for the container tests. Every header field is settable so malformed files can be
///     produced without a binary fixture.
/// </summary>
internal sealed class KtxFileBuilder
{
    private readonly int blockBytes;
    private readonly List<byte> payload = [];
    private byte patternSeed;

    private bool usePattern;

    private KtxFileBuilder(int blockBytes) => this.blockBytes = blockBytes;

    /// <summary>Gets or sets the surface width, in texels.</summary>
    internal int Width { get; set; }

    /// <summary>Gets or sets the surface height, in texels.</summary>
    internal int Height { get; set; }

    /// <summary>Gets or sets the volume depth; zero for flat, array and cubemap textures.</summary>
    internal int Depth { get; set; }

    /// <summary>Gets or sets the declared array element count.</summary>
    internal int ArrayElementCount { get; set; }

    /// <summary>Gets or sets the declared face count.</summary>
    internal int FaceCount { get; set; } = 1;

    /// <summary>Gets or sets the declared mip level count.</summary>
    internal int MipLevelCount { get; set; } = 1;

    /// <summary>Gets or sets the <c>glType</c> field.</summary>
    internal uint GlType { get; set; }

    /// <summary>Gets or sets the <c>glTypeSize</c> field.</summary>
    internal uint GlTypeSize { get; set; } = 1;

    /// <summary>Gets or sets the <c>glFormat</c> field.</summary>
    internal uint GlFormat { get; set; }

    /// <summary>Gets or sets the <c>glInternalFormat</c> field.</summary>
    internal uint GlInternalFormat { get; set; }

    /// <summary>Gets or sets the <c>glBaseInternalFormat</c> field.</summary>
    internal uint GlBaseInternalFormat { get; set; } = 0x1908;

    /// <summary>Gets or sets a value indicating whether the header fields are big-endian.</summary>
    internal bool BigEndian { get; set; }

    /// <summary>Gets or sets an explicit endianness word, written little-endian, for malformed file tests.</summary>
    internal uint? EndiannessWord { get; set; }

    /// <summary>Gets or sets a value indicating whether the file identifier is written.</summary>
    internal bool IncludeIdentifier { get; set; } = true;

    /// <summary>Gets or sets the raw key/value data block.</summary>
    internal byte[] KeyValueData { get; set; } = [];

    private bool IsNonArrayCubeMap => this.FaceCount == 6 && Math.Max(1, this.ArrayElementCount) == 1;

    /// <summary>
    ///     Creates a builder for an uncompressed R8G8B8A8_UNorm file.
    /// </summary>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    /// <returns>The builder.</returns>
    internal static KtxFileBuilder CreateRgba8(int width, int height)
        => new(0)
        {
            Width = width,
            Height = height,
            GlType = 0x1401,
            GlTypeSize = 1,
            GlFormat = 0x1908,
            GlInternalFormat = 0x8058,
            GlBaseInternalFormat = 0x1908
        };

    /// <summary>
    ///     Creates a builder for a compressed file.
    /// </summary>
    /// <param name="glInternalFormat">The <c>glInternalFormat</c> value.</param>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    /// <param name="blockBytes">The compressed block size, in bytes.</param>
    /// <returns>The builder.</returns>
    internal static KtxFileBuilder CreateCompressed(uint glInternalFormat, int width, int height, int blockBytes)
        => new(blockBytes)
        {
            Width = width,
            Height = height,
            GlType = 0,
            GlTypeSize = 1,
            GlFormat = 0,
            GlInternalFormat = glInternalFormat,
            GlBaseInternalFormat = 0x1908
        };

    /// <summary>
    ///     Gets the payload size of one surface.
    /// </summary>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    /// <returns>The payload size, in bytes.</returns>
    internal int GetSurfaceSize(int width, int height)
        => this.blockBytes == 0
            ? width * height * 4
            : (width + 3) / 4 * ((height + 3) / 4) * this.blockBytes;

    /// <summary>
    ///     Appends one payload per surface, filled with a deterministic value derived from the surface index so
    ///     tests can verify the file order.
    /// </summary>
    /// <param name="seed">The seed added to every surface index.</param>
    /// <returns>The builder.</returns>
    internal KtxFileBuilder AddPatternPayloads(byte seed = 0)
    {
        this.usePattern = true;
        this.patternSeed = seed;
        return this;
    }

    /// <summary>
    ///     Appends raw payload bytes.
    /// </summary>
    /// <param name="bytes">The bytes to append.</param>
    /// <returns>The builder.</returns>
    internal KtxFileBuilder AddPayload(params byte[] bytes)
    {
        this.payload.AddRange(bytes);
        return this;
    }

    /// <summary>
    ///     Removes payload bytes from the end of the file.
    /// </summary>
    /// <param name="count">The number of bytes to remove.</param>
    /// <returns>The builder.</returns>
    internal KtxFileBuilder TruncatePayload(int count)
    {
        this.payload.RemoveRange(this.payload.Count - count, count);
        return this;
    }

    /// <summary>
    ///     Serializes the file.
    /// </summary>
    /// <returns>The encoded KTX file.</returns>
    internal byte[] Build()
    {
        List<byte> file = new();
        int payloadOffset = 0;
        int surfaceIndex = 0;

        if (this.IncludeIdentifier)
        {
            file.AddRange(KtxConstants.Identifier.ToArray());
        }

        this.WriteEndiannessWord(file);
        this.WriteUInt32(file, this.GlType);
        this.WriteUInt32(file, this.GlTypeSize);
        this.WriteUInt32(file, this.GlFormat);
        this.WriteUInt32(file, this.GlInternalFormat);
        this.WriteUInt32(file, this.GlBaseInternalFormat);
        this.WriteUInt32(file, (uint)this.Width);
        this.WriteUInt32(file, (uint)this.Height);
        this.WriteUInt32(file, (uint)this.Depth);
        this.WriteUInt32(file, (uint)this.ArrayElementCount);
        this.WriteUInt32(file, (uint)this.FaceCount);
        this.WriteUInt32(file, (uint)this.MipLevelCount);
        this.WriteUInt32(file, (uint)this.KeyValueData.Length);
        file.AddRange(this.KeyValueData);

        int levels = Math.Max(1, this.MipLevelCount);

        for (int level = 0; level < levels; level++)
        {
            int width = Math.Max(1, this.Width >> level);
            int height = Math.Max(1, this.Height >> level);
            int depth = Math.Max(1, this.Depth >> level);
            int surfaceSize = this.GetSurfaceSize(width, height);
            long faceImageSize = (long)surfaceSize * depth;
            long imageSize = this.IsNonArrayCubeMap
                ? faceImageSize
                : faceImageSize * Math.Max(1, this.ArrayElementCount) * this.FaceCount;

            this.WriteUInt32(file, (uint)imageSize);

            if (this.IsNonArrayCubeMap)
            {
                for (int face = 0; face < this.FaceCount; face++)
                {
                    for (int slice = 0; slice < depth; slice++)
                    {
                        this.WriteSurfaceData(file, surfaceSize, ref surfaceIndex, ref payloadOffset);
                    }

                    AddZeros(file, (int)(Align4(faceImageSize) - faceImageSize));
                }
            }
            else
            {
                int surfaces = Math.Max(1, this.ArrayElementCount) * this.FaceCount * depth;

                for (int i = 0; i < surfaces; i++)
                {
                    this.WriteSurfaceData(file, surfaceSize, ref surfaceIndex, ref payloadOffset);
                }
            }

            long levelDataSize = this.IsNonArrayCubeMap
                ? Align4(imageSize) * this.FaceCount
                : imageSize * Math.Max(1, this.ArrayElementCount) * this.FaceCount;
            AddZeros(file, (int)((4 - (levelDataSize % 4)) % 4));
        }

        return file.ToArray();
    }

    private void WriteSurfaceData(List<byte> file, int surfaceSize, ref int surfaceIndex, ref int payloadOffset)
    {
        if (this.usePattern)
        {
            byte value = (byte)(this.patternSeed + surfaceIndex);
            for (int i = 0; i < surfaceSize; i++)
            {
                file.Add(value);
            }

            surfaceIndex++;
            return;
        }

        if (payloadOffset + surfaceSize > this.payload.Count)
        {
            throw new InvalidOperationException(
                $"The builder needs {surfaceSize} payload bytes at offset {payloadOffset} but only "
                + $"{this.payload.Count} were added.");
        }

        file.AddRange(this.payload.GetRange(payloadOffset, surfaceSize));
        payloadOffset += surfaceSize;
        surfaceIndex++;
    }

    private void WriteEndiannessWord(List<byte> file)
    {
        byte[] buffer = new byte[4];

        if (this.EndiannessWord is uint raw)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, raw);
        }
        else if (this.BigEndian)
        {
            BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)KtxEndianness.LittleEndian);
        }
        else
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, (uint)KtxEndianness.LittleEndian);
        }

        file.AddRange(buffer);
    }

    private void WriteUInt32(List<byte> file, uint value)
    {
        byte[] buffer = new byte[4];

        if (this.BigEndian)
        {
            BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        }
        else
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        }

        file.AddRange(buffer);
    }

    private static void AddZeros(List<byte> file, int count)
    {
        for (int i = 0; i < count; i++)
        {
            file.Add(0);
        }
    }

    private static long Align4(long value) => value + ((4 - (value % 4)) % 4);

    /// <summary>
    ///     Creates a raw key/value data block.
    /// </summary>
    /// <param name="bigEndian">Whether the entry lengths are stored big-endian.</param>
    /// <param name="pairs">The key/value pairs.</param>
    /// <returns>The raw block.</returns>
    internal static byte[] CreateKeyValueBlock(bool bigEndian, params (string Key, byte[] Value)[] pairs)
    {
        List<byte> block = [];

        foreach ((string key, byte[] value) in pairs)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] length = new byte[4];
            int lengthValue = keyBytes.Length + 1 + value.Length;

            if (bigEndian)
            {
                BinaryPrimitives.WriteUInt32BigEndian(length, (uint)lengthValue);
            }
            else
            {
                BinaryPrimitives.WriteUInt32LittleEndian(length, (uint)lengthValue);
            }

            block.AddRange(length);
            block.AddRange(keyBytes);
            block.Add(0);
            block.AddRange(value);
            AddZeros(block, (4 - (lengthValue % 4)) % 4);
        }

        return block.ToArray();
    }
}
