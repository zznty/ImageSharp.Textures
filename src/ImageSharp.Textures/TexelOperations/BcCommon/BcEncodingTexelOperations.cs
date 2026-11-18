namespace ImageSharp.Textures.Texels.BcCommon;

/// <summary>
///     Base class for block-compressed texel operations that can both decode and encode.
/// </summary>
/// <typeparam name="TBlock">The block decoder of the texel format.</typeparam>
/// <typeparam name="TEncoder">The block encoder of the texel format.</typeparam>
/// <remarks>
///     The public <see cref="TexelOperations.Encode{TPixel}" /> overload uses the default settings; container
///     encoders that carry their own settings use <see cref="EncodeWithOptions{TPixel}" /> through
///     <see cref="BcEncode" />.
/// </remarks>
internal abstract class BcEncodingTexelOperations<TBlock, TEncoder> : BcTexelOperations<TBlock>, IBcEncoder
    where TBlock : struct, IBcBlockDecoder<TBlock>
    where TEncoder : struct, IBcBlockEncoder<TEncoder>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="BcEncodingTexelOperations{TBlock, TEncoder}" /> class.
    /// </summary>
    /// <param name="texelTypeInfo">The texel type description of the format.</param>
    /// <param name="formatName">The texel format name used in error messages.</param>
    protected BcEncodingTexelOperations(TexelTypeInfo texelTypeInfo, string formatName)
        : base(texelTypeInfo, formatName)
    {
    }

    /// <inheritdoc />
    public void EncodeWithOptions<TPixel>(Image<TPixel> source, Span<byte> destination, BcEncodeOptions options)
        where TPixel : unmanaged, IPixel<TPixel>
        => BcSurfaceEncoder.Encode<TPixel, TEncoder>(source, destination, options, this.FormatName);

    /// <inheritdoc />
    public override void Encode<TPixel>(Image<TPixel> source, Span<byte> destination)
        => this.EncodeWithOptions(source, destination, BcEncodeOptions.Default);
}
