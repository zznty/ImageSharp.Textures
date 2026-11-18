using System.Buffers.Binary;
using ImageSharp.Textures.Formats.Ktx2;

namespace ImageSharp.Textures.Tests.TestUtilities.Ktx2;

/// <summary>
///     Builds KTX2 files for the container tests. Every header field is settable so malformed files can be
///     produced without a binary fixture.
/// </summary>
internal sealed class Ktx2FileBuilder
{
    private readonly int blockBytes;
    private readonly List<byte> payload = [];
    private byte patternSeed;

    private bool usePattern;

    private Ktx2FileBuilder(int blockBytes) => this.blockBytes = blockBytes;

    /// <summary>Gets or sets the surface width, in texels.</summary>
    internal int Width { get; set; }

    /// <summary>Gets or sets the surface height, in texels.</summary>
    internal int Height { get; set; }

    /// <summary>Gets or sets the volume depth; zero for flat, array and cubemap textures.</summary>
    internal int Depth { get; set; }

    /// <summary>Gets or sets the declared array layer count.</summary>
    internal int LayerCount { get; set; }

    /// <summary>Gets or sets the declared face count.</summary>
    internal int FaceCount { get; set; } = 1;

    /// <summary>Gets or sets the declared level count.</summary>
    internal int LevelCount { get; set; } = 1;

    /// <summary>Gets or sets the <c>vkFormat</c> field.</summary>
    internal uint VkFormat { get; set; }

    /// <summary>Gets or sets the <c>typeSize</c> field.</summary>
    internal uint TypeSize { get; set; } = 1;

    /// <summary>Gets or sets the <c>supercompressionScheme</c> field.</summary>
    internal uint SupercompressionScheme { get; set; }

    /// <summary>Gets or sets the <c>sgdByteLength</c> field.</summary>
    internal ulong SgdByteLength { get; set; }

    /// <summary>Gets or sets the data format descriptor bytes, including the leading total size word.</summary>
    internal byte[] DataFormatDescriptor { get; set; } = [];

