namespace ImageSharp.Textures.Texels.BcCommon;

/// <summary>
///     Base class for the block-compressed texel operations.
/// </summary>
/// <typeparam name="TBlock">The block decoder of the texel format.</typeparam>
/// <remarks>
///     The operations are stateless: the texel type description and the block size are the only state and both
///     are immutable, so every format exposes a shared singleton. Formats that also encode derive from
///     <see cref="BcEncodingTexelOperations{TBlock, TEncoder}" /> instead.
/// </remarks>
internal abstract class BcTexelOperations<TBlock> : TexelOperations
    where TBlock : struct, IBcBlockDecoder<TBlock>
{
    private readonly int blockBytes;
    private readonly TexelTypeInfo texelTypeInfo;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BcTexelOperations{TBlock}" /> class.
    /// </summary>
    /// <param name="texelTypeInfo">The texel type description of the format.</param>
    /// <param name="formatName">The texel format name used in error messages.</param>
    protected BcTexelOperations(TexelTypeInfo texelTypeInfo, string formatName)
    {
        this.texelTypeInfo = texelTypeInfo;
        this.blockBytes = TBlock.BlockBytes;
        this.FormatName = formatName;
    }

    /// <summary>
    ///     Gets the texel format name used in error messages.
    /// </summary>
    protected string FormatName { get; }

    /// <inheritdoc />
    public override TexelTypeInfo GetTexelTypeInfo() => this.texelTypeInfo;

    /// <inheritdoc />
    public override int GetEncodedByteCount(int width, int height)
        => checked(((width + 3) >> 2) * ((height + 3) >> 2) * this.blockBytes);

    /// <inheritdoc />
    public override Image<TPixel> Decode<TPixel>(
        ReadOnlySpan<byte> source,
        Configuration configuration,
        int width,
        int height)
        => BcSurfaceDecoder.Decode<TPixel, TBlock>(source, configuration, width, height, this.FormatName);

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    ///     Always thrown: this format provides decoding only.
    /// </exception>
    public override void Encode<TPixel>(Image<TPixel> source, Span<byte> destination)
        => throw new NotSupportedException($"BC encoding is not available for {this.FormatName}.");
}
