using System.Buffers.Binary;
using System.Text;

namespace ImageSharp.Textures.Formats.Ktx;

/// <summary>
///     Parses and sizes the key/value data block shared by the KTX v1 and KTX2 containers.
/// </summary>
internal static class KtxKeyValueData
{
    /// <summary>
    ///     Parses a key/value data block.
    /// </summary>
    /// <param name="data">The raw key/value data block, including every size field and padding byte.</param>
    /// <param name="swapEndianness">Whether the block size fields are stored big-endian.</param>
    /// <returns>The parsed pairs, in file order.</returns>
    /// <exception cref="InvalidImageContentException">The block is malformed.</exception>
    internal static List<KtxKeyValuePair> Parse(ReadOnlySpan<byte> data, bool swapEndianness)
    {
        List<KtxKeyValuePair> pairs = [];
        int offset = 0;

        while (offset < data.Length)
        {
            if (data.Length - offset < 4)
            {
                throw new InvalidImageContentException(
                    $"The key/value data block ends inside a size field at offset {offset}.");
            }

            uint length = swapEndianness
                ? BinaryPrimitives.ReadUInt32BigEndian(data[offset..])
                : BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);

            offset += 4;

            if (length > (uint)(data.Length - offset))
            {
                throw new InvalidImageContentException(
                    $"The key/value data block declares {length} bytes at offset {offset - 4} but only "
                    + $"{data.Length - offset} bytes remain.");
            }

            ReadOnlySpan<byte> pair = data.Slice(offset, (int)length);
            offset += (int)length;

            int terminator = pair.IndexOf((byte)0);

            string key;
            byte[] value;
            if (terminator < 0)
            {
                throw new InvalidImageContentException(
                    "A key/value data entry is missing the NUL terminator that ends its key.");
            }

            key = Encoding.UTF8.GetString(pair[..terminator]);
            value = pair[(terminator + 1)..].ToArray();
            pairs.Add(new KtxKeyValuePair(key, value));

            int padding = (4 - (int)(length % 4)) % 4;
            if (padding > data.Length - offset)
            {
                throw new InvalidImageContentException(
                    "The key/value data block ends inside the padding that follows an entry.");
            }

            offset += padding;
        }

        return pairs;
    }
}
