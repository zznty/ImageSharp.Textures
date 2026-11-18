namespace ImageSharp.Textures.Texels.BcCommon;

/// <summary>
///     Per-encode settings shared by the block-compressed texel operations.
/// </summary>
/// <remarks>
///     The public <see cref="TexelOperations.Encode{TPixel}" /> signature is frozen and carries no options, so
///     container encoders hand these settings to the block encoders through the internal
///     <see cref="IBcEncoder" /> entry point.
/// </remarks>
internal readonly struct BcEncodeOptions
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="BcEncodeOptions" /> struct.
    /// </summary>
    /// <param name="bc1AlphaThreshold">The alpha value below which BC1 uses the punch-through mode.</param>
    /// <param name="bc7Quality">The BC7 quality preset.</param>
    internal BcEncodeOptions(float bc1AlphaThreshold, Bc7EncodingQuality bc7Quality)
    {
        this.Bc1AlphaThreshold = bc1AlphaThreshold;
        this.Bc7Quality = bc7Quality;
    }

    /// <summary>
    ///     Gets the default encode settings.
    /// </summary>
    internal static BcEncodeOptions Default => new(0.5f, Bc7EncodingQuality.Default);

    /// <summary>
    ///     Gets the alpha value, in the range 0 to 1, below which BC1 uses the three colour punch-through mode.
    /// </summary>
    internal float Bc1AlphaThreshold { get; }

    /// <summary>
    ///     Gets the quality preset used when encoding BC7.
    /// </summary>
    internal Bc7EncodingQuality Bc7Quality { get; }
}
