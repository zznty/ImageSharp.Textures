using System.Buffers.Binary;
using ImageSharp.Textures.Formats.Ktx2.Enums;

namespace ImageSharp.Textures.Formats.Ktx2;

/// <summary>
///     Reads and writes the KTX2 Data Format Descriptor (DFD) block that belongs to a Vulkan format.
/// </summary>
internal static class Ktx2DataFormatDescriptor
{
    /// <summary>
    ///     The word index at which the first sample descriptor starts within a basic descriptor block.
    /// </summary>
    internal const int SampleStartWord = 6;

    /// <summary>
    ///     The number of words in one sample descriptor.
    /// </summary>
    internal const int SampleWords = 4;

    /// <summary>
    ///     The size of the basic descriptor block header, in bytes.
    /// </summary>
    internal const int BlockHeaderBytes = SampleStartWord * 4;

    /// <summary>
    ///     The size of one sample descriptor, in bytes.
    /// </summary>
    internal const int SampleBytes = SampleWords * 4;

    /// <summary>
    ///     The <c>KHR_DF_VERSIONNUMBER_LATEST</c> value the codec writes.
    /// </summary>
    internal const ushort VersionNumber = 2;

    /// <summary>
    ///     The <c>KHR_DF_VENDORID_KHRONOS</c> value.
    /// </summary>
    internal const uint VendorIdKhronos = 0;

    /// <summary>
    ///     The <c>KHR_DF_KHR_DESCRIPTORTYPE_BASICFORMAT</c> value.
    /// </summary>
    internal const uint DescriptorTypeBasicFormat = 0;

    /// <summary>
    ///     The <c>KHR_DF_PRIMARIES_BT709</c> value.
    /// </summary>
    internal const byte PrimariesBt709 = 1;

    /// <summary>
    ///     The <c>KHR_DF_TRANSFER_LINEAR</c> value.
    /// </summary>
    internal const byte TransferLinear = 1;

    /// <summary>
    ///     The <c>KHR_DF_TRANSFER_SRGB</c> value.
    /// </summary>
    internal const byte TransferSrgb = 2;

