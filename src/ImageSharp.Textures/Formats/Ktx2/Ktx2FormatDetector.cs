namespace ImageSharp.Textures.Formats.Ktx2;

/// <summary>
///     Detects KTX2 containers from their file identifier.
/// </summary>
internal sealed class Ktx2FormatDetector : ITextureFormatDetector
{
    /// <inheritdoc />
    public ITextureFormat? DetectFormat(ReadOnlySpan<byte> header)
        => header.Length >= Ktx2Constants.IdentifierLength &&
           header[..Ktx2Constants.IdentifierLength].SequenceEqual(Ktx2Constants.Identifier)
            ? Ktx2Format.Instance
            : null;
}
