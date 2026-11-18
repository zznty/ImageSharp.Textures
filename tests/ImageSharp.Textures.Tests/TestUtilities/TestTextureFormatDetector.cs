namespace ImageSharp.Textures.Tests.TestUtilities;

/// <summary>
///     A format detector that matches a fixed magic header.
/// </summary>
internal sealed class TestTextureFormatDetector : ITextureFormatDetector
{
    private readonly ITextureFormat format;
    private readonly byte[] magic;

    public TestTextureFormatDetector(ITextureFormat format, params byte[] magic)
    {
        this.format = format;
        this.magic = magic;
    }

    public ITextureFormat? DetectFormat(ReadOnlySpan<byte> header)
        => header.StartsWith(this.magic) ? this.format : null;
}