    /// <summary>
    ///     Creates the data format descriptor of a format.
    /// </summary>
    /// <param name="formatInfo">The format information.</param>
    /// <returns>The descriptor bytes, including the leading <c>dfdTotalSize</c> word.</returns>
    internal static byte[] Create(Ktx2FormatInfo formatInfo)
    {
        ArgumentNullException.ThrowIfNull(formatInfo);

        int blockBytes = BlockHeaderBytes + (formatInfo.Samples.Length * SampleBytes);
        byte[] dfd = new byte[4 + blockBytes];
        Span<byte> span = dfd;

        BinaryPrimitives.WriteUInt32LittleEndian(span, (uint)dfd.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(
            span[4..],
            VendorIdKhronos | (DescriptorTypeBasicFormat << 17));
        BinaryPrimitives.WriteUInt16LittleEndian(span[8..], VersionNumber);
        BinaryPrimitives.WriteUInt16LittleEndian(span[10..], (ushort)blockBytes);
        span[12] = (byte)formatInfo.ColorModel;
        span[13] = PrimariesBt709;
        span[14] = formatInfo.IsSrgb ? TransferSrgb : TransferLinear;
        span[15] = 0;
        span[16] = (byte)(formatInfo.BlockWidth - 1);
        span[17] = (byte)(formatInfo.BlockHeight - 1);
        span[18] = (byte)(formatInfo.BlockDepth - 1);
        span[19] = 0;
        span[20] = (byte)formatInfo.BlockBytes;
        span[21] = 0;
        span[22] = 0;
        span[23] = 0;
        span[24] = 0;
        span[25] = 0;
        span[26] = 0;
        span[27] = 0;

        int sampleOffset = 4 + BlockHeaderBytes;
        foreach (Ktx2SampleInfo sample in formatInfo.Samples)
        {
            uint word = sample.BitOffset
                        | ((uint)(sample.BitLength - 1) << 16)
                        | ((uint)sample.ChannelId << 24);

            BinaryPrimitives.WriteUInt32LittleEndian(span[sampleOffset..], word);
            BinaryPrimitives.WriteUInt32LittleEndian(span[(sampleOffset + 4)..], 0);
            BinaryPrimitives.WriteUInt32LittleEndian(span[(sampleOffset + 8)..], sample.Lower);
            BinaryPrimitives.WriteUInt32LittleEndian(span[(sampleOffset + 12)..], sample.Upper);
            sampleOffset += SampleBytes;
        }

        return dfd;
    }

    /// <summary>
    ///     Validates a data format descriptor against the Vulkan format it describes.
    /// </summary>
    /// <param name="dfd">The descriptor bytes.</param>
    /// <param name="formatInfo">The expected format information.</param>
    /// <param name="vkFormat">The declared Vulkan format.</param>
    /// <exception cref="InvalidImageContentException">The descriptor does not describe the declared format.</exception>
    internal static void Validate(ReadOnlySpan<byte> dfd, Ktx2FormatInfo formatInfo, VkFormat vkFormat)
    {
        ArgumentNullException.ThrowIfNull(formatInfo);

        if (dfd.Length < 4 + BlockHeaderBytes + SampleBytes)
        {
            throw new InvalidImageContentException(
                $"The KTX2 data format descriptor is {dfd.Length} bytes, which is shorter than the smallest valid "
                + $"descriptor ({4 + BlockHeaderBytes + SampleBytes} bytes).");
        }

        uint totalSize = BinaryPrimitives.ReadUInt32LittleEndian(dfd);
        if (totalSize != dfd.Length)
        {
            throw new InvalidImageContentException(
                $"The KTX2 data format descriptor declares a total size of {totalSize} bytes but "
                + $"{dfd.Length} bytes were read.");
        }

        uint vendorAndType = BinaryPrimitives.ReadUInt32LittleEndian(dfd[4..]);
        uint vendorId = vendorAndType & 0x1FFFFU;
        uint descriptorType = vendorAndType >> 17;
        if (vendorId != VendorIdKhronos || descriptorType != DescriptorTypeBasicFormat)
        {
            throw new InvalidImageContentException(
                $"The KTX2 data format descriptor declares vendor {vendorId} and descriptor type {descriptorType}; "
                + "the basic Khronos format block (vendor 0, type 0) is required.");
        }

        ushort version = BinaryPrimitives.ReadUInt16LittleEndian(dfd[8..]);
        if (version != VersionNumber)
        {
            throw new InvalidImageContentException(
                $"The KTX2 data format descriptor declares version {version}; version {VersionNumber} is required.");
        }

        ushort blockSize = BinaryPrimitives.ReadUInt16LittleEndian(dfd[10..]);
        if (blockSize < BlockHeaderBytes + SampleBytes
            || (blockSize - BlockHeaderBytes) % SampleBytes != 0
            || 4 + blockSize > dfd.Length)
        {
            throw new InvalidImageContentException(
                $"The KTX2 data format descriptor declares a descriptor block size of {blockSize} bytes, which is "
                + "inconsistent with the descriptor layout.");
        }

        int sampleCount = (blockSize - BlockHeaderBytes) / SampleBytes;
        if (sampleCount != formatInfo.Samples.Length)
        {
            throw new InvalidImageContentException(
                $"The KTX2 data format descriptor for {vkFormat} declares {sampleCount} samples but "
                + $"{formatInfo.Samples.Length} are required.");
        }

        int colorModel = dfd[12];
        if (colorModel != formatInfo.ColorModel)
        {
            throw new InvalidImageContentException(
                $"The KTX2 data format descriptor for {vkFormat} declares color model {colorModel} but "
                + $"{formatInfo.ColorModel} is required.");
        }

        byte transferFunction = dfd[14];
        if (formatInfo.IsSrgb && transferFunction != TransferSrgb)
        {
            throw new InvalidImageContentException(
                $"The KTX2 data format descriptor for the sRGB format {vkFormat} declares transfer function "
                + $"{transferFunction} but {TransferSrgb} (sRGB) is required.");
        }

        uint expectedDimensions = (uint)(formatInfo.BlockWidth - 1)
                                  | ((uint)(formatInfo.BlockHeight - 1) << 8)
                                  | ((uint)(formatInfo.BlockDepth - 1) << 16);
        uint dimensions = BinaryPrimitives.ReadUInt32LittleEndian(dfd[16..]);
        if (dimensions != expectedDimensions)
        {
            throw new InvalidImageContentException(
                $"The KTX2 data format descriptor for {vkFormat} declares texel block dimensions 0x{dimensions:X8} "
                + $"but 0x{expectedDimensions:X8} is required.");
        }

        uint bytesPlane = BinaryPrimitives.ReadUInt32LittleEndian(dfd[20..]);
        if ((bytesPlane & 0xFF) != formatInfo.BlockBytes
            || (bytesPlane & 0xFFFFFF00) != 0
            || BinaryPrimitives.ReadUInt32LittleEndian(dfd[24..]) != 0)
        {
            throw new InvalidImageContentException(
                $"The KTX2 data format descriptor for {vkFormat} declares bytesPlane 0x{bytesPlane:X8} but "
                + $"{formatInfo.BlockBytes} bytes in plane 0 and zero for every other plane is required.");
        }

        for (int i = 0; i < sampleCount; i++)
        {
            int offset = 4 + BlockHeaderBytes + (i * SampleBytes);
            uint word = BinaryPrimitives.ReadUInt32LittleEndian(dfd[offset..]);
            ushort bitOffset = (ushort)(word & 0xFFFF);
            int bitLength = (int)((word >> 16) & 0xFF) + 1;
            byte channelId = (byte)((word >> 24) & 0x0F);
            ushort bitLengthField = (ushort)((word >> 16) & 0xFF);

            Ktx2SampleInfo expected = formatInfo.Samples[i];
            if (bitOffset != expected.BitOffset
                || bitLengthField != expected.BitLength - 1
                || !IsChannelIdCompatible(formatInfo.ColorModel, channelId, expected.ChannelId))
            {
                throw new InvalidImageContentException(
                    $"The KTX2 data format descriptor for {vkFormat} declares sample {i} with channel {channelId}, "
                    + $"offset {bitOffset} and length {bitLength} bits but channel {expected.ChannelId}, offset "
                    + $"{expected.BitOffset} and length {expected.BitLength} bits are required.");
            }

            if (BinaryPrimitives.ReadUInt32LittleEndian(dfd[(offset + 4)..]) != 0)
            {
                throw new InvalidImageContentException(
                    $"The KTX2 data format descriptor for {vkFormat} declares non-zero sample positions for sample {i}.");
            }

            uint lower = BinaryPrimitives.ReadUInt32LittleEndian(dfd[(offset + 8)..]);
            uint upper = BinaryPrimitives.ReadUInt32LittleEndian(dfd[(offset + 12)..]);
            if (lower != expected.Lower || (upper != expected.Upper && upper != 0xFFFFFFFFU))
            {
                throw new InvalidImageContentException(
                    $"The KTX2 data format descriptor for {vkFormat} declares sample {i} bounds 0x{lower:X8}..0x{upper:X8} "
                    + $"but 0x{expected.Lower:X8}..0x{expected.Upper:X8} is required.");
            }
        }
    }

    private static bool IsChannelIdCompatible(int colorModel, byte actual, byte expected)
    {
        if (actual == expected)
        {
            return true;
        }

        // KHR_DF_MODEL_BC1A uses channel 0 for opaque BC1 and channel 1 for BC1A with punch-through alpha.
        // Both describe the same 64 bit block, so either is accepted for the alpha-present variants.
        return colorModel == 128 && expected == 1 && actual == 0;
    }
}
