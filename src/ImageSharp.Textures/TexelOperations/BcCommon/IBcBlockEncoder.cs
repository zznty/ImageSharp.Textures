namespace ImageSharp.Textures.Texels.BcCommon;

/// <summary>
///     Implemented by the structure that encodes a single block of a block-compressed texel format.
/// </summary>
/// <typeparam name="TSelf">The implementing type.</typeparam>
internal interface IBcBlockEncoder<TSelf>
    where TSelf : struct, IBcBlockEncoder<TSelf>
{
    /// <summary>
    ///     Gets the encoded size, in bytes, of one block.
    /// </summary>
    public static abstract int BlockBytes { get; }

    /// <summary>
    ///     Encodes one 4x4 block of RGBA32 texels.
    /// </summary>
    /// <param name="block">The source block: sixteen RGBA32 texels, four rows of four.</param>
    /// <param name="destination">The destination, <see cref="BlockBytes" /> bytes.</param>
    /// <param name="scratch">The per-encoder float scratch (<see cref="BcBlockEncoder.ScratchFloats" /> elements).</param>
    /// <param name="options">The encode settings.</param>
    public static abstract void EncodeBlock(
        ReadOnlySpan<byte> block,
        Span<byte> destination,
        Span<float> scratch,
        BcEncodeOptions options);
}
