namespace ImageSharp.Textures;

/// <summary>
///     Detects texture container formats from their leading header bytes.
/// </summary>
public interface ITextureFormatDetector
{
    /// <summary>
    ///     Detects whether the header belongs to the format handled by the detector.
    /// </summary>
    /// <param name="header">The leading bytes of the encoded texture.</param>
    /// <returns>The detected format; otherwise, <see langword="null" />.</returns>
    public ITextureFormat? DetectFormat(ReadOnlySpan<byte> header);
}
