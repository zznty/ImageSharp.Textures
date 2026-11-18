namespace ImageSharp.Textures.Formats.Ktx2;

/// <summary>
///     Describes one sample (channel) of a data format descriptor block.
/// </summary>
/// <param name="ChannelId">The channel identifier of the sample.</param>
/// <param name="BitOffset">The sample offset within the texel block, in bits.</param>
/// <param name="BitLength">The sample length, in bits.</param>
/// <param name="Lower">The value that maps to the sample's lower bound.</param>
/// <param name="Upper">The value that maps to the sample's upper bound.</param>
internal readonly record struct Ktx2SampleInfo(
    byte ChannelId,
    ushort BitOffset,
    byte BitLength,
    uint Lower,
    uint Upper);
