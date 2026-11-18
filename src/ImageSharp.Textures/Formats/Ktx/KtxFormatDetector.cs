namespace ImageSharp.Textures.Formats.Ktx;

/// <summary>
///     Detects KTX v1 containers from their file identifier.
/// </summary>
internal sealed class KtxFormatDetector : ITextureFormatDetector
{
    /// <inheritdoc />
    public ITextureFormat? DetectFormat(ReadOnlySpan<byte> header)
        => header.Length >= KtxConstants.IdentifierLength &&
           header[..KtxConstants.IdentifierLength].SequenceEqual(KtxConstants.Identifier)
            ? KtxFormat.Instance
            : null;
}
