using System.Buffers.Binary;

namespace ImageSharp.Textures.Formats.Dds;

/// <summary>
///     Detects DDS containers from their magic word.
/// </summary>
internal sealed class DdsFormatDetector : ITextureFormatDetector
{
    /// <inheritdoc />
    public ITextureFormat? DetectFormat(ReadOnlySpan<byte> header)
        => header.Length >= DdsConstants.MagicLength
           && BinaryPrimitives.ReadUInt32LittleEndian(header) == DdsConstants.MagicWord
            ? DdsFormat.Instance
            : null;
}
