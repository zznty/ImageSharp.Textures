namespace ImageSharp.Textures;

/// <summary>
///     Identifies the quality preset used when encoding BC7 texel data.
/// </summary>
public enum Bc7EncodingQuality
{
    /// <summary>
    ///     Fast encoding using a single partition mode. Lowest quality, highest throughput.
    /// </summary>
    Quick,

    /// <summary>
    ///     Full multi-mode encoding search. Higher quality, slower.
    /// </summary>
    Default
}