    /// <summary>Gets or sets the raw key/value data block.</summary>
    internal byte[] KeyValueData { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether the file identifier is written.</summary>
    internal bool IncludeIdentifier { get; set; } = true;

    /// <summary>
    ///     Creates a builder for a file with the given Vulkan format.
    /// </summary>
    /// <param name="vkFormat">The Vulkan format value.</param>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    /// <param name="blockBytes">The texel block size, in bytes.</param>
    /// <returns>The builder.</returns>
    internal static Ktx2FileBuilder Create(uint vkFormat, int width, int height, int blockBytes)
        => new(blockBytes) { Width = width, Height = height, VkFormat = vkFormat };

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
    internal Ktx2FileBuilder AddPatternPayloads(byte seed = 0)
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
    internal Ktx2FileBuilder AddPayload(params byte[] bytes)
    {
        this.payload.AddRange(bytes);
        return this;
    }

    /// <summary>
    ///     Removes payload bytes from the end of the file.
    /// </summary>
    /// <param name="count">The number of bytes to remove.</param>
    /// <returns>The builder.</returns>
    internal Ktx2FileBuilder TruncatePayload(int count)
    {
        this.payload.RemoveRange(this.payload.Count - count, count);
        return this;
    }

    /// <summary>
    ///     Builds the level index that the current layout requires.
    /// </summary>
    /// <returns>The level index entries, ordered from the base level.</returns>
    internal Ktx2LevelIndexEntry[] BuildLevelIndex()
    {
        int levels = Math.Max(1, this.LevelCount);
        int alignment = GetRequiredAlignment(this.blockBytes);
        int dfdOffset = Ktx2Constants.HeaderLength + (levels * Ktx2Constants.LevelIndexEntryLength);
        long dataOffset = dfdOffset + this.DataFormatDescriptor.Length + this.KeyValueData.Length;
        long cursor = Align(dataOffset, alignment);

        Ktx2LevelIndexEntry[] entries = new Ktx2LevelIndexEntry[levels];

        for (int level = levels - 1; level >= 0; level--)
        {
            long length = this.GetLevelSize(level);
            entries[level] = new Ktx2LevelIndexEntry(cursor, length, length);
            cursor += length;

            if (level > 0)
            {
                cursor = Align(cursor, alignment);
            }
        }

        return entries;
    }

    /// <summary>
    ///     Serializes the file.
    /// </summary>
    /// <returns>The encoded KTX2 file.</returns>
    internal byte[] Build()
    {
        List<byte> file = [];
        int levels = Math.Max(1, this.LevelCount);
        int layers = Math.Max(1, this.LayerCount);
        int alignment = GetRequiredAlignment(this.blockBytes);
        int dfdOffset = Ktx2Constants.HeaderLength + (levels * Ktx2Constants.LevelIndexEntryLength);
        int kvdOffset = this.KeyValueData.Length > 0 ? dfdOffset + this.DataFormatDescriptor.Length : 0;
        long dataOffset = dfdOffset + this.DataFormatDescriptor.Length + this.KeyValueData.Length;
        long dataStart = Align(dataOffset, alignment);
        Ktx2LevelIndexEntry[] entries = this.BuildLevelIndex();

        if (this.IncludeIdentifier)
        {
            file.AddRange(Ktx2Constants.Identifier.ToArray());
        }

        WriteUInt32(file, this.VkFormat);
        WriteUInt32(file, this.TypeSize);
        WriteUInt32(file, (uint)this.Width);
        WriteUInt32(file, (uint)this.Height);
        WriteUInt32(file, (uint)this.Depth);
        WriteUInt32(file, (uint)this.LayerCount);
        WriteUInt32(file, (uint)this.FaceCount);
        WriteUInt32(file, (uint)this.LevelCount);
        WriteUInt32(file, this.SupercompressionScheme);
        WriteUInt32(file, (uint)dfdOffset);
        WriteUInt32(file, (uint)this.DataFormatDescriptor.Length);
        WriteUInt32(file, (uint)kvdOffset);
        WriteUInt32(file, (uint)this.KeyValueData.Length);
        WriteUInt64(file, 0);
        WriteUInt64(file, this.SgdByteLength);

        foreach (Ktx2LevelIndexEntry entry in entries)
        {
            WriteUInt64(file, (ulong)entry.ByteOffset);
            WriteUInt64(file, (ulong)entry.ByteLength);
            WriteUInt64(file, (ulong)entry.UncompressedByteLength);
        }

        file.AddRange(this.DataFormatDescriptor);
        file.AddRange(this.KeyValueData);
        AddZeros(file, (int)(dataStart - dataOffset));

        int payloadOffset = 0;
        int surfaceIndex = 0;

        for (int level = levels - 1; level >= 0; level--)
        {
            int width = Math.Max(1, this.Width >> level);
            int height = Math.Max(1, this.Height >> level);
            int depth = Math.Max(1, this.Depth >> level);
            int surfaceSize = this.GetSurfaceSize(width, height);
            int surfaces = layers * this.FaceCount * depth;

            for (int i = 0; i < surfaces; i++)
            {
                this.WriteSurfaceData(file, surfaceSize, ref surfaceIndex, ref payloadOffset);
            }

            if (level > 0)
            {
                long levelEnd = entries[level].ByteOffset + entries[level].ByteLength;
                AddZeros(file, (int)(entries[level - 1].ByteOffset - levelEnd));
            }
        }

        return file.ToArray();
    }

    private long GetLevelSize(int level)
    {
        int width = Math.Max(1, this.Width >> level);
        int height = Math.Max(1, this.Height >> level);
        int depth = Math.Max(1, this.Depth >> level);

        return (long)this.GetSurfaceSize(width, height)
               * Math.Max(1, this.LayerCount) * this.FaceCount * depth;
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

    private static void WriteUInt32(List<byte> file, uint value)
    {
        byte[] buffer = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        file.AddRange(buffer);
    }

    private static void WriteUInt64(List<byte> file, ulong value)
    {
        byte[] buffer = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        file.AddRange(buffer);
    }

    private static void AddZeros(List<byte> file, int count)
    {
        for (int i = 0; i < count; i++)
        {
            file.Add(0);
        }
    }

    private static int GetRequiredAlignment(int blockBytes)
    {
        if (blockBytes == 0)
        {
            return 4;
        }

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

    private static long Align(long value, int alignment) => (value + alignment - 1) / alignment * alignment;
}
