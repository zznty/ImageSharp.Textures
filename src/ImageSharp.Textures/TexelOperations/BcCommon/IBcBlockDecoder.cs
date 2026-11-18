namespace ImageSharp.Textures.Texels.BcCommon;

/// <summary>
///     Implemented by the structure that decodes a single block of a block-compressed texel format.
/// </summary>
/// <typeparam name="TSelf">The implementing type.</typeparam>
internal interface IBcBlockDecoder<TSelf>
    where TSelf : struct, IBcBlockDecoder<TSelf>
{
    /// <summary>
    ///     Gets the encoded size, in bytes, of one block.
    /// </summary>
    public static abstract int BlockBytes { get; }

    /// <summary>
    ///     Decodes one block into tightly packed 4x4 RGBA32 pixels.
    /// </summary>
    /// <param name="source">The encoded block.</param>
    /// <param name="block">The 64-byte destination, four rows of four RGBA texels.</param>
    /// <param name="scratch">Scratch space of at least 96 bytes.</param>
    public static abstract void DecodeBlock(ReadOnlySpan<byte> source, Span<byte> block, Span<byte> scratch);
}
